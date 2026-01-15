# Star Citizen Stream Deck Plugin 


#### *A Stream Deck plugin that provides bindable actions for Star Citizen, allowing seamless integration of game controls with your Stream Deck device.*

---

## Table of Contents

1. [Requirements](#requirements)
2. [Project Info](#project-info)
3. [Current Features](#current-features)
4. [Installation](#installation)
5. [Usage](#usage)
6. [Acknowledgements](#acknowledgements)
7. 
---

## _Requirements_

[![Windows 10+](https://img.shields.io/badge/Windows-10%2B-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Stream Deck v6.4+](https://img.shields.io/badge/Stream%20Deck%20App-6.4%2B-purple?logo=elgato&logoColor=white)](https://www.elgato.com/s/stream-deck-app)
[![.NET 8 Runtime](https://img.shields.io/badge/.NET%20Runtime-8.0-blue?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## _Project Info_

___

#### Status
[![GitHub release](https://img.shields.io/github/release/Jarex985/SCStreamDeck?include_prereleases=&sort=semver&color=2ea44f)](https://github.com/Jarex985/SCStreamDeck/releases/)
[![License](https://img.shields.io/badge/License-MIT-2ea44f)](LICENSE.md)
[![Contributions - welcome](https://img.shields.io/badge/Contributions-welcome-2ea44f)](CONTRIBUTING.md)

#### Programming Languages
[![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp)
[![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white)](https://developer.mozilla.org/docs/Web/HTML)
[![CSS3](https://img.shields.io/badge/CSS3-1572B6?logo=css3&logoColor=white)](https://developer.mozilla.org/docs/Web/CSS)
[![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?logo=javascript&logoColor=black)](https://developer.mozilla.org/docs/Web/JavaScript)

#### IDE / Tools  
[![JetBrains Rider](https://img.shields.io/badge/JetBrains%20Rider-000000?logo=JetBrains&logoColor=white)](https://www.jetbrains.com/rider/)
[![JetBrains WebStorm](https://img.shields.io/badge/JetBrains%20WebStorm-000000?logo=JetBrains&logoColor=white)](https://www.jetbrains.com/webstorm/)

#### Support / Funding  
[![Buy Me A Coffee](https://img.shields.io/badge/Buy_Me_A_Coffee-Link-2ea44f)](https://www.buymeacoffee.com/jarex9851)

---


> [!WARNING]
>  **Note:** This is, just like Star Citizen, an alpha build. Some features are still missing and may not be fully stable.

## <ins>_Current Features_</ins>

- **Adaptive Key**: Dynamically bindable key actions for Star Citizen that execute only their assigned function based on the ActivationMode.
    - *Example:* Tap vs Hold on `Num-` executes only the Tap function.  


- **Mouse Wheel Support**: Supports mouse wheel actions for bindings that utilize mouse wheel input (Mouse Wheel Up/Down).  
  

- **Custom Language Support**: Supports custom language files for localization when using custom global.ini from the Community, e.g. [StarCitizen-Deutsch-INI by rjcncpt](https://github.com/rjcncpt/StarCitizen-Deutsch-INI).


- **Basic Support for all Channels**: Basic implementation for supporting `HOTFIX, PTU, and EPTU` in the future. Currently only `LIVE` is fully supported, but the framework is in place for switching to other channels.

___

## <ins>_Installation_</ins>


### <ins>Prebuilt Installation</ins>

1. Download the latest release from [Releases](https://github.com/Jarex985/SCStreamDeck/releases).
2. Double-click the `.streamDeckPlugin` file to install.

> [!IMPORTANT]
> **When updating to a newer Release Build, you need to uninstall the Plugin first.**

### <ins>Manual Installation</ins>

1. Clone the repository:

   ```bash
   git clone https://github.com/Jarex985/SCStreamDeck.git
   ```

2. Open the solution in Visual Studio or your preferred IDE.
3. Build the project in Release mode.
4. Locate the built plugin folder in `PluginCore/bin/Release/`.
5. Pack the Plugin using the Stream Deck SDK or manually copy the folder to the Stream Deck plugins directory:
   - Windows: `%APPDATA%/Elgato/StreamDeck/Plugins`
6. Restart Stream Deck software if necessary.

___

## <ins>_Usage_</ins>

1. After installation, add the "Adaptive Key" action to a button on your Stream Deck.
2. Configure the key binding in the property inspector to match your Star Citizen controls.

___

## <ins>_Acknowledgements_</ins>

This project was inspired by the following repositories (code rewritten from scratch and optimized):

- [unp4k by dolkensp](https://github.com/dolkensp/unp4k) - for letting me browse through the P4K file and understand its structure.
- [SCJMapper-V2 by SCToolsfactory](https://github.com/SCToolsfactory/SCJMapper-V2) - for the great work on Star Citizen keybindings extraction.
- [streamdeck-starcitizen by mhwlng](https://github.com/mhwlng/streamdeck-starcitizen) - for the initial idea of a Stream Deck plugin for Star Citizen. :)

