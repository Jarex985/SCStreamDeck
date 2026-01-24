using BarRaider.SdTools;
using SCStreamDeck.Infrastructure;
using SCStreamDeck.Logging;
using SCStreamDeck.Services.Core;

namespace SCStreamDeck;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Log.Initialize((level, message) =>
        {
            TracingLevel tracingLevel = level switch
            {
                Log.Level.Debug => TracingLevel.DEBUG,
                Log.Level.Info => TracingLevel.INFO,
                Log.Level.Warn => TracingLevel.WARN,
                Log.Level.Error => TracingLevel.ERROR,
                _ => TracingLevel.INFO
            };

            Logger.Instance.LogMessage(tracingLevel, message);
        });

        // Uncomment this line of code to allow for debugging
        //while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

        // Initialize DI container & pre-initialize services before StreamDeck connection
        ServiceConfiguration.BuildAndInitialize();
        InitializationService initService = ServiceLocator.GetService<InitializationService>();

        try
        {
            InitializationResult result = await initService.EnsureInitializedAsync();

            if (!result.IsSuccess)
            {
                Log.Warn($"[Program] Pre-initialization failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log.Err($"[Program] Pre-initialization exception: {ex.Message}", ex);
        }

        SDWrapper.Run(args);
    }
}
