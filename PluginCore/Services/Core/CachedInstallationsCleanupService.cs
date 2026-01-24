using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

public sealed class CachedInstallationsCleanupResult
{
    public required List<SCInstallCandidate> ValidCandidates { get; init; }
    public required bool NeedsFullDetection { get; init; }
    public required bool AnyKeybindingsDeleted { get; init; }
}

public interface ICachedInstallationsCleanupService
{
    Task<CachedInstallationsCleanupResult> CleanupAsync(
        IReadOnlyList<SCInstallCandidate>? cachedCandidates,
        CancellationToken cancellationToken);
}

/// <summary>
///     Validates cached installations and performs cleanup for removed installations.
/// </summary>
public sealed class CachedInstallationsCleanupService(
    StateService stateService,
    IKeybindingsJsonCache keybindingsJsonCache,
    IFileSystem fileSystem) : ICachedInstallationsCleanupService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IKeybindingsJsonCache _keybindingsJsonCache =
        keybindingsJsonCache ?? throw new ArgumentNullException(nameof(keybindingsJsonCache));

    private readonly StateService _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));

    public async Task<CachedInstallationsCleanupResult> CleanupAsync(
        IReadOnlyList<SCInstallCandidate>? cachedCandidates,
        CancellationToken cancellationToken)
    {
        CachedInstallationsValidator.Result result = CachedInstallationsValidator.Validate(
            cachedCandidates,
            _fileSystem.FileExists,
            _fileSystem.DirectoryExists);

        bool anyKeybindingsDeleted = false;
        foreach (SCInstallCandidate cached in result.InvalidCandidates)
        {
            Log.Warn(
                $"[{nameof(CachedInstallationsCleanupService)}] {cached.Channel} installation no longer exists, cleaning up");

            anyKeybindingsDeleted |= _keybindingsJsonCache.TryDelete(cached.Channel);
            await _stateService.RemoveInstallationAsync(cached.Channel, cancellationToken).ConfigureAwait(false);
        }

        return new CachedInstallationsCleanupResult
        {
            ValidCandidates = result.ValidCandidates.ToList(),
            NeedsFullDetection = result.NeedsFullDetection,
            AnyKeybindingsDeleted = anyKeybindingsDeleted
        };
    }
}
