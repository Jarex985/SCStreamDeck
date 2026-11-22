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
    public void Render_Hidden_DrawsOnlyBackground()
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

            using Bitmap hidden = renderer.Render(new TelemetryRenderModel("Text", path, "#FFFFFF", "#FF0000", true));

            for (int y = 0; y < hidden.Height; y += 8)
            {
                for (int x = 0; x < hidden.Width; x += 8)
                {
                    hidden.GetPixel(x, y).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("#00FF00", true)]
    [InlineData("nonsense-color", false)]
    [InlineData(null, false)]
    public void ParseColor_AcceptsHtmlColorsOnly(string? input, bool valid) =>
        (TelemetryKeyRenderer.ParseColor(input) != null).Should().Be(valid);
}
