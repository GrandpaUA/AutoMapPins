# Contributing

Use [Issues](https://github.com/GrandpaUA/AutoMapPins/issues) for bugs, questions, and proposals. Discuss changes to saved pins, config compatibility, or multiplayer behavior before opening a large pull request.

See [BUILD.md](BUILD.md) for dependencies and commands. Run both regression suites for behavior changes. They compile production source files against runtime stubs; they do not establish compatibility with a running Valheim game.

Documentation has two audiences: repository-root `README.md` is the extended GitHub guide; `ThunderStorePackage/README.md` is the short player-facing mod-site page and is packaged as `README.md`. Keep the site's description and features first. Ukrainian versions are `docs/README.uk.md` (GitHub) and `ThunderStorePackage/README.uk.md` (site). Keep factual changes consistent across versions without expanding the short page into the full reference.

For a bug fix, add a regression exercising the actual affected source and failing scenario. Include relevant negative cases such as reload, restart, disabled/missing optional mods, and existing saved pins. State which game versions and real gameplay scenarios you checked.

Preserve `FixItFelix.AutoMapPins` as the plugin/config identity and preserve existing configuration compatibility. Do not add game DLLs, personal profiles, logs, bot state, credentials, or third-party assets without provenance.

Code retains the repository's LGPLv3 license. Keep dependency notices and artwork attribution with distributions. Artwork has separate terms; see the README credits and packaged third-party notices.
