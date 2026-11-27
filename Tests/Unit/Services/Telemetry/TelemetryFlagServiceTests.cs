using FluentAssertions;
using Newtonsoft.Json.Linq;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryFlagServiceTests
{
    private static TelemetryFlag Flag(string id, bool start = false) => new() { Id = id, Name = id, DefaultValue = start };

    private static TelemetryFlagService WithFlags(params TelemetryFlag[] flags)
    {
        TelemetryFlagService service = new();
        service.Update(flags);
        return service;
    }

    [Fact]
    public void Update_StartsNewFlagsAtTheirDefaultAndKeepsKnownValues()
    {
        TelemetryFlagService service = WithFlags(Flag("warn"), Flag("preflight", true));

        service.IsSet("warn").Should().BeFalse();
        service.IsSet("preflight").Should().BeTrue();

        service.Set("warn", true);
        service.Update([Flag("warn"), Flag("other", true)]);

        service.IsSet("warn").Should().BeTrue("a known flag keeps its value");
        service.IsSet("other").Should().BeTrue();
        service.IsSet("preflight").Should().BeFalse("a deleted flag is gone");
    }

    [Fact]
    public void SetAndToggle_ChangeOnlyKnownFlagsAndNotifyOnChange()
    {
        TelemetryFlagService service = WithFlags(Flag("warn"));
        int changes = 0;
        service.Changed += () => changes++;

        service.Set("warn", true).Should().BeTrue();
        service.Set("warn", true).Should().BeFalse("nothing changed");
        service.Toggle("warn").Should().BeTrue();
        service.Set("missing", true).Should().BeFalse();
        service.Toggle(null).Should().BeFalse();

        service.IsSet("warn").Should().BeFalse();
        changes.Should().Be(2);
    }

    [Fact]
    public void Apply_RuleAssignmentsAndPressActions()
    {
        TelemetryFlagService service = WithFlags(Flag("a"), Flag("b", true));

        service.Apply([new TelemetryFlagAssignment { Flag = "a", Value = true }, new TelemetryFlagAssignment { Flag = "b", Value = false }])
            .Should().BeTrue();
        service.IsSet("a").Should().BeTrue();
        service.IsSet("b").Should().BeFalse();

        service.Apply(new TelemetryPressAction { Flag = "a", FlagMode = TelemetryFlagMode.Off });
        service.IsSet("a").Should().BeFalse();
        service.Apply(new TelemetryPressAction { Flag = "a", FlagMode = TelemetryFlagMode.On });
        service.IsSet("a").Should().BeTrue();
        service.Apply(new TelemetryPressAction { Flag = "a" }); // toggle is the default
        service.IsSet("a").Should().BeFalse();
    }

    [Fact]
    public void Overlay_LetsRulesTestFlagsAsBooleanAttributes()
    {
        TelemetryFlagService service = WithFlags(Flag("warn"));
        TelemetryDisplaySettings settings = new()
        {
            FieldPath = null,
            Rules =
            [
                new TelemetryRule
                {
                    Conditions = [new TelemetryCondition { FieldPath = TelemetryFlagService.PathFor("warn"), Op = "eq", Value = "true" }],
                    Text = "MASTER WARN"
                }
            ],
            DefaultRule = new TelemetryRule { Text = "ok" }
        };

        TelemetryRuleMatch Evaluate(TelemetrySnapshot? snapshot) =>
            TelemetryRuleEvaluator.EvaluateWithResolver(settings,
                path => service.Overlay(snapshot) is { } s && s.TryGetValue(path, out TelemetryValue v) ? v : null);

        Evaluate(null).Rule.Text.Should().Be("ok");
        Evaluate(null).HasData.Should().BeFalse("flags do not count as telemetry data");

        service.Set("warn", true);
        TelemetryRuleMatch match = Evaluate(null);
        match.Rule.Text.Should().Be("MASTER WARN");
        match.HasData.Should().BeTrue();
    }

    [Fact]
    public void Overlay_KeepsTheTelemetryParts()
    {
        TelemetryFlagService service = WithFlags(Flag("warn"));
        TelemetrySnapshot snapshot = new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
        {
            ["vehicle"] = new Dictionary<string, TelemetryValue>
            {
                ["vehicle.speed"] = new(TelemetryValueKind.Number, 5)
            }
        });

        TelemetrySnapshot? overlaid = service.Overlay(snapshot);

        overlaid!.TryGetValue("vehicle.speed", out TelemetryValue speed).Should().BeTrue();
        speed.Number.Should().Be(5);
        overlaid.TryGetValue("flag.warn", out TelemetryValue flag).Should().BeTrue();
        flag.Boolean.Should().BeFalse();
        new TelemetryFlagService().Overlay(null).Should().BeNull("without flags there is nothing to add");
    }

    [Fact]
    public void Parse_ReadsFlagsAndSkipsInvalidEntries()
    {
        JObject settings = JObject.Parse("""
            { "flags": [ { "id": "a", "name": "Master warn", "defaultValue": true }, { "name": "no id" }, { "id": "b", "defaultValue": "x" } ] }
            """);

        IReadOnlyList<TelemetryFlag> flags = TelemetryFlagService.Parse(settings);

        flags.Select(f => f.Id).Should().Equal("a");
        flags[0].DefaultValue.Should().BeTrue();
    }

    [Fact]
    public void ApplyGlobalSettings_RestoresFlagsStreamDeckLost()
    {
        string path = Path.Combine(Path.GetTempPath(), $"flags-{Guid.NewGuid():N}", TelemetryFlagService.BackupFileName);
        try
        {
            new TelemetryFlagService(() => path)
                .ApplyGlobalSettings(JObject.Parse("""{ "flags": [ { "id": "a", "name": "Preflight" } ] }""")).Should().BeNull();

            TelemetryFlagService reinstalled = new(() => path);
            JObject? toWrite = reinstalled.ApplyGlobalSettings(new JObject());

            TelemetryFlagService.Parse(toWrite).Select(f => f.Id).Should().Equal("a");
            reinstalled.Flags.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void GlobalSettings_RestoresBothListsInOneWriteBack()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"lists-{Guid.NewGuid():N}");
        string templatesPath = Path.Combine(folder, "templates.json");
        string flagsPath = Path.Combine(folder, "flags.json");
        try
        {
            TelemetryGlobalSettings.Apply(new TelemetryTemplateService(() => templatesPath), new TelemetryFlagService(() => flagsPath),
                JObject.Parse("""{ "formatTemplates": [ { "id": "t" } ], "flags": [ { "id": "f" } ] }"""));

            JObject? toWrite = TelemetryGlobalSettings.Apply(
                new TelemetryTemplateService(() => templatesPath), new TelemetryFlagService(() => flagsPath), new JObject());

            TelemetryTemplateService.Parse(toWrite).Select(t => t.Id).Should().Equal("t");
            TelemetryFlagService.Parse(toWrite).Select(f => f.Id).Should().Equal("f");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void GetPressActions_KeepsFlagActionsWithoutAFunction()
    {
        TelemetryRule rule = new()
        {
            Actions =
            [
                new TelemetryPressAction { Flag = "a", FlagMode = TelemetryFlagMode.Toggle },
                new TelemetryPressAction { Flag = "" },
                new TelemetryPressAction { Function = "v_toggle_lights" }
            ]
        };

        rule.GetPressActions().Select(a => a.IsFlagAction).Should().Equal(true, false);
    }

    [Fact]
    public void Tracker_ReportsWhenARuleBecomesActiveIncludingTheDefaultRule()
    {
        TelemetryRuleTransitionTracker tracker = new();
        TelemetryRuleMatch Match(int index, bool hasData = true) => new(new TelemetryRule(), index, hasData);

        tracker.OnEvaluated(Match(-1, false), out bool entered);
        entered.Should().BeFalse("the first evaluation only records the state");

        tracker.OnEvaluated(Match(0), out entered);
        entered.Should().BeTrue();
        tracker.OnEvaluated(Match(0), out entered);
        entered.Should().BeFalse();
        tracker.OnEvaluated(Match(-1), out entered);
        entered.Should().BeTrue();
        tracker.OnEvaluated(Match(1, false), out entered);
        entered.Should().BeFalse("a rule without data does not count");
    }
}
