# Source and asset provenance

AutoMapPins is a maintained continuation by [GrandpaUA](https://github.com/GrandpaUA).

- Original source: [FelixReuthlinger/AutoMapPins](https://github.com/FelixReuthlinger/AutoMapPins), revision `a0d8ec6c15af6f4d10a28e23325bc0e55a73d5b1` (2.2.2).
- Compatibility source base: [littlebeagle/AutoMapPins](https://github.com/littlebeagle/AutoMapPins), revision beginning `a492ac0` (March 2026 compatibility work).
- Code license: the original LGPLv3 text is retained in `LICENSE`, with its incorporated GPL text in packaged notices.
- ServerSync: vendored dependency with a narrow local compatibility patch. See [patch provenance](../libraries/ServerSync.compatibility.md) and the packaged third-party notices; exact upstream source-to-binary correspondence remains unverified.
- YamlDotNet 15.1.4: restored from the pinned dependency package and merged at build time; the MIT notice is retained in packaged notices.
- Built-in artwork: original author credits are retained in README.
- Package logo: generated from the maintainer's supplied location-pin/map reference and CraftSort palette. The master and export notes are in `Icons/Branding`; embedded map-pin artwork is unchanged.
- Additional Cartur PNGs: supplied with the contributed configuration from BioHazardN7. The contributor reported obtaining permission and the maintainer confirmed inclusion. See [Cartur's source](https://github.com/jekkle/cartur-map-pins) and [artwork notice](../ThunderStorePackage/plugins/AutoMapPins/licenses/THIRD-PARTY.md). No code license is assigned to these images.

The new hosted repository starts from a curated source snapshot. Private development reports, conversations, logs, game assemblies, and local bot/agent tooling are excluded. The development repository's historical index and working files are preserved locally.
