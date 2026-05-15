# Linux (Wine) Support

SCStreamDeck is a Windows .NET plugin. On Linux, it runs under
[Wine](https://www.winehq.org/) inside a plugin host such as
[OpenDeck](https://github.com/nekename/OpenDeck) — which natively spawns the
`.exe` through Wine when the manifest declares the `linux` platform.

Under Wine, Win32 `SendInput` produces synthetic input messages that **stay
inside the Wine process tree** and never reach native Linux applications
such as Star Citizen launched via Proton/Lutris/Heroic. The plugin works
around this by detecting Wine at runtime and routing input through
[`ydotool`](https://github.com/ReimuNotMoe/ydotool) — a Linux utility that
synthesises events through `/dev/uinput`, so they are seen as real keyboard
and mouse activity by every application in the session, including games.

When running on native Windows nothing changes: the patch is a *passive*
branch that only activates if it detects Wine.

---

## How it works

```
OpenDeck (native Linux Tauri app)
    │
    │ spawn (Wine, declared in manifest "OS": [{"Platform":"linux"}])
    ▼
SCStreamDeck.exe ── BarRaider SD Tools ── WebSocket ── back to OpenDeck
    │
    │ keyDown
    ▼
ActionKeys/AdaptiveKey.cs · ToggleKey.cs
    │
    ▼
PluginCore/Services/Keybinding/ActivationHandlers/KeybindingInputExecutor.cs
    │
    ▼
WindowsInput.InputSimulator
  → KeyboardSimulator / MouseSimulator → InputBuilder → INPUT[]
    │
    ▼
IInputMessageDispatcher.DispatchInput(INPUT[])
    │
    ├── Windows (native)  →  NativeMethods.SendInput  →  kernel
    │
    └── Wine            →  LinuxInputBridge
                                │
                                ▼
                          Z:\usr\bin\ydotool key …
                                │
                                ▼ Unix socket
                          ydotoold daemon (native Linux)
                                │
                                ▼ /dev/uinput
                          kernel → focused window (e.g. Star Citizen)
```

The branch is taken inside
[`WindowsInputMessageDispatcher.DispatchInput`](https://github.com/Jarex985/SCStreamDeck/blob/main/InputSimulatorPlus/WindowsInput/WindowsInputMessageDispatcher.cs):
the existing dispatcher first checks `WineDetector.IsWine` and forwards the
`INPUT[]` array to `LinuxInputBridge.DispatchInput` instead of calling
`SendInput` when it is `true`.

### `WineDetector`

Single-shot probe that resolves `ntdll!wine_get_version` via `LoadLibrary`
+ `GetProcAddress`. The symbol exists only inside Wine, so the boolean is
true under Wine and false everywhere else. Result is cached in a
`Lazy<bool>` for the lifetime of the process.

### `LinuxInputBridge`

Walks the `INPUT[]` array once per `DispatchInput` call:

| INPUT type | Translation |
|---|---|
| `INPUT_KEYBOARD` with VK | `Vk2Evdev.TryGet(vk)` → evdev keycode |
| `INPUT_KEYBOARD` with `KEYEVENTF_SCANCODE` | `Vk2Evdev.TryFromScan(scan, extended)` → evdev (PS/2 Set 1 identity for non-extended; small table for extended) |
| `INPUT_KEYBOARD` with `KEYEVENTF_UNICODE` | `ydotool type "<char>"` |
| `INPUT_MOUSE` button down/up | `ydotool click 0x<C/4/8><btn>` |
| `INPUT_MOUSE` wheel | `ydotool mousemove --wheel -- dx dy` |

All keyboard events from a single `DispatchInput` invocation are batched
into one `ydotool key …` call so a 4-key combo only costs one
`fork+exec` (~5–20 ms).

The bridge resolves the daemon socket at startup by reading
`Z:\proc\self\environ` (the Linux process environment is reachable from
Wine via the `Z:` mapped drive). It honours `$XDG_RUNTIME_DIR/.ydotool_socket`
where modern systemd-user setups put the socket, and falls back to
`/tmp/.ydotool_socket` on older configurations. The `ydotool` binary path
defaults to `/usr/bin/ydotool` and may be overridden via the
`YDOTOOL_PATH` environment variable.

Unknown virtual-keys and unknown scan codes are reported once per process
on `Console.Error`. They show up in the plugin's log under
`~/.local/share/opendeck/logs/plugins/com.jarex985.scstreamdeck.sdPlugin.log`
prefixed with `[LinuxInputBridge]`. File a `Vk2Evdev` addition if you see
real keystrokes mapped to game actions appearing there.

### `Vk2Evdev`

Translation table between Win32 virtual-key codes (and PS/2 Set 1 / DIK
scan codes) and Linux evdev keycodes. The full keyboard is covered:
alpha-num, function row, modifiers (with L/R distinction), numpad,
navigation cluster, OEM punctuation (US layout for VK; PS/2 scan path
covers all layouts).

---

## Prerequisites

All of the following must be in place before SCStreamDeck can deliver
input on Linux.

### 1. OpenDeck

```bash
# Arch / CachyOS
yay -S opendeck-bin

# Or via Flathub (note: the Flatpak build cannot spawn the .exe under Wine
# for license/sandboxing reasons — install the native package instead).
```

### 2. Wine

```bash
sudo pacman -S wine
```

Wine 9.0 or newer is recommended for the .NET 8 desktop runtime that
SCStreamDeck targets. Wine-Mono is bundled with current Wine builds and
provides the desktop runtime out of the box.

### 3. .NET 8 SDK (only if building from source)

```bash
sudo pacman -S dotnet-sdk-8.0
```

A pre-built artefact ships with each release on GitHub; this is only
required if you build the plugin yourself.

### 4. `ydotool` + `ydotoold`

```bash
sudo pacman -S ydotool
systemctl --user enable --now ydotool
```

Confirm the socket exists at the expected location:

```bash
ls -la "$XDG_RUNTIME_DIR/.ydotool_socket"
# srw------- 1 you you 0 ... /run/user/1000/.ydotool_socket
```

If you see `/tmp/.ydotool_socket` instead, that also works — the bridge
auto-detects.

### 5. `/dev/uinput` permission

`ydotoold` runs unprivileged and needs write access to `/dev/uinput`. By
default that device is owned `root:root mode 0600`. Three ways to grant
access, in order of preference:

**A. You already have it.** Some packages (OpenLinkHub, Razer driver,
Steam) ship a permissive udev rule that adds ACLs for your user. Check:

```bash
getfacl /dev/uinput
# user::rw-
# user:you:rw-       ← if this line is present, you're done
```

**B. Group-based rule.** If no ACL is present, install a udev rule that
gives the `input` group access:

```bash
echo 'KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"' \
  | sudo tee /etc/udev/rules.d/99-uinput.rules
sudo udevadm control --reload-rules
sudo udevadm trigger /dev/uinput
sudo usermod -aG input "$USER"
# Log out and back in for the new group to take effect.
```

**C. Loosen permissions (last resort).** Less secure — any process can
synthesise input:

```bash
sudo chmod 666 /dev/uinput
```

---

## Building from source

```bash
git clone https://github.com/Alexconquer/SCStreamDeck.git
cd SCStreamDeck
# Linux cross-compile to win-x64: the SDK pulls the Windows Desktop
# reference pack from NuGet (enabled via <EnableWindowsTargeting>).
dotnet publish PluginCore/PluginCore.csproj \
  -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=false \
  -o "$HOME/.config/opendeck/plugins/com.jarex985.scstreamdeck.sdPlugin"
```

Then restart OpenDeck (close fully via the tray icon, reopen). On
startup you should see in
`~/.local/share/opendeck/logs/opendeck.log`:

```
Registered plugin com.jarex985.scstreamdeck.sdPlugin
```

and in the per-plugin log:

```
[WindowsInput] WineDetector.IsWine = True
```

> **Important:** do not deploy the plugin via a symlink that resolves to a
> directory called `publish/` (e.g. the default `bin/Release/.../win-x64/publish/`).
> OpenDeck registers the plugin under the resolved directory's basename,
> so it would appear as `publish` instead of
> `com.jarex985.scstreamdeck.sdPlugin`, breaking the property-inspector
> paths stored inside existing button configurations. Publish directly
> with `-o <plugin-dir>` as shown above, or copy the publish output into
> the correctly-named folder.

---

## Verifying the setup

After OpenDeck restart, open any text editor (gedit, kate, vim in a
terminal), give it focus, and press a Stream Deck button whose
`function` references a keyboard-bound SC action — the keystroke must
appear in the editor.

If nothing happens, in order:

1. **Plugin loaded?** Check `~/.local/share/opendeck/logs/opendeck.log`
   for `Registered plugin com.jarex985.scstreamdeck.sdPlugin`. If absent,
   the manifest's `OS` array might be missing `linux`, or the plugin
   directory has the wrong name.
2. **Wine detected?** Grep
   `~/.local/share/opendeck/logs/plugins/com.jarex985.scstreamdeck.sdPlugin.log`
   for `WineDetector.IsWine = True`. If it says `False`, your Wine build
   is hiding the `wine_get_version` symbol (uncommon).
3. **ydotool daemon up?** `systemctl --user status ydotool` and
   `ls $XDG_RUNTIME_DIR/.ydotool_socket`.
4. **uinput accessible?** `getfacl /dev/uinput` and look for an entry
   for your user, or check group membership.
5. **Unknown keystrokes?** Look for `[LinuxInputBridge] unknown VK` or
   `unknown SCAN` lines in the plugin log. File an issue with the line
   contents and the SC action you triggered, so the relevant entry can
   be added to `Vk2Evdev`.

---

## Limitations

- **Latency**: each `DispatchInput` call spawns `ydotool` once (regardless
  of combo size). Cold spawn is typically 5–20 ms on a modern desktop.
  For sustained rapid input this is noticeable; for SC action buttons
  it is imperceptible. A future improvement would be to talk to the
  ydotool socket directly from Wine via WinSock.
- **Joystick / gamepad / VR bindings**: these are not synthesised by
  `ydotool` and are out of scope. SCStreamDeck's existing behaviour
  (game bindings configured in-game's actionmaps) remains intact for
  keyboard and mouse bindings only.
- **Mouse buttons** beyond left/right/middle and side-button 1/2 are
  not currently mapped — patches welcome.
- **macOS** is not handled. The Wine detector returns `false` there and
  the code falls through to `SendInput`, which won't work on macOS. If
  someone wants to add a `LinuxInputBridge` equivalent for macOS
  (e.g. via `CGEventPost`), the architecture supports it cleanly: add a
  new dispatcher and a corresponding `OsDetector` check.

---

## File picker paths (audio, Data.p4k)

OpenDeck's Property Inspector runs in the host's WebView, not under Wine.
When you click the "FILE" button to pick an audio click sound (Adaptive
Key / Toggle Key) or a `Data.p4k` install override (Control Panel), the
dialog that appears is the host's **native Linux** file picker. It hands
back paths like `/home/user/Music/foo.mp3` rather than the `C:\…` or
`Z:\…` form Wine would normally expose.

The plugin compensates server-side: `WinePath.Translate` rewrites any
incoming Unix-style absolute path to `Z:\…` when running under Wine,
before the path is passed to `File.Exists`, `NAudio.AudioFileReader`,
or the P4K opener. Existing Windows-style paths (e.g. saved from a
prior Windows install of SCStreamDeck) are left untouched.

The UI still displays the Linux-style path it captured — only the
behind-the-scenes I/O is rewritten. A future improvement would be a
custom Wine-aware file dialog rendered by the plugin itself; not in
scope here.

## Internals for contributors

The Wine-aware path is contained in five files; everything else in the
codebase is untouched.

| File | Purpose |
|---|---|
| [`InputSimulatorPlus/WindowsInput/WineDetector.cs`](https://github.com/Jarex985/SCStreamDeck/blob/main/InputSimulatorPlus/WindowsInput/WineDetector.cs) | One-shot Wine probe via `wine_get_version` |
| [`InputSimulatorPlus/WindowsInput/LinuxInputBridge.cs`](https://github.com/Jarex985/SCStreamDeck/blob/main/InputSimulatorPlus/WindowsInput/LinuxInputBridge.cs) | INPUT[] → ydotool command translation |
| [`InputSimulatorPlus/WindowsInput/Native/Vk2Evdev.cs`](https://github.com/Jarex985/SCStreamDeck/blob/main/InputSimulatorPlus/WindowsInput/Native/Vk2Evdev.cs) | VK + PS/2 scan code → evdev keycode tables |
| [`InputSimulatorPlus/WindowsInput/WindowsInputMessageDispatcher.cs`](https://github.com/Jarex985/SCStreamDeck/blob/main/InputSimulatorPlus/WindowsInput/WindowsInputMessageDispatcher.cs) | 4-line patch that delegates to `LinuxInputBridge` when `WineDetector.IsWine` |
| [`PluginCore/Infrastructure/WinePath.cs`](https://github.com/Jarex985/SCStreamDeck/blob/main/PluginCore/Infrastructure/WinePath.cs) | Rewrites Linux paths from the native file picker to `Z:\` form for NAudio / `File.*` consumers |

Two ancillary build adjustments make the project cross-build cleanly
from Linux to win-x64:

- `InputSimulatorPlus/WindowsInput/WindowsInput.csproj` was converted
  from the legacy MSBuild format to the SDK-style format targeting
  `net8.0-windows`. The legacy project required a .NET Framework 4.8
  targeting pack that isn't shipped on Linux.
- `PluginCore/PluginCore.csproj` adds `<EnableWindowsTargeting>true</EnableWindowsTargeting>`
  so the Windows Desktop reference pack is pulled from NuGet when
  building on Linux. The setting is a no-op on Windows.

Manifest change: `"linux"` is appended to the `OS` array in
`PluginCore/manifest.json` so OpenDeck spawns the plugin via Wine on
Linux hosts.
