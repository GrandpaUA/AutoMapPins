# Custom map icons

`FixItFelix.AutoMapPins.icons.yaml` accepts PNG paths relative to `BepInEx/config` under `icons:` for AMP pin names. `vanillaOverrides:` additionally replaces **vanilla map pin types** when keyed by their game name: `Icon0`, `Icon1`, `Icon2`, `Icon3`, `Death`, `Bed`, `Icon4`, `Shout`, `Boss`, `Player`, `RandomEvent`, `Ping`, `EventArea`, `Hildir1`, `Hildir2`, `Hildir3`, or `Memorial`. `None` is deliberately excluded because it also represents location pins and unrelated unsaved pins. Starting with private 2.3.14, `vanillaOverrides` also recognizes exact case-sensitive **location IDs** such as `StartTemple`, `Vendor_BlackForest`, `Hildir_camp`, `BogWitch_Camp`, `AncientUpgradeStation`, matching only game-owned location markers returned by ZoneSystem. Example: `vanillaOverrides: { Vendor_BlackForest: { file: "AutoMapPins/icons/custom_trader.png", size: 125 } }`. The PNG must exist inside `BepInEx/config`; an unknown ID has no effect and removing an override restores the previous game icon. This does not activate AMP pin categories or create additional pins. Legacy `vanillaOverrides` keys such as `fire` remain usable for AMP icon-name lookups but do not change vanilla pin types. Since private 2.3.15, a valid override for `Icon0`–`Icon4`, `Death` or `Boss` also replaces the matching button image in the map's pin-type picker panel (only if the button still shows the vanilla sprite; selection frame, tint and size stay unchanged, and removing the entry restores the original). Other game UI is not changed. Actual behavior on Valheim 1.0.21 needs in-game verification.

An optional `size` on an icon is its **map display size in percent**: 100 is unchanged, 10 is one tenth, 150 is 1.5 times as large, accepted range 1–300. Omitting `size` leaves the game's size unchanged. It does not alter PNG dimensions. Edits to the PNG bytes or icon YAML reload after a short quiet interval; an unreadable PNG retains that entry's last working image and size **while the game remains open**, while other valid entries continue updating. After restarting with that PNG still absent, its configured `fallbackIcon` can use an AMP built-in name such as `mine` or another configured icon; `Icon0` is a vanilla pin *type*, not an AMP built-in fallback icon name. Without a usable override, Valheim displays its original vanilla Icon0. Back up your icon YAML and image files before editing. At `size: 10` the map icon can be almost invisible due to display pixel rounding, although it is scaled to 10%. In category YAML, the canonical key is `iconName`; historic `iconname` is now accepted with a warning, but correcting its spelling is recommended.

## Available icons

The built-in icons below carry the original project's CC BY attributions. The additional Cartur artwork has separate attribution; see the [full artwork credits](../ThunderStorePackage/plugins/AutoMapPins/licenses/CREDITS.md).

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

## More guides

- [Category and pin configuration](CONFIGURATION.md).
- [Back to the mod overview](../README.md).
