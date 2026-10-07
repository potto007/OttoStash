namespace OttoStash.Pulling;

/// The status effect icon that shows while the player has pulling switched off.
internal static class PullStatusEffect
{
    internal static StatusEffect? Effect;

    internal static void Create()
    {
        StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
        effect.name = "OttoStash_PullingOff";
        effect.m_name = "Not pulling from chests";
        effect.m_tooltip = "Crafting and building use only what you carry. Press TogglePullingShortcut to pull from nearby chests again.";
        effect.m_icon = LoadSprite("pullingicon.png");
        effect.m_startMessageType = MessageHud.MessageType.TopLeft;
        effect.m_startMessage = "";
        effect.m_stopMessageType = MessageHud.MessageType.TopLeft;
        effect.m_stopMessage = "";
        Effect = effect;
    }

    internal static void Register(ObjectDB objectDb)
    {
        if (Effect == null || objectDb.m_StatusEffects.Contains(Effect))
            return;
        objectDb.m_StatusEffects.Add(Effect);
        objectDb.UpdateRegisters();
    }
}
