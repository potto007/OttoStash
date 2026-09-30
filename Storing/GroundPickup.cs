using Object = UnityEngine.Object;

namespace OttoStash.Storing;

/// Pulls dropped items off the ground into the chests in range of them, on a
/// timer that the interval setting controls. One instance lives for the whole
/// game session.
internal sealed class GroundPickup : MonoBehaviour
{
    private const float ClaimRetrySeconds = 0.25f;

    private static readonly Dictionary<ZDOID, float> LastClaimAttempt = new();

    private void Awake()
    {
        InvokeRepeating(nameof(Tick), IntervalSeconds.Value, IntervalSeconds.Value);
        IntervalSeconds.SettingChanged += OnIntervalChanged;
    }

    private void OnDestroy()
    {
        IntervalSeconds.SettingChanged -= OnIntervalChanged;
    }

    private void OnIntervalChanged(object sender, EventArgs e)
    {
        CancelInvoke(nameof(Tick));
        InvokeRepeating(nameof(Tick), IntervalSeconds.Value, IntervalSeconds.Value);
    }

    private void Tick()
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.IsTeleporting() || player.IsDead())
            return;

        if (ContainerRegistry.Containers.Count == 0)
            return;

        foreach (ItemDrop itemDrop in ItemDrop.s_instances.Where(drop => !drop.IsPiece()).ToList())
        {
            if (itemDrop == null || itemDrop.transform == null || itemDrop.m_nview == null || !itemDrop.m_nview.IsValid())
                continue;

            TryStoreDrop(itemDrop);
        }
    }

    /// Stores one dropped item into the chests whose pickup range covers it.
    /// A drop nobody owns is claimed first and stored on a later tick.
    internal static void TryStoreDrop(ItemDrop itemDrop)
    {
        if (itemDrop == null || itemDrop.transform == null)
            return;
        if (ChestsPickupFromGround.Value.IsOff())
            return;

        ZNetView? view = itemDrop.m_nview;
        if (view == null || !view.IsValid())
            return;

        if (itemDrop.m_itemData.m_dropPrefab == null)
            return;

        // A fish still in the water stays there unless netting is on.
        if (itemDrop.m_itemData.m_dropPrefab.TryGetComponent(out Fish fish) && !fish.IsOutOfWater() && !FishSuction.Value.IsOn())
            return;

        ZDO? zdo = view.GetZDO();
        if (zdo == null)
            return;

        if (!view.IsOwner())
        {
            ClaimIfUnowned(view, zdo);
            return;
        }

        bool anyChanged = false;
        foreach (Container chest in ContainerRegistry.Containers)
        {
            if (chest == null || chest.transform == null || chest.GetInventory() == null)
                continue;

            if (Vector3.Distance(chest.transform.position, itemDrop.transform.position) > ChestStore.RangeOf(chest))
                continue;

            if (StorePause.IsPaused(chest))
                continue;

            if (ChestGate.IsOpenElsewhere(chest))
                continue;

            StashLog.Debug($"Nearby item name: {itemDrop.m_itemData.m_dropPrefab.name}");

            if (!ChestStore.TryStore(chest, ref itemDrop.m_itemData, fromPlayer: false, announce: false))
                continue;

            anyChanged = true;
            if (itemDrop.m_itemData.m_stack <= 0)
                break;
        }

        if (!anyChanged)
            return;

        itemDrop.Save();

        if (itemDrop.m_itemData.m_stack > 0)
            return;

        if (itemDrop.m_nview == null)
            Object.DestroyImmediate(itemDrop.gameObject);
        else
            ZNetScene.instance.Destroy(itemDrop.gameObject);
    }

    // Only an unowned drop is claimed, and not more often than every quarter second.
    private static void ClaimIfUnowned(ZNetView view, ZDO zdo)
    {
        if (view.HasOwner())
            return;

        float now = Time.time;
        if (LastClaimAttempt.TryGetValue(zdo.m_uid, out float last) && now - last < ClaimRetrySeconds)
            return;

        LastClaimAttempt[zdo.m_uid] = now;
        try
        {
            view.ClaimOwnership();
        }
        catch
        {
            // A claim that fails is retried on a later tick.
        }
    }
}
