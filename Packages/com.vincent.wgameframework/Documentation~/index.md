# WGame Framework Preview Documentation

WGame Framework is a reusable Unity 6 package for deterministic runtime composition, gameplay systems and portable Editor authoring.

## Start here

- `README.md` at the package root describes installation, required dependencies and optional integrations.
- `Samples~/RuntimeHostMinimalLifecycle` is the self-contained package sample.
- `Tests/Runtime` and `Tests/Editor` contain the package-owned contract tests.
- Repository-level design and interface documentation is maintained under `Docs/` in the source repository.

## Preview support boundary

The package remains a preview. Consume an immutable full commit SHA and run the Consumer evidence gate for the exact base or optional profile used by the project. `optional-integrations.json` is the machine authority for optional support tiers.

The package does not yet claim release readiness. License, third-party notices, tagged source, Player execution, N-1 upgrade and compatibility commitments remain P8 gates.
