# Troubleshooting

## The plugin does not show up in Stream Deck

1. Confirm the Stream Deck app is version 6.4+.
2. Confirm .NET 8 Desktop Runtime is installed.
3. Close Stream Deck completely then open it again.
4. Go to `%APPDATA%\Elgato\StreamDeck\Plugins`. If you see a folder named `com.jarex985.scstreamdeck.sdPlugin`, delete it and try reinstalling after confirming steps 1 and 2.

## Double-clicking `com.jarex985.scstreamdeck.streamDeckPlugin` does nothing

1. Right-click the downloaded file and choose `Properties`.
2. If you see an `Unblock` checkbox, enable it.
3. Click `OK` and try again.
4. Make sure that you uninstalled any previous versions of the plugin.

!!! note
    Windows SmartScreen or antivirus can block new downloads. If the file was removed, download it again.

## Star Citizen path not detected

1. Drag `Control Panel` from the right panel and drop it to a desired key on the left.
2. Set a custom installation path for your desired channel (`LIVE`, `HOTFIX`, `PTU`, `EPTU`).

## Actions do nothing in game

- Make sure Star Citizen is the active window.
- Try running the Stream Deck app as Administrator.
- If you changed any keybinding in-game while using the Plugin, you can either:

    1. Restart Stream Deck app.
    2. Use `Control Panel` and click `FORCE REDETECTION`.

