using System.Collections.Concurrent;
using BarRaider.SdTools;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;
using WindowsInput;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for executing keybinding actions.
/// </summary>
public sealed class KeybindingExecutorService : IKeybindingExecutorService, IDisposable
{
    private readonly ConcurrentDictionary<string, Timer> _activationTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ActivationModeHandlerRegistry _handlerRegistry;
    private readonly ConcurrentDictionary<string, byte> _holdStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IInputExecutor _inputExecutor;
    private readonly IInputSimulator _inputSimulator;
    private readonly IKeybindingLoaderService _loaderService;
    private readonly IKeybindingParserService _parserService;
    private bool _disposed;

    public KeybindingExecutorService(
        IKeybindingLoaderService loaderService,
        IKeybindingParserService parserService,
        IInputSimulator? inputSimulator = null)
    {
        _loaderService = loaderService ?? throw new ArgumentNullException(nameof(loaderService));
        _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
        _inputSimulator = inputSimulator ?? new InputSimulator();

        _handlerRegistry = new ActivationModeHandlerRegistry();
        _inputExecutor = new KeybindingInputExecutor(_inputSimulator, _holdStates, _activationTimers);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Collect all timers first
        Timer[] timers = _activationTimers.Values.ToArray();

        // Clear the dictionary
        _activationTimers.Clear();

        // Dispose all timers
        foreach (Timer timer in timers)
        {
            timer.Dispose();
        }

        _holdStates.Clear();
        _disposed = true;
    }

    public async Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsValid(out string? errorMessage))
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(KeybindingExecutorService)}] Invalid execution context - {errorMessage}");

            return false;
        }

        try
        {
            return await Task.Run(() => ExecuteWithActivationMode(context, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR,
                $"[{nameof(KeybindingExecutorService)}] {ErrorMessages.OperationFailedFor} '{context.ActionName}': {ex.Message}");

            return false;
        }
    }

    /// <summary>
    ///     Executes an action using the Strategy pattern via ActivationModeHandlerRegistry.
    ///     Each activation mode (press, hold, tap, etc.) has its own handler.
    /// </summary>
    private bool ExecuteWithActivationMode(KeybindingExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ParsedInputResult? parsedInput = _parserService.ParseBinding(context.Binding);
        if (parsedInput == null)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN,
                $"[{nameof(KeybindingExecutorService)}] Failed to parse binding '{context.Binding}'");

            return false;
        }

        // Get activation mode metadata for the specific action
        ActivationModeMetadata metadata = _loaderService.GetMetadata(context.ActionName) ?? ActivationModeMetadata.Empty();

        ActivationExecutionContext executionContext = new()
        {
            ActionName = context.ActionName,
            Input = new ParsedInput { Type = parsedInput.Type, Value = parsedInput.Value },
            IsKeyDown = context.IsKeyDown,
            Mode = context.ActivationMode,
            Metadata = metadata
        };

        return _handlerRegistry.Execute(executionContext, _inputExecutor);
    }
}
