namespace SCStreamDeck.Models;

/// <summary>
///     Parameter object for keybinding execution to avoid primitive obsession.
/// </summary>
public sealed class KeybindingExecutionContext
{
    public required string ActionName { get; init; }
    public required string Binding { get; init; }
    public required ActivationMode ActivationMode { get; init; }
    public required bool IsKeyDown { get; init; }

    public bool IsValid(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(ActionName))
        {
            errorMessage = "ActionName cannot be null or whitespace";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Binding))
        {
            errorMessage = "Binding cannot be null or whitespace";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
