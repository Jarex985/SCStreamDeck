# Changelog

**Version format:** `MAJOR.MINOR.PATCH.BUILD`

| Position  | Meaning                                            | Example / Usage                                                                                                                        |
| --------- | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| **MAJOR** | Major version / breaking change in behavior        | `1.0.0.0` = First stable, feature-complete release for the planned scope.                                                              |
| **MINOR** | Big user-facing release / new feature line         | `0.1.0.0` = New features, UX overhauls, or major internal changes that affect users. Breaking changes will be called out with a warning. |
| **PATCH** | Smaller compatible improvements + bug fixes        | `0.0.1.0` = Small UX improvements and bug fixes. No existing settings are affected.                                                    |
| **BUILD** | Internal-only / no user-visible changes            | `0.0.0.1` = Code cleanup, refactors, tests without functional changes.                                                                 |

Notes:
- Versions below `1.0.0.0` are pre-release.
- If a release resets settings or requires manual steps, it will be explicitly highlighted.

---

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
