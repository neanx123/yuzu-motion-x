using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>The quick tutorial window (same content as the README).</summary>
public class TutorialForm : Form
{
    readonly RichTextBox box;

    public const string Summary =
        "Yuzu Motion X gives the yuzu emulator motion controls (gyro, tilt, shake, swing) without a real Joy-Con. " +
        "Use a controller, a mouse or the keyboard as the device, for up to 8 players at once - each player has its own " +
        "device, profiles and server:port.";

    public TutorialForm()
    {
        Text = "Yuzu Motion X - Quick tutorial";
        Icon = AppIcon.Get();
        BackColor = Theme.Bg;
        ClientSize = new Size(780, 740);
        MinimumSize = new Size(560, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Card,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 10f),
            DetectUrls = false,
        };
        Controls.Add(box);
        Fill();
        box.SelectionStart = 0;
        box.ScrollToCaret();
    }

    void T(string text, float size = 10f, bool bold = false, Color? color = null)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionFont = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        box.SelectionColor = color ?? Theme.Text;
        box.AppendText(text);
    }

    void H(string text) { T("\n" + text + "\n", 13f, true, Theme.Accent); }
    void Step(string text) { T("\n" + text + "\n", 11.5f, true, Theme.Warn); }
    void Line(string text) { T("    •  " + text + "\n"); }

    void Fill()
    {
        T("Yuzu Motion X\n", 22f, true);
        T("Version 1.0   ·   By NeanX\n", 11f, true, Theme.Accent);
        T("\n" + Summary + "\n", 10.5f, false, Theme.Sub);

        H("How to Set Up Motion Controls");
        T("Follow these steps to get started:\n");

        Step("Step 1 – Open Yuzu Motion X");
        Line("Launch the app on your PC.");
        Line("Pick the player tab (Player 1 … Player 8), choose the device (Controller, Mouse or Keyboard Motion) and press \"Player is ON\".");
        Line("Make sure your device is connected and test that it moves in the live preview (the Test buttons also move it).");

        Step("Step 2 – Configure Yuzu");
        Line("Open Yuzu.");
        Line("Navigate to Emulation → Configure → Controls.");
        Line("Select the controller you want to use.");
        Line("Enable the box \"Motion\".");
        Line("Press \"Configure\".");
        Line("Add 127.0.0.1:26761 (example) Server/Port - use the exact server:port shown for that player in Yuzu Motion X.");
        Line("Click on \"Motion 1\" (or 2 / 3 / 4 … depending on the player you are setting).");
        Line("Move the device specified until it appears \"cemuhookudp\".");

        H("Server : port for each player");
        T("Every player is its own motion server, so each one needs a different port. You can change them in the player tab; the defaults go up by one:\n");
        for (int i = 0; i < AppSettings.PlayerCount; i++)
            Line("Player " + (i + 1) + "  =  127.0.0.1:" + PlayerSettings.DefaultPort(i));
        T("The server:port in Yuzu Motion X and in yuzu's Motion configuration must match exactly.\n", 10f, false, Theme.Sub);

        H("The three devices");
        Line("Controller - an Xbox-style (XInput) pad. In the Device list pick Pad 1-4 (shown with the pad's full name). Other pads work through Steam Input, DS4Windows or x360ce. Only the RIGHT stick moves the motion by default, so the left stick stays free for the game.");
        Line("Mouse - mouse movement tilts/rotates/shakes the virtual Joy-Con (Windows Raw Input, works while yuzu has the focus). " +
             "In the Device list choose \"All mice\" or one specific mouse, so two players can each use their own mouse.");
        Line("Keyboard Motion (EXPERIMENTAL) - choose \"All keyboards\" or one specific keyboard in the Device list, click inside the circle to place points, press the key for each point. Holding the key " +
             "moves the virtual stick towards that point. Each point has its own submenu: behavior (Hold / Toggle / Pulse), position, " +
             "strength, time to reach the point, time to come back, max press time and smooth start/stop.");

        H("Profiles");
        Line("DEFAULT (red) - locked: it can't be edited or deleted, and \"Reset to DEFAULT\" always brings it back.");
        Line("Profiles that start with * (green) - made by the program. They never change; use \"Save new profile from it\" to keep your own version.");
        Line("Your own profiles (white) - \"Save Changes\", \"Save new profile from it\", \"Delete profile\".");
        Line("Each player has separate profiles for Controller, Mouse and Keyboard. Everything is saved next to the .exe.");

        H("Tips");
        Line("Map Motion 1 AND Motion 2 in yuzu if a game uses either Joy-Con.");
        Line("The Test buttons help yuzu detect the device while you map the motion button.");
        Line("If a game moves the wrong way, use \"Fix wrong motion\" in Advanced settings (reverse an axis).");
        Line("For Super Mario Party minigames that hold the Joy-Con vertically, the DEFAULT profile is the one to start with.");

        T("\nYuzu Motion X v1.0  ·  By NeanX\n", 10f, true, Theme.Accent);
    }
}
