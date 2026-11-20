using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

public class MainWindow : Form
{
    readonly AppSettings settings;
    readonly PlayerRunner[] runners;
    readonly PlayerPanel[] pages = new PlayerPanel[AppSettings.PlayerCount];
    readonly RadioButton[] tabs = new RadioButton[AppSettings.PlayerCount];
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    Panel pageHost;
    TutorialForm tutorial;
    int selected = -1;
    int tickCount;

    public MainWindow(AppSettings settings, PlayerRunner[] runners)
    {
        this.settings = settings;
        this.runners = runners;

        Text = "Yuzu Motion X v1.0";
        Icon = AppIcon.Get();
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1010, 840);
        MinimumSize = new Size(860, 560);
        StartPosition = FormStartPosition.CenterScreen;

        Build();
        SelectPlayer(settings.SelectedPlayer);

        timer.Interval = 33;
        timer.Tick += OnTick;
        timer.Start();

        FormClosing += (s, ev) =>
        {
            timer.Stop();
            settings.Save();
            foreach (var r in runners) r.Shutdown();
        };
    }

    void Build()
    {
        // ---- header: name, version, author, summary, tutorial button
        var header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.Bg };
        var title = Ui.Txt("Yuzu Motion X", Theme.Text, 24f, true);
        title.Location = new Point(14, 6);
        var ver = Ui.Txt("Version 1.0     ·     By NeanX", Theme.Accent, 10.5f, true);
        ver.Location = new Point(17, 52);
        var sum = Ui.Txt(TutorialForm.Summary, Theme.Sub, 9.5f);
        sum.MaximumSize = new Size(720, 0);
        sum.Location = new Point(17, 76);
        var tut = Ui.Btn("Tutorial", (s, ev) => ShowTutorial(), true);
        tut.Font = new Font("Segoe UI Semibold", 11f);
        tut.Padding = new Padding(16, 6, 16, 6);
        tut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tut.Location = new Point(header.Width - 140, 14);
        header.Controls.Add(title);
        header.Controls.Add(ver);
        header.Controls.Add(sum);
        header.Controls.Add(tut);
        header.Resize += (s, ev) => tut.Location = new Point(header.Width - tut.Width - 20, 14);

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

        // ---- the page of the selected player (scrolls if the window is small)
        pageHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };

        // the fill control is added first so the top controls dock above it
        Controls.Add(pageHost);
        Controls.Add(strip);
        Controls.Add(header);
    }

    void ShowTutorial()
    {
        if (tutorial == null || tutorial.IsDisposed) tutorial = new TutorialForm();
        if (!tutorial.Visible) tutorial.Show(this);
        tutorial.BringToFront();
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
