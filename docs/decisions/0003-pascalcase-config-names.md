# 0003 - Every config name is PascalCase, and OttoStash migrates the player's file itself

- Status: accepted
- Date: 2026-10-07

## Context

3.6.0 added the `Reclaim`, `ReclaimUI`, `Discard`, `Trash`, `ReclaimDebug` and
`CraftFromChests` sections with PascalCase keys (ADR 0002). The settings OttoStash
inherited from AzuAutoStore kept their spaced, numbered names, such as `1 - General` and
`Player Range`, so one file held two spellings.

The spaced names were not kept by choice. 3.2.0 moved to PascalCase through
Ottomation_ModLib, which renamed the player's file in place (Ottomation_ModLib ADR-0007 and
ADR-0008). 3.2.1 dropped the library and lost the migration with it, so it went back to the
spaced names, the only way a config from 3.1.3 or earlier would still load.

## Decision

Every section and key is PascalCase, with no section ordinal. The `config` and
`TextEntryConfig` helpers bind through `ConfigName.Section` and `ConfigName.Key`, and the
names in `Plugin.cs` are already written that way. `ConfigNameMigration.Apply` runs in
`Awake` after the AzuAutoStore carry-over and before the first bind.

Both files under `Configuration/` are vendored from Ottomation_ModLib, the same way
OttoAura carries them. The migration differs in one rule. When a file holds a setting under
both spellings, the library keeps the PascalCase line, because there the spaced one can only
come from a downgrade. OttoStash keeps the spaced line. 3.2.0 wrote the PascalCase line and
3.2.1 through 3.5.1 wrote the spaced one later, and BepInEx keeps a line it no longer binds,
so the spaced value is the one the player last saw. The same holds after a downgrade from
3.6.0, since a pre-3.6.0 build writes only spaced lines.

## Consequences

A player upgrading from any earlier release keeps their values. A file is rewritten once,
with a backup that is put back if the reload fails, and a failure never stops the plugin
from loading.

A player who changed a setting under 3.2.0 and never touched it again gets the 3.2.1 value,
which is what they have run since 3.2.1.

Configuration Manager lists the sections in bind order unless its sort-by-name option is on,
and then alphabetically.

The `Azumatt.AzuCraftyBoxes.cfg` and `Azumatt.Recycle_N_Reclaim.cfg` carry-overs read
those mods' own files under their own names and are not affected.

## What would reverse this

A player losing settings to the migration. Then drop the in-place rewrite and bind both
spellings for a release.
