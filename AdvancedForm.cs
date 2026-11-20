using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>The less-used options of one device of one player, kept out of sight in their own window.</summary>
public class AdvancedForm : Form
{
    readonly Config cfg;
    readonly UiCtx ctx = new UiCtx();
    readonly List<Control> profileCards = new List<Control>();   // greyed out while DEFAULT is active
    public event Action RequestSync;

    public AdvancedForm(int playerIndex, string kind, Config cfg)
    {
        this.cfg = cfg;
        Text = "Yuzu Motion X - Player " + (playerIndex + 1) + " - Advanced (" + kind + ")";
        Icon = AppIcon.Get();
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(580, 660);
        MinimumSize = new Size(520, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(6),
            BackColor = Theme.Bg,
        };
        Controls.Add(scroll);
        const int W = 520;

        // ---- 1. keys / buttons of this device (global for the device: always editable, not part of profiles)
        FlowLayoutPanel c;
        if (kind == "Controller")
        {
            c = Ui.Card(scroll, "Controller buttons (not part of profiles)", W);
            ctx.Combo(c, "Recenter button", GamepadReader.ButtonNames, () => cfg.RecenterButton, v => cfg.RecenterButton = v);
            ctx.Combo(c, "Shake button (hold)", GamepadReader.ButtonNames, () => cfg.ShakeButton, v => cfg.ShakeButton = v);
        }
        else if (kind == "Mouse")
        {
            c = Ui.Card(scroll, "Mouse motion on/off and keys (not part of profiles)", W);
            ctx.Combo(c, "Mouse motion is", Config.ActivationModes, () => cfg.ActivationMode, v => cfg.ActivationMode = v);
            ctx.Combo(c, "On/off key", KeyState.Names, () => cfg.ActivationKey, v => cfg.ActivationKey = v);
            ctx.Combo(c, "Recenter key", KeyState.Names, () => cfg.RecenterKey, v => cfg.RecenterKey = v);
            ctx.Combo(c, "Shake key (hold)", KeyState.Names, () => cfg.ShakeKey, v => cfg.ShakeKey = v);
            ctx.Check(c, "Freeze the cursor while mouse motion is on", () => cfg.LockCursor, v => cfg.LockCursor = v);
            Note(c, "Always on = the mouse always moves the controller. Toggle = the key switches it on/off. Hold = only while the key is held. " +
                    "Freezing the cursor only works with Toggle or Hold, so you can always use the mouse normally. " +
                    "Pick which physical mouse this player listens to in the Device list of the player's tab.");
        }
        else
        {
            c = Ui.Card(scroll, "Keyboard keys (not part of profiles)", W);
            ctx.Combo(c, "Recenter key", KeyState.Names, () => cfg.RecenterKey, v => cfg.RecenterKey = v);
            ctx.Combo(c, "Shake key (hold)", KeyState.Names, () => cfg.ShakeKey, v => cfg.ShakeKey = v);
            Note(c, "The keys of the points themselves are set in the circle on the main window (click inside it to add a point).");
        }

        // ---- 2. feel
        if (kind == "Controller")
        {
            c = Ui.Card(scroll, "Stick", W);
            profileCards.Add(c);
            ctx.Combo(c, "Stick to use", Config.Sources, () => cfg.Source, v => cfg.Source = v);
            ctx.Combo(c, "Left/right stick does", Config.XTargets, () => cfg.XTarget, v => cfg.XTarget = v);
            ctx.Slider(c, "Dead zone", 0.0, 0.5, () => cfg.Deadzone, v => cfg.Deadzone = v, "0.00");
            ctx.Slider(c, "Response curve (1 = linear)", 0.5, 3.0, () => cfg.Curve, v => cfg.Curve = v, "0.00");
            ctx.Slider(c, "Max angle from neutral", 5, 180, () => cfg.RangeLimit, v => cfg.RangeLimit = v, "0' deg'");
            ctx.Slider(c, "Return speed (after letting go)", 0.2, 3.0, () => cfg.ReturnSpeed, v => cfg.ReturnSpeed = v, "0.00");
            ctx.Check(c, "Triggers change speed (LT = half, RT = 5x)", () => cfg.TriggerShift, v => cfg.TriggerShift = v);
            Note(c, "'Auto' turns left/right into a turn when the Joy-Con is held vertically, and into a lean otherwise.");
        }
        else
        {
            c = Ui.Card(scroll, kind == "Mouse" ? "Mouse feel" : "Keyboard feel", W);
            profileCards.Add(c);
            ctx.Combo(c, kind == "Mouse" ? "Left/right mouse does" : "Left/right points do", Config.XTargets, () => cfg.XTarget, v => cfg.XTarget = v);
            if (kind == "Mouse") ctx.Slider(c, "Dead zone", 0.0, 0.5, () => cfg.Deadzone, v => cfg.Deadzone = v, "0.00");
            ctx.Slider(c, "Response curve (1 = linear)", 0.5, 3.0, () => cfg.Curve, v => cfg.Curve = v, "0.00");
            ctx.Slider(c, "Max angle from neutral", 5, 180, () => cfg.RangeLimit, v => cfg.RangeLimit = v, "0' deg'");
            Note(c, kind == "Mouse"
                ? "'Auto' turns left/right into a turn when the Joy-Con is held vertically, and into a lean otherwise. " +
                  "Tilt and Swing: the mouse pushes a spring-loaded stick. Gyro and Shake: your mouse speed is the stick."
                : "'Auto' turns left/right into a turn when the Joy-Con is held vertically, and into a lean otherwise. " +
                  "Timing (reach, return, max press time) is set per point: select a point in the circle.");
        }

        // ---- 3. shake
        c = Ui.Card(scroll, "Shake", W);
        profileCards.Add(c);
        ctx.Combo(c, "Shake direction", Config.ShakeDirs, () => cfg.ShakeDir, v => cfg.ShakeDir = v);

        // ---- 4. neutral custom angles
        c = Ui.Card(scroll, "Neutral position - custom angles", W);
        profileCards.Add(c);
        ctx.Slider(c, "Pitch (tip forward/back)", -180, 180, () => cfg.NeutralPitch, v => { cfg.NeutralPitch = v; cfg.NeutralPreset = "Custom"; }, "0' deg'");
        ctx.Slider(c, "Yaw (turn)", -180, 180, () => cfg.NeutralYaw, v => { cfg.NeutralYaw = v; cfg.NeutralPreset = "Custom"; }, "0' deg'");
        ctx.Slider(c, "Roll (lean sideways)", -180, 180, () => cfg.NeutralRoll, v => { cfg.NeutralRoll = v; cfg.NeutralPreset = "Custom"; }, "0' deg'");

        // ---- 5. hold style
        c = Ui.Card(scroll, "Joy-Con hold style (Pro Controller ignores this)", W);
        profileCards.Add(c);
        ctx.Combo(c, "Hold style", Config.HoldStyles, () => cfg.HoldStyle, v => cfg.HoldStyle = v);
        Note(c, "Try 'Sideways' if a game that holds a single Joy-Con sideways ignores tilt.");

        // ---- 6. fix wrong motion
        c = Ui.Card(scroll, "Fix wrong motion (only if a game moves the wrong way)", W);
        profileCards.Add(c);
        AddAxis(c, "Pitch (forward/back)", () => cfg.PitchOn, v => cfg.PitchOn = v, () => cfg.PitchInv, v => cfg.PitchInv = v);
        AddAxis(c, "Yaw (turn)", () => cfg.YawOn, v => cfg.YawOn = v, () => cfg.YawInv, v => cfg.YawInv = v);
        AddAxis(c, "Roll (lean sideways)", () => cfg.RollOn, v => cfg.RollOn = v, () => cfg.RollInv, v => cfg.RollInv = v);
        var flips = Ui.Row(c);
        flips.Controls.Add(Ui.Txt("If the pose fights itself, flip gyro:", Theme.Text, 9f, false, 235));
        ctx.Check(flips, "Pitch", () => cfg.PitchFlip, v => cfg.PitchFlip = v);
        ctx.Check(flips, "Yaw", () => cfg.YawFlip, v => cfg.YawFlip = v);
        ctx.Check(flips, "Roll", () => cfg.RollFlip, v => cfg.RollFlip = v);
        foreach (Control ch in flips.Controls) ch.Margin = new Padding(0, 3, 10, 3);
        ctx.Combo(c, "Gravity", new[] { "Follows the pose", "Fixed at neutral" },
                  () => cfg.AccelMode == "fixed" ? "Fixed at neutral" : "Follows the pose",
                  v => cfg.AccelMode = v.StartsWith("Fixed") ? "fixed" : "follow");
        ctx.Check(c, "Invert gravity (pose looks upside down in the game)", () => cfg.InvertGravity, v => cfg.InvertGravity = v);

        // ---- 7. program
        c = Ui.Card(scroll, "Program", W);
        Note(c, "Settings file: " + AppSettings.FilePath);
        var reset = Ui.Btn("Reset this device to its DEFAULT profile", (s, ev) =>
        {
            if (MessageBox.Show(this, "Reset every setting of this device to its DEFAULT profile?", "Yuzu Motion X",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            cfg.ApplyProfile(Config.DefaultProfileName);
            ctx.SyncAll();
            RequestSync?.Invoke();
        });
        reset.Margin = new Padding(0, 8, 0, 0);
        c.Controls.Add(reset);

        ctx.SyncAll();

        FormClosing += (s, ev) =>
        {
            if (ev.CloseReason == CloseReason.UserClosing)
            {
                ev.Cancel = true;
                Hide();
            }
        };
    }

    public void SyncAll() { ctx.SyncAll(); }

    /// <summary>While DEFAULT is active the profile settings here are greyed out (keys/buttons stay editable).</summary>
    public void SetProfileLocked(bool locked)
    {
        foreach (Control card in profileCards) card.Enabled = !locked;
    }

    static void Note(Control parent, string text)
    {
        var l = Ui.Txt(text, Theme.Sub, 8.5f);
        l.MaximumSize = new Size(490, 0);
        l.Margin = new Padding(0, 6, 0, 0);
        parent.Controls.Add(l);
    }

    /// <summary>One drop-down per axis: Normal / Reversed / Off.</summary>
    void AddAxis(Control parent, string label, Func<bool> getOn, Action<bool> setOn,
                 Func<bool> getInv, Action<bool> setInv)
    {
        ctx.Combo(parent, label, new[] { "Normal", "Reversed", "Off" },
                  () => !getOn() ? "Off" : (getInv() ? "Reversed" : "Normal"),
                  v => { setOn(v != "Off"); setInv(v == "Reversed"); },
                  150, 120);
    }
}
