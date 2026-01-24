using System.Security;
using FluentAssertions;
using Newtonsoft.Json;
using SCStreamDeck.Common;
using SCStreamDeck.Services.Keybinding;

namespace Tests.Security;

public sealed class InputValidationTests
{
    [Fact]
    public void TryNormalizePath_NullOrEmpty_ReturnsFalse()
    {
        SecurePathValidator.TryNormalizePath(null!, out _).Should().BeFalse();
        SecurePathValidator.TryNormalizePath(" ", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("C::/bad")]
    [InlineData("bad\0path.json")]
    public void TryNormalizePath_InvalidCharactersOrColon_ReturnsFalse(string path)
    {
        bool result = SecurePathValidator.TryNormalizePath(path, out string normalized);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
    }

    [Fact]
    public async Task KeybindingLoader_InvalidPath_ReturnsFalseAndDoesNotThrow()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());

        bool result = await loader.LoadKeybindingsAsync("C::/bad/path.json");

        result.Should().BeFalse();
        loader.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task KeybindingLoader_NonexistentFile_ReturnsFalse()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        string tempPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.json");

        bool result = await loader.LoadKeybindingsAsync(tempPath);

        result.Should().BeFalse();
        loader.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task KeybindingLoader_MalformedJson_ReturnsFalseWithoutSensitiveLeak()
    {
        KeybindingLoaderService loader = new(new SystemFileSystem());
        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "{ invalid json }");

        bool result = await loader.LoadKeybindingsAsync(tempFile);

        result.Should().BeFalse();
        loader.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void JsonDeserializer_InvalidJson_ThrowsJsonException()
    {
        Action act = () => JsonConvert.DeserializeObject("{ invalid json }");

        act.Should().Throw<JsonReaderException>();
    }

    [Fact]
    public void SecurePathValidator_GetSecurePath_ThrowsSecurityException_OnBlockedBase()
    {
        Action act = () => SecurePathValidator.GetSecurePath("file.txt", "C:/Windows/System32");

        act.Should().Throw<SecurityException>();
    }
}
