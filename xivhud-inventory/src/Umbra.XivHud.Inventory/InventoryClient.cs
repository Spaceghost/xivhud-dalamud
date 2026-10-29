using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Inventory;

public enum InventoryLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, inventory commands, or input hooks are used.</summary>
[Service]
public sealed class InventoryClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public InventoryClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Inventory.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Inventory.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Inventory.OpenSettings");
        Refresh();
    }

    public InventoryLinkState State { get; private set; } = InventoryLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        InventoryLinkState.Ready => "Open your Inventory.",
        InventoryLinkState.Waiting => "Inventory is getting ready. Try again in a moment.",
        _ => "Inventory is unavailable. Enable XivHud · Inventory in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? InventoryLinkState.Missing
                : ready.InvokeFunc() ? InventoryLinkState.Ready : InventoryLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = InventoryLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != InventoryLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = InventoryLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Inventory] Inventory connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = InventoryLinkState.Missing; HasSettings = false;
    }
}
