using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Vitals;

public enum VitalsLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, vitals commands, or input hooks are used.</summary>
[Service]
public sealed class VitalsClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public VitalsClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Vitals.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Vitals.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Vitals.OpenSettings");
        Refresh();
    }

    public VitalsLinkState State { get; private set; } = VitalsLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        VitalsLinkState.Ready => "Open your Vitals.",
        VitalsLinkState.Waiting => "Vitals is getting ready. Try again in a moment.",
        _ => "Vitals is unavailable. Enable XivHud · Vitals in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? VitalsLinkState.Missing
                : ready.InvokeFunc() ? VitalsLinkState.Ready : VitalsLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = VitalsLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != VitalsLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = VitalsLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Vitals] Vitals connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = VitalsLinkState.Missing; HasSettings = false;
    }
}
