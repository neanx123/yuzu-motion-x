using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>
/// The full settings screen of ONE device (Controller, Mouse or Keyboard) of ONE player:
/// live preview, profiles (locked DEFAULT, green * profiles, your own), motion style, sliders, neutral position, tests.
/// </summary>
public class MotionPanel : UserControl
{
    readonly PlayerRunner runner;
    readonly string kind;
    readonly Config cfg;
    readonly MotionEngine eng;
    readonly UiCtx ctx = new UiCtx();
    readonly UiCtx dotCtx = new UiCtx();
    AdvancedForm adv;

    ControllerView view;
    PadView pad;
    KeyPadView keypad;
    BarsView bars;
    Label desc, saveMsg, noteMain, noteGood, keyLabel;
    ComboBox profileCombo, presetCombo;
    FlowLayoutPanel strengthRow, hzRow, profileRow2, dotCard;
    Button btnSaveChanges, btnSaveNew, btnDelete, btnReset;

    /// <summary>Everything that changes profile settings. All of it is greyed out while DEFAULT is active.</summary>
    readonly List<Control> lockables = new List<Control>();

    string lastPreset = "";
    int tickCount;
    bool profileModified;

    readonly string[] modeKeys = { "tilt", "gyro", "shake", "swing" };
    readonly string[] modeNames = { "Tilt", "Gyro", "Shake", "Swing" };
    readonly string[] modeDesc =
    {
        "TILT - the stick angle becomes the controller angle. Best for balance and tilt minigames.",
        "GYRO - the stick turns the controller like a real gyro. Best for aiming.",
        "SHAKE - push the stick (or hold the Shake key/button) to shake the controller back and forth.",
        "SWING - flick the stick for a fast swing or jab. Best for racket, sword and throwing games.",
    };

    bool Locked => cfg.ActiveProfile == Config.DefaultProfileName;
    KeyDot Sel => keypad?.SelectedDot;

    public MotionPanel(PlayerRunner runner, string kind)
    {
        this.runner = runner;
        this.kind = kind;
        cfg = runner.S.For(kind);
        eng = runner.Engines[kind];

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f);
        Margin = new Padding(0);

        Build();
        RefreshProfiles();
        SyncAll();
    }

    public void SyncAll()
    {
        ctx.SyncAll();
        ctx.Quiet(() =>
        {
            string cur = cfg.ActiveProfile ?? "";
            if (profileCombo != null && profileCombo.Items.Contains(cur)) profileCombo.SelectedItem = cur;
        });
        RebuildPresets();
        UpdateModeUi();
        profileModified = false;
        if (keypad != null) keypad.Selected = -1;
        UpdateProfileUi();
    }

    // ------------------------------------------------------------------ layout
    RadioButton MakeToggle(Control row, string text, int w, int h, float size, Func<bool> isChecked, Action onUserCheck)
    {
        var rb = Ui.Toggle(row, text, w, h, size);
        rb.CheckedChanged += (s, ev) =>
        {
            if (!ctx.Updating && rb.Checked && !Locked) onUserCheck();
        };
        ctx.Syncs.Add(() => rb.Checked = isChecked());
        lockables.Add(rb);
        return rb;
    }

    void Build()
    {
        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Bg,
        };
        Controls.Add(root);
        var leftCol = Ui.Column();
        var rightCol = Ui.Column();
        root.Controls.Add(leftCol);
        root.Controls.Add(rightCol);

        // ---------------------------------------------------------------- live preview
        var pv = Ui.Card(leftCol, "Live preview", 400);
        view = new ControllerView();
        pv.Controls.Add(view);
        var lower = Ui.Row(pv);
        if (kind == "Keyboard")
        {
            keypad = new KeyPadView(250);
            keypad.GetDots = () => cfg.Dots;
            keypad.GetLevel = d => runner.KeyPoints.GetLevel(d);
            keypad.AddRequested += OnAddRequested;
            keypad.SelectionChanged += () => UpdateDotCard();
            lower.Controls.Add(keypad);
        }
        else
        {
            pad = new PadView(130);
            pad.Caption = kind == "Mouse" ? "mouse stick / drag" : "analog / drag";
            pad.StickMoved += (x, y) => { runner.VX = x; runner.VY = y; runner.VirtualActive = true; };
            pad.StickReleased += () => runner.VirtualActive = false;
            lower.Controls.Add(pad);
        }
        bars = new BarsView();
        lower.Controls.Add(bars);

        var typeRow = Ui.Row(pv);
        typeRow.Margin = new Padding(0, 10, 0, 0);
        string[] typeNames = { "Joy-Con L", "Joy-Con R", "Pro Controller" };
        for (int i = 0; i < Config.ControllerTypes.Length; i++)
        {
            string key = Config.ControllerTypes[i];
            MakeToggle(typeRow, typeNames[i], key == "Pro Controller" ? 120 : 110, 32, 9.5f,
                       () => cfg.ControllerType == key,
                       () => OnControllerType(key));
        }
        string hintText =
            kind == "Mouse"
                ? "The circle is the virtual stick your mouse pushes (hollow ring = raw, dot = used). Dashed outline = neutral position. " +
                  "Drag inside the circle to test without moving the real mouse."
                : kind == "Keyboard"
                    ? "EXPERIMENTAL. Click inside the circle to add a point and press its key. Click a point to edit it, drag it to move it, " +
                      "right-click it to delete it. The hollow ring is the live stick. Dashed outline = neutral position."
                    : "Hollow ring = the stick you push. Dashed outline = neutral position. Drag inside the circle to use the mouse as a stick.";
        var hint = Ui.Txt(hintText, Theme.Sub, 8.5f);
        hint.MaximumSize = new Size(370, 0);
        hint.Margin = new Padding(0, 8, 0, 0);
        pv.Controls.Add(hint);

        // ---------------------------------------------------------------- profile
        var pc = Ui.Card(rightCol, "Profile (settings for a game)", 480);
        profileCombo = Ui.NewCombo(450);
        profileCombo.DrawMode = DrawMode.OwnerDrawFixed;
        profileCombo.ItemHeight = 22;
        profileCombo.DrawItem += DrawProfileItem;
        profileCombo.SelectionChangeCommitted += OnProfileCommitted;
        pc.Controls.Add(profileCombo);

        var prow1 = Ui.Row(pc);
        btnSaveChanges = Ui.Btn("Save Changes", (s, ev) => SaveChanges());
        btnSaveNew = Ui.Btn("Save new profile from it", (s, ev) => SaveNewProfile());
        prow1.Controls.Add(btnSaveChanges);
        prow1.Controls.Add(btnSaveNew);
        profileRow2 = Ui.Row(pc);
        btnDelete = Ui.Btn("Delete profile", (s, ev) => DeleteProfile());
        btnReset = Ui.Btn("Reset to DEFAULT", (s, ev) => ResetToDefault());
        profileRow2.Controls.Add(btnDelete);
        profileRow2.Controls.Add(btnReset);

        noteMain = Ui.Txt("", Theme.Sub, 8.5f);
        noteMain.MaximumSize = new Size(450, 0);
        noteMain.Margin = new Padding(0, 8, 0, 0);
        pc.Controls.Add(noteMain);
        noteGood = Ui.Txt("", Theme.Good, 8.5f);
        noteGood.MaximumSize = new Size(450, 0);
        noteGood.Margin = new Padding(0, 4, 0, 0);
        pc.Controls.Add(noteGood);

        // ---------------------------------------------------------------- keyboard: selected point submenu
        if (kind == "Keyboard") BuildDotCard(rightCol);

        // ---------------------------------------------------------------- motion style
        var sc = Ui.Card(rightCol, "Motion style", 480);
        var mrow = Ui.Row(sc);
        for (int i = 0; i < modeKeys.Length; i++)
        {
            string key = modeKeys[i];
            MakeToggle(mrow, modeNames[i], 102, 38, 10f,
                       () => Config.NormMode(cfg.Mode) == key,
                       () => { cfg.Mode = key; UpdateModeUi(); });
        }
        desc = Ui.Txt("", Theme.Sub, 9f);
        desc.MaximumSize = new Size(450, 0);
        desc.Margin = new Padding(0, 8, 0, 0);
        sc.Controls.Add(desc);

        // ---------------------------------------------------------------- adjustments
        var ac = Ui.Card(rightCol, "Adjust", 480);
        if (kind == "Mouse")
            lockables.Add(ctx.Slider(ac, "Mouse sensitivity", 0.2, 5.0, () => cfg.MouseSensitivity, v => cfg.MouseSensitivity = v, "0.00"));
        lockables.Add(ctx.Slider(ac, kind == "Controller" ? "Sensitivity (how far)" : "Tilt range (how far)", 0.1, 4.0,
                                 () => cfg.Sensitivity, v => cfg.Sensitivity = v, "0.00"));
        lockables.Add(ctx.Slider(ac, "Speed (how fast)", 0.1, 4.0, () => cfg.Speed, v => cfg.Speed = v, "0.00"));
        lockables.Add(ctx.Slider(ac, "Stability (smoothing)", 0, 500, () => cfg.SmoothingMs, v => cfg.SmoothingMs = v, "0' ms'"));
        if (kind == "Mouse")
            lockables.Add(ctx.Slider(ac, "Centering speed", 0.1, 6.0, () => cfg.ReturnSpeed, v => cfg.ReturnSpeed = v, "0.00"));
        strengthRow = ctx.Slider(ac, "Shake strength (g)", 0.5, 8.0, () => cfg.ShakeStrength, v => cfg.ShakeStrength = v, "0.0");
        hzRow = ctx.Slider(ac, "Shake speed (Hz)", 2.0, 15.0, () => cfg.ShakeHz, v => cfg.ShakeHz = v, "0.0");
        lockables.Add(strengthRow);
        lockables.Add(hzRow);

        if (kind != "Keyboard")
        {
            var rrow = Ui.Row(ac);
            rrow.Margin = new Padding(0, 8, 0, 0);
            rrow.Controls.Add(Ui.Txt(kind == "Mouse" ? "When I stop moving the mouse:" : "When I let go of the stick:",
                                     Theme.Text, 9f, false, 185));
            MakeToggle(rrow, "Return to neutral", 125, 30, 9f, () => cfg.ReturnToNeutral, () => cfg.ReturnToNeutral = true);
            MakeToggle(rrow, "Stay where it stopped", 145, 30, 9f, () => !cfg.ReturnToNeutral, () => cfg.ReturnToNeutral = false);
        }

        // ---------------------------------------------------------------- neutral position
        var nc = Ui.Card(rightCol, "Neutral position", 480);
        var nrow = Ui.Row(nc);
        presetCombo = Ui.NewCombo(240);
        presetCombo.SelectionChangeCommitted += OnPresetCommitted;
        lockables.Add(presetCombo);
        nrow.Controls.Add(presetCombo);
        nrow.Controls.Add(Ui.Btn("Recenter", (s, ev) => eng.Request("recenter")));
        var useCurrent = Ui.Btn("Use current pose", (s, ev) => { if (!Locked) eng.Request("capture"); });
        lockables.Add(useCurrent);
        nrow.Controls.Add(useCurrent);

        // ---------------------------------------------------------------- tests
        var tc = Ui.Card(rightCol, "Test it (4-second demo)", 480);
        var trow = Ui.Row(tc);
        for (int i = 0; i < modeKeys.Length; i++)
        {
            string key = modeKeys[i];
            trow.Controls.Add(Ui.Btn("Test " + modeNames[i], (s, ev) => eng.StartTest(key, 4.0)));
        }
        var th = Ui.Txt("Use these while mapping Motion in yuzu, and to see what each style does.", Theme.Sub, 8.5f);
        th.Margin = new Padding(0, 4, 0, 0);
        tc.Controls.Add(th);

        // ---------------------------------------------------------------- footer
        var frow = Ui.Row(rightCol);
        frow.Margin = new Padding(6, 2, 6, 2);
        frow.Controls.Add(Ui.Btn("Advanced settings...", (s, ev) => ToggleAdvanced()));
        saveMsg = Ui.Txt("", Theme.Good, 9f);
        saveMsg.Margin = new Padding(0, 9, 0, 0);
        frow.Controls.Add(saveMsg);
    }

    // ------------------------------------------------------------------ keyboard point submenu
    void BuildDotCard(Control parent)
    {
        dotCard = Ui.Card(parent, "Selected point - submenu (click a point in the circle)", 480);

        var krow = Ui.Row(dotCard);
        krow.Controls.Add(Ui.Txt("Key", Theme.Text, 9f, false, 150));
        keyLabel = Ui.Txt("-", Theme.Warn, 11f, true, 110);
        krow.Controls.Add(keyLabel);
        krow.Controls.Add(Ui.Btn("Change key...", (s, ev) => ChangeKey()));

        dotCtx.Combo(dotCard, "Behavior", Config.Behaviors, () => Sel?.Behavior ?? "Hold", v => { if (Sel != null) Sel.Behavior = v; });
        dotCtx.Slider(dotCard, "Left/right position", -100, 100, () => (Sel?.X ?? 0) * 100, v => { if (Sel != null) Sel.X = Math.Round(v / 100, 2); }, "0' %'");
        dotCtx.Slider(dotCard, "Up/down position", -100, 100, () => (Sel?.Y ?? 0) * 100, v => { if (Sel != null) Sel.Y = Math.Round(v / 100, 2); }, "0' %'");
        dotCtx.Slider(dotCard, "Strength (how far)", 5, 100, () => Sel?.Strength ?? 100, v => { if (Sel != null) Sel.Strength = v; }, "0' %'");
        dotCtx.Slider(dotCard, "Time to reach the point", 0, 2000, () => Sel?.ReachMs ?? 0, v => { if (Sel != null) Sel.ReachMs = v; }, "0' ms'");
        dotCtx.Slider(dotCard, "Time to come back", 0, 2000, () => Sel?.ReleaseMs ?? 0, v => { if (Sel != null) Sel.ReleaseMs = v; }, "0' ms'");
        dotCtx.Slider(dotCard, "Max press time (0 = no limit)", 0, 5000, () => Sel?.MaxHoldMs ?? 0, v => { if (Sel != null) Sel.MaxHoldMs = v; }, "0' ms'");
        dotCtx.Check(dotCard, "Smooth start and stop", () => Sel?.Ease ?? true, v => { if (Sel != null) Sel.Ease = v; });
        dotCard.Controls.Add(Ui.Btn("Delete this point", (s, ev) => DeleteDot()));

        var note = Ui.Txt("Hold = the point is active while the key is pressed.  Toggle = press once to go there, press again to come back.  " +
                          "Pulse = a tap goes there and comes back after the max press time.  Several keys at once add up.",
                          Theme.Sub, 8.5f);
        note.MaximumSize = new Size(450, 0);
        note.Margin = new Padding(0, 6, 0, 0);
        dotCard.Controls.Add(note);
    }

    void UpdateDotCard()
    {
        if (dotCard == null) return;
        dotCard.Enabled = Sel != null && !Locked;
        keyLabel.Text = Sel?.Key ?? "-";
        dotCtx.SyncAll();
        keypad?.Invalidate();
    }

    void OnAddRequested(double x, double y)
    {
        if (Locked) return;
        string key = KeyCaptureForm.Ask(FindForm(), "Press the key you want for this new point");
        if (key == null) return;
        cfg.Dots.Add(new KeyDot { Key = key, X = Math.Round(x, 2), Y = Math.Round(y, 2) });
        keypad.Selected = cfg.Dots.Count - 1;
        UpdateDotCard();
    }

    void ChangeKey()
    {
        KeyDot d = Sel;
        if (d == null || Locked) return;
        string key = KeyCaptureForm.Ask(FindForm(), "Press the new key for this point");
        if (key == null) return;
        d.Key = key;
        UpdateDotCard();
    }

    void DeleteDot()
    {
        KeyDot d = Sel;
        if (d == null || Locked) return;
        cfg.Dots.Remove(d);
        keypad.Selected = -1;
        UpdateDotCard();
    }

    // ------------------------------------------------------------------ profile list (colours)
    Color ProfileColor(string name)
    {
        if (Config.IsDefault(name)) return Theme.Red;           // DEFAULT = red
        if (cfg.IsPremade(name)) return Theme.Good;             // made by the program = green
        return Color.FromArgb(245, 245, 245);                   // made by the user = white
    }

    void DrawProfileItem(object sender, DrawItemEventArgs ev)
    {
        if (ev.Index < 0) return;
        string name = profileCombo.Items[ev.Index] as string ?? "";
        bool hot = (ev.State & DrawItemState.Selected) != 0 && (ev.State & DrawItemState.ComboBoxEdit) == 0;
        using (var bg = new SolidBrush(hot ? Color.FromArgb(70, 76, 90) : Theme.Card2))
            ev.Graphics.FillRectangle(bg, ev.Bounds);
        var r = new Rectangle(ev.Bounds.X + 6, ev.Bounds.Y, ev.Bounds.Width - 8, ev.Bounds.Height);
        TextRenderer.DrawText(ev.Graphics, name, profileCombo.Font, r, ProfileColor(name),
                              TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    void RefreshProfiles()
    {
        ctx.Quiet(() =>
        {
            profileCombo.Items.Clear();
            foreach (var b in cfg.BuiltinList) profileCombo.Items.Add(b.Name);       // DEFAULT first, then the * profiles
            foreach (string k in cfg.Profiles.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                profileCombo.Items.Add(k);
            string cur = cfg.ActiveProfile ?? "";
            profileCombo.SelectedItem = profileCombo.Items.Contains(cur) ? cur : Config.DefaultProfileName;
        });
    }

    /// <summary>Shows the right buttons and notes for DEFAULT / built-in / own profiles, and locks DEFAULT.</summary>
    void UpdateProfileUi()
    {
        if (btnSaveNew == null) return;
        string name = cfg.ActiveProfile ?? "";
        bool isDefault = Config.IsDefault(name);
        bool premade = cfg.IsPremade(name);
        bool own = !isDefault && !premade;

        btnSaveChanges.Visible = own;
        btnSaveNew.Visible = true;
        btnDelete.Visible = own;
        btnReset.Visible = !isDefault;
        profileRow2.Visible = own || !isDefault;

        if (isDefault)
        {
            noteMain.Font = new Font("Segoe UI", 10.5f);
            noteMain.ForeColor = Theme.Red;
            noteMain.Text = cfg.DefaultObservation;
            noteGood.Font = new Font("Segoe UI Semibold", 10.5f);
            noteGood.ForeColor = Theme.Good;
            noteGood.Text = cfg.DefaultObservationGood;
            noteGood.Visible = true;
        }
        else
        {
            noteMain.Font = new Font("Segoe UI", 8.5f);
            noteMain.ForeColor = Theme.Sub;
            noteGood.Visible = false;
            if (premade)
            {
                noteMain.Text = "Built-in profile made by the program. It never changes: press \"Save new profile from it\" to keep your own version." +
                                (profileModified ? "\nYou changed some values - they are NOT saved into this profile." : "");
            }
            else
            {
                noteMain.Text = "Your profile." +
                                (profileModified ? "\nYou have unsaved changes - press \"Save Changes\" to keep them." : "");
            }
        }

        // DEFAULT is locked: everything that changes profile settings is greyed out
        foreach (Control c in lockables) c.Enabled = !isDefault;
        if (keypad != null) keypad.ReadOnly = isDefault;
        adv?.SetProfileLocked(isDefault);
        UpdateDotCard();
    }

    // ------------------------------------------------------------------ profile actions
    void Persist() { AppSettings.Current?.Save(); }

    void ActivateProfile(string name)
    {
        if (!cfg.ApplyProfile(name)) return;
        SyncAll();
        adv?.SyncAll();
        eng.Request("recenter");
        Persist();
    }

    void OnProfileCommitted(object sender, EventArgs ev)
    {
        string name = profileCombo.SelectedItem as string;
        if (name == null) return;
        ActivateProfile(name);
        saveMsg.Text = "";
    }

    void ResetToDefault()
    {
        ActivateProfile(Config.DefaultProfileName);
        saveMsg.Text = "DEFAULT restored.";
    }

    void SaveChanges()
    {
        string name = cfg.ActiveProfile ?? "";
        if (!cfg.Profiles.ContainsKey(name)) return;
        cfg.Profiles[name] = cfg.ProfileJson();
        Persist();
        profileModified = false;
        UpdateProfileUi();
        saveMsg.Text = "Changes saved.";
    }

    void SaveNewProfile()
    {
        string cur = cfg.ActiveProfile ?? "";
        // from a built-in profile the box starts as "<name> (mine)"; from DEFAULT or your own it starts blank
        string suggestion = cfg.IsPremade(cur) ? cur.TrimStart('*').Trim() + " (mine)" : "";
        string input = Ui.Prompt(FindForm(), "Save new profile", "Name for the new profile:", suggestion);
        if (input == null) return;
        string name = Config.CleanProfileName(input);
        if (name.Length == 0) return;
        if (cfg.Profiles.ContainsKey(name) &&
            MessageBox.Show(FindForm(), "A profile named \"" + name + "\" already exists. Replace it?", "Yuzu Motion X",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        cfg.Profiles[name] = cfg.ProfileJson();      // copy of what is active right now
        cfg.ActiveProfile = name;
        RefreshProfiles();
        profileModified = false;
        UpdateProfileUi();
        Persist();
        saveMsg.Text = "New profile saved.";
    }

    void DeleteProfile()
    {
        string name = cfg.ActiveProfile ?? "";
        if (!cfg.Profiles.ContainsKey(name)) return;
        if (MessageBox.Show(FindForm(), "Delete profile \"" + name + "\"?", "Yuzu Motion X",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        cfg.Profiles.Remove(name);
        RefreshProfiles();
        ActivateProfile(Config.DefaultProfileName);
        saveMsg.Text = "Profile deleted.";
    }

    // ------------------------------------------------------------------ other behaviour
    void UpdateModeUi()
    {
        string m = Config.NormMode(cfg.Mode);
        int idx = Array.IndexOf(modeKeys, m);
        if (idx >= 0 && desc != null) desc.Text = modeDesc[idx];
        bool shaky = m == "shake" || m == "swing";
        if (strengthRow != null) strengthRow.Visible = shaky;
        if (hzRow != null) hzRow.Visible = m == "shake";
    }

    void OnControllerType(string type)
    {
        cfg.ControllerType = type;
        foreach (var p in Config.Presets)
        {
            if (p.Name == cfg.NeutralPreset && p.JoyConOnly && Config.IsPro(type))
            {
                ApplyPreset("Flat (controller on a table)");
                adv?.SyncAll();
                return;
            }
        }
        RebuildPresets();
        adv?.SyncAll();
    }

    /// <summary>Fills the neutral-position list for the current controller type.</summary>
    void RebuildPresets()
    {
        if (presetCombo == null) return;
        bool pro = Config.IsPro(cfg.ControllerType);
        ctx.Quiet(() =>
        {
            presetCombo.Items.Clear();
            foreach (var p in Config.Presets)
                if (!(pro && p.JoyConOnly)) presetCombo.Items.Add(p.Name);
            presetCombo.Items.Add("Custom");
            presetCombo.Items.Add("Captured");
            if (presetCombo.Items.Contains(cfg.NeutralPreset)) presetCombo.SelectedItem = cfg.NeutralPreset;
            else presetCombo.SelectedIndex = -1;
        });
        lastPreset = cfg.NeutralPreset;
    }

    void ApplyPreset(string name)
    {
        if (Locked) return;
        cfg.NeutralPreset = name;
        foreach (var p in Config.Presets)
        {
            if (p.Name == name)
            {
                cfg.NeutralPitch = p.Pitch;
                cfg.NeutralYaw = p.Yaw;
                cfg.NeutralRoll = p.Roll;
            }
        }
        RebuildPresets();
        adv?.SyncAll();
        eng.Request("recenter");
    }

    void OnPresetCommitted(object sender, EventArgs ev)
    {
        if (Locked) { RebuildPresets(); return; }
        string name = presetCombo.SelectedItem as string;
        if (name == null) return;
        if (name == "Custom" || name == "Captured")
        {
            cfg.NeutralPreset = name;
            lastPreset = name;
            eng.Request("recenter");
            return;
        }
        ApplyPreset(name);
    }

    void ToggleAdvanced()
    {
        if (adv == null)
        {
            adv = new AdvancedForm(runner.Index, kind, cfg);
            adv.RequestSync += () => { SyncAll(); eng.Request("recenter"); };
            adv.SetProfileLocked(Locked);
        }
        if (adv.Visible)
        {
            adv.Hide();
        }
        else
        {
            adv.SyncAll();
            adv.Show(FindForm());
        }
    }

    /// <summary>
    /// DEFAULT can never end up changed (even by tricks): if anything differs it is put straight back.
    /// For other profiles it just notices "unsaved changes".
    /// </summary>
    void CheckProfileIntegrity()
    {
        string name = cfg.ActiveProfile ?? "";
        string snap = cfg.ProfileSnapshot(name);
        if (snap.Length == 0) return;
        bool changed = cfg.ProfileJson() != snap;

        if (Config.IsDefault(name))
        {
            if (changed)
            {
                cfg.ApplyProfile(Config.DefaultProfileName);
                SyncAll();
                adv?.SyncAll();
            }
            return;
        }
        if (changed != profileModified)
        {
            profileModified = changed;
            UpdateProfileUi();
        }
    }

    /// <summary>Called ~30 times a second while this panel is on screen.</summary>
    public void Tick()
    {
        LiveStatus live = runner.Live;

        view.Pose = eng.ViewPose;
        view.Neutral = eng.ViewNeutral;
        view.Lin = eng.Lin;
        view.Kind = cfg.ControllerType;
        view.Testing = eng.Testing;
        view.Sx = eng.Fx;
        view.Sy = eng.Fy;
        double shakeG = Math.Sqrt(eng.Lin[0] * eng.Lin[0] + eng.Lin[1] * eng.Lin[1] + eng.Lin[2] * eng.Lin[2]);
        view.Title = eng.ActiveMode.ToUpperInvariant();
        view.Info = eng.DevDeg.ToString("F0") + "° from neutral   stick " +
                    eng.RawX.ToString("0.00") + ", " + eng.RawY.ToString("0.00") +
                    "   shake " + shakeG.ToString("F1") + " g";
        view.Invalidate();

        if (pad != null)
        {
            pad.Sx = eng.Fx;
            pad.Sy = eng.Fy;
            pad.RawX = eng.RawX;
            pad.RawY = eng.RawY;
            pad.Invalidate();
        }
        if (keypad != null)
        {
            keypad.LiveX = eng.RawX;
            keypad.LiveY = eng.RawY;
            keypad.Invalidate();
        }

        bars.Gyro = new double[] { live.Gyro[0], live.Gyro[1], live.Gyro[2] };
        bars.Accel = live.Accel;
        bars.Invalidate();

        tickCount++;
        if (tickCount % 15 == 0 && !profileCombo.DroppedDown && !presetCombo.DroppedDown)
            CheckProfileIntegrity();

        // keep the neutral list in step when the setting changed elsewhere (Advanced, "Use current pose").
        // Never touch the list while it is open - that was what made it jump around before.
        if (cfg.NeutralPreset != lastPreset && !presetCombo.DroppedDown)
        {
            lastPreset = cfg.NeutralPreset;
            ctx.Quiet(() =>
            {
                if (presetCombo.Items.Contains(lastPreset)) presetCombo.SelectedItem = lastPreset;
            });
        }
    }
}
