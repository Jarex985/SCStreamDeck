# *Changelog*

**Version Format: MAJOR.MINOR.PATCH.BUILD**

| Position  | Meaning                                            | Example / Usage                                                                                                                       |
| --------- |----------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| **MAJOR** | Major version / Breaking change in API or behavior | 1.0.0.0 = Major stable and feature complete release.                                                                                  |
| **MINOR** | Big user-facing release / new feature line         | 0.1.0.0 = New features, UX overhauls or major internal changes that affects users. Updates with breaking changes will show a warning. |
| **PATCH** | Smaller compatible improvements + bug fixes        | 0.0.1.0 = small UX improvements, bug fixes. No existing settings affected.                                                            |
| **BUILD** | Internal-only / no user-visible changes            | 0.0.0.1 = Code cleanup, refactors, tests without functional changes.                                                                  |

___

## v0.3.0.0 - Minor Release

**Features / Improvements:**

    - Added new Control Panel Key along with a redesigned Property Inspector UI/UX
    - Added theme switching support and a shared base CSS stylesheet so users can create custom themes
    - Added a Default and Crusader Blue as new themes
    - Added Channel switching support to Control Panel Key
    - Improved Control Panel UI/UX for channel overrides/state management (clearer loading/success/error states)
    - Improved keybinding processing and action mapping (better label disambiguation and metadata usage)
    - Improved installation detection UX/Messaging
    - Removed custom-paths.ini workaround, as custom paths can now be managed via the Control Panel Key

**Bug Fixes:**

    - None explicitly called out for this release (mostly new functionality + stability/UX improvements)

**Internal / Refactor:**

    - Major refactor/cleanup across keybinding services (executor/parser wiring simplifications; removed unused deps; some helpers moved to static)
    - Reduced Risk Hotspots and improved maintainability by extracting logic into focused helpers/services
    - Expanded and reorganized unit tests significantly (keybinding parsing/execution, activation modes, installation/state, localization, data parsing)
    - Reduced reliance on [ExcludeFromCodeCoverage] by covering previously-excluded logic with tests
    - SonarQube/Rider-driven cleanup: reduced cognitive complexity, standardized formatting, removed dead code, tightened access modifiers
    - DI/service initialization cleanup and consolidation

## v0.2.2.1

**Features / Improvements:**

    - (Experimental) Added a Click Sound feature to provide audio feedback
        - Sound file can be configured for every Key inside Property Inspector
        - Currently supported formats: .wav and .mp3
        - Only supports on KeyPress event for now, ignores Activation Modes

**Bug Fixes:**

    - None

**Internal / Refactor:**

    - Major internal refactor of Testing project structure and organization
    - Removed UnitTests and added Tests project with better structure and organization
    - Major refactor of PluginCore project structure and organization
      - This should not affect functionality but improves maintainability and readability.


## v0.2.1.0

**Features / Improvements:**

    - Added custom-paths.ini as backup plan if auto-detection still fails to locate Star Citizen installation path
      - Users can manually add installation paths to custom-paths.ini located in the plugin's root folder
      - The plugin will check these paths if auto-detection does not find a valid installation
      - Instructions for editing custom-paths.ini can be found within the ini file

Path to Plugin: `%APPDATA%/Elgato/StreamDeck/Plugins/com.jarex985.scstreamdeck.sdPlugin`

**Bug Fixes:**

    - Fixed an issue that made auto-detection too restrictive, so it would fail detecting some valid installation paths

**Internal / Refactor:**

    - Improved error handling and logging for installation path detection


## v0.2.0.0 - Minor Release

> [!WARNING]
> **Note: Keybindings from earlier Plugin Versions inside Stream Deck App will be reset due to internal changes.**

**Features / Improvements:**

    - None

**Bug Fixes:**

    - Corrected mouse wheel direction mapping for Star Citizen
    - Fixed issues with action parsing and unbound actions
    - Minor UI and CSS improvements

**Internal / Refactor:**

    - Removed some debug logging and cleaned up comments
    - Updated collection initializations
    - Refactored handler registration and restricted visibility for cleaner architecture
    - Streamlined SmartToggleHandler execution for clarity and reliability
    - Modularized input executor and keybinding loader for better error handling
    - Consolidated ActivationModeHandler metadata into execution context

## v0.1.0.2

**Features / Improvements:**

    - None

**Bug Fixes:**

    - Press & Delayed Press should now execute correctly as long as Key is held down
    - Updated CSS to prevent pointer events on dropdown overlays

**Internal / Refactor:**

    - Cleaned up comments and removed unused code
    - Improved logging messages for better clarity

## v0.1.0.1

**Features / Improvements:**

    - Added basic UI Theme (Concierge Color Scheme)
    - Default Key SVG added

**Bug Fixes:**

    - Minor bug fixes

## v0.1.0.0

    - Initial release
