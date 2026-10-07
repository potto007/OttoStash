using TMPro;
using UnityEngine.UI;

namespace OttoStash.Reclaiming;

/// The Reclaim tab at a crafting station, next to Craft and Upgrade. It reuses
/// the crafting panel: the recipe list shows the items that can be reclaimed, and
/// the craft button reclaims the selected one.
internal sealed class ReclaimTab : MonoBehaviour
{
    private GameObject? _tabObject;
    private Button? _tabButton;
    private readonly List<ReclaimAnalysis> _analyses = new();
    private int _lastInventoryHash;

    private void Start()
    {
        InvokeRepeating(nameof(EnsureTabExists), 5f, 5f);
    }

    private void EnsureTabExists()
    {
        if (InventoryGui.instance == null)
            return;
        if (_tabButton == null)
            SetupTabButton();
    }

    private void OnDestroy()
    {
        if (_tabObject != null)
            Destroy(_tabObject);
    }

    private void SetupTabButton()
    {
        if (Player.m_localPlayer == null)
            return;

        InventoryGui igui = InventoryGui.instance;
        Transform upgradeTab = igui.m_tabUpgrade.transform;
        _tabObject = Instantiate(igui.m_tabUpgrade.gameObject, upgradeTab.position, upgradeTab.rotation, upgradeTab.parent);
        _tabObject.name = "RECLAIM";
        // The tab border has to stay the last sibling or it draws under the new tab.
        _tabObject.transform.parent.Find("TabBorder").SetAsLastSibling();
        _tabObject.transform.localPosition = new Vector3(-45, -94, 0);
        _tabButton = _tabObject.GetComponent<Button>();
        _tabButton.interactable = true;
        _tabButton.onClick = new Button.ButtonClickedEvent();
        _tabButton.onClick.AddListener(OnTabClick);
        TMP_Text? text = _tabObject.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = ReclaimText.Localize("$ottostash_reclaim_reclaim_tab");

        _tabObject.SetActive(ReclaimTabEnabled.Value.IsOn() && Player.m_localPlayer.GetCurrentCraftingStation() != null);
    }

    private void OnTabClick()
    {
        _tabButton!.interactable = false;
        InventoryGui.instance.m_tabCraft.interactable = true;
        InventoryGui.instance.m_tabUpgrade.interactable = true;
        UpdateCraftingPanel();
    }

    internal void UpdateCraftingPanel()
    {
        InventoryGui igui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        if (player.GetCurrentCraftingStation() == null && !player.NoCostCheat())
        {
            igui.m_tabCraft.interactable = false;
            igui.m_tabUpgrade.interactable = true;
            igui.m_tabUpgrade.gameObject.SetActive(false);
            _tabButton!.interactable = true;
            _tabButton.gameObject.SetActive(false);
        }
        else
        {
            igui.m_tabUpgrade.gameObject.SetActive(true);
        }

        UpdateRecyclingList();

        if (igui.m_availableRecipes.Count > 0)
            igui.SetRecipe(igui.m_selectedRecipe.Recipe != null ? igui.GetSelectedRecipeIndex(false) : 0, true);
        else
            igui.SetRecipe(-1, true);
    }

    internal void UpdateRecyclingList()
    {
        Player player = Player.m_localPlayer;
        InventoryGui igui = InventoryGui.instance;
        List<InventoryGui.RecipeDataPair> recipeList = igui.m_availableRecipes;
        foreach (InventoryGui.RecipeDataPair element in recipeList)
            Destroy(element.InterfaceElement);
        recipeList.Clear();

        _analyses.Clear();
        // Display impediments decide what is listed. An item with no recipe is
        // dropped outright rather than listed as blocked.
        _analyses.AddRange(Reclaimer.AnalyzeInventory(player.GetInventory(), player)
            .Where(analysis => analysis.Recipe != null && analysis.DisplayImpediments.Count == 0
                                                       && (analysis.Item.m_dropPrefab == null || !ReclaimRules.IsExcludedInReclaiming(analysis.Item.m_dropPrefab.name))));
        foreach (ReclaimAnalysis analysis in _analyses)
            AddRecipeToList(analysis, recipeList);

        igui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(igui.m_recipeListBaseSize, recipeList.Count * igui.m_recipeListSpace));
        _lastInventoryHash = InventoryHash(player.GetInventory());
    }

    private void AddRecipeToList(ReclaimAnalysis analysis, List<InventoryGui.RecipeDataPair> recipeList)
    {
        InventoryGui igui = InventoryGui.instance;
        GameObject element = Instantiate(igui.m_recipeElementPrefab, igui.m_recipeListRoot);
        element.SetActive(true);
        ((RectTransform)element.transform).anchoredPosition = new Vector2(0.0f, recipeList.Count * -igui.m_recipeListSpace);

        bool blocked = analysis.RecyclingImpediments.Count > 0;
        Image icon = element.transform.Find("icon").GetComponent<Image>();
        icon.sprite = analysis.Item.GetIcon();
        icon.color = blocked ? new Color(1f, 0.0f, 1f, 0.0f) : Color.white;
        if (ReclaimFeature.HasEpicLoot && !blocked)
            EpicLootAPI.EpicLoot.ApplyMagicItemBackgroundToIcon(icon.gameObject, analysis.Item);

        TMP_Text name = element.transform.Find("name").GetComponent<TMP_Text>();
        string label = ReclaimText.Localize(ItemDisplayName(analysis.Item, blocked));
        if (analysis.Item.m_stack > 1 && analysis.Item.m_shared.m_maxStackSize > 1)
            label = $"{label} x{analysis.Item.m_stack}";
        name.text = label;
        name.color = blocked ? new Color(0.66f, 0.66f, 0.66f, 1f) : Color.white;

        GuiBar durability = element.transform.Find("Durability").GetComponent<GuiBar>();
        if (analysis.Item.m_shared.m_useDurability && analysis.Item.m_durability < (double)analysis.Item.GetMaxDurability())
        {
            durability.gameObject.SetActive(true);
            durability.SetValue(analysis.Item.GetDurabilityPercentage());
        }
        else
        {
            durability.gameObject.SetActive(false);
        }

        TMP_Text quality = element.transform.Find("QualityLevel").GetComponent<TMP_Text>();
        quality.gameObject.SetActive(true);
        quality.text = analysis.Item.m_quality.ToString();

        element.GetComponent<Button>().onClick.AddListener(() => igui.OnSelectedRecipe(element));
        // check_enums: verified - written against Valheim 1.0.17 source, as vanilla InventoryGui.AddRecipeToList reads it.
        bool noCraftCost = ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
        CraftingStation? station = Player.m_localPlayer.GetCurrentCraftingStation();
        bool atUpgrader = station != null && station.m_upgrader;
        bool canCraft = igui.InCraftTab()
            ? Player.m_localPlayer.HaveRequirements(analysis.Recipe, false, 1) | noCraftCost
            : (analysis.Item.m_quality < analysis.Item.m_shared.m_maxQuality || atUpgrader) && Player.m_localPlayer.HaveRequirements(analysis.Recipe, false, analysis.Item.m_quality + 1) | noCraftCost;
        recipeList.Add(new InventoryGui.RecipeDataPair(analysis.Recipe, analysis.Item, element, canCraft));
    }

    /// Epic Loot's decorated name carries a color tag that beats the text color,
    /// so a blocked row is dimmed through the tag.
    private static string ItemDisplayName(ItemDrop.ItemData item, bool blocked)
    {
        if (!ReclaimFeature.HasEpicLoot)
            return item.m_shared.m_name;
        return EpicLootAPI.EpicLoot.GetDecoratedName(item, blocked ? "#A8A8A8FF" : null!);
    }

    private static int InventoryHash(Inventory inventory)
    {
        unchecked
        {
            int hash = 17;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems().OrderBy(i => i.m_shared.m_name).ThenBy(i => i.m_quality))
            {
                hash = hash * 31 + item.m_shared.m_name.GetHashCode();
                hash = hash * 31 + item.m_quality;
                hash = hash * 31 + item.m_stack;
                hash = hash * 31 + (item.m_equipped ? 1 : 0);
            }

            return hash;
        }
    }

    /// False when only positions changed, so the list need not be rebuilt.
    internal bool HasInventoryChanged(Inventory inventory)
    {
        int hash = InventoryHash(inventory);
        if (hash == _lastInventoryHash)
            return false;
        _lastInventoryHash = hash;
        return true;
    }

    internal bool InReclaimTab()
    {
        return _tabButton != null && !_tabButton.interactable;
    }

    internal void SetInteractable(bool interactable)
    {
        EnsureTabExists();
        if (_tabButton != null)
            _tabButton.interactable = interactable;
    }

    internal void SetActive(bool active)
    {
        if (_tabObject != null)
            _tabObject.SetActive(active && ReclaimTabEnabled.Value.IsOn());
    }

    internal void UpdateRecipe(Player player, float dt)
    {
        InventoryGui igui = InventoryGui.instance;
        int selected = igui.GetSelectedRecipeIndex(false);

        RefreshSelectedAnalysis(selected, player);
        UpdateCraftingStationUI(player);

        if (igui.m_selectedRecipe.Recipe && selected >= 0 && selected < _analyses.Count)
            UpdateRecipeUI(selected, igui);
        else
            ClearRecipeUI(igui);

        UpdateCraftingTimer(dt, selected, player, igui);
    }

    private void RefreshSelectedAnalysis(int selected, Player player)
    {
        if (selected < 0 || selected >= _analyses.Count)
            return;
        ReclaimAnalysis fresh = new(_analyses[selected].Item);
        Reclaimer.TryAnalyze(fresh, player.GetInventory(), player);
        _analyses[selected] = fresh;
    }

    private static void UpdateCraftingStationUI(Player player)
    {
        InventoryGui igui = InventoryGui.instance;
        CraftingStation? station = player.GetCurrentCraftingStation();
        if (station)
        {
            SetActive(igui.m_craftingStationIcon.gameObject, true);
            SetActive(igui.m_craftingStationLevelRoot.gameObject, true);
            igui.m_craftingStationName.text = ReclaimText.Localize(station!.m_name);
            igui.m_craftingStationIcon.sprite = station.m_icon;
            igui.m_craftingStationLevel.text = station.GetLevel().ToString();
        }
        else
        {
            SetActive(igui.m_craftingStationIcon.gameObject, false);
            SetActive(igui.m_craftingStationLevelRoot.gameObject, false);
            igui.m_craftingStationName.text = ReclaimText.Localize("$hud_crafting");
        }
    }

    private void UpdateRecipeUI(int selected, InventoryGui igui)
    {
        ReclaimAnalysis analysis = _analyses[selected];
        ItemDrop.ItemData? itemData = igui.m_selectedRecipe.ItemData;

        igui.m_recipeIcon.enabled = true;
        igui.m_recipeName.enabled = true;
        igui.m_recipeDecription.enabled = true;

        igui.m_recipeIcon.sprite = igui.m_selectedRecipe.Recipe.m_item.m_itemData.m_shared.m_icons[itemData?.m_variant ?? igui.m_selectedVariant];
        if (ReclaimFeature.HasEpicLoot)
            EpicLootAPI.EpicLoot.ApplyMagicItemBackgroundToIcon(igui.m_recipeIcon.gameObject, analysis.Item);
        string name = ReclaimText.Localize(ItemDisplayName(analysis.Item, blocked: false));
        if (analysis.Item.m_stack > 1)
            name = name + " x" + analysis.Item.m_stack;
        igui.m_recipeName.text = name;

        igui.m_recipeDecription.text = analysis.RecyclingImpediments.Count == 0
            ? "\n" + ReclaimText.Localize("$ottostash_reclaim_requirements_fulfilled")
            : "\n" + ReclaimText.Localize("$ottostash_reclaim_requirements_blocked") + $":\n\n<size=15>{string.Join("\n", analysis.RecyclingImpediments)}</size>";

        if (itemData != null)
        {
            SetActive(igui.m_itemCraftType.gameObject, true);
            string craftTypeName = ReclaimFeature.HasEpicLoot ? EpicLootAPI.EpicLoot.GetDisplayName(itemData) : itemData.m_shared.m_name;
            igui.m_itemCraftType.text = ReclaimText.Localize("$ottostash_reclaim_reclaim_item_level", ReclaimText.Localize(craftTypeName), itemData.m_quality.ToString());
        }
        else
        {
            SetActive(igui.m_itemCraftType.gameObject, false);
        }

        SetActive(igui.m_variantButton.gameObject, igui.m_selectedRecipe.Recipe.m_item.m_itemData.m_shared.m_variants > 1 && igui.m_selectedRecipe.ItemData == null);

        if (ReclaimFeature.HasEpicLoot)
            SetupRequirementListEpicLoot(analysis);
        else
            SetupRequirementList(analysis);

        SetActive(igui.m_minStationLevelIcon.gameObject, false);
        igui.m_craftButton.interactable = analysis.RecyclingImpediments.Count == 0;
        igui.m_craftButton.GetComponentInChildren<TMP_Text>().text = ReclaimText.Localize("$ottostash_reclaim_reclaim_button");
        igui.m_craftButton.GetComponent<UITooltip>().m_text = analysis.RecyclingImpediments.Count == 0 ? "" : ReclaimText.Localize("$msg_missingrequirement");
    }

    private static void ClearRecipeUI(InventoryGui igui)
    {
        igui.m_recipeIcon.enabled = false;
        if (ReclaimFeature.HasEpicLoot)
            EpicLootAPI.EpicLoot.ApplyMagicItemBackgroundToIcon(igui.m_recipeIcon.gameObject, null!);
        igui.m_recipeName.enabled = false;
        igui.m_recipeDecription.enabled = false;

        SetActive(igui.m_qualityPanel.gameObject, false);
        SetActive(igui.m_minStationLevelIcon.gameObject, false);
        igui.m_craftButton.GetComponent<UITooltip>().m_text = "";
        SetActive(igui.m_variantButton.gameObject, false);

        igui.m_craftButton.GetComponentInChildren<TMP_Text>().text = ReclaimText.Localize("$ottostash_reclaim_reclaim_button");
        SetActive(igui.m_itemCraftType.gameObject, false);
        foreach (GameObject requirement in igui.m_recipeRequirementList)
            InventoryGui.HideRequirement(requirement.transform);
        igui.m_craftButton.interactable = false;
    }

    private void UpdateCraftingTimer(float dt, int selected, Player player, InventoryGui igui)
    {
        if (igui.m_craftTimer < 0.0)
        {
            SetActive(igui.m_craftProgressPanel.gameObject, false);
            SetActive(igui.m_craftButton.gameObject, true);
            return;
        }

        SetActive(igui.m_craftButton.gameObject, false);
        SetActive(igui.m_craftProgressPanel.gameObject, true);
        igui.m_craftProgressBar.SetMaxValue(igui.m_craftDuration);
        igui.m_craftProgressBar.SetValue(igui.m_craftTimer);
        igui.m_craftTimer += dt;
        if (igui.m_craftTimer < (double)igui.m_craftDuration)
            return;

        if (selected >= 0 && selected < _analyses.Count)
            Reclaimer.ApplyToInventory(_analyses[selected], player.GetInventory(), player, recordUndo: true);
        igui.m_craftTimer = -1f;
        igui.SetRecipe(-1, false);
        UpdateCraftingPanel();
    }

    private static void SetActive(GameObject gameObject, bool isActive)
    {
        if (gameObject)
            gameObject.SetActive(isActive);
    }

    /// Epic Loot does not take to the vanilla paging of the requirement list, so
    /// with it installed the first entries are shown without paging.
    private static void SetupRequirementListEpicLoot(ReclaimAnalysis analysis)
    {
        InventoryGui igui = InventoryGui.instance;
        List<ReclaimYield> shown = analysis.Entries.Where(entry => entry.Amount != 0).ToList();
        for (int i = 0; i < igui.m_recipeRequirementList.Length; ++i)
        {
            Transform element = igui.m_recipeRequirementList[i].transform;
            if (i < shown.Count)
                SetupRequirement(element, shown[i]);
            else
                InventoryGui.HideRequirement(element);
        }
    }

    /// More entries than slots page through over time, as the vanilla list does.
    private static void SetupRequirementList(ReclaimAnalysis analysis)
    {
        InventoryGui igui = InventoryGui.instance;
        List<ReclaimYield> shown = analysis.Entries.Where(entry => entry.Amount != 0).ToList();
        int slots = igui.m_recipeRequirementList.Length;
        int start = shown.Count > slots ? (int)Time.fixedTime % (int)Mathf.Ceil(shown.Count / (float)slots) * slots : 0;

        int slot = 0;
        for (int i = start; i < shown.Count && slot < slots; ++i)
        {
            if (SetupRequirement(igui.m_recipeRequirementList[slot].transform, shown[i]))
                ++slot;
        }

        for (; slot < slots; ++slot)
            InventoryGui.HideRequirement(igui.m_recipeRequirementList[slot].transform);
    }

    private static bool SetupRequirement(Transform elementRoot, ReclaimYield entry)
    {
        Image icon = elementRoot.transform.Find("res_icon").GetComponent<Image>();
        TMP_Text name = elementRoot.transform.Find("res_name").GetComponent<TMP_Text>();
        TMP_Text amount = elementRoot.transform.Find("res_amount").GetComponent<TMP_Text>();
        UITooltip tooltip = elementRoot.GetComponent<UITooltip>();
        icon.gameObject.SetActive(true);
        name.gameObject.SetActive(true);
        amount.gameObject.SetActive(true);
        icon.sprite = entry.RecipeItemData.GetIcon();
        icon.color = Color.white;
        tooltip.m_text = ReclaimText.Localize(entry.RecipeItemData.m_shared.m_name);
        name.text = ReclaimText.Localize(entry.RecipeItemData.m_shared.m_name);
        amount.text = entry.Amount.ToString();
        amount.color = Color.white;
        if (entry.Amount > 0)
            return true;
        InventoryGui.HideRequirement(elementRoot);
        return false;
    }
}
