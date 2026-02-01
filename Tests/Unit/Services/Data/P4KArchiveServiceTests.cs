using System.IO.Compression;
using System.Text;
using FluentAssertions;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using Moq;
using SCStreamDeck.Common;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Data;

namespace Tests.Unit.Services.Data;

public sealed class P4KArchiveServiceTests
{
    [Fact]
    public async Task OpenArchiveAsync_WithNullPath_ReturnsFalse()
    {
        P4KArchiveService service = new(new SystemFileSystem());

        bool result = await service.OpenArchiveAsync(null!, CancellationToken.None);

        result.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenArchiveAsync_WithNonexistentFile_ReturnsFalse()
    {
        P4KArchiveService service = new(new SystemFileSystem());
        string path = Path.Combine(Path.GetTempPath(), "missing-data.p4k");

        bool result = await service.OpenArchiveAsync(path, CancellationToken.None);

        result.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenArchiveAsync_WhenZipOpenFails_DoesNotLeakStream()
    {
        string p4KPath = Path.Combine(Path.GetTempPath(), $"scstreamdeck-{Guid.NewGuid():N}.p4k");

        TrackingMemoryStream trackingStream = new([0x00]);

        Mock<IFileSystem> fileSystem = new(MockBehavior.Strict);
        fileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(trackingStream);

        P4KArchiveService service = new(fileSystem.Object);

        bool opened = await service.OpenArchiveAsync(p4KPath, CancellationToken.None);

        opened.Should().BeFalse();
        service.IsArchiveOpen.Should().BeFalse();
        trackingStream.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void CloseArchive_IsSafeWhenNotOpened()
    {
        P4KArchiveService service = new(new SystemFileSystem());

        service.CloseArchive();

        service.IsArchiveOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenArchiveAsync_WithValidZip_ReturnsTrue_AndScanDirectoryFindsEntries()
    {
        string p4KPath = CreateZip(
            ("Data/Libs/Config/defaultProfile.xml", "<xml />", CompressionLevel.Optimal),
            ("Data/Libs/Config/readme.txt", "hello", CompressionLevel.NoCompression),
            ("Data/Objects/ship.xml", "<ship />", CompressionLevel.Optimal));

        P4KArchiveService? service = null;

        try
        {
            service = new P4KArchiveService(new SystemFileSystem());

            bool opened = await service.OpenArchiveAsync(p4KPath, CancellationToken.None);
            opened.Should().BeTrue();
            service.IsArchiveOpen.Should().BeTrue();

            IReadOnlyList<P4KFileEntry> found = await service.ScanDirectoryAsync(
                "data/libs/config",
                "DEFAULTPROFILE.XML",
                CancellationToken.None);

            found.Should().HaveCount(1);
            found[0].Path.Should().Be("Data/Libs/Config/defaultProfile.xml");

            string? text = await service.ReadFileAsTextAsync(found[0], CancellationToken.None);
            text.Should().Be("<xml />");
        }
        finally
        {
            service?.Dispose();
            File.Delete(p4KPath);
        }
    }

    [Fact]
    public async Task ReadFileAsTextAsync_FindsEntry_WhenDataPrefixDiffers()
    {
        string p4KPath = CreateZip(
            ("Data/Objects/a.txt", "A", CompressionLevel.Optimal),
            ("Objects/b.txt", "B", CompressionLevel.Optimal));

        P4KArchiveService? service = null;

        try
        {
            service = new P4KArchiveService(new SystemFileSystem());
            (await service.OpenArchiveAsync(p4KPath, CancellationToken.None)).Should().BeTrue();

            string? a = await service.ReadFileAsTextAsync(
                new P4KFileEntry
                {
                    Path = "Objects/a.txt",
                    Offset = 0,
                    CompressedSize = 0,
                    UncompressedSize = 0,
                    IsCompressed = false
                }, CancellationToken.None);
            a.Should().Be("A");

            string? b = await service.ReadFileAsTextAsync(
                new P4KFileEntry
                {
                    Path = "Data/Objects/b.txt",
                    Offset = 0,
                    CompressedSize = 0,
                    UncompressedSize = 0,
                    IsCompressed = false
                }, CancellationToken.None);
            b.Should().Be("B");
        }
        finally
        {
            service?.Dispose();
            File.Delete(p4KPath);
        }
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsNull_WhenEntryMissing()
    {
        string p4KPath = CreateZip(("Data/Objects/present.txt", "OK", CompressionLevel.NoCompression));

        P4KArchiveService? service = null;

        try
        {
            service = new P4KArchiveService(new SystemFileSystem());
            (await service.OpenArchiveAsync(p4KPath, CancellationToken.None)).Should().BeTrue();

            byte[]? missing = await service.ReadFileAsync(
                new P4KFileEntry
                {
                    Path = "Data/Objects/missing.txt",
                    Offset = 0,
                    CompressedSize = 0,
                    UncompressedSize = 0,
                    IsCompressed = false
                }, CancellationToken.None);
            missing.Should().BeNull();
        }
        finally
        {
            service?.Dispose();
            File.Delete(p4KPath);
        }
    }

    private static string CreateZip(params (string Path, string Contents, CompressionLevel Level)[] entries)
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"scstreamdeck-{Guid.NewGuid():N}.p4k");

        using FileStream fs = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using ZipOutputStream zos = new(fs) { IsStreamOwner = true };

        foreach ((string path, string contents, CompressionLevel level) in entries)
        {
            byte[] data = Encoding.UTF8.GetBytes(contents);
            ZipEntry ze = new(path);

            if (level == CompressionLevel.NoCompression)
            {
                ze.CompressionMethod = CompressionMethod.Stored;
                ze.Size = data.Length;

                Crc32 crc = new();
                crc.Update(data);
                ze.Crc = crc.Value;
            }
            else
            {
                ze.CompressionMethod = CompressionMethod.Deflated;
            }

            zos.PutNextEntry(ze);
            zos.Write(data, 0, data.Length);
            zos.CloseEntry();
        }

        zos.Finish();

        return filePath;
    }

    private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
