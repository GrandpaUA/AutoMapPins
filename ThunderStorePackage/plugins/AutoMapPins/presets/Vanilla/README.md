# Previous vanilla preset

The package now installs BioHazardN7's configuration with Cartur icons by default. These files retain the previous vanilla-focused category defaults and an empty custom-icon template, without Exploration skill requirements.

To switch, close the game and back up your character save and AMP YAML. Move other `FixItFelix.AutoMapPins.categories*.yaml` and `FixItFelix.AutoMapPins.icons*.yaml` files outside `BepInEx/config`, then copy these two files into that directory. A legacy `categories.vanilla.yaml` in the config tree takes precedence over `categories.yaml`; keep the backup outside the watched tree.

Changing category policies can remove existing AMP pins. Restore both the backed-up config and save to undo such changes.

Questions and reports: [GitHub Issues](https://github.com/GrandpaUA/AutoMapPins/issues).
