# UPM-P6 Local Consumer E1 Matrix

> Evidence level: E1 diagnostic only
>
> Candidate: dirty P6 working tree based on `32f7fd0d195651def1b9655fa0913397191aefba`
>
> Unity: `6000.5.0f1`

| Profile | Tests | Package tree | Report SHA-256 | Result |
| --- | ---: | --- | --- | --- |
| base | 14/14 | `69cd892d31fc342a1ea9b13ebc94eaf194a521aa2863c25fc5aeec9d94145d4c` | `833994ee45e848554cf6e6aec7b08375d9c2c593276f87cd560a61944433a18b` | Passed |
| with-fairygui | 15/15 | same | `65d321b0084dbd7b152791e423fafa5ba6e5832b89dfb76e2293a386a53da730` | Passed |
| with-unity-transport | 15/15 | same | `3b4595cc8f7f42de67174a01c71166b32b12f7727e60db80f86d8a9c0a4c8d6f` | Passed |
| with-vfx-graph | 15/15 | same | `6fa446e7d1565f5311d97b89df3d177797b6fd0a7477cedff6cdb4279199dcb8` | Passed |

All four rows used `file:C:/UnityProjects/WGameFramework/Packages/com.vincent.wgameframework`, a clean Consumer Library, exact expected tests, and passing cold-import Assets, final Assets, ProjectSettings and package-tree mutation guards. The framework working tree was intentionally dirty during implementation, so these rows cannot be promoted.
