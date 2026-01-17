# *Changelog*

**Version Format: MAJOR.MINOR.PATCH.BUILD**

| Position  | Meaning                                                         | Example / Usage                                                                                     |
| --------- | --------------------------------------------------------------- |-----------------------------------------------------------------------------------------------------|
| **MAJOR** | Major version / Breaking change in API or behavior              | 1.0.0.0 → Major stable, feature complete release.                                                   |
| **MINOR** | Feature line or breaking change within 0.x                      | 0.2.0.0 → New feature or major internal changes. Updates with breaking changes will show a warning. |
| **PATCH** | Bug fixes or new features **compatible** with previous versions | 0.2.1.0 → Added new action, UI improvement, no existing settings affected.                          |
| **BUILD** | Internal revision / code cleanup / cosmetic changes             | 0.2.1.1 → Logging improvements, comments, refactors without behavior impact.                        |

___

## v0.2.1.0

**Features / Improvements:**

    - Added custom-paths.ini as backup plan if auto-detection still fails to locate Star Citizen installation path.
      - Users can manually add installation paths to custom-paths.ini located in the plugin's root folder. 
      - The plugin will check these paths if auto-detection does not find a valid installation.
      - Instructions for editing custom-paths.ini can be found within the ini file.

Path to Plugin: `%APPDATA%/Elgato/StreamDeck/Plugins/com.jarex985.scstreamdeck.sdPlugin`

**Bug Fixes:**

    - Fixed an issue that made auto-detection too restrictive, so it would fail detecting some valid installation paths

**Internal / Refactor:**

    - Improved error handling and logging for installation path detection


## v0.2.0.0 - Minor Update

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
