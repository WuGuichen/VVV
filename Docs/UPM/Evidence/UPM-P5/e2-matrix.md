# UPM-P5 E2 Immutable Editor Portability Matrix

> Status: Current
>
> Candidate: `b2a688ab7caca80f0c5ef6e3b521fb06cfed20ea`
>
> Remote: `origin/main`
>
> Unity: `6000.5.0f1`
>
> Resolved package tree: `d66e7b4428860c57d461410cc97b8544ddb618f7d24dbedba1d809cf69c17dde`

## PinnedGit rows

Every row resolved the same remote full SHA above, compiled its exact expected optional closure, passed cold-import Assets checks, package/window tests and post-normalization ProjectSettings checks.

| Profile | Tests | `report.json` SHA-256 | `editmode-results.xml` SHA-256 |
| --- | ---: | --- | --- |
| base | 14/14 | `a26f37da8f2e8e463a6cb82285b420e658633596ee524c9f345432518fbfc313` | `2f16f27fe2549c14202d9630321a2a71de9c0349a0b708bf328bec43c90422ed` |
| FairyGUI | 15/15 | `38500c0a299065710fee08e268fb3ae866c6fb06fe9583777614a37e3c7e66c0` | `26a9b020f50e0fd06d7f94400e0eb747636a14e3c6bc04cb9e4733e211250f43` |
| Unity Transport | 15/15 | `febdc15f7f7a424b91fcfbf0231f604941cc2956cc1f17a0f6d70ee0311e7873` | `141f1379c428ed130eb3519d13807a582fafbb32547f1a0e7cbd7092eb639938` |
| VFX Graph | 15/15 | `d056742fd53e13b20646a727853b6e0df599dd1126563df0dc7d8590cbb2fc3c` | `6be46fed7895a1e4575eba7c6b3cc3d34380797fbf3eddadabc8759b9fb75392` |

The four `package-boundary.json` files are byte-identical with SHA-256 `941ac465612078df89f71e1cd5e1bd70835ab724f948dd7d0d81bcc3be3e4bd5`.

## Portability evidence

- 29/29 project-only Editor source/asmdef GUID and content-hash pairs were preserved; deletion count 0.
- strict E0: 14 dependencies, 117 package asmdefs, 14 initializers, 0 blocking failures, 6 readiness gaps.
- base Consumer excludes Tools, FGUIProject, Config, Scene, VFX, Bundles and all optional adapter DLLs including FMOD.
- VFX Resource Library reports stable unavailable when its project catalog is absent.
- package asset styles resolve through the canonical locator in the installed package.
- domain reload no longer generates the published VFX projection.
- all profiles preserve Consumer Assets and package content; ProjectSettings are unchanged after normalization, except the exact external Unity VFX `VFXManager.asset` first-use allowlist in the VFX positive profile.
- Reports are stored outside the repository under `C:/WGameFrameworkConsumerSmokeRuns/P5-E2-b2a688ab7/PinnedGit/<profile>/`.

## Decision

The matrix is complete and promotable for UPM-P5. The locator's sole development fallback intentionally remains for P6; no other live package Editor code contains the legacy root. This is not an E3 preview release receipt and does not close the remaining six release-readiness gaps.
