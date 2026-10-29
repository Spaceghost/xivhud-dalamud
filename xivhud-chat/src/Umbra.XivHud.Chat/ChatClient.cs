using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Chat;

public enum ChatLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class ChatClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public ChatClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Chat.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Chat.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Chat.OpenSettings");
        Refresh();
    }

    public ChatLinkState State { get; private set; } = ChatLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        ChatLinkState.Ready => "Open your Chat.",
        ChatLinkState.Waiting => "Chat is getting ready. Try again in a moment.",
        _ => "Chat is unavailable. Enable XivHud · Chat in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? ChatLinkState.Missing
                : ready.InvokeFunc() ? ChatLinkState.Ready : ChatLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = ChatLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != ChatLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = ChatLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Chat] Chat connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = ChatLinkState.Missing; HasSettings = false;
    }
}
