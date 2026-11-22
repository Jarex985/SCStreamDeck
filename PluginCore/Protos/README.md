# Telemetry proto

`sc_telemetry_v1.proto` is the schema of Star Citizen's local gRPC telemetry API (`sc.game.telemetry.v1`).
It is unofficial: recovered from the descriptors embedded in `StarCitizen.exe`. The reference copy and the notes
live in the `sharing` repository; this file is compiled by `Grpc.Tools` (see `PluginCore.csproj`) into the client used by
the `Telemetry Display` key. Update it by replacing the file with the newer reference copy; field changes show up in the
Property Inspector automatically because the attribute list is generated from the descriptors.

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
