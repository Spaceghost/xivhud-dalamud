using System.Numerics;
using Dalamud.Bindings.ImGui;
using XivHud.Chat.Core;
using static XivHud.Style.IllustratedStyle;

namespace XivHud.Chat;

internal sealed class ChatWindow(Configuration config, ChatStore history, NotificationStore notices, Action save, Action<ChatChannel, string, string> send)
{
    internal bool Open, SettingsOpen, Preview;
    internal volatile bool Sending;
    internal volatile string? SentDraft;
    internal string Status = "", Draft = "", Recipient = "";
    private string search = "";
    private ChatChannel channel = ChatChannel.All, compose = ChatChannel.Say;
    private long seenRevision = -1;
    private bool follow = true, showNotifications;
    private IReadOnlyList<ChatEntry> cached = [];
    private string cachedSearch = "";
    private ChatChannel cachedChannel;

    internal void Draw(bool allowed, bool canSend)
    {
        if (!allowed && !Preview) return;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, V(Ink));
        ImGui.PushStyleColor(ImGuiCol.Border, V(Gold));
        ImGui.PushStyleColor(ImGuiCol.Text, V(Ivory));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, V(Leather));
        ImGui.PushStyleColor(ImGuiCol.Button, V(Leather));
        try
        {
            if (Open) Conversation(canSend);
            if (Preview || config.Enabled && config.Notifications) Notifications();
            if (SettingsOpen) Settings();
        }
        finally { ImGui.PopStyleColor(5); }
    }
    private void Conversation(bool canSend)
    {
        if (SentDraft is { } sent) { if (Draft == sent) Draft = ""; SentDraft = null; }
        ImGui.SetNextWindowSize(new(760, 500), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(480, 340), new(float.MaxValue));
        var shown = Open;
        var drew = ImGui.Begin(Preview ? "Conversations · SAMPLE###XivHud.Chat" : "Conversations###XivHud.Chat", ref shown);
        try
        {
            if (!drew) return;
            ImGui.TextColored(new Vector4(.9f, .8f, .55f, 1), "CONVERSATIONS");
            ImGui.SameLine(); if (ImGui.Button(showNotifications ? "Conversations" : "Notifications")) showNotifications = !showNotifications;
            if (showNotifications) { NotificationInbox(); return; }
            if (ImGui.BeginCombo("Channel", ChannelLabel(channel)))
            {
                foreach (var item in Enum.GetValues<ChatChannel>())
                {
                    var unread = Preview ? 0 : history.Unread(item);
                    if (ImGui.Selectable(ChannelLabel(item) + (unread > 0 ? $" ({unread})" : ""), item == channel)) { channel = item; follow = true; }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine(); ImGui.BeginDisabled(Preview); if (ImGui.Button("Mark read")) { var last = history.Snapshot(channel).LastOrDefault()?.Id ?? 0; history.MarkRead(channel, last); } ImGui.EndDisabled();
            ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##search", "Search this conversation…", ref search, 160);
            if (seenRevision != history.Revision || cachedChannel != channel || cachedSearch != search)
            { cached = history.Snapshot(channel, search); cachedChannel = channel; cachedSearch = search; seenRevision = history.Revision; }
            IReadOnlyList<ChatEntry> rows = Preview ? Samples() : cached;
            if (ImGui.BeginChild("##messages", new Vector2(0, -125), false))
            {
                // Respect wheel and scrollbar movement before requesting any follow scroll.
                if (ImGui.IsWindowHovered() && (ImGui.GetIO().MouseWheel != 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left)))
                    follow = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 8;
                ImGui.PushTextWrapPos(0);
                foreach (var item in rows)
                {
                    ImGui.PushID(item.Id.ToString());
                    ImGui.TextColored(ChannelColour(item.Channel), $"{item.Timestamp.ToLocalTime():HH:mm}  [{ChannelLabel(item.Channel)}] {item.Sender}");
                    ImGui.TextWrapped(item.Text);
                    if (ImGui.BeginPopupContextItem("##message-menu"))
                    {
                        if (ImGui.MenuItem("Copy message")) ImGui.SetClipboardText(item.Text);
                        ImGui.EndPopup();
                    }
                    ImGui.Spacing(); ImGui.PopID();
                }
                ImGui.PopTextWrapPos();
                if (rows.Count == 0) ImGui.TextDisabled("Messages appear here while chat capture is enabled. Native history remains in game.");
                if (follow && search.Length == 0) ImGui.SetScrollHereY(1);
                follow = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 8;
                if (follow && search.Length == 0 && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !Preview)
                    history.MarkRead(channel, rows.LastOrDefault()?.Id ?? 0);
            }
            ImGui.EndChild();
            ImGui.Separator();
            ImGui.SetNextItemWidth(160);
            if (ImGui.BeginCombo("##compose-channel", ChannelLabel(compose)))
            {
                foreach (var item in new[] { ChatChannel.Say, ChatChannel.Party, ChatChannel.Alliance, ChatChannel.FreeCompany, ChatChannel.Tell })
                    if (ImGui.Selectable(ChannelLabel(item), item == compose)) compose = item;
                ImGui.EndCombo();
            }
            if (compose == ChatChannel.Tell)
            { ImGui.SameLine(); ImGui.SetNextItemWidth(-1); var recipient = Preview ? "Sample Friend@World" : Recipient; ImGui.InputTextWithHint("##recipient", "Firstname Lastname@World", ref recipient, 100); if (!Preview) Recipient = recipient; }
            ImGui.BeginDisabled(!canSend || Preview || !config.Enabled);
            ImGui.SetNextItemWidth(-80);
            var draft = Preview ? "" : Draft;
            var enter = ImGui.InputTextWithHint("##draft", Preview ? "Sending is disabled in sample preview" : "Write a message…", ref draft, 2000, ImGuiInputTextFlags.EnterReturnsTrue);
            if (!Preview) Draft = draft;
            ImGui.SameLine(); var clicked = ImGui.Button("Send", new(70, 0));
            if ((enter || clicked) && !Preview && canSend && config.Enabled) send(compose, Draft, Recipient);
            ImGui.EndDisabled();
            ImGui.TextDisabled("Session-only history · native chat and rich links remain available");
            if (Status.Length > 0) ImGui.TextWrapped(Status);
        }
        finally { ImGui.End(); Open = shown; }
    }
    private void NotificationInbox()
    {
        ImGui.TextUnformatted("Notification center");
        ImGui.BeginDisabled(Preview); if (ImGui.Button("Clear notifications")) notices.Clear(); ImGui.EndDisabled();
        if (ImGui.BeginChild("##notice-history", new Vector2(0, 0), false))
        {
            var items = Preview ? new Notice[] { new(-1, "SAMPLE", "Quest update", "A sample notification stays in the center after its card expires.", NotificationKind.Quest, Environment.TickCount64) } : notices.History();
            foreach (var item in items)
            {
                ImGui.PushID(item.Id.ToString());
                ImGui.TextColored(item.Kind == NotificationKind.Error ? new Vector4(1, .5f, .4f, 1) : new Vector4(.65f, .8f, 1, 1), item.Title);
                ImGui.TextDisabled(item.Source + (item.Count > 1 ? " ×" + item.Count : "")); ImGui.TextWrapped(item.Text);
                if (item.Channel is { } target && ImGui.Button("Open conversation")) { channel = target; showNotifications = false; follow = true; }
                ImGui.Separator(); ImGui.PopID();
            }
            if (items.Count == 0) ImGui.TextDisabled("No notifications in this session");
        }
        ImGui.EndChild();
    }

    private void Notifications()
    {
        var viewport = ImGui.GetMainViewport(); var offset = 0f;
        foreach (var notice in Preview ? new Notice[] { new(-1, "SAMPLE", "A sample tell", "Click a notification to return to its conversation.", NotificationKind.Message, 0, Channel: ChatChannel.Tell) } : notices.Active(Environment.TickCount64))
        {
            ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(viewport.WorkSize.X - 370, 70 + offset));
            ImGui.SetNextWindowSize(new(350, 0));
            var shown = true;
            var flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
            var drew = ImGui.Begin(notice.Title.Replace("##", "＃＃", StringComparison.Ordinal) + "###XivHud.Chat.Notice." + notice.Id, ref shown, flags);
            try
            {
                if (drew)
                {
                    ImGui.TextColored(notice.Kind == NotificationKind.Error ? new Vector4(1, .5f, .4f, 1) : new Vector4(.65f, .8f, 1, 1), notice.Source + (notice.Count > 1 ? " ×" + notice.Count : ""));
                    ImGui.TextWrapped(notice.Text);
                    if (notice.Channel is { } target && ImGui.Button("Open conversation")) { channel = target; Open = true; follow = true; notices.Dismiss(notice.Id); }
                    offset += ImGui.GetWindowSize().Y + 8;
                }
            }
            finally { ImGui.End(); }
            if (!shown) notices.Dismiss(notice.Id);
        }
    }
    private void Settings()
    {
        var shown = SettingsOpen;
        if (ImGui.Begin("Chat and notifications###XivHud.Chat.Settings", ref shown, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var enabled = config.Enabled; if (ImGui.Checkbox("Capture new chat in this session", ref enabled)) { config.Enabled = enabled; if (!enabled) { history.Clear(); notices.Clear(); } save(); }
            var notifications = config.Notifications; if (ImGui.Checkbox("Notification cards", ref notifications)) { config.Notifications = notifications; save(); }
            var tells = config.NotifyTells; if (ImGui.Checkbox("Incoming tells", ref tells)) { config.NotifyTells = tells; save(); }
            var errors = config.NotifyErrors; if (ImGui.Checkbox("Game errors", ref errors)) { config.NotifyErrors = errors; save(); }
            var quests = config.NotifyQuests; if (ImGui.Checkbox("Quest updates", ref quests)) { config.NotifyQuests = quests; save(); }
            var normal = config.NotifyNormal; if (ImGui.Checkbox("Other game toasts", ref normal)) { config.NotifyNormal = normal; save(); }
            if (ImGui.Button("Open conversations")) Open = true;
            ImGui.SameLine(); if (ImGui.Button("Clear chat and notifications")) { history.Clear(); notices.Clear(); }
            ImGui.TextWrapped("Messages are kept only in memory and cleared on logout. The native chat log and native toasts are unchanged. Sending requires explicit input in the composer.");
        }
        ImGui.End(); SettingsOpen = shown;
    }
    private static string ChannelLabel(ChatChannel value) => value == ChatChannel.FreeCompany ? "Free Company" : value.ToString();
    private static Vector4 ChannelColour(ChatChannel value) => value switch
    { ChatChannel.Tell => new(1, .65f, .85f, 1), ChatChannel.Party or ChatChannel.Alliance => new(.6f, .8f, 1, 1), ChatChannel.FreeCompany => new(.65f, .9f, .7f, 1), ChatChannel.System => new(.8f, .75f, .6f, 1), _ => new(.85f, .84f, .79f, 1) };
    private static IReadOnlyList<ChatEntry> Samples() => [new(1, DateTimeOffset.Now, ChatChannel.Party, "A sample companion", "Ready when you are. This is labelled preview data.", false), new(2, DateTimeOffset.Now, ChatChannel.Tell, "A sample friend", "Your conversations and notifications share one interface.", true)];
}
