# UPM-P5 No-Implicit-Write Matrix

> Status: Current
>
> Evidence level: Local E1, non-promotable

The Consumer runner now separates cold import/domain reload from the test phase:

1. cold import resolves packages and compiles without running package tests;
2. Consumer Assets must remain byte-identical during cold import;
3. after Unity's fixture normalization, Assets and ProjectSettings snapshots are captured;
4. menu/window/package tests run;
5. Assets, ProjectSettings and package source trees are compared again.

| Phase | Assets | ProjectSettings | Package tree | Result |
| --- | --- | --- | --- | --- |
| base cold import | unchanged | Unity fixture normalization precedes baseline | Local source unchanged | passed |
| base menu/window tests | unchanged | unchanged | unchanged | passed |
| optional positive profiles | unchanged | unchanged | unchanged | passed |
| VFX Graph behavior | unchanged | only `VFXManager.asset` allowlisted as an external Unity VFX first-use write | unchanged | passed |

The allowlist is exact and profile-scoped. No MxFramework file, other ProjectSettings file, repository path or unknown Consumer asset may change. `VfxPublishedBindingEditorBootstrap` no longer writes on reload, and project-only FairyGUI import processing is absent from the package.
