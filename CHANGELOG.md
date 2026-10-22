# Changelog

All notable changes to **oth-hshifter** are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.8.0] - 2026-10-22

### Added
- Optional **keyboard clutch**: new `ClutchKey` setting (default `None`). Together with `ClutchButton` the clutch now has a keyboard and a gamepad binding in Settings > Controls, like the game's own actions. While the clutch key is held the shifter stays active even if the game has switched to the keyboard input scheme; the gear lever is still moved with the gamepad's right stick.

## [0.7.2] - 2026-10-22

### Added
- Removing the clutch binding in Settings > Controls now means *no clutch* instead of falling back to L1.

### Changed
- `VerboseLog` is off by default; clutch, gear and transfer-case messages are only logged when it is on.
- Quieter log output (the "no camera actions found" warning is only logged with verbose logging).

### Removed
- The **F8** toggle hotkey and the **F10** diagnostics dump, together with the `ToggleKey` and `StartActive` settings.
- Development-only diagnostics (UI style dump file, throttle logging) and the settings-migration code.

## [0.6.0] - 2026-10-22

First public release.

### Added
- SnowRunner-style **H-pattern shifter with a clutch**: hold the clutch (default L1) and the car drops to neutral; the right stick moves a physical gear lever; releasing the clutch engages the gear under the lever immediately.
- A bounded **gate**: the lever travels in the direction the stick is pushed, only along the rails (sideways in the neutral channel, up/down in a column) and stops at the gear slots. Neutral is simply "not in a slot". Crossing between columns is forgiving (rail-entry tolerance, magnet to column centres); lever speed is configurable (`LeverSpeed`).
- **High / Low range** columns, layout `[1L 2L] [1H 2H] [R]`; moving into an L or H slot also switches the transfer case with the game's own hi/lo press.
- **HUD overlay** at the bottom centre drawn with the game's own style sheet: white gear chips that turn yellow when active, white rails, a round lever.
- **Settings > Controls** integration: the game's *Automatic / Manual* selector gets a third option, **H-Shifter** (selected automatically on first run).
- Gear changes in single player / as host set the car's gear directly (any gear, including N); as a joined client the game's own gear up / down presses are used.
- The game's own bumper shifting is ignored while the shifter is active; the camera is paused while the clutch is held so the right stick can't orbit it. Throttle held through a shift keeps working.
