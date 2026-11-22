using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     Everything needed to draw one key frame.
/// </summary>
public sealed record TelemetryRenderModel(
    string Text,
    string? ImagePath,
    string? TextColor,
    string? BackgroundColor,
    bool Hidden = false);

/// <summary>
///     Draws a telemetry key: optional background image (cover-fit) with outlined, auto-fitted text on top.
///     Not thread-safe: use one instance per key and serialize calls.
/// </summary>
public sealed class TelemetryKeyRenderer : IDisposable
{
    public const int Size = 144;

    private const string FontName = "Segoe UI";
    private const float MaxFontSize = 34f;
    private const float MinFontSize = 9f;
    private const int Margin = 6;
    private const int MaxCachedImages = 16;

    private static readonly Color s_defaultBackground = Color.FromArgb(0x1B, 0x1F, 0x27);

    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (DateTime Stamp, Bitmap Image)> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    public Bitmap Render(TelemetryRenderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Bitmap canvas = new(Size, Size);
        using Graphics g = Graphics.FromImage(canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        g.Clear(ParseColor(model.BackgroundColor) ?? s_defaultBackground);

        if (model.Hidden)
        {
            return canvas;
        }

        bool hasImage = false;
        Bitmap? image = LoadImage(model.ImagePath);
        if (image != null)
        {
            DrawCover(g, image);
            hasImage = true;
        }

        DrawText(g, model.Text, ParseColor(model.TextColor) ?? Color.White, hasImage);
        return canvas;
    }

    public void Dispose()
    {
        lock (_cacheGate)
        {
            foreach ((DateTime _, Bitmap Image) entry in _imageCache.Values)
            {
                entry.Image.Dispose();
            }

            _imageCache.Clear();
        }
    }

    public static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return ColorTranslator.FromHtml(value.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    private Bitmap? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            DateTime stamp = File.GetLastWriteTimeUtc(path);
            lock (_cacheGate)
            {
                if (_imageCache.TryGetValue(path, out (DateTime Stamp, Bitmap Image) cached) && cached.Stamp == stamp)
                {
                    return cached.Image;
                }

                if (_imageCache.Count >= MaxCachedImages)
                {
                    foreach ((DateTime _, Bitmap Image) entry in _imageCache.Values)
                    {
                        entry.Image.Dispose();
                    }

                    _imageCache.Clear();
                }

                // Copy so the file is not locked while the Stream Deck profile references it.
                using Image source = Image.FromFile(path);
                Bitmap copy = new(source);
                _imageCache[path] = (stamp, copy);
                return copy;
            }
        }
        catch (Exception ex) when (ex is IOException or OutOfMemoryException or UnauthorizedAccessException
                                       or ArgumentException or ExternalException)
        {
            Log.Warn($"[{nameof(TelemetryKeyRenderer)}] Cannot load image '{path}': {ex.Message}");
            return null;
        }
    }

    private static void DrawCover(Graphics g, Image image)
    {
        float scale = Math.Max((float)Size / image.Width, (float)Size / image.Height);
        float width = image.Width * scale;
        float height = image.Height * scale;
        g.DrawImage(image, (Size - width) / 2f, (Size - height) / 2f, width, height);
    }

    private static void DrawText(Graphics g, string text, Color color, bool overImage)
    {
        string[] lines = text.Split('\n', StringSplitOptions.None);
        if (lines.All(string.IsNullOrWhiteSpace))
        {
            return;
        }

        float available = Size - (Margin * 2);
        float fontSize = FitFontSize(g, lines, available, available);
        using Font font = new(FontName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        float lineHeight = font.GetHeight(g);
        float blockHeight = lineHeight * lines.Length;
        float top = overImage ? Size - Margin - blockHeight : (Size - blockHeight) / 2f;

        using StringFormat format = new(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.None
        };
        using SolidBrush brush = new(color);
        using Pen outline = new(Color.FromArgb(220, 0, 0, 0), Math.Max(2f, fontSize / 6f)) { LineJoin = LineJoin.Round };

        for (int i = 0; i < lines.Length; i++)
        {
            RectangleF box = new(Margin, top + (lineHeight * i), available, lineHeight);
            using GraphicsPath path = new();
            path.AddString(lines[i], font.FontFamily, (int)font.Style, fontSize, box, format);

            if (overImage)
            {
                g.DrawPath(outline, path);
            }

            g.FillPath(brush, path);
        }
    }

    private static float FitFontSize(Graphics g, string[] lines, float maxWidth, float maxHeight)
    {
        float size = MaxFontSize;
        while (size > MinFontSize)
        {
            using Font font = new(FontName, size, FontStyle.Bold, GraphicsUnit.Pixel);
            float widest = lines.Max(l => g.MeasureString(l, font, int.MaxValue, StringFormat.GenericTypographic).Width);
            if (widest <= maxWidth && font.GetHeight(g) * lines.Length <= maxHeight)
            {
                break;
            }

            size -= 1f;
        }

        return size;
    }
}
