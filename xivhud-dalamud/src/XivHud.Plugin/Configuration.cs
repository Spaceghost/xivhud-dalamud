using Dalamud.Configuration;

namespace XivHud.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool SwapConfirmCancel { get; set; }
}
