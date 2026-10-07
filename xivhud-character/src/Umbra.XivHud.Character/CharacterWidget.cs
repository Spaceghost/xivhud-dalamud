using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Umbra.Common;
using Umbra.Widgets;
using Una.Drawing;

namespace Umbra.XivHud.Character;

[ToolbarWidget("XivHudCharacter", "XivHud · Character",
    "Open XivHud · Character from your toolbar. A record button with a customizable icon and label.",
    ["character", "xivhud", "gear", "jobs", "attributes", "record"])]
public sealed class CharacterWidget(WidgetInfo info, string? guid = null,
    Dictionary<string, object>? configValues = null) : StandardToolbarWidget(info, guid, configValues)
{
    private CharacterClient? client;
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.Text
        | StandardWidgetFeatures.Icon | StandardWidgetFeatures.CustomizableIcon;
    protected override string DefaultIconType => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.IdCard;

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables() =>
    [
        .. base.GetConfigVariables(),
        new StringWidgetConfigVariable("CharacterLabel", "Label", "Text on the Character button.", "Character", 32),
        new BooleanWidgetConfigVariable("DimUnavailable", "Dim when unavailable",
            "Grey the record icon while Character is unavailable or getting ready.", true),
        new BooleanWidgetConfigVariable("RightClickSettings", "Right-click opens Character settings",
            "Open Character settings when available. Ctrl+Shift+right-click still opens Umbra's widget settings.", true),
    ];

    protected override void OnLoad()
    {
        client = Framework.Service<CharacterClient>();
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
        var label = GetConfigValue<string>("CharacterLabel").Trim();
        SetText(string.IsNullOrEmpty(label) ? "Character" : label);
        if (client is null) { SetTooltip("Character is unavailable."); return; }
        // Umbra reapplies the user's chosen icon styling before OnDraw. Only
        // add the unavailable treatment; never erase a saved desaturation.
        if (client.State != CharacterLinkState.Ready && GetConfigValue<bool>("DimUnavailable"))
            SetIconDesaturated(true);
        var tip = client.Description;
        if (client.HasSettings && GetConfigValue<bool>("RightClickSettings"))
            tip += "\nRight-click: Character settings.";
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
