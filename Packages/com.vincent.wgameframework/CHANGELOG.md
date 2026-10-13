# Changelog

All notable package changes are recorded here. Versions follow Semantic Versioning; preview versions do not imply API stability.

## [Unreleased]

### Added

- Added clean local and Git-pinned Consumer Smoke profiles for FairyGUI, Unity Transport, and VFX Graph, including package resolution, adapter assembly compilation, and one focused behavior test per integration.
- Generate every disposable Consumer gate project under the repository-external `C:\WGameFrameworkConsumerSmokeRuns` root by default, keeping `C:\WGameFrameworkConsumerSmoke` and `C:\WGameFrameworkConsumerRemote` available as long-lived local-source and immutable-remote development consumers and retaining path guards that reject those projects, repository-contained paths, or overly broad cleanup roots.

### Fixed

- Treat the project-owned published VFX binding manifest as an opt-in Editor integration, so a base UPM consumer without `Assets/Config/MxFramework/Vfx/vfx_published_bindings.json` imports without an initialization `LogError`.

## [0.8.0-preview.1] - 2026-07-20

### Added

- Declared the first explicit preview package channel.
- Added package-level installation, dependency, and optional-integration guidance.
- Added an isolated external UPM consumer smoke fixture and `RuntimeHost` lifecycle test.

### Changed

- Aligned the Unity and required package baseline with the versions validated by the main repository, including explicit Newtonsoft Json and all Unity built-in modules used directly by the package instead of relying on host or transitive dependencies.
- Isolated FMOD, FairyGUI, Unity Transport, and VFX Graph integrations from base-package compilation.

### Removed

- Removed the accidental stable implication of the historical `1.0.0` package version.
