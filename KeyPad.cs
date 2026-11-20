using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>Small window that waits for the next key press and returns its name.</summary>
public sealed class KeyCaptureForm : Form
{
    public string Result;

    public KeyCaptureForm(string prompt)
    {
        Text = "Assign a key";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 140);
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;

        var l = new Label
        {
            Text = prompt,
            Left = 14, Top = 16, Width = 352, Height = 50,
            Font = new Font("Segoe UI", 11f),
            ForeColor = Theme.Text,
        };
        var hint = new Label
        {
            Text = "Press any key now.  (Esc or Cancel = no key)",
            Left = 14, Top = 70, Width = 352,
            ForeColor = Theme.Sub,
        };
        var cancel = new Button { Text = "Cancel", Left = 291, Top = 100, Width = 75, DialogResult = DialogResult.Cancel };
        Controls.Add(l);
        Controls.Add(hint);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys k = keyData & Keys.KeyCode;
        if (k == Keys.None) return base.ProcessCmdKey(ref msg, keyData);
        if (k == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            return true;
        }
        Result = k.ToString();
        DialogResult = DialogResult.OK;
        return true;
    }

    public static string Ask(IWin32Window owner, string prompt)
    {
        using (var f = new KeyCaptureForm(prompt))
            return f.ShowDialog(owner) == DialogResult.OK ? f.Result : null;
    }
}

/// <summary>
/// The Keyboard Motion screen: a circle (the virtual stick area). Click inside it to add a point, click a point
/// to select it, drag to move it, right-click to delete it. The hollow ring is the live stick position.
/// </summary>
public class KeyPadView : Control
{
    public Func<List<KeyDot>> GetDots;
    public Func<KeyDot, double> GetLevel;
    public bool ReadOnly;
    public double LiveX, LiveY;
    public int Selected = -1;
    public event Action<double, double> AddRequested;
    public event Action SelectionChanged;

    int dragIndex = -1;

    public KeyPadView(int size)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(size, size);
        Margin = new Padding(0, 0, 8, 0);
        Cursor = Cursors.Cross;
    }

    public KeyDot SelectedDot
    {
        get
        {
            var d = GetDots?.Invoke();
            return d != null && Selected >= 0 && Selected < d.Count ? d[Selected] : null;
        }
    }

    float Center => Width / 2f;
    float Radius => Width / 2f - 16f;

    PointF ToScreen(double x, double y) => new PointF((float)(Center + x * Radius), (float)(Center - y * Radius));

    static string ShortKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "?";
        switch (key)
        {
            case "Left": return "←";
            case "Right": return "→";
            case "Up": return "↑";
            case "Down": return "↓";
            case "Space": return "Spc";
            case "Return": return "Ent";
            case "ControlKey": return "Ctl";
            case "ShiftKey": return "Shf";
            case "Menu": return "Alt";
        }
        if (key.StartsWith("NumPad") && key.Length == 7) return "N" + key[6];
        if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1])) return key.Substring(1);
        return key.Length > 4 ? key.Substring(0, 4) : key;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.View);
        float c = Center, r = Radius;

        using (var pen = new Pen(Color.FromArgb(107, 119, 133)))
            g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
        using (var pen2 = new Pen(Color.FromArgb(58, 66, 75)))
        {
            g.DrawLine(pen2, c, c - r, c, c + r);
            g.DrawLine(pen2, c - r, c, c + r, c);
            g.DrawEllipse(pen2, c - r / 2, c - r / 2, r, r);
        }

        var dots = GetDots?.Invoke();
        if (dots != null)
        {
            using (var f = new Font("Segoe UI Semibold", 8f))
            {
                for (int i = 0; i < dots.Count; i++)
                {
                    KeyDot d = dots[i];
                    PointF p = ToScreen(d.X * d.Strength / 100.0, d.Y * d.Strength / 100.0);
                    double level = GetLevel != null ? GetLevel(d) : 0.0;
                    bool sel = i == Selected;
                    Color fill = level > 0.05 ? Theme.Warn : (sel ? Theme.Accent : Theme.Card2);
                    using (var br = new SolidBrush(fill))
                        g.FillEllipse(br, p.X - 13f, p.Y - 13f, 26f, 26f);
                    using (var pen = new Pen(sel ? Color.White : Color.FromArgb(120, 127, 138), sel ? 2f : 1f))
                        g.DrawEllipse(pen, p.X - 13f, p.Y - 13f, 26f, 26f);
                    string label = ShortKey(d.Key);
                    SizeF sz = g.MeasureString(label, f);
                    using (var tb = new SolidBrush(fill == Theme.Card2 ? Theme.Text : Theme.AccentText))
                        g.DrawString(label, f, tb, p.X - sz.Width / 2f, p.Y - sz.Height / 2f);
                }
            }
        }

        PointF live = ToScreen(LiveX, LiveY);
        using (var ring = new Pen(Color.White, 1.5f))
            g.DrawEllipse(ring, live.X - 7f, live.Y - 7f, 14f, 14f);

        using (var f = new Font("Segoe UI", 7.5f))
        using (var tb = new SolidBrush(Theme.Sub))
            g.DrawString(ReadOnly ? "locked (DEFAULT)" : "click = add point", f, tb, 6f, Height - 16f);
    }

    int HitTest(int mx, int my)
    {
        var dots = GetDots?.Invoke();
        if (dots == null) return -1;
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            PointF p = ToScreen(dots[i].X * dots[i].Strength / 100.0, dots[i].Y * dots[i].Strength / 100.0);
            double dx = mx - p.X, dy = my - p.Y;
            if (dx * dx + dy * dy <= 15 * 15) return i;
        }
        return -1;
    }

    void SetSelected(int i)
    {
        if (Selected == i) return;
        Selected = i;
        SelectionChanged?.Invoke();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitTest(e.X, e.Y);
        if (e.Button == MouseButtons.Left)
        {
            if (hit >= 0)
            {
                SetSelected(hit);
                if (!ReadOnly) dragIndex = hit;
            }
            else
            {
                double x = (e.X - Center) / Radius, y = -(e.Y - Center) / Radius;
                if (x * x + y * y <= 1.0)
                {
                    if (ReadOnly) SetSelected(-1);
                    else AddRequested?.Invoke(x, y);
                }
            }
        }
        else if (e.Button == MouseButtons.Right && hit >= 0 && !ReadOnly)
        {
            var dots = GetDots?.Invoke();
            if (dots != null && hit < dots.Count)
            {
                dots.RemoveAt(hit);
                Selected = -1;
                SelectionChanged?.Invoke();
                Invalidate();
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragIndex < 0) return;
        var dots = GetDots?.Invoke();
        if (dots == null || dragIndex >= dots.Count) return;
        KeyDot d = dots[dragIndex];
        double x = (e.X - Center) / Radius, y = -(e.Y - Center) / Radius;
        // the point's own position is shown scaled by its Strength, so undo that while dragging
        double k = Math.Max(0.05, d.Strength / 100.0);
        x /= k; y /= k;
        double m = Math.Sqrt(x * x + y * y);
        if (m > 1.0) { x /= m; y /= m; }
        d.X = Math.Round(x, 2);
        d.Y = Math.Round(y, 2);
        SelectionChanged?.Invoke();      // refresh the point's submenu sliders
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragIndex = -1;
    }
}
