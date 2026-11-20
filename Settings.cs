using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuMotionX;

/// <summary>Everything about one player: device, server:port and the three device configs.</summary>
public class PlayerSettings
{
    public bool On { get; set; }
    public string Device { get; set; } = "Controller";       // Controller | Mouse | Keyboard
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 26760;
    public Config Controller { get; set; } = new Config { Kind = "Controller" };
    public Config Mouse { get; set; } = new Config { Kind = "Mouse" };
    public Config Keyboard { get; set; } = new Config { Kind = "Keyboard" };

    public Config For(string device) =>
        device == "Mouse" ? Mouse : (device == "Keyboard" ? Keyboard : Controller);

    [JsonIgnore]
    public Config Current => For(Device);

    public static int DefaultPort(int index) => 26760 + index;
}

/// <summary>The whole program state, saved next to the .exe.</summary>
public class AppSettings
{
    public const int PlayerCount = 8;
    public static readonly string[] Devices = { "Controller", "Mouse", "Keyboard" };

    public int Version { get; set; } = 1;
    public int SelectedPlayer { get; set; }
    public List<PlayerSettings> Players { get; set; } = new List<PlayerSettings>();

    public static AppSettings Current;

    static string ExeDir()
    {
        try
        {
            string p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p))
            {
                string d = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(d)) return d;
            }
        }
        catch { }
        return AppContext.BaseDirectory;
    }

    public static string FilePath => Path.Combine(ExeDir(), "Yuzu Motion X settings.json");
    static string AppDataPath => Path.Combine(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YuzuMotionX"), "settings.json");

    public static string LastSavedPath = "";

    /// <summary>
    /// No settings file -> every player starts with DEFAULT. Settings file found -> every device starts in
    /// the profile it was using when the program was last closed.
    /// </summary>
    public static AppSettings Load()
    {
        AppSettings a = null;
        foreach (string path in new[] { FilePath, AppDataPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                a = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (a != null) { LastSavedPath = path; break; }
            }
            catch { }
        }
        if (a == null)
        {
            a = new AppSettings();
            LastSavedPath = FilePath;
        }
        Normalize(a);
        return a;
    }

    static void Normalize(AppSettings a)
    {
        if (a.Players == null) a.Players = new List<PlayerSettings>();
        while (a.Players.Count < PlayerCount) a.Players.Add(null);
        if (a.Players.Count > PlayerCount) a.Players.RemoveRange(PlayerCount, a.Players.Count - PlayerCount);
        if (a.SelectedPlayer < 0 || a.SelectedPlayer >= PlayerCount) a.SelectedPlayer = 0;

        for (int i = 0; i < PlayerCount; i++)
        {
            bool fresh = a.Players[i] == null;
            PlayerSettings ps = a.Players[i] ?? new PlayerSettings();
            a.Players[i] = ps;
            if (fresh)
            {
                ps.On = i == 0;
                ps.Port = PlayerSettings.DefaultPort(i);
                ps.Controller.PadIndex = (i % 4) + 1;
            }
            if (string.IsNullOrWhiteSpace(ps.Host) || !System.Net.IPAddress.TryParse(ps.Host, out _)) ps.Host = "127.0.0.1";
            if (ps.Port < 1 || ps.Port > 65535) ps.Port = PlayerSettings.DefaultPort(i);
            if (Array.IndexOf(Devices, ps.Device) < 0) ps.Device = "Controller";

            foreach (string kind in Devices)
            {
                Config c = ps.For(kind);
                c.Kind = kind;
                if (c.Profiles == null) c.Profiles = new Dictionary<string, string>();
                if (c.CapturedQ == null) c.CapturedQ = Array.Empty<double>();
                if (c.Dots == null) c.Dots = new List<KeyDot>();
                if (c.PadIndex < 1 || c.PadIndex > 4) c.PadIndex = (i % 4) + 1;
                string start = c.ActiveProfile ?? "";
                if (!c.ApplyProfile(start)) c.ApplyProfile(Config.DefaultProfileName);
            }
        }
    }

    public bool Save()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        foreach (var ps in Players)
            foreach (string kind in Devices)
            {
                Config c = ps.For(kind);
                if (c.ActiveProfile == Config.DefaultProfileName) c.ApplyProfile(Config.DefaultProfileName);   // DEFAULT can never be saved changed
            }
        string json = JsonSerializer.Serialize(this, opts);
        foreach (string path in new[] { FilePath, AppDataPath })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
                LastSavedPath = path;
                return true;
            }
            catch { }
        }
        return false;
    }
}
