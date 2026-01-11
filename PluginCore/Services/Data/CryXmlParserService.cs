using System.Buffers.Binary;
using System.Text;
using System.Xml;
using SCStreamDeck.Common;

namespace SCStreamDeck.Services.Data;

/// <summary>
///     Service for parsing CryEngine binary XML files.
/// </summary>
public sealed class CryXmlParserService : ICryXmlParserService
{
    private const string CryXmlSignature = SCConstants.CryXml.CryXmlSignature;
    private const int HeaderSize = SCConstants.CryXml.HeaderSize;
    private const int NodeStructureSize = SCConstants.CryXml.NodeStructureSize;
    private const int AttributeEntrySize = SCConstants.CryXml.AttributeEntrySize;
    private const int ChildIndexSize = SCConstants.CryXml.ChildIndexSize;

    public Task<string?> ConvertCryXmlToTextAsync(byte[] binaryXmlData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binaryXmlData);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !TryConvertCryXmlToText(binaryXmlData, out string xmlText, out _) ? null : xmlText;
        }, cancellationToken);
    }

    public bool IsCryXml(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            return false;
        }

        string signature = ReadAsciiZ(data[..8]);

        return string.Equals(signature, CryXmlSignature, StringComparison.Ordinal);
    }

    #region CryXml Parsing Logic

    private static bool TryConvertCryXmlToText(ReadOnlySpan<byte> data, out string xmlText, out string error)
    {
        xmlText = string.Empty;
        CryXmlDocument? doc = TryParseCryXml(data, out error);
        if (doc is null)
        {
            return false;
        }

        XmlWriterSettings settings = new()
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Document,
            Encoding = Encoding.UTF8,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };

        StringBuilder stringBuilder = new(Math.Min(4 * 1024 * 1024, data.Length * 4));
        using (StringWriter sw = new(stringBuilder))
        using (XmlWriter xw = XmlWriter.Create(sw, settings))
        {
            WriteCryXmlNode(xw, doc, 0);
            xw.Flush();
        }

        xmlText = stringBuilder.ToString();
        return true;
    }

    // TODO: Another Candidate for ErrorMessages.cs pattern
    private static CryXmlDocument? TryParseCryXml(ReadOnlySpan<byte> data, out string error)
    {
        error = string.Empty;

        if (data.Length < HeaderSize)
        {
            error = "File is not a binary XML file (file size is too small).";
            return null;
        }

        string signature = ReadAsciiZ(data[..8]);
        if (!string.Equals(signature, CryXmlSignature, StringComparison.Ordinal))
        {
            error = "File is not a binary XML object (wrong header signature).";
            return null;
        }

        try
        {
            return ParseCryXmlInternal(data, out error);
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return null;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = $"Data access out of range: {ex.Message}";
            return null;
        }
        catch (OverflowException ex)
        {
            error = $"Arithmetic overflow: {ex.Message}";
            return null;
        }
    }

    private static CryXmlDocument? ParseCryXmlInternal(ReadOnlySpan<byte> data, out string error)
    {
        CryXmlHeader header = ReadCryXmlHeader(data);
        ValidateCryXmlBounds(data, header);
        ReadOnlySpan<byte> stringData = data.Slice((int)header.StringDataPos, (int)header.StringDataSize);
        (uint KeyOffset, uint ValueOffset)[] attributes = ReadCryXmlAttributesTable(data, header);
        uint[] childIndices = ReadCryXmlChildIndicesTable(data, header);
        CryXmlRawNode[] rawNodes = ReadCryXmlRawNodes(data, header);
        CryXmlNode[] nodes = BuildCryXmlNodes(stringData, rawNodes, attributes, childIndices);

        if (nodes.Length == 0)
        {
            error = "CryXML contains no nodes.";
            return null;
        }

        error = string.Empty;
        return new CryXmlDocument(nodes[0], nodes);
    }

    private static CryXmlHeader ReadCryXmlHeader(ReadOnlySpan<byte> data) =>
        new(
            ReadU32(data, 12),
            ReadU32(data, 16),
            ReadU32(data, 20),
            ReadU32(data, 24),
            ReadU32(data, 28),
            ReadU32(data, 32),
            ReadU32(data, 36),
            ReadU32(data, 40));

    private static void ValidateCryXmlBounds(ReadOnlySpan<byte> data, CryXmlHeader header)
    {
        checked
        {
            EnsureWithin(data.Length, header.NodeTablePos, header.NodeCount * NodeStructureSize, "node table");
            EnsureWithin(data.Length, header.AttrTablePos, header.AttrCount * AttributeEntrySize, "attribute table");
            EnsureWithin(data.Length, header.ChildTablePos, header.ChildCount * ChildIndexSize, "child index table");
            EnsureWithin(data.Length, header.StringDataPos, header.StringDataSize, "string data");
        }
    }

    private static (uint KeyOffset, uint ValueOffset)[] ReadCryXmlAttributesTable(ReadOnlySpan<byte> data,
        CryXmlHeader header)
    {
        (uint KeyOffset, uint ValueOffset)[] attributes = new (uint KeyOffset, uint ValueOffset)[header.AttrCount];
        for (uint i = 0; i < header.AttrCount; i++)
        {
            int offset = (int)header.AttrTablePos + (int)(i * AttributeEntrySize);
            attributes[i] = (ReadU32(data, offset), ReadU32(data, offset + 4));
        }

        return attributes;
    }

    private static uint[] ReadCryXmlChildIndicesTable(ReadOnlySpan<byte> data, CryXmlHeader header)
    {
        uint[] childIndices = new uint[header.ChildCount];
        for (uint i = 0; i < header.ChildCount; i++)
        {
            int offset = (int)header.ChildTablePos + (int)(i * ChildIndexSize);
            childIndices[i] = ReadU32(data, offset);
        }

        return childIndices;
    }

    private static CryXmlRawNode[] ReadCryXmlRawNodes(ReadOnlySpan<byte> data, CryXmlHeader header)
    {
        CryXmlRawNode[] rawNodes = new CryXmlRawNode[header.NodeCount];
        for (uint i = 0; i < header.NodeCount; i++)
        {
            int offset = (int)header.NodeTablePos + (int)(i * NodeStructureSize);
            rawNodes[i] = new CryXmlRawNode(
                ReadU32(data, offset),
                ReadU32(data, offset + 4),
                ReadU16(data, offset + 8),
                ReadU16(data, offset + 10),
                ReadU32(data, offset + 12),
                ReadU32(data, offset + 16),
                ReadU32(data, offset + 20));
        }

        return rawNodes;
    }

    private static CryXmlNode[] BuildCryXmlNodes(ReadOnlySpan<byte> stringData, CryXmlRawNode[] rawNodes,
        (uint KeyOffset, uint ValueOffset)[] attributes, uint[] childIndices)
    {
        CryXmlNode[] nodes = new CryXmlNode[rawNodes.Length];
        for (int i = 0; i < rawNodes.Length; i++)
        {
            nodes[i] = BuildCryXmlNode(stringData, rawNodes[i], attributes, childIndices, rawNodes.Length);
        }

        return nodes;
    }

    private static CryXmlNode BuildCryXmlNode(ReadOnlySpan<byte> stringData, CryXmlRawNode rawNode,
        (uint KeyOffset, uint ValueOffset)[] attributes, uint[] childIndices, int nodeCount)
    {
        string tag = ReadStringByOffset(stringData, rawNode.TagOffset);
        string content = ReadStringByOffset(stringData, rawNode.ContentOffset);
        List<KeyValuePair<string, string>> attrs = new(rawNode.AttributeCount);

        for (uint a = 0; a < rawNode.AttributeCount; a++)
        {
            uint attrIndex = rawNode.FirstAttributeIndex + a;
            if (attrIndex >= attributes.Length)
            {
                throw new FormatException($"Attribute index out of bounds: {attrIndex} (count={attributes.Length})");
            }

            (uint keyOffset, uint valueOffset) = attributes[attrIndex];
            string key = ReadStringByOffset(stringData, keyOffset);
            string value = ReadStringByOffset(stringData, valueOffset);
            attrs.Add(new KeyValuePair<string, string>(key, value));
        }

        List<int> children = new(rawNode.ChildCount);
        for (uint c = 0; c < rawNode.ChildCount; c++)
        {
            uint childIdxIndex = rawNode.FirstChildIndex + c;
            if (childIdxIndex >= childIndices.Length)
            {
                throw new FormatException($"Child index out of bounds: {childIdxIndex} (count={childIndices.Length})");
            }

            uint childNodeIndex = childIndices[childIdxIndex];
            if (childNodeIndex >= nodeCount)
            {
                throw new FormatException($"Child node index out of bounds: {childNodeIndex} (nodeCount={nodeCount})");
            }

            children.Add((int)childNodeIndex);
        }

        return new CryXmlNode(tag, content, attrs, children);
    }

    private static void WriteCryXmlNode(XmlWriter xw, CryXmlDocument doc, int nodeIndex)
    {
        CryXmlNode node = doc.Nodes[nodeIndex];
        xw.WriteStartElement(node.Tag);

        foreach ((string key, string value) in node.Attributes)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            xw.WriteAttributeString(key, value);
        }

        if (!string.IsNullOrEmpty(node.Content))
        {
            xw.WriteString(node.Content);
        }

        foreach (int childIndex in node.Children)
        {
            WriteCryXmlNode(xw, doc, childIndex);
        }

        xw.WriteEndElement();
    }

    #endregion

    #region Helper Methods

    private static uint ReadU32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));

    private static ushort ReadU16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));

    private static void EnsureWithin(int dataLength, uint offset, uint length, string label)
    {
        if (offset > (uint)dataLength)
        {
            throw new FormatException($"{label}: offset out of bounds ({offset} > {dataLength})");
        }

        uint end = checked(offset + length);
        if (end > (uint)dataLength)
        {
            throw new FormatException($"{label}: range out of bounds (end={end} > {dataLength})");
        }
    }

    private static string ReadStringByOffset(ReadOnlySpan<byte> stringData, uint offset) =>
        offset >= (uint)stringData.Length ? string.Empty : ReadAsciiZ(stringData[(int)offset..]);

    private static string ReadAsciiZ(ReadOnlySpan<byte> data)
    {
        int len = 0;
        while (len < data.Length && data[len] != 0)
        {
            len++;
        }

        return Encoding.ASCII.GetString(data[..len]);
    }

    #endregion

    #region Internal Types

    private sealed record CryXmlNode(
        string Tag,
        string Content,
        IReadOnlyList<KeyValuePair<string, string>> Attributes,
        IReadOnlyList<int> Children);

    private sealed record CryXmlDocument(CryXmlNode Root, IReadOnlyList<CryXmlNode> Nodes);

    private sealed record CryXmlHeader(
        uint NodeTablePos,
        uint NodeCount,
        uint AttrTablePos,
        uint AttrCount,
        uint ChildTablePos,
        uint ChildCount,
        uint StringDataPos,
        uint StringDataSize);

    private readonly record struct CryXmlRawNode(
        uint TagOffset,
        uint ContentOffset,
        ushort AttributeCount,
        ushort ChildCount,
        uint ParentIndex,
        uint FirstAttributeIndex,
        uint FirstChildIndex);

    #endregion
}
