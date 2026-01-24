using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Core keybinding service that orchestrates loading, parsing, and execution.
///     Uses composition of specialized services for SRP.
/// </summary>
public sealed class KeybindingService(
    KeybindingLoaderService loaderService,
    KeybindingExecutorService executorService)
    : IDisposable
{
    private readonly KeybindingExecutorService _executorService =
        executorService ?? throw new ArgumentNullException(nameof(executorService));

    private readonly KeybindingLoaderService _loaderService =
        loaderService ?? throw new ArgumentNullException(nameof(loaderService));

    private bool _disposed;

    public bool IsLoaded => _loaderService.IsLoaded;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _executorService.Dispose();

        _disposed = true;
    }

    public async Task<bool> LoadKeybindingsAsync(string jsonPath, CancellationToken cancellationToken = default) =>
        await _loaderService.LoadKeybindingsAsync(jsonPath, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ExecuteAsync(KeybindingExecutionContext context, CancellationToken cancellationToken = default) =>
        await _executorService.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

    public bool TryGetAction(string? actionName, out KeybindingAction? action) =>
        _loaderService.TryGetAction(actionName, out action);

    public IReadOnlyList<KeybindingAction> GetAllActions() => _loaderService.GetAllActions();

    public IntPtr GetKeyboardLayoutId() => _loaderService.GetKeyboardLayoutId();
}
