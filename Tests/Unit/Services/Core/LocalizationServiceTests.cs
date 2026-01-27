using FluentAssertions;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Core;
using SCStreamDeck.Services.Data;

namespace Tests.Unit.Services.Core;

public sealed class LocalizationServiceTests
{
    #region Existing Tests

    [Fact]
    public async Task LoadGlobalIniAsync_FallsBackToDefault_WhenNotFound()
    {
        Mock<IP4KArchiveService> p4K = new();
        LocalizationService service = new(p4K.Object, new SystemFileSystem());

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            "XX",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLanguageSettingAsync_ReturnsDefault_WhenConfigMissing()
    {
        Mock<IP4KArchiveService> p4K = new();
        LocalizationService service = new(p4K.Object, new SystemFileSystem());

        string language = await service.ReadLanguageSettingAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    #endregion

    #region LoadGlobalIniAsync I/O Tests

    [Fact]
    public async Task LoadGlobalIniAsync_UsesCache_OnSecondCall()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllTextAsync(validOverridePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("key=value");

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result1 = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        IReadOnlyDictionary<string, string> result2 = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result1.Should().ContainKey("key");
        result1["key"].Should().Be("value");
        ReferenceEquals(result1, result2).Should().BeTrue();

        fileSystem.Verify(x => x.FileExists(validOverridePath), Times.Once);
        fileSystem.Verify(x => x.ReadAllTextAsync(validOverridePath, It.IsAny<CancellationToken>()), Times.Once);
        p4K.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoadGlobalIniAsync_CanBeClearedFromCache()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(true);
        fileSystem.SetupSequence(x => x.ReadAllTextAsync(validOverridePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("key=one")
            .ReturnsAsync("key=two");

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result1 = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        service.ClearCache(channelPath, "english");

        IReadOnlyDictionary<string, string> result2 = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result1["key"].Should().Be("one");
        result2["key"].Should().Be("two");

        fileSystem.Verify(x => x.ReadAllTextAsync(validOverridePath, It.IsAny<CancellationToken>()), Times.Exactly(2));
        p4K.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoadGlobalIniAsync_LoadsFromP4K_WhenOverrideMissing()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        P4KFileEntry entry = new()
        {
            Path = "Data/Localization/ENGLISH/global.ini",
            Offset = 0,
            CompressedSize = 1,
            UncompressedSize = 1,
            IsCompressed = false
        };

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(false);
        fileSystem.Setup(x => x.FileExists(dataP4KPath)).Returns(true);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.Setup(x => x.OpenArchiveAsync(dataP4KPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        p4K.Setup(x => x.ScanDirectoryAsync(
                It.Is<string>(d => d.Equals("Data/Localization/ENGLISH", StringComparison.Ordinal)),
                SCConstants.Files.GlobalIniFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { entry });
        p4K.Setup(x => x.ReadFileAsTextAsync(entry, It.IsAny<CancellationToken>())).ReturnsAsync("key=value");
        p4K.Setup(x => x.CloseArchive());

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result.Should().ContainKey("key");
        result["key"].Should().Be("value");
        p4K.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task LoadGlobalIniAsync_ReturnsEmpty_WhenP4KOpenFails()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(false);
        fileSystem.Setup(x => x.FileExists(dataP4KPath)).Returns(true);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.Setup(x => x.OpenArchiveAsync(dataP4KPath, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result.Should().BeEmpty();
        p4K.Verify(x => x.CloseArchive(), Times.Never);
    }

    [Fact]
    public async Task LoadGlobalIniAsync_ReturnsEmptyAndClosesArchive_WhenNoEntriesInP4K()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(false);
        fileSystem.Setup(x => x.FileExists(dataP4KPath)).Returns(true);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.Setup(x => x.OpenArchiveAsync(dataP4KPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        p4K.Setup(x => x.ScanDirectoryAsync(
                It.Is<string>(d => d.Equals("Data/Localization/ENGLISH", StringComparison.Ordinal)),
                SCConstants.Files.GlobalIniFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry>());
        p4K.Setup(x => x.CloseArchive());

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result.Should().BeEmpty();
        p4K.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task LoadGlobalIniAsync_ReturnsEmptyAndClosesArchive_WhenP4KReadThrows()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePath = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePath = Path.GetFullPath(overridePath);

        P4KFileEntry entry = new()
        {
            Path = "Data/Localization/ENGLISH/global.ini",
            Offset = 0,
            CompressedSize = 1,
            UncompressedSize = 1,
            IsCompressed = false
        };

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePath)).Returns(false);
        fileSystem.Setup(x => x.FileExists(dataP4KPath)).Returns(true);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.Setup(x => x.OpenArchiveAsync(dataP4KPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        p4K.Setup(x => x.ScanDirectoryAsync(
                It.Is<string>(d => d.Equals("Data/Localization/ENGLISH", StringComparison.Ordinal)),
                SCConstants.Files.GlobalIniFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { entry });
        p4K.Setup(x => x.ReadFileAsTextAsync(entry, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("boom"));
        p4K.Setup(x => x.CloseArchive());

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            channelPath,
            "english",
            dataP4KPath,
            CancellationToken.None);

        result.Should().BeEmpty();
        p4K.Verify(x => x.CloseArchive(), Times.Once);
    }

    [Fact]
    public async Task LoadGlobalIniAsync_FallsBackToDefaultLanguage_WhenNonDefaultNotFound()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string dataP4KPath = Path.Combine(channelPath, SCConstants.Files.DataP4KFileName);

        string overridePathGerman = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "GERMAN_(GERMANY)",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePathGerman = Path.GetFullPath(overridePathGerman);

        string overridePathEnglish = Path.Combine(
            channelPath,
            "data",
            SCConstants.Localization.LocalizationSubdirectory,
            "ENGLISH",
            SCConstants.Files.GlobalIniFileName);
        string validOverridePathEnglish = Path.GetFullPath(overridePathEnglish);

        P4KFileEntry entryEnglish = new()
        {
            Path = "Data/Localization/ENGLISH/global.ini",
            Offset = 0,
            CompressedSize = 1,
            UncompressedSize = 1,
            IsCompressed = false
        };

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validOverridePathGerman)).Returns(false);
        fileSystem.Setup(x => x.FileExists(validOverridePathEnglish)).Returns(false);
        fileSystem.Setup(x => x.FileExists(dataP4KPath)).Returns(true);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        p4K.Setup(x => x.OpenArchiveAsync(dataP4KPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        p4K.Setup(x => x.ScanDirectoryAsync(
                It.Is<string>(d => d.Equals("Data/Localization/GERMAN_(GERMANY)", StringComparison.Ordinal)),
                SCConstants.Files.GlobalIniFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry>());
        p4K.Setup(x => x.ScanDirectoryAsync(
                It.Is<string>(d => d.Equals("Data/Localization/ENGLISH", StringComparison.Ordinal)),
                SCConstants.Files.GlobalIniFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<P4KFileEntry> { entryEnglish });
        p4K.Setup(x => x.ReadFileAsTextAsync(entryEnglish, It.IsAny<CancellationToken>())).ReturnsAsync("key=value");
        p4K.Setup(x => x.CloseArchive());

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        IReadOnlyDictionary<string, string> result = await service.LoadGlobalIniAsync(
            channelPath,
            "german_(germany)",
            dataP4KPath,
            CancellationToken.None);

        result.Should().ContainKey("key");
        result["key"].Should().Be("value");
        p4K.Verify(x => x.CloseArchive(), Times.Exactly(2));
    }

    #endregion

    #region ReadLanguageSettingAsync I/O Tests

    [Fact]
    public async Task ReadLanguageSettingAsync_ReturnsDefault_WhenPathIsInvalid()
    {
        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        LocalizationService service = new(p4K.Object, fileSystem.Object);

        string language = await service.ReadLanguageSettingAsync("C:\\temp:bad", CancellationToken.None);

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
        p4K.VerifyNoOtherCalls();
        fileSystem.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadLanguageSettingAsync_ReturnsParsedLanguage_WhenConfigExists()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string userConfigPath = Path.Combine(channelPath, SCConstants.Files.UserConfigFileName);
        string validPath = Path.GetFullPath(userConfigPath);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validPath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllLinesAsync(validPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "g_language=german_(germany)" });

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        string language = await service.ReadLanguageSettingAsync(channelPath, CancellationToken.None);

        language.Should().Be("GERMAN_(GERMANY)");
    }

    [Fact]
    public async Task ReadLanguageSettingAsync_ReturnsDefault_WhenReadAllLinesThrows()
    {
        string channelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string userConfigPath = Path.Combine(channelPath, SCConstants.Files.UserConfigFileName);
        string validPath = Path.GetFullPath(userConfigPath);

        Mock<IP4KArchiveService> p4K = new(MockBehavior.Strict);
        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(validPath)).Returns(true);
        fileSystem.Setup(x => x.ReadAllLinesAsync(validPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("boom"));

        LocalizationService service = new(p4K.Object, fileSystem.Object);

        string language = await service.ReadLanguageSettingAsync(channelPath, CancellationToken.None);

        language.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    #endregion

    #region ParseGlobalIni Tests

    [Fact]
    public void ParseGlobalIni_EmptyContent_ReturnsEmptyDictionary()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseGlobalIni_NullOrWhitespaceContent_ReturnsEmptyDictionary()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("   \n\t  ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseGlobalIni_SingleValidEntry_ParsesCorrectly()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1");

        result.Should().ContainSingle();
        result.Should().ContainKey("key1");
        result["key1"].Should().Be("value1");
    }

    [Fact]
    public void ParseGlobalIni_MultipleValidEntries_ParsesAll()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\nkey2=value2\nkey3=value3");

        result.Should().HaveCount(3);
        result["key1"].Should().Be("value1");
        result["key2"].Should().Be("value2");
        result["key3"].Should().Be("value3");
    }

    [Fact]
    public void ParseGlobalIni_CommentLines_Skipped()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\n-- comment\nkey2=value2");

        result.Should().HaveCount(2);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
    }

    [Fact]
    public void ParseGlobalIni_AllCommentPrefixes_Skipped()
    {
        string content =
            "key1=value1\n-- double dash comment\nkey2=value2\n// slash slash comment\nkey3=value3\n# hash comment\nkey4=value4";
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni(content);

        result.Should().HaveCount(4);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
        result.Should().ContainKey("key3");
        result.Should().ContainKey("key4");
    }

    [Fact]
    public void ParseGlobalIni_EmptyLines_Skipped()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\n\n\nkey2=value2\n   \nkey3=value3");

        result.Should().HaveCount(3);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
        result.Should().ContainKey("key3");
    }

    [Fact]
    public void ParseGlobalIni_LineWithoutEquals_Skipped()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\ninvalid line\nkey2=value2");

        result.Should().HaveCount(2);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
    }

    [Fact]
    public void ParseGlobalIni_EmptyKey_Skipped()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\n=value\nkey2=value2");

        result.Should().HaveCount(2);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
    }

    [Fact]
    public void ParseGlobalIni_UiKeyTransform_AddsAtPrefix()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("ui_action=Action Label");

        result.Should().ContainSingle();
        result.Should().ContainKey("@ui_action");
        result["@ui_action"].Should().Be("Action Label");
    }

    [Fact]
    public void ParseGlobalIni_WhitespaceAroundKeyValue_TrimsCorrectly()
    {
        Dictionary<string, string> result =
            LocalizationService.ParseGlobalIni("  key1  =  value1  \n  key2=value2\nkey3=  value3  ");

        result.Should().HaveCount(3);
        result["key1"].Should().Be("value1");
        result["key2"].Should().Be("value2");
        result["key3"].Should().Be("value3");
    }

    [Fact]
    public void ParseGlobalIni_MixedValidAndInvalid_ParsesValidOnly()
    {
        string content = "key1=value1\n-- comment\n\ninvalid line\n=value\nkey2=value2\nui_action=Action";
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni(content);

        result.Should().HaveCount(3);
        result.Should().ContainKey("key1");
        result.Should().ContainKey("key2");
        result.Should().ContainKey("@ui_action");
    }

    [Fact]
    public void ParseGlobalIni_DuplicateKeys_LastValueWins()
    {
        Dictionary<string, string> result = LocalizationService.ParseGlobalIni("key1=value1\nKEY1=value2\nkey1=value3");

        result.Should().HaveCount(1);
        result.Should().ContainKey("key1");
        result["key1"].Should().Be("value3");
    }

    #endregion

    #region ParseLanguageFromLines Tests

    [Fact]
    public void ParseLanguageFromLines_EmptyLines_ReturnsDefault()
    {
        string result = LocalizationService.ParseLanguageFromLines(["", "  ", "\t"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_CommentLines_Skipped()
    {
        string result = LocalizationService.ParseLanguageFromLines(["-- comment", "// comment", "# comment"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_MissingConfigKey_ReturnsDefault()
    {
        string result = LocalizationService.ParseLanguageFromLines(["other_key=value", "another_key=value2"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_ValidLanguage_ReturnsNormalized()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=german_(germany)"]);

        result.Should().Be("GERMAN_(GERMANY)");
    }

    [Fact]
    public void ParseLanguageFromLines_ValidLanguageCaseInsensitive_ReturnsNormalized()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=English"]);

        result.Should().Be("ENGLISH");
    }

    [Fact]
    public void ParseLanguageFromLines_InvalidLanguage_ReturnsDefault()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=invalid_language"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_EmptyValue_ReturnsDefault()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=", "g_language=   "]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_ValueWithSpaces_TrimsCorrectly()
    {
        string result = LocalizationService.ParseLanguageFromLines(["  g_language  =  french_(france)  "]);

        result.Should().Be("FRENCH_(FRANCE)");
    }

    [Fact]
    public void ParseLanguageFromLines_MultipleLines_FirstMatchWins()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=english", "g_language=german_(germany)"]);

        result.Should().Be("ENGLISH");
    }

    [Fact]
    public void ParseLanguageFromLines_AllSupportedLanguages_ParseCorrectly()
    {
        string[] languages =
        [
            "chinese_(simplified)",
            "chinese_(traditional)",
            "english",
            "french_(france)",
            "german_(germany)",
            "italian_(italy)",
            "japanese_(japan)",
            "korean_(south_korea)",
            "polish_(poland)",
            "portuguese_(brazil)",
            "spanish_(latin_america)",
            "spanish_(spain)"
        ];

        foreach (string lang in languages)
        {
            string result = LocalizationService.ParseLanguageFromLines([$"g_language={lang}"]);
            result.Should().Be(lang.ToUpperInvariant());
        }
    }

    [Fact]
    public void ParseLanguageFromLines_ValueWithWhitespaceAround_Trims()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language=  english  "]);

        result.Should().Be("ENGLISH");
    }

    [Fact]
    public void ParseLanguageFromLines_NoEqualsInConfigLine_Skipped()
    {
        string result = LocalizationService.ParseLanguageFromLines(["g_language english", "g_language: english"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    [Fact]
    public void ParseLanguageFromLines_EqualsBeforeKey_Skipped()
    {
        string result = LocalizationService.ParseLanguageFromLines(["=english", " = english", "g_language"]);

        result.Should().Be(SCConstants.Localization.DefaultLanguage);
    }

    #endregion
}
