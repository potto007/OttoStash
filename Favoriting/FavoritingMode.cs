namespace OttoStash.Favoriting;

/// Whether clicks in the inventory grid favorite things right now, either
/// because a modifier key is held or because the mode was toggled on.
internal static class FavoritingMode
{
    internal static bool Toggled;

    internal static bool IsActive()
    {
        return Toggled
               || FavoritingModifierKeybind1.Value.IsKeyHeld()
               || FavoritingModifierKeybind2.Value.IsKeyHeld()
               || SearchModifierKeybind.Value.IsKeyHeld();
    }
}
