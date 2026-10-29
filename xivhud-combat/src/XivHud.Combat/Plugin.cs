using System.Collections.Concurrent;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using XivHud.Combat.Core;
using XivHud.Protocol;
using XivHud.Style;

namespace XivHud.Combat;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IGameGui game;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly IllustratedChrome chrome;
    private readonly CombatData data;
    private readonly CombatWindow window;
    private readonly ConcurrentQueue<string> ui = new();
    private readonly ConcurrentQueue<SlotIdentity> actions = new();
    private readonly HudEndpoint endpoint = new("combat");
    private ICallGateProvider<string, string>? call;
    private ICallGateProvider<object>? toggle, openSettings;
    private ICallGateProvider<bool>? ready;
    private CombatSnapshot? snapshot;
    private long nextRead, nextPublish;
    private bool disposed, registered, readFailed, drawFailed;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework, IClientState client,
        ICondition condition, IGameGui game, IPluginLog log, IObjectTable objects, ITargetManager targets,
        IPartyList party, IDataManager sheets, IJobGauges gauges, ITextureProvider textures)
    {
        this.pi = pi; this.commands = commands; this.framework = framework; this.client = client;
        this.condition = condition; this.game = game; this.log = log;
        config = pi.GetPluginConfig() as Configuration ?? new(); config.Normalize();
        data = new(objects, targets, party, client, sheets, gauges);
        chrome = new(pi);
        window = new(chrome, config, Save, textures, token => { if (!disposed && actions.Count < 16) actions.Enqueue(token); });
        try
        {
            call = pi.GetIpcProvider<string, string>("XivHud.Combat.v1.Call"); call.RegisterFunc(endpoint.Call);
            toggle = pi.GetIpcProvider<object>("XivHud.Combat.Toggle"); toggle.RegisterAction(() => Enqueue("toggle"));
            openSettings = pi.GetIpcProvider<object>("XivHud.Combat.OpenSettings"); openSettings.RegisterAction(Settings);
            ready = pi.GetIpcProvider<bool>("XivHud.Combat.Ready"); ready.RegisterFunc(() => !disposed);
            registered = commands.AddHandler("/xivcombat", new CommandInfo((_, text) => Enqueue(text.Trim().ToLowerInvariant()))
                { HelpMessage = "Combat HUD: open, close, settings, preview. Mirrors saved hotbars; native bindings are unchanged." });
            if (!registered) throw new InvalidOperationException("/xivcombat is already registered");
            framework.Update += Update; pi.UiBuilder.Draw += Draw; client.Logout += Logout;
            pi.UiBuilder.OpenMainUi += Open; pi.UiBuilder.OpenConfigUi += Settings;
        }
        catch { Dispose(); throw; }
    }
    private bool Allowed => client.IsLoggedIn && !game.GameUiHidden && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51]
        && !condition[ConditionFlag.WatchingCutscene] && !condition[ConditionFlag.WatchingCutscene78] && !condition[ConditionFlag.OccupiedInCutSceneEvent];
    private void Save() => pi.SavePluginConfig(config);
    private void Enqueue(string command) { if (!disposed && ui.Count < 32) ui.Enqueue(command); }
    private void Open() => Enqueue("open");
    private void Settings() => Enqueue("settings");
    private void Logout(int a, int b) { snapshot = null; actions.Clear(); }
    private void Apply(string command)
    {
        switch (command)
        {
            case "toggle": Apply(config.Enabled ? "close" : "open"); break;
            case "open": config.Enabled = true; window.Preview = false; drawFailed = false; nextRead = 0; Save(); break;
            case "close": config.Enabled = false; window.Preview = window.SettingsOpen = false; snapshot = null; actions.Clear(); Save(); break;
            case "preview": window.Preview = !window.Preview; window.SettingsOpen = true; actions.Clear(); drawFailed = false; break;
            default: window.SettingsOpen = true; break;
        }
    }
    private void Update(IFramework _)
    {
        if (disposed) return;
        endpoint.Process(c => { if (c.Method == "window.layout") { window.SetScreenLayout(ScreenLayout.Read(c.Parameters)); return null; } Apply(c.Method switch { "window.open" => "open", "window.close" => "close", _ => "settings" }); return null; }, e => log.Error(e, "Combat UI request failed"));
        for (var i = 0; i < 8 && ui.TryDequeue(out var command); i++) Apply(command);
        var now = Environment.TickCount64;
        if (!Allowed || !config.Enabled || drawFailed) { snapshot = null; actions.Clear(); }
        else if (now >= nextRead)
        {
            nextRead = now + 100;
            try { snapshot = data.Read(now, config); readFailed = false; window.Status = snapshot is null ? "Waiting for player/hotbars" : "Live"; }
            catch (Exception e) { snapshot = null; nextRead = now + 1000; window.Status = "Combat data unavailable · native HUD remains available"; if (!readFailed) log.Warning(e, "Combat HUD read failed"); readFailed = true; }
        }
        if (actions.TryDequeue(out var token))
        {
            try { window.ActionStatus = data.Execute(token, now, config.Enabled && config.ClickActions && Allowed && !drawFailed, window.Preview); }
            catch (Exception e) { window.ActionStatus = "Native hotbar input unavailable"; log.Warning(e, "Manual hotbar input failed"); }
        }
        if (now >= nextPublish)
        {
            nextPublish = now + 200;
            endpoint.Publish(new { ready = !drawFailed, loggedIn = client.IsLoggedIn, enabled = config.Enabled,
                dataReady = Allowed && snapshot?.IsFresh(now) == true, status = window.Status, preview = window.Preview,
                slots = snapshot?.Slots.Count ?? 0, settingsOpen = window.SettingsOpen,
                window = new { layout = window.Layout, open = config.Enabled || window.Preview, actualMode = "screen", requestedMode = "screen" }, recoveryCommand = "/xivcombat close" });
        }
    }
    private void Draw()
    {
        if (disposed || drawFailed) return;
        try { window.Draw(snapshot, Allowed); }
        catch (Exception e) { snapshot = null; actions.Clear(); drawFailed = true; log.Error(e, "Combat HUD draw stopped; native UI unchanged"); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        framework.Update -= Update; pi.UiBuilder.Draw -= Draw; client.Logout -= Logout;
        pi.UiBuilder.OpenMainUi -= Open; pi.UiBuilder.OpenConfigUi -= Settings;
        toggle?.UnregisterAction(); openSettings?.UnregisterAction(); ready?.UnregisterFunc();
        chrome.Dispose(); call?.UnregisterFunc(); endpoint.Dispose(); if (registered) commands.RemoveHandler("/xivcombat");
        snapshot = null; ui.Clear(); actions.Clear();
    }
}
