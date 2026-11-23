using System.IO.MemoryMappedFiles;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Recovers the telemetry protocol definition from <c>StarCitizen.exe</c>. The game embeds its protobuf
///     <c>FileDescriptorProto</c>s (files under <c>sc/game/telemetry/v1/</c>) as raw bytes. A descriptor starts with
///     field 1 (the file name), so each occurrence of the path prefix preceded by a matching tag/length header is a
///     candidate start; the end is found by following fields while they still look like a descriptor.
///     The result always matches the installed build, also after a game patch.
/// </summary>
public static class ProtoDescriptorExtractor
{
    private const int MaxDescriptorBytes = 4 << 20;
    private const int MaxFileNameBytes = 512;
    private const int WindowBytes = 1 << 30;
    private const int WindowOverlapBytes = 1 << 23;
    private const int MaxEndAttempts = 8;
    private const byte NameTag = 0x0A;

    private static readonly byte[] s_needle = Encoding.ASCII.GetBytes("sc/game/telemetry");

    private static readonly FileDescriptor[] s_wellKnownFiles =
    [
        AnyReflection.Descriptor, ApiReflection.Descriptor, DurationReflection.Descriptor, EmptyReflection.Descriptor,
        FieldMaskReflection.Descriptor, SourceContextReflection.Descriptor, StructReflection.Descriptor,
        TimestampReflection.Descriptor, TypeReflection.Descriptor, WrappersReflection.Descriptor
    ];

    // FileDescriptorProto field number -> wire type (0 varint, 2 length-delimited).
    private static readonly Dictionary<int, int> s_fileDescriptorFields = new()
    {
        [1] = 2, [2] = 2, [3] = 2, [4] = 2, [5] = 2, [6] = 2, [7] = 2, [8] = 2, [9] = 2,
        [10] = 0, [11] = 0, [12] = 2, [14] = 0
    };

    /// <summary>
    ///     Scans the executable and returns the telemetry file descriptors found, dependencies first.
    ///     Reading uses a shared, read-only mapping, so it also works while the game is running.
    /// </summary>
    public static unsafe IReadOnlyList<FileDescriptorProto> ExtractFromFile(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        using FileStream stream = new(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = stream.Length;
        if (length == 0)
        {
            return [];
        }

        using MemoryMappedFile map = MemoryMappedFile.CreateFromFile(
            stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        using MemoryMappedViewAccessor view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        byte* pointer = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try
        {
            pointer += view.PointerOffset;
            Dictionary<string, FileDescriptorProto> found = new(StringComparer.Ordinal);

            // Windows overlap so a descriptor on a window boundary is still seen whole.
            for (long offset = 0; offset < length; offset += WindowBytes - WindowOverlapBytes)
            {
                int windowLength = (int)Math.Min(WindowBytes, length - offset);
                ScanInto(new ReadOnlySpan<byte>(pointer + offset, windowLength), found);

                if (offset + windowLength >= length)
                {
                    break;
                }
            }

            return OrderByDependencies(found);
        }
        finally
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    /// <summary>
    ///     Extracts descriptors from a raw byte buffer (the content of an executable).
    /// </summary>
    public static IReadOnlyList<FileDescriptorProto> Extract(ReadOnlySpan<byte> data)
    {
        Dictionary<string, FileDescriptorProto> found = new(StringComparer.Ordinal);
        ScanInto(data, found);
        return OrderByDependencies(found);
    }

    /// <summary>
    ///     Builds usable descriptors; throws if a dependency is missing or the definitions are inconsistent.
    ///     Imports of protobuf's well-known types (timestamp, wrappers, ...) are satisfied automatically.
    /// </summary>
    public static IReadOnlyList<FileDescriptor> Build(IReadOnlyList<FileDescriptorProto> protos)
    {
        ArgumentNullException.ThrowIfNull(protos);

        HashSet<string> provided = protos.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        List<ByteString> data = [];

        void AddWellKnown(string name)
        {
            FileDescriptor? file = s_wellKnownFiles.FirstOrDefault(f => f.Name == name);
            if (file == null || !provided.Add(name))
            {
                return;
            }

            foreach (FileDescriptor dependency in file.Dependencies)
            {
                AddWellKnown(dependency.Name);
            }

            data.Add(file.SerializedData);
        }

        foreach (string dependency in protos.SelectMany(p => p.Dependency).ToList())
        {
            AddWellKnown(dependency);
        }

        data.AddRange(protos.Select(p => p.ToByteString()));
        return FileDescriptor.BuildFromByteStrings(data);
    }

    private static void ScanInto(ReadOnlySpan<byte> data, Dictionary<string, FileDescriptorProto> found)
    {
        int position = 0;
        while (position < data.Length)
        {
            int relative = data[position..].IndexOf(s_needle);
            if (relative < 0)
            {
                return;
            }

            int hit = position + relative;
            position = hit + 1;

            FileDescriptorProto? descriptor = TryReadDescriptorContaining(data, hit);
            if (descriptor == null)
            {
                continue;
            }

            if (!found.TryGetValue(descriptor.Name, out FileDescriptorProto? previous) ||
                descriptor.CalculateSize() > previous.CalculateSize())
            {
                found[descriptor.Name] = descriptor;
            }
        }
    }

    // `hit` is where the file name text would start: it must be preceded by tag 0x0A and the name length.
    private static FileDescriptorProto? TryReadDescriptorContaining(ReadOnlySpan<byte> data, int hit)
    {
        foreach (int headerLength in (ReadOnlySpan<int>)[2, 3])
        {
            int start = hit - headerLength;
            if (start < 0 || data[start] != NameTag)
            {
                continue;
            }

            if (!TryReadVarint(data, start + 1, out ulong nameLength, out int nameStart) || nameStart != hit ||
                nameLength is 0 or > MaxFileNameBytes || hit + (int)nameLength > data.Length)
            {
                continue;
            }

            string name = Encoding.UTF8.GetString(data.Slice(hit, (int)nameLength));
            if (!name.EndsWith(".proto", StringComparison.Ordinal))
            {
                continue;
            }

            FileDescriptorProto? parsed = ParseLongestValidPrefix(data, start, name);
            if (parsed != null)
            {
                return parsed;
            }
        }

        return null;
    }

    // The end of a descriptor is not marked. Take the longest run of plausible fields that still parses.
    private static FileDescriptorProto? ParseLongestValidPrefix(ReadOnlySpan<byte> data, int start, string name)
    {
        List<int> ends = FieldEnds(data, start);
        for (int i = ends.Count - 1; i >= Math.Max(0, ends.Count - MaxEndAttempts); i--)
        {
            try
            {
                FileDescriptorProto candidate = FileDescriptorProto.Parser.ParseFrom(data[start..ends[i]]);
                if (candidate.Name == name && (candidate.MessageType.Count > 0 || candidate.Service.Count > 0 ||
                                              candidate.EnumType.Count > 0))
                {
                    return candidate;
                }
            }
            catch (InvalidProtocolBufferException)
            {
                // Trailing bytes were not part of the descriptor; try a shorter prefix.
            }
        }

        return null;
    }

    private static List<int> FieldEnds(ReadOnlySpan<byte> data, int start)
    {
        List<int> ends = [];
        int limit = (int)Math.Min(data.Length, (long)start + MaxDescriptorBytes);
        int index = start;
        bool seenName = false;

        while (index < limit)
        {
            if (!TryReadVarint(data, index, out ulong tag, out int next))
            {
                break;
            }

            int field = (int)(tag >> 3);
            int wireType = (int)(tag & 7);
            if (!s_fileDescriptorFields.TryGetValue(field, out int expected) || expected != wireType ||
                (field == 1 && seenName))
            {
                break;
            }

            if (wireType == 0)
            {
                if (!TryReadVarint(data, next, out _, out next))
                {
                    break;
                }
            }
            else
            {
                if (!TryReadVarint(data, next, out ulong length, out next) || length > (ulong)(limit - next))
                {
                    break;
                }

                next += (int)length;
            }

            seenName |= field == 1;
            index = next;
            ends.Add(index);
        }

        return ends;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> data, int index, out ulong value, out int next)
    {
        value = 0;
        next = index;
        int shift = 0;

        while (next < data.Length && shift <= 63)
        {
            byte b = data[next++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
        }

        return false;
    }

    // Dependencies first, as required to build descriptors. Unknown dependencies are left for Build to report.
    private static List<FileDescriptorProto> OrderByDependencies(Dictionary<string, FileDescriptorProto> found)
    {
        List<FileDescriptorProto> ordered = [];
        HashSet<string> done = new(StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);

        void Visit(FileDescriptorProto file)
        {
            if (done.Contains(file.Name) || !visiting.Add(file.Name))
            {
                return;
            }

            foreach (string dependency in file.Dependency)
            {
                if (found.TryGetValue(dependency, out FileDescriptorProto? dep))
                {
                    Visit(dep);
                }
            }

            visiting.Remove(file.Name);
            done.Add(file.Name);
            ordered.Add(file);
        }

        foreach (FileDescriptorProto file in found.Values.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            Visit(file);
        }

        return ordered;
    }
}
