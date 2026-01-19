using System.Text;
using FluentAssertions;
using SCStreamDeck.Services.Data;

namespace Tests.Unit.Services.Data;

public sealed class CryXmlParserServiceTests
{
    [Fact]
    public async Task ConvertCryXmlToTextAsync_InvalidSignature_ReturnsNull()
    {
        CryXmlParserService service = new();
        byte[] invalid = "NotCryXml"u8.ToArray();

        string? result = await service.ConvertCryXmlToTextAsync(invalid);

        result.Should().BeNull();
    }

    [Fact]
    public void IsCryXml_WithValidSignature_ReturnsTrue()
    {
        CryXmlParserService service = new();
        byte[] data = new byte[44];
        Encoding.ASCII.GetBytes("CryXmlB").CopyTo(data, 0);

        bool result = service.IsCryXml(data);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCryXml_TooSmall_ReturnsFalse()
    {
        CryXmlParserService service = new();
        byte[] data = [1, 2, 3];

        bool result = service.IsCryXml(data);

        result.Should().BeFalse();
    }
}
