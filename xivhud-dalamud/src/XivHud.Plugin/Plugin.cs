using System.Collections.Concurrent;
using XivHud.Protocol;
using Dalamud.Plugin.Ipc;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace XivHud.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/xivhud";
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly HudVisibility visibility;
    private readonly ControllerBroker controller = null!;
    private readonly ConcurrentQueue<int> requests = new();
    private readonly HudEndpoint ipc = new("hud");
    private ICallGateProvider<string, string>? call;
    private IDtrBarEntry? entry;
    private string lastEntry = "";
    private volatile bool open, disposed;
    private bool commandRegistered;
    private bool controllerFocused;
    private ulong focusGeneration;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework,
        IClientState client, IChatGui chat, IDtrBar dtr, IPluginLog log,
        IGameInteropProvider interop, IGamepadState gamepad, ICondition condition)
    {
        this.pi = pi; this.commands = commands; this.framework = framework;
        this.client = client; this.chat = chat; this.log = log;
        config = pi.GetPluginConfig() as Configuration ?? new();
        visibility = new(log);
        try
        {
            controller = new(pi, interop, gamepad, client, condition, log);
            call = pi.GetIpcProvider<string, string>("XivHud.v1.Call");
            call.RegisterFunc(ipc.Call);
            PublishStatus();
            commandRegistered = commands.AddHandler(Command, new CommandInfo(OnCommand)
                { HelpMessage = "Umbra HUD settings. /xivhud on hides the native HUD; off restores it; toggle switches; status reports the mode." });
            if (!commandRegistered) throw new InvalidOperationException("The /xivhud command is already registered.");
            entry = dtr.Get("XivHud");
            entry.OnClick = e => { if (e.ClickType == MouseClickType.Right) open = true; else requests.Enqueue(2); };
            pi.UiBuilder.DisableUserUiHide = true;
            pi.UiBuilder.Draw += Draw;
            pi.UiBuilder.OpenMainUi += Open;
            pi.UiBuilder.OpenConfigUi += Open;
            framework.Update += Update;
            client.Logout += OnLogout;
        }
        catch { Dispose(); throw; }
    }

    private void Open() => open = true;

    private void OnLogout(int type, int code) => visibility.Update(config.Enabled, false);

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "on": requests.Enqueue(1); break;
            case "off": case "restore": requests.Enqueue(0); break;
            case "toggle": requests.Enqueue(2); break;
            case "status": requests.Enqueue(3); break;
            default: Open(); break;
        }
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        ipc.Process(command =>
        {
            switch (command.Method)
            {
                case "hud.set_enabled": requests.Enqueue(command.Parameters.GetProperty("enabled").GetBoolean() ? 1 : 0); break;
                case "window.settings": open = true; break;
                case "window.close": open = false; ReleaseController(); break;
                default: return "unsupported_method";
            }
            return null;
        }, e => log.Error(e, "HUD IPC operation failed."));
        var changed = false; var report = false;
        while (requests.TryDequeue(out var request))
        {
            if (request == 3) { report = true; continue; }
            var enabled = request == 2 ? !config.Enabled : request == 1;
            changed |= enabled != config.Enabled;
            config.Enabled = enabled;
        }
        if (changed) pi.SavePluginConfig(config);
        visibility.Update(config.Enabled, client.IsLoggedIn);
        if (report)
        {
            chat.Print($"[XivHud] {visibility.Status}");
            log.Information("XivHud controller: {Status}", controller.Diagnostic);
        }
        var text = visibility.IsApplied ? "HUD: Umbra" : config.Enabled ? "HUD: waiting" : "HUD: native";
        if (entry is not null && text != lastEntry)
        {
            lastEntry = text;
            entry.Text = text;
            entry.Tooltip = (config.Enabled ? "Click to restore the game HUD." : "Click to hide the native HUD and keep Umbra.")
                + "\nRight-click for HUD settings.";
            entry.Shown = true;
        }
        PublishStatus();
    }

    private long nextIpcPublish;
    private void PublishStatus()
    {
        if (disposed || Environment.TickCount64 < nextIpcPublish) return;
        nextIpcPublish = Environment.TickCount64 + 200;
        ipc.Publish(new { ready = !disposed, loggedIn = client.IsLoggedIn, enabled = config.Enabled, applied = visibility.IsApplied, status = visibility.Status, settingsOpen = open, controller = controller?.Status, recoveryCommand = "/xivhud off" });
    }

    private void Draw()
    {
        if (!open || disposed) { ReleaseController(); return; }
        var shown = open;
        ImGui.SetNextWindowSize(new(390, 0), ImGuiCond.FirstUseEver);
        var drew = ImGui.Begin("Umbra HUD###XivHud", ref shown, ImGuiWindowFlags.AlwaysAutoResize);
        try
        {
            if (!drew) { ReleaseController(); return; }
            var focused = shown && client.IsLoggedIn && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)
                && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
            if (focused && !controllerFocused)
            {
                focusGeneration++; if (focusGeneration == 0) focusGeneration++;
                controllerFocused = true;
            }
            var ready = focused && controller.Renew("XivHud.Settings", focusGeneration);
            if (!focused || !ready) ReleaseController();
            var frame = ready ? controller.ReadSnapshot("XivHud.Settings", focusGeneration) : null;
            var confirm = config.SwapConfirmCancel ? GamepadButtonsFlags.Circle : GamepadButtonsFlags.Cross;
            var cancel = config.SwapConfirmCancel ? GamepadButtonsFlags.Cross : GamepadButtonsFlags.Circle;
            if (frame is { Phase: "Active" } input)
            {
                if ((input.Pressed & (uint)cancel) != 0) { shown = false; ReleaseController(); }
                else if ((input.Pressed & (uint)GamepadButtonsFlags.Square) != 0)
                {
                    config.SwapConfirmCancel = !config.SwapConfirmCancel;
                    pi.SavePluginConfig(config);
                }
                else if ((input.Pressed & (uint)confirm) != 0) requests.Enqueue(config.Enabled ? 0 : 1);
            }
            ImGui.TextUnformatted("A clearer view of the world");
            ImGui.Separator();
            ImGui.TextWrapped(visibility.Status);
            ImGui.Spacing();
            if (ImGui.Button(config.Enabled ? "Show game HUD" : "Hide native HUD")) requests.Enqueue(config.Enabled ? 0 : 1);
            if (frame is { Phase: "Active" })
                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin() - new System.Numerics.Vector2(2),
                    ImGui.GetItemRectMax() + new System.Numerics.Vector2(2), 0xFFDAB778, 3, ImDrawFlags.None, 2);
            ImGui.Spacing();
            ImGui.TextWrapped("Use the HUD button in Umbra's server-info widget to switch views. Your saved HUD layout is preserved.");
            ImGui.TextWrapped("Native HUD, chat, nameplates and action bars are hidden together. Open game windows and other plugins keep their own controls.");
            ImGui.TextUnformatted("Restore anytime: /xivhud off");
            ImGui.Separator();
            var swapped = config.SwapConfirmCancel;
            if (ImGui.Checkbox("Swap confirm and back", ref swapped))
            { config.SwapConfirmCancel = swapped; pi.SavePluginConfig(config); }
            ImGui.TextWrapped(config.SwapConfirmCancel
                ? "Circle: change HUD · Cross: close · Square: swap confirm/back"
                : "Cross: change HUD · Circle: close · Square: swap confirm/back");
            if (focused && !ready) ImGui.TextWrapped(controller.Status);
            else if (frame is { Phase: "WaitingForNeutral" }) ImGui.TextWrapped("Release the buttons and center the sticks to use this window.");
        }
        finally
        {
            ImGui.End();
            open = shown;
            if (!open) ReleaseController();
        }
    }

    private void ReleaseController()
    {
        if (!controllerFocused) return;
        controllerFocused = false;
        controller?.Release("XivHud.Settings", focusGeneration);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        call?.UnregisterFunc(); ipc.Dispose();
        framework.Update -= Update;
        client.Logout -= OnLogout;
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Open;
        pi.UiBuilder.OpenConfigUi -= Open;
        if (commandRegistered) commands.RemoveHandler(Command);
        entry?.Remove(); entry = null;
        ReleaseController();
        controller?.Dispose();
        if (framework.IsInFrameworkUpdateThread) visibility.Dispose();
        else framework.RunOnFrameworkThread(visibility.Dispose).GetAwaiter().GetResult();
        log.Information("XivHud unloaded; native HUD visibility lease released.");
    }
}
