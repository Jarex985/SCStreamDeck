using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryTemplateServiceTests
{
    private static TelemetryFormatTemplate Warning() => new()
    {
        Id = "t1", Name = "warning", TextColor = "#FFFF00", BackgroundColor = "#AA0000", ImagePath = "warn.png", FlashMs = 500
    };

    [Fact]
    public void Resolve_FillsInWhatTheRuleDoesNotSet()
    {
        TelemetryTemplateService service = new();
        service.Update([Warning()]);

        TelemetryRuleAppearance look = service.Resolve(new TelemetryRule { TemplateId = "t1" });

        look.Should().Be(new TelemetryRuleAppearance("#FFFF00", "#AA0000", "warn.png", 500));
    }

    [Fact]
    public void Resolve_RuleValuesOverrideTheTemplate()
    {
        TelemetryTemplateService service = new();
        service.Update([Warning()]);

        TelemetryRuleAppearance look = service.Resolve(new TelemetryRule
        {
            TemplateId = "t1", TextColor = "#00FF00", FlashMs = 1000, ImagePath = " "
        });

        look.TextColor.Should().Be("#00FF00");
        look.FlashMs.Should().Be(1000);
        look.ImagePath.Should().Be("warn.png"); // blank on the rule means "not set"
        look.BackgroundColor.Should().Be("#AA0000");
    }

    [Fact]
    public void Resolve_WithoutAKnownTemplateUsesTheRuleOnly()
    {
        TelemetryTemplateService service = new();
        TelemetryRule rule = new() { TemplateId = "gone", TextColor = "#123456" };

        service.Resolve(rule).Should().Be(new TelemetryRuleAppearance("#123456", null, null, null));

        service.Update([Warning()]);
        service.Update([]); // deleted again
        service.Resolve(new TelemetryRule { TemplateId = "t1" }).Should().Be(default(TelemetryRuleAppearance));
    }

    [Fact]
    public void Update_NotifiesOnlyWhenTheLookChanges()
    {
        TelemetryTemplateService service = new();
        int changes = 0;
        service.Changed += () => changes++;

        service.Update([Warning()]);
        service.Update([Warning()]); // same content
        TelemetryFormatTemplate edited = Warning();
        edited.TextColor = "#FFFFFF";
        service.Update([edited]);

        changes.Should().Be(2);
    }

    [Fact]
    public void Parse_ReadsTemplatesFromGlobalSettingsAndSkipsInvalidEntries()
    {
        JObject settings = JObject.Parse("""
            {
              "other": 1,
              "formatTemplates": [
                { "id": "a", "name": "warning", "textColor": "#FF0000", "flashMs": 750 },
                { "name": "no id" },
                { "id": "b", "flashMs": "not a number" },
                { "id": "c", "name": "plain" }
              ]
            }
            """);

        IReadOnlyList<TelemetryFormatTemplate> templates = TelemetryTemplateService.Parse(settings);

        templates.Select(t => t.Id).Should().Equal("a", "c");
        templates[0].FlashMs.Should().Be(750);
        TelemetryTemplateService.Parse(null).Should().BeEmpty();
        TelemetryTemplateService.Parse(new JObject()).Should().BeEmpty();
    }
}
