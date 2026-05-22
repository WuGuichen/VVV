# Issue 74 CharacterStudio Runtime Resource Plan UX

Date: 2026-05-22

Labels:
- `type/refactor`
- `module/editor`
- `status/spec-draft`
- `priority/medium`

## Goal

Make the CharacterStudio runtime resource plan panel understandable for content
authors by translating internal scheduling/fallback terminology into clear
author-facing categories and actions.

## Background

The current `运行时资源计划` section exposes internal plan terminology such as:

- `SpawnCritical / FailSpawn`
- `EquipmentInitial / UseFallbackEquipment`
- `AnimationWarmup / UseFallbackPose`
- `VfxWarmup optional / SkipEffect`
- `UiDeferred optional / ShowPlaceholder`
- `Audio optional / MuteMissingCue`

This is useful as a low-level diagnostic view, but it is difficult for authors
to understand during character setup. The panel currently mixes:

- internal scheduling strategy
- fallback policy
- raw resource keys
- readiness state

without a user-facing semantic layer explaining what is required now, what is
optional, and what happens when something is missing.

As a result, first-time authors cannot easily tell:

- which resources block spawn
- which resources are only recommended
- which resources can be deferred
- whether the panel is informational or editable

## Scope

- Redesign the runtime resource plan panel into author-facing categories.
- Separate resource importance from fallback policy.
- Clarify whether each item is:
  - required
  - recommended/warmup
  - optional/deferred
- Present missing-resource consequences in plain language.
- Keep low-level diagnostics available, but behind a secondary or expandable
  detail view.

Possible implementation options:

- top-level sections such as `必需资源`, `推荐预热`, `可选表现`
- badges for `阻止生成`, `允许降级`, `占位显示`, `静音跳过`
- expandable advanced details showing original compiler/runtime plan terms
- help text explaining that this panel is diagnostic, not a primary config form

## Out Of Scope

- Changing the underlying character compiler resource plan
- Rewriting runtime spawn rules
- Replacing detailed diagnostics entirely
- Solving unrelated package validation issues

## Related Modules

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Interfaces/CharacterApplication.md`

## Must Read

- `AGENTS.md`
- `Docs/README.md`
- `Docs/Interfaces/CharacterApplication.md`
- `Tools/MxFramework.CharacterStudio/README.md`

## Allowed Read/Write

- `Tools/MxFramework.CharacterStudio/web/`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/`
- `Docs/Tasks/`

## Forbidden By Default

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`

## Acceptance Criteria

- A first-time author can identify which resources are mandatory for spawn.
- The panel clearly distinguishes required, recommended, and optional resource
  groups.
- The meaning of fallback behavior is understandable without knowing internal
  compiler terminology.
- Advanced diagnostic terms remain available without dominating the default UI.
- Existing underlying plan data and save behavior remain unchanged.

## Validation

- Manual walkthrough using a package with body, animation, preview, and missing
  optional resources
- Verify authors can correctly answer:
  - what blocks spawn
  - what can be deferred
  - what uses placeholders or mute fallback

## Public API

- No public runtime API changes intended

## Agent Constraints

- Preserve access to low-level diagnostics for advanced users
- Do not change runtime behavior while improving the author-facing view
