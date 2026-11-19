# License and provenance notes

AutoMapPins is a continuation of FelixReuthlinger's AutoMapPins; see the packaged README credits and root LICENSE (LGPLv3). LGPLv3 incorporates GPLv3: the accompanying [GPL text](<GPL-3.0.txt>) was obtained from https://www.gnu.org/licenses/gpl-3.0.txt . Including license texts does not replace source/distribution obligations.

## Merged dependencies

Release builds merge ServerSync and YamlDotNet into AutoMapPins.dll using ILRepack.

- YamlDotNet15.1.4: [MIT notice](<YamlDotNet.txt>), copied from the installed dependency package. Upstream: https://github.com/aaubry/YamlDotNet .
- ServerSync: [upstream license text](<ServerSync-upstream.txt>) from https://github.com/blaxxun-boop/ServerSync/blob/b623798824685776f81fc71c3c37daf99fad8355/LICENSE.txt . GitHub identifies this as MIT-0 (MIT No Attribution), not LGPL. Copyright2021Tykea.

The unmodified vendored ServerSync.dll has Git blob262e9a49bf2efe702af32589630ee80df3d66d8c, identical to FelixReuthlinger/AutoMapPins at revision a0d8ec6c15af6f4d10a28e23325bc0e55a73d5b1. The local version includes a narrow IL compatibility modification replacing three obsolete literal-field loads; see the repository's libraries/ServerSync.compatibility.md and scripts/Patch-ServerSyncCompatibility.ps1. Current patched SHA256:9A4EA6F34FDFC63FDEEFAC75520C2C5330C1CFCD9DD0709352A627B46B18E6BB.

Exact ServerSync source revision/build correspondence remains unverified. The upstream notice is evidence of published upstream terms, not a claim of reproducible source-to-binary verification. Review this provenance before a public release.

## Artwork

Embedded artwork credits are in the packaged README.

The additional PNGs under `config/AutoMapPins/CartursIconsPack/` are by **Cartur**, from [Cartur's Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/) ([source](https://github.com/jekkle/cartur-map-pins), [contributor's project link](https://valheim.hexium.gg/mods/Cartur/Carturs_Map_Pins)). BioHazardN7 supplied the configuration and reported obtaining the author's permission; the maintainer confirmed inclusion of the supplied artwork. This records the basis on which the files were supplied, rather than independent verification of a permission exchange.

Cartur retains authorship of these images. No explicit artwork license was found in the source project during preparation; AutoMapPins does not assign them its LGPLv3 code license. Keep the author credit and project links with this artwork.

This document does not grant new rights or replace any original license.
