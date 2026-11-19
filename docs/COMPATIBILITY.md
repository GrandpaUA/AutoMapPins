# Configuration and optional mods

The default YAML is BioHazardN7's contributed configuration: **94 categories, 78 named custom icons, and 10 native/location icon overrides**. The contributor's `isActive` choices are preserved: some categories are configured but disabled. Enable only the categories you want. Cartur PNGs are included; installing Cartur's separate pin-generating mod is not required.

Only BepInEx is a mandatory package dependency. Additional object IDs have no effect when their supplying mod is absent. Source/config matching below establishes what the configuration targets; it is not a claim that every combination has passed gameplay tests on the current candidate.

| Mod | What this configuration supports | Evidence and limits |
| --- | --- | --- |
| Valheim | Vanilla resources, locations, plants, chests, spawners, camps, and other configured objects | Full candidate gameplay still needs verification against the player's game version. The local release build was checked against Valheim 1.0.21. |
| [Monstrum — Therzie](https://thunderstore.io/c/valheim/p/Therzie/Monstrum/) | `AltarAsmodeus_TW`, `AltarSvalt_TW`, `AltarVrykolathas_TW` and their three `Vegvisir_location_*_TW` entries | Exact IDs are listed by the mod author. The vegvisir categories are off by default; the altar categories are on. |
| [Wizardry — Therzie](https://thunderstore.io/c/valheim/p/Therzie/Wizardry/) | Four `Pickable_Mushroom_*_TW` entries: Witcheye, Deathcap, Wizardbutter, and Devilstongue | The [author's wiki](https://github.com/Therzie/therzie-wiki/tree/main/src/data/items) identifies these four mushrooms as Wizardry items. The configured pickable-object IDs need confirmation in a running installation of the exact plugin version. |
| [Jewelcrafting — Smoothbrain](https://github.com/blaxxun-boop/Jewelcrafting) | Nine `Raw_*_Gemstone` entries, `JC_Crystal_Cave`, and `JC_Gacha_Location` | Author source registers the [gemstone destructibles](https://github.com/blaxxun-boop/Jewelcrafting/blob/master/Jewelcrafting/Setup/DestructibleSetup.cs), [cave](https://github.com/blaxxun-boop/Jewelcrafting/blob/master/Jewelcrafting/Setup/CaveSetup.cs), and [gacha location](https://github.com/blaxxun-boop/Jewelcrafting/blob/master/Jewelcrafting/Setup/GachaSetup.cs). The gemstone category is off by default; the two location categories are on. |
| [Exploration — Smoothbrain](https://github.com/blaxxun-boop/Exploration) | `skillRequire` thresholds and exploration-radius integration | Optional. Without the plugin, thresholds are ignored. With it, copper requires 20 and silver 75 in the supplied defaults. Temporary pins follow threshold changes; previously discovered permanent pins survive skill loss. |
| [Sailing — Smoothbrain](https://github.com/blaxxun-boop/Sailing) | Observation of the final exploration radius while sailing | Optional runtime integration; this mod does not supply extra category IDs. Radius depends on the exact installed versions and settings. |

BioHazardN7 also identifies the three content mods above as the targets of the supplied configuration. Install only the optional mods you want; their own dependencies and compatibility requirements still apply. The current candidate has not yet been verified in-game against their exact versions.

The YAML is not a complete inventory of everything any listed mod can add. Use `amp scan [radius] [all]` near a missing loaded object and attach the output to [Issues](https://github.com/GrandpaUA/AutoMapPins/issues).

## Upgrade and rollback

Back up the character save and AMP configuration before changing presets. Move old category/icon YAML outside `BepInEx/config` to avoid merging incompatible policies. The legacy `categories.vanilla.yaml` wins over `categories.yaml` when both are present. Explicit category disable/permanence changes can remove existing AMP pins.

The old vanilla-focused preset remains in `BepInEx/plugins/AutoMapPins/presets/Vanilla`. To undo policy changes, restore the backed-up configuration and save together.
