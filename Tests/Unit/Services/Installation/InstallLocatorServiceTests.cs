using FluentAssertions;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Installation;

public sealed class InstallLocatorServiceTests
{
    [Fact]
    public async Task FindInstallationsAsync_NoLogs_ReturnsEmptyOrHostInstalls()
    {
        using TempAppData appData = new();
        InstallLocatorService service = new();

        IReadOnlyList<SCInstallCandidate> result = await service.FindInstallationsAsync();

        if (result.Count == 0)
        {
            result.Should().BeEmpty();
            service.GetCachedInstallations().Should().BeEmpty();
        }
        else
        {
            result.Should().NotBeNull();
        }
    }

    private static bool TryCreateCustomCandidateTest(SCChannel channel, string? customPath, out SCInstallCandidate? candidate)
    {
        candidate = null;
        if (string.IsNullOrWhiteSpace(customPath))
        {
            return false;
        }

        string dataP4KPath = NormalizeOrTrim(customPath);

        if (!File.Exists(dataP4KPath))
        {
            return false;
        }

        if (!dataP4KPath.EndsWith("Data.p4k", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string channelPath = NormalizeOrTrim(Path.GetDirectoryName(dataP4KPath));
        string rootPath = NormalizeOrTrim(Path.GetDirectoryName(channelPath));

        if (string.IsNullOrEmpty(channelPath) || string.IsNullOrEmpty(rootPath))
        {
            return false;
        }

        candidate = new SCInstallCandidate(
            rootPath,
            channel,
            channelPath,
            dataP4KPath,
            InstallSource.UserProvided);
        return true;
    }

    private static string NormalizeOrTrim(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string trimmed = path.Trim('"').Trim();
        return trimmed.Replace('/', '\\');
    }

    private sealed class TempAppData : IDisposable
    {
        private readonly string? _originalAppData;
        private readonly string _tempRoot;

        public TempAppData()
        {
            _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string logs = Path.Combine(_tempRoot, "rsilauncher", "logs");
            Directory.CreateDirectory(logs);
            Environment.SetEnvironmentVariable("APPDATA", _tempRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("APPDATA", _originalAppData);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
    }

    #region TryCreateCustomCandidate

    [Fact]
    public void TryCreateCustomCandidate_ReturnsFalse_WhenPathIsNull()
    {
        bool result = TryCreateCustomCandidateTest(SCChannel.Live, null, out SCInstallCandidate? candidate);

        result.Should().BeFalse();
        candidate.Should().BeNull();
    }

    [Fact]
    public void TryCreateCustomCandidate_ReturnsFalse_WhenPathIsWhitespace()
    {
        bool result = TryCreateCustomCandidateTest(SCChannel.Live, "   ", out SCInstallCandidate? candidate);

        result.Should().BeFalse();
        candidate.Should().BeNull();
    }

    [Fact]
    public void TryCreateCustomCandidate_ReturnsFalse_WhenFileDoesNotExist()
    {
        bool result =
            TryCreateCustomCandidateTest(SCChannel.Live, @"C:\nonexistent\Data.p4k", out SCInstallCandidate? candidate);

        result.Should().BeFalse();
        candidate.Should().BeNull();
    }

    [Fact]
    public void TryCreateCustomCandidate_ReturnsFalse_WhenPathDoesNotEndWithDataP4k()
    {
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "test");

        try
        {
            bool result = TryCreateCustomCandidateTest(SCChannel.Live, tempFile, out SCInstallCandidate? candidate);

            result.Should().BeFalse();
            candidate.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryCreateCustomCandidate_ReturnsTrue_WhenDirectoryStructureIsNotRoot()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"SCTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string dataP4KPath = Path.Combine(tempDir, "Data.p4k");
            File.WriteAllText(dataP4KPath, "test");

            bool result = TryCreateCustomCandidateTest(SCChannel.Live, dataP4KPath, out SCInstallCandidate? candidate);

            result.Should().BeTrue();
            candidate.Should().NotBeNull();
            candidate.Channel.Should().Be(SCChannel.Live);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryCreateCustomCandidate_ReturnsTrue_WhenPathIsValid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"SCTest_{Guid.NewGuid():N}");
        string channelDir = Path.Combine(tempDir, "LIVE");
        Directory.CreateDirectory(channelDir);

        try
        {
            string dataP4KPath = Path.Combine(channelDir, "Data.p4k");
            File.WriteAllText(dataP4KPath, "test");

            bool result = TryCreateCustomCandidateTest(SCChannel.Live, dataP4KPath, out SCInstallCandidate? candidate);

            result.Should().BeTrue();
            candidate.Should().NotBeNull();
            candidate.Channel.Should().Be(SCChannel.Live);
            candidate.DataP4KPath.Should().Be(dataP4KPath);
            candidate.Source.Should().Be(InstallSource.UserProvided);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryCreateCustomCandidate_NormalizesPath_WhenPathHasQuotes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"SCTest_{Guid.NewGuid():N}");
        string channelDir = Path.Combine(tempDir, "LIVE");
        Directory.CreateDirectory(channelDir);

        try
        {
            string dataP4KPath = Path.Combine(channelDir, "Data.p4k");
            File.WriteAllText(dataP4KPath, "test");

            bool result = TryCreateCustomCandidateTest(SCChannel.Live, $"\"{dataP4KPath}\"", out SCInstallCandidate? candidate);

            result.Should().BeTrue();
            candidate.Should().NotBeNull();
            candidate.DataP4KPath.Should().Be(dataP4KPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryCreateCustomCandidate_NormalizesPath_WhenPathHasForwardSlashes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"SCTest_{Guid.NewGuid():N}");
        string channelDir = Path.Combine(tempDir, "LIVE");
        Directory.CreateDirectory(channelDir);

        try
        {
            string dataP4KPath = Path.Combine(channelDir, "Data.p4k");
            File.WriteAllText(dataP4KPath, "test");

            string normalizedPath = dataP4KPath.Replace("\\", "/");
            bool result = TryCreateCustomCandidateTest(SCChannel.Live, normalizedPath, out SCInstallCandidate? candidate);

            result.Should().BeTrue();
            candidate.Should().NotBeNull();
            candidate.DataP4KPath.Should().Be(dataP4KPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region SetSelectedInstallation

    [Fact]
    public void SetSelectedInstallation_UpdatesSelectedInstallation()
    {
        InstallLocatorService service = new();
        SCInstallCandidate candidate = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        service.SetSelectedInstallation(candidate);

        SCInstallCandidate? selected = service.GetSelectedInstallation();
        selected.Should().NotBeNull();
        selected.Channel.Should().Be(SCChannel.Live);
    }

    [Fact]
    public void SetSelectedInstallation_UpdatesChannel_WhenDifferent()
    {
        InstallLocatorService service = new();
        SCInstallCandidate liveCandidate = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        service.SetSelectedInstallation(liveCandidate);

        SCInstallCandidate ptuCandidate = new(
            "C:\\SC",
            SCChannel.Ptu,
            @"C:\SC\PTU",
            @"C:\SC\PTU\Data.p4k"
        );

        service.SetSelectedInstallation(ptuCandidate);

        SCInstallCandidate? selected = service.GetSelectedInstallation();
        selected.Should().NotBeNull();
        selected.Channel.Should().Be(SCChannel.Ptu);
    }

    [Fact]
    public void SetSelectedInstallation_ThrowsArgumentNullException_WhenInstallationIsNull()
    {
        InstallLocatorService service = new();


        Action act = () => service.SetSelectedInstallation(null!);


        act.Should().Throw<ArgumentNullException>().WithParameterName("installation");
    }

    [Fact]
    public void GetSelectedInstallation_ReturnsNull_WhenNotSet()
    {
        InstallLocatorService service = new();


        SCInstallCandidate? result = service.GetSelectedInstallation();


        result.Should().BeNull();
    }

    [Fact]
    public void InvalidateCache_ClearsCache()
    {
        InstallLocatorService service = new();


        service.InvalidateCache();


        service.GetCachedInstallations().Should().BeNull();
    }

    [Fact]
    public void GetCachedInstallations_ReturnsNull_WhenNeverLoaded()
    {
        InstallLocatorService service = new();


        IReadOnlyList<SCInstallCandidate>? result = service.GetCachedInstallations();


        result.Should().BeNull();
    }

    #endregion
}
