# Telemetry proto

`sc_telemetry_v1.proto` is the schema of Star Citizen's local gRPC telemetry API (`sc.game.telemetry.v1`).
It is unofficial: recovered from the descriptors embedded in `StarCitizen.exe`.

**At runtime the plugin does not rely on this file.** On first use of a telemetry key it reads the protocol
definition straight from the installed game (`<channel>\Bin64\StarCitizen.exe`, see `ProtoDescriptorExtractor`), caches it
in the plugin's `cache` folder per exe size and modification time, and uses it to build the attribute list and to decode the
stream. The attributes therefore always match the installed build, also after a patch. This bundled copy is the fallback
(game not found, or its definitions could not be read) and is what the unit tests and the mock server use. To refresh it,
replace the file with the newer reference copy from the `sharing` repository.

## Enabling telemetry in the game

The server listens on `127.0.0.1:2942` (plaintext, no authentication) and is **off by default**.

- **4.10.3 LIVE** (display only): close the game and RSI Launcher, run `setx GRPC_CLIENT_SERVER_ENABLED true`, start the launcher, then the game.
- **5.0 PTU**: add `server.game.telemetry.enabled=1` to `user.cfg` in the channel folder.

The stream starts once the character has spawned.

## Testing without the game

```powershell
dotnet run --project Tools/MockTelemetryServer
```

serves a fake stream (pressure, health, locomotion, breathable atmosphere) on `127.0.0.1:2942`.
