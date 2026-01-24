using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Abstraction for installation discovery and selection.
///     Used by core orchestration (InitializationService) and implemented by InstallLocatorService.
/// </summary>
public interface IInstallLocatorService
{
    Task<IReadOnlyList<SCInstallCandidate>> FindInstallationsAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
    IReadOnlyList<SCInstallCandidate>? GetCachedInstallations();
    SCInstallCandidate? GetSelectedInstallation();
    void SetSelectedInstallation(SCInstallCandidate installation);
}
