using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>
/// Round analog-stick display. Hollow ring = the stick you are pushing,
/// filled dot = the value actually used. Drag with the mouse to use it as a virtual stick.
/// </summary>
public class PadView : Control
{
    public double Sx, Sy;        // value in use
    public double RawX, RawY;    // stick as pushed (after dead zone)
    public string Caption = "stick / drag";

    // ----- axis override dots (8 dots: where each pushed direction really goes)
    public Func<double[]> GetOverride;
    public Action<double[]> SetOverride;
    public bool EditOverride;          // the dots can be dragged
    public bool ReadOnly;              // locked profile: they can't be changed
    public event Action OverrideChanged;
    int dragDot = -1;

    public static readonly string[] DotNames =
        { "Right", "Up-Right", "Up", "Up-Left", "Left", "Down-Left", "Down", "Down-Right" };
    static readonly string[] DotLabels = { "R", "UR", "U", "UL", "L", "DL", "D", "DR" };

    public static double[] HomeDots()
    {
        var d = new double[16];
        for (int k = 0; k < 8; k++)
        {
            double a = k * Math.PI / 4.0;
            d[2 * k] = Math.Cos(a);
            d[2 * k + 1] = Math.Sin(a);
        }
        return d;
    }

    double[] Dots()
    {
        var o = GetOverride?.Invoke();
        return o != null && o.Length == 16 ? o : HomeDots();
    }

    bool HasOverride()
    {
        var o = GetOverride?.Invoke();
        return o != null && o.Length == 16;
    }

    PointF DotPoint(double x, double y)
    {
        float c = Width / 2f, r = Width / 2f - 12f;
        return new PointF((float)(c + x * r), (float)(c - y * r));
    }

    void DrawDots(Graphics g)
    {
        double[] d = Dots(), home = HomeDots();
        using (var f = new Font("Segoe UI Semibold", 7f))
        {
            for (int k = 0; k < 8; k++)
            {
                PointF p = DotPoint(d[2 * k], d[2 * k + 1]);
                bool moved = Math.Abs(d[2 * k] - home[2 * k]) > 1e-6 || Math.Abs(d[2 * k + 1] - home[2 * k + 1]) > 1e-6;
                if (EditOverride)
                {
                    PointF h = DotPoint(home[2 * k], home[2 * k + 1]);
                    if (moved)
                        using (var ln = new Pen(Color.FromArgb(70, Theme.Warn), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                            g.DrawLine(ln, h, p);
                    using (var br = new SolidBrush(moved ? Theme.Warn : Color.FromArgb(120, 127, 138)))
                        g.FillEllipse(br, p.X - 10f, p.Y - 10f, 20f, 20f);
                    SizeF sz = g.MeasureString(DotLabels[k], f);
                    using (var tb = new SolidBrush(Theme.AccentText))
                        g.DrawString(DotLabels[k], f, tb, p.X - sz.Width / 2f, p.Y - sz.Height / 2f);
                }
                else if (moved)
                {
                    using (var pen = new Pen(Theme.Warn, 1.5f))
                        g.DrawEllipse(pen, p.X - 5f, p.Y - 5f, 10f, 10f);
                }
            }
        }
    }

    int HitDot(int mx, int my)
    {
        double[] d = Dots();
        for (int k = 7; k >= 0; k--)
        {
            PointF p = DotPoint(d[2 * k], d[2 * k + 1]);
            double dx = mx - p.X, dy = my - p.Y;
            if (dx * dx + dy * dy <= 12 * 12) return k;
        }
        return -1;
    }

    void WriteDot(int k, double x, double y)
    {
        double[] o = (double[])Dots().Clone();
        o[2 * k] = x;
        o[2 * k + 1] = y;
        double[] home = HomeDots();
        bool allHome = true;
        for (int i = 0; i < 16; i++) if (Math.Abs(o[i] - home[i]) > 1e-6) { allHome = false; break; }
        SetOverride?.Invoke(allHome ? Array.Empty<double>() : o);     // all dots at home = no override at all
        OverrideChanged?.Invoke();
        Invalidate();
    }
    public event Action<double, double> StickMoved;
    public event Action StickReleased;
    bool down;

    public PadView(int size)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(size, size);
        Margin = new Padding(0, 0, 8, 0);
        Cursor = Cursors.Cross;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.View);
        float c = Width / 2f, r = Width / 2f - 12f;
        using (var pen = new Pen(Color.FromArgb(107, 119, 133)))
            g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
        using (var pen2 = new Pen(Color.FromArgb(58, 66, 75)))
        {
            g.DrawLine(pen2, c, c - r, c, c + r);
            g.DrawLine(pen2, c - r, c, c + r, c);
        }
        if (EditOverride || HasOverride()) DrawDots(g);
        float px = (float)(c + Sx * r);
        float py = (float)(c - Sy * r);
        using (var br = new SolidBrush(Theme.Accent))
            g.FillEllipse(br, px - 7f, py - 7f, 14f, 14f);
        float rx = (float)(c + RawX * r);
        float ry = (float)(c - RawY * r);
        using (var ring = new Pen(Color.White, 1.5f))
            g.DrawEllipse(ring, rx - 10f, ry - 10f, 20f, 20f);
        using (var f = new Font("Segoe UI", 7.5f))
        using (var tb = new SolidBrush(Theme.Sub))
            g.DrawString(Caption, f, tb, 6f, Height - 16f);
    }

    void Report(MouseEventArgs e)
    {
        double c = Width / 2.0, r = Width / 2.0 - 12.0;
        double x = Math.Clamp((e.X - c) / r, -1.0, 1.0);
        double y = Math.Clamp(-(e.Y - c) / r, -1.0, 1.0);
        StickMoved?.Invoke(x, y);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (EditOverride)
        {
            if (ReadOnly) return;
            int hit = HitDot(e.X, e.Y);
            if (hit < 0) return;
            if (e.Button == MouseButtons.Right)
            {
                double[] home = HomeDots();
                WriteDot(hit, home[2 * hit], home[2 * hit + 1]);       // right-click = back to its home
            }
            else if (e.Button == MouseButtons.Left)
            {
                dragDot = hit;
            }
            return;
        }
        if (e.Button == MouseButtons.Left) { down = true; Report(e); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (EditOverride)
        {
            if (dragDot < 0) return;
            double c = Width / 2.0, r = Width / 2.0 - 12.0;
            double x = (e.X - c) / r, y = -(e.Y - c) / r;
            double m = Math.Sqrt(x * x + y * y);
            if (m > 1.0) { x /= m; y /= m; }
            double[] home = HomeDots();
            if (Math.Abs(x - home[2 * dragDot]) < 0.07 && Math.Abs(y - home[2 * dragDot + 1]) < 0.07)
            { x = home[2 * dragDot]; y = home[2 * dragDot + 1]; }     // snaps back home when dropped near it
            WriteDot(dragDot, Math.Round(x, 3), Math.Round(y, 3));
            return;
        }
        if (down) Report(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragDot = -1;
        if (down) { down = false; StickReleased?.Invoke(); }
    }
}

/// <summary>Gyro and accelerometer bar meters.</summary>
public class BarsView : Control
{
    public double[] Gyro = new double[3];
    public double[] Accel = new double[3];

    public BarsView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(240, 130);
        Margin = new Padding(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.View);
        DrawGroup(g, 0, "GYRO  deg/s", new[] { "Pitch", "Yaw", "Roll" }, Gyro, 300.0);
        DrawGroup(g, Width / 2, "ACCEL  g", new[] { "X", "Y", "Z" }, Accel, 3.0);
    }

    void DrawGroup(Graphics g, int x0, string title, string[] names, double[] vals, double range)
    {
        using (var f = new Font("Segoe UI", 8f))
        using (var sub = new SolidBrush(Theme.Sub))
        using (var track = new SolidBrush(Theme.Track))
        using (var fill = new SolidBrush(Theme.Accent))
        using (var mid = new Pen(Color.FromArgb(150, 157, 168)))
        {
            g.DrawString(title, f, sub, x0 + 6, 6);
            int bx = x0 + 40;
            int bw = Width / 2 - 50;
            int half = bw / 2;
            for (int i = 0; i < 3; i++)
            {
                int y = 30 + i * 32;
                g.DrawString(names[i], f, sub, x0 + 6, y - 1);
                g.FillRectangle(track, bx, y, bw, 12);
                double v = Math.Clamp(vals[i] / range, -1.0, 1.0);
                int len = (int)(Math.Abs(v) * half);
                if (v >= 0) g.FillRectangle(fill, bx + half, y, len, 12);
                else g.FillRectangle(fill, bx + half - len, y, len, 12);
                g.DrawLine(mid, bx + half, y - 2, bx + half, y + 14);
            }
        }
    }
}

/// <summary>3D preview of a Joy-Con or Pro Controller that moves exactly like the motion data being sent.</summary>
public class ControllerView : Control
{
    public Q Pose = Q.Identity;
    public Q Neutral = Q.Identity;
    public double[] Lin = new double[3];
    public string Kind = "Joy-Con (left)";
    public bool Testing;
    public double Sx, Sy;
    public string Title = "";
    public string Info = "";

    // ----- "set a custom position" editor: three rings (world axes) that you grab and turn with the mouse
    public bool EditMode;
    public Q EditQ = Q.Identity;
    public double[] Move = new double[3];       // Move style position (for the preview)
    public event Action EditChanged;
    int editAxis = -1;
    Point editLast;
    const double RingR = 1.95;
    static readonly Color[] RingColors = { Color.FromArgb(255, 90, 90), Color.FromArgb(110, 220, 130), Color.FromArgb(90, 160, 255) };
    static readonly string[] RingNames = { "X", "Y", "Z" };

    class Part
    {
        public double Cx, Cy, Cz, Hx, Hy, Hz;
        public Color Col;
        public Part(double cx, double cy, double cz, double hx, double hy, double hz, Color col)
        {
            Cx = cx; Cy = cy; Cz = cz; Hx = hx; Hy = hy; Hz = hz; Col = col;
        }
    }

    class FaceDraw
    {
        public double Depth;
        public PointF[] Pts;
        public Color Col;
        public bool BodyTop;
    }

    readonly List<PointF> trail = new List<PointF>();

    const double CamDist = 7.0;
    static readonly double ViewRad = 55.0 * Math.PI / 180.0;
    static readonly double Cv = Math.Cos(ViewRad);
    static readonly double Sv = Math.Sin(ViewRad);
    const double LightX = -0.303, LightY = 0.505, LightZ = 0.808;   // light direction (view space)

    static readonly int[][] Faces =
    {
        new[] { 2, 3, 7, 6 },   // +Y  (top: sticks and buttons)
        new[] { 0, 4, 5, 1 },   // -Y
        new[] { 1, 5, 7, 3 },   // +X
        new[] { 0, 2, 6, 4 },   // -X
        new[] { 4, 6, 7, 5 },   // +Z
        new[] { 0, 1, 3, 2 },   // -Z
    };

    static readonly double[][] Normals =
    {
        new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, -1.0, 0.0 },
        new[] { 1.0, 0.0, 0.0 }, new[] { -1.0, 0.0, 0.0 },
        new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 0.0, -1.0 },
    };

    public ControllerView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(380, 270);
        Margin = new Padding(0, 0, 0, 8);
    }

    // ------------------------------------------------------------------ model
    List<Part> BuildParts(out double sizeFactor)
    {
        var parts = new List<Part>();
        if (Config.IsPro(Kind))
        {
            Color body = Color.FromArgb(82, 84, 95);
            Color grip = Color.FromArgb(60, 62, 71);
            parts.Add(new Part(0, 0, -0.1, 1.2, 0.17, 0.62, body));          // main body (index 0)
            parts.Add(new Part(-0.98, -0.04, 0.85, 0.3, 0.2, 0.58, grip));   // left grip
            parts.Add(new Part(0.98, -0.04, 0.85, 0.3, 0.2, 0.58, grip));    // right grip
            sizeFactor = 0.7;
        }
        else
        {
            Color c = Kind.Contains("right") ? Color.FromArgb(255, 70, 50) : Color.FromArgb(20, 190, 235);
            parts.Add(new Part(0, 0, 0, 0.45, 0.17, 1.25, c));
            sizeFactor = 1.0;
        }
        return parts;
    }

    static double[] Vert(Part p, int i) => new[]
    {
        p.Cx + ((i & 1) != 0 ? p.Hx : -p.Hx),
        p.Cy + ((i & 2) != 0 ? p.Hy : -p.Hy),
        p.Cz + ((i & 4) != 0 ? p.Hz : -p.Hz),
    };

    static (double x, double y, double z) ToView((double x, double y, double z) w) =>
        (w.x, w.y * Cv - w.z * Sv, w.y * Sv + w.z * Cv);

    static PointF Proj(Q q, double x, double y, double z, double scale, float cx, float cy)
    {
        var v = ToView(q.Rotate(x, y, z));
        double f = CamDist / (CamDist - v.z);
        return new PointF((float)(cx + v.x * scale * f), (float)(cy - v.y * scale * f));
    }

    static Color Shade(Color c, double k) => Color.FromArgb(
        c.A,
        (int)Math.Min(255.0, c.R * k),
        (int)Math.Min(255.0, c.G * k),
        (int)Math.Min(255.0, c.B * k));

    // ------------------------------------------------------------------ painting
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.View);

        var parts = BuildParts(out double sf);
        float cx = Width / 2f, cy = Height / 2f + 6f;
        double scale = Math.Min(Width / 4.2, Height / 3.4) * sf;

        Q pose = EditMode ? EditQ : Pose;

        // linear (shake) acceleration and the Move position move the whole controller on screen
        float ox = 0f, oy = 0f;
        if (!EditMode)
        {
            var lw = ToView(pose.Rotate(Lin[0], Lin[1], Lin[2]));
            var mv = ToView((Move[0], Move[1], Move[2]));
            ox = (float)M.Clamp(lw.x * 14.0 + mv.x * 45.0, -70.0, 70.0);
            oy = (float)M.Clamp(-lw.y * 14.0 - mv.y * 45.0, -70.0, 70.0);
            DrawEdges(g, parts, Neutral, scale, cx, cy);
        }

        trail.Add(new PointF(cx + ox, cy + oy));
        if (trail.Count > 10) trail.RemoveAt(0);
        if (Math.Abs(ox) + Math.Abs(oy) > 3f)
        {
            for (int i = 0; i < trail.Count - 1; i++)
            {
                using (var br = new SolidBrush(Color.FromArgb(20 + i * 10, Theme.Accent)))
                    g.FillEllipse(br, trail[i].X - 5f, trail[i].Y - 5f, 10f, 10f);
            }
        }

        DrawModel(g, parts, pose, scale, cx + ox, cy + oy);
        if (EditMode) DrawRings(g, scale, cx, cy);

        using (var f = new Font("Segoe UI Semibold", 10f))
        using (var accent = new SolidBrush(Theme.Accent))
            g.DrawString(Title, f, accent, 10f, 8f);
        if (Testing)
        {
            using (var f = new Font("Segoe UI Semibold", 9f))
            using (var warn = new SolidBrush(Theme.Warn))
                g.DrawString("● TEST RUNNING", f, warn, Width - 120f, 9f);
        }
        using (var f = new Font("Segoe UI", 8.5f))
        using (var sub = new SolidBrush(Theme.Sub))
        {
            g.DrawString(Info, f, sub, 10f, Height - 22f);
            g.DrawString(EditMode ? "drag a ring to turn the controller" : "dashed = neutral", f, sub, Width - (EditMode ? 200f : 90f), Height - 22f);
        }
    }

    static (double x, double y, double z) RingPoint(int axis, double t)
    {
        double c = Math.Cos(t) * RingR, s = Math.Sin(t) * RingR;
        switch (axis)
        {
            case 0: return (0, c, s);       // turns about X
            case 1: return (s, 0, c);       // turns about Y
            default: return (c, s, 0);      // turns about Z
        }
    }

    PointF[] RingScreen(int axis, double scale, float cx, float cy)
    {
        const int N = 72;
        var pts = new PointF[N + 1];
        for (int i = 0; i <= N; i++)
        {
            var p = RingPoint(axis, i * 2.0 * Math.PI / N);
            pts[i] = Proj(Q.Identity, p.x, p.y, p.z, scale, cx, cy);
        }
        return pts;
    }

    void Geometry(out double scale, out float cx, out float cy)
    {
        BuildParts(out double sf);
        cx = Width / 2f;
        cy = Height / 2f + 6f;
        scale = Math.Min(Width / 4.2, Height / 3.4) * sf;
    }

    void DrawRings(Graphics g, double scale, float cx, float cy)
    {
        for (int k = 0; k < 3; k++)
        {
            PointF[] pts = RingScreen(k, scale, cx, cy);
            using (var pen = new Pen(Color.FromArgb(editAxis == k ? 255 : 200, RingColors[k]), editAxis == k ? 4f : 2.5f))
                g.DrawLines(pen, pts);
            using (var f = new Font("Segoe UI Semibold", 9f))
            using (var br = new SolidBrush(RingColors[k]))
                g.DrawString(RingNames[k], f, br, pts[0].X + 4f, pts[0].Y - 8f);
        }
    }

    /// <summary>Nearest ring point to the mouse: returns the ring (or -1) and the index of the nearest point.</summary>
    int NearestRing(Point m, int onlyAxis, out int index, out double dist)
    {
        Geometry(out double scale, out float cx, out float cy);
        int best = -1;
        index = 0;
        dist = double.MaxValue;
        for (int k = 0; k < 3; k++)
        {
            if (onlyAxis >= 0 && k != onlyAxis) continue;
            PointF[] pts = RingScreen(k, scale, cx, cy);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                double dx = m.X - pts[i].X, dy = m.Y - pts[i].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < dist) { dist = d; best = k; index = i; }
            }
        }
        return best;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!EditMode || e.Button != MouseButtons.Left) return;
        int axis = NearestRing(e.Location, -1, out _, out double dist);
        if (axis >= 0 && dist < 12.0)
        {
            editAxis = axis;
            editLast = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!EditMode) return;
        if (editAxis < 0)
        {
            NearestRing(e.Location, -1, out _, out double d);
            Cursor = d < 12.0 ? Cursors.Hand : Cursors.Default;
            return;
        }

        // grab the ring where the mouse is and move it along the ring: that is the turn about its axis
        Geometry(out double scale, out float cx, out float cy);
        NearestRing(e.Location, editAxis, out int idx, out _);
        PointF[] pts = RingScreen(editAxis, scale, cx, cy);
        PointF a = pts[(idx + pts.Length - 2) % (pts.Length - 1)];
        PointF b = pts[(idx + 1) % (pts.Length - 1)];
        double tx = b.X - a.X, ty = b.Y - a.Y;
        double tl = Math.Sqrt(tx * tx + ty * ty);
        if (tl < 1e-6) return;
        tx /= tl; ty /= tl;
        double move = (e.X - editLast.X) * tx + (e.Y - editLast.Y) * ty;
        double angle = move / (scale * RingR);
        double ax = editAxis == 0 ? 1 : 0, ay = editAxis == 1 ? 1 : 0, az = editAxis == 2 ? 1 : 0;
        EditQ = (Q.FromAxisAngle(ax, ay, az, angle) * EditQ).Normalized();
        editLast = e.Location;
        EditChanged?.Invoke();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        editAxis = -1;
        if (EditMode) Invalidate();
    }

    void DrawEdges(Graphics g, List<Part> parts, Q q, double scale, float cx, float cy)
    {
        using (var pen = new Pen(Color.FromArgb(120, 150, 157, 168), 1f) { DashStyle = DashStyle.Dash })
        {
            int[] bits = { 1, 2, 4 };
            foreach (Part part in parts)
            {
                var pts = new PointF[8];
                for (int i = 0; i < 8; i++)
                {
                    double[] v = Vert(part, i);
                    pts[i] = Proj(q, v[0], v[1], v[2], scale, cx, cy);
                }
                for (int i = 0; i < 8; i++)
                    foreach (int bit in bits)
                        if ((i & bit) == 0) g.DrawLine(pen, pts[i], pts[i | bit]);
            }
        }
    }

    void DrawModel(Graphics g, List<Part> parts, Q q, double scale, float cx, float cy)
    {
        var faces = new List<FaceDraw>();
        for (int pi = 0; pi < parts.Count; pi++)
        {
            Part part = parts[pi];
            for (int f = 0; f < 6; f++)
            {
                double[] nl = Normals[f];
                var nv = ToView(q.Rotate(nl[0], nl[1], nl[2]));
                if (nv.z <= 0) continue;

                var pts = new PointF[4];
                double depth = 0;
                for (int k = 0; k < 4; k++)
                {
                    double[] v = Vert(part, Faces[f][k]);
                    var vv = ToView(q.Rotate(v[0], v[1], v[2]));
                    depth += vv.z;
                    double persp = CamDist / (CamDist - vv.z);
                    pts[k] = new PointF((float)(cx + vv.x * scale * persp), (float)(cy - vv.y * scale * persp));
                }
                double shade = 0.40 + 0.60 * Math.Max(0.0, nv.x * LightX + nv.y * LightY + nv.z * LightZ);
                if (f == 1) shade *= 0.8;
                faces.Add(new FaceDraw
                {
                    Depth = depth / 4.0,
                    Pts = pts,
                    Col = Shade(part.Col, shade),
                    BodyTop = pi == 0 && f == 0,
                });
            }
        }

        faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));   // far faces first

        foreach (FaceDraw fd in faces)
        {
            using (var br = new SolidBrush(fd.Col))
                g.FillPolygon(br, fd.Pts);
            using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 1f))
                g.DrawPolygon(pen, fd.Pts);
            if (fd.BodyTop) DrawTopDetails(g, q, parts[0], scale, cx, cy);
        }
    }

    void DrawTopDetails(Graphics g, Q q, Part body, double scale, float cx, float cy)
    {
        double top = body.Cy + body.Hy + 0.004;
        Color dark = Color.FromArgb(30, 30, 35);
        Color cap = Color.FromArgb(110, 113, 120);
        Color btn = Color.FromArgb(38, 40, 46);
        double sx = Sx * 0.07, sz = -Sy * 0.07;      // stick tops follow the analog stick

        if (Config.IsPro(Kind))
        {
            // left stick, d-pad, face buttons, right stick
            DrawDisc(g, q, top, -0.78, -0.30, 0.20, dark, scale, cx, cy);
            DrawDisc(g, q, top, -0.78 + sx, -0.30 + sz, 0.13, cap, scale, cx, cy);
            DrawDisc(g, q, top, 0.42, 0.18, 0.20, dark, scale, cx, cy);
            DrawDisc(g, q, top, 0.42, 0.18, 0.13, cap, scale, cx, cy);
            double[,] pad = { { -0.14, 0.0 }, { 0.14, 0.0 }, { 0.0, -0.14 }, { 0.0, 0.14 } };
            for (int i = 0; i < 4; i++)
            {
                DrawDisc(g, q, top, -0.42 + pad[i, 0], 0.18 + pad[i, 1], 0.065, btn, scale, cx, cy);
                DrawDisc(g, q, top, 0.78 + pad[i, 0], -0.30 + pad[i, 1], 0.065, btn, scale, cx, cy);
            }
        }
        else
        {
            bool right = Kind.Contains("right");
            double stickZ = right ? 0.55 : -0.55;
            double btnZ = -stickZ;
            DrawDisc(g, q, top, 0, stickZ, 0.20, dark, scale, cx, cy);
            DrawDisc(g, q, top, sx, stickZ + sz, 0.13, cap, scale, cx, cy);
            double[,] offs = { { 0.0, -0.17 }, { 0.0, 0.17 }, { -0.17, 0.0 }, { 0.17, 0.0 } };
            for (int i = 0; i < 4; i++)
                DrawDisc(g, q, top, offs[i, 0], btnZ + offs[i, 1], 0.075, btn, scale, cx, cy);
        }
    }

    void DrawDisc(Graphics g, Q q, double top, double lx, double lz, double r, Color col,
                  double scale, float cx, float cy)
    {
        var pts = new PointF[16];
        for (int i = 0; i < 16; i++)
        {
            double a = i * Math.PI / 8.0;
            pts[i] = Proj(q, lx + r * Math.Cos(a), top, lz + r * Math.Sin(a), scale, cx, cy);
        }
        using (var br = new SolidBrush(col))
            g.FillPolygon(br, pts);
    }
}
