using System.Text.Json;
using Dalamud.Plugin.Ipc;
using Umbra.Common;
namespace Umbra.XivHud.Vitals;

public enum VitalsLinkState { Missing, Waiting, Ready }

/// <summary>The versioned endpoint also supports existing Vitals installations without toolbar aliases.</summary>
[Service]
public sealed class VitalsClient : IDisposable
{
    private readonly ICallGateSubscriber<string, string> call;
    private bool disposed, enabled;
    private long nextRefresh;
    public VitalsLinkState State { get; private set; }
    public bool HasSettings => State == VitalsLinkState.Ready;
    public string Description => State == VitalsLinkState.Ready ? "Toggle your Vitals. Right-click for settings." : "Enable XivHud · Vitals in Dalamud to use this button.";
    public VitalsClient() { call = Framework.DalamudPlugin.GetIpcSubscriber<string, string>("XivHud.Vitals.v1.Call"); Refresh(); }
    [OnTick] private void Tick() { if (!disposed && Environment.TickCount64 >= nextRefresh) { nextRefresh = Environment.TickCount64 + 1000; Refresh(); } }
    private void Refresh()
    {
        if (disposed) return;
        try
        {
            using var doc = JsonDocument.Parse(call.InvokeFunc("{\"method\":\"status\"}"));
            if (!doc.RootElement.GetProperty("ok").GetBoolean()) { State = VitalsLinkState.Missing; return; }
            var state = doc.RootElement.GetProperty("result").GetProperty("state");
            State = state.GetProperty("ready").GetBoolean() ? VitalsLinkState.Ready : VitalsLinkState.Waiting;
            enabled = state.GetProperty("enabled").GetBoolean();
        }
        catch { State = VitalsLinkState.Missing; }
    }
    public bool Toggle() { Refresh(); return Send(enabled ? "window.close" : "window.open"); }
    public bool OpenSettings() { Refresh(); return Send("window.settings"); }
    private bool Send(string method)
    {
        if (disposed || State != VitalsLinkState.Ready) return false;
        try
        {
            using var doc = JsonDocument.Parse(call.InvokeFunc(JsonSerializer.Serialize(new { method })));
            return doc.RootElement.GetProperty("ok").GetBoolean() && doc.RootElement.GetProperty("result").GetProperty("accepted").GetBoolean();
        }
        catch { State = VitalsLinkState.Missing; return false; }
    }
    public void Dispose() => disposed = true;
}
