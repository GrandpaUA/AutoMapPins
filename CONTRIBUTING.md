# Contributing

Use [Issues](https://github.com/GrandpaUA/AutoMapPins/issues) for bugs, questions, and proposals. Discuss changes to saved pins, config compatibility, or multiplayer behavior before opening a large pull request.

See [BUILD.md](BUILD.md) for dependencies and commands. Run both regression suites for behavior changes. They compile production source files against runtime stubs; they do not establish compatibility with a running Valheim game.

For a bug fix, add a regression exercising the actual affected source and failing scenario. Include relevant negative cases such as reload, restart, disabled/missing optional mods, and existing saved pins. State which game versions and real gameplay scenarios you checked.

Preserve `FixItFelix.AutoMapPins` as the plugin/config identity and preserve existing configuration compatibility. Do not add game DLLs, personal profiles, logs, bot state, credentials, or third-party assets without provenance.

Code retains the repository's LGPLv3 license. Keep dependency notices and artwork attribution with distributions. Artwork has separate terms; see the README credits and packaged third-party notices.
