using OttoStash.APIs.Compatibility.WardIsLove;

namespace OttoStash.Storing;

/// Decides which containers become autostore targets. Every registration path
/// (a container waking up, the player opening one, the scan after the local
/// player spawns) goes through Register so the rules cannot drift apart.
internal static class StoreTargets
{
    internal static void Register(Container container)
    {
        try
        {
            if (IsEligible(container))
                Boxes.AddContainer(container);
        }
        catch (Exception e)
        {
            OttoStashLogger.LogDebug($"Could not evaluate a container for autostore: {e.Message}");
        }
    }

    /// Evaluates every container in the loaded scene. Containers that loaded
    /// before the local player existed never had a player to register against.
    internal static int RegisterLoaded()
    {
        int before = Boxes.Containers.Count;
        foreach (Container container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            Register(container);
        return Boxes.Containers.Count - before;
    }

    /// The rule on plain values, so it can be checked without the engine.
    internal static bool IsEligible(bool liveNetworkObject, bool playerBuilt, bool hasInventory, bool carriedByAnotherCharacter, bool wardAccess)
    {
        return liveNetworkObject && playerBuilt && hasInventory && !carriedByAnotherCharacter && wardAccess;
    }

    internal static bool IsEligible(Container container)
    {
        if (container == null)
            return false;

        ZNetView? view = container.m_nview;
        if (view == null || !view.IsValid())
            return false;

        ZDO? zdo = view.GetZDO();
        if (zdo == null)
            return false;

        return IsEligible(
            liveNetworkObject: true,
            playerBuilt: zdo.GetLong(ZDOVars.s_creator) != 0L,
            hasInventory: container.GetInventory() != null,
            carriedByAnotherCharacter: IsCarriedByAnotherCharacter(container),
            wardAccess: HasWardAccess(container.transform.position));
    }

    /// A container that hangs off another player or a creature belongs to that
    /// character and is never a chest to store into. Claiming its network object
    /// from a second client has broken that player's session.
    internal static bool IsCarriedByAnotherCharacter(Container container)
    {
        Character? carrier = container.GetComponentInParent<Character>();
        if (carrier == null && container.m_nview != null)
            carrier = container.m_nview.GetComponentInParent<Character>();

        return carrier != null && carrier != Player.m_localPlayer;
    }

    private static bool HasWardAccess(Vector3 position)
    {
        if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled()!.Value && WardMonoscript.CheckAccess(position, flash: false, wardCheck: true))
            return true;

        return PrivateArea.CheckAccess(position, flash: false, wardCheck: true);
    }
}
