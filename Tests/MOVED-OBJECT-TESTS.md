# Moved-object pin cleanup

Run `python scripts/Run-MovedObjectTests.py` from repository root. [Runner](<../scripts/Run-MovedObjectTests.py>) runs full bounded actual-source suite then [original reproducer](<MovedObjectLifecycleTests.cs>) and [extended controls](<MovedObjectExtendedTests.cs>) in separate fresh projects/processes. No game deployment.

Contract: when an object moves after insertion, hide/destroy removes its temporary membership by the original pin identity. A later visible rescan removes the old temporary membership before insertion at the new position. Group removal must preserve other members. Permanent discoveries remain saved, and duplicate lifecycle calls remain harmless.

This does not implement live moving-object tracking. Visibility still uses the position cached at Awake; fixing cleanup does not prove current-position discovery. Tests use real PinComponent/MapPin/MinimapPatch against Unity stubs. Real game acceptance needs a configured temporary movable object: discover, move, unload/destroy, verify no old marker; grouped control must keep stationary member; permanent control must retain discovery. Do not distribute an unversioned audit DLL for that test.
