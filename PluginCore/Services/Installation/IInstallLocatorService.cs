using SCStreamDeck.SCCore.Models;

// ReSharper disable UnusedMember.Global

namespace SCStreamDeck.SCCore.Services.Installation;

/// <summary>
///     Service for detecting Star Citizen installations.
/// </summary>
public interface IInstallLocatorService
{
    /// <summary>
    ///     Finds all Star Citizen installation candidates asynchronously.
    ///     Results are cached until InvalidateCache() is called.
    /// </summary>
    Task<IReadOnlyList<SCInstallCandidate>> FindInstallationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates the installation cache, forcing next FindInstallationsAsync to re-scan.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    ///     Gets the cached installations if available without re-scanning.
    /// </summary>
    IReadOnlyList<SCInstallCandidate>? GetCachedInstallations();

    /// <summary>
    ///     Gets the currently selected Star Citizen installation.
    /// </summary>
    SCInstallCandidate? GetSelectedInstallation();

    /// <summary>
    ///     Sets the currently selected Star Citizen installation.
    /// </summary>
    void SetSelectedInstallation(SCInstallCandidate installation);
}
