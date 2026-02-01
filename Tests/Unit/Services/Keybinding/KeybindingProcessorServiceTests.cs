using System.Reflection;
using System.Text;
using FluentAssertions;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Unit.Services.Keybinding;

public sealed class KeybindingProcessorServiceTests
{
    #region HasBindingsOrValidLabel

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenHasKeyboardBinding()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Category = "@category",
            Bindings = new InputBindings { Keyboard = "SPACE" }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenHasMouseBinding()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings { Mouse = "MOUSE1" }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenHasJoystickBinding()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Category = "@category",
            Bindings = new InputBindings { Joystick = "joystick1" }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenHasGamepadBinding()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Category = "@category",
            Bindings = new InputBindings { Gamepad = "button1" }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenHasValidLabel()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings()
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsFalse_WhenNoBindingsAndNoValidLabel()
    {
        KeybindingActionData action = new() { Name = "test_action", Label = "", Category = "", Bindings = new InputBindings() };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeFalse();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsFalse_WhenWhitespaceBindingsAndNoValidLabel()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "   ",
            Category = "   ",
            Bindings = new InputBindings { Keyboard = "  ", Mouse = "  " }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeFalse();
    }

    [Fact]
    public void HasBindingsOrValidLabel_ReturnsTrue_WhenOnlyBindingIsPresent()
    {
        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "", Category = "", Bindings = new InputBindings { Keyboard = "SPACE" }
        };

        bool result = typeof(KeybindingProcessorService)
            .GetMethod("HasBindingsOrValidLabel", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [action]) as bool? ?? false;

        result.Should().BeTrue();
    }

    #endregion

    #region ApplyLocalizationAsync

    [Fact]
    public async Task ApplyLocalizationAsync_AppliesLocalization_WhenLocalizationExists()
    {
        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService
            .Setup(x => x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["@label"] = "Localized Label", ["@category"] = "Localized Category"
            });

        KeybindingProcessorService service = new(
            Mock.Of<IP4KArchiveService>(),
            Mock.Of<ICryXmlParserService>(),
            mockLocalizationService.Object,
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        List<KeybindingActionData> actions =
        [
            new() { Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings() }
        ];

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalizationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)methodInfo.Invoke(service, [actions, installation, "english", CancellationToken.None])!;

        actions[0].Label.Should().Be("Localized Label");
        actions[0].Category.Should().Be("Localized Category");
    }

    [Fact]
    public async Task ApplyLocalizationAsync_DoesNotModify_WhenLocalizationIsEmpty()
    {
        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService
            .Setup(x => x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        KeybindingProcessorService service = new(
            Mock.Of<IP4KArchiveService>(),
            Mock.Of<ICryXmlParserService>(),
            mockLocalizationService.Object,
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        List<KeybindingActionData> actions =
        [
            new() { Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings() }
        ];

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalizationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)methodInfo.Invoke(service, [actions, installation, "english", CancellationToken.None])!;

        actions[0].Label.Should().Be("@label");
        actions[0].Category.Should().Be("@category");
    }

    [Fact]
    public async Task ApplyLocalizationAsync_DoesNotModify_WhenLocalizationLoadThrows()
    {
        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService
            .Setup(x => x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("boom"));

        KeybindingProcessorService service = new(
            Mock.Of<IP4KArchiveService>(),
            Mock.Of<ICryXmlParserService>(),
            mockLocalizationService.Object,
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        List<KeybindingActionData> actions =
        [
            new() { Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings() }
        ];

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalizationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)methodInfo.Invoke(service, [actions, installation, "english", CancellationToken.None])!;

        actions[0].Label.Should().Be("@label");
        actions[0].Category.Should().Be("@category");
    }

    [Fact]
    public async Task ApplyLocalizationAsync_AppliesToMultipleActions()
    {
        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService
            .Setup(x => x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["@label1"] = "Localized Label 1",
                ["@category1"] = "Localized Category 1",
                ["@label2"] = "Localized Label 2",
                ["@category2"] = "Localized Category 2"
            });

        KeybindingProcessorService service = new(
            Mock.Of<IP4KArchiveService>(),
            Mock.Of<ICryXmlParserService>(),
            mockLocalizationService.Object,
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        List<KeybindingActionData> actions =
        [
            new() { Name = "test_action_1", Label = "@label1", Category = "@category1", Bindings = new InputBindings() },

            new() { Name = "test_action_2", Label = "@label2", Category = "@category2", Bindings = new InputBindings() }
        ];

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalizationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)methodInfo.Invoke(service, [actions, installation, "english", CancellationToken.None])!;

        actions[0].Label.Should().Be("Localized Label 1");
        actions[0].Category.Should().Be("Localized Category 1");
        actions[1].Label.Should().Be("Localized Label 2");
        actions[1].Category.Should().Be("Localized Category 2");
    }

    #endregion

    #region ExtractDefaultProfileAsync

    [Fact]
    public async Task ExtractDefaultProfileAsync_ReturnsBytes_WhenFileExists()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockP4KService.Setup(x => x.CloseArchive());

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        byte[] testBytes = [0x3C, 0x3F, 0x78, 0x6D, 0x6C];
        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testBytes);

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "Data.p4k");

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ExtractDefaultProfileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        byte[]? result = await (Task<byte[]?>)methodInfo.Invoke(service, [tempPath, CancellationToken.None])!;

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(testBytes);

        mockP4KService.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task ExtractDefaultProfileAsync_ReturnsNull_WhenFileNotFoundInArchive()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockP4KService.Setup(x => x.CloseArchive());

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry>());

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "Data.p4k");

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ExtractDefaultProfileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        byte[]? result = await (Task<byte[]?>)methodInfo.Invoke(service, [tempPath, CancellationToken.None])!;

        result.Should().BeNull();

        mockP4KService.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task ExtractDefaultProfileAsync_ReturnsNull_WhenReadReturnsNull()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockP4KService.Setup(x => x.CloseArchive());

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "Data.p4k");

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ExtractDefaultProfileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        byte[]? result = await (Task<byte[]?>)methodInfo.Invoke(service, [tempPath, CancellationToken.None])!;

        result.Should().BeNull();

        mockP4KService.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task ExtractDefaultProfileAsync_ReturnsNull_WhenReadReturnsEmpty()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockP4KService.Setup(x => x.CloseArchive());

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "Data.p4k");

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ExtractDefaultProfileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        byte[]? result = await (Task<byte[]?>)methodInfo.Invoke(service, [tempPath, CancellationToken.None])!;

        result.Should().BeNull();

        mockP4KService.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task ExtractDefaultProfileAsync_DoesNotCloseArchive_WhenOpenFails()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "Data.p4k");

        MethodInfo methodInfo = typeof(KeybindingProcessorService)
            .GetMethod("ExtractDefaultProfileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        byte[]? result = await (Task<byte[]?>)methodInfo.Invoke(service, [tempPath, CancellationToken.None])!;

        result.Should().BeNull();
        mockP4KService.Verify(x => x.CloseArchive(), Times.Never);
    }

    #endregion

    #region ApplyLocalization

    [Fact]
    public void ApplyLocalization_UpdatesLabel_WhenLocalizationExists()
    {
        Dictionary<string, string> localization = new() { ["@label"] = "Localized Label" };

        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Label.Should().Be("Localized Label");
    }

    [Fact]
    public void ApplyLocalization_KeepsLabel_WhenLocalizationDoesNotExist()
    {
        Dictionary<string, string> localization = new() { ["@other_label"] = "Localized Label" };

        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Label.Should().Be("@label");
    }

    [Fact]
    public void ApplyLocalization_UpdatesDescription_WhenLocalizationExists()
    {
        Dictionary<string, string> localization = new() { ["@description"] = "Localized Description" };

        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Category = "@category",
            Description = "@description",
            Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Description.Should().Be("Localized Description");
    }

    [Fact]
    public void ApplyLocalization_DoesNotUpdateDescription_WhenDescriptionIsEmpty()
    {
        Dictionary<string, string> localization = new() { ["@description"] = "Localized Description" };

        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Category = "@category",
            Description = "",
            Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Description.Should().Be("");
    }

    [Fact]
    public void ApplyLocalization_UpdatesMapLabel_WhenLocalizationExists()
    {
        Dictionary<string, string> localization = new() { ["@map"] = "Localized Map" };

        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            MapLabel = "@map",
            Category = "@category",
            Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.MapLabel.Should().Be("Localized Map");
    }

    [Fact]
    public void ApplyLocalization_UpdatesCategory_WhenLocalizationExists()
    {
        Dictionary<string, string> localization = new() { ["@category"] = "Localized Category" };

        KeybindingActionData action = new()
        {
            Name = "test_action", Label = "@label", Category = "@category", Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Category.Should().Be("Localized Category");
    }

    [Fact]
    public void ApplyLocalization_UpdatesMultipleFields()
    {
        Dictionary<string, string> localization = new()
        {
            ["@label"] = "Localized Label",
            ["@description"] = "Localized Description",
            ["@map"] = "Localized Map",
            ["@category"] = "Localized Category"
        };

        KeybindingActionData action = new()
        {
            Name = "test_action",
            Label = "@label",
            Description = "@description",
            MapLabel = "@map",
            Category = "@category",
            Bindings = new InputBindings()
        };

        typeof(KeybindingProcessorService)
            .GetMethod("ApplyLocalization", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [localization, action]);

        action.Label.Should().Be("Localized Label");
        action.Description.Should().Be("Localized Description");
        action.MapLabel.Should().Be("Localized Map");
        action.Category.Should().Be("Localized Category");
    }

    #endregion

    #region ProcessKeybindingsAsync

    [Fact]
    public async Task ProcessKeybindingsAsync_ReturnsSuccess_WhenAllStepsSucceed()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        byte[] testXmlBytes = Encoding.UTF8.GetBytes("<root><test/></root>");
        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testXmlBytes);

        Mock<ICryXmlParserService> mockCryXmlParser = new();
        mockCryXmlParser.Setup(x => x.ConvertCryXmlToTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CryXmlConversionResult.Success("<root><test/></root>"));

        Mock<IKeybindingXmlParserService> mockXmlParser = new();
        mockXmlParser.Setup(x => x.ParseActivationModes(It.IsAny<string>()))
            .Returns(new Dictionary<string, ActivationModeMetadata>
            {
                ["press"] = new() { OnPress = true, OnHold = false, OnRelease = false }
            });

        mockXmlParser.Setup(x => x.ParseXmlToActions(It.IsAny<string>()))
            .Returns([
                new KeybindingActionData
                {
                    Name = "test_action",
                    Label = "@label",
                    Category = "@category",
                    Bindings = new InputBindings { Keyboard = "SPACE" }
                }
            ]);

        Mock<IKeybindingMetadataService> mockMetadataService = new();
        mockMetadataService.Setup(x => x.DetectLanguage(It.IsAny<string>()))
            .Returns("EN");

        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService.Setup(x =>
                x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["@label"] = "Localized Label", ["@category"] = "Localized Category"
            });

        Mock<IKeybindingOutputService> mockOutputService = new();
        mockOutputService.Setup(x => x.WriteKeybindingsJsonAsync(
                It.IsAny<SCInstallCandidate>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<KeybindingActionData>>(),
                It.IsAny<Dictionary<string, ActivationModeMetadata>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            mockCryXmlParser.Object,
            mockLocalizationService.Object,
            mockXmlParser.Object,
            mockMetadataService.Object,
            mockOutputService.Object,
            new SystemFileSystem()
        );

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        string outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "keybindings.json");

        KeybindingProcessResult result = await service.ProcessKeybindingsAsync(
            installation,
            null,
            outputPath,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.DetectedLanguage.Should().Be("EN");

        mockOutputService.Verify(x => x.WriteKeybindingsJsonAsync(
            installation,
            null,
            "EN",
            outputPath,
            It.IsAny<List<KeybindingActionData>>(),
            It.IsAny<Dictionary<string, ActivationModeMetadata>>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ProcessKeybindingsAsync_ReturnsFailure_WhenExtractFails()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry>());

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            Mock.Of<ICryXmlParserService>(),
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        string outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "keybindings.json");

        KeybindingProcessResult result = await service.ProcessKeybindingsAsync(
            installation,
            null,
            outputPath,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to extract defaultProfile.xml from P4K");
    }

    [Fact]
    public async Task ProcessKeybindingsAsync_ReturnsFailure_WhenParseCryXmlFails()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        byte[] testBytes = [0x01, 0x02, 0x03];
        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testBytes);

        Mock<ICryXmlParserService> mockCryXmlParser = new();
        mockCryXmlParser.Setup(x => x.ConvertCryXmlToTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CryXmlConversionResult.Failure("mock parse failure"));

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            mockCryXmlParser.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<IKeybindingXmlParserService>(),
            Mock.Of<IKeybindingMetadataService>(),
            Mock.Of<IKeybindingOutputService>(),
            new SystemFileSystem()
        );

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        string outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "keybindings.json");

        KeybindingProcessResult result = await service.ProcessKeybindingsAsync(
            installation,
            null,
            outputPath,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to parse CryXml binary data");
        result.ErrorMessage.Should().Contain("mock parse failure");
    }

    [Fact]
    public async Task ProcessKeybindingsAsync_ReturnsFailure_WhenNoActionsFound()
    {
        Mock<IP4KArchiveService> mockP4KService = new();
        mockP4KService.Setup(x => x.OpenArchiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        P4KFileEntry mockEntry = new()
        {
            Path = "Data/Libs/Config/defaultProfile.xml",
            Offset = 0,
            CompressedSize = 100,
            UncompressedSize = 200,
            IsCompressed = true
        };

        mockP4KService.Setup(x => x.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { mockEntry });

        byte[] testXmlBytes = Encoding.UTF8.GetBytes("<root></root>");
        mockP4KService.Setup(x => x.ReadFileAsync(It.IsAny<P4KFileEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testXmlBytes);

        Mock<ICryXmlParserService> mockCryXmlParser = new();
        mockCryXmlParser.Setup(x => x.ConvertCryXmlToTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CryXmlConversionResult.Success("<root></root>"));

        Mock<IKeybindingXmlParserService> mockXmlParser = new();
        mockXmlParser.Setup(x => x.ParseActivationModes(It.IsAny<string>()))
            .Returns(new Dictionary<string, ActivationModeMetadata>());

        mockXmlParser.Setup(x => x.ParseXmlToActions(It.IsAny<string>()))
            .Returns([]);

        Mock<IKeybindingMetadataService> mockMetadataService = new();
        mockMetadataService.Setup(x => x.DetectLanguage(It.IsAny<string>()))
            .Returns("EN");

        Mock<ILocalizationService> mockLocalizationService = new();
        mockLocalizationService.Setup(x =>
                x.LoadGlobalIniAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        Mock<IKeybindingOutputService> mockOutputService = new();

        KeybindingProcessorService service = new(
            mockP4KService.Object,
            mockCryXmlParser.Object,
            mockLocalizationService.Object,
            mockXmlParser.Object,
            mockMetadataService.Object,
            mockOutputService.Object,
            new SystemFileSystem()
        );

        SCInstallCandidate installation = new(
            "C:\\SC",
            SCChannel.Live,
            @"C:\SC\LIVE",
            @"C:\SC\LIVE\Data.p4k"
        );

        string outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}", "keybindings.json");

        KeybindingProcessResult result = await service.ProcessKeybindingsAsync(
            installation,
            null,
            outputPath,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("No actions found in defaultProfile.xml");
    }

    #endregion

    #region ApplyOverridesIfPresent

    [Fact]
    public void ApplyOverridesIfPresent_DoesNothing_WhenParsedOverridesAreEmpty()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"scstreamdeck_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string actionMapsPath = Path.Combine(tempDir, SCConstants.Files.ActionMapsFileName);
        File.WriteAllText(actionMapsPath,
            "<?xml version=\"1.0\"?><root><action name=\"test_action\"><rebind input=\"xx_FOO\"/></action></root>");

        try
        {
            KeybindingProcessorService service = new(
                Mock.Of<IP4KArchiveService>(),
                Mock.Of<ICryXmlParserService>(),
                Mock.Of<ILocalizationService>(),
                Mock.Of<IKeybindingXmlParserService>(),
                Mock.Of<IKeybindingMetadataService>(),
                Mock.Of<IKeybindingOutputService>(),
                new SystemFileSystem());

            List<KeybindingActionData> actions =
            [
                new()
                {
                    Name = "test_action",
                    Label = "@label",
                    Category = "@category",
                    Bindings = new InputBindings { Keyboard = "W", Mouse = "MOUSE2" }
                }
            ];

            MethodInfo methodInfo = typeof(KeybindingProcessorService)
                .GetMethod("ApplyOverridesIfPresent", BindingFlags.NonPublic | BindingFlags.Instance)!;

            methodInfo.Invoke(service, [actions, actionMapsPath]);

            actions[0].Bindings.Keyboard.Should().Be("W");
            actions[0].Bindings.Mouse.Should().Be("MOUSE2");
        }
        finally
        {
            if (File.Exists(actionMapsPath))
            {
                File.Delete(actionMapsPath);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ApplyOverridesIfPresent_AppliesOverrides_WhenOverridesPresent()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"scstreamdeck_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string actionMapsPath = Path.Combine(tempDir, SCConstants.Files.ActionMapsFileName);
        File.WriteAllText(actionMapsPath,
            "<?xml version=\"1.0\"?><root><action name=\"test_action\"><rebind input=\"kb_A\"/><rebind input=\"mo_MOUSE1\"/></action></root>");

        try
        {
            KeybindingProcessorService service = new(
                Mock.Of<IP4KArchiveService>(),
                Mock.Of<ICryXmlParserService>(),
                Mock.Of<ILocalizationService>(),
                Mock.Of<IKeybindingXmlParserService>(),
                Mock.Of<IKeybindingMetadataService>(),
                Mock.Of<IKeybindingOutputService>(),
                new SystemFileSystem());

            List<KeybindingActionData> actions =
            [
                new()
                {
                    Name = "test_action",
                    Label = "@label",
                    Category = "@category",
                    Bindings = new InputBindings { Keyboard = "W", Mouse = "MOUSE2" }
                }
            ];

            MethodInfo methodInfo = typeof(KeybindingProcessorService)
                .GetMethod("ApplyOverridesIfPresent", BindingFlags.NonPublic | BindingFlags.Instance)!;

            methodInfo.Invoke(service, [actions, actionMapsPath]);

            actions[0].Bindings.Keyboard.Should().Be("A");
            actions[0].Bindings.Mouse.Should().Be("MOUSE1");
        }
        finally
        {
            if (File.Exists(actionMapsPath))
            {
                File.Delete(actionMapsPath);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ApplyOverridesIfPresent_DoesNotThrow_WhenApplyOverridesThrows()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"scstreamdeck_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string actionMapsPath = Path.Combine(tempDir, SCConstants.Files.ActionMapsFileName);
        File.WriteAllText(actionMapsPath,
            "<?xml version=\"1.0\"?><root><action name=\"test_action\"><rebind input=\"kb_A\"/></action></root>");

        try
        {
            KeybindingProcessorService service = new(
                Mock.Of<IP4KArchiveService>(),
                Mock.Of<ICryXmlParserService>(),
                Mock.Of<ILocalizationService>(),
                Mock.Of<IKeybindingXmlParserService>(),
                Mock.Of<IKeybindingMetadataService>(),
                Mock.Of<IKeybindingOutputService>(),
                new SystemFileSystem());

            MethodInfo methodInfo = typeof(KeybindingProcessorService)
                .GetMethod("ApplyOverridesIfPresent", BindingFlags.NonPublic | BindingFlags.Instance)!;

            Action act = () => methodInfo.Invoke(service, new object?[] { null, actionMapsPath });
            act.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(actionMapsPath))
            {
                File.Delete(actionMapsPath);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    #endregion
}
