using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Combat;

public enum CombatLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class CombatClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public CombatClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Combat.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Combat.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Combat.OpenSettings");
        Refresh();
    }

    public CombatLinkState State { get; private set; } = CombatLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        CombatLinkState.Ready => "Open your Combat.",
        CombatLinkState.Waiting => "Combat is getting ready. Try again in a moment.",
        _ => "Combat is unavailable. Enable XivHud · Combat in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? CombatLinkState.Missing
                : ready.InvokeFunc() ? CombatLinkState.Ready : CombatLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = CombatLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != CombatLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = CombatLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Combat] Combat connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = CombatLinkState.Missing; HasSettings = false;
    }
}
