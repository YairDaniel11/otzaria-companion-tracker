using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OtzariaCompanionGuard;

public sealed class AppEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Args { get; set; } = "";
    public string WorkingDir { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class Config
{
    public string TargetProcess { get; set; } = "otzaria";
    public bool Autostart { get; set; } = false;
    public bool ShowTray { get; set; } = true;
    public string Theme { get; set; } = "system";          // light | dark | system
    public bool LeaveRunning { get; set; } = true;          // leave companions that were already running
    public int GracefulTimeoutSeconds { get; set; } = 5;
    public int MaxRestarts { get; set; } = 3;
    public bool Learning { get; set; } = true;
    public List<AppEntry> Apps { get; set; } = new();
}

public sealed class ProgramInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Reason { get; set; } = "";
    public double MemoryMb { get; set; }
}

public sealed class LearnedEntry
{
    public string Path { get; set; } = "";
    public int Count { get; set; }
    public string LastSeen { get; set; } = "";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(List<ProgramInfo>))]
[JsonSerializable(typeof(List<LearnedEntry>))]
internal partial class AppJson : JsonSerializerContext { }

internal static class Store
{
    public static readonly string Dir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OtzariaCompanionTracker");

    public static string ConfigPath => System.IO.Path.Combine(Dir, "config.json");
    static string LearnedPath => System.IO.Path.Combine(Dir, "learned.json");

    public static bool Exists => File.Exists(ConfigPath);

    public static DateTime Stamp()
    {
        try { return File.GetLastWriteTimeUtc(ConfigPath); } catch { return DateTime.MinValue; }
    }

    public static Config? TryLoad()
    {
        try { return JsonSerializer.Deserialize(File.ReadAllText(ConfigPath), AppJson.Default.Config); }
        catch { return null; }
    }

    public static Config Load() => TryLoad() ?? new Config();

    public static void Save(Config c)
    {
        Directory.CreateDirectory(Dir);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, AppJson.Default.Config), new UTF8Encoding(false));
        File.Move(tmp, ConfigPath, true);
    }

    public static List<LearnedEntry> LoadLearned()
    {
        try { return JsonSerializer.Deserialize(File.ReadAllText(LearnedPath), AppJson.Default.ListLearnedEntry) ?? new(); }
        catch { return new(); }
    }

    public static void SaveLearned(List<LearnedEntry> l)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(LearnedPath, JsonSerializer.Serialize(l, AppJson.Default.ListLearnedEntry), new UTF8Encoding(false));
        }
        catch { }
    }
}
