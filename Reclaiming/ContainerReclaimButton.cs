using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OttoStash.Reclaiming;

/// The Reclaim All button on an open container. The first click arms it, the
/// second reclaims everything the rules allow. Hold Left Ctrl and drag it with
/// the right mouse button to move it.
internal sealed class ContainerReclaimButton : MonoBehaviour
{
    private Button? _button;
    private bool _armed;
    private TMP_Text _text = null!;
    private Image _image = null!;

    private void Start()
    {
        InvokeRepeating(nameof(EnsureButtonExists), 0f, 5f);
    }

    private void EnsureButtonExists()
    {
        if (InventoryGui.instance == null)
            return;
        if (_button == null)
            SetupButton();
        _button!.gameObject.SetActive(ContainerRecyclingEnabled.Value.IsOn());
    }

    private void OnDestroy()
    {
        if (_button != null)
            Destroy(_button.gameObject);
    }

    private void FixedUpdate()
    {
        if (_button == null || ContainerRecyclingEnabled.Value.IsOff())
            return;
        // The armor stand panel reuses the container panel, but has no container to reclaim.
        bool standOpen = StandSession.Current != null;
        if (_button.gameObject.activeSelf == standOpen)
            _button.gameObject.SetActive(!standOpen);
        if (_armed && (standOpen || !InventoryGui.instance.IsContainerOpen()))
            SetArmed(false);
    }

    private void SetupButton()
    {
        Button takeAll = InventoryGui.instance.m_takeAllButton;
        _button = Instantiate(takeAll, takeAll.transform);
        _button.transform.SetParent(takeAll.transform.parent);
        _button.transform.localPosition = ContainerButtonPosition.Value;
        _button.onClick = new Button.ButtonClickedEvent();
        _button.onClick.AddListener(OnPressed);
        _text = _button.GetComponentInChildren<TMP_Text>();
        _image = _button.GetComponentInChildren<Image>();
        _button.gameObject.AddComponent<UIDragger>().Dropped += position => ContainerButtonPosition.Value = position;
        SetArmed(false);
    }

    private void SetArmed(bool armed)
    {
        _armed = armed;
        _text.text = ReclaimText.Localize(armed ? "$ottostash_reclaim_confirm" : "$ottostash_reclaim_reclaim_all");
        _image.color = armed ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 1f, 0.5f);
    }

    private void OnPressed()
    {
        Player player = Player.m_localPlayer;
        if (!player)
            return;
        if (!_armed)
        {
            SetArmed(true);
            return;
        }

        SetArmed(false);
        Container? container = InventoryGui.instance.m_currentContainer;
        if (container == null)
            return;
        Reclaimer.ReclaimInventory(container.GetInventory(), PrefabNames.FromSceneName(container.transform.name), player);
    }
}

/// Lets the player move a UI element: hold Left Ctrl and drag with the right
/// mouse button.
internal sealed class UIDragger : EventTrigger
{
    internal event Action<Vector3>? Dropped;
    private bool _dragging;

    private void Update()
    {
        if (_dragging)
            transform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && Input.GetKey(KeyCode.LeftControl))
            _dragging = true;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || !_dragging)
            return;
        _dragging = false;
        Vector3 local = transform.localPosition;
        Dropped?.Invoke(new Vector3(local.x, local.y, -1f));
    }
}

/// Scales the mouse wheel speed of the crafting and Reclaim recipe lists.
[HarmonyPatch]
internal static class RecipeListScroll
{
    private static ScrollRect? _scrollRect;
    private static float _vanillaSensitivity = 40f;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    private static void InventoryGuiAwakePostfix(InventoryGui __instance)
    {
        _scrollRect = FindRecipeScrollRect(__instance);
        if (_scrollRect == null)
        {
            StashLog.Warning("Could not find the recipe list's scroll view, so RecipeListScrollSpeedMultiplier has no effect.");
            return;
        }

        _vanillaSensitivity = _scrollRect.scrollSensitivity;
        Apply();
    }

    private static ScrollRect? FindRecipeScrollRect(InventoryGui igui)
    {
        RectTransform? root = igui.m_recipeListRoot;
        if (root == null)
            return null;
        ScrollRect? scrollRect = root.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
            return scrollRect;
        // UI mods move things around, so fall back to finding it by its content.
        return igui.GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(candidate => candidate.content == root);
    }

    internal static void Apply()
    {
        if (_scrollRect != null)
            _scrollRect.scrollSensitivity = _vanillaSensitivity * Mathf.Max(0.1f, RecipeListScrollSpeedMultiplier.Value);
    }
}
