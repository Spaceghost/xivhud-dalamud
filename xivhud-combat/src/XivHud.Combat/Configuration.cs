using Dalamud.Configuration;

namespace XivHud.Combat;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool DarkPaper { get; set; }
    public bool Ornament { get; set; } = true;
    public bool Enabled { get; set; }
    public bool Cross { get; set; }
    public int FirstBar { get; set; } = 1;
    public int Rows { get; set; } = 2;
    public bool ClickActions { get; set; } = true;
    public bool LockLayout { get; set; } = true;
    public float Scale { get; set; } = 1;
    public float X { get; set; } = .5f;
    public float Y { get; set; } = .96f;
    public void Normalize()
    {
        FirstBar = Math.Clamp(FirstBar, 1, Cross ? 8 : 10); Rows = Math.Clamp(Rows, 1, Math.Min(3, (Cross ? 8 : 10) - FirstBar + 1));
        Scale = float.IsFinite(Scale) ? Math.Clamp(Scale, .65f, 2) : 1;
        X = float.IsFinite(X) ? Math.Clamp(X, 0, 1) : .5f; Y = float.IsFinite(Y) ? Math.Clamp(Y, 0, 1) : .96f;
    }
}
