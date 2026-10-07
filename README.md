![OttoStash - Everything In Its Place](https://raw.githubusercontent.com/potto007/OttoStash/main/docs/images/ottostash-title.png)

# OttoStash

### Updated for Valheim 1.0

**Version 3.6.0**, built and Harmony-checked against Valheim 1.0.17.

**Maintainer:** Paul Otto

--------------------

**Coming from AzuAutoStore?** Your settings carry over. On first run OttoStash looks
for `Azumatt.AzuAutoStore.cfg` and `Azumatt.AzuAutoStore.yml` and copies them into its
own `potto007.OttoStash.cfg` and `potto007.OttoStash.yml`. It never touches an
OttoStash file that already exists.

Two names changed with the rename:

- The console command is now `ottostashsearch`, not `azuautostoresearch`.
- The config files are now `potto007.OttoStash.cfg` and `potto007.OttoStash.yml`.

**Setting names are PascalCase.** Every section and setting uses one spelling, so
`1 - General` is now `General` and `Player Range` is now `PlayerRange`. On the first run
OttoStash renames them in `potto007.OttoStash.cfg`, and your values stay as they were.

**Coming from Recycle_N_Reclaim?** OttoStash now does everything it did: the Reclaim tab,
Reclaim All on containers, inventory discard, trash marking and undo. Remove
Recycle_N_Reclaim; while it is installed, OttoStash leaves reclaiming and trash marking
to it and only stores. On the first run with these features, and on every start while
Recycle_N_Reclaim is still installed, OttoStash copies the
settings in `Azumatt.Recycle_N_Reclaim.cfg` into new sections of `potto007.OttoStash.cfg`,
and `Azumatt.Recycle_N_Reclaim_ExcludeLists.yml` to `potto007.OttoStash.Reclaim.yml`.
Neither old file is changed. The settings now have PascalCase names under `Reclaim`,
`ReclaimUI`, `Discard`, `Trash` and `ReclaimDebug`; for example
`EnableExperimentalCraftingTabUI` is now `ReclaimUI.ReclaimTabEnabled`.

**Coming from AzuCraftyBoxes?** OttoStash now crafts and builds from nearby chests. On
first run it copies the settings in `Azumatt.AzuCraftyBoxes.cfg` into the `CraftFromChests`
section, and merges the container rules in `Azumatt.AzuCraftyBoxes.yml` into
`potto007.OttoStash.yml` as `pull:` blocks. Remove AzuCraftyBoxes afterwards: while it is
installed, OttoStash leaves crafting from chests to it. AzuCraftyBoxes' Use-to-fill on
smelters, kilns, fires and other stations is not part of OttoStash; OttoFuel fuels them.

--------------------

# Description

This mod pulls nearby items from the ground into containers. Optionally, use a hotkey to store items in your inventory
into containers. WardIsLove compatible, HotBar items can be ignored (on by default), Quickslots can be
ignored (`if turned on in the configuration!`).

## 1. Need to know

`ServerSync checks the version. A client running a different OttoStash version cannot join the server. A client without OttoStash can still join.`

`This mod uses ServerSync, if installed on the server and all clients, it will sync all configs to client`

`This mod uses a file watcher. If the configuration file is not changed with BepInEx Configuration manager, but changed in the file directly on the server, upon file save, it will sync the changes to all clients.`

## TL;DR

1. Drop this mod in, start a world.
2. Nearby dropped items are pulled into containers according to the YAML rules.
3. Press `.` to dump your inventory into nearby containers (respecting favorites and YAML rules).
4. Middle-click an item to store just that one.
5. Hold `Y` and click an item (or use `ottostashsearch`) to find where you put something.
6. Edit `potto007.OttoStash.yml` to define per-container ranges and allowed/excluded items.
7. At a crafting station, open the Reclaim tab to turn an item back into most of its resources.
8. Click Reclaim All twice on an open container to reclaim everything in it.
9. Hold `X` and click inventory slots to mark them as trash, then middle-click to discard them all.
10. Edit `potto007.OttoStash.Reclaim.yml` to keep items out of reclaiming and discarding.
11. Craft and build with what is in nearby chests. `Alt+O` switches that off and on for you.
12. All configuration files are found in the `BepInEx/config` folder. Examples are found in the yml files. 

## Compatibility

- **WardIsLove** – fully compatible; wards can protect containers as usual.
- **Quick Stack / sorting mods** (e.g. QuickStackStore) – UI integration is ordered to avoid grabbing each other’s
  elements and double buttons.
- **Backpack mods** (Smoothbrain’s Backpacks, AdventureBackpack, etc.) – supports storing *into* backpacks and has
  options to globally disable storing to any backpack containers.
- **ItemDrawers** – supports both KG’s fork and the original Makail version for storing and searching.
- **Epic Loot and Jewelcrafting** – reclaiming and discarding also return enchanting materials and socketed gems.
- **Recycle_N_Reclaim** – OttoStash replaces it. If both are installed, OttoStash turns its own reclaiming and
  trash marking off and leaves them to Recycle_N_Reclaim.

## Features

- Automatically store dropped resources into nearby containers within a configurable range
- Restrict specific items from being stored into containers by defining rules in the configuration file in
  the `BepInEx/config` folder called `potto007.OttoStash.yml`
- Pause the storing of items with `PauseShortcut` for `SecondsToWaitBeforeStoring` seconds
- Store a *single* hovered item into nearby containers with `StoreSingleItemShortcut` (Default: Mouse2 / Middle Click)
- Find where your items ended up: hold `SearchModifierKeybind` (Default: Y) and click an item, or use the `ottostashsearch`
  command to ping the nearest container holding it and see how many exist. You can use `/ottostashsearch` in the chat
  window. Auto complete for the command is possible so you can type `/ottostash` and press tab to complete if you don't
  want to type it all out :D
- `Favoriting from GoldenRevolver` By holding `FavoritingModifierKeybind1` (default: Z) or by using a new button, you can left
  click on an item to favorite it,
  or
  right click to favorite the slot it is in. This prevents the quick storing from player inventory from having the mod
  affect that item. No accidental
  storing something you didn't want. The favoriting state is shown with a custom colored border around the slot. If
  GoldenRevolver's mod is present, it will read his favoriting file and use that instead.

- **Craft and build from nearby chests.** Crafting and building take whatever you lack
  from containers within `PullRange` (20 m), plus kg drawers, the backpacks and gem bags
  you carry. The requirement lists show `have/need`, the amount flashes when chests make
  up the difference, and the build menu shows how many of a piece you can build. Private
  chests you cannot open, carts on the move and chests another player has open are
  skipped. A `pull:` block on a container or crafting station in the yml file keeps items
  from being pulled; see the end of the example file. `LeaveOneItem` keeps the last of
  each item in every chest. `Alt+O` switches pulling off for you alone, with a status
  icon while it is off. Epic Loot's enchanting table can use the chests too.

- **Armor stand panel.** Press Use on an armor stand and your inventory opens with the
  stand's slots beside it, laid out as a figure: head on top, hands either side of the
  chest, cape, legs and utility item along the bottom. Drag gear onto a slot or back
  into your bag, or hold Ctrl and click to move it in one go. Take All empties the
  stand into your inventory. Nothing lands on the ground. The hotbar keys still attach
  the vanilla way, and the panel waits for the stand to be yours to change before it
  moves anything, so two players cannot pull the same item. Turn `ArmorStandPanel` off to
  get the vanilla take-and-drop back.

- **Reclaiming, from Recycle_N_Reclaim.** A Reclaim tab sits next to Craft and Upgrade at every
  crafting station. It lists what you carry that the station could have made, and the craft button
  turns the selected item back into its resources, at `RecyclingRate` (half by default, rounded
  down; higher quality returns more). An item you need a better station for, or whose recipe you
  do not know, shows why it is blocked. Press `Ctrl+Z` within 20 seconds to undo the last reclaim.
  Turn on `ShowRecycleYieldInTooltip` to see the yield in every item tooltip.
- **Reclaim All.** An open container gets a Reclaim All button. Click it twice to reclaim
  everything in the container; anything that cannot be reclaimed is listed with the reason. Hold
  Left Ctrl and drag the button with the right mouse button to move it.
- **Discard and trash marking.** Drag an item and press `Delete` to destroy it and get its
  resources back. Hold `X` and click slots to mark them as trash (red border), then middle-click
  while still holding `X` to discard them all. Both are admin-only by default
  (`Discard.LockToAdmin`).
- **Reclaim rules.** `potto007.OttoStash.Reclaim.yml` keeps items or groups out of reclaiming, out
  of discarding, or out of Reclaim All for one kind of container, and sets per-item or per-group
  recycle rates.

Designed to be server-friendly: runs on configurable intervals, chunks bulk transfers, and throttles ownership requests
to avoid lag and race conditions.

Includes multiple protections against item loss and duplication, especially when teleporting, dying, or interacting with
many containers.

## FAQ

- Frequently asked questions will be added to the wiki tab of this mod as they are asked. Wiki tab is located at the top
  of this page.

## 2. Configuration (Collapsed due to length. Click to expand)

<details> <summary><b>Available Configuration Options</b></summary>




`General`

LockConfiguration [Synced with Server]

* If on, the configuration is locked and can be changed by server admins only.
    * Default Value: On

DontStoreToBackpacks [Synced with Server]

* If on, items will not be stored in backpacks.
    * Default Value: Off

ChestsPickupFromGround [Synced with Server]

* If on, chests will pick up items from the ground if they are in range. If off, chests will not begin their periodic
  checks for items nearby. Reloading zone or logging out might be required.
    * Default Value: On

MustHaveExistingItemToPull [Synced with Server]

* If on, the chest must already have the item in its inventory to pull it from the world or player into the chest.
    * Default Value: On

PlayerRange [Synced with Server]

* The maximum distance from the player to store items in chests when StoreShortcut is pressed. Follows storage
  rules for allowed items.
    * Default Value: 5

FallbackRange [Synced with Server]

* The range to use if the container has no range set in the yml file. This will be the fallback range for all
  containers.
    * Default Value: 10

PlayerIgnoreHotbar [Not Synced with Server]

* If on, the player's hotbar will not be stored when StoreShortcut is pressed.
    * Default Value: On

PlayerIgnoreQuickSlots [Not Synced with Server]

* If on, the player's quick slots will not be stored when StoreShortcut is pressed. (Requires Quick Slots mod, turn
  on only if you need it!)
    * Default Value: Off

PingVFX [Synced with Server]

* The VFX to play when a chest is pinged. Leave blank to disable and only highlight the chest. (Full prefab
  list: https://valheim-modding.github.io/Jotunn/data/prefabs/prefab-list.html)
    * Default Value: vfx_Potion_health_medium

HighlightContainers [Not Synced with Server]

* If on, the containers will be highlighted when something is stored in them. If off, the containers will not be
  highlighted if something is stored in them.
    * Default Value: On

PingContainers [Not Synced with Server]

* If on, the containers will be pinged with PingVFX when something is stored in them. If off, the containers will
  not be pinged if something is stored in them.
    * Default Value: On

SecondsToWaitBeforeStoring [Synced with Server]

* The number of seconds to wait before storing items into chests nearby automatically after you have pressed your hotkey
  to pause.
    * Default Value: 10

IntervalSeconds [Synced with Server]

* The number of seconds that must pass before the chest will do an automatic check for items nearby, WARNING: Reducing
  this will decrease performance!
    * Default Value: 10

`Fish`

FishSuction [Synced with Server]

* Allow auto-storing fish that are still in water, as in boat netting. Off requires fish to be out of the water. On
  skips that check.
    * Default Value: Off

`Shortcuts`

StoreSingleItemShortcut [Not Synced with Server]

* Keyboard shortcut/Hotkey to store a single item that you click from your inventory into nearby containers.
    * Default Value: Mouse2

StoreShortcut [Not Synced with Server]

* Keyboard shortcut/Hotkey to store your inventory into nearby containers.
    * Default Value: Period

PauseShortcut [Not Synced with Server]

* Keyboard shortcut/Hotkey to temporarily stop storing items into chests nearby automatically. Does not override the
  player hotkey store.
    * Default Value: Period + LeftShift

SearchModifierKeybind [Not Synced with Server]

* While holding this, you can search nearby chests for the prefab you clicked in your inventory.
    * Default Value: Y

`Favoriting`

BorderColorFavoritedItem [Not Synced with Server]

* Color of the border for slots containing favorited items.
    * Default Value: FFD800FF

BorderColorFavoritedItemOnFavoritedSlot [Not Synced with Server]

* Color of the border of a favorited slot that also contains a favorited item.
    * Default Value: 80AC80FF

BorderColorFavoritedSlot [Not Synced with Server]

* Color of the border for favorited slots.
    * Default Value: 0080FFFF

DisplayTooltipHint [Not Synced with Server]

* Whether to add additional info the item tooltip of a favorited or trash flagged item.
    * Default Value: true

FavoritingModifierKeybind1 [Not Synced with Server]

* While holding this, left clicking on items or right clicking on slots favorites them, disallowing storing Identical to
  FavoritingModifierKeybind2.
    * Default Value: Z

FavoritingModifierKeybind2 [Not Synced with Server]

* While holding this, left clicking on items or right clicking on slots favorites them, disallowing storing Identical to
  FavoritingModifierKeybind1.
    * Default Value: Z

FavoritedItemTooltip [Not Synced with Server]

*
    * Default Value: Item is favorited and will not be stored

FavoritedSlotTooltip [Not Synced with Server]

*
    * Default Value: Slot is favorited and will not be stored

ItemOnFavoritedSlotTooltip [Not Synced with Server]

*
    * Default Value: Item & Slot are favorited and will not be stored

`ArmorStands`

ArmorStandPanel [Synced with Server]

* If on, Use on an armor stand opens your inventory beside the slots of the stand. Drag gear onto a slot or back out, or hold Ctrl and click to move it in one go. The hotbar keys still attach the vanilla way. If off, Use takes the item and throws it on the ground, as in vanilla.
    * Default Value: On

`Reclaim`

ApplyCraftedBy [Synced with Server]

* If on, you are the crafter of every item that reclaiming or discarding gives back. If off, the crafter is left empty.
    * Default Value: On

RecyclingRate [Synced with Server]

* The share of an item's crafting resources that reclaiming returns, from 0 to 1. The amount always rounds down, so 2.5 becomes 2. At 0.5 a quality 1 item returns half of what it cost. Higher quality items return more, because their upgrades cost more. The recycleRates list in potto007.OttoStash.Reclaim.yml overrides this for single items or groups.
    * Default Value: 0.5

UnstackableItemsAlwaysReturnAtLeastOneResource [Synced with Server]

* If on, reclaiming an item that does not stack returns at least 1 of each resource that would otherwise round down to 0.
    * Default Value: On

RequireExactCraftingStationForRecycling [Synced with Server]

* If on, reclaiming needs the crafting station the item is made at, at the level its quality needs. If off, any station will do.
    * Default Value: On

ReturnEnchantedResources [Synced with Server]

* If on and Epic Loot or Jewelcrafting is installed, reclaiming also returns the enchanting materials or socketed gems.
    * Default Value: On

PreventZeroResourceYields [Synced with Server]

* If on, an item that would return 0 of any resource cannot be reclaimed, so nothing is lost to rounding.
    * Default Value: On

AllowRecyclingUnknownRecipes [Synced with Server]

* If on, you can reclaim items whose recipe you have not learned yet. Off by default, because it skips progression.
    * Default Value: Off

ShowRecycleYieldInTooltip [Not Synced with Server]

* If on, an item's tooltip lists what reclaiming it would return.
    * Default Value: false

UndoRecycleKeybind [Not Synced with Server]

* Undoes the last reclaim from the Reclaim tab, within the grace period.
    * Default Value: Z + LeftControl

UndoRecycleGracePeriodSeconds [Synced with Server]

* Seconds after a reclaim during which it can be undone. 0 turns undo off.
    * Default Value: 20

`ReclaimUI`

ReclaimTabEnabled [Synced with Server]

* If on, crafting stations get a Reclaim tab next to Craft and Upgrade.
    * Default Value: On

ContainerRecyclingEnabled [Synced with Server]

* If on, an open container shows a Reclaim All button. Click it twice to reclaim everything in the container.
    * Default Value: On

ContainerButtonPosition [Not Synced with Server]

* Where the Reclaim All button sits. Hold Left Ctrl and drag the button with the right mouse button to move it; the new position is saved here.
    * Default Value: {"x":496.0,"y":-374.0,"z":-1.0}

NotifyOnSalvagingImpediments [Synced with Server]

* If on, Reclaim All shows a message in the middle of the screen for every item it could not reclaim, and why: not enough free slots, an unknown recipe, or a resource that would return 0.
    * Default Value: On

HideRecipesForEquippedItems [Synced with Server]

* If on, items you have equipped are left out of the Reclaim tab.
    * Default Value: On

IgnoreItemsOnHotbar [Synced with Server]

* If on, items on your hotbar are left out of the Reclaim tab.
    * Default Value: On

RecipeListScrollSpeedMultiplier [Not Synced with Server]

* Multiplies how far the mouse wheel scrolls the crafting and Reclaim recipe lists. Newer Unity reports much smaller wheel steps, which made the vanilla speed crawl. 1 is vanilla speed. Raise it toward 20 if it still crawls, as it can on Linux and Proton.
    * Default Value: 10

StationFilterEnabled [Synced with Server]

* If on, items made at the stations in StationFilterList are left out of the Reclaim tab. It keeps food out of the list.
    * Default Value: On

StationFilterList [Synced with Server]

* Comma separated prefab names of the crafting stations whose items the Reclaim tab leaves out. The vanilla stations are forge, blackforge, piece_workbench, piece_cauldron, piece_stonecutter, piece_artisanstation and piece_magetable.
    * Default Value: piece_cauldron

`Discard`

Enabled [Synced with Server]

* If on, pressing DiscardHotkey while dragging an item in your inventory destroys it and returns its resources.
    * Default Value: On

LockToAdmin [Synced with Server]

* If on, only admins can discard from the inventory or bulk trash marked slots.
    * Default Value: On

DiscardHotkey [Not Synced with Server]

* Press while dragging an item to discard it.
    * Default Value: Delete

ReturnUnknownResources [Synced with Server]

* If on, discarding returns resources even when you do not know the item's recipe.
    * Default Value: Off

ReturnEnchantedResources [Synced with Server]

* If on and Epic Loot or Jewelcrafting is installed, discarding also returns the enchanting materials or socketed gems.
    * Default Value: On

ReturnResources [Synced with Server]

* The share of an item's resources that discarding returns, from 0 to 1. 0 destroys the item and returns nothing.
    * Default Value: 1

`Trash`

TrashingModifierKeybind [Not Synced with Server]

* While holding this, left clicking an inventory slot marks or unmarks it as trash. Letting go clears every mark.
    * Default Value: X

TrashingKeybind [Not Synced with Server]

* Pressed while holding TrashingModifierKeybind, discards every marked slot. The default is the middle mouse button.
    * Default Value: Mouse2

BorderColorTrashedSlot [Not Synced with Server]

* Color of the border on a slot marked as trash.
    * Default Value: FF0000FF

DisplayTooltipHint [Not Synced with Server]

* If on, a marked slot's tooltip says it is marked as trash.
    * Default Value: true

TrashedSlotTooltip [Not Synced with Server]

* The tooltip line on a slot marked as trash.
    * Default Value: Slot is marked as trash and will be discarded

`ReclaimDebug`

DebugAlwaysDumpAnalysisContext [Synced with Server]

* If on, every reclaim writes a full report to the log. Slow; turn it on only to chase a problem.
    * Default Value: Off

DebugAllowSpammyLogs [Synced with Server]

* If on, every yield calculation is written to the log. Very noisy and slow.
    * Default Value: Off

`CraftFromChests`

PullFromChests [Synced with Server]

* If on, crafting and building take the materials you lack from containers within PullRange, and the crafting and build menus count them.
    * Default Value: On

PullRange [Synced with Server]

* The maximum distance from the player to a container that crafting and building may take materials from.
    * Default Value: 20

LeaveOneItem [Synced with Server]

* If on, pulling leaves one of each item in every container, so the container still has it and keeps storing it.
    * Default Value: Off

TogglePullingShortcut [Not Synced with Server]

* Keyboard shortcut/Hotkey that switches pulling off and on for you alone.
    * Default Value: O + LeftAlt

TogglePullingMessage, TogglePullingMessageFormat, PullingOffStatusEffect [Not Synced with Server]

* The message above your head after the toggle, its format, and the status icon while pulling is off.

RequirementFormat [Not Synced with Server]

* How requirements read while pulling is on. {0} is what you have, carried and nearby; {1} is what is needed. Empty keeps the vanilla amount.
    * Default Value: {0}/{1}

FlashColor, UnflashColor, CanBuildColor, CannotBuildColor [Not Synced with Server]

* The requirement flash colors, and the colors of the build menu's buildable count.

</details>


<details>
<summary><b>Installation Instructions</b></summary>

### Manual Installation

`Note: (Manual installation is likely how you have to do this on a server, make sure BepInEx is installed on the server correctly)`

1. **Download the latest release of BepInEx.**
2. **Extract the contents of the zip file to your game's root folder.**
3. **Download the latest release of OttoStash from Thunderstore.io.**
4. **Extract the contents of the zip file to the `BepInEx/plugins` folder.**
5. **Launch the game.**

### Installation through r2modman or Thunderstore Mod Manager

1. **Install [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/)
   or [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager).**

   > For r2modman, you can also install it through the Thunderstore site.
   ![](https://i.imgur.com/s4X4rEs.png "r2modman Download")

   > For Thunderstore Mod Manager, you can also install it through the Overwolf app store
   ![](https://i.imgur.com/HQLZFp4.png "Thunderstore Mod Manager Download")
2. **Open the Mod Manager and search for "OttoStash" under the Online
   tab.**
   The image below shows VikingShip as an example, but it was easier to reuse the image. Type OttoStash.

![](https://i.imgur.com/5CR5XKu.png)

3. **Click the Download button to install the mod.**
4. **Launch the game.**

</details>

Example: make a chest that only ever pulls Food & Potions:

```yaml
piece_chest:
  range: 10
  exclude:
    - All
  includeOverride:
    - Food
    - Potion
```

<details><summary><b>Example YAML</b></summary>

```yaml
# Below you can find example groups. Groups are used to exclude or includeOverride quickly. They are reusable lists! 
# Please note that some of these groups/container limitations are kinda pointless but are here for example.
# Make sure to follow the format of the example below. If you have any questions, please ask in my discord.

# Full vanilla prefab name list: https://valheim-modding.github.io/Jotunn/data/prefabs/prefab-list.html
# Item prefab name list: https://valheim-modding.github.io/Jotunn/data/objects/item-list.html

# There are several predefined groups set up for you that are not listed. You can use these just like you would any group you create yourself.
# These are the "All", "Food", "Potion", "Fish", "Swords", "Bows", "Crossbows", "Axes", "Clubs", "Knives", "Pickaxes", "Polearms", "Spears", "Equipment", "Boss Trophy", "Trophy", "Crops", "Seeds", "Ores", "Metals", and "Woods" groups.
# The criteria for these groups are as follows:
# groups:
#   Food:
#     - Criteria: Both of the following properties must have a value greater than 0.0 on the sharedData property of the ItemDrop script:
#         - food
#         - foodStamina
#   Potion:
#     - Criteria: The following properties must meet the specified conditions on the sharedData property of the ItemDrop script:
#         - 'food' > 0.0
#         - 'foodStamina' == 0.0
#         - Any status effect names/categories contain "potion"
#   Fish:
#     - itemType: Fish
#   Swords, Bows, Crossbows, Axes, Clubs, Knives, Pickaxes, Polearms, Spears:
#     - itemType: OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bow
#     - Criteria: Items in these groups have a specific skillType on the sharedData property of the ItemDrop script. Each group corresponds to the skillType as follows:
#         - Swords: skillType == Skills.SkillType.Swords
#         - Bows: skillType == Skills.SkillType.Bows
#         - Crossbows: skillType == Skills.SkillType.Crossbows
#         - Axes: skillType == Skills.SkillType.Axes
#         - Clubs: skillType == Skills.SkillType.Clubs
#         - Knives: skillType == Skills.SkillType.Knives
#         - Pickaxes: skillType == Skills.SkillType.Pickaxes
#         - Polearms: skillType == Skills.SkillType.Polearms
#         - Spears: skillType == Skills.SkillType.Spears
#            Example:   An item with itemType set to OneHandedWeapon and skillType set to Skills.SkillType.Swords would belong to the Swords group.
#   Armor:
#     - itemType: Chest, Legs, Shoulder, Helmet
#   Equipment:
#     - itemType: Torch, Bow, OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Helmet, Chest, Legs, Shoulder, Utility, Shield
#   Weapons:
#     - itemType: OneHandedWeapon, TwoHandedWeapon, TwoHandedWeaponLeft, Bows, Swords, Crossbows, Axes, Clubs, Knives, Pickaxes, Polearms, Spears
#   Shield:
#     - itemType: Shield
#   Round Shield:
#     - itemType: Shield
#     - Criteria: sharedData.m_timedBlockBonus > 0.0 "round"
#   Tower Shield:
#     - itemType: Shield
#     - Criteria: sharedData.m_timedBlockBonus <= 0.0 "tower"
#   Chest:
#     - itemType: Chest
#   Legs:
#     - itemType: Legs
#   Shoulder:
#     - itemType: Shoulder
#   Helmet:
#     - itemType: Helmet
#   Utility:
#     - itemType: Utility
#   Trinket:
#     - itemType: Trinket
#   Ammo:
#     - itemType: Ammo
#   Arrows:
#     - itemType: Ammo
#     - Criteria: sharedData.m_ammoType == "$ammo_arrows"
#   Bolts:
#     - itemType: Ammo
#     - Criteria: sharedData.m_ammoType == "$ammo_bolts"
#   ElementalMagic:
#     - itemType: ElementalMagic
#   BloodMagic:
#     - itemType: BloodMagic
#   Boss Trophy:
#     - itemType: Trophy
#     - Criteria: sharedData.m_name ends with any of the following boss names:
#         - eikthyr, elder, bonemass, dragonqueen, goblinking, SeekerQueen
#   Trophy:
#     - itemType: Trophy
#     - Criteria: sharedData.m_name does not end with any boss names
#   Crops:
#     - itemType: Material
#     - Criteria: Can be cultivated and grown into a pickable object with an amount greater than 1
#   Seeds:
#     - itemType: Material
#     - Criteria: Can be cultivated and grown into a pickable object with an amount equal to 1
#   Ores:
#     - itemType: Material
#     - Criteria: Can be processed by any of the following smelters:
#         - smelter
#         - blastfurnace
#   Metals:
#     - itemType: Material
#     - Criteria: Is the result of processing an ore in any of the following smelters:
#         - smelter
#         - blastfurnace
#   Woods:
#     - itemType: Material
#     - Criteria: Can be processed by the charcoal_kiln smelter
#   All:
#     - Criteria: Item has an ItemDrop script and all needed fields are populated. (all items)




groups:
  BronzeGear: # Group name
    - ArmorBronzeChest # Item prefab name, note that this is case sensitive and must be the prefab name
    - ArmorBronzeLegs
    - ArmorBronzeHelmet
  Tier 2 Items:
    - Bronze
    - PickaxeBronze
    - ArmorBronzeChest
    - ArmorBronzeLegs


# By default, if you don't specify a container below, it will be considered as you want to allow storing all objects into it.
# If you are having issues with a container, please make sure you have the full prefab name of the container. Additionally, make sure you have range and exclude or includeOverride set up correctly.
# Worst case you can define a container like this. This will allow everything to be pulled from the container within a range of 30.
# rk_barrel:  
#   range: 30
#  includeOverride: []

## Please note that the below containers are just examples. You can add as many containers as you want.
## If you want to add a new container, just copy and paste the below example and change the name of the container to the prefab name of the container you want to add.
## The values are set up to include everything by using the includeOverride (aside from things that aren't really a part of vanilla recipes, like Swords or Bows). 
## This is to give you examples on how it's done, but still allow everything to be stored into the container.

Player_tombstone: # This is to exclude the tombstone from randomly picking up items that fall near it.
  range: 10
  exclude:
    - All

Player: # This is to exclude backpacks from randomly storing items into them.
  range: 10
  exclude:
    - All

piece_chest:
  range: 10 # This is the range that the container will store items. This overrides the global range set in the config.
  exclude: # Exclude these items from being able to be stored into the container
    #- Food # Exclude all in group
    - PickaxeBronze # Allow prefab names as well, in this case we will use something that isn't a food
  includeOverride:
    # - Food # This would not work, you cannot includeOverride a group that is excluded. You can only override prefabs from that group.
    - PickaxeBronze # You can however, be weird, and override a prefab name you have excluded.

# It's highly unlikely that you will need the armor, swords, bows, etc. groups below. These are just in case you want to use them. 
# They were also easy ways for me to show you how to use the groups without actually excluding something you might want to always pull by default.

piece_chest_wood:
  range: 10
  exclude:
    - Swords # Exclude all in group
    - Tier 2 Items # Exclude all in group
    - Bows # Exclude all in group
  includeOverride: # If the item is in the groups above, say, you were using a predefined group but want to override just one item to be ignored and allow pulling it
    - BowFineWood
    - Wood
    - Bronze
    - PickaxeBronze
    - ArmorBronzeChest
    - ArmorBronzeLegs

piece_chest_private:
  range: 10
  exclude:
    - All # Exclude everything

piece_chest_blackmetal:
  range: 10
  exclude:
    - Swords # Exclude all in group
    - Tier 2 Items # Exclude all in group
    - Bows # Exclude all in group
  includeOverride: # If the item is in the groups above, say, you were using a predefined group but want to override just one item to be ignored and allow storing it
    - BowFineWood
    - Wood
    - Bronze

rk_cabinet: # rk_ is typically the prefix for containers coming from RockerKitten's mods
  range: 10
  exclude:
    - Food
  includeOverride:
    - Food

rk_cabinet2:
  range: 10
  exclude:
    - Food
  includeOverride:
    - Food

rk_barrel:
  range: 10
  exclude:
    - Armor
    - Swords

rk_barrel2:
  range: 10
  exclude:
    - Armor
    - Swords

rk_crate:
  range: 10
  exclude:
    - Armor
    - Swords

rk_crate2:
  range: 10
  exclude:
    - Armor
    - Swords
```

</details>

--------------------

## Version Information

The full release history lives in [CHANGELOG.md](Thunderstore/CHANGELOG.md), and it
renders on the Changelog tab of the Thunderstore package page.

## Credits

OttoStash is maintained by **Paul Otto**.

Reclaiming, discard and trash marking come from Azumatt's
[Recycle_N_Reclaim](https://thunderstore.io/c/valheim/p/Azumatt/Recycle_N_Reclaim/), MIT licensed,
which grew out of OdinsInventoryDiscard and took contributions from MidnightsFX, Vapok,
shudnal and Goldenrevolver.
