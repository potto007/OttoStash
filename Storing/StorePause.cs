namespace OttoStash.Storing;

/// The pause switch that stops chests pulling items from the ground. The local
/// toggle is sent to every registered chest as a routed RPC, and the chest's
/// owner records it on the ZDO so every client sees the same state.
internal static class StorePause
{
    internal const string RpcName = "RequestPause";
    internal static readonly int PausedHash = "storingPaused".GetStableHashCode();

    internal static bool Paused;

    /// Registers the pause handler exactly once per network view. Awake can run
    /// more than once for the same object, and the routed RPC table refuses a
    /// second registration under the same name, so any earlier handler is
    /// dropped before the new one goes in.
    internal static void RegisterHandler(ZNetView view, Container container)
    {
        view.Unregister(RpcName);
        view.Register<bool>(RpcName, (sender, pause) => OnRequestPause(sender, pause, container));
    }

    /// Flips the local switch and tells every registered chest about it.
    internal static void Toggle()
    {
        Paused = !Paused;
        foreach (Container container in ContainerRegistry.Containers)
        {
            if (container.m_nview == null || !container.m_nview.IsValid())
                continue;
            container.m_nview.InvokeRPC(RpcName, Paused);
        }
    }

    internal static bool IsPaused(Container container)
    {
        return container.m_nview.GetZDO().GetBool(PausedHash, false);
    }

    private static void OnRequestPause(long sender, bool pause, Container container)
    {
        if (!container.m_nview.IsValid())
            return;
        if (container.m_nview.IsOwner())
            container.m_nview.GetZDO().Set(PausedHash, pause);
    }
}
