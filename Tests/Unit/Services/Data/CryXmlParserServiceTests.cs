using System.Buffers.Binary;
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

        CryXmlConversionResult result = await service.ConvertCryXmlToTextAsync(invalid);

        result.IsSuccess.Should().BeFalse();
        result.Xml.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
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

    [Fact]
    public async Task ConvertCryXmlToTextAsync_MinimalValid_ReturnsRootElement()
    {
        CryXmlParserService service = new();
        byte[] data = BuildCryXmlMinimal("Root");

        CryXmlConversionResult result = await service.ConvertCryXmlToTextAsync(data);

        result.IsSuccess.Should().BeTrue();
        result.Xml.Should().Be("<Root />");
    }

    [Fact]
    public async Task ConvertCryXmlToTextAsync_WithAttributeAndChild_ReturnsExpectedXml()
    {
        CryXmlParserService service = new();
        byte[] data = BuildCryXmlWithAttributeAndChild();

        CryXmlConversionResult result = await service.ConvertCryXmlToTextAsync(data);

        result.IsSuccess.Should().BeTrue();
        result.Xml.Should().Be("<Root attr=\"value\"><Child>Hello</Child></Root>");
    }

    [Fact]
    public async Task ConvertCryXmlToTextAsync_WithInvalidAttributeIndex_ReturnsNull()
    {
        CryXmlParserService service = new();
        byte[] data = BuildCryXmlWithInvalidAttributeIndex();

        CryXmlConversionResult result = await service.ConvertCryXmlToTextAsync(data);

        result.IsSuccess.Should().BeFalse();
        result.Xml.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    private static byte[] BuildCryXmlMinimal(string tag)
    {
        (byte[] stringData, uint tagOffset, uint emptyOffset) = BuildStringTable(tag, string.Empty);

        const uint nodeTablePos = 44;
        const uint nodeCount = 1;
        uint attrTablePos = nodeTablePos + nodeCount * 28;
        const uint attrCount = 0;
        uint childTablePos = attrTablePos + attrCount * 8;
        const uint childCount = 0;
        uint stringDataPos = childTablePos + childCount * 4;

        byte[] buffer = new byte[stringDataPos + stringData.Length];
        WriteSignatureAndHeader(buffer, nodeTablePos, nodeCount, attrTablePos, attrCount, childTablePos, childCount,
            stringDataPos,
            (uint)stringData.Length);

        WriteNode(buffer, (int)nodeTablePos, tagOffset, emptyOffset, 0, 0, 0,
            0);

        stringData.CopyTo(buffer, (int)stringDataPos);
        return buffer;
    }

    private static byte[] BuildCryXmlWithAttributeAndChild()
    {
        // String order matters for offsets.
        (byte[] stringData, Dictionary<string, uint> offsets) = BuildStringTableCore(
            "Root",
            string.Empty,
            "Child",
            "Hello",
            "attr",
            "value");

        const uint nodeTablePos = 44;
        const uint nodeCount = 2;
        uint attrTablePos = nodeTablePos + nodeCount * 28;
        const uint attrCount = 1;
        uint childTablePos = attrTablePos + attrCount * 8;
        const uint childCount = 1;
        uint stringDataPos = childTablePos + childCount * 4;

        byte[] buffer = new byte[stringDataPos + stringData.Length];
        WriteSignatureAndHeader(buffer, nodeTablePos, nodeCount, attrTablePos, attrCount, childTablePos, childCount,
            stringDataPos,
            (uint)stringData.Length);

        // Root node (index 0): 1 attribute, 1 child.
        WriteNode(buffer, (int)nodeTablePos,
            offsets["Root"],
            offsets[string.Empty],
            1,
            1,
            0,
            0);

        // Child node (index 1): text content.
        WriteNode(buffer, (int)(nodeTablePos + 28),
            offsets["Child"],
            offsets["Hello"],
            0,
            0,
            0,
            0);

        WriteAttribute(buffer, (int)attrTablePos, offsets["attr"], offsets["value"]);
        WriteChildIndex(buffer, (int)childTablePos, 1);

        stringData.CopyTo(buffer, (int)stringDataPos);
        return buffer;
    }

    private static byte[] BuildCryXmlWithInvalidAttributeIndex()
    {
        // Header says 0 attributes, but node says it has 1.
        (byte[] stringData, uint tagOffset, uint emptyOffset) = BuildStringTable("Root", string.Empty);

        const uint nodeTablePos = 44;
        const uint nodeCount = 1;
        uint attrTablePos = nodeTablePos + nodeCount * 28;
        const uint attrCount = 0;
        uint childTablePos = attrTablePos + attrCount * 8;
        const uint childCount = 0;
        uint stringDataPos = childTablePos + childCount * 4;

        byte[] buffer = new byte[stringDataPos + stringData.Length];
        WriteSignatureAndHeader(buffer, nodeTablePos, nodeCount, attrTablePos, attrCount, childTablePos, childCount,
            stringDataPos,
            (uint)stringData.Length);

        WriteNode(buffer, (int)nodeTablePos, tagOffset, emptyOffset, 1, 0, 0,
            0);

        stringData.CopyTo(buffer, (int)stringDataPos);
        return buffer;
    }

    private static void WriteSignatureAndHeader(byte[] buffer,
        uint nodeTablePos,
        uint nodeCount,
        uint attrTablePos,
        uint attrCount,
        uint childTablePos,
        uint childCount,
        uint stringDataPos,
        uint stringDataSize)
    {
        Encoding.ASCII.GetBytes("CryXmlB").CopyTo(buffer, 0);
        buffer[7] = 0;

        WriteU32(buffer, 12, nodeTablePos);
        WriteU32(buffer, 16, nodeCount);
        WriteU32(buffer, 20, attrTablePos);
        WriteU32(buffer, 24, attrCount);
        WriteU32(buffer, 28, childTablePos);
        WriteU32(buffer, 32, childCount);
        WriteU32(buffer, 36, stringDataPos);
        WriteU32(buffer, 40, stringDataSize);
    }

    private static void WriteNode(byte[] buffer, int offset, uint tagOffset, uint contentOffset, ushort attributeCount,
        ushort childCount,
        uint firstAttrIndex, uint firstChildIndex)
    {
        WriteU32(buffer, offset + 0, tagOffset);
        WriteU32(buffer, offset + 4, contentOffset);
        WriteU16(buffer, offset + 8, attributeCount);
        WriteU16(buffer, offset + 10, childCount);
        WriteU32(buffer, offset + 12, 0); // ParentIndex
        WriteU32(buffer, offset + 16, firstAttrIndex);
        WriteU32(buffer, offset + 20, firstChildIndex);
        WriteU32(buffer, offset + 24, 0); // unused/padding (NodeStructureSize is 28)
    }

    private static void WriteAttribute(byte[] buffer, int offset, uint keyOffset, uint valueOffset)
    {
        WriteU32(buffer, offset + 0, keyOffset);
        WriteU32(buffer, offset + 4, valueOffset);
    }

    private static void WriteChildIndex(byte[] buffer, int offset, uint childNodeIndex) =>
        WriteU32(buffer, offset, childNodeIndex);

    private static void WriteU32(byte[] buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), value);

    private static void WriteU16(byte[] buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), value);

    private static (byte[] Data, uint FirstOffset, uint SecondOffset) BuildStringTable(string first, string second)
    {
        (byte[] data, Dictionary<string, uint> offsets) = BuildStringTableCore(first, second);
        return (data, offsets[first], offsets[second]);
    }

    private static (byte[] Data, Dictionary<string, uint> Offsets) BuildStringTableCore(params string[] strings)
    {
        Dictionary<string, uint> offsets = new();
        List<byte> bytes = new();

        foreach (string s in strings)
        {
            if (!offsets.ContainsKey(s))
            {
                offsets[s] = (uint)bytes.Count;
                bytes.AddRange(Encoding.ASCII.GetBytes(s));
                bytes.Add(0);
            }
        }

        return (bytes.ToArray(), offsets);
    }
}
