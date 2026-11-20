using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YuzuMotionX;

public class LiveStatus
{
    public float[] Gyro = new float[3];
    public double[] Accel = new double[] { 0, 1, 0 };
    public int Clients;
    public string DeviceInfo = "";
    public bool Active = true;
}

/// <summary>
/// One player's background engine: reads the chosen device, runs the motion engine and (when the
/// player is ON) sends DSU packets to the player's server:port. Each player has its own thread,
/// its own server and its own state, so players never disturb each other.
/// </summary>
public class PlayerRunner
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

    public readonly int Index;
    public readonly PlayerSettings S;
    public readonly Dictionary<string, MotionEngine> Engines = new Dictionary<string, MotionEngine>();
    public readonly LiveStatus Live = new LiveStatus();
    public readonly KeyboardState KeyPoints = new KeyboardState();

    /// <summary>True while this player's tab is on screen, so the preview moves even if the player is OFF.</summary>
    public volatile bool PreviewActive;

    // drag-with-the-mouse test pad in the window (Controller / Mouse)
    public volatile bool VirtualActive;
    public double VX, VY;

    DsuServer server;
    Thread thread;
    volatile bool alive;
    readonly GamepadReader pad = new GamepadReader();
    RawInputHub.Tap tap;

    // mouse virtual stick state
    double px, py, vx, vy;
    string lastStyle = "";
    bool enabled = true, prevAct, prevRec, locked;
    int lockX, lockY;
    string lastDevice = "";

    public bool ServerRunning => server != null;

    public PlayerRunner(int index, PlayerSettings s)
    {
        Index = index;
        S = s;
        foreach (string kind in AppSettings.Devices)
            Engines[kind] = new MotionEngine(s.For(kind));
    }

    // ------------------------------------------------------------------ server
    public bool StartServer(out string error)
    {
        error = "";
        if (server != null) return true;
        try
        {
            var srv = new DsuServer(S.Host, S.Port, (byte)(1 + Index));
            srv.Start();
            server = srv;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            server = null;
            return false;
        }
    }

    public void StopServer()
    {
        var s = server;
        server = null;
        try { s?.Dispose(); } catch { }
        Live.Clients = 0;
    }

    // ------------------------------------------------------------------ thread
    public void Start()
    {
        if (alive) return;
        alive = true;
        if (RawInputHub.Shared != null) tap = RawInputHub.Shared.Subscribe();
        thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    public void Shutdown()
    {
        alive = false;
        StopServer();
        if (tap != null && RawInputHub.Shared != null) RawInputHub.Shared.Unsubscribe(tap);
        tap = null;
    }

    void UpdateMouseStick(double dt, int dx, int dy, Config cfg, string mode, out double sx, out double sy)
    {
        bool velocityStyle = mode == "gyro" || mode == "shake";
        string style = velocityStyle ? "velocity" : "position";
        if (style != lastStyle)
        {
            px = 0; py = 0; vx = 0; vy = 0;
            lastStyle = style;
        }
        double gain = Math.Max(0.05, cfg.MouseSensitivity);

        if (!velocityStyle)
        {
            double full = 400.0 / gain;                 // mouse counts for a full push of the virtual stick
            px += dx / full;
            py += -dy / full;                           // mouse up = stick up
            double m = Math.Sqrt(px * px + py * py);
            if (m > 1.0) { px /= m; py /= m; }
            if (cfg.ReturnToNeutral)
            {
                double k = Math.Exp(-dt * 1.5 * Math.Max(0.05, cfg.ReturnSpeed));
                px *= k;
                py *= k;
                if (Math.Abs(px) < 0.003) px = 0;
                if (Math.Abs(py) < 0.003) py = 0;
            }
            sx = px;
            sy = py;
        }
        else
        {
            double fullSpeed = 3000.0 / gain;           // counts per second for a full push
            double tx = dx / dt / fullSpeed;
            double ty = -dy / dt / fullSpeed;
            double m = Math.Sqrt(tx * tx + ty * ty);
            if (m > 1.0) { tx /= m; ty /= m; }
            double a = 1.0 - Math.Exp(-dt / 0.02);
            vx += (tx - vx) * a;
            vy += (ty - vy) * a;
            if (Math.Abs(vx) < 0.01) vx = 0;
            if (Math.Abs(vy) < 0.01) vy = 0;
            sx = vx;
            sy = vy;
        }
    }

    void Loop()
    {
        try { timeBeginPeriod(1); } catch { }
        var sw = Stopwatch.StartNew();
        double last = sw.Elapsed.TotalSeconds;

        while (alive)
        {
            // idle players do no work, unless their tab is on screen (live preview)
            if (!S.On && !PreviewActive)
            {
                Thread.Sleep(40);
                last = sw.Elapsed.TotalSeconds;
                if (tap != null) tap.Take(out _, out _);
                continue;
            }

            Thread.Sleep(8);
            try
            {
                double now = sw.Elapsed.TotalSeconds;
                double dt = Math.Clamp(now - last, 1e-4, 0.05);
                last = now;

                string dev = S.Device;
                Config cfg = S.For(dev);
                MotionEngine eng = Engines[dev];
                if (dev != lastDevice)
                {
                    lastDevice = dev;
                    px = 0; py = 0; vx = 0; vy = 0;
                    KeyPoints.Reset();
                    eng.Request("recenter");
                }

                int mdx = 0, mdy = 0;
                if (tap != null)
                {
                    tap.DevicePath = S.Mouse.DevicePath ?? "";      // which mouse this player listens to
                    tap.Take(out mdx, out mdy);
                }

                double lx = 0, ly = 0, rx = 0, ry = 0, dpx = 0, dpy = 0, lt = 0, rt = 0;
                bool shake = false;
                bool active = true;

                if (dev == "Controller")
                {
                    PadState p = pad.Poll(cfg);
                    lx = p.LX; ly = p.LY; rx = p.RX; ry = p.RY;
                    dpx = p.DPadX; dpy = p.DPadY; lt = p.LT; rt = p.RT;
                    shake = p.ShakeHeld;
                    if (p.RecenterPressed) eng.Request("recenter");
                    Live.DeviceInfo = pad.Name;
                }
                else if (dev == "Mouse")
                {
                    bool actDown = KeyState.IsDown(cfg.ActivationKey);
                    if (cfg.ActivationKey == "Off" || cfg.ActivationMode == "Always on") active = true;
                    else if (cfg.ActivationMode == "Toggle")
                    {
                        if (actDown && !prevAct) enabled = !enabled;
                        active = enabled;
                    }
                    else active = actDown;
                    prevAct = actDown;

                    bool recDown = KeyState.IsDown(cfg.RecenterKey);
                    if (recDown && !prevRec) eng.Request("recenter");
                    prevRec = recDown;
                    shake = KeyState.IsDown(cfg.ShakeKey) && active;

                    if (!active) { mdx = 0; mdy = 0; }

                    // optional: freeze the cursor while motion is on (never with "Always on")
                    bool wantLock = cfg.LockCursor && cfg.ActivationMode != "Always on" && cfg.ActivationKey != "Off" && active;
                    if (wantLock)
                    {
                        if (!locked) locked = CursorLock.Get(out lockX, out lockY);
                        if (locked) CursorLock.Set(lockX, lockY);
                    }
                    else locked = false;

                    UpdateMouseStick(dt, mdx, mdy, cfg, Config.NormMode(cfg.Mode), out rx, out ry);
                    Live.DeviceInfo = RawInputHub.Shared != null && RawInputHub.Shared.Registered
                        ? (string.IsNullOrEmpty(cfg.DevicePath) ? "Any mouse" : (cfg.DeviceName.Length > 0 ? cfg.DeviceName : "Selected mouse"))
                        : "Mouse - raw input NOT available";
                }
                else // Keyboard
                {
                    KeyPoints.Update(dt, cfg, out rx, out ry);
                    bool recDown = KeyState.IsDown(cfg.RecenterKey, cfg.DevicePath ?? "");
                    if (recDown && !prevRec) eng.Request("recenter");
                    prevRec = recDown;
                    shake = KeyState.IsDown(cfg.ShakeKey, cfg.DevicePath ?? "");
                    Live.DeviceInfo = (string.IsNullOrEmpty(cfg.DevicePath) ? "Any keyboard" : (cfg.DeviceName.Length > 0 ? cfg.DeviceName : "Selected keyboard")) +
                                      " - Keyboard Motion (experimental), " + cfg.Dots.Count + " point(s)";
                }

                if (VirtualActive && dev != "Keyboard")
                {
                    lx = VX; ly = VY; rx = VX; ry = VY;
                    px = 0; py = 0; vx = 0; vy = 0;
                }

                eng.Step(dt, lx, ly, rx, ry, dpx, dpy, lt, rt, shake);

                float[] gyro = eng.OutputGyro();
                float[] accel =
                {
                    (float)eng.Accel[0], (float)eng.Accel[1], (float)eng.Accel[2]
                };
                var srv = server;
                if (srv != null && S.On)
                {
                    srv.SendMotion(accel, gyro);
                    Live.Clients = srv.ClientCount;
                }
                else Live.Clients = 0;

                Live.Gyro = gyro;
                Live.Accel = new double[] { accel[0], accel[1], accel[2] };
                Live.Active = active;
            }
            catch { }
        }

        try { timeEndPeriod(1); } catch { }
    }
}
