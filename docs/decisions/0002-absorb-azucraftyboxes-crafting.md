# 0002 - OttoStash absorbs AzuCraftyBoxes' crafting from chests, without its station filling

- Status: accepted
- Date: 2026-10-07

## Context

AzuCraftyBoxes (Azumatt, MIT) lets crafting and building spend materials from nearby
containers. Paul's fork sits at 1.8.19. It does two jobs: crafting and building draw from
chests, and pressing Use on a smelter, kiln, fire, oven, fermenter, shield generator or
ballista tops it up from chests. OttoStash already finds containers by range, runs the
same exclude/includeOverride YAML rules with the same groups, and has the multiplayer
gate for chests another player has open. OttoFuel already fuels most of those stations
on its own.

## Decision

OttoStash carries the crafting and building half, under `Pulling/`.

- **Scope.** Crafting, building, the requirement counts in the crafting panel and build
  HUD, the build menu's buildable count, the personal on/off hotkey with its status
  effect, Leave One Item, kg drawers, Backpacks, Jewelcrafting gem bags, and the Epic
  Loot enchanting table. The station-filling patches stay out, so OttoStash and OttoFuel
  never patch the same add-fuel paths. AzuCraftyBoxes' public API class is dropped; no
  Otto mod calls it.
- **Rules.** One rules file. A container or crafting station entry in
  `potto007.OttoStash.yml` takes a `pull:` block with its own exclude/includeOverride
  lists, separate from the store lists beside it. A station's block applies to every
  source while the player works there.
- **Sources.** Chests come from the autostore registry, then are filtered at query time:
  private-chest access, a cart in motion, and `ChestGate.IsOpenElsewhere`. A take claims
  the chest's ownership first, the same as a store.
- **Paying.** The patches run after vanilla and act only where vanilla said no. Consuming
  pulls only the shortfall and lets vanilla remove what the player carries. A
  one-ingredient recipe is paid inside the single `Inventory.RemoveItem` call that
  DoCrafting makes after its checks pass.
- **Coexistence.** With AzuCraftyBoxes, CraftFromContainers or CFCMod loaded, every pull
  patch returns early. The other mod keeps the job and OttoStash logs a warning. The
  check reads `Chainloader.PluginInfos` in `Start`, when every plugin is listed, so no
  load-order dependency is needed.
- **Settings.** A `CraftFromChests` section with PascalCase keys (Ottomation_ModLib
  ADR-0007 and ADR-0008). On the first run whose `.cfg` has no `[CraftFromChests]`
  section, values in `Azumatt.AzuCraftyBoxes.cfg` are copied over through a fixed table.
  The character's on/off flag keeps AzuCraftyBoxes' custom-data key, `ACB_PreventPulling`.
- **Rules carry-over.** `Azumatt.AzuCraftyBoxes.yml` is merged into the OttoStash rules
  file once, as text, so its comments survive: a container both files name gains a
  `pull:` block at the block's own indent, a container only AzuCraftyBoxes named is
  appended, entries with empty lists are dropped, and a group both files define keeps its
  OttoStash members. A marker comment stops a second merge. The merged text must parse
  before it is written.

## Consequences

Players who used AzuCraftyBoxes only for crafting lose nothing once they remove it.
Players who used Use-to-fill on stations lose that and need OttoFuel instead.

AzuCraftyBoxes 1.8.19 had three bugs that this port fixes as a side effect, which changes
behaviour a player may have relied on. Its consume path ignored the pull rules, so an
excluded item was still spent. Its one-ingredient craft could spend the item it had
queued for a different recipe, or spend it when the craft failed. Its menus counted items
the rules excluded.

Upstream AzuCraftyBoxes 1.8.20-1.8.27 is not part of this port.

## What would reverse this

A player asking for station filling back. That belongs in OttoFuel, not here.
