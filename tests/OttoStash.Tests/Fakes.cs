using OttoStash.Interfaces;
using OttoStash.Storing;
using UnityEngine;

namespace OttoStash.Tests;

/// A store target that records what the run asked of it.
internal sealed class FakeTarget : IContainer
{
    private readonly Func<int> _store;

    internal FakeTarget(string name, bool isChest, bool owned = true, Func<int>? store = null)
    {
        Name = name;
        IsChest = isChest;
        Owned = owned;
        _store = store ?? (() => 0);
    }

    internal string Name { get; }
    internal bool IsChest { get; }
    internal bool Owned { get; set; }
    internal bool OpenElsewhere { get; set; }
    internal bool OwnershipClaimSucceeds { get; set; } = true;
    internal int StoreCalls { get; private set; }

    public int TryStore()
    {
        StoreCalls++;
        return _store();
    }

    public int TryStoreThisItem(ItemDrop.ItemData item, Inventory playerInventory)
    {
        return TryStore();
    }

    public bool IsOwner()
    {
        return Owned;
    }

    public GameObject gameObject => null!;
    public ZNetView m_nview => null!;
}

/// Chest access that never touches the engine and keeps a ledger of every call.
internal sealed class FakeChestAccess : IChestAccess
{
    internal readonly List<string> Ledger = new();
    internal readonly HashSet<FakeTarget> Held = new();

    public bool IsChest(IContainer target)
    {
        return ((FakeTarget)target).IsChest;
    }

    public bool IsOpenElsewhere(IContainer chest)
    {
        FakeTarget target = (FakeTarget)chest;
        Ledger.Add($"gate {target.Name}");
        return target.OpenElsewhere;
    }

    public bool TakeOwnership(IContainer chest)
    {
        FakeTarget target = (FakeTarget)chest;
        Ledger.Add($"claim {target.Name}");
        if (target.OwnershipClaimSucceeds)
            target.Owned = true;
        return target.Owned;
    }

    public IDisposable Hold(IContainer chest)
    {
        FakeTarget target = (FakeTarget)chest;
        Ledger.Add($"hold {target.Name}");
        Held.Add(target);
        return new Release(this, target);
    }

    private sealed class Release : IDisposable
    {
        private readonly FakeChestAccess _access;
        private readonly FakeTarget _target;

        internal Release(FakeChestAccess access, FakeTarget target)
        {
            _access = access;
            _target = target;
        }

        public void Dispose()
        {
            _access.Ledger.Add($"release {_target.Name}");
            _access.Held.Remove(_target);
        }
    }
}

internal static class Items
{
    internal static ItemDrop.ItemData Stack(string name, int stack, int maxStack = 50)
    {
        return new ItemDrop.ItemData
        {
            m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = maxStack },
            m_stack = stack,
        };
    }

    internal static Inventory Chest(int width, int height)
    {
        return new Inventory("chest", null, width, height);
    }
}
