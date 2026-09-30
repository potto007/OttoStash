namespace OttoStash.APIs.MUC;

internal static class MUCCompat
{
    internal static bool DoNotPatch => Chainloader.PluginInfos.ContainsKey("com.maxsch.valheim.MultiUserChest") 
                                       || Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.valheim.quick_stack") 
                                       || Chainloader.PluginInfos.ContainsKey("aedenthorn.SimpleSort") 
                                       || Chainloader.PluginInfos.ContainsKey("aedenthorn.QuickStore");
    internal static bool MUCLoaded => Chainloader.PluginInfos.ContainsKey("com.maxsch.valheim.MultiUserChest");
    internal static bool MultiUserChestActive => _forceOn || MUCLoaded;

    private static bool _forceOn = false;
    internal static void ForceEnableMUC(bool enabled) => _forceOn = enabled;
}