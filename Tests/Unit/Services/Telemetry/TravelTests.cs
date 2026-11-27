using System.Globalization;
using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TravelTests
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

    private static TravelTracker NewTracker() => new(new TelemetryService(new IdleSource()));

    private static TelemetrySnapshot Speed(double value) => new(new Dictionary<string, IReadOnlyDictionary<string, TelemetryValue>>
    {
        ["vehicle"] = new Dictionary<string, TelemetryValue>
        {
            [TravelTracker.SpeedPath] = new(TelemetryValueKind.Number, value)
        }
    });

    private static T WithInvariantCulture<T>(Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Tracker_IsIdleUntilStarted()
    {
        using TravelTracker tracker = NewTracker();

        tracker.Read(s_t0).Should().Be(new TravelReading(TravelState.Idle, TimeSpan.Zero, 0));
        tracker.Observe(100, s_t0.AddSeconds(1));
        tracker.Read(s_t0.AddSeconds(1)).Meters.Should().Be(0);
    }

    [Fact]
    public void Tracker_IntegratesSpeedWithTheTrapezoidRule()
    {
        using TravelTracker tracker = NewTracker();

        tracker.Toggle(s_t0, 0);
        tracker.Observe(100, s_t0.AddSeconds(2)); // (0 + 100) / 2 * 2 s
        tracker.Observe(100, s_t0.AddSeconds(3)); // 100 * 1 s

        TravelReading reading = tracker.Read(s_t0.AddSeconds(3));
        reading.State.Should().Be(TravelState.Running);
        reading.Elapsed.Should().Be(TimeSpan.FromSeconds(3));
        reading.Meters.Should().BeApproximately(200, 1e-9);
    }

    [Fact]
    public void Tracker_FreezesWhenStoppedAndRestartsFromZero()
    {
        using TravelTracker tracker = NewTracker();

        tracker.Toggle(s_t0, 50);
        tracker.Observe(50, s_t0.AddSeconds(5));
        tracker.Observe(50, s_t0.AddSeconds(10));
        TravelReading stopped = tracker.Toggle(s_t0.AddSeconds(15), 50); // closes the last stretch

        stopped.State.Should().Be(TravelState.Stopped);
        stopped.Meters.Should().BeApproximately(750, 1e-9);

        tracker.Observe(500, s_t0.AddSeconds(30));
        TravelReading later = tracker.Read(s_t0.AddMinutes(5));
        later.Elapsed.Should().Be(TimeSpan.FromSeconds(15));
        later.Meters.Should().BeApproximately(750, 1e-9);

        TravelReading restarted = tracker.Toggle(s_t0.AddMinutes(6), 0);
        restarted.Should().Be(new TravelReading(TravelState.Running, TimeSpan.Zero, 0));
    }

    [Fact]
    public void Tracker_DoesNotBridgeMissingSpeedOrLongGaps()
    {
        using TravelTracker tracker = NewTracker();

        tracker.Toggle(s_t0, 100);
        tracker.Observe(null, s_t0.AddSeconds(1)); // left the seat: stretch ends
        tracker.Observe(100, s_t0.AddSeconds(2)); // new reference, nothing added
        tracker.Observe(100, s_t0.AddSeconds(2) + TravelTracker.MaxSampleGap + TimeSpan.FromSeconds(1)); // too far apart
        tracker.Observe(100, s_t0.AddSeconds(2) + TravelTracker.MaxSampleGap + TimeSpan.FromSeconds(2)); // 100 m

        tracker.Read(s_t0.AddMinutes(1)).Meters.Should().BeApproximately(100, 1e-9);
    }

    [Fact]
    public void Tracker_ReadsTheSpeedFromTheTelemetryWhenToggled()
    {
        TelemetryService telemetry = new(new IdleSource());
        using TravelTracker tracker = new(telemetry);
        telemetry.ApplyFrame(Speed(80), s_t0);

        tracker.Toggle(s_t0);
        telemetry.ApplyFrame(Speed(80), s_t0.AddSeconds(5));
        TravelReading stopped = tracker.Toggle(s_t0.AddSeconds(5));

        stopped.Meters.Should().BeApproximately(400, 1e-9);
    }

    [Theory]
    [InlineData(0, "0.0 min")]
    [InlineData(2, "0.0 min")]
    [InlineData(3, "0.1 min")]
    [InlineData(204, "3.4 min")]
    [InlineData(3600, "60.0 min")]
    public void FormatMinutes_ShowsMinutesAndTenths(double seconds, string expected) =>
        WithInvariantCulture(() => TelemetryTextFormatter.FormatMinutes(TimeSpan.FromSeconds(seconds)))
            .Should().Be(expected);

    [Theory]
    [InlineData(0, "0 m")]
    [InlineData(850.4, "850 m")]
    [InlineData(999.4, "999 m")]
    [InlineData(999.5, "1.00 km")]
    [InlineData(1000, "1.00 km")]
    [InlineData(1256, "1.26 km")]
    [InlineData(25_340, "25.34 km")]
    public void FormatDistance_SwitchesToKilometersFromOneKilometer(double meters, string expected) =>
        WithInvariantCulture(() => TelemetryTextFormatter.FormatDistance(meters)).Should().Be(expected);

    [Fact]
    public void Evaluators_ShowStateAndMeasurementAndToggleOnPress()
    {
        using TravelTracker tracker = NewTracker();
        TravelToggleEvaluator timer = new(tracker);
        TravelDisplayEvaluator display = new(tracker);
        TelemetryDisplaySettings settings = new();

        TelemetryEvaluation Evaluate(ITelemetryKeyEvaluator evaluator, TimeSpan at) =>
            evaluator.Evaluate(settings, null, TelemetrySchema.Bundled, s_t0 + at);

        Evaluate(timer, TimeSpan.Zero).Text.Should().Be("Start");
        Evaluate(display, TimeSpan.Zero).Text.Should().Be($"{TelemetryTextFormatter.NoDataText}\n{TelemetryTextFormatter.NoDataText}");

        tracker.Toggle(s_t0, 100);
        for (int second = 5; second <= 30; second += 5)
        {
            tracker.Observe(100, s_t0.AddSeconds(second));
        }

        Evaluate(timer, TimeSpan.FromSeconds(30)).Text.Should().Be("Stop");
        WithInvariantCulture(() => Evaluate(display, TimeSpan.FromSeconds(30)).Text).Should().Be("0.5 min\n3.00 km");

        tracker.Toggle(s_t0.AddSeconds(30), 100);
        Evaluate(timer, TimeSpan.FromMinutes(5)).Text.Should().Be("Start");
        WithInvariantCulture(() => Evaluate(display, TimeSpan.FromMinutes(5)).Text).Should().Be("0.5 min\n3.00 km");
    }

    [Fact]
    public void Timer_UsesTheStartStopAndUnavailableLooks()
    {
        using TravelTracker tracker = NewTracker();
        TravelToggleEvaluator timer = new(tracker);
        TelemetryDisplaySettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TelemetryDisplaySettings>(
            """
            {"rules":[{"conditions":[{"fieldPath":"vehicle.state.flight.instruments.speed","op":"gt","value":"10"}],
                       "text":"MOVING","soundPath":"u.wav"}],
             "startRule":{"text":"GO","textColor":"#00FF00"},
             "stopRule":{"text":"HALT {elapsed}","soundPath":"s.wav"}}
            """)!;

        TelemetryEvaluation At(TelemetrySnapshot? snapshot, double seconds) =>
            timer.Evaluate(settings, snapshot, TelemetrySchema.Bundled, s_t0.AddSeconds(seconds));

        TelemetryEvaluation start = At(Speed(0), 0);
        start.Text.Should().Be("GO");
        start.Disabled.Should().BeFalse();
        start.Match.Rule.TextColor.Should().Be("#00FF00");

        tracker.Toggle(s_t0, 0);
        TelemetryEvaluation stop = At(Speed(0), 30);
        stop.Text.Should().StartWith("HALT ");
        stop.Disabled.Should().BeFalse();
        stop.Match.Rule.SoundPath.Should().Be("s.wav");
        stop.Match.Index.Should().NotBe(start.Match.Index);

        TelemetryEvaluation unavailable = At(Speed(50), 30);
        unavailable.Text.Should().Be("MOVING");
        unavailable.Disabled.Should().BeTrue();
        unavailable.Match.Index.Should().Be(0);
    }

    [Fact]
    public void Timer_UnavailableLookDefaultsToUnavailableText()
    {
        using TravelTracker tracker = NewTracker();
        TelemetryDisplaySettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TelemetryDisplaySettings>(
            """{"rules":[{"conditions":[{"fieldPath":"travel.state","op":"eq","value":"IDLE"}]}]}""")!;

        TelemetryEvaluation result = new TravelToggleEvaluator(tracker)
            .Evaluate(settings, null, TelemetrySchema.Bundled, s_t0);

        result.Disabled.Should().BeTrue();
        result.Text.Should().Be("Unavailable");
    }

    [Fact]
    public void Evaluators_RulesCanTestTheStateAndTheMeasurement()
    {
        using TravelTracker tracker = NewTracker();
        TravelDisplayEvaluator display = new(tracker);
        TelemetryDisplaySettings settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TelemetryDisplaySettings>(
            """
            {"rules":[{"conditions":[{"fieldPath":"travel.state","op":"eq","value":"RUNNING"},
                                      {"fieldPath":"travel.meters","op":"gte","value":"1000"}],"text":"FAR"},
                      {"conditions":[{"fieldPath":"travel.state","op":"eq","value":"RUNNING"}],"text":"GO"}]}
            """)!;

        TelemetryEvaluation At(double seconds) =>
            display.Evaluate(settings, null, TelemetrySchema.Bundled, s_t0.AddSeconds(seconds));

        At(0).Text.Should().NotBe("GO");

        tracker.Toggle(s_t0, 100);
        At(1).Text.Should().Be("GO");

        tracker.Observe(100, s_t0.AddSeconds(4));
        tracker.Observe(100, s_t0.AddSeconds(8));
        tracker.Observe(100, s_t0.AddSeconds(12));
        At(12).Text.Should().Be("FAR");
    }
}
