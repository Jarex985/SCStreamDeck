using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class DecelerationProfileTests
{
    private static readonly DateTime s_t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private sealed class IdleSource : ITelemetryStreamSource
    {
        public async IAsyncEnumerable<TelemetrySnapshot> StreamAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            yield break;
        }
    }

    // Constant deceleration from startSpeed to zero, sampled every 0.5 s.
    private static List<DecelerationSample> Run(double startSpeed, double decel, double density)
    {
        List<DecelerationSample> samples = [];
        for (double t = 0; t * decel < startSpeed; t += 0.5)
        {
            samples.Add(new DecelerationSample(s_t0.AddSeconds(t), startSpeed - (decel * t), density));
        }

        samples.Add(new DecelerationSample(s_t0.AddSeconds(startSpeed / decel), 0, density));
        return samples;
    }

    private static DecelerationObservation Obs(double speed, string ship = "ESPR_Talon|", bool boosting = false) =>
        new(ship, speed, 0, boosting, false);

    private static TelemetrySnapshot Snapshot(double speed, double density, string ship = "ESPR_Talon") =>
        new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
        {
            ["vehicle"] = new Dictionary<string, TelemetryValue>
            {
                [DecelerationProfileService.SpeedPath] = new(TelemetryValueKind.Number, speed),
                [DecelerationProfileService.DensityPath] = new(TelemetryValueKind.Number, density),
                [DecelerationProfileService.ClassPath] = new(TelemetryValueKind.Text, Text: ship)
            }
        });

    [Fact]
    public void Profile_IntegratesTheRecordedRateOverTheSpeedRange()
    {
        DecelerationProfile profile = new();
        profile.AddRun(Run(200, 10, 0)).Should().BeTrue();

        ProfileEstimate estimate = profile.Estimate(100, 0)!.Value;

        estimate.TimeSeconds.Should().BeApproximately(10, 0.1); // 100 / 10
        estimate.Units.Should().BeApproximately(500, 5); // 100^2 / (2 * 10)
        estimate.Approximate.Should().BeFalse();
    }

    [Fact]
    public void Profile_BlendsBetweenRecordedDensitiesAndFlagsFarOnes()
    {
        DecelerationProfile profile = new();
        profile.AddRun(Run(100, 10, 0));
        profile.AddRun(Run(100, 20, 1.2));

        ProfileEstimate between = profile.Estimate(100, 0.6)!.Value;
        ProfileEstimate vacuum = profile.Estimate(100, 0)!.Value;
        ProfileEstimate atmosphere = profile.Estimate(100, 1.2)!.Value;

        between.Units.Should().BeInRange(atmosphere.Units, vacuum.Units);
        between.Units.Should().BeApproximately((vacuum.Units + atmosphere.Units) / 2, 1);
        between.Approximate.Should().BeFalse();

        profile.Estimate(100, 5)!.Value.Approximate.Should().BeTrue();
        profile.Estimate(300, 0)!.Value.Approximate.Should().BeTrue();
    }

    [Fact]
    public void Profile_WeighsNewRunsMoreAndSurvivesJson()
    {
        DecelerationProfile profile = new();
        profile.AddRun(Run(100, 10, 0));
        double before = profile.Estimate(100, 0)!.Value.TimeSeconds;
        profile.AddRun(Run(100, 20, 0));

        double after = profile.Estimate(100, 0)!.Value.TimeSeconds;
        after.Should().BeLessThan(before).And.BeGreaterThan(5);

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(profile);
        DecelerationProfile restored = Newtonsoft.Json.JsonConvert.DeserializeObject<DecelerationProfile>(json)!;
        restored.Runs.Should().Be(2);
        restored.Estimate(100, 0)!.Value.TimeSeconds.Should().BeApproximately(after, 1e-6);
    }

    [Fact]
    public void Recorder_SavesARunThatFallsFromSteadySpeedToZero()
    {
        DecelerationRunRecorder recorder = new();
        List<DecelerationSample>? saved = null;
        recorder.RunCompleted += (_, samples) => saved = [.. samples];
        recorder.Arm(s_t0);

        recorder.Observe(s_t0.AddSeconds(1), Obs(100));
        recorder.Observe(s_t0.AddSeconds(2), Obs(100));
        recorder.Status(s_t0.AddSeconds(2)).Phase.Should().Be(DecelerationRecordingPhase.Armed);

        recorder.Observe(s_t0.AddSeconds(3), Obs(95));
        recorder.Status(s_t0.AddSeconds(3)).Phase.Should().Be(DecelerationRecordingPhase.Recording);
        recorder.Observe(s_t0.AddSeconds(5), Obs(50));
        recorder.Observe(s_t0.AddSeconds(8), Obs(0));

        saved.Should().NotBeNull();
        saved![0].Speed.Should().Be(100);
        saved[^1].Speed.Should().Be(0);
        recorder.Status(s_t0.AddSeconds(8)).Should().Match<DecelerationRecordingStatus>(
            s => s.Phase == DecelerationRecordingPhase.Idle && s.Outcome == DecelerationRunOutcome.Saved);
    }

    [Fact]
    public void Recorder_DiscardsRunsThatWereNotASimpleDeceleration()
    {
        DecelerationRunRecorder recorder = new();
        bool completed = false;
        recorder.RunCompleted += (_, _) => completed = true;

        void StartRun()
        {
            recorder.Arm(s_t0);
            recorder.Observe(s_t0.AddSeconds(1), Obs(100));
            recorder.Observe(s_t0.AddSeconds(2), Obs(80));
        }

        StartRun();
        recorder.Observe(s_t0.AddSeconds(3), Obs(150));
        recorder.Status(s_t0.AddSeconds(3)).Outcome.Should().Be(DecelerationRunOutcome.Discarded);

        StartRun();
        recorder.Observe(s_t0.AddSeconds(3), Obs(60, boosting: true));
        recorder.Status(s_t0.AddSeconds(3)).Outcome.Should().Be(DecelerationRunOutcome.Discarded);

        StartRun();
        recorder.Observe(s_t0.AddSeconds(3), Obs(60, ship: "AEGS_Gladius|"));
        recorder.Status(s_t0.AddSeconds(3)).Outcome.Should().Be(DecelerationRunOutcome.Discarded);

        StartRun();
        recorder.Status(s_t0.AddSeconds(2) + DecelerationRunRecorder.StallTimeout + TimeSpan.FromSeconds(1))
            .Outcome.Should().Be(DecelerationRunOutcome.Discarded);

        completed.Should().BeFalse();
    }

    [Fact]
    public void Evaluator_ShowsDashUntilARunIsRecordedThenTheStoppingDistance()
    {
        string path = Path.Combine(Path.GetTempPath(), $"decel-{Guid.NewGuid():N}.json");
        try
        {
            using DecelerationProfileService service = new(new TelemetryService(new IdleSource()), () => path);
            DecelerationEvaluator evaluator = new(service);
            TelemetryDisplaySettings settings = new()
            {
                FieldPath = DecelerationProfileService.SpeedPath,
                Deceleration = new TelemetryDecelerationSettings { Output = "units", Unit = "m", UnitsRounding = 1 }
            };

            evaluator.Evaluate(settings, Snapshot(100, 0), TelemetrySchema.Bundled, s_t0).Text.Should().EndWith("—");

            service.AddRun("ESPR_Talon|", Run(200, 10, 0));
            string text = evaluator.Evaluate(settings, Snapshot(100, 0), TelemetrySchema.Bundled, s_t0).Text;
            text.Should().EndWith(" m").And.NotEndWith("—");
            double.Parse(text[text.LastIndexOf('\n')..^2], System.Globalization.CultureInfo.InvariantCulture)
                .Should().BeApproximately(500, 10);

            evaluator.Evaluate(settings, Snapshot(100, 0, "AEGS_Gladius"), TelemetrySchema.Bundled, s_t0).Text.Should().EndWith("—");

            // A second service reads the saved profile back.
            using DecelerationProfileService reloaded = new(new TelemetryService(new IdleSource()), () => path);
            reloaded.RunCount("ESPR_Talon|").Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Evaluator_PressArmsThenCancelsAndOnlyTheCancelConsumesThePress()
    {
        using DecelerationProfileService service = new(new TelemetryService(new IdleSource()), () => Path.GetTempFileName());
        DecelerationEvaluator evaluator = new(service);
        TelemetryDisplaySettings settings = new() { FieldPath = DecelerationProfileService.SpeedPath };
        evaluator.Evaluate(settings, Snapshot(100, 0), TelemetrySchema.Bundled, DateTime.UtcNow);

        evaluator.OnKeyPressed();
        evaluator.ConsumesPress.Should().BeFalse();
        service.Status(DateTime.UtcNow).Phase.Should().Be(DecelerationRecordingPhase.Armed);
        evaluator.Evaluate(settings, Snapshot(100, 0), TelemetrySchema.Bundled, DateTime.UtcNow).Text.Should().StartWith("Armed");

        evaluator.OnKeyPressed();
        evaluator.ConsumesPress.Should().BeTrue();
        service.Status(DateTime.UtcNow).Phase.Should().Be(DecelerationRecordingPhase.Idle);
    }

    [Fact]
    public void Evaluator_LiveModeKeepsTheOldBehaviorAndIgnoresPresses()
    {
        using DecelerationProfileService service = new(new TelemetryService(new IdleSource()), () => Path.GetTempFileName());
        DecelerationEvaluator evaluator = new(service);
        TelemetryDisplaySettings settings = new()
        {
            FieldPath = DecelerationProfileService.SpeedPath,
            Deceleration = new TelemetryDecelerationSettings { Mode = "live" }
        };
        evaluator.Evaluate(settings, Snapshot(100, 0), TelemetrySchema.Bundled, DateTime.UtcNow);

        evaluator.OnKeyPressed();

        evaluator.ConsumesPress.Should().BeFalse();
        service.Status(DateTime.UtcNow).Phase.Should().Be(DecelerationRecordingPhase.Idle);
    }
}
