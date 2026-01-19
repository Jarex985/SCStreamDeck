using SCStreamDeck.Services.Data;

namespace Tests.Integration.Testing;

public sealed class P4KFixture : IDisposable
{
    public P4KFixture()
    {
        P4KArchiveService = new P4KArchiveService();
        string? envPath = Environment.GetEnvironmentVariable("SCSTREAMDECK_DATA_P4K_PATH");
        string resolvedEnvPath = string.IsNullOrWhiteSpace(envPath)
            ? string.Empty
            : envPath;

        string defaultP4KPath = IntegrationTestBase.ResolvePath("Tests/TestData/Data.p4k");
        if (!File.Exists(defaultP4KPath))
        {
            string liveDirP4K = IntegrationTestBase.ResolvePath("Tests/TestData/LIVE/Data.p4k");
            if (File.Exists(liveDirP4K))
            {
                defaultP4KPath = liveDirP4K;
            }
        }

        DataP4KPath = string.IsNullOrWhiteSpace(resolvedEnvPath)
            ? defaultP4KPath
            : resolvedEnvPath;
    }

    public P4KArchiveService P4KArchiveService { get; }

    public string DataP4KPath { get; }

    public void Dispose()
    {
        P4KArchiveService.CloseArchive();
        P4KArchiveService.Dispose();
    }
}
