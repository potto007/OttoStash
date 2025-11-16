namespace AzuAutoStore.APIs.MUC.MUCSrc.Patches;

[HarmonyPatch]
public static class HumanoidPatch {
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake)), HarmonyPostfix]
    public static void HumanoidAwakePatch(Humanoid __instance) {
        if (MUCCompat.DoNotPatch) return;
        __instance.gameObject.AddComponent<HumanoidExtend>();
    }
}