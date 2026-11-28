using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

public class MainWindow : Form
{
    // natural (zoom 100%) width of a player's page with two columns / with one stacked column
    const double NaturalTwo = 1010.0;
    const double NaturalOne = 610.0;

    readonly AppSettings settings;
    readonly PlayerRunner[] runners;
    readonly PlayerPanel[] pages = new PlayerPanel[AppSettings.PlayerCount];
    readonly RadioButton[] tabs = new RadioButton[AppSettings.PlayerCount];
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer layoutTimer = new System.Windows.Forms.Timer();
    Panel pageHost;
    ComboBox zoomBox;
    TutorialForm guide;
    int selected = -1;
    int tickCount;
    double zoom = 1.0;
    bool stacked;

    static readonly string[] ZoomChoices = { "Auto", "70%", "85%", "100%", "125%", "150%", "175%", "200%" };

    public MainWindow(AppSettings settings, PlayerRunner[] runners)
    {
        this.settings = settings;
        this.runners = runners;

        Text = "Yuzu Motion X v1.2";
        Icon = AppIcon.Get();
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1030, 860);
        MinimumSize = new Size(480, 480);
        StartPosition = FormStartPosition.CenterScreen;

        Build();
        SelectPlayer(settings.SelectedPlayer);

        timer.Interval = 33;
        timer.Tick += OnTick;
        timer.Start();

        // the page is re-scaled a moment after the window stops changing size
        layoutTimer.Interval = 120;
        layoutTimer.Tick += (s, ev) => { layoutTimer.Stop(); ApplyLayout(false); };
        SizeChanged += (s, ev) => { layoutTimer.Stop(); layoutTimer.Start(); };
        Shown += (s, ev) => ApplyLayout(true);

        FormClosing += (s, ev) =>
        {
            timer.Stop();
            layoutTimer.Stop();
            settings.Save();
            foreach (var r in runners) r.Shutdown();
        };
    }

    void Build()
    {
        // ---- header: name, version, author, summary, zoom and the Setup Guide button
        var header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.Bg };
        var title = Ui.Txt("Yuzu Motion X", Theme.Text, 24f, true);
        title.Location = new Point(14, 6);
        var ver = Ui.Txt("Version 1.2     ·     By NeanX", Theme.Accent, 10.5f, true);
        ver.Location = new Point(17, 52);
        var sum = Ui.Txt(TutorialForm.Summary, Theme.Sub, 9.5f);
        sum.MaximumSize = new Size(640, 0);
        sum.Location = new Point(17, 76);

        var guideBtn = Ui.Btn("Setup Guide", (s, ev) => ShowGuide(), true);
        guideBtn.Font = new Font("Segoe UI Semibold", 11f);
        guideBtn.Padding = new Padding(16, 6, 16, 6);
        guideBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        var zoomLabel = Ui.Txt("Zoom:", Theme.Sub, 9.5f);
        zoomBox = Ui.NewCombo(90);
        zoomBox.Items.AddRange(ZoomChoices);
        zoomBox.SelectedItem = Array.IndexOf(ZoomChoices, settings.Zoom) >= 0 ? settings.Zoom : "Auto";
        zoomBox.SelectionChangeCommitted += (s, ev) =>
        {
            settings.Zoom = (string)zoomBox.SelectedItem ?? "Auto";
            settings.Save();
            ApplyLayout(true);
        };

        header.Controls.Add(title);
        header.Controls.Add(ver);
        header.Controls.Add(sum);
        header.Controls.Add(guideBtn);
        header.Controls.Add(zoomLabel);
        header.Controls.Add(zoomBox);
        Action place = () =>
        {
            guideBtn.Location = new Point(Math.Max(10, header.Width - guideBtn.Width - 20), 14);
            zoomBox.Location = new Point(Math.Max(10, guideBtn.Left - zoomBox.Width - 14), 22);
            zoomLabel.Location = new Point(Math.Max(10, zoomBox.Left - zoomLabel.Width - 6), 26);
        };
        header.Resize += (s, ev) => place();
        place();

        // ---- the player tabs
        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Theme.Bg,
            Padding = new Padding(10, 4, 10, 4),
        };
        for (int i = 0; i < AppSettings.PlayerCount; i++)
        {
            int idx = i;
            var rb = Ui.Toggle(strip, "Player " + (i + 1), 104, 36, 10f);
            rb.CheckedChanged += (s, ev) => { if (rb.Checked) SelectPlayer(idx); };
            tabs[i] = rb;
        }

        // ---- the page of the selected player (scrolls if needed)
        pageHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };

        // the fill control is added first so the top controls dock above it
        Controls.Add(pageHost);
        Controls.Add(strip);
        Controls.Add(header);
    }

    void ShowGuide()
    {
        if (guide == null || guide.IsDisposed) guide = new TutorialForm();
        if (!guide.Visible) guide.Show(this);
        guide.BringToFront();
    }

    /// <summary>
    /// Works out the zoom and the layout from the window size.
    /// Auto: wide windows get two columns, narrow/tall windows one stacked column, and the page is scaled to fill the width.
    /// A fixed zoom keeps its size and only switches between one and two columns.
    /// </summary>
    void ComputeLayout()
    {
        int availW = Math.Max(300, pageHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
        int availH = Math.Max(300, pageHost.ClientSize.Height);
        string setting = settings.Zoom ?? "Auto";
        if (setting.EndsWith("%") && int.TryParse(setting.TrimEnd('%'), out int pct))
        {
            zoom = pct / 100.0;
            stacked = availW < NaturalTwo * zoom;
        }
        else
        {
            stacked = availW < 840 || availW < availH * 0.95;
            zoom = Math.Clamp(availW / (stacked ? NaturalOne : NaturalTwo), 0.6, 1.9);
        }
        zoom = Math.Round(zoom, 2);
    }

    double appliedZoom = -1;
    bool appliedStacked;

    void ApplyLayout(bool force)
    {
        if (selected < 0 || pages[selected] == null) return;
        ComputeLayout();
        if (!force && Math.Abs(zoom - appliedZoom) < 0.005 && stacked == appliedStacked) return;
        appliedZoom = zoom;
        appliedStacked = stacked;
        pages[selected].ApplyLayout(zoom, stacked);
    }

    void SelectPlayer(int i)
    {
        if (i < 0 || i >= AppSettings.PlayerCount || i == selected) return;
        if (selected >= 0 && pages[selected] != null) pages[selected].SetShown(false);
        selected = i;
        settings.SelectedPlayer = i;

        if (pages[i] == null)
        {
            pages[i] = new PlayerPanel(runners[i]) { Location = new Point(6, 4) };
            pageHost.Controls.Add(pages[i]);
        }
        pages[i].SetShown(true);
        pageHost.AutoScrollPosition = new Point(0, 0);

        if (!tabs[i].Checked) tabs[i].Checked = true;
        UpdateTabTitles();
        if (IsHandleCreated) ApplyLayout(true);
    }

    void UpdateTabTitles()
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].Text = "Player " + (i + 1) + (runners[i].S.On ? "  ●" : "");
    }

    void OnTick(object sender, EventArgs ev)
    {
        if (selected >= 0 && pages[selected] != null) pages[selected].Tick();
        tickCount++;
        if (tickCount % 30 == 0) UpdateTabTitles();
    }
}
