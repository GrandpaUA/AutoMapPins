# Release preparation

## Current status

The current source version is **2.3.18**, a release candidate. It is not a verified stable release. The last documented tester delivery was **2.3.16**. A successful build or CI result does not replace testing the exact candidate in Valheim.

## Candidate checks

1. Resolve open candidate issues and verify the final icon/category configuration.
2. Match `AutoMapPinsPlugin.ModVersion`, assembly version, manifest, and changelog. Never distribute different DLLs under an already delivered version number.
3. Run both suites from BUILD.md and build Release with ILRepack and the ServerSync compatibility check. Keep deployment disabled.
4. Create a ZIP in a fresh output directory. Run packaging and license-payload regressions from BUILD.md. Check archive contents, manifest, DLL identity, and SHA-256 against the actual attached artifact.
5. Retain code/dependency/artwork credits. Cartur artwork is separate from the LGPLv3 code; record its source and reported permission without inventing a license on the author's behalf.
6. Test that exact candidate in-game with backups of the character, world, original DLL, and configuration. Record game/mod versions and artifact hash in the issue.

## Required in-game evidence

- Establish a baseline with the previous DLL and same config. Check temporary/permanent/grouped pins, then save, restart, and reconnect.
- With one deliberately missing PNG in icon YAML, change another readable icon's size and a category's `iconName`. The valid entry should update without restarting; the unreadable entry should keep its last working image. Restore the PNG and verify recovery.
- Change and remove native pin-type, location, and picker overrides. Verify the visible result and restoration of game icons; include a negative case without an override.
- Check Exploration installed/absent and threshold gain/loss. Already discovered permanent pins should survive skill loss. Check Sailing radius behavior with the exact optional plugin versions used.
- Verify clearing AMP pins preserves hand-placed, death, boss, and location pins. Use a disposable character or backed-up save.
- Separately test experimental multiplayer scans with matching server/client versions, reconnect, missing capability, bounded responses, and no new-zone generation on a disposable server. Keep the option off until this is established.
- Roll back the DLL and backed-up configuration/save. Attach before/after screenshots and the session log to Issues.

## GitHub publication

Publish the curated source snapshot, not a recursive upload of the development folder. Exclude private reports, logs, bot tooling/state, local agent instructions, decompiled game files, cached dependencies, and build outputs. Include all runtime source, tests, build/package scripts, license notices, and shipped configuration/assets.

Keep Issues enabled. Use the three issue forms for bugs, questions, and features. CI checks source regressions without proprietary game assemblies; release builds and in-game acceptance are separate checks.

Only publish a stable release after the matching candidate has positive gameplay evidence. An experimental prerelease must state its unverified scenarios clearly.
