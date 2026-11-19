# Icon watcher regression scope

Run `python scripts/Run-IconWatcherTests.py`. [Runner](<../scripts/Run-IconWatcherTests.py>) creates fresh baseline and probe projects and runs full icon suite plus [original reproducer](<IconWatcherCoverageTests.cs>) and [extended controls](<IconWatcherExtendedTests.cs>).

Actual IconConfigStorage and Windows FileSystemWatcher are used, with SynchronizingObject cleared for test callbacks (not Unity main-thread marshaling). Covered: default/dotted/non-dot suffix YAML creation, nested YAML, nested PNG, unrelated YAML negative, repeated disposal and cleared ownership list. Discovery supports icons*.yaml; watching must match. Fixtures retained, files created fresh; no cleanup deletion.

Bounded event waits (2–3seconds positive,500ms negative) can fail under unusually overloaded CI. No retry hides failures. Does not establish absence of late queued callbacks after disposal, overflow recovery, native Unity synchronization, Changed/Renamed/Deleted events, debounce-to-Assets-to-Minimap integration or rendering. Temporary event handles in extended short-lived process intentionally retained for any in-flight callbacks.

Native acceptance later: create/edit/rename/delete a valid non-dot suffix icon YAML inside config, including nested directory; each should trigger reload after quiet interval, malformed content should retain last-known-good, persistent deletion restore vanilla after grace. Capture logs and fixed-zoom screenshots. No new DLL distribution authorized by these local tests.
