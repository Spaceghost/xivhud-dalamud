using Dalamud.Configuration;

namespace XivHud.Windows;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool ReduceMotion { get; set; }
}
