using SCStreamDeck.Models;

namespace SCStreamDeck.Common;

internal static class KeybindingActionResolver
{
    /// <summary>
    ///     Returns the keyboard binding, or the mouse binding when it is a button/wheel; null if nothing executable.
    /// </summary>
    public static string? GetExecutableBinding(KeybindingAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!string.IsNullOrWhiteSpace(action.KeyboardBinding))
        {
            return action.KeyboardBinding;
        }

        if (string.IsNullOrWhiteSpace(action.MouseBinding))
        {
            return null;
        }

        InputType bindingType = action.MouseBinding.GetInputType();
        return bindingType is InputType.MouseButton or InputType.MouseWheel ? action.MouseBinding : null;
    }
}
