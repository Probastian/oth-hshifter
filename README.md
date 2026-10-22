# oth-hshifter

A SnowRunner-style **H-pattern manual shifter with a clutch** for [Over the Hill](https://store.steampowered.com/search/?term=over%20the%20hill) (MelonLoader mod). Hold a clutch button, move the right stick through a real H-gate, release the clutch and the gear engages.

> Unofficial fan mod. Not affiliated with or endorsed by Funselektor. Gamepad only.

## Features

- **Clutch** (default **L1**): while held, the car drops to neutral and the right stick moves a physical gear lever. Release it and the gear under the lever engages immediately; throttle held through the shift keeps working.
- **A real gate**: the lever travels *in the direction you push the stick*, along rails only - sideways in the neutral channel, up/down in a column - and stops at the gear slots. There is no separate N position; N is simply "not in a slot". Crossing between columns is forgiving (rail entry tolerance, magnet to column centres).
- **High / Low range**: gate layout `[1L 2L] [1H 2H] [R]`. Moving into an L or H slot also switches the transfer case with the game's own hi/lo press.
- **Native HUD overlay** at the bottom centre, drawn with the game's own style sheet (font, colours, rounded corners): white gear chips that turn yellow when active, white rails, a round lever.
- **Settings > Controls integration**: the game's *Automatic / Manual* selector gets a third option, **H-Shifter**. It is selected automatically on first run.
- The game's own bumper shifting is ignored while the shifter is on (L1 is the clutch).

## Requirements

- Over the Hill (Steam), Unity 6 / IL2CPP
- [MelonLoader](https://github.com/LavaGang/MelonLoader) **0.7.x** (tested with 0.7.3 Open-Beta)
- A gamepad; a vehicle with a manual gearbox

## Install

1. Install MelonLoader into the game folder and start the game once.
2. Download `HShifter.dll` from the [latest release](../../releases/latest) and put it in `<game folder>\Mods`.
3. Start the game. Settings > Controls > Automatic / Manual now offers **H-Shifter**.

Settings live in `UserData\HShifter.cfg`. They can also be edited in-game with [oth-modconfigurator](https://github.com/Probastian/oth-modconfigurator) (Settings > Mods and the Clutch button row in Settings > Controls).

## Settings (`UserData\HShifter.cfg`)

| Setting | Default | |
|---|---|---|
| `Enabled` | `true` | Same as choosing H-Shifter in Settings > Controls |
| `ClutchButton` | `LeftShoulder` | Gamepad button used as clutch |
| `LeverSpeed` | `15` | Lever speed at full stick deflection |
| `StickDeadzone` | `0.25` | |
| `UseTransferCase` | `true` | H/L columns and transfer-case switching |
| `ReverseOnRight` | `true` | Reverse column at the right end of the gate |
| `SuppressCameraStick` | `true` | Pause the camera while the clutch is held (mouse camera too) |
| `ShowHud`, `HudScale`, `HudBottomOffset` | | Overlay |
| `ForceActive` | `false` | Also run on vehicles not reported as manual |
| `VerboseLog` | `false` | Log clutch, gear and transfer-case changes |

## Multiplayer

Local input only. On a gamepad the game ignores the shifter-style gear input, so:

- **Single player / hosting** (you own the car's state): the car's selected gear is set directly - any gear including N at once. This only touches your own car.
- **Joined as a client**: only the game's own gear up / gear down presses are sent. The game's manual shifting may skip N (R <-> 1), so N can be unreachable there. The log says so (`[gear]`).

## Build from source

You need the .NET SDK, the game with MelonLoader, and to have started the game once so MelonLoader generated `MelonLoader\Il2CppAssemblies`.

```powershell
dotnet build -c Release            # copies HShifter.dll into <game>\Mods
dotnet build -c Release -p:DeployToGame=false
```

If the game is not in the default Steam path, create `GamePath.user.props`:

```xml
<Project><PropertyGroup><GamePath>D:\Games\over the hill</GamePath></PropertyGroup></Project>
```

No game files are included in this repository; the project references your local game and MelonLoader assemblies.

## License

MIT - see [LICENSE](LICENSE).
