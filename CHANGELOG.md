# Changelog

## 2.3.17 — unreleased candidate

- Fix icon hot reload so one unreadable PNG does not block other icons. The affected entry retains its own last working image until its file is readable again.
- Resolve fallback aliases against the current icon set during partial reloads.
- Add GitHub Issues forms for bugs, questions, and features; source CI and build/release documentation.
- Use BioHazardN7's 94-category configuration as the standard preset with 78 named Cartur icons and 10 native/location overrides; include all 82 supplied PNGs with Cartur attribution. Keep the previous vanilla settings as an optional preset.
- Document optional Monstrum, Wizardry, Jewelcrafting, Exploration, and Sailing support, including the difference between configured IDs and in-game verification.
- Make package regression tests select Windows PowerShell's built-in modules when launched from PowerShell 7.
- Local source regressions and Release/ILRepack/ServerSync checks pass. Gameplay of this exact candidate remains to be verified.

## 2.3.16 — private test build

- Keep labels clear of enlarged map icons.
- Make `amp clear_pins` remove only AMP pins, preserving other map markers.
- Validate diagnostic radii independently of locale; reject non-finite and out-of-range input.
- Preserve member positions when grouped pins become permanent; remove moved temporary pins by their original identity.
- Restore pins after nested or failed map-table sharing calls.
- Validate experimental scan requests and recover local scans after exceptions.
- Restrict icon paths to the config directory, including links and roots with trailing separators.
- Load YAML in deterministic order, respect individual disabled entries, watch supported icon YAML names, and tolerate atomic-save gaps.

## 2.3.15 — private test build

- Add bounded `amp scan [radius] [all]` diagnostics for loaded object IDs, counts, and nearest distance.
- Apply native pin-type overrides to matching vanilla picker button images and restore them on removal.

## 2.3.14 — private test build

- Add exact location-ID icon overrides for game-owned markers, including size updates and restoration.

## 2.3.13 — private test build

- Reload edited PNGs without YAML edits; debounce writes and retry transient read failures.
- Support icon sizes from 1 to 300 percent and warn about invalid sizes.
- Accept historical `iconname` spelling with a warning while retaining canonical `iconName`.

## 2.3.12 — private test build

- Override native map pin types and apply configured icon display sizes.

## 2.3.11 — private test build

- Ignore Exploration requirements when that optional plugin is absent.

## 2.3.10 — private test build

- Add category/object `skillRequire` thresholds; preserve permanent discoveries after skill loss.

## 2.3.9 — private test build

- Support the single-file category config name and preserve legacy category file precedence.

## 2.3.8 — private test build

- Bound scanning work and reduce repeated checks in loaded scenes.

## 2.3.7 — experimental private test build

- Add opt-in dedicated-server scanning of previously generated objects. Multiplayer field verification remains outstanding.

## 2.3.6 — experimental private test build

- Add opt-in local scanning of previously generated zones without generating new zones.

## 2.3.1–2.3.5 — private development builds

- Preserve saved AMP identity across relogs and avoid duplicate discoveries.
- Improve loaded-object discovery, including wild beehives, trees, and resource roots.
- Integrate exploration-radius observation with optional skill mods and retain a configured fallback.
- Add configurable PNG icons, reload handling, and source regression checks.

## Previous releases

* 2.3.0 -> Reworked for Valheim version 0.221.12
* 2.2.2 -> compiled for Valheim version 0.219.14
* 2.2.1 -> trying to fix a NRE during game loading up 
* 2.2.0 ->
    * reworked uniqueness logic, since Unity object IDs turned out to not be stable -> now there should not be duplicate
      pins for the same object anymore
    * deactivated tin per default
    * deactivated portals per default
    * added a comment in the readme that you need to delete permanent pins manually, to make it clear that even a
      destroyed object will not remove the permanent pin
    * excluded AMP pins from being shared to map table
* 2.1.5 ->
    * fixed https://github.com/FelixReuthlinger/AutoMapPins/issues/33
    * fixed exception caused by empty config files
* 2.1.4 ->
    * added portals
* 2.1.2 & .3 ->
    * fixed .yaml file watcher to also respect sub folders
    * added some more missing things
* 2.1.1 ->
    * added the Flametal ore to ores
    * set runestones to be permanent
    * added some more missing things
* 2.1.0 ->
    * changed from Unity color system (using float 0.0 to 1.0) to usual RGB values (0 to 255)
    * writing "example" object for individual configs also with an example for icon color
    * added console command "print_effective_config" to print the effective config of an object to console
    * added some color examples to default configs
* 2.0.1 ->
    * fixed duplication warning using configured name instead of the object name for individual configs
    * added some more missing objects detected
* 2.0.0 ->
    * Complete rework of the most logic and config parts
    * BEWARE: to use this new version, you will need to create new config files according to the changes, see mod
      description
    * Changes:
        * Added maximum height to auto map objects to .cfg (but you do not want to change that, believe me)
        * Changed the default grouping distance to 30
    * **NEW** features:
        * **COLORFUL PINS**!!!1111... some people requested it, I don't know if those provide really much value, but now
          you can use colors in configs
        * **Update config -> updates pins in game** - requested many times via different reports
        * game object discovery mode improved (needs to be activated in .cfg), will print also player messages for new
          objects, print only once per new object
    * **NEW** way to configure the mod:
        * new way to configure a whole category (without repeating many values)
        * new way to configure lists of objects, without individual changes per category
        * kind-of old way to configure individual objects in the category
        * now the mod uses Valheim game object 'name' field contents to identify pins, not parsing it too wild anymore,
          just removing the "(Clone)" and " (123)" suffixes
        * ... too much detail, please read the mod readme :)
* 1.3.1 ->
    * Updated for Ashlands Valheim version 0.218.15
    * Added new pin configs for Ashlands objects
* 1.3.0 ->
    * compiled for latest Valheim version 0.217.38
    * fixed some server sync issue
    * often requested feature added : check if the map is already explored at the area of an object before adding
      the pin => only on explored map it will show pins
* 1.2.3 -> updated server sync and compiled against 0.217.30
* 1.2.2 -> updated server sync and compiled against 0.217.24
* 1.2.1 -> fixed double initiating commands
* 1.2.0 ->
    * added new config options to not spam your logs (check new default values and change if you need):
        * silent discovery mode -> not spams your logs if prefabs were found that are not configured, yet
        * enable discovery mode -> enables that not yet configured prefabs will be collected during game play
    * improved missing config check to only mention missing configs for not matching category name and internal name in
      combination, this will also drastically reduce log messages
    * will only print missing configs if there were configs recorded during game play, will show warning if none
      recorded
    * small change on config synced pin configs
* 1.1.8 ->
    * added new symbols:
        * Village
* 1.1.6 & 7 ->
    * compiled for Valheim version 0.217.14
    * fix console boot error
* 1.1.5 ->
    * fixed groups not being permanent
    * adjusted pickable pinning to only add pin if not "picked"
    * in case an object has a Destructible and Pickable components, we skip patching the Destructible one to only apply
      a patch on the pickable that will handle state properly
    * changed pickable update from patching "Drop" to "SetPicked"
* 1.1.4 ->
    * added missing oakstub
    * set portal as active and permanent per default
* 1.1.3 ->
    * added console command to remove all pins, use that if you created to many duplicate pins before, but it will
      really remove all pins, no matter what
    * added the birchstub too config I missed somehow before
* 1.1.2 -> fixed map pin duplication issue
* 1.1.1 -> added hay icon
* 1.1.0 ->
    * added patch for new types:
        * TeleportWorld (portal)
        * PickableItem
        * Container (chests)
    * added new icon "portal"
    * updated vanilla configs with new pins
* 1.0.4 -> replaced tree, dungeon and dot icon with less bold ones
* 1.0.3 -> fixed missing tree icons
* 1.0.1 & .2 ->
    * added icons:
        * monument
        * treasure
        * whale
        * temple
        * bones
    * added missing vanilla configs
* 1.0.0 -> first release on ThunderStore
