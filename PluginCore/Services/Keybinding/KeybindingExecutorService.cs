using System.Collections.Concurrent;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;
using SCStreamDeck.Services.Keybinding.ActivationHandlers;
using WindowsInput;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for executing keybinding actions.
/// </summary>
public sealed class KeybindingExecutorService : IDisposable
{
    private readonly ConcurrentDictionary<string, Timer> _activationTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ActivationModeHandlerRegistry _handlerRegistry;
    private readonly ConcurrentDictionary<string, byte> _holdStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IInputExecutor _inputExecutor;
    private readonly KeybindingLoaderService _loaderService;
    private bool _disposed;

    public KeybindingExecutorService(
        KeybindingLoaderService loaderService,
        IInputSimulator inputSimulator)
    {
        _loaderService = loaderService ?? throw new ArgumentNullException(nameof(loaderService));
        ArgumentNullException.ThrowIfNull(inputSimulator);

        _handlerRegistry = new ActivationModeHandlerRegistry();
        _inputExecutor = new KeybindingInputExecutor(inputSimulator, _holdStates, _activationTimers);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Timer[] timers = _activationTimers.Values.ToArray();

        _activationTimers.Clear();

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
            Log.Warn($"[{nameof(KeybindingExecutorService)}] Invalid execution context - {errorMessage}");

            return false;
        }

        try
        {
            return await Task.Run(() => ExecuteWithActivationMode(context, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Log.Err(
                $"[{nameof(KeybindingExecutorService)}] Operation failed for '{context.ActionName}'",
                ex);

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

        ParsedInputResult? parsedInput = KeybindingParserService.ParseBinding(context.Binding);
        if (parsedInput == null)
        {
            Log.Warn($"[{nameof(KeybindingExecutorService)}] Failed to parse binding '{context.Binding}'");

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
