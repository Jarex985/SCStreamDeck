using BarRaider.SdTools;
using Microsoft.Extensions.DependencyInjection;
using SCStreamDeck.SCCore.Infrastructure;

namespace SCStreamDeck;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        // Uncomment this line of code to allow for debugging
        //while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }
        
        // Initialize SCCore DI container & Pre-initialize services before StreamDeck connection
        var serviceProvider = ServiceConfiguration.BuildAndInitialize();
        var initService = serviceProvider.GetRequiredService<SCCore.Services.Core.IInitializationService>();
        
        try 
        {
            var result = await initService.EnsureInitializedAsync();
            
            if (!result.IsSuccess) 
                Logger.Instance.LogMessage(TracingLevel.WARN,$"[Program] Pre-initialization failed: {result.ErrorMessage}");
        }
        catch (Exception ex) 
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,$"[Program] Pre-initialization exception: {ex.Message}");
        }
        SDWrapper.Run(args);
    }
}