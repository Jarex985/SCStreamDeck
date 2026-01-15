[![BuyMeACoffee](https://raw.githubusercontent.com/pachadotdev/buymeacoffee-badges/main/bmc-donate-white.svg)](https://www.buymeacoffee.com/jarex9851)

# ***Star Citizen Stream Deck Plugin***

A Stream Deck plugin that provides bindable actions for Star Citizen, allowing seamless integration of game controls with your Stream Deck device.

> [!WARNING]
>  **Note:** This is, just like Star Citizen, an alpha build. Some features are still missing and may not be fully stable.

### <ins>_Current Features_</ins>

- **Adaptive Key**: Dynamically bindable key actions for Star Citizen that execute only their assigned function based on the ActivationMode.
  - Since Star Citizen allows multiple functions to be bound to the same key, the Adaptive Key adapts its behavior based on the assigned functions ActivationMode.
    - **Example**: If you have two functions bound to `Num-` - one for Tap and one for Hold, and the Key is bound to the Tap function, pressing the Key will execute only the Tap function, regardless of how long you hold the key down.
- **Mouse Wheel Support**: Supports mouse wheel actions for bindings that utilize mouse wheel input (Mouse Wheel Up/Down).
- **Custom Language Support**: Supports custom language files for localization when using custom global.ini from the Community, e.g. [StarCitizen-Deutsch-INI by rjcncpt](https://github.com/rjcncpt/StarCitizen-Deutsch-INI).
- **Basic Support for all Channels**: Basic implementation for supporting `HOTFIX, PTU, and EPTU` in the future. Currently only `LIVE` is fully supported, but the framework is in place for switching to other channels.
___
## <ins>_Installation_</ins>

#### Requirements

- Windows 10 and later
- Stream Deck software version 6.4 and above
- .NET 8.0 runtime (ensure it is installed)

### <ins>Prebuilt Installation</ins>

- Download the latest release from the [Releases](https://github.com/Jarex985/SCStreamDeck/releases) page.
- Double-click the `.streamDeckPlugin` file to install it automatically.
  - When updating to a newer Release Build, you need to uninstall the Plugin first.

### <ins>Manual Installation</ins>

1. Clone the repository:
   ```
   git clone https://github.com/Jarex985/SCStreamDeck.git
   ```

2. Open the solution in Visual Studio or your preferred IDE.

3. Build the project in Release mode.

4. Locate the built plugin folder in `PluginCore/bin/Release/`.

5. Pack the Plugin using the Stream Deck SDK or manually copy the folder to the Stream Deck plugins directory:
   - Windows: `%APPDATA%/Elgato/StreamDeck/Plugins`

6. Restart Stream Deck software if necessary.

## <ins>_Usage_</ins>

1. After installation, add the "Adaptive Key" action to a button on your Stream Deck.
2. Configure the key binding in the property inspector to match your Star Citizen controls.
3. Use the button during gameplay for quick access to actions.

## <ins>_Contributing_</ins>

Contributions are welcome! Please see [CONTRIBUTING](CONTRIBUTING.md) for guidelines.

## <ins>_Acknowledgements_</ins>

This project was inspired by the following repositories (code rewritten from scratch, tailored, shortened, and optimized):

- [unp4k by dolkensp](https://github.com/dolkensp/unp4k) - for letting me browse through the P4K file and understand its structure.
- [SCJMapper-V2 by SCToolsfactory](https://github.com/SCToolsfactory/SCJMapper-V2) - for the great work on Star Citizen keybindings extraction.
- [streamdeck-starcitizen by mhwlng](https://github.com/mhwlng/streamdeck-starcitizen) - for the initial idea of a Stream Deck plugin for Star Citizen. :)

## <ins>_License_</ins>

This project is licensed under the MIT License - see the [LICENSE](LICENSE.md) file for details.
