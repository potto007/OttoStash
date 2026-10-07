namespace OttoStash.Pulling;

/// The player's own on/off switch for pulling, kept on the character so it
/// survives a logout. The status effect shows while pulling is off.
internal static class PullSwitch
{
    /// The key AzuCraftyBoxes used, so a character that had pulling switched off
    /// there keeps it off here. "0" is off; anything else, or nothing, is on.
    internal const string CustomDataKey = "ACB_PreventPulling";

    internal static bool IsAllowed(Player player)
    {
        return !player.m_customData.TryGetValue(CustomDataKey, out string? value) || value != "0";
    }

    internal static void Toggle(Player player)
    {
        bool allowed = !IsAllowed(player);
        player.m_customData[CustomDataKey] = allowed ? "1" : "0";
        ApplyStatusEffect(player);

        if (PullToggleMessage.Value.IsOff() || Chat.instance == null)
            return;

        string state = allowed ? "<color=green>On</color>" : "<color=red>Off</color>";
        string text;
        try
        {
            text = string.Format(PullToggleMessageFormat.Value, "Pull from chests", state);
        }
        catch (FormatException)
        {
            text = $"Pull from chests: {state}";
        }

        Chat.instance.AddInworldText(player.gameObject, player.GetPlayerID(), player.GetHeadPoint(), Talker.Type.Normal, UserInfo.GetLocalUser(), Localization.instance.Localize(text));
    }

    internal static void ApplyStatusEffect(Player player)
    {
        StatusEffect? effect = PullStatusEffect.Effect;
        if (effect == null || player.m_seman == null)
            return;

        if (!IsAllowed(player) && PullOffStatusEffect.Value.IsOn())
            player.m_seman.AddStatusEffect(effect);
        else
            player.m_seman.RemoveStatusEffect(effect);
    }
}
