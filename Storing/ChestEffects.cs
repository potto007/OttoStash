namespace OttoStash.Storing;

/// The ping and highlight a chest shows when something goes into it.
internal static class ChestEffects
{
    internal static void Ping(GameObject chest)
    {
        if (chest == null)
            return;

        if (PingContainers.Value.IsOn() && chest.GetComponent<ChestPingEffect>() == null)
            chest.AddComponent<ChestPingEffect>();

        if (HighlightContainers.Value.IsOn() && chest.GetComponent<ChestHighlight>() == null)
            chest.AddComponent<ChestHighlight>();
    }
}

/// Spawns the configured ping effect at the chest and removes it ten seconds later.
internal sealed class ChestPingEffect : MonoBehaviour
{
    private const float LifetimeSeconds = 10f;

    private GameObject? _effect;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(PingVfxString.Value))
            return;
        _effect = Instantiate(ZNetScene.instance.GetPrefab(PingVfxString.Value), transform.position, Quaternion.identity);
        InvokeRepeating(nameof(Finish), LifetimeSeconds, 1f);
    }

    private void Finish()
    {
        if (_effect != null)
            ZNetScene.instance.Destroy(_effect);
        DestroyImmediate(this);
    }
}

/// Keeps the chest's build highlight on for ten seconds.
internal sealed class ChestHighlight : MonoBehaviour
{
    private const float LifetimeSeconds = 10f;

    private WearNTear? _wearNTear;

    private void Awake()
    {
        _wearNTear = TryGetComponent(out WearNTear wearNTear) ? wearNTear : null;
        if (_wearNTear == null)
            Finish();
        else
            InvokeRepeating(nameof(Finish), LifetimeSeconds, 1f);
    }

    private void Update()
    {
        _wearNTear?.Highlight();
    }

    private void Finish()
    {
        DestroyImmediate(this);
    }
}
