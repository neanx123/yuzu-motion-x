using System.Drawing;
using System.Windows.Forms;

namespace YuzuMotionX;

/// <summary>The Setup Guide window (same content as the README).</summary>
public class TutorialForm : Form
{
    readonly RichTextBox box;

    public const string Summary =
        "Yuzu Motion X gives the yuzu emulator motion controls (gyro, tilt, shake, swing) without a real Joy-Con. " +
        "Use a controller, a mouse or the keyboard as the device, for up to 8 players at once - each player has its own " +
        "device, profiles and server:port.";

    public TutorialForm()
    {
        Text = "Yuzu Motion X - Setup Guide";
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
        T("Version 1.2   ·   By NeanX\n", 11f, true, Theme.Accent);
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

        H("What is Server:Port?");
        T("It is the address Yuzu Motion X listens on for ONE player: the place where yuzu asks for that player's motion. " +
          "The server:port written in the player's tab and the one you add in yuzu (Configure → Motion) must be exactly the same, " +
          "and every player needs a different port.\n");
        Line("Change it in the player's tab and press Apply. \"Default\" puts back the default of that player.");
        Line("127.0.0.1 means \"this same PC\". The defaults go up by one:");
        for (int i = 0; i < AppSettings.PlayerCount; i++)
            Line("Player " + (i + 1) + "  =  127.0.0.1:" + PlayerSettings.DefaultPort(i));

        H("The three devices");
        Line("Controller - an Xbox-style (XInput) pad. In the Device list pick Pad 1-4 (shown with the pad's full name). Other pads work through Steam Input, DS4Windows or x360ce. Only the RIGHT stick moves the motion by default, so the left stick stays free for the game.");
        Line("Mouse - mouse movement tilts/rotates/shakes the virtual Joy-Con (Windows Raw Input, works while yuzu has the focus). " +
             "In the Device list choose \"All mice\" or one specific mouse, so two players can each use their own mouse.");
        Line("Keyboard Motion (EXPERIMENTAL) - choose \"All keyboards\" or one specific keyboard in the Device list, click inside the circle to place points, press the key for each point. Holding the key " +
             "moves the virtual stick towards that point. Each point has its own submenu: behavior (Hold / Toggle / Pulse), position, " +
             "strength, time to reach the point, time to come back, max press time and smooth start/stop.");

        H("Motion styles");
        Line("Tilt - the stick angle becomes the controller angle (springs back, or stays).");
        Line("Gyro - the stick turns the controller like a real gyro. Best for aiming.");
        Line("Shake - push the stick (or hold the Shake key/button) to shake the controller back and forth.");
        Line("Swing - flick the stick for a fast swing or jab.");
        Line("Move - the whole controller moves away from where it started: up = up, down = down, left = left, right = right. " +
             "Tick \"Change Up/Down to Forward/Backward\" to push it forward and backward instead.");

        H("POSITION - how the controller is held");
        Line("Pick Joy-Con L, Joy-Con R or Pro Controller, then the position. FRONT = the face (stick/buttons) points at the screen, " +
             "BACK = it points at you. TOP = the stick end of a Joy-Con, the trigger edge of a Pro Controller.");
        Line("STRAIGHT: VERTICAL and HORIZONTAL, each FRONT / BACK, and UPSIDE DOWN (vertical) or INVERTED (horizontal).");
        Line("FLAT: facing up or down, with the long side vertical or horizontal.");
        Line("LEAN: leaning FORWARD or BACKWARD from any base position, with an adjustable angle.");
        Line("\"Set a custom position...\" - drag the red / green / blue rings in the live preview to turn the controller, then press \"Apply position\".");
        Line("\"When I let go of the stick\" (mouse: \"stop moving\", keyboard: \"release the keys\") - Return to neutral or Stay where it stopped.");
        Line("The dashed outline in the live preview always shows the position you chose.");

        H("Profiles");
        Line("DEFAULT (red) - locked: it can't be edited or deleted, and \"Reset to DEFAULT\" always brings it back.");
        Line("Your own profiles (white) - \"Save Changes\", \"Save new profile from it\", \"Delete profile\".");
        Line("Each player has separate profiles for Controller, Mouse and Keyboard. Everything is saved next to the .exe.");

        H("Axis override dots (Controller and Mouse)");
        T("Some minigames only work with one particular position, and then the direction you push does not match what the character does " +
          "(for example you must push top-left to go right). Override dots fix that without changing the position:\n");
        Line("Press \"Override dots\" under the round stick / mouse circle. 8 dots appear: Up, Down, Left, Right, Up-Left, Up-Right, Down-Left, Down-Right.");
        Line("Drag a dot to where you want that direction to go. Example (Barreling Along): drag the Right dot to the top-left and the Left dot " +
             "to the bottom-left - now pushing right moves the blue point to the top-left, and the character goes right.");
        Line("The blue point shows where the motion really goes; the hollow ring shows what you push. Directions between two dots blend smoothly.");
        Line("Right-click a dot to send it back home; \"Reset dots\" sends them all home. The dots are part of the profile (DEFAULT has none).");
        Line("Keyboard Motion does not need it: every keyboard point can already be placed anywhere.");

        H("Window size and Zoom");
        Line("The app scales with its window: maximize it for big, easy-to-read sections, or make it narrow to park it beside the game " +
             "(it then switches to one column).");
        Line("Zoom (top right): Auto follows the window size; or choose a fixed size from 70% to 200%.");

        H("Tips");
        Line("Map Motion 1 AND Motion 2 in yuzu if a game uses either Joy-Con.");
        Line("The Test buttons help yuzu detect the device while you map the motion button.");
        Line("If a game moves the wrong way, use \"Fix wrong motion\" in Advanced settings (reverse an axis).");
        Line("For Super Mario Party minigames that hold the Joy-Con vertically, the DEFAULT profile is the one to start with.");

        T("\nYuzu Motion X v1.2  ·  By NeanX\n", 10f, true, Theme.Accent);
    }
}
