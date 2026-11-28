namespace YuzuMotionX;

static class M
{
    public static double Rad(double d) => d * Math.PI / 180.0;
    public static double Deg(double r) => r * 180.0 / Math.PI;
    public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
}

/// <summary>Unit quaternion (w, x, y, z).</summary>
public readonly struct Q
{
    public readonly double W, X, Y, Z;

    public Q(double w, double x, double y, double z) { W = w; X = x; Y = y; Z = z; }

    public static Q Identity => new Q(1, 0, 0, 0);

    public static Q operator *(Q a, Q b) => new Q(
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

    public Q Conj() => new Q(W, -X, -Y, -Z);

    public Q Normalized()
    {
        double n = Math.Sqrt(W * W + X * X + Y * Y + Z * Z);
        if (n == 0) n = 1;
        return new Q(W / n, X / n, Y / n, Z / n);
    }

    public static Q FromAxisAngle(double ax, double ay, double az, double angle)
    {
        double s = Math.Sin(angle / 2.0);
        return new Q(Math.Cos(angle / 2.0), ax * s, ay * s, az * s);
    }

    /// <summary>Degrees. pitch = about X, yaw = about Y (face normal), roll = about Z (long axis).</summary>
    public static Q Euler(double pitch, double yaw, double roll) =>
        FromAxisAngle(0, 1, 0, M.Rad(yaw)) *
        FromAxisAngle(1, 0, 0, M.Rad(pitch)) *
        FromAxisAngle(0, 0, 1, M.Rad(roll));

    public (double x, double y, double z) Rotate(double vx, double vy, double vz)
    {
        Q p = new Q(0, vx, vy, vz);
        Q r = (this * p) * Conj();
        return (r.X, r.Y, r.Z);
    }

    /// <summary>Shortest-path rotation as unit axis + angle (radians).</summary>
    public (double ax, double ay, double az, double angle) ToAxisAngle()
    {
        double w = W, x = X, y = Y, z = Z;
        if (w < 0) { w = -w; x = -x; y = -y; z = -z; }
        double s = Math.Sqrt(x * x + y * y + z * z);
        if (s < 1e-9) return (1, 0, 0, 0);
        return (x / s, y / s, z / s, 2.0 * Math.Atan2(s, w));
    }
}

/// <summary>
/// Keeps a virtual controller orientation (Pose) plus a linear "shake" acceleration.
/// The gyro we send is exactly the angular velocity of Pose and the accelerometer is
/// gravity as seen from Pose plus the shake, so yuzu's sensor fusion gets consistent data.
/// Styles:  tilt  = stick sets the angle (springs back)      gyro  = stick sets rotation speed
///          shake = stick/button shakes back and forth        swing = fast tilt + flick impulses
/// </summary>
public class MotionEngine
{
    readonly Config c;
    volatile string flag;
    double testStart, testUntil;
    string testMode = "tilt";
    double prevX, prevY, shakePhase;
    readonly double[] mpos = new double[3];     // Move style: virtual body position (world)
    readonly double[] mvel = new double[3];
    readonly double[] flick = new double[3];

    public Q Pose;                 // virtual controller pose (world)
    public Q ViewPose;             // pose including the hold style (for the preview)
    public Q ViewNeutral;
    public double Fx, Fy;          // smoothed stick (for display)
    public double RawX, RawY;      // stick in use after dead zone (and the override dots), before smoothing
    public double InX, InY;        // what is really pushed (before the override dots)
    public double DevDeg;          // degrees away from neutral
    public double[] Gyro = new double[3];                 // output, deg/s
    public double[] Accel = new double[] { 0, 1, 0 };     // output, g
    public double[] Lin = new double[3];                  // linear (shake / move) acceleration, g
    public double[] MovePos = new double[3];              // Move style position, about -1..1 (for the preview)
    public string ActiveMode = "tilt";
    public bool Testing;

    public MotionEngine(Config cfg)
    {
        c = cfg;
        Pose = Neutral();
        ViewPose = Pose;
        ViewNeutral = Pose;
    }

    public void Request(string what) { flag = what; }

    public void StartTest(string mode, double seconds)
    {
        testMode = Config.NormMode(mode);
        testStart = Now();
        testUntil = testStart + seconds;
    }

    static double Now() => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

    Q Neutral()
    {
        if (c.NeutralPreset == "Captured" && c.CapturedQ != null && c.CapturedQ.Length == 4)
            return new Q(c.CapturedQ[0], c.CapturedQ[1], c.CapturedQ[2], c.CapturedQ[3]).Normalized();
        return Q.Euler(c.NeutralPitch, c.NeutralYaw, c.NeutralRoll);
    }

    public Q Hold()
    {
        if (Config.IsPro(c.ControllerType)) return Q.Identity;
        switch (c.HoldStyle)
        {
            case "Sideways (left)": return Q.Euler(0, -90, 0);
            case "Sideways (right)": return Q.Euler(0, 90, 0);
            case "Upside down": return Q.Euler(0, 180, 0);
            default: return Q.Identity;
        }
    }

    static (double, double) Shape(double x, double y, double dz, double curve)
    {
        double m = Math.Sqrt(x * x + y * y);
        if (m <= dz || m == 0) return (0, 0);
        double adj = (Math.Min(m, 1.0) - dz) / Math.Max(1e-6, 1.0 - dz);
        adj = Math.Pow(adj, Math.Max(0.1, curve));
        double s = adj / m;
        return (x * s, y * s);
    }

    /// <summary>
    /// Axis override: the pushed direction is looked up between the 8 dots (every 45 degrees) and the output
    /// goes to where that dot was placed. With all dots at home (or no dots) the input passes through unchanged.
    /// </summary>
    (double, double) MapOverride(double x, double y)
    {
        double[] o = c.AxisOverride;
        if (o == null || o.Length != 16) return (x, y);
        double m = Math.Sqrt(x * x + y * y);
        if (m < 1e-9) return (0, 0);
        double th = Math.Atan2(y, x);
        if (th < 0) th += 2.0 * Math.PI;
        double pos = th / (Math.PI / 4.0);
        double fl = Math.Floor(pos);
        int k = ((int)fl) % 8;
        int k2 = (k + 1) % 8;
        double w = pos - fl;
        double r0 = Math.Sqrt(o[2 * k] * o[2 * k] + o[2 * k + 1] * o[2 * k + 1]);
        double r1 = Math.Sqrt(o[2 * k2] * o[2 * k2] + o[2 * k2 + 1] * o[2 * k2 + 1]);
        double p0 = Math.Atan2(o[2 * k + 1], o[2 * k]);
        double p1 = Math.Atan2(o[2 * k2 + 1], o[2 * k2]);
        double d = p1 - p0;
        while (d > Math.PI) d -= 2.0 * Math.PI;
        while (d <= -Math.PI) d += 2.0 * Math.PI;
        double phi = p0 + w * d;
        double rr = r0 + w * (r1 - r0);
        double mm = Math.Min(m, 1.0);
        return (mm * rr * Math.Cos(phi), mm * rr * Math.Sin(phi));
    }

    static Q Approach(Q q, Q target, double rate, double maxDeg, double dt)
    {
        var (ax, ay, az, ang) = (q.Conj() * target).ToAxisAngle();
        if (ang < 1e-5) return target;
        double step = ang * (1.0 - Math.Exp(-rate * dt));
        step = Math.Min(step, Math.Min(M.Rad(maxDeg) * dt, ang));
        return (q * Q.FromAxisAngle(ax, ay, az, step)).Normalized();
    }

    static Q Limit(Q q, Q n, double limDeg)
    {
        var (ax, ay, az, ang) = (n.Conj() * q).ToAxisAngle();
        if (ang <= M.Rad(limDeg)) return q;
        return (n * Q.FromAxisAngle(ax, ay, az, M.Rad(limDeg))).Normalized();
    }

    public void Step(double dt, double lx, double ly, double rx, double ry, double dpx, double dpy,
                     double lt, double rt, bool shakeHeld)
    {
        Q n = Neutral();
        Q holdQ = Hold();
        Q holdInv = holdQ.Conj();

        string f = flag;
        flag = null;
        if (f == "recenter")
        {
            Pose = n; Fx = 0; Fy = 0;
            for (int i = 0; i < 3; i++) { mpos[i] = 0; mvel[i] = 0; }
        }
        else if (f == "capture" && c.ActiveProfile != Config.DefaultProfileName)
        {
            c.NeutralPreset = "Captured";
            c.PositionBase = "Custom";
            c.LeanMode = "None";
            c.CapturedQ = new[] { Pose.W, Pose.X, Pose.Y, Pose.Z };
            n = Neutral();
        }

        double now = Now();
        bool testing = now < testUntil;
        string mode = testing ? testMode : Config.NormMode(c.Mode);
        Testing = testing;
        ActiveMode = mode;

        // 1. choose the stick (left, right, or whichever is pushed furthest incl. the D-pad)
        double x, y;
        string src = Config.SourceKey(c.Source);
        if (src == "left")
        {
            x = lx; y = ly;
        }
        else if (src == "right")
        {
            x = rx; y = ry;
        }
        else
        {
            x = lx; y = ly;
            double best = lx * lx + ly * ly;
            double mr = rx * rx + ry * ry;
            double md = dpx * dpx + dpy * dpy;
            if (mr > best) { x = rx; y = ry; best = mr; }
            if (md > best) { x = dpx; y = dpy; }
        }

        // 2. dead zone + response curve (or the built-in test patterns)
        if (testing)
        {
            double e = now - testStart;
            switch (testMode)
            {
                case "gyro": x = Math.Sin(e * 2.5); y = 0.8 * Math.Cos(e * 1.7); break;
                case "shake": x = 0.9; y = 0.15; break;
                case "swing": x = Math.Sin(e * 7.0); y = 0.0; break;
                case "move": x = Math.Sign(Math.Sin(e * 1.6)); y = 0.8 * Math.Sign(Math.Cos(e * 1.0)); break;
                default: x = Math.Sin(e * 1.8); y = 0.8 * Math.Cos(e * 1.1); break;
            }
        }
        else
        {
            (x, y) = Shape(x, y, c.Deadzone, c.Curve);
        }
        InX = x;
        InY = y;
        (x, y) = MapOverride(x, y);
        double rawX = x, rawY = y;
        RawX = rawX;
        RawY = rawY;
        bool rawIdle = Math.Sqrt(rawX * rawX + rawY * rawY) < 1e-4;
        bool tiltLike = mode == "tilt" || mode == "swing" || mode == "move";

        // 3. smoothing. In "stay where it stopped" tilt the last stick value is frozen on release,
        //    so the pose stays where it was left instead of sliding back.
        bool freeze = !c.ReturnToNeutral && !testing && rawIdle && tiltLike;
        if (!freeze)
        {
            double tau = c.SmoothingMs / 1000.0;
            double a = tau <= 0 ? 1.0 : 1.0 - Math.Exp(-dt / tau);
            Fx += (x - Fx) * a;
            Fy += (y - Fy) * a;
        }

        // 4. trigger speed shift
        double speed = c.Speed;
        if (c.TriggerShift)
        {
            if (rt > 0.6) speed *= 5.0;
            else if (lt > 0.6) speed *= 0.5;
        }

        // 5. which way does left/right go?  Auto: vertical Joy-Con -> turn (yaw), otherwise lean (roll)
        string xt = Config.XTargetKey(c.XTarget);
        if (xt == "auto")
        {
            var gd = n.Conj().Rotate(0, 1, 0);
            xt = Math.Abs(gd.z) > 0.7 ? "yaw" : "roll";
        }
        double[] comp = { Fy, 0, 0 };          // 0 = pitch, 1 = yaw, 2 = roll
        if (xt == "yaw") comp[1] = Fx; else comp[2] = Fx;
        bool[] on = { c.PitchOn, c.YawOn, c.RollOn };
        bool[] inv = { c.PitchInv, c.YawInv, c.RollInv };
        double[] vals = new double[3];
        for (int i = 0; i < 3; i++)
        {
            double v = on[i] ? comp[i] : 0.0;
            vals[i] = inv[i] ? -v : v;
        }

        // 6. new orientation
        double lim = Math.Max(1.0, c.RangeLimit);
        Q qNew;
        if (mode == "gyro")
        {
            double k = 120.0 * c.Sensitivity * speed;
            double wx = M.Rad(vals[0] * k) * dt;
            double wy = M.Rad(vals[1] * k) * dt;
            double wz = M.Rad(vals[2] * k) * dt;
            double mag = Math.Sqrt(wx * wx + wy * wy + wz * wz);
            if (mag > 1e-9)
                qNew = (Pose * Q.FromAxisAngle(wx / mag, wy / mag, wz / mag, mag)).Normalized();
            else
                qNew = Pose;
            if (c.ReturnToNeutral && rawIdle && !testing)
                qNew = Approach(qNew, n, 6.0 * c.ReturnSpeed, 300.0 * c.ReturnSpeed, dt);
            qNew = Limit(qNew, n, lim);
        }
        else if (mode == "shake" || mode == "move")
        {
            qNew = Approach(Pose, n, 12.0, 360.0, dt);       // pose stays at neutral
        }
        else // tilt and swing
        {
            double slew = mode == "swing" ? 3.0 : 1.0;
            double p = M.Clamp(vals[0] * 45.0 * c.Sensitivity, -lim, lim);
            double yw = M.Clamp(vals[1] * 45.0 * c.Sensitivity, -lim, lim);
            double r = M.Clamp(vals[2] * 45.0 * c.Sensitivity, -lim, lim);
            Q target = n * Q.Euler(p, yw, r);
            qNew = Approach(Pose, target, 10.0 * speed * slew, 240.0 * speed * slew, dt);
        }

        // snap exactly onto neutral once it has (almost) arrived, so it never keeps wandering
        if (c.ReturnToNeutral && rawIdle && !testing)
        {
            var (_, _, _, da) = (n.Conj() * qNew).ToAxisAngle();
            if (M.Deg(da) < 0.2) qNew = n;
        }

        // 7. linear acceleration: shake oscillation and/or swing flick impulses
        shakePhase = (shakePhase + 2.0 * Math.PI * c.ShakeHz * dt) % (2.0 * Math.PI);
        double[] sh = new double[3];
        bool doShake = mode == "shake" || (shakeHeld && !testing);
        if (doShake)
        {
            double m0 = Math.Sqrt(Fx * Fx + Fy * Fy);
            double drive = shakeHeld ? 1.0 : Math.Min(1.0, m0);
            double dx = 0, dy = 0, dz = 0;
            switch (c.ShakeDir)
            {
                case "Left-right": dx = 1; break;
                case "Forward-back": dz = 1; break;
                case "Up-down": dy = 1; break;
                default:
                    if (m0 > 0.02) { dx = Fx / m0; dz = Fy / m0; }
                    else dx = 1;
                    break;
            }
            double amp = drive * c.ShakeStrength * Math.Sin(shakePhase);
            sh[0] = dx * amp; sh[1] = dy * amp; sh[2] = dz * amp;
        }

        if (mode == "swing")
        {
            double k2 = c.ShakeStrength / 10.0;
            double vx = (rawX - prevX) / dt;
            double vy = (rawY - prevY) / dt;
            double sm = 1.0 - Math.Exp(-dt / 0.02);
            flick[0] += (M.Clamp(vx * k2, -8, 8) - flick[0]) * sm;
            flick[1] += (0.0 - flick[1]) * sm;
            flick[2] += (M.Clamp(vy * k2, -8, 8) - flick[2]) * sm;
        }
        else
        {
            flick[0] = 0; flick[1] = 0; flick[2] = 0;
        }
        prevX = rawX;
        prevY = rawY;

        // Move: the whole controller moves away from where it started (up/down/left/right, or forward/backward)
        // and comes back. A spring-damper "body" follows the stick; its acceleration is what the game feels.
        double[] mv = new double[3];
        if (mode == "move")
        {
            double w = 18.0 * Math.Max(0.1, speed);
            double zeta = 0.8;
            double reach = 2.0 * c.Sensitivity;                          // push in g at full deflection
            double unit = reach / (w * w);
            double sideV = xt == "yaw" ? vals[1] : vals[2];              // left / right
            double upV = vals[0];                                        // up / down  (or forward / backward)
            double[] tgt = { sideV * unit, c.MoveForward ? 0.0 : upV * unit, c.MoveForward ? -upV * unit : 0.0 };
            int sub = Math.Max(1, (int)Math.Ceiling(w * dt / 0.25));
            double h = dt / sub;
            double[] accSum = new double[3];
            for (int k3 = 0; k3 < sub; k3++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double acc = w * w * (tgt[j] - mpos[j]) - 2.0 * zeta * w * mvel[j];
                    mvel[j] += acc * h;
                    mpos[j] += mvel[j] * h;
                    accSum[j] += acc;
                }
            }
            var accDev = qNew.Conj().Rotate(accSum[0] / sub, accSum[1] / sub, accSum[2] / sub);
            mv[0] = accDev.x; mv[1] = accDev.y; mv[2] = accDev.z;
            for (int j = 0; j < 3; j++) MovePos[j] = unit > 1e-9 ? M.Clamp(mpos[j] / unit, -1.5, 1.5) : 0.0;
        }
        else
        {
            for (int j = 0; j < 3; j++) { mpos[j] = 0; mvel[j] = 0; MovePos[j] = 0; }
        }

        for (int i = 0; i < 3; i++) Lin[i] = M.Clamp(sh[i] + flick[i] + mv[i], -8, 8);

        // 8. outputs: gyro = angular velocity travelled; accel = gravity in that pose + linear
        var (gax, gay, gaz, gang) = (Pose.Conj() * qNew).ToAxisAngle();
        double rate = dt > 0 ? M.Deg(gang) / dt : 0.0;
        var gyroOut = holdInv.Rotate(gax * rate, gay * rate, gaz * rate);
        Gyro[0] = gyroOut.x;
        Gyro[1] = gyroOut.y;
        Gyro[2] = gyroOut.z;
        Pose = qNew;

        double grav = c.InvertGravity ? -1.0 : 1.0;
        Q refQ = c.AccelMode == "fixed" ? n : qNew;
        var g3 = refQ.Conj().Rotate(0, grav, 0);
        var accOut = holdInv.Rotate(g3.x + Lin[0], g3.y + Lin[1], g3.z + Lin[2]);
        Accel[0] = accOut.x;
        Accel[1] = accOut.y;
        Accel[2] = accOut.z;

        var (_, _, _, dev) = (n.Conj() * qNew).ToAxisAngle();
        DevDeg = M.Deg(dev);
        ViewPose = qNew * holdQ;
        ViewNeutral = n * holdQ;
    }

    public float[] OutputGyro() => new float[]
    {
        (float)(Gyro[0] * (c.PitchFlip ? -1 : 1)),
        (float)(Gyro[1] * (c.YawFlip ? -1 : 1)),
        (float)(Gyro[2] * (c.RollFlip ? -1 : 1)),
    };
}
