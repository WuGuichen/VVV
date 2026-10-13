# UPM-P2 E2 Immutable Consumer Matrix

> Status: Current
>
> Candidate: `9080f66586db5e2043046085de35f045d7f4b07e`
>
> Remote: `origin/main`
>
> Unity: `6000.5.0f1`
>
> Resolved package tree: `edfb7c5c5fc22bbd80bd670ffdd34f9db0aad3617f5902355d2ffffdfd56e2cf`

## PinnedGit E2 rows

All four rows resolved `com.vincent.wgameframework` from the remote Git subpath `/Assets/Scripts/MxFramework`. Every row recorded requested revision = resolved revision = repository commit = the candidate full SHA above, compiled successfully, passed the Consumer Assets mutation guard, and passed strict E0 policy v3 with 0 blocking failures and 7 release-readiness gaps.

| Profile | Tests | `report.json` SHA-256 | `editmode-results.xml` SHA-256 |
| --- | ---: | --- | --- |
| `base` | 7/7 | `178e29737b6d996ae12b3f33b95f3b3839769580f86997a9dc2ad51f164cc2a1` | `694fde9240f2bacfe25460be6797a1f2e1cc9478d7cd5911fd14b031b8ba37c4` |
| `with-fairygui` | 8/8 | `95bfe2bd84d021aa9530635446f3a9f9943be3cee731ffde2989aeea543d541d` | `c5cf3685a2f955a1861ae0e92927882f0279632d486c685628d5e0f700d97155` |
| `with-unity-transport` | 8/8 | `04071c7ce56a2ad30ed24a4a7a0f536046b0e79d3f14a7d91a2789ad6570de41` | `23fb46f907c1a56eb002bca2a2debb9bd9e18bc1e7feccf302d44105702bc8b8` |
| `with-vfx-graph` | 8/8 | `810a726225a14985eb69166b426d9ef244bd8ee6cb9272524f548fa80acce4ad` | `88e28886f55f553f0b11020c8632251dee668a4cbf1153401111142377bb6851` |

The four `package-boundary.json` files are byte-identical with SHA-256 `f876873b8f88d5cc6d8a2b4a81d6692b8f8a235057a1066cbbc9a190a7b9ae4a`. Reports are stored outside the repository under `C:/WGameFrameworkConsumerSmokeRuns/P2-9080f6658/PinnedGit/<profile>/`.

## Local diagnostic rows

A clean detached checkout of the same commit also passed Local base 7/7 and all three optional profiles 8/8 with both Consumer Assets and package mutation guards. These rows remain E1/non-promotable and are not combined with E2.

The Local source tree hash differs from the resolved PinnedGit tree because Unity Package Manager excludes the package-root `.gitignore`, converts resolved text files to CRLF on this Windows host, and injects a private `_fingerprint` field into its cached `package.json`. A file-by-file audit found no binary/LFS content difference. Candidate identity for promotion is therefore the four-row remote PinnedGit matrix above, not a synthesized cross-source tree hash.

## Decision

The PinnedGit matrix is complete and promotable for the UPM-P2 phase gate. It proves the remote immutable CharacterTest-extraction candidate across base and every affected positive optional profile. It is not an E3 release receipt and does not close the seven package release-readiness gaps.
