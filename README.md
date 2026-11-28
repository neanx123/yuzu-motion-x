# Yuzu Motion X  -  Version 1.2  -  By NeanX

**What it is for:** Yuzu Motion X gives the yuzu emulator motion controls (gyro, tilt, shake, swing, move) without a real Joy-Con.
Use a **controller**, a **mouse** or the **keyboard** as the device, for up to **8 players at once**. Every player has its own
device, profiles and server:port. Native Windows app (.NET 8), one self-contained `.exe`.
The **Setup Guide** button in the app opens this same guide. See `changelog.txt` for what is new in 1.2.

## How to Set Up Motion Controls
Follow these steps to get started:

**Step 1 – Open Yuzu Motion X**
- Launch the app on your PC.
- Pick the player tab (Player 1 … Player 8), choose the device (Controller, Mouse or Keyboard Motion) and press **Player is ON**.
- Make sure your device is connected and test that it moves in the live preview (the Test buttons also move it).

**Step 2 – Configure Yuzu**
- Open Yuzu.
- Navigate to Emulation → Configure → Controls.
- Select the controller you want to use.
- Enable the box "Motion".
- Press "Configure".
- Add `127.0.0.1:26761` (example) Server/Port - use the exact server:port shown for that player in Yuzu Motion X.
- Click on "Motion 1" (or 2 / 3 / 4 … depending on the player you are setting).
- Move the device specified until it appears "cemuhookudp".

## What is Server:Port?
It is the address Yuzu Motion X listens on for ONE player: the place where yuzu asks for that player's motion. The server:port written in the player's tab and the one you add in yuzu (Configure → Motion) must be exactly the same, and every player needs a different port.
- Change it in the player's tab and press **Apply**. **Default** puts back the default of that player.
- `127.0.0.1` means "this same PC". The defaults go up by one:

| Player | Server:port | Player | Server:port |
|---|---|---|---|
| 1 | 127.0.0.1:26760 | 5 | 127.0.0.1:26764 |
| 2 | 127.0.0.1:26761 | 6 | 127.0.0.1:26765 |
| 3 | 127.0.0.1:26762 | 7 | 127.0.0.1:26766 |
| 4 | 127.0.0.1:26763 | 8 | 127.0.0.1:26767 |

## The window
- **Header**: name, version, author, summary, the **Zoom** box and the **Setup Guide** button.
- **Player tabs**: Player 1 … Player 8 (a dot marks players that are ON). Players that are OFF use no ports and no CPU.
- **Inside a tab** (top to bottom): *Player is ON/OFF*, the device type (**Controller / Mouse / Keyboard Motion (experimental)**), the **Device:** list (the exact physical device, by its full name), the **Server : port** boxes with **Apply** / **Default** under them, and the screen of the chosen device.
- **Left**: *Live preview* (a 3D Joy-Con / Pro Controller that moves exactly like the data sent to yuzu, dashed outline = the position) and **ADJUST** (sensitivity, speed, stability…).
- **Right**: *Profile*, *Motion style*, the big **POSITION** box, *Test it* and *Advanced settings*.
- Everything is saved next to the .exe in `Yuzu Motion X settings.json`.

## Motion styles
- **Tilt** - the stick angle becomes the controller angle. **Gyro** - the stick turns the controller like a real gyro.
- **Shake** - shake the controller back and forth. **Swing** - flick for a fast swing or jab.
- **Move** - the whole controller moves away from where it started: up = up, down = down, left = left, right = right. Tick *Change Up/Down to Forward/Backward* to push it forward and backward instead.

## POSITION - how the controller is held
- Pick **Joy-Con L / Joy-Con R / Pro Controller**, then the position. **FRONT** = the face (stick/buttons) points at the screen, **BACK** = it points at you. **TOP** = the stick end of a Joy-Con, the trigger edge of a Pro Controller.
- **Straight**: VERTICAL and HORIZONTAL, each FRONT / BACK, plus UPSIDE DOWN (vertical) or INVERTED (horizontal).
- **Flat**: facing up or down, long side vertical or horizontal.
- **Lean**: *Leaning FORWARD / BACKWARD* from any base position, with an adjustable angle.
- **Set a custom position...**: drag the red / green / blue rings in the live preview to turn the controller, then press *Apply position*.
- **When I let go of the stick** (mouse: *stop moving*, keyboard: *release the keys*): *Return to neutral* or *Stay where it stopped*.

## Axis override dots (Controller and Mouse)
Some minigames only work with one particular position, and then the direction you push does not match what the character does (for example you must push top-left to go right). Override dots fix that without changing the position:
- Press **Override dots** under the round stick / mouse circle. 8 dots appear: Up, Down, Left, Right, Up-Left, Up-Right, Down-Left, Down-Right.
- Drag a dot to where you want that direction to go. *Example (Barreling Along):* drag the **Right** dot to the top-left and the **Left** dot to the bottom-left - now pushing right moves the blue point to the top-left, and the character goes right.
- The blue point shows where the motion really goes; the hollow ring shows what you push. Directions between two dots blend smoothly.
- Right-click a dot to send it back home; **Reset dots** sends them all home. The dots are saved in the profile (DEFAULT has none). Keyboard Motion does not need them: every keyboard point can already be placed anywhere.

## Window size and Zoom
The app scales with its window: maximize it for big, easy-to-read sections, or make it narrow to park it beside the game (it then switches to one column). The **Zoom** box (top right) is *Auto* (follows the window) or a fixed size from 70% to 200%.

## The three devices
- **Controller** - an Xbox-style (XInput) pad; in **Device:** pick Pad 1-4 (shown with the pad's full name). Other pads work through Steam Input, DS4Windows or x360ce. The controller profiles read the **right stick only** (Advanced settings -> Stick to use can change that).
- **Mouse** - Windows Raw Input on its own thread (works while yuzu has the focus). **Device:** = *All mice* or one specific mouse.
- **Keyboard Motion (EXPERIMENTAL)** - **Device:** = *All keyboards* or one specific keyboard. Click inside the circle to place a **point**, press the key for it; holding the key moves the virtual stick towards the point. Each point has a submenu: behavior (Hold / Toggle / Pulse), position, strength, time to reach, time to come back, max press time, smooth start/stop. Drag a point to move it, right-click to delete it.

## Profiles (per player, per device)
- **DEFAULT** (red) - locked: it can't be edited or deleted, and *Reset to DEFAULT* always brings it back. Only *Save new profile from it* is shown, and every setting control is greyed out. It is the setting tuned for the Super Mario Party minigames (magnifying glass, sizzling steak): Joy-Con R, **VERTICAL UPSIDE DOWN FRONT**, Tilt.
- **Your profiles** (white) - *Save Changes*, *Save new profile from it*, *Delete profile*, *Reset to DEFAULT*.
- Since 1.1 there are no pre-made profiles besides DEFAULT.

## Tips
- Map Motion 1 AND Motion 2 in yuzu if a game uses either Joy-Con.
- The Test buttons help yuzu detect the device while you map the motion button.
- If a game moves the wrong way, use "Fix wrong motion" in Advanced settings.
- Only one copy of Yuzu Motion X can run at a time.

## Get the .exe
**Option A - no installs**: upload this folder to a GitHub repository (keep `.github/workflows/build.yml`), Actions -> "Build Yuzu Motion X" -> Run workflow, download `Yuzu-Motion-X-v1.2-windows` (zip with `Yuzu Motion X v1.2.exe`).
**Option B**: install the .NET 8 SDK and double-click `build.bat`.
