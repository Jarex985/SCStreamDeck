using Grpc.Core;
using Sc.Game.Telemetry.V1;

// Fake Star Citizen telemetry server for developing/testing the Telemetry Display key without the game.
// Usage: dotnet run --project Tools/MockTelemetryServer [-- --port 2942]
int port = 2942;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int parsed))
    {
        port = parsed;
    }
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(port, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2));
builder.Services.AddGrpc();

WebApplication app = builder.Build();
app.MapGrpcService<MockTelemetryService>();
Console.WriteLine($"Mock telemetry server on http://127.0.0.1:{port} (Ctrl+C to stop)");
Console.WriteLine("Pressure sweeps 0-120 kPa every 24 s, health 100->0 every 40 s, locomotion cycles every 10 s.");
Console.WriteLine("Vehicle part (speed) is sent for 25 s, then omitted for 15 s, like leaving a ship seat.");
app.Run();

internal sealed class MockTelemetryService : GameTelemetryService.GameTelemetryServiceBase
{
    private const int FramesPerSecond = 17;

    public override async Task TelemetryStream(
        TelemetryStreamRequest request, IServerStreamWriter<TelemetryStreamResponse> responseStream, ServerCallContext context)
    {
        DateTime start = DateTime.UtcNow;
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1.0 / FramesPerSecond));

        while (await timer.WaitForNextTickAsync(context.CancellationToken))
        {
            double t = (DateTime.UtcNow - start).TotalSeconds;
            double pressure = 60 + (60 * Math.Sin(t * 2 * Math.PI / 24));
            double healthPct = 100 * (1 - ((t % 40) / 40));

            await responseStream.WriteAsync(new TelemetryStreamResponse
            {
                Actor = new ActorFrame
                {
                    Vitals = new ActorVitals
                    {
                        Pressure = (float)Math.Max(0, pressure),
                        Health = new Gauge { Current = (float)healthPct, Max = 100 },
                        Oxygen = new Gauge { Current = 90, Max = 100 },
                        HeartRate = 70 + (int)(t % 30)
                    },
                    State = new ActorState
                    {
                        Locomotion = (ActorLocomotionState)(1 + ((int)(t / 10) % 3)),
                        UecBalance = 123456
                    },
                    Environment = new ActorEnvironment { BreathableAtmosphere = ((int)(t / 5) % 2) == 0 }
                },
                // Like the game, the vehicle part is only sent while seated (25 s seated, 15 s on foot).
                Vehicle = (t % 40) < 25
                    ? new VehicleFrame
                    {
                        State = new VehicleState
                        {
                            Name = "Mock Ship",
                            Flight = new VehicleFlight
                            {
                                Instruments = new VehicleFlightInstruments { Speed = (float)(100 + (50 * Math.Sin(t))) }
                            }
                        }
                    }
                    : null
            });
        }
    }

    public override Task<ListTriggerableActionsResponse> ListTriggerableActions(
        ListTriggerableActionsRequest request, ServerCallContext context) =>
        Task.FromResult(new ListTriggerableActionsResponse());

    public override Task<TriggerGameActionResponse> TriggerGameAction(
        TriggerGameActionRequest request, ServerCallContext context) =>
        Task.FromResult(new TriggerGameActionResponse { Status = TriggerGameActionStatus.UnknownAction });
}
