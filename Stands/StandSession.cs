namespace OttoStash.Stands;

/// The armor stand the local player has open in the inventory screen.
///
/// The panel shows a mirror Inventory, one cell per stand slot, so the vanilla grid
/// does the drawing, dragging, tooltips and gamepad work. The stand's ZDO stays the
/// truth: every change to the mirror is written to the ZDO in the same call, and a
/// change in the ZDO made elsewhere reloads the slot. No item moves unless this
/// client owns the ZDO, so nothing can leave an inventory before it can be written.
internal sealed class StandSession
{
    private const float OwnershipRequestInterval = 0.5f;

    internal static StandSession? Current { get; private set; }

    internal readonly ArmorStand Stand;
    internal readonly Inventory Mirror;

    private readonly ZNetView _view;
    private readonly Vector2i[] _cells;
    private readonly int[] _slotByCell;
    private readonly int[] _lastHash;
    private readonly ItemDrop.ItemData?[] _lastItem;
    // A slot holding a prefab this client does not know keeps whatever it holds.
    private readonly bool[] _unknown;
    private bool _loading;
    private float _nextOwnershipRequest;

    private StandSession(ArmorStand stand)
    {
        Stand = stand;
        _view = stand.m_nview;

        List<VisSlot> visSlots = stand.m_slots.Select(slot => slot.m_slot).ToList();
        _cells = StandLayout.Place(visSlots, out int height);
        _slotByCell = StandLayout.SlotByCell(_cells, height);
        _lastHash = new int[_cells.Length];
        _lastItem = new ItemDrop.ItemData?[_cells.Length];
        _unknown = new bool[_cells.Length];

        Mirror = new Inventory(stand.m_name, null, StandLayout.Width, height);
        LoadAll();
        Mirror.m_onChanged = OnMirrorChanged;
    }

    internal static void Open(ArmorStand stand)
    {
        Close();
        Current = new StandSession(stand);
        Current.RequestOwnership();
        InventoryGui.instance.Show(null);
    }

    internal static void Close()
    {
        if (Current == null)
            return;
        Current.Mirror.m_onChanged = null;
        Current = null;
    }

    internal string Name => Localization.instance.Localize(Stand.m_name);

    internal bool IsValid => Stand != null && _view != null && _view.IsValid();

    internal bool IsOwner => IsValid && _view.IsOwner();

    /// The slot index at a grid cell, or -1 for a cell no slot claims.
    internal int SlotAt(Vector2i pos)
    {
        if (pos.x < 0 || pos.x >= StandLayout.Width || pos.y < 0)
            return -1;
        int index = pos.y * StandLayout.Width + pos.x;
        return index < _slotByCell.Length ? _slotByCell[index] : -1;
    }

    internal int SlotAtCellIndex(int index)
    {
        return index >= 0 && index < _slotByCell.Length ? _slotByCell[index] : -1;
    }

    internal string SlotLabel(int slot)
    {
        return StandRules.Label(Stand.m_slots[slot].m_supportedTypes);
    }

    /// Whether a move may happen now. Tells the player why not.
    internal bool CanMove()
    {
        if (!IsValid)
            return false;
        if (IsOwner)
            return true;
        RequestOwnership();
        Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Waiting for the stand");
        return false;
    }

    /// Whether one item may land in the cell: a known slot, one item, right type.
    internal bool AcceptsAt(Vector2i pos, ItemDrop.ItemData item, int amount)
    {
        int slot = SlotAt(pos);
        if (slot < 0 || _unknown[slot])
            return false;
        if (amount != 1)
            return false;
        return StandRules.Accepts(Stand.m_slots[slot], item);
    }

    /// Puts the item in the first empty slot that takes it. Returns false when none does.
    internal bool TryQuickLoad(ItemDrop.ItemData item, Inventory from)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            Vector2i cell = _cells[i];
            if (Mirror.GetItemAt(cell.x, cell.y) != null)
                continue;
            if (!AcceptsAt(cell, item, 1))
                continue;
            return Mirror.MoveItemToThis(from, item, 1, cell.x, cell.y);
        }
        return false;
    }

    /// Moves every item on the stand into the player's inventory, as far as it fits.
    internal void TakeAll(Inventory playerInventory)
    {
        foreach (ItemDrop.ItemData item in Mirror.GetAllItems().ToList())
            playerInventory.MoveItemToThis(Mirror, item);
    }

    /// Once a frame while the panel is open.
    internal void Tick()
    {
        if (!IsValid)
            return;
        if (!IsOwner && Time.time >= _nextOwnershipRequest)
            RequestOwnership();

        // A hotbar attach or a take by someone else shows up as a changed hash.
        ZDO zdo = _view.GetZDO();
        for (int i = 0; i < _cells.Length; i++)
        {
            if (zdo.GetInt(ArmorStand.s_itemKeyHashes[i]) != _lastHash[i])
                LoadSlot(i);
        }
    }

    private void RequestOwnership()
    {
        if (!IsValid || IsOwner)
            return;
        _nextOwnershipRequest = Time.time + OwnershipRequestInterval;
        if (_view.GetZDO().GetOwner() == 0L)
            _view.ClaimOwnership();
        else
            _view.InvokeRPC("RPC_RequestOwn");
    }

    private void LoadAll()
    {
        for (int i = 0; i < _cells.Length; i++)
            LoadSlot(i);
    }

    private void LoadSlot(int slot)
    {
        _loading = true;
        try
        {
            ZDO zdo = _view.GetZDO();
            Vector2i cell = _cells[slot];
            ItemDrop.ItemData? shown = Mirror.GetItemAt(cell.x, cell.y);
            if (shown != null)
                Mirror.RemoveItem(shown);

            int hash = zdo.GetInt(ArmorStand.s_itemKeyHashes[slot]);
            _lastHash[slot] = hash;
            _lastItem[slot] = null;
            _unknown[slot] = false;
            if (hash == 0)
                return;

            GameObject? prefab = ObjectDB.instance.GetItemPrefab(hash);
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                StashLog.Warning($"Armor stand slot {slot} holds an item this game does not know ({hash}). Leaving it alone.");
                _unknown[slot] = true;
                return;
            }

            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            ItemDrop.LoadFromZDO(item, zdo, slot);
            item.m_stack = 1;
            item.m_equipped = false;
            if (!Mirror.AddItem(item, cell))
            {
                StashLog.Error($"Armor stand slot {slot} could not be shown at {cell.x},{cell.y}.");
                _unknown[slot] = true;
                return;
            }
            _lastItem[slot] = item;
        }
        finally
        {
            _loading = false;
        }
    }

    // Writes the mirror to the ZDO, slot by slot. Idempotent, so the partial states
    // of a vanilla swap are fine: each step writes what the mirror holds at that moment.
    private void OnMirrorChanged()
    {
        if (_loading || !IsValid)
            return;
        if (!IsOwner)
        {
            // Moves are gated on ownership, so this is a bug or a mid-frame loss.
            StashLog.Error("The armor stand changed while this client did not own it. The stand keeps what it had.");
            LoadAll();
            return;
        }

        ZDO zdo = _view.GetZDO();
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_unknown[i])
                continue;

            Vector2i cell = _cells[i];
            ItemDrop.ItemData? item = Mirror.GetItemAt(cell.x, cell.y);
            int current = zdo.GetInt(ArmorStand.s_itemKeyHashes[i]);

            if (item == null)
            {
                if (current != 0)
                    ClearSlot(zdo, i);
                _lastItem[i] = null;
                continue;
            }

            int hash = PrefabHash(item);
            if (hash == 0)
                continue;
            if (hash != current || !ReferenceEquals(item, _lastItem[i]))
                WriteSlot(zdo, i, item, hash);
        }
    }

    private void WriteSlot(ZDO zdo, int slot, ItemDrop.ItemData item, int hash)
    {
        zdo.Set(ArmorStand.s_itemKeyHashes[slot], hash);
        zdo.Set(ArmorStand.s_variantKeyHashes[slot], item.m_variant);
        ItemDrop.SaveToZDO(item, zdo, slot);
        _view.InvokeRPC(ZNetView.Everybody, "RPC_SetVisualItem", slot, hash, item.m_variant);
        Stand.m_effects.Create(Stand.transform.position, Quaternion.identity);
        Game.instance.IncrementPlayerStat(PlayerStatType.ArmorStandUses);
        _lastHash[slot] = hash;
        _lastItem[slot] = item;
    }

    private void ClearSlot(ZDO zdo, int slot)
    {
        zdo.Set(ArmorStand.s_itemKeyHashes[slot], 0);
        _view.InvokeRPC(ZNetView.Everybody, "RPC_SetVisualItem", slot, 0, 0);
        Stand.m_destroyEffects.Create(Stand.m_dropSpawnPoint.position, Quaternion.identity);
        Stand.UpdateSupports();
        _lastHash[slot] = 0;
    }

    private static int PrefabHash(ItemDrop.ItemData item)
    {
        if (item.m_dropPrefab != null)
            return item.m_dropPrefab.name.GetStableHashCode();
        if (ObjectDB.instance != null && ObjectDB.instance.TryGetItemPrefab(item.m_shared, out GameObject prefab))
        {
            item.m_dropPrefab = prefab;
            return prefab.name.GetStableHashCode();
        }
        StashLog.Error($"{item.m_shared.m_name} has no prefab, so it cannot be put on the stand.");
        return 0;
    }
}
