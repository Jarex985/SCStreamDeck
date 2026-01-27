# Tests

## Structure

- Unit/Common: Unit/Common: shared utilities and common test behaviors
- Unit/Services: service-level unit tests
- Integration:  end-to-end flows across multiple components
- Security: path validation and security-sensitive scenarios
- TestData: fixtures and sample payloads (see `Tests/TestData/README.md`)

## Test Data Setup

- `LIVE/LIVE-keybindings.json`:  Contains an example keybindings file for integration tests.
- Sample CryXML fixture files:: `Tests/TestData/actionmaps_sample.xml`, `Tests/TestData/localization_sample.xml`.

## Running Tests

- All tests: `dotnet test Tests/Tests.csproj`
- Filter by class/name: `dotnet test --filter "FullyQualifiedName~ClassName" -- Tests/Tests.csproj`
- Collect coverage: `dotnet test Tests/Tests.csproj --collect:"XPlat Code Coverage"`

## Coverage

- coverlet.collector is invoked via `--collect:"XPlat Code Coverage"` and writes reports into `Tests/TestResults/coverage/` (json, cobertura)
- No thresholds are enforced; coverage is informational/for visibility.
- To change output location/format, edit `Tests/Tests.csproj` coverlet properties
