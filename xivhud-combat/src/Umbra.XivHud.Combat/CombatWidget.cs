using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Umbra.Common;
using Umbra.Widgets;
using Una.Drawing;

namespace Umbra.XivHud.Combat;

[ToolbarWidget("XivHudCombat", "XivHud · Combat",
    "Open XivHud · Combat from your toolbar. A toolbar button with a customizable icon and label.",
    ["combat", "xivhud", "interface", "umbra"])]
public sealed class CombatWidget(WidgetInfo info, string? guid = null,
    Dictionary<string, object>? configValues = null) : StandardToolbarWidget(info, guid, configValues)
{
    private CombatClient? client;
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.Text
        | StandardWidgetFeatures.Icon | StandardWidgetFeatures.CustomizableIcon;
    protected override string DefaultIconType => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Bolt;

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables() =>
    [
        .. base.GetConfigVariables(),
        new StringWidgetConfigVariable("CombatLabel", "Label", "Text on the Combat button.", "Combat", 32),
        new BooleanWidgetConfigVariable("DimUnavailable", "Dim when unavailable",
            "Grey the toolbar icon while Combat is unavailable or getting ready.", true),
        new BooleanWidgetConfigVariable("RightClickSettings", "Right-click opens Combat settings",
            "Open Combat settings when available. Ctrl+Shift+right-click still opens Umbra's widget settings.", true),
    ];

    protected override void OnLoad()
    {
        client = Framework.Service<CombatClient>();
        Node.OnClick += OnClick;
        Node.OnRightClick += OnRightClick;
    }

    protected override void OnUnload()
    {
        Node.OnClick -= OnClick;
        Node.OnRightClick -= OnRightClick;
        client = null;
    }

    protected override void OnDraw()
    {
        var label = GetConfigValue<string>("CombatLabel").Trim();
        SetText(string.IsNullOrEmpty(label) ? "Combat" : label);
        if (client is null) { SetTooltip("Combat is unavailable."); return; }
        // Umbra reapplies the user's chosen icon styling before OnDraw. Only
        // add the unavailable treatment; never erase a saved desaturation.
        if (client.State != CombatLinkState.Ready && GetConfigValue<bool>("DimUnavailable"))
            SetIconDesaturated(true);
        var tip = client.Description;
        if (client.HasSettings && GetConfigValue<bool>("RightClickSettings"))
            tip += "\nRight-click: Combat settings.";
        SetTooltip(tip);
    }

    private static bool IsUmbraShortcut()
    {
        var io = ImGui.GetIO();
        return io.KeyCtrl && io.KeyShift;
    }

    private void OnClick(Node _)
    {
        if (!IsUmbraShortcut()) client?.Toggle();
    }

    private void OnRightClick(Node _)
    {
        if (!IsUmbraShortcut() && GetConfigValue<bool>("RightClickSettings")) client?.OpenSettings();
    }
}
