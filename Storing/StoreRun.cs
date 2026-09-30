namespace OttoStash.Storing;

/// What a store run needs from vanilla chests. The run never touches the engine
/// through anything else, so it can be exercised without one.
internal interface IChestAccess
{
    /// True for a vanilla chest, the only kind of target that needs network
    /// ownership and an in-use hold.
    bool IsChest(IStoreTarget target);

    /// True when another player has the chest open right now.
    bool IsOpenElsewhere(IStoreTarget chest);

    /// Claims network ownership and reports whether this client holds it afterwards.
    bool TakeOwnership(IStoreTarget chest);

    /// Marks the chest in use until the handle is disposed.
    IDisposable Hold(IStoreTarget chest);
}

/// One pass over the nearby targets, in the calling frame. A local ownership
/// claim takes effect at once, so nothing waits on the network.
internal static class StoreRun
{
    /// Stores into every target and returns the number of items moved.
    internal static int Run(IEnumerable<IStoreTarget> targets, Func<IStoreTarget, int> store, IChestAccess chests, bool multiUserChestActive, Action<string> debug, Action<string> error)
    {
        int total = 0;
        foreach (IStoreTarget target in targets)
            total += StoreInto(target, store, chests, multiUserChestActive, debug, error);
        return total;
    }

    private static int StoreInto(IStoreTarget target, Func<IStoreTarget, int> store, IChestAccess chests, bool multiUserChestActive, Action<string> debug, Action<string> error)
    {
        // Drawers and backpacks arbitrate their own access; only touch the ones this client owns.
        if (!chests.IsChest(target))
            return target.IsOwner() ? store(target) : 0;

        // MultiUserChest handles concurrent access itself.
        if (multiUserChestActive)
            return store(target);

        // Checked before any ownership claim, so a chest someone else is using is never taken over.
        if (chests.IsOpenElsewhere(target))
        {
            debug($"Skipping {Name(target)}: another player has it open.");
            return 0;
        }

        if (!chests.TakeOwnership(target))
        {
            debug($"Skipping {Name(target)}: could not take ownership.");
            return 0;
        }

        // Held for the duration of the store so an open request from another player
        // is refused meanwhile, and released whatever happens inside.
        using (chests.Hold(target))
        {
            try
            {
                return store(target);
            }
            catch (Exception e)
            {
                error($"Error while storing to {Name(target)}: {e}");
                return 0;
            }
        }
    }

    private static string Name(IStoreTarget target)
    {
        return target.GameObject != null ? target.GameObject.name : "container";
    }
}
