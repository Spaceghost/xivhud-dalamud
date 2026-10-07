using Dalamud.Plugin.Ipc;
using Umbra.Common;

namespace Umbra.XivHud.Character;

public enum CharacterLinkState { Missing, Waiting, Ready }

/// <summary>Optional IPC client. Provider reloads are checked at every action;
/// no plugin assembly references, chat commands, or input hooks are used.</summary>
[Service]
public sealed class CharacterClient : IDisposable
{
    private readonly ICallGateSubscriber<object> toggle;
    private readonly ICallGateSubscriber<bool> ready;
    private readonly ICallGateSubscriber<object> settings;
    private long nextRefresh;
    private string? lastProblem;
    private bool disposed;

    public CharacterClient()
    {
        var pi = Framework.DalamudPlugin;
        toggle = pi.GetIpcSubscriber<object>("XivHud.Character.Toggle");
        ready = pi.GetIpcSubscriber<bool>("XivHud.Character.Ready");
        settings = pi.GetIpcSubscriber<object>("XivHud.Character.OpenSettings");
        Refresh();
    }

    public CharacterLinkState State { get; private set; } = CharacterLinkState.Missing;
    public bool HasSettings { get; private set; }
    public string Description => State switch
    {
        CharacterLinkState.Ready => "Open your Character record.",
        CharacterLinkState.Waiting => "Character is getting ready. Try again in a moment.",
        _ => "Character is unavailable. Enable XivHud · Character in the Dalamud plugin installer.",
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
            State = !toggle.HasAction || !ready.HasFunction ? CharacterLinkState.Missing
                : ready.InvokeFunc() ? CharacterLinkState.Ready : CharacterLinkState.Waiting;
            lastProblem = null;
        }
        catch (Exception error)
        {
            State = CharacterLinkState.Missing; HasSettings = false;
            LogOnce(error);
        }
    }

    public bool Toggle()
    {
        if (disposed) return false;
        Refresh();
        if (State != CharacterLinkState.Ready) return false;
        try { toggle.InvokeAction(); return true; }
        catch (Exception error)
        {
            State = CharacterLinkState.Missing;
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
        Logger.Warning("[Umbra.XivHud.Character] Character connection unavailable: " + message);
    }

    public void Dispose()
    {
        disposed = true;
        State = CharacterLinkState.Missing; HasSettings = false;
    }
}
