# Configuration guide

## Included configuration

The standard configuration is contributed by **BioHazardN7**: 94 categories using the shipped **Cartur** PNGs, optional Exploration skill thresholds, and additional IDs for Monstrum, Wizardry, and Jewelcrafting. Those mods are optional. **Some configured categories are disabled by default**; support for an object does not automatically enable its pins. The supplied `isActive` choices are preserved. See [configuration compatibility](https://github.com/GrandpaUA/AutoMapPins/blob/main/docs/COMPATIBILITY.md) for the exact scope and verification status.

For upgrades, back up your character save and AMP YAML. Move old category/icon YAML outside `BepInEx/config` before installing this preset; otherwise old and new entries can merge. In particular, `FixItFelix.AutoMapPins.categories.vanilla.yaml` takes precedence over the new `categories.yaml`. The mod preserves existing files. The previous vanilla preset is included at `BepInEx/plugins/AutoMapPins/presets/Vanilla` if you prefer its settings.

* The mod generates a short BepInEx config file `FixItFelix.AutoMapPins.cfg`
* Most configuration is provided via YAML files, see [config data model](#config-data-model)

## Category files

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
2. "iconName" -> the name of the icon to use; see the [icon guide](ICONS.md#available-icons)
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
      the data model (see [config data model](#config-data-model))

## More guides

- [Custom and built-in icons](ICONS.md).
- [Experimental distant scanning](EXPERIMENTAL-SCANNING.md).
- [Optional mod entries](COMPATIBILITY.md).
- [Back to the mod overview](../README.md).
