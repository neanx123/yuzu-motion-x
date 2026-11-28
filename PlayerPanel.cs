using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>
/// One player's page: ON/OFF, the device type (Controller / Mouse / Keyboard Motion), the physical device,
/// the server:port address, and below it the full settings screen of the chosen device type.
/// </summary>
public class PlayerPanel : UserControl
{
    readonly PlayerRunner runner;
    readonly PlayerSettings S;
    readonly UiCtx ctx = new UiCtx();
    readonly Dictionary<string, MotionPanel> panels = new Dictionary<string, MotionPanel>();

    CheckBox onBox;
    TextBox hostBox, portBox;
    Label status;
    ComboBox deviceCombo;
    readonly List<string> devPaths = new List<string>();
    readonly List<string> devNames = new List<string>();
    FlowLayoutPanel deviceHost;
    string lastError = "";
    bool stacked;

    static readonly string[] DeviceNames = { "Controller", "Mouse", "Keyboard Motion (experimental)" };

    public PlayerPanel(PlayerRunner runner)
    {
        this.runner = runner;
        S = runner.S;

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        Margin = new Padding(0);

        Build();
        ctx.SyncAll();
        ShowDevice();
    }

    int N => runner.Index + 1;

    void Build()
    {
        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Bg,
        };
        Controls.Add(root);

        var card = Ui.CardBig(root, "PLAYER " + N + " - DEVICE AND SERVER", 590);

        // ---- 1. ON / OFF
        var r1 = Ui.Row(card);
        onBox = new CheckBox
        {
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 34),
            Text = "Player is OFF",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 10f),
            BackColor = Theme.Card2,
            ForeColor = Theme.Text,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 14, 0),
        };
        onBox.FlatAppearance.BorderSize = 0;
        onBox.FlatAppearance.CheckedBackColor = Theme.Good;
        onBox.CheckedChanged += (s, ev) =>
        {
            onBox.Text = onBox.Checked ? "Player is ON" : "Player is OFF";
            onBox.ForeColor = onBox.Checked ? Theme.AccentText : Theme.Text;
            if (!ctx.Updating) OnToggled();
        };
        ctx.Syncs.Add(() =>
        {
            onBox.Checked = S.On;
            onBox.Text = S.On ? "Player is ON" : "Player is OFF";
            onBox.ForeColor = S.On ? Theme.AccentText : Theme.Text;
        });
        r1.Controls.Add(onBox);

        // ---- 2. the device type
        var r2 = Ui.Row(card);
        r2.Margin = new Padding(0, 8, 0, 0);
        string[] devKeys = AppSettings.Devices;
        for (int i = 0; i < devKeys.Length; i++)
        {
            string key = devKeys[i];
            var rb = Ui.Toggle(r2, DeviceNames[i], key == "Keyboard" ? 215 : 110, 34, 9.5f);
            rb.CheckedChanged += (s, ev) =>
            {
                if (!ctx.Updating && rb.Checked && S.Device != key)
                {
                    S.Device = key;
                    ShowDevice();
                    AppSettings.Current?.Save();
                }
            };
            ctx.Syncs.Add(() => rb.Checked = S.Device == key);
        }

        // ---- 3. the physical device (below the device type buttons)
        var r3 = Ui.Row(card);
        r3.Margin = new Padding(0, 10, 0, 0);
        r3.Controls.Add(Ui.Txt("Device:", Theme.Text, 9.5f, true, 95));
        deviceCombo = Ui.NewCombo(340);
        deviceCombo.SelectionChangeCommitted += OnDeviceCommitted;
        r3.Controls.Add(deviceCombo);
        r3.Controls.Add(Ui.Btn("Refresh list", (s, ev) => RefreshDevices()));

        // ---- 4. server : port
        var r4 = Ui.Row(card);
        r4.Margin = new Padding(0, 10, 0, 0);
        r4.Controls.Add(Ui.Txt("Server : port", Theme.Text, 9.5f, true, 95));
        hostBox = new TextBox { Width = 130, Text = S.Host, Margin = new Padding(0, 4, 4, 0) };
        portBox = new TextBox { Width = 70, Text = S.Port.ToString(), Margin = new Padding(4, 4, 10, 0) };
        r4.Controls.Add(hostBox);
        r4.Controls.Add(Ui.Txt(":", Theme.Text, 11f, true, 12));
        r4.Controls.Add(portBox);

        // ---- 5. Apply / Default, below the input boxes
        var r5 = Ui.Row(card);
        r5.Margin = new Padding(95, 4, 0, 0);
        r5.Controls.Add(Ui.Btn("Apply", (s, ev) => ApplyAddress(), true));
        r5.Controls.Add(Ui.Btn("Default (127.0.0.1:" + PlayerSettings.DefaultPort(runner.Index) + ")", (s, ev) => DefaultAddress()));

        var pointer = Ui.Txt("What is Server:Port? Open the Setup Guide (top right).", Theme.Sub, 8.5f);
        pointer.Margin = new Padding(0, 6, 0, 0);
        card.Controls.Add(pointer);

        status = Ui.Txt("", Theme.Sub, 9.5f, true);
        status.MaximumSize = new Size(560, 0);
        status.Margin = new Padding(0, 6, 0, 0);
        card.Controls.Add(status);

        // ---- the device screens (created the first time they are needed)
        deviceHost = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Bg,
            Margin = new Padding(0),
        };
        root.Controls.Add(deviceHost);
    }

    MotionPanel GetPanel(string device)
    {
        if (!panels.TryGetValue(device, out MotionPanel p))
        {
            p = new MotionPanel(runner, device);
            panels[device] = p;
            deviceHost.Controls.Add(p);
            p.SetStacked(stacked);
            UiScale.Apply(p, UiScale.Factor);        // a new screen starts at the zoom that is active now
        }
        return p;
    }

    void ShowDevice()
    {
        MotionPanel current = GetPanel(S.Device);
        foreach (var kv in panels) kv.Value.Visible = kv.Key == S.Device;
        current.SyncAll();
        RefreshDevices();
    }

    /// <summary>Called by the main window when the window size / zoom changes.</summary>
    public void ApplyLayout(double zoom, bool stackedColumns)
    {
        stacked = stackedColumns;
        foreach (var kv in panels) kv.Value.SetStacked(stackedColumns);
        UiScale.Apply(this, zoom);
    }

    /// <summary>Fills the Device list for the chosen device type, with the full device names.</summary>
    void RefreshDevices()
    {
        deviceCombo.Items.Clear();
        devPaths.Clear();
        devNames.Clear();
        int select = 0;

        if (S.Device == "Controller")
        {
            string[] names = Devices.PadNames();
            for (int i = 0; i < 4; i++) deviceCombo.Items.Add("Pad " + (i + 1) + ":  " + names[i]);
            select = Math.Clamp(S.Controller.PadIndex, 1, 4) - 1;
        }
        else
        {
            bool mouse = S.Device == "Mouse";
            deviceCombo.Items.Add(mouse ? "All mice (any mouse)" : "All keyboards (any keyboard)");
            devPaths.Add("");
            devNames.Add("");
            foreach (DeviceInfo d in (mouse ? Devices.Mice() : Devices.Keyboards()))
            {
                deviceCombo.Items.Add(d.Name);
                devPaths.Add(d.Path);
                devNames.Add(d.Name);
            }
            Config c = S.For(S.Device);
            if (!string.IsNullOrEmpty(c.DevicePath))
            {
                int idx = devPaths.FindIndex(x => string.Equals(x, c.DevicePath, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    // the chosen device is not plugged in right now: keep showing it
                    deviceCombo.Items.Add((string.IsNullOrEmpty(c.DeviceName) ? "Selected device" : c.DeviceName) + " (not connected)");
                    devPaths.Add(c.DevicePath);
                    devNames.Add(c.DeviceName ?? "");
                    idx = devPaths.Count - 1;
                }
                select = idx;
            }
        }
        deviceCombo.SelectedIndex = select < deviceCombo.Items.Count ? select : 0;
    }

    void OnDeviceCommitted(object sender, EventArgs ev)
    {
        int i = deviceCombo.SelectedIndex;
        if (i < 0) return;
        if (S.Device == "Controller")
        {
            S.Controller.PadIndex = i + 1;
        }
        else if (i < devPaths.Count)
        {
            Config c = S.For(S.Device);
            c.DevicePath = devPaths[i];
            c.DeviceName = devNames[i];
        }
        AppSettings.Current?.Save();
    }

    MotionPanel ActivePanel => panels.TryGetValue(S.Device, out MotionPanel p) ? p : null;

    // ------------------------------------------------------------------ ON/OFF and address
    void OnToggled()
    {
        if (onBox.Checked)
        {
            if (runner.StartServer(out string err))
            {
                S.On = true;
                lastError = "";
            }
            else
            {
                lastError = "Could not start on " + S.Host + ":" + S.Port + " - " + err +
                            "  (is the port already used by another player or program?)";
                ctx.Quiet(() => onBox.Checked = false);
                onBox.Text = "Player is OFF";
                onBox.ForeColor = Theme.Text;
                S.On = false;
            }
        }
        else
        {
            S.On = false;
            runner.StopServer();
            lastError = "";
        }
        AppSettings.Current?.Save();
    }

    void ApplyAddress()
    {
        string host = hostBox.Text.Trim();
        if (!System.Net.IPAddress.TryParse(host, out _))
        {
            MessageBox.Show(FindForm(), "The server must be an IP address like 127.0.0.1.", "Yuzu Motion X",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!int.TryParse(portBox.Text.Trim(), out int port) || port < 1024 || port > 65535)
        {
            MessageBox.Show(FindForm(), "The port must be a number from 1024 to 65535 (for example " + PlayerSettings.DefaultPort(runner.Index) + ").",
                            "Yuzu Motion X", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        S.Host = host;
        S.Port = port;
        if (S.On)
        {
            runner.StopServer();
            if (!runner.StartServer(out string err))
            {
                lastError = "Could not start on " + S.Host + ":" + S.Port + " - " + err;
                S.On = false;
                ctx.Quiet(() => onBox.Checked = false);
                onBox.Text = "Player is OFF";
                onBox.ForeColor = Theme.Text;
            }
            else lastError = "";
        }
        AppSettings.Current?.Save();
    }

    void DefaultAddress()
    {
        hostBox.Text = "127.0.0.1";
        portBox.Text = PlayerSettings.DefaultPort(runner.Index).ToString();
        ApplyAddress();
    }

    // ------------------------------------------------------------------ called by the main window
    public void SetShown(bool shown)
    {
        runner.PreviewActive = shown;
        Visible = shown;
    }

    public void Tick()
    {
        LiveStatus live = runner.Live;
        string info = live.DeviceInfo;
        if (S.Device == "Controller" && deviceCombo.SelectedIndex >= 0 && info.IndexOf("not connected") < 0) info = deviceCombo.Text;
        if (lastError.Length > 0)
        {
            status.ForeColor = Theme.Red;
            status.Text = lastError;
        }
        else if (!S.On)
        {
            status.ForeColor = Theme.Sub;
            status.Text = "OFF - this player is not sending anything yet. Turn it ON to start the server on " + S.Host + ":" + S.Port + ".   (" + info + ")";
        }
        else
        {
            status.ForeColor = live.Clients > 0 ? Theme.Good : Theme.Warn;
            status.Text = "ON - listening on " + S.Host + ":" + S.Port + "  -  " + live.Clients + " yuzu client(s) connected" +
                          (live.Active ? "" : "  -  mouse motion PAUSED") + "   (" + info + ")";
        }
        ActivePanel?.Tick();
    }
}
