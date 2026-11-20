using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace YuzuMotionX;

// ====================================================================== controller (XInput)
static class XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GamepadData
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StateData
    {
        public uint PacketNumber;
        public GamepadData Gamepad;
    }

    /// <summary>XINPUT_CAPABILITIES plus the vendor/product ids that xinput1_4.dll reports through ordinal 108.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct CapsEx
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public GamepadData Gamepad;
        public ushort VibLeft;
        public ushort VibRight;
        public ushort VendorId;
        public ushort ProductId;
        public ushort Version;
        public ushort Unknown1;
        public uint Unknown2;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern int XInputGetState(int userIndex, out StateData state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    static extern int XInputGetCapabilitiesNative(int userIndex, int flags, out CapsEx caps);

    [DllImport("xinput1_4.dll", EntryPoint = "#108")]
    static extern int XInputGetCapabilitiesEx(int one, int userIndex, int flags, out CapsEx caps);

    public static bool TryGet(int index, out StateData state)
    {
        try
        {
            return XInputGetState(index, out state) == 0;
        }
        catch (Exception)
        {
            state = default;
            return false;
        }
    }

    /// <summary>Vendor / product id of the pad in this slot (so its real name can be looked up).</summary>
    public static bool TryGetIds(int index, out int vendor, out int product)
    {
        vendor = 0; product = 0;
        try
        {
            if (XInputGetCapabilitiesEx(1, index, 0, out CapsEx caps) == 0 && caps.VendorId != 0)
            {
                vendor = caps.VendorId;
                product = caps.ProductId;
                return true;
            }
        }
        catch (Exception) { }
        return false;
    }

    /// <summary>Generic name from the XInput capabilities (used when the real product name can't be found).</summary>
    public static string TypeName(int index)
    {
        try
        {
            if (XInputGetCapabilitiesNative(index, 0, out CapsEx caps) == 0)
            {
                string t;
                switch (caps.SubType)
                {
                    case 2: t = "Racing wheel"; break;
                    case 3: t = "Arcade stick"; break;
                    case 4: t = "Flight stick"; break;
                    case 5: t = "Dance pad"; break;
                    case 6: t = "Guitar"; break;
                    case 8: t = "Drum kit"; break;
                    case 19: t = "Arcade pad"; break;
                    default: t = "Xbox-style Gamepad"; break;
                }
                return (caps.Flags & 0x0002) != 0 ? t + " (wireless)" : t;
            }
        }
        catch (Exception) { }
        return "XInput controller";
    }
}

public struct PadState
{
    public double LX, LY, RX, RY, LT, RT;
    public bool RecenterPressed;
    public bool ShakeHeld;
    public double DPadX, DPadY;
}

/// <summary>Reads one XInput pad (1-4) - each player with a Controller device has its own reader.</summary>
public class GamepadReader
{
    public static readonly string[] ButtonNames =
        { "Off", "R3", "L3", "Back", "Start", "LB", "RB", "A", "B", "X", "Y" };

    static readonly Dictionary<string, ushort> Masks = new Dictionary<string, ushort>
    {
        { "R3", 0x0080 }, { "L3", 0x0040 }, { "Back", 0x0020 }, { "Start", 0x0010 },
        { "LB", 0x0100 }, { "RB", 0x0200 },
        { "A", 0x1000 }, { "B", 0x2000 }, { "X", 0x4000 }, { "Y", 0x8000 },
    };

    bool prevDown;
    long nextTry;

    public string Name { get; private set; } = "Controller - not connected";
    public bool Connected { get; private set; }

    static double Norm(short v) => Math.Clamp(v / 32767.0, -1.0, 1.0);

    public PadState Poll(Config cfg)
    {
        var r = new PadState();
        int idx = Math.Clamp(cfg.PadIndex, 1, 4) - 1;

        // polling an empty XInput slot is slow, so retry once per second
        long now = Environment.TickCount64;
        if (!Connected && now < nextTry) return r;

        if (!XInput.TryGet(idx, out XInput.StateData st))
        {
            Connected = false;
            nextTry = now + 1000;
            Name = "Pad " + (idx + 1) + " - not connected";
            prevDown = false;
            return r;
        }

        Connected = true;
        Name = "Pad " + (idx + 1);
        r.LX = Norm(st.Gamepad.ThumbLX);
        r.LY = Norm(st.Gamepad.ThumbLY);
        r.RX = Norm(st.Gamepad.ThumbRX);
        r.RY = Norm(st.Gamepad.ThumbRY);
        ushort btn = st.Gamepad.Buttons;
        r.DPadX = (btn & 0x0008) != 0 ? 1.0 : ((btn & 0x0004) != 0 ? -1.0 : 0.0);
        r.DPadY = (btn & 0x0001) != 0 ? 1.0 : ((btn & 0x0002) != 0 ? -1.0 : 0.0);
        r.LT = st.Gamepad.LeftTrigger / 255.0;
        r.RT = st.Gamepad.RightTrigger / 255.0;

        bool down = false;
        if (cfg.RecenterButton != null && Masks.TryGetValue(cfg.RecenterButton, out ushort mask))
            down = (btn & mask) != 0;
        r.RecenterPressed = down && !prevDown;
        prevDown = down;

        bool shake = false;
        if (cfg.ShakeButton != null && Masks.TryGetValue(cfg.ShakeButton, out ushort shakeMask))
            shake = (btn & shakeMask) != 0;
        r.ShakeHeld = shake;
        return r;
    }
}

// ====================================================================== raw input (mouse + keyboard)
/// <summary>
/// Windows "Raw Input" for the mouse and the keyboard, running on its OWN thread with its own message loop,
/// so window painting can never delay it. It keeps working while yuzu has the focus, tells the devices apart
/// (so every player can use a different mouse or keyboard) and never changes how yuzu sees them.
/// </summary>
public sealed class RawInputHub : NativeWindow
{
    public static RawInputHub Shared;

    /// <summary>One subscriber (a player). Collects mouse movement, optionally only from one mouse.</summary>
    public sealed class Tap
    {
        internal readonly object Lk = new object();
        internal int X, Y;

        /// <summary>"" = every mouse, otherwise the device path of the one mouse to listen to.</summary>
        public volatile string DevicePath = "";

        /// <summary>Mouse counts moved since the last call.</summary>
        public void Take(out int dx, out int dy)
        {
            lock (Lk)
            {
                dx = X; dy = Y;
                X = 0; Y = 0;
            }
        }
    }

    const int WM_INPUT = 0x00FF;
    const uint RID_INPUT = 0x10000003;
    const uint RIM_TYPEMOUSE = 0;
    const uint RIM_TYPEKEYBOARD = 1;
    const uint RIDEV_INPUTSINK = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    readonly object lk = new object();
    readonly List<Tap> taps = new List<Tap>();
    readonly bool[] none = new bool[256];
    readonly Dictionary<string, bool[]> keys = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<IntPtr, string> paths = new Dictionary<IntPtr, string>();

    public bool Registered { get; private set; }

    /// <summary>Starts the hub on its own thread (call once at start-up).</summary>
    public static void Start()
    {
        var ready = new ManualResetEventSlim(false);
        var t = new Thread(() =>
        {
            try { Shared = new RawInputHub(); } catch { }
            ready.Set();
            Application.Run();                      // message loop for this thread only
        })
        { IsBackground = true, Name = "RawInput", Priority = ThreadPriority.AboveNormal };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        ready.Wait(3000);
    }

    RawInputHub()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) });         // message-only window
        var dev = new[]
        {
            new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = RIDEV_INPUTSINK, Target = Handle },   // mouse
            new RAWINPUTDEVICE { UsagePage = 1, Usage = 6, Flags = RIDEV_INPUTSINK, Target = Handle },   // keyboard
        };
        try
        {
            Registered = RegisterRawInputDevices(dev, 2, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        }
        catch { }
    }

    public Tap Subscribe()
    {
        var t = new Tap();
        lock (lk) taps.Add(t);
        return t;
    }

    public void Unsubscribe(Tap t)
    {
        lock (lk) taps.Remove(t);
    }

    /// <summary>Is this key down on this keyboard? (device path "" is handled by the caller.)</summary>
    public bool IsKeyDown(string devicePath, int vk)
    {
        if (vk <= 0 || vk > 254) return false;
        lock (lk)
        {
            return keys.TryGetValue(devicePath ?? "", out bool[] arr) && arr[vk];
        }
    }

    string PathOf(IntPtr device)
    {
        lock (lk)
        {
            if (paths.TryGetValue(device, out string known)) return known;
        }
        string p = Devices.PathOfHandle(device);
        lock (lk) paths[device] = p;
        return p;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            try { Read(m.LParam); } catch { }
        }
        base.WndProc(ref m);
    }

    void Read(IntPtr hRaw)
    {
        uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        uint size = 0;
        GetRawInputData(hRaw, RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0) return;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(hRaw, RID_INPUT, buf, ref size, headerSize) != size) return;
            uint type = (uint)Marshal.ReadInt32(buf, 0);
            IntPtr device = Marshal.ReadIntPtr(buf, 8);
            int off = (int)headerSize;                           // RAWMOUSE / RAWKEYBOARD follow the header

            if (type == RIM_TYPEMOUSE)
            {
                int flags = Marshal.ReadInt16(buf, off) & 0xFFFF;
                if ((flags & 0x01) != 0) return;                 // absolute position (remote desktop/tablet): ignore
                int dx = Marshal.ReadInt32(buf, off + 12);       // lLastX
                int dy = Marshal.ReadInt32(buf, off + 16);       // lLastY
                if (dx == 0 && dy == 0) return;
                string path = PathOf(device);
                Tap[] list;
                lock (lk) list = taps.ToArray();
                foreach (Tap t in list)
                {
                    string want = t.DevicePath;
                    if (string.IsNullOrEmpty(want) || string.Equals(want, path, StringComparison.OrdinalIgnoreCase))
                    {
                        lock (t.Lk) { t.X += dx; t.Y += dy; }
                    }
                }
            }
            else if (type == RIM_TYPEKEYBOARD)
            {
                int kflags = Marshal.ReadInt16(buf, off + 2) & 0xFFFF;
                int vkey = Marshal.ReadInt16(buf, off + 6) & 0xFFFF;
                if (vkey <= 0 || vkey > 254) return;
                bool up = (kflags & 0x01) != 0;                  // RI_KEY_BREAK
                string path = PathOf(device);
                lock (lk)
                {
                    if (!keys.TryGetValue(path, out bool[] arr))
                    {
                        arr = new bool[256];
                        keys[path] = arr;
                    }
                    arr[vkey] = !up;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
}

// ====================================================================== device names
public sealed class DeviceInfo
{
    public string Path = "";
    public string Name = "";
    public override string ToString() => Name;
}

/// <summary>Lists the connected controllers, mice and keyboards with their full names.</summary>
public static class Devices
{
    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICELIST
    {
        public IntPtr Device;
        public uint Type;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] list, ref uint count, uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    static extern bool HidD_GetProductString(IntPtr handle, StringBuilder buffer, uint length);

    const uint RIDI_DEVICENAME = 0x20000007;

    public static string PathOfHandle(IntPtr device)
    {
        try
        {
            uint size = 0;
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0) return "";
            IntPtr p = Marshal.AllocHGlobal((int)size * 2 + 4);
            try
            {
                if (GetRawInputDeviceInfo(device, RIDI_DEVICENAME, p, ref size) == uint.MaxValue) return "";
                return Marshal.PtrToStringUni(p) ?? "";
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }
        catch
        {
            return "";
        }
    }

    static List<(uint Type, string Path)> Raw()
    {
        var result = new List<(uint, string)>();
        try
        {
            uint size = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
            uint count = 0;
            if (GetRawInputDeviceList(null, ref count, size) != 0 || count == 0) return result;
            var list = new RAWINPUTDEVICELIST[count];
            uint got = GetRawInputDeviceList(list, ref count, size);
            if (got == uint.MaxValue) return result;
            for (int i = 0; i < got && i < list.Length; i++)
                result.Add((list[i].Type, PathOfHandle(list[i].Device)));
        }
        catch { }
        return result;
    }

    static string ProductString(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        try
        {
            IntPtr h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return "";
            try
            {
                var sb = new StringBuilder(128);
                return HidD_GetProductString(h, sb, 256) ? sb.ToString().Trim() : "";
            }
            finally
            {
                CloseHandle(h);
            }
        }
        catch
        {
            return "";
        }
    }

    static string VidPid(string path)
    {
        Match m = Regex.Match(path ?? "", "VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() + ":" + m.Groups[2].Value.ToUpperInvariant() : "";
    }

    static string Describe(string path, string fallback)
    {
        string name = ProductString(path);
        if (name.Length > 0) return name;
        if ((path ?? "").IndexOf("RDP", StringComparison.OrdinalIgnoreCase) >= 0) return "Remote Desktop " + fallback.ToLowerInvariant();
        string ids = VidPid(path);
        return ids.Length > 0 ? fallback + " (" + ids + ")" : fallback;
    }

    static List<DeviceInfo> Find(uint type, string fallback)
    {
        var res = new List<DeviceInfo>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in Raw())
        {
            if (d.Type != type || string.IsNullOrEmpty(d.Path)) continue;
            string name = Describe(d.Path, fallback);
            seen.TryGetValue(name, out int n);
            seen[name] = n + 1;
            res.Add(new DeviceInfo { Path = d.Path, Name = n == 0 ? name : name + " (" + (n + 1) + ")" });
        }
        return res;
    }

    public static List<DeviceInfo> Mice() => Find(0, "Mouse");
    public static List<DeviceInfo> Keyboards() => Find(1, "Keyboard");

    /// <summary>The four XInput slots: the pad's full name, or "not connected".</summary>
    public static string[] PadNames()
    {
        var names = new string[4];
        Dictionary<string, string> hid = null;
        for (int i = 0; i < 4; i++)
        {
            if (!XInput.TryGet(i, out _)) { names[i] = "not connected"; continue; }
            string name = "";
            if (XInput.TryGetIds(i, out int vid, out int pid))
            {
                if (hid == null) hid = HidProducts();
                hid.TryGetValue(vid.ToString("X4") + ":" + pid.ToString("X4"), out name);
            }
            names[i] = string.IsNullOrEmpty(name) ? XInput.TypeName(i) : name;
        }
        return names;
    }

    static Dictionary<string, string> HidProducts()
    {
        var map = new Dictionary<string, string>();
        foreach (var d in Raw())
        {
            if (d.Type != 2) continue;                       // HID devices
            string ids = VidPid(d.Path);
            if (ids.Length == 0 || map.ContainsKey(ids)) continue;
            string name = ProductString(d.Path);
            if (name.Length > 0) map[ids] = name;
        }
        return map;
    }
}

// ====================================================================== keyboard / buttons
/// <summary>Key / mouse-button state. Works while yuzu has the focus.</summary>
public static class KeyState
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    /// <summary>Choices for the hotkey drop-downs.</summary>
    public static readonly string[] Names =
    {
        "Off", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "Mouse 4", "Mouse 5", "Middle mouse", "Right mouse",
        "Left Ctrl", "Left Alt", "Left Shift",
    };

    static readonly Dictionary<string, int> cache = new Dictionary<string, int>();

    static int Vk(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "Off") return 0;
        lock (cache)
        {
            if (cache.TryGetValue(name, out int v)) return v;
            switch (name)
            {
                case "Mouse 4": v = 0x05; break;
                case "Mouse 5": v = 0x06; break;
                case "Middle mouse": v = 0x04; break;
                case "Right mouse": v = 0x02; break;
                case "Left Ctrl": v = 0xA2; break;
                case "Left Alt": v = 0xA4; break;
                case "Left Shift": v = 0xA0; break;
                default:
                    v = Enum.TryParse(name, out Keys k) ? (int)(k & Keys.KeyCode) : 0;
                    break;
            }
            cache[name] = v;
            return v;
        }
    }

    /// <summary>
    /// Is this key down? With a device path, only THAT keyboard counts (keys of mouse buttons always use the
    /// global state); with "" any keyboard counts.
    /// </summary>
    public static bool IsDown(string name, string devicePath = "")
    {
        int vk = Vk(name);
        if (vk == 0) return false;
        if (!string.IsNullOrEmpty(devicePath) && vk >= 0x07 && RawInputHub.Shared != null)
        {
            int raw = vk;
            if (vk == 0xA0 || vk == 0xA1) raw = 0x10;            // raw input reports the generic Shift / Ctrl / Alt
            else if (vk == 0xA2 || vk == 0xA3) raw = 0x11;
            else if (vk == 0xA4 || vk == 0xA5) raw = 0x12;
            return RawInputHub.Shared.IsKeyDown(devicePath, raw);
        }
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }
}

/// <summary>Freezes the cursor in place (optional, only while mouse motion is switched on by a key).</summary>
public static class CursorLock
{
    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    public static bool Get(out int x, out int y)
    {
        if (GetCursorPos(out POINT p)) { x = p.X; y = p.Y; return true; }
        x = 0; y = 0;
        return false;
    }

    public static void Set(int x, int y) { SetCursorPos(x, y); }
}

/// <summary>
/// Keyboard Motion: every point (KeyDot) has a level 0..1 that moves towards 1 while its key asks for it
/// and back to 0 afterwards, taking ReachMs / ReleaseMs. The virtual stick is the sum of all points.
/// </summary>
public class KeyboardState
{
    class St
    {
        public double Level;
        public bool Latched;
        public double HeldFor;
        public bool TimedOut;
        public bool PrevDown;
        public double PulseLeft;
    }

    readonly Dictionary<KeyDot, St> states = new Dictionary<KeyDot, St>();
    readonly object lk = new object();

    /// <summary>How "pressed" a point is right now (0..1), for the glow in the editor.</summary>
    public double GetLevel(KeyDot d)
    {
        lock (lk) return states.TryGetValue(d, out St s) ? s.Level : 0.0;
    }

    public void Reset()
    {
        lock (lk) states.Clear();
    }

    public void Update(double dt, Config cfg, out double sx, out double sy)
    {
        KeyDot[] dots = cfg.Dots.ToArray();
        string device = cfg.DevicePath ?? "";
        double ax = 0, ay = 0;
        lock (lk)
        {
            // forget points that were deleted
            if (states.Count > dots.Length)
            {
                foreach (KeyDot gone in states.Keys.Where(k => Array.IndexOf(dots, k) < 0).ToList())
                    states.Remove(gone);
            }

            foreach (KeyDot d in dots)
            {
                if (!states.TryGetValue(d, out St st))
                {
                    st = new St();
                    states[d] = st;
                }
                bool down = KeyState.IsDown(d.Key, device);
                bool pressed = down && !st.PrevDown;
                st.PrevDown = down;

                double target = 0;
                switch (d.Behavior)
                {
                    case "Toggle":
                        if (pressed) st.Latched = !st.Latched;
                        target = st.Latched ? 1 : 0;
                        break;
                    case "Pulse":
                        if (pressed) st.PulseLeft = Math.Max(d.MaxHoldMs, 50.0) / 1000.0;
                        if (st.PulseLeft > 0) { st.PulseLeft -= dt; target = 1; }
                        break;
                    default:   // Hold
                        if (down)
                        {
                            if (pressed) { st.HeldFor = 0; st.TimedOut = false; }
                            st.HeldFor += dt;
                            if (d.MaxHoldMs > 0 && st.HeldFor * 1000.0 > d.MaxHoldMs) st.TimedOut = true;
                        }
                        else
                        {
                            st.HeldFor = 0;
                            st.TimedOut = false;
                        }
                        target = down && !st.TimedOut ? 1 : 0;
                        break;
                }

                if (target > st.Level)
                {
                    st.Level = d.ReachMs <= 0 ? target : Math.Min(target, st.Level + dt * 1000.0 / d.ReachMs);
                }
                else if (target < st.Level)
                {
                    st.Level = d.ReleaseMs <= 0 ? target : Math.Max(target, st.Level - dt * 1000.0 / d.ReleaseMs);
                }

                double eased = d.Ease ? st.Level * st.Level * (3.0 - 2.0 * st.Level) : st.Level;
                double k = d.Strength / 100.0 * eased;
                ax += d.X * k;
                ay += d.Y * k;
            }
        }
        double m = Math.Sqrt(ax * ax + ay * ay);
        if (m > 1.0) { ax /= m; ay /= m; }
        sx = ax;
        sy = ay;
    }
}
