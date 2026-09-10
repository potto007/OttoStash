#if DEBUG
using System.Text;
using System.Text.RegularExpressions;
#endif
using OttoStash.APIs.MUC;
using BepInEx.Logging;
using JetBrains.Annotations;
using ServerSync;

namespace OttoStash;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInDependency("Azumatt.AzuExtendedPlayerInventory", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(KgGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(BackpacksGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class OttoStashPlugin : BaseUnityPlugin
{
    internal const string ModName = "OttoStash";
    internal const string ModVersion = "3.1.0";
    internal const string Author = "potto007";
    internal const string ModGUID = $"{Author}.{ModName}";
    internal const string KgGuid = "kg.ItemDrawers";
    internal const string BackpacksGuid = "org.bepinex.plugins.backpacks";
    private const string ConfigFileName = ModGUID + ".cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    internal static string ConnectionError = "";
    private readonly Harmony _harmony = new(ModGUID);
    public static readonly ManualLogSource OttoStashLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
    private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
    internal static bool BackpacksIsLoaded = false;
    internal static readonly string yamlFileName = $"{ModGUID}.yml";
    internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
    internal static readonly CustomSyncedValue<string> OttoStashContainerData = new(ConfigSync, "ottostashData", "");
    internal static readonly CustomSyncedValue<string> CraftyContainerGroupsData = new(ConfigSync, "ottostashGroupsData", "");

    //
    internal static Dictionary<string, object>? yamlData;
    internal static Dictionary<string?, HashSet<string?>> groups = null!;
    internal static OttoStashPlugin self = null!;

    public enum Toggle
    {
        Off,
        On
    }

    /// <summary>
    /// Copy the AzuAutoStore config and container rules across on first run, so a
    /// player who upgrades keeps the settings and the per-chest YAML they had.
    /// Never touches an OttoStash file that already exists.
    /// </summary>
    private static void CarryOverAzuAutoStoreConfig()
    {
        try
        {
            string oldConfig = Paths.ConfigPath + Path.DirectorySeparatorChar + "Azumatt.AzuAutoStore.cfg";
            if (File.Exists(oldConfig) && !File.Exists(ConfigFileFullPath))
            {
                File.Copy(oldConfig, ConfigFileFullPath);
                OttoStashLogger.LogInfo($"Carried your AzuAutoStore settings over to {ConfigFileName}.");
            }

            string oldYaml = Paths.ConfigPath + Path.DirectorySeparatorChar + "Azumatt.AzuAutoStore.yml";
            if (File.Exists(oldYaml) && !File.Exists(yamlPath))
            {
                File.Copy(oldYaml, yamlPath);
                OttoStashLogger.LogInfo($"Carried your AzuAutoStore container rules over to {yamlFileName}.");
            }
        }
        catch (Exception e)
        {
            // A failure here must not stop the mod from loading.
            OttoStashLogger.LogWarning($"Could not carry the AzuAutoStore configuration over: {e.Message}");
        }
    }

    public void Awake()
    {
        self = this;

        CarryOverAzuAutoStoreConfig();

        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, new ConfigDescription("If on, the configuration is locked and can be changed by server admins only.", null, new ConfigurationManagerAttributes() { Order = 10 }));
        ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        DontStoreToBackpacks = config("1 - General", "Dont Store to Backpacks", Toggle.Off, new ConfigDescription("If on, items will not be stored in backpacks.", null, new ConfigurationManagerAttributes() { Order = 9 }));
        ChestsPickupFromGround = config("1 - General", "Chests Pickup From Ground", Toggle.On, new ConfigDescription("If on, chests will pick up items from the ground if they are in range. If off, chests will not begin their periodic checks for items nearby. Reloading zone or logging out might be required.", null, new ConfigurationManagerAttributes() { Order = 8 }));
        MustHaveExistingItemToPull = config("1 - General", "Must Have Existing Item To Pull", Toggle.On, new ConfigDescription("If on, the chest must already have the item in its inventory to pull it from the world or player into the chest.", null, new ConfigurationManagerAttributes() { Order = 7 }));
        PlayerRange = config("1 - General", "Player Range", 5f, new ConfigDescription("The maximum distance from the player to store items in chests when the Store Shortcut is pressed. Follows storage rules for allowed items.", new AcceptableValueRange<float>(1f, 100f), new ConfigurationManagerAttributes() { Order = 6 }));
        FallbackRange = config("1 - General", "Fallback Range", 10f, new ConfigDescription("The range to use if the container has no range set in the yml file. This will be the fallback range for all containers.", null, new ConfigurationManagerAttributes() { Order = 5 }));
        PlayerIgnoreHotbar = config("1 - General", "Player Ignore Hotbar", Toggle.On, new ConfigDescription("If on, the player's hotbar will not be stored when the Store Shortcut is pressed.", null, new ConfigurationManagerAttributes() { Order = 4 }), false);
        PlayerIgnoreQuickSlots = config("1 - General", "Player Ignore Quick Slots", Toggle.Off, new ConfigDescription("If on, the player's quick slots will not be stored when the Store Shortcut is pressed. (Requires Quick Slots mod, turn on only if you need it!)", null, new ConfigurationManagerAttributes() { Order = 3 }), false);
        PingVfxString = TextEntryConfig("1 - General", "Ping VFX", "vfx_Potion_health_medium", new ConfigDescription("The VFX to play when a chest is pinged. Leave blank to disable and only highlight the chest. (Full prefab list: https://valheim-modding.github.io/Jotunn/data/prefabs/prefab-list.html)", null, new ConfigurationManagerAttributes() { Order = 2 }));
        HighlightContainers = config("1 - General", "Highlight Containers", Toggle.On, new ConfigDescription("If on, the containers will be highlighted when something is stored in them. If off, the containers will not be highlighted if something is stored in them.", null, new ConfigurationManagerAttributes() { Order = 1 }), false);
        PingContainers = config("1 - General", "Ping Containers", Toggle.On, new ConfigDescription("If on, the containers will be pinged with the Ping VFX when something is stored in them. If off, the containers will not be pinged if something is stored in them.", null, new ConfigurationManagerAttributes() { Order = 0 }), false);
        SecondsToWaitBeforeStoring = config("1 - General", "Seconds To Wait Before Storing", 10, new ConfigDescription("The number of seconds to wait before storing items into chests nearby automatically after you have pressed your hotkey to pause.", new AcceptableValueRange<int>(0, 60)));
        IntervalSeconds = config("1 - General", nameof(IntervalSeconds), 10.0f, new ConfigDescription("The number of seconds that must pass before the chest will do an automatic check for items nearby, WARNING: Reducing this will decrease performance!"));
        FishSuction = config("1.5 - Fish", "Fish Suction", Toggle.Off, new ConfigDescription("Allow auto-storing fish that are still in water (boat 'netting'). Off = require fish to be out of the water; On = ignore IsOutOfWater() check."));
        SingleItemShortcut = config("2 - Shortcuts", "Store Single Item Shortcut", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Keyboard shortcut/Hotkey to store a single item that you click from your inventory into nearby containers.", new AcceptableShortcuts(), new ConfigurationManagerAttributes() { Order = 2 }), false);
        _storeShortcut = config("2 - Shortcuts", "Store Shortcut", new KeyboardShortcut(KeyCode.Period), new ConfigDescription("Keyboard shortcut/Hotkey to store your inventory into nearby containers.", new AcceptableShortcuts(), new ConfigurationManagerAttributes() { Order = 1 }), false);
        _pauseShortcut = config("2 - Shortcuts", "Pause Shortcut", new KeyboardShortcut(KeyCode.Period, KeyCode.LeftShift), new ConfigDescription("Keyboard shortcut/Hotkey to temporarily stop storing items into chests nearby automatically. Does not override the player hotkey store.", new AcceptableShortcuts()), false);
        SearchModifierKeybind = config("2 - Shortcuts", nameof(SearchModifierKeybind), new KeyboardShortcut(KeyCode.Y), new ConfigDescription("While holding this, you can search nearby chests for the prefab you clicked in your inventory.", new AcceptableShortcuts()), false);

        string sectionName = "3 - Favoriting";
        string favoritingKey = $"While holding this, left clicking on items or right clicking on slots favorites them, disallowing storing";

        BorderColorFavoritedItem = config(sectionName, nameof(BorderColorFavoritedItem), new Color(1f, 0.8482759f, 0f), "Color of the border for slots containing favorited items.", false);
        BorderColorFavoritedItem.SettingChanged += (a, b) => FavoritingMode.RefreshDisplay();

        // dark-ish green
        BorderColorFavoritedItemOnFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedItemOnFavoritedSlot), new Color(0.5f, 0.67413795f, 0.5f), "Color of the border of a favorited slot that also contains a favorited item.", false);

        // light-ish blue
        BorderColorFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedSlot), new Color(0f, 0.5f, 1f), "Color of the border for favorited slots.", false);

        DisplayTooltipHint = config(sectionName, nameof(DisplayTooltipHint), true, "Whether to add additional info the item tooltip of a favorited or trash flagged item.", false);

        FavoritingModifierKeybind1 = config(sectionName, nameof(FavoritingModifierKeybind1), new KeyboardShortcut(KeyCode.Z), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind2)}.", false);
        FavoritingModifierKeybind2 = config(sectionName, nameof(FavoritingModifierKeybind2), new KeyboardShortcut(KeyCode.Z), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind1)}.", false);
        FavoritedItemTooltip = config(sectionName, nameof(FavoritedItemTooltip), "Item is favorited and won't be stored", string.Empty, false);
        FavoritedSlotTooltip = config(sectionName, nameof(FavoritedSlotTooltip), "Slot is favorited and won't be stored", string.Empty, false);
        ItemOnFavoritedSlotTooltip = config(sectionName, nameof(ItemOnFavoritedSlotTooltip), "Item & Slot are favorited and won't be stored", string.Empty, false);

        if (!File.Exists(yamlPath))
        {
            WriteConfigFileFromResource(yamlPath);
        }

        OttoStashContainerData.ValueChanged += OnValChangedUpdate; // check for file changes
        OttoStashContainerData.AssignLocalValue(File.ReadAllText(yamlPath));

        AutoDoc();
        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();
    }

    public void Start()
    {
        BorderRenderer.Border = loadSprite("border.png");
        if (Chainloader.PluginInfos.ContainsKey(BackpacksGuid))
        {
            BackpacksIsLoaded = true;
        }
        
        if (!MUCCompat.MUCLoaded)
        {
            MUCCompat.ForceEnableMUC(true);
        }
    }

    private void AutoDoc()
    {
#if DEBUG
            // Store Regex to get all characters after a [
            Regex regex = new(@"\[(.*?)\]");

            // Strip using the regex above from Config[x].Description.Description
            string Strip(string x) => regex.Match(x).Groups[1].Value;
            StringBuilder sb = new();
            string lastSection = "";
            foreach (ConfigDefinition x in Config.Keys)
            {
                // skip first line
                if (x.Section != lastSection)
                {
                    lastSection = x.Section;
                    sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
                }

                sb.Append($"\n{x.Key} [{Strip(Config[x].Description.Description)}]" +
                          $"{Environment.NewLine}   * {Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                          $"{Environment.NewLine}     * Default Value: {Config[x].GetSerializedValue()}{Environment.NewLine}");
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModName}_AutoDoc.md"), sb.ToString());
#endif
    }

    private static void WriteConfigFileFromResource(string configFilePath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "OttoStash.Example.yml";

        using Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found in the assembly.");
        }

        using StreamReader reader = new StreamReader(resourceStream);
        string contents = reader.ReadToEnd();

        File.WriteAllText(configFilePath, contents);
    }

    private void Update()
    {
        if (!Player.m_localPlayer) return;
        if (_storeShortcut.Value.IsDown() && Player.m_localPlayer.TakeInput())
        {
#if DEBUG
                OttoStashLogger.LogError("Taking input");
#endif
            Functions.TryStore();
        }

        if (_pauseShortcut.Value.IsDown() && Player.m_localPlayer.TakeInput())
        {
            Boxes.StoringPaused = !Boxes.StoringPaused;
            foreach (Container container in Boxes.Containers)
            {
                if (!container.m_nview.IsValid()) continue;
                container.m_nview.InvokeRPC("RequestPause", Boxes.StoringPaused);
            }
        }
    }

    private void OnDestroy()
    {
        Config.Save();
    }

    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;

        FileSystemWatcher yamlwatcher = new(Paths.ConfigPath, yamlFileName);
        yamlwatcher.Changed += ReadYamlFiles;
        yamlwatcher.Created += ReadYamlFiles;
        yamlwatcher.Renamed += ReadYamlFiles;
        yamlwatcher.IncludeSubdirectories = true;
        yamlwatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        yamlwatcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            Functions.LogDebug("ReadConfigValues called");
            Config.Reload();
        }
        catch
        {
            Functions.LogError($"There was an issue loading your {ConfigFileName}");
            Functions.LogError("Please check your config entries for spelling and format!");
        }
    }

    private void ReadYamlFiles(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(yamlPath)) return;
        try
        {
            OttoStashLogger.LogDebug("ReadConfigValues called");
            OttoStashContainerData.AssignLocalValue(File.ReadAllText(yamlPath));
        }
        catch
        {
            OttoStashLogger.LogError($"There was an issue loading your {yamlFileName}");
            OttoStashLogger.LogError("Please check your entries for spelling and format!");
        }
    }

    private static void OnValChangedUpdate()
    {
        OttoStashLogger.LogDebug("OnValChanged called");
        try
        {
            YamlUtils.ReadYaml(OttoStashContainerData.Value);
            YamlUtils.ParseGroups();
        }
        catch (Exception e)
        {
            OttoStashLogger.LogError($"Failed to deserialize {yamlFileName}: {e}");
        }
    }

    private static byte[] ReadEmbeddedFileBytes(string name)
    {
        using MemoryStream stream = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
        return stream.ToArray();
    }

    // UnityEngine.ImageConversionModule cannot be referenced from net48 at all: its
    // metadata names ReadOnlySpan<byte>, and that type lives in the game's Mono
    // mscorlib rather than in the net48 reference assemblies. The byte[] overload of
    // LoadImage still exists, so bind it once at startup and drop the reference.
    // A netstandard2.1 target would resolve the span type and make this unnecessary.
    private static readonly MethodInfo? LoadImageMethod = AccessTools.Method(
        "UnityEngine.ImageConversion:LoadImage", [typeof(Texture2D), typeof(byte[])]);

    private static Texture2D loadTexture(string name)
    {
        Texture2D texture = new(0, 0);

        if (LoadImageMethod == null)
        {
            OttoStashLogger.LogError("UnityEngine.ImageConversion.LoadImage was not found. Textures will not load.");
            return texture;
        }

        LoadImageMethod.Invoke(null, [texture, ReadEmbeddedFileBytes("assets." + name)]);

        return texture!;
    }

    internal static Sprite loadSprite(string name)
    {
        Texture2D texture = loadTexture(name);
        if (texture != null)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }

        return null!;
    }


    #region ConfigOptions

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    internal static ConfigEntry<Toggle> DontStoreToBackpacks = null!;
    internal static ConfigEntry<Toggle> ChestsPickupFromGround = null!;
    internal static ConfigEntry<Toggle> MustHaveExistingItemToPull = null!;
    internal static ConfigEntry<KeyboardShortcut> SingleItemShortcut = null!;
    private static ConfigEntry<KeyboardShortcut> _storeShortcut = null!;
    private static ConfigEntry<KeyboardShortcut> _pauseShortcut = null!;
    internal static ConfigEntry<int> SecondsToWaitBeforeStoring = null!;
    internal static ConfigEntry<float> IntervalSeconds = null!;
    internal static ConfigEntry<Toggle> FishSuction = null!;
    internal static ConfigEntry<float> PlayerRange = null!;
    internal static ConfigEntry<float> FallbackRange = null!;
    internal static ConfigEntry<Toggle> PlayerIgnoreHotbar = null!;
    internal static ConfigEntry<Toggle> PlayerIgnoreQuickSlots = null!;
    internal static ConfigEntry<string> PingVfxString = null!;
    internal static ConfigEntry<Toggle> HighlightContainers = null!;
    internal static ConfigEntry<Toggle> PingContainers = null!;

    // Favoriting

    public static ConfigEntry<Color> BorderColorFavoritedItem = null!;
    public static ConfigEntry<Color> BorderColorFavoritedItemOnFavoritedSlot = null!;
    public static ConfigEntry<Color> BorderColorFavoritedSlot = null!;
    public static ConfigEntry<bool> DisplayTooltipHint = null!;
    public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind1 = null!;
    public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind2 = null!;
    public static ConfigEntry<KeyboardShortcut> SearchModifierKeybind = null!;

    public static ConfigEntry<string> FavoritedItemTooltip = null!;
    public static ConfigEntry<string> FavoritedSlotTooltip = null!;
    public static ConfigEntry<string> ItemOnFavoritedSlotTooltip = null!;

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(
                description.Description +
                (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(
                description.Description +
                (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc,
        bool synchronizedSetting = true)
    {
        ConfigurationManagerAttributes attributes = new()
        {
            CustomDrawer = ConfigExtensions.TextAreaDrawer
        };
        return TextEntryConfig(group, name, value, new ConfigDescription(desc, null, attributes), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order = null!;
        [UsedImplicitly] public bool? Browsable = null!;
        [UsedImplicitly] public string Category = null!;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
    }

    class AcceptableShortcuts : AcceptableValueBase
    {
        public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
        {
        }

        public override object Clamp(object value) => value;
        public override bool IsValid(object value) => true;

        public override string ToDescriptionString() =>
            "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
    }

    #endregion
}

public static class ToggleExtensions
{
    public static bool IsOn(this Toggle toggle)
    {
        return toggle == Toggle.On;
    }

    public static bool IsOff(this Toggle toggle)
    {
        return toggle == Toggle.Off;
    }
}

public static class KeyboardExtensions
{
    public static bool IsKeyDown(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }

    public static bool IsKeyHeld(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }
}

public static class ConfigExtensions
{
    internal static void TextAreaDrawer(ConfigEntryBase entry)
    {
        GUILayout.ExpandHeight(true);
        GUILayout.ExpandWidth(true);
        entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }
}