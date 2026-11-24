# Usage

## Adaptive Key

Use `Adaptive Key` for bindings you want to trigger in Star Citizen.
This is the most common key type you will use.

Basic flow:

1. Drag `Adaptive Key` onto a Stream Deck key.
2. Click on it to open the Property Inspector.
3. Select the Star Citizen function you want.
4. (Optional) Select a sound file (.wav/.mp3) from your system.

![Adaptive Key](assets/images/adaptive-key.png){ style="width:50%; height:auto;" }

## Toggle Key

Use `Toggle Key` for bindings that have two states (e.g., landing gear up/down).
You can also set a Reset threshold to resync the key state if it gets out of sync with the game.
While you can use all functions with Toggle Keys, they are best suited for functions that have a clear On/Off state.

Basic flow:

1. Drag `Toggle Key` onto a Stream Deck button.
2. Click on it to open the Property Inspector.
3. Select the Star Citizen function you want.
4. (Optional) Set a Reset threshold (from 0.2 to 10 seconds, default is 1). This defines how long you need to hold the key to reset its state (On → Off or Off → On).
5. (Optional) Select a Sound file (.wav/.mp3) from your system.

![Toggle Key](assets/images/toggle-key.png){ style="width:50%; height:auto;" }

## Telemetry Display Key

Use `Telemetry Display` to show a live value from Star Citizen's local telemetry API (vitals, environment, ship state and more) and change the key's text, image and colors based on that value.
For example, show `Pressure` and switch to a "low pressure" image below a threshold.

Requirements:

- The game's telemetry server must be enabled (it is off by default). See the [telemetry setup guide](https://github.com/Jarex985/SCStreamDeck/blob/main/PluginCore/Protos/README.md).
- Your character must have spawned. Until then the key shows the default appearance with `—`.
- Ship values (speed, shields, ...) only exist while you are seated; about 3 seconds after you leave the seat the key falls back to its Default section.
- The list of attributes is read from the installed game (`StarCitizen.exe` of the channel you selected in the Control Panel), so it always matches your game version. If the game cannot be found, a built-in list is used. The protocol that was read is exported next to the plugin log as `telemetry-protocol.proto` (readable) and `telemetry-protocol.desc` (binary, for `grpcurl -protoset`).

Basic flow:

1. Drag `Telemetry Display` onto a Stream Deck key and open the Property Inspector.
2. Search and select the attribute to show (for example `Actor Vitals › Pressure`). Gauges (health, oxygen, ...) also offer a `percent` value.
3. (Optional) Set a Label (used for `{label}`) and the number of Decimals.
4. Add Rules. The first rule whose condition matches wins:
    - Numbers: is less than / at most / greater than / at least / between / equals / does not equal
    - On/Off values and enums (for example `Locomotion`): is / is not
    - Per rule: Text (`{value}` and `{label}` are replaced, `\n` is a new line), Image (.png/.jpg/.bmp/.gif), Text and Back colors, an optional Flash, and an optional Press action
    - Sound: a .wav/.mp3 played once each time the rule becomes the active one (not for the Default section, and not when the key first appears)
    - Flash: enter a time in ms (minimum 250). While that rule is active the text and image are shown for that long, then hidden (only the background color remains) for the same time, repeating. Empty = no flashing
5. The Default section is used when no rule matches or there is no data.
6. (Optional) Select a Press sound (.wav/.mp3), played when a press action runs.

Press actions use the same keybindings as `Adaptive Key`, run for the rule that is active when you press the key, and only run while telemetry data is available.

## Advanced Telemetry Display Key

Use `Advanced Telemetry Display` when a state depends on **several attributes at once**, for example a warning light that appears only when the ship is **landed AND low on hydrogen fuel**.
It has the same text, image, color, flash, sound and press-action options as `Telemetry Display`; only the rule conditions differ.

1. Drag `Advanced Telemetry Display` onto a key and open the Property Inspector.
2. (Optional) Pick a Display attribute. It only feeds `{value}` and `{label}` in the rule texts.
3. Add a Rule and build its **When** part: choose `All of these (AND)` or `Any of these (OR)`, then add conditions. Each condition has its own attribute, operator and value (for example `Is Landed` is `On`, `Hydrogen Fuel` is less than `20`).
4. In a rule's Text you can print any attribute with `{value:attribute.path}`, for example `REFUEL\n{value:vehicle.state.flight.instruments.hydrogen_fuel}`.
5. Rules are checked in order and the first one that matches wins. A condition on an attribute that has no data (for example ship values while you are on foot) is false, so such a rule does not match. The Default section applies when no rule matches.
6. Each rule (and the Default section) has a **Press** list: add one or more actions, optionally with a delay in ms before each one. When you press the key, the actions of the rule that is active at that moment run in order. A single action without a delay behaves like `Adaptive Key` (it is held while you hold the key); with several actions or a delay, each step is a short tap. Pressing again while a sequence is still running is ignored.

Example toggle on one key: Rule 1 = `Gear` is `Deployed` shows "Gear DOWN" and presses the landing gear action; the Default section shows "Gear UP" and presses the same action, so the key always shows and does the right thing.

Example warning light: Rule 1 = ALL of `Is Landed` is `On`, `Hydrogen Fuel` is less than `20`; set a red image, Flash `1000` and a sound.

## Deceleration Key

Use `Deceleration` to see how far you will still travel, and how long it will take, until a value reaches zero while it is dropping. For the ship's speed in m/s the cumulative units are meters, so the key answers "how much distance do I need to stop?".

1. Drag `Deceleration` onto a key and open the Property Inspector.
2. Pick a numeric attribute (for example `Vehicle State Flight Instruments › Speed`).
3. Set the **Window** (ms, default 1000): the key compares the value now with the value one window ago. A longer window gives a steadier estimate, a shorter one reacts faster.
4. Choose what to **Show**: units and time, units only, or time only. **Divide by** changes the order of magnitude of the units (1000 turns meters into kilometers; pick a preset or type your own) and **Unit** is the suffix shown after them, for example `km`. **Round units** / **Round time** round to a multiple in the displayed unit (for example 0.5 shows 2.0 km instead of 1.96 km, 50 shows 1,950 m instead of 1,963 m); empty keeps one decimal.
5. Rules work like those of `Advanced Telemetry Display`: each rule combines conditions (all / any) on **any attributes** and on the estimate itself (`Estimate (units)` in the displayed unit, `Estimate (time, s)`). Example: "Estimate (units) is less than 2 AND Is Landed is Off" turns the key red when you are braking within 2 km of standing still while flying. A condition on the estimate is false while there is no estimate. Rules, images, colors, flash, sounds and several press actions per rule work as on the advanced key. Texts can use `{units}`, `{time}`, `{value}` (the selected output), `{label}` and `{value:attribute.path}` for any attribute. Rules saved earlier with a single comparison are converted to a condition on the estimate when you open the settings.

The estimate assumes the value keeps dropping at the rate seen over the window: time = value / rate and units = value² / (2 × rate). It needs one full window of history first, and while the value is not clearly decreasing (constant, increasing, already zero, or no data) the key shows `—` and uses the Default section.

## Control Panel Key

Use `Control Panel` for global plugin settings:

- Theme
- Channel (`LIVE`, `HOTFIX`, `PTU`, `EPTU`)
- Custom installation paths (if auto-detection fails)
- Force Redetection
- Factory reset

![Control Panel](assets/images/control-panel.png){ style="width:50%; height:auto;" }


!!! note "What Force Redetection & Factory Reset do"
    - **Force Redetection**: Re-runs the auto-detection of your Star Citizen installation path and keybindings. Use this if you moved your installation or changed keybindings in-game.
    - **Factory Reset**: Clears cached installs, your current theme, and custom overrides, then rebuilds keybindings.
