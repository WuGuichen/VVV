# UPM-P4 E2 Immutable Sample Matrix

> Status: Current
>
> Candidate: `7cd881755a6c6df31f47ac9c479bd16b024f222b`
>
> Remote: `origin/main`
>
> Unity: `6000.5.0f1`
>
> Resolved package tree: `98517f12c0dbb07cb321a87ca585e3cc7eba11dff7a72e7e83194564d4a1219b`

## Immutable rows

Both rows installed `com.vincent.wgameframework` from the remote Git subpath `/Assets/Scripts/MxFramework`. Requested revision, resolved revision and repository commit all equal the candidate full SHA above.

| Row | Result | Report SHA-256 | Test XML SHA-256 |
| --- | ---: | --- | --- |
| PinnedGit base Consumer | 11/11 | `3418127b8345f6abd87414d5ce30d03a7348ccd038b7642a71666975438f9d72` | `8e9f629f6d188fd4e06e4515912776a557dc7f4dec756fe10d2d16c2c64600fa` |
| Explicit sample Import/Run | 1/1 | `d69122e3aefaa6fa5ff16c082102d5db0a6391c3c922cb8743097534bf6c9382` | `b38fdf798614e4560e7b44cd3a4c62171f14ba41f5b7b8d57965d88c6cccfe98` |

The sample was imported through Unity Package Manager to `Assets/Samples/WGame Framework/0.8.0-preview.1/Runtime Host Minimal Lifecycle/RuntimeHostMinimalLifecycle.unity`. The smoke opened that scene, entered Play Mode, observed the `Initialize -> Start -> Tick x3 -> Stop -> Dispose` contract and confirmed the RuntimeHost reference was released.

## Identity and mutation guards

- Repository Demo move: `113/113` file GUID and SHA-256 pairs preserved; deletion count 0.
- Sample source tree: 10 files including `.meta`; path/content tree `6dc19c65e5c77421a985acc2441e9c8a17cac7c4f692dc9dd5518489a89df113`.
- Resolved package tree remained unchanged during Import/Run.
- Consumer `ProjectSettings` remained unchanged.
- Consumer Assets outside the explicit `Assets/Samples` import root remained unchanged.
- strict E0: 14 dependencies, 119 asmdefs, 17 initializers, 0 blocking failures and 6 release-readiness gaps.
- Reports are stored outside the repository under `C:/WGameFrameworkSampleSmokeRuns/P4-E2-7cd881755/PinnedGit/base/`.

## Decision

This immutable base + explicit sample matrix is promotable for the UPM-P4 phase gate. No optional adapter was added to or required by the sample, so P4 has no matching positive optional row. This receipt is not an E3 preview-release receipt and does not close the remaining six release-readiness gaps.
