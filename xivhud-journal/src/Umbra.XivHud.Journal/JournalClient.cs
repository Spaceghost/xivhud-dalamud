using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Journal;

public enum JournalLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class JournalClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public JournalClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Journal.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Journal.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Journal.OpenSettings");
        Refresh();
    }

    public JournalLinkState State { get; private set; } = JournalLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        JournalLinkState.Ready => "Open your Journal.",
        JournalLinkState.Waiting => "Journal is getting ready. Try again in a moment.",
        _ => "Journal is unavailable. Enable XivHud · Journal in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? JournalLinkState.Missing
                : ready.InvokeFunc() ? JournalLinkState.Ready : JournalLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = JournalLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != JournalLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = JournalLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Journal] Journal connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = JournalLinkState.Missing; HasSettings = false;
    }
}
