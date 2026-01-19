using SCStreamDeck.Models;

// ReSharper disable UnusedMember.Global

namespace SCStreamDeck.Services.Installation;

/// <summary>
///     Service for detecting Star Citizen installations.
/// </summary>
public interface IInstallLocatorService
{
    /// <summary>
    ///     Finds all Star Citizen installation candidates asynchronously.
    ///     Results are cached until InvalidateCache() is called.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of installation candidates.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
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
    /// <param name="installation">The installation to select.</param>
    /// <exception cref="ArgumentNullException">Thrown when installation is null.</exception>
    void SetSelectedInstallation(SCInstallCandidate installation);
}
