using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuMotionX;

/// <summary>One point of Keyboard Motion: a spot on the circle that a key pushes the virtual stick towards.</summary>
public class KeyDot
{
    public string Key { get; set; } = "Up";
    public double X { get; set; }                       // -1 .. 1  (left .. right)
    public double Y { get; set; }                       // -1 .. 1  (down .. up)
    public string Behavior { get; set; } = "Hold";      // Hold | Toggle | Pulse
    public double Strength { get; set; } = 100;         // % of the way to the point
    public double ReachMs { get; set; } = 150;          // time to reach the point
    public double ReleaseMs { get; set; } = 150;        // time to come back
    public double MaxHoldMs { get; set; } = 0;          // Hold: auto-release after this long (0 = never). Pulse: pulse length
    public bool Ease { get; set; } = true;              // smooth start/stop

    public KeyDot Clone() => (KeyDot)MemberwiseClone();
}

/// <summary>
/// The settings of ONE device of ONE player (Controller, Mouse or Keyboard).
/// Profile settings = everything copied by CopyMotion. Device settings (buttons/keys) are not part of profiles.
/// </summary>
public class Config
{
    public string Kind { get; set; } = "Controller";        // Controller | Mouse | Keyboard

    // ----- profile settings (the property defaults are the tuned DEFAULT of the controller) -----
    public string ControllerType { get; set; } = "Joy-Con (right)";
    public string Mode { get; set; } = "tilt";               // tilt | gyro | shake | swing
    public string Source { get; set; } = "Right stick";
    public string XTarget { get; set; } = "Auto";
    public double MouseSensitivity { get; set; } = 1.0;
    public double Sensitivity { get; set; } = 1.2;
    public double Speed { get; set; } = 1.5;
    public double Deadzone { get; set; } = 0.06;
    public double Curve { get; set; } = 1.0;
    public double SmoothingMs { get; set; } = 25;
    public double RangeLimit { get; set; } = 90;
    public bool TriggerShift { get; set; } = true;
    public bool ReturnToNeutral { get; set; } = true;
    public double ReturnSpeed { get; set; } = 1.0;
    public double ShakeStrength { get; set; } = 2.0;
    public double ShakeHz { get; set; } = 6.0;
    public string ShakeDir { get; set; } = "Stick direction";
    public string HoldStyle { get; set; } = "Normal";
    public string NeutralPreset { get; set; } = "Vertical - top down";
    public double NeutralPitch { get; set; } = -90;
    public double NeutralYaw { get; set; } = 0;
    public double NeutralRoll { get; set; } = 0;
    public double[] CapturedQ { get; set; } = Array.Empty<double>();
    public bool PitchOn { get; set; } = true;
    public bool YawOn { get; set; } = true;
    public bool RollOn { get; set; } = true;
    public bool PitchInv { get; set; } = false;
    public bool YawInv { get; set; } = false;
    public bool RollInv { get; set; } = false;
    public bool PitchFlip { get; set; } = false;
    public bool YawFlip { get; set; } = false;
    public bool RollFlip { get; set; } = false;
    public string AccelMode { get; set; } = "follow";
    public bool InvertGravity { get; set; } = false;
    public List<KeyDot> Dots { get; set; } = new List<KeyDot>();   // Keyboard Motion points

    // ----- device settings (NOT part of profiles) -----
    public int PadIndex { get; set; } = 1;                   // Controller: XInput pad 1-4
    public string DevicePath { get; set; } = "";             // Mouse / Keyboard: which physical device ("" = any)
    public string DeviceName { get; set; } = "";             // its name, shown while it is unplugged
    public string RecenterButton { get; set; } = "Off";      // Controller
    public string ShakeButton { get; set; } = "Off";         // Controller
    public string ActivationMode { get; set; } = "Always on"; // Mouse: Always on | Toggle | Hold
    public string ActivationKey { get; set; } = "F8";
    public string RecenterKey { get; set; } = "F9";          // Mouse / Keyboard
    public string ShakeKey { get; set; } = "Off";            // Mouse / Keyboard
    public bool LockCursor { get; set; } = false;

    // ----- profiles -----
    public string ActiveProfile { get; set; } = "DEFAULT";
    public Dictionary<string, string> Profiles { get; set; } = new Dictionary<string, string>();

    // ------------------------------------------------------------------ constants
    public const string DefaultProfileName = "DEFAULT";
    public static readonly string[] ControllerTypes = { "Joy-Con (left)", "Joy-Con (right)", "Pro Controller" };
    public static readonly string[] Sources = { "Either stick", "Left stick", "Right stick" };
    public static readonly string[] XTargets = { "Auto", "Lean (roll)", "Turn (yaw)" };
    public static readonly string[] HoldStyles = { "Normal", "Sideways (left)", "Sideways (right)", "Upside down" };
    public static readonly string[] ShakeDirs = { "Stick direction", "Left-right", "Forward-back", "Up-down" };
    public static readonly string[] ActivationModes = { "Always on", "Toggle", "Hold" };
    public static readonly string[] Behaviors = { "Hold", "Toggle", "Pulse" };

    public static bool IsPro(string type) => type == "Pro Controller";
    public static bool IsDefault(string name) => name == DefaultProfileName;

    public static readonly (string Name, double Pitch, double Yaw, double Roll, bool JoyConOnly)[] Presets =
    {
        ("Flat (controller on a table)", 0, 0, 0, false),
        ("Vertical - top up (Mario Party)", 90, 0, 0, true),
        ("Vertical - top down", -90, 0, 0, true),
        ("Held forward (30 deg up)", 30, 0, 0, false),
        ("Tilted forward 45 deg", 45, 0, 0, false),
        ("Rolled right 45 deg", 0, 0, 45, false),
        ("Rolled left 45 deg", 0, 0, -45, false),
    };

    public static string NormMode(string m)
    {
        if (m == "rate") return "gyro";
        if (m == "tilt" || m == "gyro" || m == "shake" || m == "swing") return m;
        return "tilt";
    }

    public static string SourceKey(string s)
    {
        s = (s ?? "").ToLowerInvariant();
        if (s.Contains("left")) return "left";
        if (s.Contains("right")) return "right";
        return "either";
    }

    public static string XTargetKey(string s)
    {
        s = (s ?? "").ToLowerInvariant();
        if (s.Contains("yaw")) return "yaw";
        if (s.Contains("roll")) return "roll";
        return "auto";
    }

    // ------------------------------------------------------------------ keyboard point sets
    static KeyDot Dot(string key, double x, double y, double reach = 150, double release = 150) =>
        new KeyDot { Key = key, X = x, Y = y, ReachMs = reach, ReleaseMs = release };

    static List<KeyDot> FourKeys(string up, string down, string left, string right, double reach = 150, double release = 150) =>
        new List<KeyDot>
        {
            Dot(up, 0, 1, reach, release), Dot(down, 0, -1, reach, release),
            Dot(left, -1, 0, reach, release), Dot(right, 1, 0, reach, release),
        };

    static List<KeyDot> NumpadEight() => new List<KeyDot>
    {
        Dot("NumPad8", 0, 1), Dot("NumPad2", 0, -1), Dot("NumPad4", -1, 0), Dot("NumPad6", 1, 0),
        Dot("NumPad7", -0.7, 0.7), Dot("NumPad9", 0.7, 0.7), Dot("NumPad1", -0.7, -0.7), Dot("NumPad3", 0.7, -0.7),
    };

    // ------------------------------------------------------------------ built-in profiles
    static Config Baseline(string kind) => new Config
    {
        Kind = kind,
        ControllerType = "Joy-Con (left)",
        Mode = "tilt",
        MouseSensitivity = 1.0,
        Sensitivity = 1.0,
        Speed = 1.0,
        Deadzone = kind == "Controller" ? 0.10 : (kind == "Mouse" ? 0.02 : 0.0),
        SmoothingMs = 60,
        NeutralPreset = "Flat (controller on a table)",
        NeutralPitch = 0,
        NeutralYaw = 0,
        NeutralRoll = 0,
        Dots = kind == "Keyboard" ? FourKeys("Up", "Down", "Left", "Right") : new List<KeyDot>(),
    };

    static Config B(string kind, Action<Config> modify)
    {
        Config c = Baseline(kind);
        modify(c);
        return c;
    }

    static readonly (string Name, Func<Config> Make)[] ControllerBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Controller" }),
        ("*Mario Party - vertical tilt (top up)",
            () => B("Controller", c => { c.ControllerType = "Joy-Con (right)"; c.NeutralPreset = "Vertical - top up (Mario Party)";
                                         c.NeutralPitch = 90; c.Sensitivity = 1.2; c.Speed = 1.5; c.SmoothingMs = 25; c.Deadzone = 0.06; })),
        ("*Mario Party - tilt (Joy-Con sideways)",
            () => B("Controller", c => { c.HoldStyle = "Sideways (left)"; c.Sensitivity = 1.6; c.Speed = 1.5; c.SmoothingMs = 25;
                                         c.Deadzone = 0.06; c.RangeLimit = 120; })),
        ("*Mario Party - shake",
            () => B("Controller", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "shake"; c.ShakeStrength = 3.5;
                                         c.ShakeHz = 7; c.SmoothingMs = 15; c.Deadzone = 0.08; })),
        ("*Joy-Con - tilt (flat)", () => B("Controller", c => { })),
        ("*Joy-Con - gyro aiming (stays put)",
            () => B("Controller", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "gyro"; c.SmoothingMs = 30;
                                         c.RangeLimit = 180; c.ReturnToNeutral = false; })),
        ("*Swing / flick games",
            () => B("Controller", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "swing"; c.ShakeStrength = 3; c.Speed = 1.5;
                                         c.SmoothingMs = 10; c.Deadzone = 0.08; c.RangeLimit = 120; })),
        ("*Pro Controller - tilt",
            () => B("Controller", c => { c.ControllerType = "Pro Controller"; c.NeutralPreset = "Held forward (30 deg up)"; c.NeutralPitch = 30; })),
        ("*Pro Controller - gyro aiming (returns to neutral)",
            () => B("Controller", c => { c.ControllerType = "Pro Controller"; c.Mode = "gyro"; c.SmoothingMs = 30; c.RangeLimit = 180;
                                         c.NeutralPreset = "Held forward (30 deg up)"; c.NeutralPitch = 30; })),
        ("*Precision - slow and steady",
            () => B("Controller", c => { c.Sensitivity = 0.6; c.Speed = 0.7; c.SmoothingMs = 160; c.Curve = 1.8; c.Deadzone = 0.15; })),
    };

    static readonly (string Name, Func<Config> Make)[] MouseBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Mouse", Deadzone = 0.02 }),
        ("*Mario Party - vertical tilt (top up)",
            () => B("Mouse", c => { c.ControllerType = "Joy-Con (right)"; c.NeutralPreset = "Vertical - top up (Mario Party)";
                                    c.NeutralPitch = 90; c.Sensitivity = 1.2; c.Speed = 1.5; c.SmoothingMs = 25; })),
        ("*Tilt - holds its angle (mouse stays)",
            () => B("Mouse", c => { c.ControllerType = "Joy-Con (right)"; c.ReturnToNeutral = false; })),
        ("*Gyro - mouse aiming",
            () => B("Mouse", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "gyro"; c.SmoothingMs = 15; c.RangeLimit = 180;
                                    c.ReturnToNeutral = false; })),
        ("*Shake - move the mouse fast",
            () => B("Mouse", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "shake"; c.ShakeStrength = 3.5; c.ShakeHz = 7;
                                    c.SmoothingMs = 15; })),
        ("*Swing / flick games",
            () => B("Mouse", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "swing"; c.ShakeStrength = 3; c.Speed = 1.5;
                                    c.SmoothingMs = 10; c.RangeLimit = 120; })),
        ("*Pro Controller - tilt",
            () => B("Mouse", c => { c.ControllerType = "Pro Controller"; c.NeutralPreset = "Held forward (30 deg up)"; c.NeutralPitch = 30; })),
        ("*Precision - slow and steady",
            () => B("Mouse", c => { c.Sensitivity = 0.6; c.Speed = 0.7; c.SmoothingMs = 160; c.Curve = 1.8; c.MouseSensitivity = 0.7; })),
    };

    static readonly (string Name, Func<Config> Make)[] KeyboardBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Keyboard", Deadzone = 0.0, Dots = FourKeys("Up", "Down", "Left", "Right") }),
        ("*Arrow keys - slow and smooth",
            () => B("Keyboard", c => { c.ControllerType = "Joy-Con (right)"; c.NeutralPreset = "Vertical - top down"; c.NeutralPitch = -90;
                                       c.Sensitivity = 1.2; c.Speed = 1.5; c.SmoothingMs = 25;
                                       c.Dots = FourKeys("Up", "Down", "Left", "Right", 400, 300); })),
        ("*WASD tilt",
            () => B("Keyboard", c => { c.ControllerType = "Joy-Con (right)"; c.NeutralPreset = "Vertical - top down"; c.NeutralPitch = -90;
                                       c.Sensitivity = 1.2; c.Speed = 1.5; c.SmoothingMs = 25;
                                       c.Dots = FourKeys("W", "S", "A", "D"); })),
        ("*Numpad - 8 directions",
            () => B("Keyboard", c => { c.ControllerType = "Joy-Con (right)"; c.NeutralPreset = "Vertical - top down"; c.NeutralPitch = -90;
                                       c.Sensitivity = 1.2; c.Speed = 1.5; c.SmoothingMs = 25; c.Dots = NumpadEight(); })),
        ("*Arrow keys - gyro (hold to rotate)",
            () => B("Keyboard", c => { c.ControllerType = "Joy-Con (right)"; c.Mode = "gyro"; c.SmoothingMs = 20; c.RangeLimit = 180;
                                       c.Dots = FourKeys("Up", "Down", "Left", "Right", 0, 0); })),
        ("*Pro Controller - arrow keys tilt",
            () => B("Keyboard", c => { c.ControllerType = "Pro Controller"; c.NeutralPreset = "Held forward (30 deg up)"; c.NeutralPitch = 30; })),
    };

    [JsonIgnore]
    public (string Name, Func<Config> Make)[] BuiltinList =>
        Kind == "Mouse" ? MouseBuiltins : (Kind == "Keyboard" ? KeyboardBuiltins : ControllerBuiltins);

    [JsonIgnore]
    public string DefaultObservation =>
        Kind == "Controller"
            ? "The DEFAULT profile was made thinking of Super Mario Party minigames (tested on magnifying glass and " +
              "sizzling steak). It is locked (can not be edited or deleted, and \"Reset to DEFAULT\" always brings it back)."
            : (Kind == "Mouse"
                ? "The DEFAULT profile is the starting point for mouse motion. It uses the settings tuned for the Super Mario Party " +
                  "minigames (vertical Joy-Con, tilt). It is locked (can not be edited or deleted, and \"Reset to DEFAULT\" always brings it back)."
                : "EXPERIMENTAL. The DEFAULT profile is the starting point for Keyboard Motion: four arrow-key points that tilt the tuned " +
                  "Super Mario Party vertical Joy-Con. It is locked (can not be edited or deleted, and \"Reset to DEFAULT\" always brings it back).");

    [JsonIgnore]
    public string DefaultObservationGood =>
        Kind == "Controller"
            ? "It was tested and working flawless on those minigames!"
            : (Kind == "Mouse"
                ? "Mouse feel depends on your mouse: press \"Save new profile from it\" and tune the sliders for your setup."
                : "Click inside the circle to add points and give each one a key. Press \"Save new profile from it\" to make your own set.");

    public bool IsBuiltin(string name)
    {
        foreach (var b in BuiltinList) if (b.Name == name) return true;
        return false;
    }

    public bool IsPremade(string name) => name != DefaultProfileName && IsBuiltin(name);

    /// <summary>A name typed by the user: no leading "*" (reserved for built-in profiles), never "DEFAULT".</summary>
    public static string CleanProfileName(string input)
    {
        string n = (input ?? "").Trim().TrimStart('*').Trim();
        if (n.Length > 60) n = n.Substring(0, 60).Trim();
        if (n.Length == 0) return "";
        if (string.Equals(n, DefaultProfileName, StringComparison.OrdinalIgnoreCase)) n += " (mine)";
        return n;
    }

    // ------------------------------------------------------------------ profiles
    static readonly HashSet<string> Keep = new HashSet<string>
    {
        "Kind", "Profiles", "ActiveProfile",
        "PadIndex", "DevicePath", "DeviceName", "RecenterButton", "ShakeButton",
        "ActivationMode", "ActivationKey", "RecenterKey", "ShakeKey", "LockCursor",
    };

    /// <summary>Copies every profile setting (not device settings) between configs.</summary>
    public static void CopyMotion(Config from, Config to)
    {
        foreach (var p in typeof(Config).GetProperties())
        {
            if (!p.CanRead || !p.CanWrite || Keep.Contains(p.Name)) continue;
            object v = p.GetValue(from);
            if (v is double[] arr) v = arr.ToArray();
            else if (v is List<KeyDot> dots) v = dots.Select(d => d.Clone()).ToList();
            p.SetValue(to, v);
        }
    }

    public string ProfileJson()
    {
        var c = new Config();
        CopyMotion(this, c);
        return JsonSerializer.Serialize(c);
    }

    /// <summary>The settings a profile stands for, as normalised JSON ("" if unknown).</summary>
    public string ProfileSnapshot(string name)
    {
        foreach (var b in BuiltinList)
            if (b.Name == name) return b.Make().ProfileJson();
        if (Profiles.TryGetValue(name, out string json))
        {
            try
            {
                var t = JsonSerializer.Deserialize<Config>(json);
                if (t != null)
                {
                    var c = new Config();
                    CopyMotion(t, c);
                    return JsonSerializer.Serialize(c);
                }
            }
            catch { }
        }
        return "";
    }

    public bool ApplyProfile(string name)
    {
        foreach (var b in BuiltinList)
        {
            if (b.Name == name)
            {
                CopyMotion(b.Make(), this);
                ActiveProfile = name;
                return true;
            }
        }
        if (Profiles.TryGetValue(name ?? "", out string json))
        {
            try
            {
                var t = JsonSerializer.Deserialize<Config>(json);
                if (t != null)
                {
                    CopyMotion(t, this);
                    ActiveProfile = name;
                    return true;
                }
            }
            catch { }
        }
        return false;
    }
}
