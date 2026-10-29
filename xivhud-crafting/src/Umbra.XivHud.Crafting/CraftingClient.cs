using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Crafting;

public enum CraftingLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, crafting commands, or input hooks are used.</summary>
[Service]
public sealed class CraftingClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public CraftingClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Crafting.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Crafting.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Crafting.OpenSettings");
        Refresh();
    }

    public CraftingLinkState State { get; private set; } = CraftingLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        CraftingLinkState.Ready => "Open your Crafting.",
        CraftingLinkState.Waiting => "Crafting is getting ready. Try again in a moment.",
        _ => "Crafting is unavailable. Enable XivHud · Crafting in the Dalamud plugin installer.",
    };

    [OnTick]
    private void Tick()
    {
        if (disposed || Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 1000;
        Refresh();
    }

    private void Refresh()
    {
        if (disposed) return;
        try
        {
            HasSettings = settings.HasAction;
            State = !toggle.HasAction || !ready.HasFunction ? CraftingLinkState.Missing
                : ready.InvokeFunc() ? CraftingLinkState.Ready : CraftingLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = CraftingLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != CraftingLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = CraftingLinkState.Missing;
            LogOnce(error); return false;
        }
    }

    public bool OpenSettings()
    {
        if (disposed) return false;
        Refresh();
        if (!HasSettings) return false;
        try { settings.InvokeAction(); return true; }
        catch (Exception error)
        {
            HasSettings = false;
            LogOnce(error); return false;
        }
    }

    private void LogOnce(Exception error)
    {
        var message = error.GetType().Name + ": " + error.Message;
        if (message == lastProblem) return;
        lastProblem = message;
        Logger.Warning("[Umbra.XivHud.Crafting] Crafting connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = CraftingLinkState.Missing; HasSettings = false;
    }
}
