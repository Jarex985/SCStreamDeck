using SCStreamDeck.Services.Keybinding;

namespace Tests.Integration.Testing;

public sealed class KeybindingPipelineFixture : IDisposable
{
    public KeybindingPipelineFixture()
    {
        LoaderService = new KeybindingLoaderService();
        string? envPath = Environment.GetEnvironmentVariable("SCSTREAMDECK_LIVE_KEYBINDINGS_PATH");
        string resolvedEnvPath = string.IsNullOrWhiteSpace(envPath)
            ? string.Empty
            : envPath;

        string defaultLivePath = IntegrationTestBase.ResolvePath("Tests/TestData/LIVE/LIVE-keybindings.json");
        if (!File.Exists(defaultLivePath))
        {
            defaultLivePath = IntegrationTestBase.ResolvePath("Tests/TestData/LIVE-keybindings.json");
        }

        KeybindingsPath = string.IsNullOrWhiteSpace(resolvedEnvPath)
            ? defaultLivePath
            : resolvedEnvPath;
    }

    public KeybindingLoaderService LoaderService { get; }

    public string KeybindingsPath { get; }

    public void Dispose()
    {
    }
}
