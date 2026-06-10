# Hand of Fate Access

A mod that makes Hand of Fate (the original game) playable without sight. Everything the game shows is spoken through your screen reader, and combat is made playable through a layer of added audio cues. There is no visual fallback; speech and sound are the whole interface.

## Requirements

- Windows.
- Hand of Fate, the original game (not Hand of Fate 2).
- A screen reader. The mod speaks through NVDA, JAWS, and the other major screen readers, and falls back to Windows SAPI if none is running.

## Installation

1. Download the latest release zip.
2. Extract it directly into the game folder, the one containing `Hand of Fate.exe`. For a default Steam install that is `C:\Program Files (x86)\Steam\steamapps\common\Hand of Fate`. Allow it to merge with the existing folders.
3. Launch the game. Shortly after launch you should hear "Hand of Fate Access loaded".

The zip contains everything the mod needs, including the mod loader, already configured. There is nothing else to install or set up.

## How it works

This is deliberately a small mod. Hand of Fate already has complete keyboard and controller navigation for all of its menus and card play; the mod does not replace any of that, it reads it. As you move the game's own focus, each control speaks with its full information: card names and text, costs, token stakes, equipment stats, shop prices, and so on.

To learn or change the game's controls, open the pause menu and press right arrow to reach the Controls page. Everything is rebindable there.

One game control is easy to miss and worth knowing: the inspect command, Triangle or Y on a controller and right mouse button on keyboard by default, zooms into many things for more information. Once zoomed in, the left and right bumpers (Page Up and Page Down on keyboard) page through tabs of additional info. The UI is fairly sparse overall, but some details only live behind that zoom.

## Keys the mod adds

The mod adds only three controls of its own. They are not rebindable, but they were chosen to avoid every key the game uses.

- Read resources: slash on keyboard, or click the right stick (R3). Speaks your current resources on demand without moving focus.
- Ping nearest enemy: L on keyboard, or click the left stick (L3), during combat. Plays a single beep from the direction of the nearest living enemy, so you can orient and close in.
- Map cursor: on the map screen, the right stick, or Ctrl plus arrow keys on keyboard, moves a free-roam cursor over the whole visible board. The game's own cursor can only reach cards adjacent to your token; this one walks card to card across everything you can see, reading each card's identity, its state (where you are, completed, reachable, open, or locked), and which directions have neighbouring cards, so you can learn the board's shape. It is for surveying only; moving your token still uses the game's own arrows and confirm.

## Combat

Most of what the mod adds is combat audio:

- Wall tones: directional wind sounds that swell as you get close to walls, so you can feel the arena's edges.
- Attack cues: when an enemy attack is about to land you hear one of two sounds. A clang means block now and it will connect; a crack means the attack cannot be blocked, move.
- Projectiles: every enemy projectile in flight gets its own sound, panned and pitched by where it is so you can hear it coming and dodge.
- Hazards and traps: ground hazards, beams, fire trails, and the level's traps all sound continuously, always placed at the nearest dangerous point, so moving away from the sound is always the way out. Armed and safe phases sound different, so a cycling trap's rhythm can be timed by ear.
- Chests and exits: each pings repeatedly so you can walk to it. Pings always mean a point of interest; looping sounds always mean danger.

You do not need to memorize any of this up front. The pause menu contains a new option, the sound glossary, at the bottom of its list. It holds every combat sound with a one-line description; arrow through the list and press confirm to play each one.

## The gambit minigame

Some encounters resolve through the game's chance-card shuffle. It is a shell game: you are shown which card is which, the cards are shuffled around, and you have to keep track of where the one you want ended up. The mod makes this playable by ear. First it teaches you the table: each card slot plays a distinct instrument tone and speaks its outcome. Then, during the shuffle, each card's tone follows it around the stereo field as it moves. Track the tone of the card you want and pick its final position.

## Languages

The mod speaks in whatever language the game is set to. All twelve of the game's languages are supported.

## Known limitations

The final Dealer boss fight has no bespoke support yet. It is heavily scripted, and the author has deliberately left it unspoiled until reaching it in their own playthrough. Everything else in the game is expected to work; if you find something that does not, please report it.

## For developers

The repo builds with the .NET SDK and requires a local install of the game (the plugin references its assemblies). `setup-bepinex.ps1` installs the vendored, correctly configured BepInEx into the game folder; `build.ps1` builds the plugin and deploys it plus its native dependencies into the game's plugins folder; `test.ps1` runs the offline test suite, which needs neither the game nor Unity.

Logs: the game writes `Hand of Fate_Data\output_log.txt` in the game folder, and BepInEx writes `BepInEx\LogOutput.log`. Both reset on every launch. All mod lines are prefixed `[HoFAccess]`.
