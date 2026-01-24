using System.Text;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;
using WindowsInput;

namespace Tests.Unit.Services.Core;

public sealed class InitializationServiceComponentTests : IDisposable
{
    private readonly string _root;

    public InitializationServiceComponentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"SCStreamDeck_InitSvc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenHasCandidate_GeneratesAndLoadsKeybindings()
    {
        using SutContext ctx = CreateContext([CreateLiveCandidate(_root)]);

        InitializationResult result = await ctx.InitService.EnsureInitializedAsync();

        result.IsSuccess.Should().BeTrue();
        result.SelectedChannel.Should().Be(SCChannel.Live);
        result.DetectedInstallations.Should().Be(1);
        ctx.KeybindingService.IsLoaded.Should().BeTrue();

        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved.LiveInstallation.Should().NotBeNull();
        saved.SelectedChannel.Should().Be(SCChannel.Live);
        ctx.InstallLocator.SelectedInstallation.Should().NotBeNull();
        ctx.InstallLocator.SelectedInstallation!.Channel.Should().Be(SCChannel.Live);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenCachedInstallationMissing_CleansUpStateAndKeybindingsJson_EvenIfDetectionFails()
    {
        using SutContext ctx = CreateContext(Array.Empty<SCInstallCandidate>());

        // Seed invalid cache (missing folder + Data.p4k)
        string missingRoot = Path.Combine(_root, "MissingRoot");
        string missingChannel = Path.Combine(missingRoot, "LIVE");

        await ctx.StateService.UpdateInstallationAsync(
            SCChannel.Live,
            new InstallationState(missingRoot, SCChannel.Live, missingChannel));

        string existingKeybindings = ctx.PathProvider.GetKeybindingJsonPath(SCChannel.Live.ToString());
        File.WriteAllText(existingKeybindings, "{}", Encoding.UTF8);
        File.Exists(existingKeybindings).Should().BeTrue();

        InitializationResult result = await ctx.InitService.EnsureInitializedAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        File.Exists(existingKeybindings).Should().BeFalse("invalid cached installation should be cleaned up");

        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved.LiveInstallation.Should().BeNull();
    }

    [Fact]
    public async Task FactoryResetAsync_DeletesPluginStateAndKeybindings_AndResetsThemeSelection()
    {
        SCInstallCandidate live = CreateLiveCandidate(_root);
        Directory.CreateDirectory(live.ChannelPath);
        File.WriteAllText(live.DataP4KPath, "test");

        using SutContext ctx = CreateContext([live]);

        // Seed persisted state with a custom override.
        await ctx.StateService.UpdateInstallationAsync(
            SCChannel.Live,
            new InstallationState(live.RootPath, SCChannel.Live, live.ChannelPath, true));

        await ctx.StateService.UpdateSelectedThemeAsync("Desert.css");

        string stateFile = Path.Combine(ctx.PathProvider.CacheDirectory, ".plugin-state.json");
        File.Exists(stateFile).Should().BeTrue();

        // Seed generated keybindings.
        string liveKeybindings = ctx.PathProvider.GetKeybindingJsonPath(SCChannel.Live.ToString());
        File.WriteAllText(liveKeybindings, "{}");

        InitializationResult result = await ctx.InitService.FactoryResetAsync();
        result.IsSuccess.Should().BeTrue();

        // State/keybindings should be regenerated after reset, but the custom override must be gone.
        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved!.LiveInstallation.Should().NotBeNull();
        saved.LiveInstallation!.IsCustomPath.Should().BeFalse("factory reset should remove custom overrides");
        saved.SelectedTheme.Should().BeNull("factory reset should reset theme selection");

        File.Exists(stateFile).Should().BeTrue("state should be recreated after re-initialization");
        File.Exists(liveKeybindings).Should().BeTrue("keybindings should be regenerated after reset");
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenSelectedChannelKeybindingGenerationFails_ReturnsFailure()
    {
        SCInstallCandidate live = CreateLiveCandidate(_root);

        using SutContext ctx = CreateContext(
            [live],
            mock =>
            {
                mock.Setup(s => s.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("boom"));
            });

        await ctx.StateService.UpdateSelectedChannelAsync(SCChannel.Live);

        InitializationResult result = await ctx.InitService.EnsureInitializedAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ApplyCustomDataP4KOverrideAsync_WhenApplyingOverride_PersistsCustomInstallAndReloadsKeybindings()
    {
        using SutContext ctx = CreateContext(Array.Empty<SCInstallCandidate>());

        string rootPath = Path.Combine(_root, "Custom");
        string channelPath = Path.Combine(rootPath, "LIVE");
        Directory.CreateDirectory(channelPath);

        string dataP4KPath = Path.Combine(channelPath, "Data.p4k");
        File.WriteAllText(dataP4KPath, "test");

        bool ok = await ctx.InitService.ApplyCustomDataP4KOverrideAsync(SCChannel.Live, dataP4KPath);

        ok.Should().BeTrue();
        ctx.InitService.IsInitialized.Should().BeTrue();
        ctx.InitService.CurrentChannel.Should().Be(SCChannel.Live);
        ctx.KeybindingService.IsLoaded.Should().BeTrue();

        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved.LiveInstallation.Should().NotBeNull();
        saved.LiveInstallation!.IsCustomPath.Should().BeTrue();
        saved.SelectedChannel.Should().Be(SCChannel.Live);

        ctx.InstallLocator.SelectedInstallation.Should().NotBeNull();
        ctx.InstallLocator.SelectedInstallation!.Source.Should().Be(InstallSource.UserProvided);
    }

    [Fact]
    public async Task ApplyCustomDataP4KOverrideAsync_WhenClearingOverride_RedetectsChannelAndRemovesCustomFlag()
    {
        SCInstallCandidate live = CreateLiveCandidate(_root);
        Directory.CreateDirectory(live.ChannelPath);
        File.WriteAllText(live.DataP4KPath, "test");

        using SutContext ctx = CreateContext([live]);

        // Seed custom override state
        await ctx.StateService.UpdateInstallationAsync(
            SCChannel.Live,
            new InstallationState(live.RootPath, SCChannel.Live, live.ChannelPath, true));

        bool ok = await ctx.InitService.ApplyCustomDataP4KOverrideAsync(SCChannel.Live, null);

        ok.Should().BeTrue();

        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved.LiveInstallation.Should().NotBeNull();
        saved.LiveInstallation!.IsCustomPath.Should().BeFalse("redetection should replace the custom override");
    }

    [Fact]
    public async Task ApplyCustomDataP4KOverrideAsync_WhenClearingActiveChannelAndRedetectFails_FallsBackToAnotherChannel()
    {
        // Auto-detected LIVE exists.
        SCInstallCandidate live = CreateLiveCandidate(_root);
        Directory.CreateDirectory(live.ChannelPath);
        File.WriteAllText(live.DataP4KPath, "test");

        using SutContext ctx = CreateContext([live]);

        // Seed state + keybindings JSON for LIVE so self-heal can switch without a full re-detection.
        await ctx.StateService.UpdateInstallationAsync(
            SCChannel.Live,
            InstallationState.FromCandidate(live));

        string liveJson = ctx.PathProvider.GetKeybindingJsonPath(SCChannel.Live.ToString());
        KeybindingDataFile dataFile = new()
        {
            Metadata = new KeybindingMetadata { ExtractedAt = DateTime.UtcNow },
            Actions =
            [
                new KeybindingActionData
                {
                    Name = "TestAction",
                    Label = "Test",
                    Description = "",
                    Category = "Test",
                    MapName = "Test",
                    MapLabel = "Test",
                    ActivationMode = ActivationMode.press,
                    Bindings = new InputBindings { Keyboard = "A" }
                }
            ]
        };
        File.WriteAllText(liveJson, JsonConvert.SerializeObject(dataFile, Formatting.Indented), Encoding.UTF8);

        // Create a custom HOTFIX override (no real HOTFIX install exists).
        string hotfixRoot = Path.Combine(_root, "CustomHotfix");
        string hotfixChannelPath = Path.Combine(hotfixRoot, "HOTFIX");
        Directory.CreateDirectory(hotfixChannelPath);
        string hotfixDataP4KPath = Path.Combine(hotfixChannelPath, "Data.p4k");
        File.WriteAllText(hotfixDataP4KPath, "test");

        bool applied = await ctx.InitService.ApplyCustomDataP4KOverrideAsync(SCChannel.Hotfix, hotfixDataP4KPath);
        applied.Should().BeTrue();
        ctx.InitService.CurrentChannel.Should().Be(SCChannel.Hotfix);
        ctx.InitService.KeybindingsJsonExists().Should().BeTrue();

        // Clearing the active channel should not leave the plugin stuck on a missing keybindings JSON.
        bool cleared = await ctx.InitService.ApplyCustomDataP4KOverrideAsync(SCChannel.Hotfix, null);
        cleared.Should().BeTrue();
        ctx.InitService.CurrentChannel.Should().Be(SCChannel.Live);
        ctx.InitService.KeybindingsJsonExists().Should().BeTrue();
        ctx.KeybindingService.IsLoaded.Should().BeTrue();

        PluginState? saved = await ctx.StateService.LoadStateAsync();
        saved.Should().NotBeNull();
        saved!.SelectedChannel.Should().Be(SCChannel.Live);
    }

    private SutContext CreateContext(
        IReadOnlyList<SCInstallCandidate> locatorCandidates,
        Action<Mock<IP4KArchiveService>>? configureP4K = null)
    {
        string baseDir = Path.Combine(_root, "base");
        string cacheDir = Path.Combine(_root, "cache");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(cacheDir);

        PathProviderService pathProvider = new TestPathProviderService(baseDir, cacheDir);
        StateService stateService = new(pathProvider, new SystemFileSystem());

        KeybindingLoaderService loaderService = new(new SystemFileSystem());

        Mock<IInputSimulator> inputSimulator = new(MockBehavior.Strict);
        KeybindingExecutorService executorService = new(loaderService, inputSimulator.Object);
        KeybindingService keybindingService = new(loaderService, executorService);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.SetupGet(s => s.IsArchiveOpen).Returns(true);
        p4K.Setup(s => s.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        p4K.Setup(s => s.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new P4KFileEntry
                {
                    Path = "defaultProfile.xml",
                    Offset = 0,
                    CompressedSize = 0,
                    UncompressedSize = 0,
                    IsCompressed = false
                }
            ]);
        p4K.Setup(s => s.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("<xml />"));
        p4K.Setup(s => s.CloseArchive());

        configureP4K?.Invoke(p4K);

        Mock<ICryXmlParserService> cryXml = new(MockBehavior.Strict);
        cryXml.Setup(s => s.ConvertCryXmlToTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CryXmlConversionResult.Success("<xml />"));

        Mock<ILocalizationService> localization = new(MockBehavior.Strict);
        localization.Setup(s =>
                s.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        localization.Setup(s => s.ReadLanguageSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("english");
        localization.Setup(s => s.ClearCache(It.IsAny<string>(), It.IsAny<string>()));

        Mock<IKeybindingXmlParserService> xmlParser = new(MockBehavior.Strict);
        xmlParser.Setup(s => s.ParseActivationModes(It.IsAny<string>()))
            .Returns(new Dictionary<string, ActivationModeMetadata>(StringComparer.OrdinalIgnoreCase));
        xmlParser.Setup(s => s.ParseXmlToActions(It.IsAny<string>()))
            .Returns(
            [
                new KeybindingActionData
                {
                    Name = "TestAction",
                    Label = "Test",
                    Description = "",
                    Category = "TestCategory",
                    MapName = "TestMap",
                    MapLabel = "TestMap",
                    ActivationMode = ActivationMode.press,
                    Bindings = new InputBindings { Keyboard = "A" }
                }
            ]);

        Mock<IKeybindingMetadataService> metadataService = new(MockBehavior.Strict);
        metadataService.Setup(s => s.DetectLanguage(It.IsAny<string>())).Returns("english");
        metadataService.Setup(s => s.NeedsRegeneration(It.IsAny<string>(), It.IsAny<SCInstallCandidate>())).Returns(false);

        IKeybindingOutputService outputService = new TestKeybindingOutputService();

        // Not used by these tests, but required by ctor.
        Mock<ILocalizationService> localizationService = localization;

        KeybindingProcessorService processor = new(
            p4K.Object,
            cryXml.Object,
            localizationService.Object,
            xmlParser.Object,
            metadataService.Object,
            outputService,
            new SystemFileSystem());

        FakeInstallLocatorService locator = new(locatorCandidates);
        IFileSystem fileSystem = new SystemFileSystem();
        IKeybindingsJsonCache keybindingsJsonCache = new KeybindingsJsonCache(pathProvider, fileSystem);
        ICachedInstallationsCleanupService cachedCleanup =
            new CachedInstallationsCleanupService(stateService, keybindingsJsonCache, fileSystem);
        InitializationService initService =
            new(keybindingService, locator, processor, pathProvider, stateService, keybindingsJsonCache, cachedCleanup);

        return new SutContext
        {
            PathProvider = pathProvider,
            StateService = stateService,
            KeybindingService = keybindingService,
            KeybindingProcessor = processor,
            InstallLocator = locator,
            InitService = initService
        };
    }

    private static SCInstallCandidate CreateLiveCandidate(string ctxRoot)
    {
        string rootPath = Path.Combine(ctxRoot, "SC");
        string channelPath = Path.Combine(rootPath, "LIVE");
        string dataP4K = Path.Combine(channelPath, "Data.p4k");
        return new SCInstallCandidate(rootPath, SCChannel.Live, channelPath, dataP4K);
    }

    private sealed class SutContext : IDisposable
    {
        public required PathProviderService PathProvider { get; init; }
        public required StateService StateService { get; init; }
        public required KeybindingService KeybindingService { get; init; }
        public required KeybindingProcessorService KeybindingProcessor { get; init; }
        public required FakeInstallLocatorService InstallLocator { get; init; }
        public required InitializationService InitService { get; init; }

        public void Dispose() => InitService.Dispose();
    }

    private sealed class FakeInstallLocatorService : IInstallLocatorService
    {
        private readonly IReadOnlyList<SCInstallCandidate> _candidates;

        public FakeInstallLocatorService(IReadOnlyList<SCInstallCandidate> candidates) => _candidates = candidates;

        public int InvalidateCacheCalls { get; private set; }
        public SCInstallCandidate? SelectedInstallation { get; private set; }

        public Task<IReadOnlyList<SCInstallCandidate>> FindInstallationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_candidates);

        public void InvalidateCache() => InvalidateCacheCalls++;
        public IReadOnlyList<SCInstallCandidate>? GetCachedInstallations() => _candidates;
        public SCInstallCandidate? GetSelectedInstallation() => SelectedInstallation;
        public void SetSelectedInstallation(SCInstallCandidate installation) => SelectedInstallation = installation;
    }

    private sealed class TestPathProviderService(string baseDirectory, string cacheDirectory)
        : PathProviderService(baseDirectory, cacheDirectory);

    private sealed class TestKeybindingOutputService : IKeybindingOutputService
    {
        public Task WriteKeybindingsJsonAsync(
            SCInstallCandidate installation,
            string? actionMapsPath,
            string language,
            string outputJsonPath,
            List<KeybindingActionData> actions,
            Dictionary<string, ActivationModeMetadata> activationModes,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputJsonPath)!);

            KeybindingDataFile dataFile = new()
            {
                Metadata = new KeybindingMetadata
                {
                    ExtractedAt = DateTime.UtcNow,
                    Language = language,
                    DataP4KPath = installation.DataP4KPath,
                    DataP4KSize = 0,
                    DataP4KLastWrite = DateTime.UtcNow,
                    ActionMapsPath = actionMapsPath,
                    ActionMapsSize = null,
                    ActionMapsLastWrite = null,
                    ActivationModes = activationModes.Count > 0 ? activationModes : null
                },
                Actions = actions
            };

            File.WriteAllText(outputJsonPath, JsonConvert.SerializeObject(dataFile, Formatting.Indented), Encoding.UTF8);
            return Task.CompletedTask;
        }
    }
}
