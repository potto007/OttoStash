namespace OttoStash.Storing;

/// The engine side of IChestAccess, for the game's own Container component.
internal sealed class VanillaChestAccess : IChestAccess
{
    internal static readonly VanillaChestAccess Instance = new();

    public bool IsChest(IStoreTarget target)
    {
        return target is ChestTarget;
    }

    public bool IsOpenElsewhere(IStoreTarget chest)
    {
        return ChestGate.IsOpenElsewhere(ChestOf(chest));
    }

    public bool TakeOwnership(IStoreTarget chest)
    {
        ZNetView? view = chest.NetView;
        if (view == null || !view.IsValid())
            return false;
        if (!view.IsOwner())
            view.ClaimOwnership();
        return view.IsOwner();
    }

    public IDisposable Hold(IStoreTarget chest)
    {
        return new InUseHold(ChestOf(chest));
    }

    private static Container? ChestOf(IStoreTarget target)
    {
        return (target as ChestTarget)?.Chest;
    }

    /// Raises the chest's own in-use flag, which is what the owner consults when
    /// another player asks to open it, and puts the flag back on dispose.
    private sealed class InUseHold : IDisposable
    {
        private readonly Container? _chest;
        private readonly bool _wasInUse;

        internal InUseHold(Container? chest)
        {
            _chest = chest;
            if (_chest == null)
                return;
            _wasInUse = _chest.m_inUse;
            _chest.m_inUse = true;
        }

        public void Dispose()
        {
            if (_chest != null)
                _chest.m_inUse = _wasInUse;
        }
    }
}
