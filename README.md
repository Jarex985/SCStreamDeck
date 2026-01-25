# Star Citizen Stream Deck Plugin

A Stream Deck plugin that provides bindable actions for Star Citizen.

User Guide (Website): https://jarex985.github.io/SCStreamDeck/

Download latest: https://github.com/Jarex985/SCStreamDeck/releases/latest/download/com.jarex985.scstreamdeck.streamDeckPlugin

Report bugs / feature requests: https://github.com/Jarex985/SCStreamDeck/issues

## Requirements

[![Windows 10+](https://img.shields.io/badge/Windows-10%2B-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Stream Deck v6.4+](https://img.shields.io/badge/Stream%20Deck%20App-6.4%2B-purple?logo=elgato&logoColor=white)](https://www.elgato.com/s/stream-deck-app)
[![.NET 8 Desktop Runtime](https://img.shields.io/badge/.NET%20Desktop%20Runtime-8.0-blue?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)

## Project Info

### Status
[![GitHub release](https://img.shields.io/github/release/Jarex985/SCStreamDeck?include_prereleases=&sort=semver&color=2ea44f)](https://github.com/Jarex985/SCStreamDeck/releases/)
[![License](https://img.shields.io/badge/License-MIT-2ea44f)](LICENSE.md)
[![Contributions - welcome](https://img.shields.io/badge/Contributions-welcome-2ea44f)](CONTRIBUTING.md)  

![Code scanning](https://github.com/Jarex985/SCStreamDeck/workflows/CodeQL/badge.svg)
[![CI](https://github.com/Jarex985/SCStreamDeck/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Jarex985/SCStreamDeck/actions/workflows/ci.yml)

### Programming Languages
[![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp)
[![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white)](https://developer.mozilla.org/docs/Web/HTML)
[![CSS3](https://img.shields.io/badge/CSS3-1572B6?logo=css3&logoColor=white)](https://developer.mozilla.org/docs/Web/CSS)
[![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?logo=javascript&logoColor=black)](https://developer.mozilla.org/docs/Web/JavaScript)
### IDE / Tools
[![JetBrains Rider](https://img.shields.io/badge/JetBrains%20Rider-000000?logo=JetBrains&logoColor=white)](https://www.jetbrains.com/rider/)
[![JetBrains WebStorm](https://img.shields.io/badge/JetBrains%20WebStorm-000000?logo=JetBrains&logoColor=white)](https://www.jetbrains.com/webstorm/)
### Support / Funding
[![Buy Me A Coffee](https://img.shields.io/badge/BuyMeACoffee-Support-orange)](https://www.buymeacoffee.com/jarex9851)
[![Ko-Fi](https://img.shields.io/badge/Ko--Fi-Support-orange?style=flat&logo=kofi&logoColor=white)](https://ko-fi.com/jarex985)


## Current Features

- **Adaptive Key**: Dynamically bindable key actions for Star Citizen that execute only their assigned function based on the ActivationMode.
    - *Example:* Tap vs Hold on `Num-` executes only the Tap function when this is the assigned function.  
  

- **Control Panel Key**: Allows you to change settings like: 
  - Current Theme for the Plugin to use
  - Current Channel (`LIVE, HOTFIX, PTU, EPTU`) 
  - Custom Installation Paths (if auto-detection fails)
  - Force Redetection for auto detection (e.g. after moving the installation)
  - Factory Reset (clears cached installs + custom overrides, rebuilds keybindings; keeps theme)


- **Mouse Wheel Support**: Supports mouse wheel actions for bindings that utilize mouse wheel input (Mouse Wheel Up/Down).  
  

- **Custom Language Support**: Supports custom language files for localization when using custom global.ini from the Community, e.g. [StarCitizen-Deutsch-INI by rjcncpt](https://github.com/rjcncpt/StarCitizen-Deutsch-INI).


- **Support for all Channels**: `LIVE, HOTFIX, PTU, and EPTU` support. You can switch between channels via Control Panel Key. 


- **Auto-Detection of Star Citizen Installation Path**: Automatically detects the installation path of Star Citizen.


- **Theme Support**: Themes for customizing the appearance of the plugin. Includes a template for creating your own themes!


- **(Experimental) Click Sound**: Provides audio feedback on key presses with configurable sound files (.wav and .mp3).


## Install

See the full install guide: https://jarex985.github.io/SCStreamDeck/install/

## Development

- Build: `dotnet build SCStreamDeck.sln -c Release`
- Test: `dotnet test Tests/Tests.csproj -c Release --no-build`

## Credits

Star Citizen Stream Deck Plugin uses the following open-source projects and libraries:

- [streamdeck-tools by BarRaider](https://github.com/BarRaider/streamdeck-tools) - for the excellent C# library.
- [sdpi-components by GeekyEggo](https://github.com/GeekyEggo/sdpi-components) - for the excellent Stream Deck Property Inspector components.
- [InputSimulatorPlus by TChatzigiannakis](https://github.com/TChatzigiannakis/InputSimulatorPlus) - (although i think this might be a modified fork of BarRaider, will verify later)
- [NAudio by Mark Heath](https://github.com/naudio/NAudio) - for audio playback support.

## Acknowledgements

This project was inspired by the following repositories (code rewritten from scratch and optimized):

- [unp4k by dolkensp](https://github.com/dolkensp/unp4k) - for letting me browse through the P4K file and understand its structure.
- [SCJMapper-V2 by SCToolsfactory](https://github.com/SCToolsfactory/SCJMapper-V2) - for the great work on Star Citizen keybindings extraction.
- [streamdeck-starcitizen by mhwlng](https://github.com/mhwlng/streamdeck-starcitizen) - for the initial idea of a Stream Deck plugin for Star Citizen. :)
