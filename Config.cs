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
    public string NeutralPreset { get; set; } = "VERTICAL UPSIDE DOWN FRONT";   // label of the position (numbers below decide)
    public string PositionBase { get; set; } = "VERTICAL UPSIDE DOWN FRONT";   // chosen base position (or "Custom")
    public string LeanMode { get; set; } = "None";                              // None | Forward | Backward
    public double LeanAngle { get; set; } = 45;                                 // degrees of lean
    public bool MoveForward { get; set; } = false;                              // Move style: stick up/down = forward/backward
    /// <summary>
    /// Axis override dots: 16 numbers = 8 dots (x, y each) for Right, Up-Right, Up, Up-Left, Left, Down-Left, Down, Down-Right.
    /// Empty = no override (the input goes through unchanged). Pushing a direction makes the output go to that dot.
    /// </summary>
    public double[] AxisOverride { get; set; } = Array.Empty<double>();
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

    /// <summary>
    /// The base positions of the POSITION box. World: x right, y up, z toward the player (the screen is at -z).
    /// "TOP" = the stick end of a Joy-Con / the trigger edge of a Pro Controller.
    /// FRONT = the face (stick/buttons) points at the screen, BACK = the face points at the player.
    /// The first 8 are the straight positions, the last 4 are the flat ones.
    /// </summary>
    public static readonly (string Name, double Pitch, double Yaw, double Roll)[] Positions =
    {
        ("VERTICAL FRONT", 90, 0, 180),
        ("VERTICAL BACK", 90, 0, 0),
        ("VERTICAL UPSIDE DOWN FRONT", -90, 0, 0),          // = the tuned DEFAULT position
        ("VERTICAL UPSIDE DOWN BACK", -90, 0, 180),
        ("HORIZONTAL FRONT", 0, 90, -90),
        ("HORIZONTAL BACK", 0, 90, 90),
        ("HORIZONTAL INVERTED FRONT", 0, -90, 90),
        ("HORIZONTAL INVERTED BACK", 0, -90, -90),
        ("FLAT FACING HORIZONTALLY UP", 0, 90, 0),
        ("FLAT FACING HORIZONTALLY DOWN", 0, 90, 180),
        ("FLAT FACING VERTICALLY UP", 0, 0, 0),
        ("FLAT FACING VERTICALLY DOWN", 0, 0, 180),
    };

    public static bool TryPosition(string name, out double pitch, out double yaw, out double roll)
    {
        foreach (var p in Positions)
        {
            if (p.Name == name)
            {
                pitch = p.Pitch; yaw = p.Yaw; roll = p.Roll;
                return true;
            }
        }
        pitch = 0; yaw = 0; roll = 0;
        return false;
    }

    /// <summary>The base position this config is in right now, or "Custom".</summary>
    public string CurrentBase()
    {
        if (NeutralPreset == "Captured")
            return LeanMode != "None" && PositionBase != "Custom" ? PositionBase : "Custom";
        foreach (var p in Positions)
        {
            if (Math.Abs(p.Pitch - NeutralPitch) < 0.5 && Math.Abs(p.Yaw - NeutralYaw) < 0.5 && Math.Abs(p.Roll - NeutralRoll) < 0.5)
                return p.Name;
        }
        return "Custom";
    }

    public string CurrentLean() => NeutralPreset == "Captured" && LeanMode != "None" && PositionBase != "Custom" ? LeanMode : "None";

    /// <summary>Puts the controller in a base position, optionally leaning forward/backward (45 degrees by default).</summary>
    public void ApplyPosition(string name, string lean, double angle)
    {
        if (!TryPosition(name, out double p, out double y, out double r)) return;
        PositionBase = name;
        LeanMode = lean;
        LeanAngle = angle;
        NeutralPitch = p;
        NeutralYaw = y;
        NeutralRoll = r;
        if (lean == "Forward" || lean == "Backward")
        {
            // leaning forward = the top moves towards the screen = a turn about the world X axis
            double a = M.Rad(lean == "Forward" ? -angle : angle);
            Q q = (Q.FromAxisAngle(1, 0, 0, a) * Q.Euler(p, y, r)).Normalized();
            CapturedQ = new[] { q.W, q.X, q.Y, q.Z };
            NeutralPreset = "Captured";
        }
        else
        {
            NeutralPreset = name;
        }
    }

    public static string NormMode(string m)
    {
        if (m == "rate") return "gyro";
        if (m == "tilt" || m == "gyro" || m == "shake" || m == "swing" || m == "move") return m;
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

    // ------------------------------------------------------------------ keyboard points
    static KeyDot Dot(string key, double x, double y, double reach = 150, double release = 150) =>
        new KeyDot { Key = key, X = x, Y = y, ReachMs = reach, ReleaseMs = release };

    static List<KeyDot> FourKeys(string up, string down, string left, string right, double reach = 150, double release = 150) =>
        new List<KeyDot>
        {
            Dot(up, 0, 1, reach, release), Dot(down, 0, -1, reach, release),
            Dot(left, -1, 0, reach, release), Dot(right, 1, 0, reach, release),
        };

    // ------------------------------------------------------------------ built-in profiles
    // Only the locked DEFAULT exists. (The property defaults above ARE the DEFAULT.)
    static readonly (string Name, Func<Config> Make)[] ControllerBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Controller" }),
    };

    static readonly (string Name, Func<Config> Make)[] MouseBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Mouse", Deadzone = 0.02 }),
    };

    static readonly (string Name, Func<Config> Make)[] KeyboardBuiltins =
    {
        (DefaultProfileName, () => new Config { Kind = "Keyboard", Deadzone = 0.0, Dots = FourKeys("Up", "Down", "Left", "Right") }),
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
