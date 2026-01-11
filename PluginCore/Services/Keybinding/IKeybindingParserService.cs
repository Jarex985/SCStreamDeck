using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Keybinding;

/// <summary>
///     Service for parsing keybinding strings into executable inputs.
/// </summary>
public interface IKeybindingParserService
{
    /// <summary>
    ///     Parses a binding string into a ParsedInputResult.
    /// </summary>
    ParsedInputResult? ParseBinding(string binding);
}

/// <summary>
///     Result of parsing a binding string.
/// </summary>
public sealed record ParsedInputResult(InputType Type, object Value);
