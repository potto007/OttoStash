using System.Reflection.Emit;

namespace OttoStash.APIs.MUC.MUCSrc.Patches.Compatibility;

public static class QuickStackPatch {
    // Without QuickStack the string target resolves to nothing, and one unresolved
    // target aborts the whole PatchAll.
    private static bool Prepare() => AccessTools.TypeByName("QuickStack.QuickStackPlugin, QuickStack") != null;

    [HarmonyPatch("QuickStack.QuickStackPlugin, QuickStack", "StackToMany"), HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> QuickStackPatchTranspiler(IEnumerable<CodeInstruction> instructions) {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch[] {
                new CodeMatch(OpCodes.Ldloc_S),
                new CodeMatch(i => i.IsVirtCall(nameof(ZNetView), nameof(ZNetView.ClaimOwnership)))
            })
            .RemoveInstructions(9)
            .InstructionEnumeration();
    }
}