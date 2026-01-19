using BarRaider.SdTools;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Installation;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Integration.Testing;

public sealed class ServiceIntegrationFixture : IDisposable
{
    private readonly string? _customPathsFile;

    public ServiceIntegrationFixture()
    {
        PathProviderService pathProvider = new();
        P4KArchiveService p4KArchiveService = new();
        LocalizationService localizationService = new(p4KArchiveService);
        KeybindingMetadataService metadataService = new();
        KeybindingXmlParserService xmlParser = new();
        CryXmlParserService cryXmlParser = new();
        KeybindingOutputService outputService = new();

        KeybindingProcessorService processorService = new(
            p4KArchiveService,
            cryXmlParser,
            localizationService,
            xmlParser,
            metadataService,
            outputService);

        KeybindingLoaderService loaderService = new();
        KeybindingParserService parserService = new();
        KeybindingExecutorService executorService = new(loaderService, parserService);
        KeybindingService keybindingService = new(loaderService, executorService);

        InstallLocatorService installLocatorService = new();
        VersionProviderService versionProvider = new(pathProvider);
        StateService stateService = new(pathProvider, versionProvider);

        InitializationService = new InitializationService(
            keybindingService,
            installLocatorService,
            processorService,
            pathProvider,
            stateService);
        DataP4KPath = IntegrationTestBase.ResolvePath("Tests/TestData/Data.p4k");
        HasDataP4K = File.Exists(DataP4KPath);
        _customPathsFile = CreateCustomPathsFile();
    }

    public InitializationService InitializationService { get; }

    public string DataP4KPath { get; }

    public bool HasDataP4K { get; }

    public void Dispose()
    {
        InitializationService.Dispose();

        if (!string.IsNullOrWhiteSpace(_customPathsFile) && File.Exists(_customPathsFile))
        {
            File.Delete(_customPathsFile);
        }
    }

    private string? CreateCustomPathsFile()
    {
        string? dataP4KFromEnv = Environment.GetEnvironmentVariable("SCSTREAMDECK_DATA_P4K_PATH");
        string candidatePath = !string.IsNullOrWhiteSpace(dataP4KFromEnv) ? dataP4KFromEnv : DataP4KPath;

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        if (!File.Exists(candidatePath))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[ServiceIntegrationFixture] Data.p4k not found at '{candidatePath}', skipping custom path setup");
            return null;
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string iniPath = Path.Combine(baseDirectory, "custom-paths.ini");
        string contents = "[Paths]" + Environment.NewLine + "Live=" + candidatePath + Environment.NewLine;

        File.WriteAllText(iniPath, contents);

        return iniPath;
    }
}

