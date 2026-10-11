# UPM-P6 Immutable PinnedGit E2 Matrix

> Candidate: `c046158f569536cd8167471524f3a7ed4fa0adad`
>
> Git subpath: `/Packages/com.vincent.wgameframework`
>
> Resolved package tree: `72f190366d0d8d40130fe88ee5010412c198e7d8dac7f67e6e1f6b7c95a7d432`
>
> Unity: `6000.5.0f1`

| Profile | Tests | Requested/resolved SHA | Report SHA-256 | XML SHA-256 | Result |
| --- | ---: | --- | --- | --- | --- |
| base | 14/14 | exact | `aadf4c9aaf711629fae8d8a64eb6cc58d5ea570809f86a2965324a6d6ac5de71` | `e203d84c44eb122350e1ad7a7a010877235b3f3ebff0ec47b1f4bc7d6567b8f5` | Passed |
| with-fairygui | 15/15 | exact | `96927767a8e080e5a23263aee09b2094a8ebf1ac6d62d2fb53c035bc89566c1e` | `9234688d5cf0b9d2148bb6481d65f93e010e3b846126e9e6639bdbf263bc9958` | Passed |
| with-unity-transport | 15/15 | exact | `63961532508b762c37209baa6fd88c719cdff7bf026f862e1d00bc5b842a1a3a` | `e93cd38d66f2a533762b7f18ebd05ead29d3ec07f5fbcf213a924f289ad9c987` | Passed |
| with-vfx-graph | 15/15 | exact | `eddbe6e01e5139febb9c053913609794f1032b1871a009b13affaf9ba90100e3` | `3c867cae7ffa68946f230a5af5d7bd475a4e9e86fad4370ab21d5771c04d68a3` | Passed |

Every row resolved the requested full SHA exactly, observed the same package version and materialized tree, used a fresh Consumer Library, passed the exact expected tests, and passed cold-import Assets, final Assets and ProjectSettings mutation guards. The remote candidate was fetched from the new canonical Git subpath; no row used the retired Assets subpath.
