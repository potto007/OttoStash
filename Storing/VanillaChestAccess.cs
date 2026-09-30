namespace OttoStash.Storing;

/// The engine side of IChestAccess, for the game's own Container component.
internal sealed class VanillaChestAccess : IChestAccess
{
    internal static readonly VanillaChestAccess Instance = new();

    public bool IsChest(IContainer target)
    {
        return target is VanillaContainers;
    }

    public bool IsOpenElsewhere(IContainer chest)
    {
        return ChestGate.IsOpenElsewhere(ChestOf(chest));
    }

    public bool TakeOwnership(IContainer chest)
    {
        ZNetView? view = chest.m_nview;
        if (view == null || !view.IsValid())
            return false;
        if (!view.IsOwner())
            view.ClaimOwnership();
        return view.IsOwner();
    }

    public IDisposable Hold(IContainer chest)
    {
        return new InUseHold(ChestOf(chest));
    }

    private static Container? ChestOf(IContainer target)
    {
        return target.gameObject != null ? target.gameObject.GetComponent<Container>() : null;
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
