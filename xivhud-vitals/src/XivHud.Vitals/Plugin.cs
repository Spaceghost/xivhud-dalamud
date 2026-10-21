using System.Collections.Concurrent;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using XivHud.Protocol;
using XivHud.Vitals.Core;

namespace XivHud.Vitals;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/xivvitals";
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly VitalsFeed feed;
    private readonly VitalsWindow window = null!;
    private readonly ConcurrentQueue<string> requests = new();
    private readonly HudEndpoint ipc = new("vitals");
    private ICallGateProvider<string, string>? call;
    private IDtrBarEntry? toolbar;
    private bool disposed, commandRegistered, drawFailed;
    private long nextPublish;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework, IClientState client,
        ICondition condition, IPluginLog log, IObjectTable objects, ITargetManager targets, IPartyList party,
        IDataManager dataManager, IDtrBar dtr, IGameGui gameGui, ITextureProvider textures)
    {
        this.pi = pi; this.commands = commands; this.framework = framework; this.client = client;
        this.condition = condition; this.log = log;
        this.gameGui = gameGui;
        config = pi.GetPluginConfig() as Configuration ?? new(); config.Normalize();
        var data = new VitalsData(objects, targets, party, client, dataManager);
        feed = new(now => data.Read(now, config.ShowPlayerEffects, config.ShowTargetEffects));
        try
        {
            window = new(pi, config, Save, textures);
            call = pi.GetIpcProvider<string, string>("XivHud.Vitals.v1.Call");
            call.RegisterFunc(ipc.Call);
            commandRegistered = commands.AddHandler(Command, new CommandInfo(OnCommand)
            { HelpMessage = "Illustrated Vitals HUD. Options: open, close, settings, preview, lock, unlock, reset, effects [on/off/mine/all], status. Native HUD visibility is unchanged." });
            if (!commandRegistered) throw new InvalidOperationException("/xivvitals is already registered.");
            toolbar = dtr.Get("XivHud.Vitals");
            toolbar.Text = "Vitals"; toolbar.Tooltip = "Vitals HUD\nClick for appearance and layout. Right-click to show/hide the live HUD.";
            toolbar.OnClick = e => Enqueue(e.ClickType == MouseClickType.Right ? "toggle" : "settings");
            framework.Update += Update;
            client.Logout += Logout;
            pi.UiBuilder.Draw += Draw;
            pi.UiBuilder.OpenMainUi += Settings;
            pi.UiBuilder.OpenConfigUi += Settings;
            Publish();
        }
        catch { Dispose(); throw; }
    }

    private void Save() => pi.SavePluginConfig(config);
    private void Settings() => Enqueue("settings");
    private void Enqueue(string request) { if (!disposed && requests.Count < 32) requests.Enqueue(request); }
    private void OnCommand(string command, string arguments) => Enqueue(arguments.Trim().ToLowerInvariant());
    private void Logout(int type, int code) => feed.Reset();
    private bool CanRead => client.IsLoggedIn && !gameGui.GameUiHidden && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51]
        && !condition[ConditionFlag.WatchingCutscene] && !condition[ConditionFlag.WatchingCutscene78] && !condition[ConditionFlag.OccupiedInCutSceneEvent];

    private void Apply(string request)
    {
        switch (request)
        {
            case "open": case "on": config.Enabled = true; window.Preview = false; drawFailed = false; feed.Reset(); Save(); break;
            case "close": case "off": window.Close(); feed.Reset(); break;
            case "toggle": Apply(config.Enabled ? "close" : "open"); break;
            case "preview": window.Preview = !window.Preview; window.SettingsOpen = true; drawFailed = false; break;
            case "lock": config.Locked = true; Save(); break;
            case "unlock": config.Locked = false; window.SettingsOpen = true; Save(); break;
            case "reset": config.ResetLayout(); window.SettingsOpen = true; Save(); break;
            case "effects": case "effects on": config.ShowPlayerEffects = true; config.ShowTargetEffects = true; Save(); break;
            case "effects off": config.ShowPlayerEffects = false; config.ShowTargetEffects = false; Save(); break;
            case "effects mine": config.TargetEffectsMineOnly = true; Save(); break;
            case "effects all": config.TargetEffectsMineOnly = false; Save(); break;
            case "status": log.Information("XivHud.Vitals: enabled={Enabled}; preview={Preview}; locked={Locked}; data={Status}; panels={Panels}", config.Enabled, window.Preview, config.Locked, window.DataStatus, window.VisiblePanels); break;
            default: window.SettingsOpen = true; break;
        }
    }

    private string? ApplyIpc(HudCommand command)
    {
        switch (command.Method)
        {
            case "window.open": Apply("open"); break;
            case "window.close": Apply("close"); break;
            case "window.settings": Apply("settings"); break;
            default: return "unsupported_method";
        }
        return null;
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        ipc.Process(ApplyIpc, e => log.Error(e, "Vitals IPC operation failed."));
        for (var i = 0; i < 8 && requests.TryDequeue(out var request); i++) Apply(request);
        var now = Environment.TickCount64;
        var availability = !config.Enabled ? FeedAvailability.Disabled : !client.IsLoggedIn ? FeedAvailability.LoggedOut
            : !CanRead ? FeedAvailability.Hidden : FeedAvailability.Ready;
        if (feed.Update(now, availability) is { } error)
            log.Warning(error, "Vitals reading failed; discarded snapshot. Native HUD is unchanged.");
        window.DataStatus = feed.Status;
        Publish();
    }

    private void Draw()
    {
        if (disposed || drawFailed) return;
        try { window.Draw(feed.Snapshot, CanRead); }
        catch (Exception e)
        {
            drawFailed = true; feed.Reset();
            log.Error(e, "Vitals drawing stopped for this session; native HUD is unchanged. /xivvitals open or preview retries.");
        }
    }

    private void Publish()
    {
        if (disposed || Environment.TickCount64 < nextPublish) return;
        nextPublish = Environment.TickCount64 + 200;
        // Diagnostics deliberately exclude player/party names and live combat facts.
        // A future companion transport needs its own explicit data contract.
        ipc.Publish(new
        {
            ready = !drawFailed, loggedIn = client.IsLoggedIn, enabled = config.Enabled,
            dataReady = !drawFailed && CanRead && feed.Snapshot?.IsFresh(Environment.TickCount64) == true,
            dataStatus = window.DataStatus, preview = window.Preview, locked = config.Locked,
            visiblePanels = drawFailed || gameGui.GameUiHidden ? 0 : window.RecentPanels(Environment.TickCount64), settingsOpen = window.SettingsOpen,
            effects = new { playerEnabled = config.ShowPlayerEffects, targetEnabled = config.ShowTargetEffects, targetMineOnly = config.TargetEffectsMineOnly, capacity = StatusRibbon.Capacity },
            window = new { requestedMode = "screen", actualMode = "screen" },
            recoveryCommand = "/xivvitals close", nativeRecoveryCommand = "/xivhud off",
        });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        call?.UnregisterFunc(); ipc.Dispose();
        framework.Update -= Update; client.Logout -= Logout;
        pi.UiBuilder.Draw -= Draw; pi.UiBuilder.OpenMainUi -= Settings; pi.UiBuilder.OpenConfigUi -= Settings;
        if (commandRegistered) commands.RemoveHandler(Command);
        toolbar?.Remove(); window?.Dispose(); feed.Reset();
        requests.Clear();
    }
}
