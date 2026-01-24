using FluentAssertions;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Installation;

namespace Tests.Unit.Services.Core;

public sealed class StateServiceTests
{
    [Fact]
    public async Task LoadStateAsync_NoStateFile_ReturnsNull()
    {
        using TestStateFile testState = new();
        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        PluginState? result = await stateService.LoadStateAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadStateAsync_ValidStateFile_ReturnsPluginState()
    {
        using TestStateFile testState = new();
        PluginState expectedState = CreateValidState();
        testState.WriteState(expectedState);

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        PluginState? result = await stateService.LoadStateAsync();

        result.Should().NotBeNull();
        result.SelectedChannel.Should().Be(expectedState.SelectedChannel);
        result.LiveInstallation.Should().NotBeNull();
        result.LiveInstallation!.RootPath.Should().Be(expectedState.LiveInstallation!.RootPath);
    }

    [Fact]
    public async Task LoadStateAsync_InvalidJson_ReturnsNull()
    {
        using TestStateFile testState = new();
        testState.WriteInvalidJson();

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        PluginState? result = await stateService.LoadStateAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadStateAsync_CancellationToken_CancelsOperation()
    {
        using TestStateFile testState = new();
        PluginState validState = CreateValidState();
        testState.WriteState(validState);

        CancellationTokenSource cts = new();
        await cts.CancelAsync();


        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        await Assert.ThrowsAsync<TaskCanceledException>(() => stateService.LoadStateAsync(cts.Token));
        cts.Dispose();
    }

    [Fact]
    public async Task SaveAndLoad_Cycle_PreservesState()
    {
        using TestStateFile testState = new();
        PluginState originalState = CreateValidState();

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        await stateService.SaveStateAsync(originalState);
        PluginState? loadedState = await stateService.LoadStateAsync();

        loadedState.Should().NotBeNull();
        loadedState.SelectedChannel.Should().Be(originalState.SelectedChannel);
        loadedState.LiveInstallation!.RootPath.Should().Be(originalState.LiveInstallation!.RootPath);
    }

    [Fact]
    public async Task LoadStateAsync_MissingOptionalFields_ReturnsValidState()
    {
        using TestStateFile testState = new();
        PluginState minimalState = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            null,
            null,
            null,
            null
        );
        testState.WriteState(minimalState);

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        PluginState? result = await stateService.LoadStateAsync();

        result.Should().NotBeNull();
        result.LiveInstallation.Should().BeNull();
        result.HotfixInstallation.Should().BeNull();
        result.PtuInstallation.Should().BeNull();
        result.EptuInstallation.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedCandidatesAsync_ReturnsNull_WhenStateNotValid()
    {
        using TestStateFile testState = new();

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        IReadOnlyList<SCInstallCandidate>? result = await stateService.GetCachedCandidatesAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedCandidatesAsync_ReturnsCandidates_WhenStateValid()
    {
        using TestStateFile testState = new();
        string livePath = Path.Combine(testState.CacheDirectory, "LIVE");
        Directory.CreateDirectory(livePath);
        await File.WriteAllTextAsync(Path.Combine(livePath, "Data.p4k"), "test");

        InstallationState liveInstall = new(
            testState.CacheDirectory,
            SCChannel.Live,
            livePath
        );

        PluginState validState = new(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstall,
            null,
            null,
            null
        );
        testState.WriteState(validState);

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        IReadOnlyList<SCInstallCandidate>? result = await stateService.GetCachedCandidatesAsync();

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(1);
    }


    [Fact]
    public async Task DeleteStateFile_DeletesStateFile_WhenPresent()
    {
        using TestStateFile testState = new();
        PluginState state = CreateValidState();
        testState.WriteState(state);

        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        stateService.DeleteStateFile();

        string stateFile = Path.Combine(testState.CacheDirectory, ".plugin-state.json");
        File.Exists(stateFile).Should().BeFalse();

        PluginState? loaded = await stateService.LoadStateAsync();
        loaded.Should().BeNull();
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenPathProviderIsNull()
    {
        using TestStateFile testState = new();
        TestPathProvider pathProvider = new(testState.CacheDirectory);

        Action act = () => new StateService(null!, new SystemFileSystem());

        act.Should().Throw<ArgumentNullException>().WithParameterName("pathProvider");
    }

    [Fact]
    public async Task SaveStateAsync_ThrowsArgumentNullException_WhenStateIsNull()
    {
        using TestStateFile testState = new();
        TestPathProvider pathProvider = new(testState.CacheDirectory);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        Func<Task> act = async () => await stateService.SaveStateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("state");
    }

    private static PluginState CreateValidState()
    {
        InstallationState liveInstall = new(
            "C:/Games/StarCitizen",
            SCChannel.Live,
            "C:/Games/StarCitizen/LIVE"
        );

        return new PluginState(
            DateTime.UtcNow,
            SCChannel.Live,
            null,
            liveInstall,
            null,
            null,
            null
        );
    }

    private sealed class TestPathProvider(string cacheDir) : PathProviderService(cacheDir, cacheDir)
    {
        public override string GetSecureCachePath(string relativePath) =>
            SecurePathValidator.GetSecurePath(relativePath, CacheDirectory);
    }

    private sealed class TestStateFile : IDisposable
    {
        public TestStateFile()
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            StateFilePath = Path.Combine(CacheDirectory, ".plugin-state.json");
            Directory.CreateDirectory(CacheDirectory);
        }

        public string CacheDirectory { get; }

        private string StateFilePath { get; }

        public void Dispose()
        {
            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, true);
            }
        }

        public void WriteState(PluginState state) =>
            File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(state, Formatting.Indented));

        public void WriteInvalidJson() => File.WriteAllText(StateFilePath, "{ invalid json }");
    }
}
