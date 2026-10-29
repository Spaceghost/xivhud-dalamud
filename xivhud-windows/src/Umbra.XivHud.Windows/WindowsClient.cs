using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Windows;

public enum WindowsLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, windows commands, or input hooks are used.</summary>
[Service]
public sealed class WindowsClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public WindowsClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Windows.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Windows.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Windows.OpenSettings");
        Refresh();
    }

    public WindowsLinkState State { get; private set; } = WindowsLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        WindowsLinkState.Ready => "Open your Windows.",
        WindowsLinkState.Waiting => "Windows is getting ready. Try again in a moment.",
        _ => "Windows is unavailable. Enable XivHud · Windows in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? WindowsLinkState.Missing
                : ready.InvokeFunc() ? WindowsLinkState.Ready : WindowsLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = WindowsLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != WindowsLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = WindowsLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Windows] Windows connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = WindowsLinkState.Missing; HasSettings = false;
    }
}
