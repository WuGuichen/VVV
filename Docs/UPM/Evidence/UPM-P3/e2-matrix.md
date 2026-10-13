# UPM-P3 E2 Immutable Consumer Matrix

> Status: Current
>
> Candidate: `f8958d7cb66764e77a4fa79814494b14dc46c7dc`
>
> Remote: `origin/main`
>
> Unity: `6000.5.0f1`
>
> Resolved package tree: `04fe8219728c654a3b60d13077d837ea4d28cd1c39ee1cc66209503f601ebe88`

## PinnedGit E2 rows

All four rows installed `com.vincent.wgameframework` from the remote Git subpath `/Assets/Scripts/MxFramework`. Every row recorded requested revision = resolved revision = repository commit = the candidate full SHA above, compiled successfully, ran the exact expected Consumer and package-owned tests, passed the Consumer Assets mutation guard, and passed strict E0 policy v3 with 0 blocking failures and 7 release-readiness gaps.

| Profile | Tests | `report.json` SHA-256 | `editmode-results.xml` SHA-256 |
| --- | ---: | --- | --- |
| `base` | 11/11 | `b8a042e9a0f06a1ddb9acc3f2013c076ce083c99a6996a5158a335c4572855cf` | `255f9d1c34144e103242cebd2e7169e9c81df53ec5ab4c4baae5ac964ed91657` |
| `with-fairygui` | 12/12 | `389463764d8288edf7b475dee8d5416099301bd707ba297126887ef9a992527d` | `d4085b8395c91ea9354e7c1590774c5e797500491112f535bad7bc355977d333` |
| `with-unity-transport` | 12/12 | `3aa9e41a5be81f95f870568f7f31351c1161f11a4a289c83f093af763fede772` | `34001d4d38bd0e6aa012db68a6225d8e9c4babb0d1451a43cabc2ceafaf8cee0` |
| `with-vfx-graph` | 12/12 | `671c4cf76c5a570aff239cafa5a562ad9b91ea03a8e536cdc1f94f984390656a` | `c24c7b939cc88d9bafd7dc24ccc58203b34e09f82ead0ed924481f23f8876cf5` |

The four `package-boundary.json` files are byte-identical with SHA-256 `58b0ad8ae54560e86411ea5d8972b6f846bd9ad2052a8816f22c4017f8440379`. Reports are stored outside the repository under `C:/WGameFrameworkConsumerSmokeRuns/P3-E2-f8958d7cb/PinnedGit/<profile>/`.

## Local diagnostic rows

Before the immutable run, the current working tree passed matching Local base 11/11 and FairyGUI 12/12. Consumer Assets and package tree mutation guards passed, and both rows shared source tree hash `69051b0d23063bcf476aa445e1d37d39915b72066b8a616a37194530eba22062`; `repositoryDirty=true` reflects the task changes and preserved unrelated untracked files. These rows remain E1/non-promotable. The previous clean four-row Local matrix belongs to superseded candidate `a48ad5c1660c3ccd8fd19006b99e6141db1b4745` and is not combined with this candidate.

The Local source tree hash differs from the resolved PinnedGit tree because Unity Package Manager changes the resolved representation on this Windows host. Candidate identity for promotion is the four-row remote PinnedGit matrix above, not a synthesized cross-source tree hash.

## Decision

The PinnedGit matrix is complete and promotable for the UPM-P3 phase gate. It supersedes the earlier P3 candidate after the repository regressions were repaired, and proves the immutable test-surface split across base and every positive optional profile, including the four package-owned contract tests enabled through explicit Consumer `testables` and Test Framework ownership. It is not an E3 release receipt and does not close the seven package release-readiness gaps.
