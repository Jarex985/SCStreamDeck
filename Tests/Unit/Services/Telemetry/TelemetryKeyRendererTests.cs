using System.Drawing;
using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryKeyRendererTests
{
    [Fact]
    public void Render_DrawsBackgroundColorAndText()
    {
        using TelemetryKeyRenderer renderer = new();

        using Bitmap bitmap = renderer.Render(new TelemetryRenderModel("Pressure\n12.3", null, "#FFFFFF", "#FF0000"));

        bitmap.Width.Should().Be(TelemetryKeyRenderer.Size);
        bitmap.GetPixel(2, 2).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
        bool textDrawn = false;
        for (int y = 0; y < bitmap.Height && !textDrawn; y++)
        {
            for (int x = 0; x < bitmap.Width && !textDrawn; x++)
            {
                textDrawn = bitmap.GetPixel(x, y).G > 200;
            }
        }

        textDrawn.Should().BeTrue();
    }

    [Fact]
    public void Render_UsesImageAndIgnoresMissingFiles()
    {
        string path = Path.Combine(Path.GetTempPath(), $"telemetry-test-{Guid.NewGuid():N}.png");
        try
        {
            using (Bitmap source = new(10, 10))
            {
                using Graphics g = Graphics.FromImage(source);
                g.Clear(Color.Lime);
                source.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }

            using TelemetryKeyRenderer renderer = new();

            using Bitmap withImage = renderer.Render(new TelemetryRenderModel("", path, null, null));
            withImage.GetPixel(72, 72).ToArgb().Should().Be(Color.Lime.ToArgb());

            Action missing = () => renderer.Render(new TelemetryRenderModel("x", @"C:\does\not\exist.png", null, null)).Dispose();
            missing.Should().NotThrow();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Render_HiddenEverything_DrawsOnlyBlack()
    {
        string path = WriteLimeImage();
        try
        {
            using TelemetryKeyRenderer renderer = new();

            using Bitmap hidden = renderer.Render(
                new TelemetryRenderModel("Text", path, "#FFFFFF", "#FF0000", true, TelemetryFlashTarget.Everything));

            AllPixels(hidden).Should().OnlyContain(c => c == Color.Black.ToArgb());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Render_HiddenBackgroundAndImage_KeepsTextOnBlack()
    {
        string path = WriteLimeImage();
        try
        {
            using TelemetryKeyRenderer renderer = new();

            using Bitmap hidden = renderer.Render(
                new TelemetryRenderModel("Text", path, "#FFFFFF", "#FF0000", true, TelemetryFlashTarget.BackgroundAndImage));

            hidden.GetPixel(2, 2).ToArgb().Should().Be(Color.Black.ToArgb());
            AllPixels(hidden).Should().NotContain(Color.Lime.ToArgb()).And.Contain(c => (c & 0xFF) > 200);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Render_HiddenText_KeepsBackgroundAndImage()
    {
        string path = WriteLimeImage();
        try
        {
            using TelemetryKeyRenderer renderer = new();

            using Bitmap hidden = renderer.Render(
                new TelemetryRenderModel("Text", path, "#FFFFFF", "#FF0000", true, TelemetryFlashTarget.Text));

            hidden.GetPixel(72, 72).ToArgb().Should().Be(Color.Lime.ToArgb());
            hidden.GetPixel(72, 130).ToArgb().Should().Be(Color.Lime.ToArgb()); // where the text would be over an image
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteLimeImage()
    {
        string path = Path.Combine(Path.GetTempPath(), $"telemetry-test-{Guid.NewGuid():N}.png");
        using Bitmap source = new(10, 10);
        using Graphics g = Graphics.FromImage(source);
        g.Clear(Color.Lime);
        source.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    private static List<int> AllPixels(Bitmap bitmap)
    {
        List<int> pixels = [];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                pixels.Add(bitmap.GetPixel(x, y).ToArgb());
            }
        }

        return pixels;
    }

    [Theory]
    [InlineData("#00FF00", true)]
    [InlineData("nonsense-color", false)]
    [InlineData(null, false)]
    public void ParseColor_AcceptsHtmlColorsOnly(string? input, bool valid) =>
        (TelemetryKeyRenderer.ParseColor(input) != null).Should().Be(valid);
}
