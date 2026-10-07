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
[BepInDependency("org.bepinex.plugins.jewelcrafting", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
public class OttoStashPlugin : BaseUnityPlugin
{
    internal const string ModName = "OttoStash";
    internal const string ModVersion = "3.5.1";
    internal const string Author = "potto007";
    internal const string ModGUID = $"{Author}.{ModName}";
    internal const string KgGuid = "kg.ItemDrawers";
    internal const string BackpacksGuid = "org.bepinex.plugins.backpacks";
    private const string ConfigFileName = ModGUID + ".cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    private readonly Harmony _harmony = new(ModGUID);
    internal static readonly ManualLogSource LogSource = BepInEx.Logging.Logger.CreateLogSource(ModName);
    private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion, ModRequired = false };
    internal static bool BackpacksIsLoaded = false;
    internal static readonly string yamlFileName = $"{ModGUID}.yml";
    internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
    internal static readonly CustomSyncedValue<string> OttoStashContainerData = new(ConfigSync, "ottostashData", "");
    internal static readonly CustomSyncedValue<string> CraftyContainerGroupsData = new(ConfigSync, "ottostashGroupsData", "");

    //
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
                LogSource.LogInfo($"Carried your AzuAutoStore settings over to {ConfigFileName}.");
            }

            string oldYaml = Paths.ConfigPath + Path.DirectorySeparatorChar + "Azumatt.AzuAutoStore.yml";
            if (File.Exists(oldYaml) && !File.Exists(yamlPath))
            {
                File.Copy(oldYaml, yamlPath);
                LogSource.LogInfo($"Carried your AzuAutoStore container rules over to {yamlFileName}.");
            }
        }
        catch (Exception e)
        {
            // A failure here must not stop the mod from loading.
            LogSource.LogWarning($"Could not carry the AzuAutoStore configuration over: {e.Message}");
        }
    }

    public void Awake()
    {
        self = this;

        CarryOverAzuAutoStoreConfig();
        string oldCraftyConfig = Paths.ConfigPath + Path.DirectorySeparatorChar + PullCarryOver.OldConfigName;
        bool carryOverCraftyConfig = File.Exists(oldCraftyConfig) && !ConfigHasSection(PullSection);

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

        // dark-ish green
        BorderColorFavoritedItemOnFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedItemOnFavoritedSlot), new Color(0.5f, 0.67413795f, 0.5f), "Color of the border of a favorited slot that also contains a favorited item.", false);

        // light-ish blue
        BorderColorFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedSlot), new Color(0f, 0.5f, 1f), "Color of the border for favorited slots.", false);

        DisplayTooltipHint = config(sectionName, nameof(DisplayTooltipHint), true, "Whether to add additional info the item tooltip of a favorited or trash flagged item.", false);

        FavoritingModifierKeybind1 = config(sectionName, nameof(FavoritingModifierKeybind1), new KeyboardShortcut(KeyCode.Z), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind2)}.", false);
        FavoritingModifierKeybind2 = config(sectionName, nameof(FavoritingModifierKeybind2), new KeyboardShortcut(KeyCode.Z), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind1)}.", false);
        FavoritedItemTooltip = config(sectionName, nameof(FavoritedItemTooltip), "Item is favorited and will not be stored", string.Empty, false);
        FavoritedSlotTooltip = config(sectionName, nameof(FavoritedSlotTooltip), "Slot is favorited and will not be stored", string.Empty, false);
        ItemOnFavoritedSlotTooltip = config(sectionName, nameof(ItemOnFavoritedSlotTooltip), "Item & Slot are favorited and will not be stored", string.Empty, false);

        // BepInEx saves an apostrophe as \', which breaks TOML formatters, so these
        // defaults no longer use one. A value still on the old default moves to the new
        // text. A value the player wrote stays as it is.
        ReplaceOldDefault(FavoritedItemTooltip, "Item is favorited and won't be stored");
        ReplaceOldDefault(FavoritedSlotTooltip, "Slot is favorited and won't be stored");
        ReplaceOldDefault(ItemOnFavoritedSlotTooltip, "Item & Slot are favorited and won't be stored");

        ArmorStandPanel = config("4 - Armor Stands", "Armor Stand Panel", Toggle.On, new ConfigDescription("If on, Use on an armor stand opens your inventory beside the slots of the stand. Drag gear onto a slot or back out, or hold Ctrl and click to move it in one go. The hotbar keys still attach the vanilla way. If off, Use takes the item and throws it on the ground, as in vanilla."));

        PullEnabled = config(PullSection, "Pull From Chests", Toggle.On, new ConfigDescription("If on, crafting and building take the materials you lack from containers within Pull Range, and the crafting and build menus count them. Add a pull block to a container or crafting station in the yml file to keep items from being pulled."));
        PullRange = config(PullSection, "Pull Range", 20f, new ConfigDescription("The maximum distance from the player to a container that crafting and building may take materials from."));
        LeaveOneItem = config(PullSection, "Leave One Item", Toggle.Off, new ConfigDescription("If on, pulling leaves one of each item in every container, so the container still has it and keeps storing it."));
        TogglePullingShortcut = config(PullSection, "Toggle Pulling Shortcut", new KeyboardShortcut(KeyCode.O, KeyCode.LeftAlt), new ConfigDescription("Keyboard shortcut/Hotkey that switches pulling off and on for you alone. While it is off, crafting and building use only what you carry.", new AcceptableShortcuts()), false);
        PullToggleMessage = config(PullSection, "Toggle Pulling Message", Toggle.On, new ConfigDescription("If on, a message above your head says whether pulling is on after you press the Toggle Pulling Shortcut."), false);
        PullToggleMessageFormat = config(PullSection, "Toggle Pulling Message Format", "<size=30><color=#ffffff>{0}</color></size>\n<size=25>{1}</size>", new ConfigDescription("Format of the toggle message. {0} is replaced by the message and {1} by On or Off."), false);
        PullOffStatusEffect = config(PullSection, "Pulling Off Status Effect", Toggle.On, new ConfigDescription("If on, a status effect icon shows while you have pulling switched off."), false);
        RequirementFormat = config(PullSection, "Requirement Format", "{0}/{1}", new ConfigDescription("How the crafting and build menus show each requirement while pulling is on. {0} is replaced by how many you have, carried and nearby, and {1} by how many are needed. Leave it empty to keep the vanilla amount."), false);
        FlashColor = config(PullSection, "Flash Color", Color.yellow, new ConfigDescription("A requirement amount flashes to this color when the nearby containers make up what you do not carry."), false);
        UnflashColor = config(PullSection, "Unflash Color", Color.white, new ConfigDescription("A requirement amount flashes from this color when the nearby containers make up what you do not carry. Set both colors the same for no flashing."), false);
        CanBuildColor = config(PullSection, "Can Build Color", Color.green, new ConfigDescription("Color of the build menu's count of how many of a piece you can build."), false);
        CannotBuildColor = config(PullSection, "Cannot Build Color", Color.red, new ConfigDescription("Color of the build menu's count when you cannot build a piece."), false);

        if (carryOverCraftyConfig)
        {
            PullCarryOver.CarryOverConfig(oldCraftyConfig, [
                (PullEnabled, "1 - General", "Mod Enabled"),
                (PullRange, "2 - CraftyBoxes", "Container Range"),
                (LeaveOneItem, "2 - CraftyBoxes", "Leave One Item"),
                (RequirementFormat, "2 - CraftyBoxes", "ResourceCostString"),
                (FlashColor, "2 - CraftyBoxes", "FlashColor"),
                (UnflashColor, "2 - CraftyBoxes", "UnFlashColor"),
                (CanBuildColor, "2 - CraftyBoxes", "Can Build Color"),
                (CannotBuildColor, "2 - CraftyBoxes", "Cannot Build Color"),
                (TogglePullingShortcut, "3 - Keys", "Prevent Pulling Logic"),
                (PullToggleMessage, "1 - General", "Prevent Pulling Message"),
                (PullToggleMessageFormat, "1 - General", "Prevent Pulling Format"),
                (PullOffStatusEffect, "1 - General", "Prevent Pulling Status"),
            ]);
            LogSource.LogInfo($"Carried your AzuCraftyBoxes settings over to the {PullSection} section of {ConfigFileName}.");
        }

        if (!File.Exists(yamlPath))
        {
            WriteConfigFileFromResource(yamlPath);
        }

        CarryOverAzuCraftyBoxesRules();

        OttoStashContainerData.ValueChanged += OnValChangedUpdate; // check for file changes
        OttoStashContainerData.AssignLocalValue(File.ReadAllText(yamlPath));

        AutoDoc();
        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();
    }

    public void Start()
    {
        FavoritingPatches.BorderSprite = LoadSprite("border.png");
        PullStatusEffect.Create();
        Pull.FindConflictingMod();
        EpicLootCompat.Init();
        if (Chainloader.PluginInfos.ContainsKey(BackpacksGuid))
        {
            BackpacksIsLoaded = true;
        }
        
        if (!MUCCompat.MUCLoaded)
        {
            MUCCompat.ForceEnableMUC(true);
        }
    }

    private static bool ConfigHasSection(string section)
    {
        try
        {
            return File.Exists(ConfigFileFullPath) && File.ReadAllText(ConfigFileFullPath).Contains($"[{section}]");
        }
        catch (Exception e)
        {
            LogSource.LogWarning($"Could not read {ConfigFileName}: {e.Message}");
            return true;
        }
    }

    /// Writes the AzuCraftyBoxes container rules into the OttoStash rules file as
    /// pull blocks, once. A failure leaves the rules file as it was.
    private static void CarryOverAzuCraftyBoxesRules()
    {
        string oldRules = Paths.ConfigPath + Path.DirectorySeparatorChar + PullCarryOver.OldRulesName;
        if (!File.Exists(oldRules))
            return;

        try
        {
            List<string> notes = new();
            string? merged = PullCarryOver.Merge(File.ReadAllText(yamlPath), File.ReadAllText(oldRules), notes);
            foreach (string note in notes)
                LogSource.LogWarning($"Carrying {PullCarryOver.OldRulesName} over: {note}");
            if (merged == null)
                return;

            // Never write a file the rule reader cannot read back.
            ContainerRules.Read(merged);
            File.WriteAllText(yamlPath, merged);
            LogSource.LogInfo($"Carried your AzuCraftyBoxes container rules over to {yamlFileName} as pull blocks.");
        }
        catch (Exception e)
        {
            LogSource.LogWarning($"Could not carry the AzuCraftyBoxes container rules over: {e.Message}");
        }
    }

    private static void ReplaceOldDefault(ConfigEntry<string> entry, string oldDefault)
    {
        if (entry.Value == oldDefault)
        {
            entry.Value = (string)entry.DefaultValue;
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
                LogSource.LogError("Taking input");
#endif
            PlayerStore.StoreAll();
        }

        if (_pauseShortcut.Value.IsDown() && Player.m_localPlayer.TakeInput())
        {
            StorePause.Toggle();
        }

        if (TogglePullingShortcut.Value.IsKeyDown() && Player.m_localPlayer.TakeInput())
        {
            PullSwitch.Toggle(Player.m_localPlayer);
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
            StashLog.Debug("ReadConfigValues called");
            Config.Reload();
        }
        catch
        {
            StashLog.Error($"There was an issue loading your {ConfigFileName}");
            StashLog.Error("Please check your config entries for spelling and format!");
        }
    }

    private void ReadYamlFiles(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(yamlPath)) return;
        try
        {
            LogSource.LogDebug("ReadConfigValues called");
            OttoStashContainerData.AssignLocalValue(File.ReadAllText(yamlPath));
        }
        catch
        {
            LogSource.LogError($"There was an issue loading your {yamlFileName}");
            LogSource.LogError("Please check your entries for spelling and format!");
        }
    }

    private static void OnValChangedUpdate()
    {
        LogSource.LogDebug("OnValChanged called");
        try
        {
            ContainerRules.Read(OttoStashContainerData.Value);
            ContainerRules.ParseGroups();
        }
        catch (Exception e)
        {
            LogSource.LogError($"Failed to deserialize {yamlFileName}: {e}");
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

    private static Texture2D LoadTexture(string name)
    {
        Texture2D texture = new(0, 0);

        if (LoadImageMethod == null)
        {
            LogSource.LogError("UnityEngine.ImageConversion.LoadImage was not found. Textures will not load.");
            return texture;
        }

        LoadImageMethod.Invoke(null, [texture, ReadEmbeddedFileBytes("assets." + name)]);

        return texture!;
    }

    internal static Sprite LoadSprite(string name)
    {
        Texture2D texture = LoadTexture(name);
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

    // Armor stands

    internal static ConfigEntry<Toggle> ArmorStandPanel = null!;

    // Crafting from chests

    private const string PullSection = "5 - Crafting From Chests";
    internal static ConfigEntry<Toggle> PullEnabled = null!;
    internal static ConfigEntry<float> PullRange = null!;
    internal static ConfigEntry<Toggle> LeaveOneItem = null!;
    internal static ConfigEntry<KeyboardShortcut> TogglePullingShortcut = null!;
    internal static ConfigEntry<Toggle> PullToggleMessage = null!;
    internal static ConfigEntry<string> PullToggleMessageFormat = null!;
    internal static ConfigEntry<Toggle> PullOffStatusEffect = null!;
    internal static ConfigEntry<string> RequirementFormat = null!;
    internal static ConfigEntry<Color> FlashColor = null!;
    internal static ConfigEntry<Color> UnflashColor = null!;
    internal static ConfigEntry<Color> CanBuildColor = null!;
    internal static ConfigEntry<Color> CannotBuildColor = null!;

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