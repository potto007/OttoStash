using System.Collections;
using AzuAutoStore.APIs.Compatibility;
using AzuAutoStore.APIs.MUC;
using AzuAutoStore.Patches;
using Object = UnityEngine.Object;

namespace AzuAutoStore.Util;

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

    internal static IEnumerator WaitForOwnershipAndStore(VanillaContainers vc, float timeoutSeconds = 1f)
    {
        if (vc == null || vc.m_nview == null || !vc.m_nview.IsValid())
        {
            if (--InProgressStores == 0) StoreSuccess(InProgressTotal);
            yield break;
        }

        if (!MUCCompat.MultiUserChestActive)
        {
            if (!vc.m_nview.IsOwner())
                vc.m_nview.ClaimOwnership();

            float t = 0f;
            while (!vc.m_nview.IsOwner() && t < timeoutSeconds)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (!vc.m_nview.IsOwner())
            {
                LogDebug($"Ownership claim timed out for {vc.gameObject.name}.");
                if (--InProgressStores == 0) StoreSuccess(InProgressTotal);
                yield break;
            }
        }

        try
        {
            int moved = vc.TryStore();
            InProgressTotal += moved;

            Container? c = vc.gameObject.GetComponent<Container>();
            if (!c) yield break;
            c.Save();
            PingContainer(c.gameObject);
        }
        catch (Exception e)
        {
            LogError($"Error while storing to {vc.gameObject.name}: {e}");
        }
        finally
        {
            if (--InProgressStores == 0)
                StoreSuccess(InProgressTotal);
        }
    }


    internal static float GetContainerRange(Container container)
    {
        if (yamlData == null)
        {
            AzuAutoStoreLogger.LogError("yamlData is null when trying to get the container range for a container. Make sure that your YAML file is not empty or to call DeserializeYamlFile() before using GetContainerRange.");
            return -1f;
        }

        if (container.GetInventory() == null)
            return -1f;

        // Try to get container settings from YAML configuration
        string containerName = MiscFunctions.GetPrefabName(container.transform.root.name);
        if (yamlData.TryGetValue(containerName, out object containerData))
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
                AzuAutoStoreLogger.LogError($"Unable to cast containerData for container '{containerName}' to Dictionary<object, object>.");
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
            bool isPaused = container.m_nview.GetZDO().GetBool(ContainerAwakePatch.storingPausedHash, false);
            if (isPaused) continue;

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


    internal static int InProgressStores = 0;
    internal static int InProgressTotal = 0;

    internal static void TryStore()
    {
        if (!Player.m_localPlayer) return;
        LogDebug("Trying to store items from player inventory");

        IContainer?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value).ToArray();

        int total = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearby) continue;

            bool canImmediate = nearby.IsOwner() || (nearby is VanillaContainers && MUCCompat.MultiUserChestActive);

            if (!canImmediate) continue;

            uncheckedContainers[i] = null;
            total += nearby.TryStore();
        }


        InProgressTotal += total;

        InProgressStores = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not VanillaContainers v) continue;

            InProgressStores++;
            self.StartCoroutine(WaitForOwnershipAndStore(v));
        }

        if (InProgressStores == 0)
        {
            StoreSuccess(InProgressTotal);
        }
    }

    internal static void TryStoreThisItem(ItemDrop.ItemData itemData, Inventory m_inventory)
    {
        if (!Player.m_localPlayer) return;
        if (m_inventory != Player.m_localPlayer.GetInventory()) return;

        LogDebug($"Trying to store {itemData.m_shared.m_name}");

        IContainer?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, PlayerRange.Value).ToArray();

        int total = 0;

        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearby) continue;

            bool canImmediate =
                nearby.IsOwner() ||
                (nearby is VanillaContainers && MUCCompat.MultiUserChestActive);

            if (!canImmediate) continue;

            uncheckedContainers[i] = null;
            total += nearby.TryStoreThisItem(itemData, m_inventory);
        }


        InProgressTotal += total;

        InProgressStores = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not VanillaContainers v) continue;

            InProgressStores++;
            self.StartCoroutine(WaitForOwnershipAndStoreSingle(v, itemData, m_inventory));
        }

        if (InProgressStores == 0)
        {
            StoreSuccess(InProgressTotal);
        }
    }

    internal static IEnumerator WaitForOwnershipAndStoreSingle(VanillaContainers vc, ItemDrop.ItemData item, Inventory inv, float timeoutSeconds = 1f)
    {
        if (vc == null || vc.m_nview == null || !vc.m_nview.IsValid())
        {
            if (--InProgressStores == 0) StoreSuccess(InProgressTotal);
            yield break;
        }

        if (!MUCCompat.MultiUserChestActive)
        {
            if (!vc.m_nview.IsOwner())
                vc.m_nview.ClaimOwnership();

            float t = 0f;
            while (!vc.m_nview.IsOwner() && t < timeoutSeconds)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (!vc.m_nview.IsOwner())
            {
                LogDebug($"Ownership claim timed out for {vc.gameObject.name} (single item).");
                if (--InProgressStores == 0) StoreSuccess(InProgressTotal);
                yield break;
            }
        }

        try
        {
            int moved = vc.TryStoreThisItem(item, inv);
            InProgressTotal += moved;

            Container? c = vc.gameObject.GetComponent<Container>();
            if (c)
            {
                c.Save();
                PingContainer(c.gameObject);
            }
        }
        catch (Exception e)
        {
            LogError($"Error while storing to {vc.gameObject.name}: {e}");
        }
        finally
        {
            if (--InProgressStores == 0)
                StoreSuccess(InProgressTotal);
        }
    }


    internal static void StoreSuccess(int total)
    {
        try
        {
            if (total > 0)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Stored {total} items from your inventory into nearby containers");

                foreach (IContainer c in Boxes.ContainersToPing)
                {
                    PingContainer(c.gameObject);
                    try
                    {
                        if (c.IsOwner() && InventoryGui.instance && c != InventoryGui.instance.m_currentContainer)
                        {
                            c.m_nview.GetZDO().Set(ZDOVars.s_inUse, 0, false);
                            if (c is VanillaContainers container)
                            {
                                container.gameObject.GetComponent<Container>().SetInUse(false);
                                InventoryGui.instance.m_moveItemEffects.Create(c.gameObject.transform.position, Quaternion.identity);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AzuAutoStoreLogger.LogError($"Error while trying to reset ownership of container {c.gameObject.name}: {e}");
                    }
                }
            }
        }
        finally
        {
            Boxes.ContainersToPing.Clear();
            InProgressTotal = 0;
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
        AzuAutoStoreLogger.LogDebug(data);
    }

    internal static void LogIfBuildDebug(string data)
    {
#if DEBUG
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
#endif
    }

    internal static void LogError(string data)
    {
        AzuAutoStoreLogger.LogError(data);
    }

    internal static void LogInfo(string data)
    {
        AzuAutoStoreLogger.LogInfo(data);
    }

    internal static void LogWarning(string data)
    {
        AzuAutoStoreLogger.LogWarning(data);
    }
}