![Star Citizen Stream Deck Plugin](./docs/assets/images/scsd-banner.png)


User Guide: https://jarex985.github.io/SCStreamDeck/  
Download latest Release: [Here](https://github.com/Jarex985/SCStreamDeck/releases/latest/download/com.jarex985.scstreamdeck.streamDeckPlugin)  
Report bugs / feature requests: https://github.com/Jarex985/SCStreamDeck/issues

<br>

> [!WARNING]
> **This site is not endorsed by or affiliated with the Cloud Imperium or Roberts Space Industries group of companies. 
> All game content and materials are copyright Cloud Imperium Rights LLC and Cloud Imperium Rights Ltd.. Star Citizen®, 
> Squadron 42®, Roberts Space Industries®, and Cloud Imperium® are registered trademarks of Cloud Imperium Rights LLC. All rights reserved.**  

<br>

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
[![Buy Me A Coffee](https://img.shields.io/badge/BuyMeACoffee-Support-yellow?style=flat&logo=buy-me-a-coffee&logoColor=white)](https://www.buymeacoffee.com/jarex9851)
[![Ko-Fi](https://img.shields.io/badge/Ko--Fi-Support-orange?style=flat&logo=kofi&logoColor=white)](https://ko-fi.com/jarex985)
[![PayPal](https://img.shields.io/badge/PayPal-Support-blue?style=flat&logo=paypal&logoColor=white)](https://www.paypal.me/jarex985)



## Current Features

- **Adaptive Key**: A key that executes keybindings based on the activation mode for a given binding.
    - *Example:* Two in-game bindings (Tap vs Hold) on `Num-` executes only the Tap function when this is the assigned function.
- **Toggle Key**: A key that toggles between two states (e.g., landing gear up/down). Can be reset to match the current in-game state on de-sync.
- **Control Panel Key**: A dedicated key for managing global plugin settings such as themes, channel selection, and installation paths.
- **Auto-Detection of Star Citizen Installation Path**: Automatically detects the installation path of Star Citizen.
- **Multiple Channels Support**: Supports different Star Citizen channels including LIVE, HOTFIX, PTU, and EPTU.
- **Mouse Wheel Support**: Supports mouse wheel actions for bindings that use mouse wheel input (Mouse Wheel Up/Down).
- **Custom Language Support**: Supports custom language files for localization when using custom global.ini from the community, e.g. [StarCitizen-Deutsch-INI by rjcncpt](https://github.com/rjcncpt/StarCitizen-Deutsch-INI).
- **Theme Support**: Themes for customizing the appearance of the plugin. Includes a template for creating your own themes!
- **Click Sound**: Provides audio feedback on key presses with configurable sound files (.wav and .mp3).


## Installation

See the full installation guide: https://jarex985.github.io/SCStreamDeck/install/


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
