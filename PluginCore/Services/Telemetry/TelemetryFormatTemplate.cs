using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SCStreamDeck.Services.Telemetry;

/// <summary>
///     A named, reusable look: colors, image and flash interval. Rules refer to it by <see cref="Id" />, so renaming
///     a template or editing its look updates every rule that uses it.
/// </summary>
public sealed class TelemetryFormatTemplate
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    [JsonProperty("name")] public string? Name { get; set; }

    [JsonProperty("textColor")] public string? TextColor { get; set; }

    [JsonProperty("backgroundColor")] public string? BackgroundColor { get; set; }

    [JsonProperty("imagePath")] public string? ImagePath { get; set; }

    [JsonProperty("flashMs")] public int? FlashMs { get; set; }

    [JsonProperty("flashTarget")] public string? FlashTarget { get; set; }
}

/// <summary>
///     The look a rule ends up with after applying its template.
/// </summary>
public readonly record struct TelemetryRuleAppearance(
    string? TextColor, string? BackgroundColor, string? ImagePath, int? FlashMs, string? FlashTarget = null);

/// <summary>
///     Holds the format templates shared by all telemetry keys (stored in the Stream Deck plugin's global settings)
///     and resolves a rule's look: values set on the rule itself win, the template fills in the rest.
/// </summary>
public sealed class TelemetryTemplateService
{
    public const string GlobalSettingsKey = "formatTemplates";

    private volatile IReadOnlyDictionary<string, TelemetryFormatTemplate> _byId =
        new Dictionary<string, TelemetryFormatTemplate>();

    public event Action? Changed;

    public IReadOnlyCollection<TelemetryFormatTemplate> Templates => _byId.Values.ToList();

    /// <summary>
    ///     Reads the templates from the plugin's global settings; invalid entries are ignored.
    /// </summary>
    public static IReadOnlyList<TelemetryFormatTemplate> Parse(JObject? globalSettings)
    {
        if (globalSettings?[GlobalSettingsKey] is not JArray array)
        {
            return [];
        }

        List<TelemetryFormatTemplate> result = [];
        foreach (JToken token in array)
        {
            try
            {
                if (token.ToObject<TelemetryFormatTemplate>() is { Id.Length: > 0 } template)
                {
                    result.Add(template);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidCastException or FormatException)
            {
                // Skip a malformed entry rather than losing all templates.
            }
        }

        return result;
    }

    /// <summary>
    ///     Replaces the known templates and notifies listeners when something changed.
    /// </summary>
    public void Update(IEnumerable<TelemetryFormatTemplate>? templates)
    {
        Dictionary<string, TelemetryFormatTemplate> next = new(StringComparer.Ordinal);
        foreach (TelemetryFormatTemplate template in templates ?? [])
        {
            next[template.Id] = template;
        }

        IReadOnlyDictionary<string, TelemetryFormatTemplate> previous = _byId;
        _byId = next;

        if (!SameTemplates(previous, next))
        {
            Changed?.Invoke();
        }
    }

    public TelemetryRuleAppearance Resolve(TelemetryRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        TelemetryFormatTemplate? template =
            !string.IsNullOrWhiteSpace(rule.TemplateId) && _byId.TryGetValue(rule.TemplateId, out TelemetryFormatTemplate? found)
                ? found
                : null;

        return new TelemetryRuleAppearance(
            FirstNonEmpty(rule.TextColor, template?.TextColor),
            FirstNonEmpty(rule.BackgroundColor, template?.BackgroundColor),
            FirstNonEmpty(rule.ImagePath, template?.ImagePath),
            rule.FlashMs is > 0 ? rule.FlashMs : template?.FlashMs,
            FirstNonEmpty(rule.FlashTarget, template?.FlashTarget));
    }

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : string.IsNullOrWhiteSpace(second) ? null : second;

    private static bool SameTemplates(
        IReadOnlyDictionary<string, TelemetryFormatTemplate> a, Dictionary<string, TelemetryFormatTemplate> b) =>
        a.Count == b.Count && a.All(pair =>
            b.TryGetValue(pair.Key, out TelemetryFormatTemplate? other) &&
            pair.Value.TextColor == other.TextColor && pair.Value.BackgroundColor == other.BackgroundColor &&
            pair.Value.ImagePath == other.ImagePath && pair.Value.FlashMs == other.FlashMs &&
            pair.Value.FlashTarget == other.FlashTarget);
}
