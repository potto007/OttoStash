namespace OttoStash.Storing;

/// Anything nearby that can take items from the player: a chest, a drawer, a backpack.
internal interface IStoreTarget
{
    /// Stores everything eligible from the player's inventory and returns the number moved.
    int StoreAll();

    /// Stores one item out of the given inventory and returns the number moved.
    int StoreItem(ItemDrop.ItemData item, Inventory playerInventory);

    bool IsOwner();

    GameObject GameObject { get; }

    ZNetView NetView { get; }
}
