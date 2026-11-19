# Icon path boundary regression

Run from repository root:

```powershell
python scripts/Run-IconPathRegression.py
```

The [runner](<../scripts/Run-IconPathRegression.py>) first runs the complete existing icon suite, then imports that generated project with [IconPathRegression](<IconPathRegression.cs>) as the entry point. Both compile the actual [Assets](<../Icons/Assets.cs>) source. Outputs and synthetic fixtures are retained in a new UUID directory under artifacts; no existing output is overwritten.

Nine checks cover ordinary files, missing ordinary files, a directory symlink with existing/missing leaf, file symlink, traversal/absolute outside path, trusted linked root and rejected linked descendant. Requires .NET8, Windows PowerShell, installed project YamlDotNet package and permission to create symlinks. Missing symlink permissions fail the run, rather than silently skipping protection tests.

This verifies path resolution using real filesystem links, not Unity PNG decoding. The default icon suite additionally checks load failure/last-known-good behavior with Unity stubs. No native gameplay claim. Junctions share ReparsePoint detection but are not independently created by these nine cases. The configured root is deliberately trusted even when linked; descendant reparse points are rejected. Local file replacement races are outside this defense-in-depth contract.
