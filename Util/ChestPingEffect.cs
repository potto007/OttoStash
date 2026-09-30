namespace OttoStash.Util;

public class ChestPingEffect : MonoBehaviour
{
    GameObject pingObject;

    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(PingVfxString.Value))
        {
            pingObject = Instantiate(ZNetScene.instance.GetPrefab(PingVfxString.Value), transform.position, Quaternion.identity);
            Trigger();
        }
    }

    public void Trigger() => InvokeRepeating(nameof(DestroyNow), 10f, 1f); // 10 seconds after the awake, it will start to destroy the object

    public void DestroyNow()
    {
        if (pingObject != null)
            ZNetScene.instance.Destroy(pingObject);
        DestroyImmediate(this);
    }
}