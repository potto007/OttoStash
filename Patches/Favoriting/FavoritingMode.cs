namespace OttoStash.Patches.Favoriting;

internal class FavoritingMode
{
    private static bool hasCurrentlyToggledFavoriting = false;

    internal static bool HasCurrentlyToggledFavoriting
    {
        get => hasCurrentlyToggledFavoriting;
        set { hasCurrentlyToggledFavoriting = value; }
    }

    internal static void RefreshDisplay()
    {
        HasCurrentlyToggledFavoriting |= false;
    }

    internal static bool IsInFavoritingMode()
    {
        return HasCurrentlyToggledFavoriting
               || FavoritingModifierKeybind1.Value.IsKeyHeld()
               || FavoritingModifierKeybind2.Value.IsKeyHeld() || SearchModifierKeybind.Value.IsKeyHeld();
    }
}