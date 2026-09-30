using OttoStash.APIs.Compatibility;
using OttoStash.APIs.MUC;
using Object = UnityEngine.Object;

namespace OttoStash.Util;

public class Functions
{
    public static void LogContainerStatus(Container container)
    {
        try
        {
            LogIfBuildDebug($"Container {container.name} at {container.transform.position} is {(container.m_nview.IsOwner() ? "owned" : "not owned")} and has {(container.GetInventory()?.NrOfItems() ?? 0)} items.");
        }
        catch
        {
            // Literally don't give a fuck if this fails. But try anyways.
        }
    }

    internal static float GetContainerRange(Container container)
    {
        if (ContainerRules.Rules == null)
        {
            OttoStashLogger.LogError("The container rules are not loaded, so no container range can be read.");
            return -1f;
        }

        if (container.GetInventory() == null)
            return -1f;

        // Try to get container settings from YAML configuration
        string containerName = MiscFunctions.GetPrefabName(container.transform.root.name);
        if (ContainerRules.Rules.TryGetValue(containerName, out object containerData))
        {
            if (containerData is Dictionary<object, object> containerInfo)
            {
                if (containerInfo.TryGetValue("range", out object rangeObj) && float.TryParse(rangeObj.ToString(), out float range))
                {
                    return range;
                }
            }
            else
            {
                OttoStashLogger.LogError($"Unable to cast containerData for container '{containerName}' to Dictionary<object, object>.");
                return -1f;
            }
        }


        return FallbackRange.Value;
    }

    private static readonly Dictionary<ZDOID, float> LastOwnAttempt = new();

    internal static void CheckItemDropInstanceAndStore(ItemDrop itemDrop)
    {
        if (Boxes.Containers == null || itemDrop == null || itemDrop.transform == null)
            return;


        if (ChestsPickupFromGround.Value.IsOff()) return;
        ZNetView? nview = itemDrop.m_nview;
        if (!nview || !nview.IsValid()) return;
        // Check if the itemdrop is a Fish and if it's not out of water before trying to store it.
        if (!itemDrop.m_itemData.m_dropPrefab) return;
        if (itemDrop.m_itemData.m_dropPrefab.TryGetComponent<Fish>(out Fish? fish))
        {
            if (!fish.IsOutOfWater() && !FishSuction.Value.IsOn()) return;
        }

        ZDO? zdo = nview.GetZDO();
        if (zdo == null) return;

        if (!nview.IsOwner())
        {
            // Only attempt to claim if nobody owns it
            if (nview.HasOwner()) return;
            float now = Time.time;
            if (LastOwnAttempt.TryGetValue(zdo.m_uid, out float last) && !((now - last) >= 0.25f)) return;
            LastOwnAttempt[zdo.m_uid] = now;
            try
            {
                nview.ClaimOwnership();
            }
            catch
            {
                /* ignore */
            }

            return;
        }

        bool anyChanged = false;
        for (int i = 0; i < Boxes.Containers.Count; ++i)
        {
            Container container = Boxes.Containers[i];
            if (!container || !container.transform || container.GetInventory() == null) continue;

            float distance = Vector3.Distance(container.transform.position, itemDrop.transform.position);
            if (distance > GetContainerRange(container)) continue;

            // Pause flag
            if (StorePause.IsPaused(container)) continue;

            if (ChestGate.IsOpenElsewhere(container)) continue;

            LogDebug($"Nearby item name: {itemDrop.m_itemData.m_dropPrefab.name}");

            if (!TryStore(container, ref itemDrop.m_itemData))
                continue;

            anyChanged = true;

            if (itemDrop.m_itemData.m_stack <= 0)
                break;
        }

        if (!anyChanged) return;
        itemDrop.Save();

        if (itemDrop.m_itemData.m_stack > 0) return;
        if (!itemDrop.m_nview)
            Object.DestroyImmediate(itemDrop.gameObject);
        else
            ZNetScene.instance.Destroy(itemDrop.gameObject);
    }

    internal static bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false, bool singleItemData = false)
    {
        bool changed = false;

        LogIfBuildDebug($"Checking container {nearbyContainer?.name}");
        if (!nearbyContainer || !MiscFunctions.CheckItemSharedIntegrity(item))
            return false;

        Inventory? target = nearbyContainer.GetInventory();
        if (target == null) return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && !target.HaveItem(item.m_shared.m_name))
        {
            if (singleItemData)
            {
                LogDebug($"Skipping {item.m_dropPrefab?.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab?.name}] is not in nearby containers</color>");
            }

            return false;
        }

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(nearbyContainer.transform.root.name), item.m_dropPrefab?.name ?? ""))
        {
            if (singleItemData)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab?.name}] cannot be stored based on configuration settings</color>");
            }

            LogDebug($"{item.m_shared.m_name} cannot be stored based on configuration setting");
            return false;
        }

        if (!nearbyContainer.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
        {
            LogDebug($"Cannot store items in {nearbyContainer.name} because the player does not have access.");
            return false;
        }

        if (!MUCCompat.MultiUserChestActive && !nearbyContainer.m_nview.IsOwner())
        {
            LogDebug($"Cannot store items in {nearbyContainer.name} because the player is not the owner.");
            return false;
        }

        int moved = InventoryMove.MoveStackChunked(target, item);
        changed = moved > 0;

        if (!changed) return changed;

        if (!fromPlayer)
            PingContainer(nearbyContainer.gameObject);

        nearbyContainer.Save();
        return changed;
    }


    internal static void TryStore()
    {
        if (!Player.m_localPlayer) return;
        LogDebug("Trying to store items from player inventory");

        List<IContainer> targets = Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value);
        int total = StoreRun.Run(targets, target => target.TryStore(), VanillaChestAccess.Instance, MUCCompat.MultiUserChestActive, LogDebug, LogError);
        StoreSuccess(total);
    }

    internal static void TryStoreThisItem(ItemDrop.ItemData itemData, Inventory m_inventory)
    {
        if (!Player.m_localPlayer) return;
        if (m_inventory != Player.m_localPlayer.GetInventory()) return;

        LogDebug($"Trying to store {itemData.m_shared.m_name}");

        List<IContainer> targets = Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value);
        int total = StoreRun.Run(targets, target => target.TryStoreThisItem(itemData, m_inventory), VanillaChestAccess.Instance, MUCCompat.MultiUserChestActive, LogDebug, LogError);
        StoreSuccess(total);
    }

    internal static void StoreSuccess(int total)
    {
        try
        {
            if (total <= 0) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Stored {total} items from your inventory into nearby containers");

            // Only containers that actually received items are on this list.
            foreach (IContainer c in Boxes.ContainersToPing)
            {
                PingContainer(c.gameObject);
                if (c is VanillaContainers && InventoryGui.instance)
                    InventoryGui.instance.m_moveItemEffects.Create(c.gameObject.transform.position, Quaternion.identity);
            }
        }
        finally
        {
            Boxes.ContainersToPing.Clear();
        }
    }


    internal static void PingContainer(GameObject container)
    {
        if (container == null) return;
        if (PingContainers.Value.IsOn() && container.GetComponent<ChestPingEffect>() == null)
            container.AddComponent<ChestPingEffect>();

        if (HighlightContainers.Value.IsOn() && container.GetComponent<HighLightChest>() == null)
            container.AddComponent<HighLightChest>();
    }


    internal static void LogDebug(string data)
    {
        OttoStashLogger.LogDebug(data);
    }

    internal static void LogIfBuildDebug(string data)
    {
#if DEBUG
        OttoStashPlugin.OttoStashLogger.LogDebug(data);
#endif
    }

    internal static void LogError(string data)
    {
        OttoStashLogger.LogError(data);
    }

    internal static void LogInfo(string data)
    {
        OttoStashLogger.LogInfo(data);
    }

    internal static void LogWarning(string data)
    {
        OttoStashLogger.LogWarning(data);
    }
}