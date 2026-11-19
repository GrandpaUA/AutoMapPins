# AutoMapPins

Maintained continuation of AutoMapPins by GrandpaUA, based on FixItFelix's original mod.

**Current source: 2.3.17 release candidate.** Local regression and build checks do not establish in-game compatibility. See [release checks](https://github.com/GrandpaUA/AutoMapPins/blob/main/docs/RELEASE.md) for the remaining verification.

**Bugs, questions, configuration help, and feature requests:** use [GitHub Issues](https://github.com/GrandpaUA/AutoMapPins/issues/new/choose). English and Ukrainian are welcome. Include your mod/game versions, relevant configuration, and a session log when reporting a bug.

This mod serves as a kind of "radar" for your Valheim map, automatically creating temporary and persistent pins for
various in-game objects.

## Features

* [Server Synced](https://github.com/blaxxun-boop/ServerSync)
* Will create pins on Map and Minimap
    * **WARNING**: do better not configure too many to be active, it will flood your map!
* Pins will only show in areas where the player did already discover the map
* Pins created are using custom icons, see [Available icons](#available-icons)
* Pins can be configured, see [Pin configuration](#pin-configuration)
* Pins can be temporary (just visible when in area) or permanent (once discovered added permanently to map)
  * Permanent pins needs to be removed manually, so think twice about using this too widely
* Pins can be configured with color as you like
* Pin are loaded/created for game objects of Valheim types:
    * Destructible
    * Leviathan
    * Location
    * MineRock (ore)
    * MineRock5 (ore)
    * Pickable
    * TeleportWorld (portals)
    * PickableItem
    * Container (chests)
* Unknown (missing config) pins will be added to a temporary list that can be printed to a file to simplify
  configuration.
* Config file watchers (updated config file will be reloaded)
* Temporary pins follow their loaded objects and disappear when the object is removed. Permanent discoveries remain saved; see the configuration notes before enabling them broadly.

## Installation

Use a Valheim mod manager to install the package and its BepInEx dependency, or extract the package's `plugins` and `config` folders into `BepInEx`. Do not install the repository's build dependencies into your game.

Back up existing AMP configuration before upgrading. Keep your custom YAML and image paths when applying changes. Optional Exploration and Sailing mods are not required for normal pin discovery.

# Configuration

### Included configuration

The standard configuration is contributed by **BioHazardN7**: 94 categories using the shipped **Cartur** PNGs, optional Exploration skill thresholds, and additional IDs for Monstrum, Wizardry, and Jewelcrafting. Those mods are optional. **Some configured categories are disabled by default**; support for an object does not automatically enable its pins. The supplied `isActive` choices are preserved. See [configuration compatibility](https://github.com/GrandpaUA/AutoMapPins/blob/main/docs/COMPATIBILITY.md) for the exact scope and verification status.

For upgrades, back up your character save and AMP YAML. Move old category/icon YAML outside `BepInEx/config` before installing this preset; otherwise old and new entries can merge. In particular, `FixItFelix.AutoMapPins.categories.vanilla.yaml` takes precedence over the new `categories.yaml`. The mod preserves existing files. The previous vanilla preset is included at `BepInEx/plugins/AutoMapPins/presets/Vanilla` if you prefer its settings.

* The mod generates a short BepInEx config file `FixItFelix.AutoMapPins.cfg`
* Most configuration is provided via YAML files, see [data model](#config-data-model)

### Experimental distant pins in previously generated zones

`3 - Map Discovery Radius / Scan Previously Generated Zones (Local World Only)` is **off by default**. The setting name remains unchanged for compatibility with 2.3.6 profiles. In single-player it gradually reads existing networked world objects (ZDOs) within the current exploration radius (up to 1100m) without generating new zones, every 15 seconds, with at most 2000 candidates and 20000 examined ZDOs per scan (partial results are flagged, not mistaken for a full snapshot). It does **not** discover ungenerated zones or non-networked objects. Temporary distant pins disappear when objects leave the radius or vanish on a complete scan.

**Experimental, not yet field-tested in multiplayer:** on a dedicated server, install the *same version* on the server and clients and enable this setting on the server (the server-synced config governs clients). Clients perform a one-time capability handshake before requesting scans; the server checks the connected peer's reference position and bounds scan work and response packets. It uses per-connection `ZRpc`, not spoofable vanilla `ZRoutedRpc` sender UIDs, to identify the connected peer. Valheim clients can still update their own peer reference position, so it is **not a tamper-proof location restriction**: enable only on a trusted private test server and do not present this build as protecting hidden map data from malicious clients. A client with an older server receives no scan response. This feature has passed bounded stub tests and build checks only; do not deploy to a production world before multiplayer testing. Keep a backup of your world and original mod DLL.

## Pin configuration

* The packaged category file is `FixItFelix.AutoMapPins.categories.yaml`; the icon file is `FixItFelix.AutoMapPins.icons.yaml`. Named category files such as `FixItFelix.AutoMapPins.categories.mods.yaml` are also supported. Existing `FixItFelix.AutoMapPins.categories.vanilla.yaml` continues to work. If both category names are present in the same config folder, the legacy file takes precedence to protect user edits: back it up before renaming it yourself. The mod never deletes your YAML.
* The default configuration includes vanilla objects and optional mod entries. Active categories and skill thresholds follow BioHazardN7's contributed settings; edit them to suit your game.
    * Missing objects and contributed configs can be reported through [Issues](https://github.com/GrandpaUA/AutoMapPins/issues).
* If you play with objects beyond the vanilla ones, you might need to create your own configuration file, the following
  sections will explain how to.

### Debugging effective configuration and loaded object IDs

`amp print_effective_config <object name>` shows the effective active object config.
`amp scan [radius] [all]` summarizes exact loaded scene-object IDs, counts, nearest
player distance and configured status (default 30 m, allowed 1–100 m). Add `all`
to include child GameObjects, e.g. `amp scan 30 all` next to a Vegvisir. The output
is capped and explicitly flags incomplete scans; farther IDs may be omitted. This
is a **local diagnostic**, not a world-wide discovery command: it cannot find
unloaded/ungenerated zones. `amp scan_nearby 30 all` remains available for individual
components and nearby GameObject names.

AMP-created pins remain deliberately excluded from cartography-table sharing;
regular game map data still syncs normally. Do not enable or promise AMP sharing
by simply removing this exclusion: other players can have different active and
permanent-pin policies, and vanilla changes pin ownership on import.

### Config data model

* all category and pin configuration is provided using YAML files

#### YAML schema

Some statements to explain the intent of the schema:

1. The top level is the name of a category that you choose yourself to group configs and share certain settings in
   between the pins configured inside this category.
2. Each Category can (optional) have either or both of "individualConfiguredObjects" and "categoryConfiguredObjects",
   and each category does have the ability to also configure values for the whole category like you can configure for
   any individual config object entry.
3. individualConfiguredObjects: does contain a dictionary, the key is the game object "name" field content the value is
   the set of fields for configuration, see below.
4. categoryConfiguredObjects: does only contain a list of game object "name" field contents that are NOT configured
   individually, but all are only configured by any value set at category level.
5. Many configuration options are inherited from category down to categoryConfiguredObjects (they inherit all settings)
   and individualConfiguredObjects (they can inherit "name", "iconName" and "iconColorRGBA").
6. Any value that is not set will either be set with some default value or be inherited if available.

Config options per object:

1. "name" -> the name shown on map, can be omitted to not show a name
2. "iconName" -> the name of the icon to use, see available icons in the respective section of this readme
3. "iconColorRGBA" -> contains the fields "red", "green", "blue", and "alpha"; the values need to be set as int type (
   per channel, from 0 to 255, where 255 is full color); alpha sets the intensity of the color (also 0 to 255)
4. "isPermanent" -> pins with this flag set to true (default false) will not be removed when the player walks away from
   the area, and they will be persisted in the player save file
5. "isActive" -> activates the mod to use the config for this pin if set to true (default false)
6. "groupable" -> activated if set to true (default false) will enable pin grouping that will create one pin (counting
   the number of occurrences of the same object around it in the name)
7. "groupingDistance" -> the distance to be used to build groups of pins (default 30.0)
8. "skillRequire" -> optional Exploration skill level (0..100) required to discover new pins, inherited from the category but overridable for an individual object. If Exploration is absent, requirements are ignored. A previously discovered permanent pin remains saved and visible after skill loss; temporary pins disappear below the threshold and return when it is reached again. The supplied configuration uses thresholds (for example, copper requires level 20 and silver level 75); the previous vanilla preset has none.

##### Yaml example

```yaml
seeds:
  individualConfiguredObjects: # using this option will let you define individual pins, like if you want to name each of them differently, or activate grouping just for some of them
    Pickable_SeedCarrot:
      name: Carrot
      isPermanent: false
      isActive: true
      groupable: true
  isActive: true
  iconName: seed
crypt:
  categoryConfiguredObjects: # using this option will apply same settings for all those pins
    - Crypt2
    - Crypt3
    - Crypt4
    - SunkenCrypt4
  name: Crypt
  isActive: true
  isPermanent: true
  iconName: dungeon
  iconColorRGBA:
    red: 120
    green: 255
    blue: 255
    alpha: 200
```

##### Yaml internal name parsing

Rules:

* removes `(Clone)`
* removes `(123)`

Example: in-game loaded `gameObject.name` entry like `Mistlands_DvergrTownEntrance1 (2)`
or `Mistlands_DvergrTownEntrance1 (Clone)`
will be parsed to: `Mistlands_DvergrTownEntrance1`

### Create config file

* You can create multiple additional config files, the mod will load every file matching the pattern inside any sub
  folder of the `.../BepInEx/config/` path.
* Pattern for new files: `FixItFelix.AutoMapPins.categories.*.yaml` (replace "*" with your choice of name)
* There is a mechanic provided by the mod that will record any kind of pin that has a missing config while running
  through the world.
    * You can output all found config files using the console command `amp write_missing_configs_file`
    * All configs created by that will have defaults set `n_a` and booleans set to `false`, change those according to
      the data model (see [data model](#config-data-model))

### Custom map icons

`FixItFelix.AutoMapPins.icons.yaml` accepts PNG paths relative to `BepInEx/config` under `icons:` for AMP pin names. `vanillaOverrides:` additionally replaces **vanilla map pin types** when keyed by their game name: `Icon0`, `Icon1`, `Icon2`, `Icon3`, `Death`, `Bed`, `Icon4`, `Shout`, `Boss`, `Player`, `RandomEvent`, `Ping`, `EventArea`, `Hildir1`, `Hildir2`, `Hildir3`, or `Memorial`. `None` is deliberately excluded because it also represents location pins and unrelated unsaved pins. Starting with private 2.3.14, `vanillaOverrides` also recognizes exact case-sensitive **location IDs** such as `StartTemple`, `Vendor_BlackForest`, `Hildir_camp`, `BogWitch_Camp`, `AncientUpgradeStation`, matching only game-owned location markers returned by ZoneSystem. Example: `vanillaOverrides: { Vendor_BlackForest: { file: "AutoMapPins/icons/custom_trader.png", size: 125 } }`. The PNG must exist inside `BepInEx/config`; an unknown ID has no effect and removing an override restores the previous game icon. This does not activate AMP pin categories or create additional pins. Legacy `vanillaOverrides` keys such as `fire` remain usable for AMP icon-name lookups but do not change vanilla pin types. Since private 2.3.15, a valid override for `Icon0`–`Icon4`, `Death` or `Boss` also replaces the matching button image in the map's pin-type picker panel (only if the button still shows the vanilla sprite; selection frame, tint and size stay unchanged, and removing the entry restores the original). Other game UI is not changed. Actual behavior on Valheim 1.0.21 needs in-game verification.

An optional `size` on an icon is its **map display size in percent**: 100 is unchanged, 10 is one tenth, 150 is 1.5 times as large, accepted range 1–300. Omitting `size` leaves the game's size unchanged. It does not alter PNG dimensions. Edits to the PNG bytes or icon YAML reload after a short quiet interval; an unreadable PNG retains that entry's last working image and size **while the game remains open**, while other valid entries continue updating. After restarting with that PNG still absent, its configured `fallbackIcon` can use an AMP built-in name such as `mine` or another configured icon; `Icon0` is a vanilla pin *type*, not an AMP built-in fallback icon name. Without a usable override, Valheim displays its original vanilla Icon0. Back up your icon YAML and image files before editing. At `size: 10` the map icon can be almost invisible due to display pixel rounding, although it is scaled to 10%. In category YAML, the canonical key is `iconName`; historic `iconname` is now accepted with a warning, but correcting its spelling is recommended.

# Available Icons

The built-in icons below carry the original project's CC BY attributions. The additional Cartur artwork has separate attribution; see the credits section.

| Icon name    | Description         | Used for                                    |
|--------------|---------------------|:--------------------------------------------|
| `axe`        | Axe 64 pixels       |                                             |
| `axe48`      | Axe 48 pixels       |                                             |
| `berry`      | Berry 64 pixels     | Pickable bushes like Raspberry              |
| `berry48`    | Berry 48 pixels     |                                             |
| `dungeon`    | Dungeon 64 pixels   | Dungeons, Caves, Crypts, ...                |
| `dungeon48`  | Dungeon 48 pixels   |                                             |
| `flower`     | Flower 64 pixels    |                                             |
| `flower48`   | Flower 48 pixels    |                                             |
| `hand`       | Hand 64 pixels      | Pickables like Flint, ...                   |
| `hand48`     | Hand 48 pixels      |                                             |
| `mine`       | Mine 64 pixels      | Minable ores, nodes, veins, ...             |
| `mine48`     | Mine 48 pixels      |                                             |
| `mushroom`   | Mushroom 64 pixels  | Mushroom, Yellow mushroom                   |
| `mushroom48` | Mushroom 48 pixels  |                                             |
| `seed`       | Seed 64 pixels      | Pickable seeds like carrot, turnip, ...     |
| `seed48`     | Seed 48 pixels      |                                             |
| `spawner`    | Pentagram 64 pixels | Spawners like Greydwarf nests or bone piles |
| `spawner48`  | Pentagram 48 pixels |                                             |
| `rune`       | Rune 64 pixels      | Runestones                                  |
| `rune48`     | Rune 48 pixels      |                                             |
| `dot`        | Circle 64 pixels    | Anything else                               |
| `dot48`      | Circle 48 pixels    |                                             |
| `herb`       | Herb 64 pixels      | Herbs like Thistle                          |
| `herb48`     | Herb 48 pixels      |                                             |
| `island`     | Island 64 pixels    | Leviathan                                   |
| `island48`   | Island 48 pixels    |                                             |
| `monument`   | Monument 64 pixels  |                                             |
| `monument48` | Monument 48 pixels  |                                             |
| `temple`     | Temple 64 pixels    |                                             |
| `temple48`   | Temple 48 pixels    |                                             |
| `treasure`   | Treasure 64 pixels  | Treasure chests                             |
| `treasure48` | Treasure 48 pixels  |                                             |
| `bones`      | Bones 64 pixels     |                                             |
| `bones48`    | Bones 48 pixels     |                                             |
| `portal`     | Portal 64 pixels    | Portal                                      |
| `portal48`   | Portal 48 pixels    |                                             |
| `hay`        | Hay 64 pixels       |                                             |
| `hay48`      | Hay 48 pixels       |                                             |
| `village`    | Village 64 pixels   |                                             |
| `village48`  | Village 48 pixels   |                                             |

# Miscellaneous

<details>
  <summary>License, credits, attributions</summary>

* [LGPLv3](https://www.gnu.org/licenses/lgpl-3.0.de.html) license based mod
* Original AutoMapPins author: [FixItFelix / FelixReuthlinger](https://github.com/FelixReuthlinger/AutoMapPins). This continuation also uses compatibility work from [littlebeagle](https://github.com/littlebeagle/AutoMapPins).
* Continuation maintained by [GrandpaUA](https://github.com/GrandpaUA).
* Testing, feedback, and contributed configuration: **BioHazardN7**.
* Additional map icons: **Cartur**, author of [Cartur's Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/) ([source project](https://github.com/jekkle/cartur-map-pins), [provided project link](https://valheim.hexium.gg/mods/Cartur/Carturs_Map_Pins)). Used with permission reported by the contributor and confirmed for inclusion by the maintainer. Cartur retains authorship; these icons are not relicensed under AutoMapPins' code license.
* This mod is inspired from concepts of [Kempeth's AutoMapPins](https://github.com/Kempeth/AutoMapPins) provided via
  LGPLv3, but I did re-write most of the code from scratch to make the mod configurable, simplified a lot of the
  concepts.
* Mod logo: recreated from the maintainer's supplied map-and-location-pin reference, using CraftSort's dark brown and golden yellow palette. High-resolution master is in `Icons/Branding`.
* Original embedded AMP/radar artwork: [radar icon](https://www.flaticon.com/free-icons/radar).
* [Axe by Danil Polshin](https://thenounproject.com/browse/icons/term/axe/) (CCBY) - modified
* [cave dungeon by Amanda Hua](https://thenounproject.com/browse/icons/term/cave-dungeon/) (CCBY) - modified
* [Flower by Vectors Market](https://thenounproject.com/browse/icons/term/flower/) (CCBY) - modified
* [Mine by Edward Boatman](https://thenounproject.com/browse/icons/term/mine/) (CCBY) - modified
* [Mushroom by Anton Gajdosik](https://thenounproject.com/browse/icons/term/mushroom/) (CCBY) - modified
* [pick by Pham Duy Phuong Hung](https://thenounproject.com/browse/icons/term/pick/) (CCBY) - modified
* [raspberry by Laymik](https://thenounproject.com/browse/icons/term/raspberry/) (CCBY) - modified
* [seeds by Orin zuu](https://thenounproject.com/browse/icons/term/seeds/) (CCBY) - modified
* [rune by NoNsEnSe ThInGs](https://thenounproject.com/browse/icons/term/rune/) (CCBY) - modified
* [Monster by BomSymbols](https://thenounproject.com/browse/icons/term/monster/) (CCBY) - modified
* [dot by Saepul Nahwan](https://thenounproject.com/browse/icons/term/dot/) (CCBY) - modified
* [Tree by Icon Solid](https://thenounproject.com/browse/icons/term/tree/) (CCBY) - modified
* [herb by scarlett mckay](https://thenounproject.com/browse/icons/term/herb/) (CCBY) - modified
* [island by David Mühlenweg](https://thenounproject.com/browse/icons/term/island/) (CCBY) - modified
* [Fire by koto](https://thenounproject.com/browse/icons/term/fire/) (CCBY) - modified
* [Monument by Doodle Icons](https://thenounproject.com/browse/icons/term/monument/) (CCBY) - modified
* [temple by Deemak Daksina](https://thenounproject.com/browse/icons/term/temple/) (CCBY) - modified
* [treasure by Vectors Market](https://thenounproject.com/browse/icons/term/treasure/) (CCBY) - modified
* [Whale by Gregor Cresnar](https://thenounproject.com/browse/icons/term/whale/) (CCBY) - modified
* [bones by Sergey Demushkin](https://thenounproject.com/browse/icons/term/bones/) (CCBY) - modified
* [warp by Dank By Design](https://thenounproject.com/browse/icons/term/warp/) (CCBY) - modified
* [hay by Eucalyp](https://thenounproject.com/browse/icons/term/hay/) (CCBY) - modified
* [Village by Adrien Coquet](https://thenounproject.com/browse/icons/term/village/) (CCBY) - modified
* The included Cartur PNGs are installed below `BepInEx/config/AutoMapPins/CartursIconsPack`. See the supplied icon configuration for names and paths. Keep this attribution when sharing the configuration and artwork.

</details>

<details>
  <summary>Contact</summary>

* [Source and releases](https://github.com/GrandpaUA/AutoMapPins)
* [Bugs, questions, and feature requests](https://github.com/GrandpaUA/AutoMapPins/issues/new/choose)

</details>

