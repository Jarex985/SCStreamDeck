using BarRaider.SdTools;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding.ActivationHandlers;

/// <summary>
///     Registry and factory for activation mode handlers.
///     Implements the Strategy pattern for activation mode execution.
/// </summary>
internal sealed class ActivationModeHandlerRegistry
{
    private readonly IActivationModeHandler _defaultHandler;
    private readonly Dictionary<string, IActivationModeHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public ActivationModeHandlerRegistry()
    {
        IActivationModeHandler[] handlers =
            new IActivationModeHandler[] { new ImmediatePressHandler(), new DelayedPressHandler(), new HoldHandler() };

        foreach (IActivationModeHandler handler in handlers)
        foreach (string mode in handler.SupportedModes)
        {
            _handlers[mode] = handler;
        }

        // Default to press handler for unknown modes
        _defaultHandler = new ImmediatePressHandler();
    }

    /// <summary>
    ///     Gets the appropriate handler for the specified activation mode.
    /// </summary>
    public IActivationModeHandler GetHandler(ActivationMode mode)
    {
        string modeName = mode.ToString();

        if (_handlers.TryGetValue(modeName, out IActivationModeHandler? handler))
        {
            return handler;
        }

        Logger.Instance.LogMessage(TracingLevel.WARN,
            $"[ActivationRegistry] Unknown activation mode '{modeName}', using default press handler");

        return _defaultHandler;
    }

    /// <summary>
    ///     Executes the appropriate activation mode handler for the given context.
    /// </summary>
    public bool Execute(
        ActivationExecutionContext context,
        ActivationModeMetadata metadata,
        IInputExecutor executor)
    {
        IActivationModeHandler handler = GetHandler(context.Mode);
        return handler.Execute(context, metadata, executor);
    }
}
