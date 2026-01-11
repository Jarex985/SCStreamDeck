using Microsoft.Extensions.DependencyInjection;

namespace SCStreamDeck.Infrastructure;

/// <summary>
///     Service locator for StreamDeck plugin dependency injection.
///     Required because StreamDeck-Tools doesn't support constructor injection for plugin actions.
/// </summary>
public static class ServiceLocator
{
    private static volatile IServiceProvider? s_serviceProvider;
    private static readonly object s_lock = new();

    /// <summary>
    ///     Initializes the service provider with registered services.
    ///     Must be called before any plugin actions are instantiated.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        lock (s_lock)
        {
            s_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
    }

    /// <summary>
    ///     Gets a service instance from the service provider.
    /// </summary>
    /// <typeparam name="T">The service type to retrieve</typeparam>
    public static T GetService<T>() where T : notnull
    {
        lock (s_lock)
        {
            return s_serviceProvider == null
                ? throw new InvalidOperationException("ServiceLocator is not initialized.")
                : s_serviceProvider.GetRequiredService<T>();
        }
    }
}
