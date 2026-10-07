# 0001 - OttoStash absorbs Recycle_N_Reclaim, and steps aside while it is installed

- Status: accepted
- Date: 2026-10-07

## Context

Azumatt stopped publishing Recycle_N_Reclaim's source; the GitHub repository is gone
and new releases appear only on Hexium. The latest release (1.4.5) differs from the
last public source (1.4.4) only in its version strings. The mod is MIT licensed. Its
features (marking slots, acting on the player's inventory, a YAML rules file,
ServerSync) sit closest to OttoStash, which already absorbed AzuAutoStore the same way.

## Decision

OttoStash 3.6.0 carries every Recycle_N_Reclaim feature except Auga support and the
extra version handshake, under `Reclaiming/` and `Trashing/`.

- **Coexistence.** If `Azumatt.Recycle_N_Reclaim` is loaded, OttoStash applies none of
  the Harmony patches in those two namespaces and creates no reclaim UI. Storing keeps
  working. A soft `BepInDependency` makes Recycle_N_Reclaim load first so the check in
  `Awake` sees it.
- **Settings.** The new settings follow the PascalCase rule (Ottomation_ModLib ADR-0007 and ADR-0008) in
  sections `Reclaim`, `ReclaimUI`, `Discard`, `Trash` and `ReclaimDebug`. On the first
  run whose `.cfg` has no `[Reclaim]` section, the values in
  `Azumatt.Recycle_N_Reclaim.cfg` are copied onto them through a fixed rename table.
  `Azumatt.Recycle_N_Reclaim_ExcludeLists.yml` is copied to
  `potto007.OttoStash.Reclaim.yml` when that file does not exist. Old files are never
  written.
- **Rules.** The reclaim rules keep Recycle_N_Reclaim's file format and its own
  predefined groups. They are not merged with the storing groups, whose names differ
  (`Helmet` vs `Helmets`, `Ammo` vs `Ammunition`), so a carried-over file keeps its
  meaning.

## Consequences

Removing Recycle_N_Reclaim is the only step a player takes; nothing is lost if they
forget, because OttoStash then does nothing new. A player who changed a reclaim setting
in OttoStash while Recycle_N_Reclaim was still installed keeps that value, since the
carry-over runs once.

## What would reverse this

Azumatt resuming public development, in which case the features could move back out
and OttoStash would keep only the carry-over.
