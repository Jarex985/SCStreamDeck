# Changelog

**Version format:** `MAJOR.MINOR.PATCH.BUILD`

| Position  | Meaning                                            | Example / Usage                                                                                                                          |
| --------- | -------------------------------------------------- |------------------------------------------------------------------------------------------------------------------------------------------|
| **MAJOR** | Major version / breaking change in behavior        | `1.0.0.0` = First stable, feature-complete release for the planned scope.                                                                |
| **MINOR** | Big user-facing release / new feature line         | `0.1.0.0` = New features, UX overhauls, or major internal changes that affect users. Breaking changes will be called out with a warning. |
| **PATCH** | Smaller compatible improvements + bug fixes        | `0.0.1.0` = Small UX improvements and bug fixes. Existing settings shouldn't be affected.                                                |
| **BUILD** | Internal-only / no user-visible changes            | `0.0.0.1` = Code cleanup, refactors, tests without functional changes.                                                                   |

Notes:
- Versions below `1.0.0.0` are pre-release.
- If a release resets settings or requires manual steps, it will be explicitly highlighted.

---

## v1.4.0.0 - Minor Release

### Features / Improvements

- New `Config` key: its Property Inspector is where the settings shared by the telemetry keys are created. All **format template** creation, editing and deletion moved there; the telemetry keys now only pick a template per rule. Existing templates are kept.
- **Flags**: create named on/off switches on the `Config` key (with a starting value; values return to it when the plugin restarts). Rule conditions can test them (`Flags › <name>`), a rule can switch flags once when it becomes active (**Sets flags**), and the press actions of `Advanced Telemetry Display` rules can set a flag on, off or toggle it (also without telemetry). Use them for example as a master warning or a "preflight check done" switch. A copy of the flags is kept in `%APPDATA%\SCStreamDeck\telemetry-flags.json` and restored like the templates.

---

## v1.3.0.2 - Minor Release

### Features / Improvements

- `Travel Display` key: like the Travel Timer it now has one **Display** look and an **Unavailable** list of rules instead of rules and a default look. While an Unavailable rule matches, the key shows that rule's look and does nothing when pressed. The look has text, image, colors, format template, flash, sound and press actions. Rules saved on the key before this version are read as Unavailable rules, so remove them.

---

## v1.3.0.1 - Minor Release

### Features / Improvements

- `Travel Timer` key: instead of rules and a default look it now has three looks, **Start**, **Stop** and **Unavailable**, each with text, image, colors, format template, flash and sound (Start and Stop also have press actions). The Unavailable rules (conditions on any attribute) decide when the key is unavailable: it then shows the matching rule's look and does nothing when pressed. Rules saved on the key before this version are read as Unavailable rules, so remove them.
- Telemetry keys run the press actions of the rule that was active when the key was pressed, even if the press changes the rule (this also applies to the `UEC Tracker` reset).

---

## v1.3.0.0 - Minor Release

### Features / Improvements

- New `Travel Timer` key: press to start measuring, press again to stop (the values freeze), press once more to reset and start a new measurement. Rules, images, flash, sounds and format templates work as on the other telemetry keys and can test the state.
- New `Travel Display` key: shows the elapsed time in minutes and tenths of a minute and the estimated distance travelled, in meters and in kilometers from 1 km. The distance integrates the ship's speed over time (trapezoid rule) while the measurement runs, also when the keys are not on the visible page.

---

## v1.2.0.2 - Minor Release

### Features / Improvements

- `UEC Tracker` key: the balance and the session change are abbreviated so the text fits the key at a bigger font size: `1.5k` from 1,000 (up to one decimal), `1.25m` from 1,000,000 and `1.25b` from 1,000,000,000 (up to two decimals). Losses stay in brackets, for example `(12k)`.

---

## v1.2.0.1 - Minor Release

### Features / Improvements

- New `UEC Tracker` key: shows your in-game UEC balance with the amount earned (green) or lost (red, in brackets) since the session started on a second line. Press the key to restart the count. Rules and format templates work as on the other telemetry keys.

---

## v1.2.0.0 - Minor Release

### Features / Improvements

- Format templates are also copied to `%APPDATA%\SCStreamDeck\telemetry-format-templates.json` and restored when Stream Deck loses the plugin's global settings, for example after installing the plugin again.
- New `Telemetry Debug` key: toggles a capture of the raw telemetry stream to `telemetry-debug.log` next to the plugin log, with field numbers, names from the protocol, unknown or mismatching fields flagged, the fields the game did not send, and a summary of attributes never sent. Use it when an attribute does not show up.
- Flashing rules and templates have a new **Flashes** option: **Text** (default; only the text blinks), **Background + image** (the text stays while the background and image blink to black) or **Everything**. Rules that flashed before now blink only the text; image and background stay.
- New format templates for the telemetry keys: a named bundle of text color, background color, image and flash interval that rules can use, shared by all telemetry keys.
- New `Deceleration` key: estimates the cumulative units (for ship speed: meters) and the time until a numeric attribute reaches zero from its reduction over a configurable window, with rounding and rule-based looks.
- The `Telemetry Display` keys read the available attributes from the installed game (`StarCitizen.exe`) instead of a built-in list, so they always match the game version; the built-in list remains as a fallback. The protocol that was read is exported next to the plugin log (`telemetry-protocol.proto` / `.desc`).
- New `Advanced Telemetry Display` key: rules combine conditions on several attributes (all / any), for example "landed AND hydrogen fuel low", texts can print any attribute with `{value:attribute.path}`, and each rule can run several press actions in order (with optional delays), which allows one key to act as a state-dependent toggle.
- New `Telemetry Display` key: shows any value from Star Citizen's local gRPC telemetry API and changes text, image and colors through ordered rules; each rule can optionally flash its text/image, play a sound when it becomes active and run a keybinding when the key is pressed.

---

## v1.1.3.0 - Patch Release

### Features / Improvements

- Bindings that use mwheel_up/mwheel_down will be retriggered while key is held down.

### Bug Fixes

- Fixed an Issue where some modifier keys didn't properly execute when combined with a mouse button.
- Fixed an Issue where `Hold` ActivationModes triggered multiple times when a binding had a modifier key

## v1.1.2.0 - Minor Release

### Features / Improvements

- Added `ActionMapsWatcherService` to monitor changes in `actionmaps.xml` and auto-sync keybindings in Stream Deck profiles when changes are detected (e.g., after in-game rebinding).
- Toggle Key now only shows Actions that are considered to be a `Toggle` based on their metadata, improving usability and reducing confusion.

### Bug Fixes

- Fixed an issue where actions with the same name/binding but different category/map would not be parsed correctly, which could lead to one action incorrectly shown as `Unbound`.
- Fixed an issue where Toggle Key would not maintain its state when switching between different pages on the Stream Deck, leading to inconsistent behavior. Now only resets the state when the user explicitly triggers the reset, or the application restarts.
- Fixed an issue where some action bindings would not get parsed and show up as `Unbound` (e.g., `focus_on_chat_textinput` with `return` key).

### Documentation

- Added a note about mouse button bindings not working when moving the mouse. This is a Star Citizen (EAC) limitation which i can't do anything about.

### Internal / Refactor

- Introduced a keybindings JSON schema version to force-refresh cached keybindings when the data format changes.

## v1.0.2.0 - Patch Release
> [!NOTE]
> This update introduces a stable v2 identifier format for saved function bindings (v2|<actionName>|<mapName>).
> Existing Stream Deck profiles using the legacy format remain supported and will auto-migrate when the key becomes active/visible.

### Bug Fixes

- Fixed an issue where some keybindings would not get parsed correctly if it just contained a modifier (e.g. `Left Ctrl`), 
  or didn't have a UICategory assigned. 

## v1.0.1.0 - Patch Release

### Bug Fixes

- Fixed an issue where Data.p4k was not closed properly after reading, which could lead to file locks (e.g. when verifying game files in the launcher).

## v1.0.0.0 - Major Release
> [!NOTE]
> No breaking changes from v0.3.0.0. This is the first stable, feature-complete release for the planned scope.

### Features / Improvements

- Added `Toggle Key` to switch between two states, with an optional reset if the state gets out of sync
- Removed unbound functions category from the function dropdown
- Added a warning indicator for functions that have no keybinding
- Improved `Force Redetection` to re-scan `actionmaps.xml` from auto-detected installs and custom install paths, then refresh cached data

### Bug Fixes

- Various minor bug fixes and improvements

### Documentation

- Updated documentation to reflect new features and changes
- Added a legal note and “Made by the Community” badge to the docs site
- Minor spelling/grammar fixes across the docs

### Internal / Refactor

- Reduced technical debt (code cleanup + improved comments)

## v0.3.0.0 - Minor Release

### Features / Improvements

- Added a new `Control Panel` key along with a redesigned Property Inspector UI/UX
- Added theme switching support and a shared base CSS stylesheet so users can create custom themes
- Added the `Default` and `Crusader Blue` themes
- Added channel switching support to the `Control Panel` key
- Improved `Control Panel` UI/UX for channel overrides and state management (clearer loading/success/error states)
- Improved keybinding processing and action mapping (better label disambiguation and metadata usage)
- Improved installation detection UX/messaging
- Removed the `custom-paths.ini` workaround (custom paths are now managed via the `Control Panel` key)

### Bug Fixes

- None explicitly called out for this release (mostly new functionality plus stability/UX improvements)

### Internal / Refactor

- Major refactor/cleanup across keybinding services (executor/parser wiring simplifications; removed unused deps; some helpers moved to static)
- Reduced risk hotspots and improved maintainability by extracting logic into focused helpers/services
- Expanded and reorganized unit tests significantly (keybinding parsing/execution, activation modes, installation/state, localization, data parsing)
- Reduced reliance on `[ExcludeFromCodeCoverage]` by covering previously excluded logic with tests
- SonarQube/Rider-driven cleanup: reduced cognitive complexity, standardized formatting, removed dead code, tightened access modifiers
- DI/service initialization cleanup and consolidation

## v0.2.2.1

### Features / Improvements

- (Experimental) Added a click sound feature to provide audio feedback
- Sound file can be configured for every key in the Property Inspector
- Supported formats: `.wav` and `.mp3`
- Only supports the key press event for now; activation modes are ignored

### Bug Fixes

- None

### Internal / Refactor

- Major internal refactor of the test project structure and organization
- Removed `UnitTests` and added a `Tests` project with better structure
- Major refactor of the PluginCore project structure and organization
  (should not affect functionality; improves maintainability and readability)


## v0.2.1.0

### Features / Improvements

- Added `custom-paths.ini` as a backup plan if auto-detection still fails to locate the Star Citizen installation path
- Users can manually add installation paths to `custom-paths.ini` located in the plugin root folder
- The plugin checks these paths if auto-detection does not find a valid installation
- Instructions for editing `custom-paths.ini` are included in the file

Path to plugin: `%APPDATA%\Elgato\StreamDeck\Plugins\com.jarex985.scstreamdeck.sdPlugin`

### Bug Fixes

- Fixed an issue where auto-detection was too restrictive and would miss valid installation paths

### Internal / Refactor

- Improved error handling and logging for installation path detection


## v0.2.0.0 - Minor Release

> [!WARNING]
> **Note: Keybindings from earlier Plugin Versions inside Stream Deck App will be reset due to internal changes.**

### Features / Improvements

- None

### Bug Fixes

- Corrected mouse wheel direction mapping for Star Citizen
- Fixed issues with action parsing and unbound actions
- Minor UI and CSS improvements

### Internal / Refactor

- Removed some debug logging and cleaned up comments
- Updated collection initializations
- Refactored handler registration and restricted visibility for cleaner architecture
- Streamlined `SmartToggleHandler` execution for clarity and reliability
- Modularized input executor and keybinding loader for better error handling
- Consolidated `ActivationModeHandler` metadata into the execution context

## v0.1.0.2

### Features / Improvements

- None

### Bug Fixes

- Press & Delayed Press now execute correctly as long as the key is held down
- Updated CSS to prevent pointer events on dropdown overlays

### Internal / Refactor

- Cleaned up comments and removed unused code
- Improved logging messages for better clarity

## v0.1.0.1

### Features / Improvements

- Added a basic UI theme (Concierge color scheme)
- Added the default key SVG

### Bug Fixes

- Minor bug fixes

## v0.1.0.0

### Features / Improvements

- Initial release
