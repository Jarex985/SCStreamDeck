# Tests

## Structure
- Unit/Common: shared utilities and common behaviors
- Unit/Services: service-level unit tests
- Integration: end-to-end flows across components
- Security: path validation and security-sensitive scenarios
- TestData: fixtures and sample payloads (see `Tests/TestData/README.md`)

## Test Data Setup
- `LIVE-keybindings.json`: copy your exported keybindings to `Tests/TestData/` or set env `SCSTREAMDECK_LIVE_KEYBINDINGS_PATH` to an absolute path.
- `Data.p4k`: place a copy in `Tests/TestData/` or set env `SCSTREAMDECK_DATA_P4K_PATH` to an absolute path.
- Optional: update `Tests/TestData/appsettings.test.json` if you prefer config-based paths; defaults assume env vars or local files.
- Sample CryXML fixtures: `Tests/TestData/actionmaps_sample.xml`, `Tests/TestData/localization_sample.xml`.

## Running Tests
- All tests: `dotnet test Tests/Tests.csproj`
- Filter by class/name: `dotnet test --filter "FullyQualifiedName~ClassName" -- Tests/Tests.csproj`
- Collect coverage: `dotnet test Tests/Tests.csproj --collect:"XPlat Code Coverage"`

## Coverage
- coverlet.collector runs via `--collect:"XPlat Code Coverage"` and writes reports under `Tests/TestResults/coverage/` (json, cobertura)
- No thresholds enforced; coverage is informational for visibility
- To change output location/format, edit `Tests/Tests.csproj` coverlet properties
