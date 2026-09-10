using Object = UnityEngine.Object;

namespace OttoStash.Managers;

[HarmonyPatch(typeof(Game), nameof(Game.Start))]
public static class GameStartPatch
{
    static void Postfix()
    {
        OttoStashItemDropManager? itemDropManager = new GameObject("OttoStash_ItemDropManager").AddComponent<OttoStashItemDropManager>();
        Object.DontDestroyOnLoad(itemDropManager);
    }
}

public class OttoStashItemDropManager : MonoBehaviour
{
    private void Awake()
    {
        InvokeRepeating(nameof(ProcessItemDrops), IntervalSeconds.Value, IntervalSeconds.Value);
        IntervalSeconds.SettingChanged += OnIntervalSecondsChanged;
    }

    private void OnDestroy()
    {
        IntervalSeconds.SettingChanged -= OnIntervalSecondsChanged;
    }

    private void ProcessItemDrops()
    {
        if (ShouldPause())
        {
            return;
        }

        if (Boxes.Containers == null || Boxes.Containers.Count == 0)
            return;

        foreach (ItemDrop itemDrop in ItemDrop.s_instances.Where(x => !x.IsPiece()).ToList())
        {
            if (itemDrop == null || itemDrop.transform == null || itemDrop.m_nview == null || !itemDrop.m_nview.IsValid())
                continue;

            Functions.CheckItemDropInstanceAndStore(itemDrop);
        }
    }

    private void OnIntervalSecondsChanged(object sender, EventArgs e)
    {
        CancelInvoke(nameof(ProcessItemDrops));
        InvokeRepeating(nameof(ProcessItemDrops), IntervalSeconds.Value, IntervalSeconds.Value);
    }

    private static bool ShouldPause()
    {
        Player? player = Player.m_localPlayer;
        return player == null || player.IsTeleporting() || player.IsDead();
    }
}