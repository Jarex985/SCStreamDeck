using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Services.UI;

namespace Tests.Unit.Services.UI;

public sealed class ThemePayloadBuilderTests
{
    [Fact]
    public void Build_ReturnsEmptySelectedTheme_WhenNoThemes()
    {
        (JArray payload, string selected) = ThemePayloadBuilder.Build(
            Array.Empty<ThemeInfo>(),
            null,
            _ => false);

        payload.Should().BeEmpty();
        selected.Should().BeEmpty();
    }

    [Fact]
    public void Build_FallsBackToFirstTheme_WhenSelectedThemeMissingOrInvalid()
    {
        ThemeInfo[] themes =
        [
            new("alpha.css", "Alpha"),
            new("bravo.css", "Bravo")
        ];

        (JArray payload, string selected) = ThemePayloadBuilder.Build(
            themes,
            "missing.css",
            file => file == "alpha.css" || file == "bravo.css");

        selected.Should().Be("alpha.css");
        payload.Select(p => p["file"]!.Value<string>()).Should().Equal("alpha.css", "bravo.css");
        payload.Select(p => p["name"]!.Value<string>()).Should().Equal("Alpha", "Bravo");
    }

    [Fact]
    public void Build_PreservesValidSelectedTheme()
    {
        ThemeInfo[] themes =
        [
            new("alpha.css", "Alpha"),
            new("bravo.css", "Bravo")
        ];

        (_, string selected) = ThemePayloadBuilder.Build(
            themes,
            "bravo.css",
            file => file == "alpha.css" || file == "bravo.css");

        selected.Should().Be("bravo.css");
    }

    [Fact]
    public void Build_ThemeEntriesHaveStableShape()
    {
        ThemeInfo[] themes =
        [
            new("alpha.css", "Alpha"),
            new("bravo.css", "Bravo")
        ];

        (JArray payload, _) = ThemePayloadBuilder.Build(
            themes,
            null,
            _ => true);

        payload.Should().HaveCount(2);

        foreach (JToken token in payload)
        {
            JObject obj = (JObject)token;
            obj.Properties().Select(p => p.Name).Should().Equal("file", "name");
        }
    }
}
