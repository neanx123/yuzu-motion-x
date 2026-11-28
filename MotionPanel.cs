using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>
/// The full settings screen of ONE device (Controller, Mouse or Keyboard) of ONE player.
/// Left: live preview and ADJUST. Right: profile, motion style, the big POSITION box, tests.
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
    Label desc, saveMsg, noteMain, noteGood, keyLabel, editHint;
    ComboBox profileCombo;
    FlowLayoutPanel rootFlow;
    CheckBox chkOverride;
    Label overrideHint;
    CheckBox moveBox;
    FlowLayoutPanel strengthRow, hzRow, profileRow2, dotCard, editRow;
    Button btnSaveChanges, btnSaveNew, btnDelete, btnReset;

    /// <summary>Everything that changes profile settings. All of it is greyed out while DEFAULT is active.</summary>
    readonly List<Control> lockables = new List<Control>();

    int tickCount;
    bool profileModified;
    bool editing;

    readonly string[] modeKeys = { "tilt", "gyro", "shake", "swing", "move" };
    readonly string[] modeNames = { "Tilt", "Gyro", "Shake", "Swing", "Move" };
    readonly string[] modeDesc =
    {
        "TILT - the stick angle becomes the controller angle. Best for balance and tilt minigames.",
        "GYRO - the stick turns the controller like a real gyro. Best for aiming.",
        "SHAKE - push the stick (or hold the Shake key/button) to shake the controller back and forth.",
        "SWING - flick the stick for a fast swing or jab. Best for racket, sword and throwing games.",
        "MOVE - the whole controller moves away from where it started: up = up, down = down, left = left, right = right, and comes back. " +
        "Tick the box below to turn up/down into forward/backward.",
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

    /// <summary>Two columns (wide window) or one stacked column (narrow window).</summary>
    public void SetStacked(bool stacked)
    {
        if (rootFlow != null)
            rootFlow.FlowDirection = stacked ? FlowDirection.TopDown : FlowDirection.LeftToRight;
    }

    public void SyncAll()
    {
        ctx.SyncAll();
        ctx.Quiet(() =>
        {
            string cur = cfg.ActiveProfile ?? "";
            if (profileCombo != null && profileCombo.Items.Contains(cur)) profileCombo.SelectedItem = cur;
        });
        UpdateModeUi();
        profileModified = false;
        if (keypad != null) keypad.Selected = -1;
        if (chkOverride != null) chkOverride.Checked = false;
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

    static string PoseText(string name)
    {
        string nl = Environment.NewLine;
        if (name.StartsWith("FLAT FACING ")) return "FLAT FACING" + nl + name.Substring("FLAT FACING ".Length);
        int sp = name.IndexOf(' ');
        return sp < 0 ? name : name.Substring(0, sp) + nl + name.Substring(sp + 1);
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
        rootFlow = root;
        var leftCol = Ui.Column();
        var rightCol = Ui.Column();
        root.Controls.Add(leftCol);
        root.Controls.Add(rightCol);

        // ================================================================ LEFT: live preview
        var pv = Ui.Card(leftCol, "Live preview", 432);
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
            var padCol = Ui.Column();
            pad = new PadView(170);
            pad.Caption = kind == "Mouse" ? "mouse stick / drag" : "analog / drag";
            pad.StickMoved += (x, y) => { runner.VX = x; runner.VY = y; runner.VirtualActive = true; };
            pad.StickReleased += () => runner.VirtualActive = false;
            pad.GetOverride = () => cfg.AxisOverride;
            pad.SetOverride = o => cfg.AxisOverride = o;
            padCol.Controls.Add(pad);

            // ---- the 8 override dots: edit them on the pad itself
            var orow = Ui.Row(padCol);
            chkOverride = new CheckBox
            {
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 30),
                Text = "Override dots",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5f),
                BackColor = Theme.Card2,
                ForeColor = Theme.Text,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 6, 0),
            };
            chkOverride.FlatAppearance.BorderSize = 0;
            chkOverride.FlatAppearance.CheckedBackColor = Theme.Warn;
            chkOverride.CheckedChanged += (s, ev) =>
            {
                pad.EditOverride = chkOverride.Checked;
                chkOverride.Text = chkOverride.Checked ? "Editing dots" : "Override dots";
                chkOverride.ForeColor = chkOverride.Enabled ? (chkOverride.Checked ? Theme.AccentText : Theme.Text) : Color.FromArgb(150, 155, 165);
                overrideHint.Visible = chkOverride.Checked;
                pad.Invalidate();
            };
            chkOverride.EnabledChanged += (s, ev) =>
            {
                chkOverride.FlatAppearance.CheckedBackColor = chkOverride.Enabled ? Theme.Warn : Theme.Disabled;
                chkOverride.ForeColor = chkOverride.Enabled ? (chkOverride.Checked ? Theme.AccentText : Theme.Text) : Color.FromArgb(150, 155, 165);
            };
            lockables.Add(chkOverride);
            orow.Controls.Add(chkOverride);
            var resetDots = Ui.Btn("Reset dots", (s, ev) =>
            {
                if (Locked) return;
                cfg.AxisOverride = Array.Empty<double>();
                pad.Invalidate();
            });
            resetDots.Margin = new Padding(0, 4, 0, 0);
            lockables.Add(resetDots);
            orow.Controls.Add(resetDots);
            overrideHint = Ui.Txt("Drag a dot: when you push that direction, the output goes to where you drop the dot. " +
                                  "Right-click a dot = back home.", Theme.Sub, 8f);
            overrideHint.MaximumSize = new Size(170, 0);
            overrideHint.Margin = new Padding(0, 4, 0, 0);
            overrideHint.Visible = false;
            padCol.Controls.Add(overrideHint);
            lower.Controls.Add(padCol);
        }
        bars = new BarsView();
        lower.Controls.Add(bars);

        string hintText =
            kind == "Mouse"
                ? "The circle is the virtual stick your mouse pushes (hollow ring = raw, dot = used). Dashed outline = the position. " +
                  "Drag inside the circle to test without moving the real mouse."
                : kind == "Keyboard"
                    ? "EXPERIMENTAL. Click inside the circle to add a point and press its key. Click a point to edit it, drag it to move it, " +
                      "right-click it to delete it. The hollow ring is the live stick. Dashed outline = the position."
                    : "Hollow ring = the stick you push. Dashed outline = the position. Drag inside the circle to use the mouse as a stick. The Override dots button lets you send a pushed direction to another place.";
        var hint = Ui.Txt(hintText, Theme.Sub, 8.5f);
        hint.MaximumSize = new Size(400, 0);
        hint.Margin = new Padding(0, 8, 0, 0);
        pv.Controls.Add(hint);

        // ================================================================ LEFT: ADJUST (below the preview)
        var ac = Ui.CardBig(leftCol, "ADJUST", 432);
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

        // ================================================================ RIGHT: profile
        var pc = Ui.CardBig(rightCol, "PROFILE (SETTINGS FOR A GAME)", 520);
        profileCombo = Ui.NewCombo(490);
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
        noteMain.MaximumSize = new Size(490, 0);
        noteMain.Margin = new Padding(0, 8, 0, 0);
        pc.Controls.Add(noteMain);
        noteGood = Ui.Txt("", Theme.Good, 8.5f);
        noteGood.MaximumSize = new Size(490, 0);
        noteGood.Margin = new Padding(0, 4, 0, 0);
        pc.Controls.Add(noteGood);

        if (kind == "Keyboard") BuildDotCard(rightCol);

        // ================================================================ RIGHT: motion style
        var sc = Ui.CardBig(rightCol, "MOTION STYLE", 520);
        var mrow = Ui.Row(sc);
        for (int i = 0; i < modeKeys.Length; i++)
        {
            string key = modeKeys[i];
            MakeToggle(mrow, modeNames[i], 92, 38, 10f,
                       () => Config.NormMode(cfg.Mode) == key,
                       () => { cfg.Mode = key; UpdateModeUi(); });
        }
        desc = Ui.Txt("", Theme.Sub, 9f);
        desc.MaximumSize = new Size(490, 0);
        desc.Margin = new Padding(0, 8, 0, 0);
        sc.Controls.Add(desc);
        moveBox = ctx.Check(sc, "Move: change Up/Down to Forward/Backward", () => cfg.MoveForward, v => cfg.MoveForward = v);
        moveBox.Margin = new Padding(0, 6, 0, 0);
        lockables.Add(moveBox);

        // ================================================================ RIGHT: POSITION (the big box)
        var pos = Ui.Card(rightCol, "", 520);
        var posTitle = Ui.Txt("POSITION", Theme.Accent, 15f, true);
        posTitle.Margin = new Padding(0, 0, 0, 4);
        pos.Controls.Add(posTitle);

        var typeRow = Ui.Row(pos);
        string[] typeNames = { "Joy-Con L", "Joy-Con R", "Pro Controller" };
        for (int i = 0; i < Config.ControllerTypes.Length; i++)
        {
            string key = Config.ControllerTypes[i];
            MakeToggle(typeRow, typeNames[i], key == "Pro Controller" ? 130 : 112, 34, 9.5f,
                       () => cfg.ControllerType == key,
                       () => OnControllerType(key));
        }

        var posNote = Ui.Txt("How is the controller held? FRONT = the face (stick/buttons) points at the screen, BACK = it points at you. " +
                             "UPSIDE DOWN / INVERTED = turned the other way round. TOP = the stick end of a Joy-Con, the trigger edge of a Pro Controller.",
                             Theme.Sub, 8.5f);
        posNote.MaximumSize = new Size(490, 0);
        posNote.Margin = new Padding(0, 6, 0, 6);
        pos.Controls.Add(posNote);

        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(494, 0),
            MaximumSize = new Size(494, 0),
            Margin = new Padding(0),
            BackColor = Color.Transparent,
        };
        pos.Controls.Add(flow);
        for (int i = 0; i < Config.Positions.Length; i++)
        {
            if (i == 0 || i == 8)
            {
                var head = Ui.Txt(i == 0 ? "STRAIGHT  (vertical / horizontal)" : "FLAT", Theme.Sub, 8.5f, true, 494);
                head.Height = 22;
                flow.Controls.Add(head);
                flow.SetFlowBreak(head, true);
            }
            string name = Config.Positions[i].Name;
            var rb = Ui.Toggle(flow, PoseText(name), 117, 46, 8f);
            rb.Margin = new Padding(0, 0, 6, 6);
            rb.CheckedChanged += (s, ev) =>
            {
                if (!ctx.Updating && rb.Checked && !Locked) OnPose(name);
            };
            ctx.Syncs.Add(() => rb.Checked = cfg.CurrentBase() == name);
            lockables.Add(rb);
        }

        // ---- lean
        var leanHead = Ui.Txt("LEAN  (tilts the position forward or backward)", Theme.Sub, 8.5f, true);
        leanHead.Margin = new Padding(0, 4, 0, 4);
        pos.Controls.Add(leanHead);
        var leanRow = Ui.Row(pos);
        string[] leanKeys = { "None", "Forward", "Backward" };
        string[] leanNames = { "Straight (no lean)", "Leaning FORWARD", "Leaning BACKWARD" };
        for (int i = 0; i < leanKeys.Length; i++)
        {
            string lk = leanKeys[i];
            MakeToggle(leanRow, leanNames[i], 160, 34, 9f, () => cfg.CurrentLean() == lk, () => OnLean(lk));
        }
        lockables.Add(ctx.Slider(pos, "Lean angle", 5, 80, () => cfg.LeanAngle, v =>
        {
            cfg.LeanAngle = v;
            if (cfg.CurrentLean() != "None") cfg.ApplyPosition(cfg.PositionBase, cfg.LeanMode, v);
        }, "0' deg'", 100, 300));

        // ---- custom position (rings in the live preview)
        var customRow = Ui.Row(pos);
        customRow.Margin = new Padding(0, 8, 0, 0);
        var btnCustom = Ui.Btn("Set a custom position...", (s, ev) => StartEdit(), true);
        lockables.Add(btnCustom);
        customRow.Controls.Add(btnCustom);
        var useCurrent = Ui.Btn("Use current pose", (s, ev) => { if (!Locked) eng.Request("capture"); });
        lockables.Add(useCurrent);
        customRow.Controls.Add(useCurrent);
        customRow.Controls.Add(Ui.Btn("Recenter now", (s, ev) => eng.Request("recenter")));

        editRow = Ui.Row(pos);
        editRow.Visible = false;
        editHint = Ui.Txt("Look at the live preview: drag the red / green / blue rings to turn the controller, then press Apply.",
                          Theme.Warn, 9f);
        editHint.MaximumSize = new Size(300, 0);
        editHint.Margin = new Padding(0, 6, 10, 0);
        editRow.Controls.Add(editHint);
        editRow.Controls.Add(Ui.Btn("Apply position", (s, ev) => ApplyEdit(), true));
        editRow.Controls.Add(Ui.Btn("Cancel", (s, ev) => CancelEdit()));

        // ---- what happens when the stick/mouse/keys are let go
        var retRow = Ui.Row(pos);
        retRow.Margin = new Padding(0, 10, 0, 0);
        retRow.Controls.Add(Ui.Txt(kind == "Mouse" ? "When I stop moving the mouse:" :
                                   (kind == "Keyboard" ? "When I release the keys:" : "When I let go of the stick:"),
                                   Theme.Text, 9f, false, 190));
        MakeToggle(retRow, "Return to neutral", 140, 32, 9f, () => cfg.ReturnToNeutral, () => cfg.ReturnToNeutral = true);
        MakeToggle(retRow, "Stay where it stopped", 160, 32, 9f, () => !cfg.ReturnToNeutral, () => cfg.ReturnToNeutral = false);

        // ================================================================ RIGHT: tests
        var tc = Ui.CardBig(rightCol, "TEST IT (4-SECOND DEMO)", 520);
        var trow = Ui.Row(tc);
        for (int i = 0; i < modeKeys.Length; i++)
        {
            string key = modeKeys[i];
            trow.Controls.Add(Ui.Btn("Test " + modeNames[i], (s, ev) => eng.StartTest(key, 4.0)));
        }
        var th = Ui.Txt("Use these while mapping Motion in yuzu, and to see what each style does.", Theme.Sub, 8.5f);
        th.Margin = new Padding(0, 4, 0, 0);
        tc.Controls.Add(th);

        // ================================================================ RIGHT: footer
        var frow = Ui.Row(rightCol);
        frow.Margin = new Padding(6, 2, 6, 2);
        frow.Controls.Add(Ui.Btn("Advanced settings...", (s, ev) => ToggleAdvanced()));
        saveMsg = Ui.Txt("", Theme.Good, 9f);
        saveMsg.Margin = new Padding(0, 9, 0, 0);
        frow.Controls.Add(saveMsg);
    }

    // ------------------------------------------------------------------ position
    void OnPose(string name)
    {
        CancelEdit();
        cfg.ApplyPosition(name, cfg.CurrentLean(), cfg.LeanAngle);
        eng.Request("recenter");
        ctx.SyncAll();
    }

    void OnLean(string lean)
    {
        CancelEdit();
        string baseName = cfg.CurrentBase();
        if (baseName == "Custom")
        {
            saveMsg.Text = "Pick one of the base positions first, then choose the lean.";
            ctx.SyncAll();
            return;
        }
        cfg.ApplyPosition(baseName, lean, cfg.LeanAngle);
        eng.Request("recenter");
        ctx.SyncAll();
    }

    void StartEdit()
    {
        if (Locked || editing) return;
        editing = true;
        view.EditQ = eng.ViewNeutral;          // start from the position that is set now
        view.EditMode = true;
        editRow.Visible = true;
    }

    void ApplyEdit()
    {
        if (!editing) return;
        // the preview shows the position with the hold style applied; take that off again
        Q n = (view.EditQ * eng.Hold().Conj()).Normalized();
        cfg.NeutralPreset = "Captured";
        cfg.CapturedQ = new[] { n.W, n.X, n.Y, n.Z };
        cfg.PositionBase = "Custom";
        cfg.LeanMode = "None";
        EndEdit();
        eng.Request("recenter");
        ctx.SyncAll();
        saveMsg.Text = "Custom position applied.";
    }

    void CancelEdit()
    {
        if (editing) EndEdit();
    }

    void EndEdit()
    {
        editing = false;
        if (view != null) view.EditMode = false;
        if (editRow != null) editRow.Visible = false;
    }

    // ------------------------------------------------------------------ keyboard point submenu
    void BuildDotCard(Control parent)
    {
        dotCard = Ui.Card(parent, "Selected point - submenu (click a point in the circle)", 520);

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
        note.MaximumSize = new Size(490, 0);
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
            foreach (var b in cfg.BuiltinList) profileCombo.Items.Add(b.Name);       // DEFAULT first
            foreach (string k in cfg.Profiles.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                profileCombo.Items.Add(k);
            string cur = cfg.ActiveProfile ?? "";
            profileCombo.SelectedItem = profileCombo.Items.Contains(cur) ? cur : Config.DefaultProfileName;
        });
    }

    /// <summary>Shows the right buttons and notes for DEFAULT / your own profiles, and locks DEFAULT.</summary>
    void UpdateProfileUi()
    {
        if (btnSaveNew == null) return;
        string name = cfg.ActiveProfile ?? "";
        bool isDefault = Config.IsDefault(name);
        bool own = !isDefault;

        btnSaveChanges.Visible = own;
        btnSaveNew.Visible = true;
        btnDelete.Visible = own;
        btnReset.Visible = !isDefault;
        profileRow2.Visible = own;

        if (isDefault)
        {
            UiScale.SetFont(noteMain, Ui.SquareFont(10.5f, FontStyle.Regular));
            noteMain.ForeColor = Theme.Red;
            noteMain.Text = cfg.DefaultObservation;
            UiScale.SetFont(noteGood, Ui.SquareFont(10.5f, FontStyle.Bold));
            noteGood.ForeColor = Theme.Good;
            noteGood.Text = cfg.DefaultObservationGood;
            noteGood.Visible = true;
        }
        else
        {
            UiScale.SetFont(noteMain, new Font("Segoe UI", 8.5f));
            noteMain.ForeColor = Theme.Sub;
            noteGood.Visible = false;
            noteMain.Text = "Your profile." +
                            (profileModified ? "\nYou have unsaved changes - press \"Save Changes\" to keep them." : "");
        }

        // DEFAULT is locked: everything that changes profile settings is greyed out
        foreach (Control c in lockables) c.Enabled = !isDefault;
        if (isDefault) CancelEdit();
        if (keypad != null) keypad.ReadOnly = isDefault;
        if (pad != null) pad.ReadOnly = isDefault;
        if (isDefault && chkOverride != null && chkOverride.Checked) chkOverride.Checked = false;
        adv?.SetProfileLocked(isDefault);
        UpdateDotCard();
    }

    // ------------------------------------------------------------------ profile actions
    void Persist() { AppSettings.Current?.Save(); }

    void ActivateProfile(string name)
    {
        CancelEdit();
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
        string input = Ui.Prompt(FindForm(), "Save new profile", "Name for the new profile:", "");
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
        if (moveBox != null) moveBox.Visible = m == "move";
    }

    void OnControllerType(string type)
    {
        cfg.ControllerType = type;
        adv?.SyncAll();
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
                CancelEdit();
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
        view.Move = eng.MovePos;
        view.Kind = cfg.ControllerType;
        view.Testing = eng.Testing;
        view.Sx = eng.Fx;
        view.Sy = eng.Fy;
        double shakeG = Math.Sqrt(eng.Lin[0] * eng.Lin[0] + eng.Lin[1] * eng.Lin[1] + eng.Lin[2] * eng.Lin[2]);
        view.Title = editing ? "EDIT POSITION" : eng.ActiveMode.ToUpperInvariant();
        view.Info = editing
            ? "turn the controller with the rings, then press Apply position"
            : eng.DevDeg.ToString("F0") + "° from position   stick " +
              eng.RawX.ToString("0.00") + ", " + eng.RawY.ToString("0.00") +
              "   push " + shakeG.ToString("F1") + " g";
        view.Invalidate();

        if (pad != null)
        {
            pad.Sx = eng.Fx;
            pad.Sy = eng.Fy;
            pad.RawX = eng.InX;
            pad.RawY = eng.InY;
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
        if (tickCount % 15 == 0 && !profileCombo.DroppedDown)
            CheckProfileIntegrity();
    }
}
