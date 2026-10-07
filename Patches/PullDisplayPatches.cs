using TMPro;

namespace OttoStash.Patches;

/// Shows the nearby stock in the requirement lists and the build menu.
[HarmonyPatch]
internal static class PullDisplayPatches
{
    private const float BuildCountSeconds = 0.5f;

    private static Piece? _countedPiece;
    private static float _countedAt = float.NegativeInfinity;
    private static int _buildCount;

    // Every requirement line, in the crafting panel and the build HUD, shows
    // have/need. The amount flashes when the containers make up the difference.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    private static void SetupRequirementPostfix(bool __result, Transform elementRoot, Piece.Requirement req, Player player, int quality, int craftMultiplier)
    {
        if (!__result || req?.m_resItem?.m_itemData?.m_shared == null || !Pull.Active(player))
            return;

        TMP_Text? text = elementRoot.Find("res_amount")?.GetComponent<TMP_Text>();
        if (text == null)
            return;

        int need = req.GetAmount(quality) * craftMultiplier;
        if (need <= 0)
            return;

        string name = req.m_resItem.m_itemData.m_shared.m_name;
        int carried = player.GetInventory().CountItems(name);
        int have = carried + Pull.Available(player, name, Pull.PrefabOf(req), -1);

        if (carried < need && have >= need)
            text.color = Mathf.Sin(Time.time * 10f) > 0f ? FlashColor.Value : UnflashColor.Value;

        string format = RequirementFormat.Value;
        if (format.Trim().Length == 0)
            return;

        try
        {
            text.text = string.Format(format, Abbreviate(have), need);
        }
        catch (FormatException)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 16f;
    }

    // The build menu's piece name gets the number of times the player can
    // build it from what they carry and what is nearby.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Hud), nameof(Hud.SetupPieceInfo))]
    private static void SetupPieceInfoPostfix(Hud __instance, Piece piece)
    {
        Player player = Player.m_localPlayer;
        if (piece == null || piece.m_name == "$piece_repair" || !Pull.Active(player))
            return;

        int builds = BuildCount(player, piece);
        string color = ColorUtility.ToHtmlStringRGBA(builds > 0 ? CanBuildColor.Value : CannotBuildColor.Value);
        string count = builds == int.MaxValue ? "∞" : builds.ToString();
        __instance.m_buildSelection.text = $"{Localization.instance.Localize(piece.m_name)} (<color=#{color}>{count}</color>)";
    }

    private static int BuildCount(Player player, Piece piece)
    {
        if (piece == _countedPiece && Time.time - _countedAt < BuildCountSeconds)
            return _buildCount;

        int builds = int.MaxValue;
        Inventory inventory = player.GetInventory();
        foreach (Piece.Requirement requirement in piece.m_resources)
        {
            if (requirement?.m_resItem == null || requirement.m_amount <= 0 || requirement.m_resItem.m_itemData?.m_shared == null)
                continue;

            string name = requirement.m_resItem.m_itemData.m_shared.m_name;
            int have = inventory.CountItems(name) + Pull.Available(player, name, Pull.PrefabOf(requirement), -1);
            builds = Math.Min(builds, have / requirement.m_amount);
            if (builds == 0)
                break;
        }

        _countedPiece = piece;
        _countedAt = Time.time;
        _buildCount = builds;
        return builds;
    }

    internal static string Abbreviate(int number)
    {
        if (number < 1000)
            return number.ToString();
        if (number < 1_000_000)
            return (number / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "K";
        return (number / 1_000_000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
    }
}
