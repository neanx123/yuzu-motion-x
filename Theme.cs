using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>Loads the program icon (embedded in the exe) for window title bars.</summary>
public static class AppIcon
{
    static Icon icon;
    static bool tried;

    public static Icon Get()
    {
        if (tried) return icon;
        tried = true;
        try
        {
            using (var s = typeof(AppIcon).Assembly.GetManifestResourceStream("icon.ico"))
            {
                if (s != null) icon = new Icon(s);
            }
        }
        catch { }
        if (icon == null)
        {
            try
            {
                string p = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(p)) icon = Icon.ExtractAssociatedIcon(p);
            }
            catch { }
        }
        return icon;
    }
}

public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(22, 24, 29);
    public static readonly Color Card = Color.FromArgb(33, 36, 43);
    public static readonly Color Card2 = Color.FromArgb(48, 52, 61);
    public static readonly Color Text = Color.FromArgb(232, 234, 238);
    public static readonly Color Sub = Color.FromArgb(150, 157, 168);
    public static readonly Color Accent = Color.FromArgb(79, 195, 247);
    public static readonly Color AccentText = Color.FromArgb(10, 20, 30);
    public static readonly Color Track = Color.FromArgb(62, 67, 78);
    public static readonly Color View = Color.FromArgb(17, 19, 23);
    public static readonly Color Warn = Color.FromArgb(255, 193, 7);
    public static readonly Color Good = Color.FromArgb(120, 220, 140);
    public static readonly Color Red = Color.FromArgb(255, 90, 90);
    public static readonly Color Disabled = Color.FromArgb(92, 97, 108);
}

/// <summary>Flat bar-style slider (the "bar meter" you drag).</summary>
public class FlatSlider : Control
{
    public double Min = 0, Max = 1;
    double val;
    bool drag;
    public event Action<double> UserChanged;

    public FlatSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(190, 26);
        Cursor = Cursors.Hand;
    }

    public double Value => val;

    public void SetValue(double v)
    {
        val = Math.Clamp(v, Min, Max);
        Invalidate();
    }

    Color Backdrop()
    {
        Control p = Parent;
        while (p != null && p.BackColor.A < 255) p = p.Parent;
        return p != null ? p.BackColor : Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Backdrop());
        float l = 10f, r = Width - 10f, cy = Height / 2f;
        float t = (float)((val - Min) / (Max - Min));
        t = Math.Clamp(t, 0f, 1f);
        float x = l + (r - l) * t;
        using (var track = new Pen(Theme.Track, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(track, l, cy, r, cy);
        using (var fill = new Pen(Enabled ? Theme.Accent : Theme.Disabled, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(fill, l, cy, x, cy);
        using (var br = new SolidBrush(Enabled ? Color.White : Color.FromArgb(130, 135, 146)))
            g.FillEllipse(br, x - 7f, cy - 7f, 14f, 14f);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    void SetFromMouse(int mx)
    {
        double t = Math.Clamp((mx - 10.0) / Math.Max(1.0, Width - 20.0), 0.0, 1.0);
        val = Min + (Max - Min) * t;
        Invalidate();
        UserChanged?.Invoke(val);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { drag = true; SetFromMouse(e.X); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (drag) SetFromMouse(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        drag = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        double step = (Max - Min) / 50.0 * (e.Delta > 0 ? 1 : -1);
        val = Math.Clamp(val + step, Min, Max);
        Invalidate();
        UserChanged?.Invoke(val);
    }
}

/// <summary>Remembers how to refresh controls from the Config ("sync") and guards against feedback loops.</summary>
public sealed class UiCtx
{
    public readonly List<Action> Syncs = new List<Action>();
    public bool Updating;

    public void SyncAll()
    {
        Updating = true;
        try { foreach (var s in Syncs) s(); }
        finally { Updating = false; }
    }

    public void Quiet(Action a)
    {
        bool old = Updating;
        Updating = true;
        try { a(); }
        finally { Updating = old; }
    }

    public FlowLayoutPanel Slider(Control parent, string label, double min, double max,
                                  Func<double> get, Action<double> set, string fmt,
                                  int labelW = 150, int sliderW = 190)
    {
        var row = Ui.Row(parent);
        var lab = Ui.Txt(label, Theme.Text, 9f, false, labelW);
        var sl = new FlatSlider { Min = min, Max = max, Width = sliderW, Margin = new Padding(0) };
        var val = Ui.Txt("", Theme.Sub, 9f, false, 64);
        sl.UserChanged += v =>
        {
            set(v);
            val.Text = v.ToString(fmt);
        };
        Syncs.Add(() =>
        {
            double cur = get();
            sl.SetValue(cur);
            val.Text = cur.ToString(fmt);
        });
        row.Controls.Add(lab);
        row.Controls.Add(sl);
        row.Controls.Add(val);
        return row;
    }

    public ComboBox Combo(Control parent, string label, string[] items,
                          Func<string> get, Action<string> set,
                          int labelW = 150, int width = 200)
    {
        var row = Ui.Row(parent);
        var cb = Ui.NewCombo(width);
        cb.Items.AddRange(items);
        cb.SelectionChangeCommitted += (s, e) =>
        {
            if (cb.SelectedItem != null) set((string)cb.SelectedItem);
        };
        Syncs.Add(() =>
        {
            string cur = get();
            if (cb.Items.Contains(cur)) cb.SelectedItem = cur;
        });
        if (!string.IsNullOrEmpty(label)) row.Controls.Add(Ui.Txt(label, Theme.Text, 9f, false, labelW));
        row.Controls.Add(cb);
        return cb;
    }

    public CheckBox Check(Control parent, string text, Func<bool> get, Action<bool> set)
    {
        var cb = new CheckBox
        {
            Text = text,
            AutoSize = true,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 3, 0, 3),
        };
        cb.CheckedChanged += (s, e) => { if (!Updating) set(cb.Checked); };
        Syncs.Add(() => cb.Checked = get());
        parent.Controls.Add(cb);
        return cb;
    }
}

/// <summary>Dark-theme building blocks shared by all windows.</summary>
public static class Ui
{
    public static Label Txt(string text, Color color, float size = 9f, bool bold = false, int width = 0)
    {
        var l = new Label
        {
            Text = text,
            ForeColor = color,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            Margin = new Padding(0),
        };
        if (width > 0)
        {
            l.AutoSize = false;
            l.Width = width;
            l.Height = 26;
            l.TextAlign = ContentAlignment.MiddleLeft;
        }
        else
        {
            l.AutoSize = true;
        }
        return l;
    }

    public static FlowLayoutPanel Column() => new FlowLayoutPanel
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0),
        BackColor = Color.Transparent,
    };

    public static FlowLayoutPanel Card(Control parent, string title, int minWidth)
    {
        var p = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Padding = new Padding(12, 8, 12, 10),
            Margin = new Padding(6),
            MinimumSize = new Size(minWidth, 0),
        };
        if (!string.IsNullOrEmpty(title))
        {
            var t = Txt(title.ToUpperInvariant(), Theme.Sub, 8f, true);
            t.Margin = new Padding(0, 0, 0, 6);
            p.Controls.Add(t);
        }
        parent.Controls.Add(p);
        return p;
    }

    public static FlowLayoutPanel Row(Control parent)
    {
        var r = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 1, 0, 1),
            BackColor = Color.Transparent,
        };
        parent.Controls.Add(r);
        return r;
    }

    public static Button Btn(string text, EventHandler click, bool accent = false)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Theme.Accent : Theme.Card2,
            ForeColor = accent ? Theme.AccentText : Theme.Text,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(0, 2, 8, 2),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    public static ComboBox NewCombo(int width)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Card2,
            ForeColor = Theme.Text,
            Width = width,
            Margin = new Padding(0, 4, 8, 2),
        };
    }

    /// <summary>Flat toggle button (radio style). Always create each group inside its own row.</summary>
    public static RadioButton Toggle(Control row, string text, int w, int h, float size)
    {
        var rb = new RadioButton
        {
            Appearance = Appearance.Button,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(w, h),
            Margin = new Padding(0, 0, 6, 0),
            BackColor = Theme.Card2,
            ForeColor = Theme.Text,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", size),
        };
        rb.FlatAppearance.BorderSize = 0;
        rb.FlatAppearance.CheckedBackColor = Theme.Accent;
        Action restyle = () =>
        {
            rb.FlatAppearance.CheckedBackColor = rb.Enabled ? Theme.Accent : Theme.Disabled;
            rb.ForeColor = !rb.Enabled ? Color.FromArgb(150, 155, 165) : (rb.Checked ? Theme.AccentText : Theme.Text);
        };
        rb.CheckedChanged += (s, e) => restyle();
        rb.EnabledChanged += (s, e) => restyle();
        row.Controls.Add(rb);
        return rb;
    }

    public static string Prompt(IWin32Window owner, string title, string label, string def)
    {
        using (var f = new Form())
        {
            f.Text = title;
            f.Icon = AppIcon.Get();
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(340, 112);
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.BackColor = Theme.Card;
            f.ForeColor = Theme.Text;
            var l = new Label { Text = label, Left = 12, Top = 12, Width = 316, ForeColor = Theme.Text };
            var tb = new TextBox { Left = 12, Top = 36, Width = 316, Text = def ?? "" };
            var ok = new Button { Text = "OK", Left = 172, Top = 74, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 253, Top = 74, Width = 75, DialogResult = DialogResult.Cancel };
            f.Controls.Add(l);
            f.Controls.Add(tb);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog(owner) == DialogResult.OK ? tb.Text.Trim() : null;
        }
    }
}
