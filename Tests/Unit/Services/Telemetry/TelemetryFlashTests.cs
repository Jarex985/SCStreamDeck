using FluentAssertions;
using SCStreamDeck.Services.Telemetry;

namespace Tests.Unit.Services.Telemetry;

public sealed class TelemetryFlashTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(100, TelemetryFlash.MinIntervalMs)]
    [InlineData(1000, 1000)]
    [InlineData(10_000_000, TelemetryFlash.MaxIntervalMs)]
    public void Normalize_ClampsOrDisables(int? input, int expected) =>
        TelemetryFlash.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null, TelemetryFlashTarget.Text)]
    [InlineData("", TelemetryFlashTarget.Text)]
    [InlineData("text", TelemetryFlashTarget.Text)]
    [InlineData("bogus", TelemetryFlashTarget.Text)]
    [InlineData("background", TelemetryFlashTarget.BackgroundAndImage)]
    [InlineData(" Everything ", TelemetryFlashTarget.Everything)]
    public void ParseTarget_DefaultsToText(string? input, TelemetryFlashTarget expected) =>
        TelemetryFlash.ParseTarget(input).Should().Be(expected);

    [Theory]
    [InlineData(999, false, 1)]
    [InlineData(1000, true, 1000)]
    [InlineData(1500, true, 500)]
    [InlineData(2000, false, 1000)]
    [InlineData(3999, true, 1)]
    public void IsHidden_ShowsThenHidesForTheSameDuration(int elapsedMs, bool hidden, int untilSwitchMs)
    {
        bool result = TelemetryFlash.IsHidden(TimeSpan.FromMilliseconds(elapsedMs), 1000, out TimeSpan until);

        result.Should().Be(hidden);
        until.Should().Be(TimeSpan.FromMilliseconds(untilSwitchMs));
    }
}
