# STORY_S6_EXTERNAL_AUTHORING_TOOLS

## Goal

Implement Issue #442:

- external `Tools/MxFrameworkStoryAuthoring/story_authoring.py` CLI.
- Markdown Story Outline v1 importer.
- generated Story.Config draft JSON fixture.
- validator and tests for the generated draft.

Authoring AI Assist is explicitly deferred in this S6 delivery. This task may add docs placeholders for future Authoring AI Assist, but must not add LLM runtime dependencies, API keys, prompt execution, or generated-prompt paths.

## Workflow Level

- Issue: `#442`
- Parent: `#436`
- Task level: `S3`
- Delivery level: `External Tool Slice`
- Suggested branch: `feature/442-story-authoring-tools`
- Required PR: yes
- Merge policy: implementation agent opens PR and does not merge it.

## Preconditions

- #438 Story.Config is merged.
- #441 Story S5 playable slice is merged.
- The initial authoring tool targets Story.Config row contracts only; it must not modify Runtime Story assemblies.

## Required Reading

1. `AGENTS.md`
2. `Assets/AGENTS.md`
3. `Assets/Scripts/MxFramework/AGENTS.md`
4. `Docs/PROJECT_INDEX.md`
5. `Docs/README.md`
6. `Docs/WORKFLOW.md`
7. `Docs/QUALITY_GATE.md`
8. `Docs/Decisions/ADR-004-story-module-scope.md`
9. `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`
10. `Docs/Interfaces/Story.md`
11. `Docs/Interfaces/Story.Config.md`
12. `Docs/Interfaces/Config.md`
13. `Docs/INTERFACES.md` AI Terminology section.
14. Existing `Tools/**` CLI/test conventions.
15. This task document.

## API Reuse Plan

| Need | Framework API / Contract | Use in S6 | Notes / intentional gaps |
| --- | --- | --- | --- |
| Story output contract | `StoryGraphConfig`, `StoryBeatConfig`, `StoryStepConfig`, `StoryBranchConfig`, `StoryChoiceConfig`, `StoryFactConfig` | Yes | The external tool emits JSON rows matching these public Story.Config contracts. It does not instantiate C# DTOs directly. |
| Story validation rules | `Docs/Interfaces/Story.Config.md` validator contract | Yes | Python validator mirrors the public row constraints needed for the fixture: missing entry beat, duplicate ids, invalid branch / choice target, missing text key, invalid trigger/effect id, unsupported step kind / wait policy. |
| Config handoff | `.story.json` draft file | Yes | This is a tool-layer interchange draft, not a Unity asset and not runtime save data. |
| Runtime Story | `StoryDirector`, `StoryRuntimeModule`, runtime commands | No | Out of scope. Runtime assemblies must not change. |
| Unity Editor | `Story.Editor`, Unity Editor menus | No | External CLI must run without Unity. |
| Authoring AI Assist | `Authoring AI Assist` terminology only | Deferred | No LLM dependency, API key, prompt runner, or automatic text generation in this issue. |
| Existing authoring tools | `Tools/MxFramework.Authoring` patterns | Reference only | Do not couple this Python slice to the .NET authoring solution unless the PR first updates the task doc with a concrete replacement command set. |

## Markdown Story Outline v1

The first importer only needs a deterministic, easy-to-review subset.

Required fixture:

```text
Tools/MxFrameworkStoryAuthoring/fixtures/markdown/basic_choice_story.md
```

Suggested syntax:

```markdown
---
graph: 442001
entry: intro
source: basic_choice_story
---

# Basic Choice Story

## Beat intro
id: 442101
trigger: 442201
line: 442301 | WaitForCommand | A signal waits at the story boundary.
choice: 442401 | 442302 | end | effect 442501 | Stabilize signal

## Beat end
id: 442102
line: 442303 | NoWait | Signal stabilized through generated Story config.
set-fact: 442601 | Bool | true
```

Parser requirements:

- Standard library only.
- Stable deterministic ids from explicit ids in the file; no random ids.
- Beat heading slug is local authoring name; generated config uses numeric `id`.
- `entry` references a beat slug.
- `line` emits `StoryStepConfig` with `Kind=Line`, `TextKey`, `WaitPolicy`, and source text metadata.
- `set-fact` emits a `StoryFactConfig` and `StoryStepConfig` with `Kind=SetFact`.
- `choice` emits `StoryChoiceConfig`; target beat slug may be `0` / `complete` to end the graph.
- Unsupported directives must produce structured diagnostics and a non-zero exit code.

## Generated `.story.json` Draft

Required output:

```text
Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
```

Minimum top-level shape:

```json
{
  "schema": "mx.story.config.draft.v1",
  "sourcePath": "Tools/MxFrameworkStoryAuthoring/fixtures/markdown/basic_choice_story.md",
  "graphs": [],
  "beats": [],
  "steps": [],
  "branches": [],
  "choices": [],
  "facts": [],
  "textKeys": [],
  "texts": []
}
```

Row property names should match Story.Config public DTO names:

- `StoryGraphConfig`: `Id`, `Version`, `EntryBeatId`, `SourcePath`
- `StoryBeatConfig`: `Id`, `GraphId`, `SortOrder`, `ChoiceSetId`, `TriggerIds`
- `StoryStepConfig`: `Id`, `GraphId`, `BeatId`, `SortOrder`, `Kind`, `TextKey`, `SpeakerId`, `ResourceId`, `WaitPolicy`, `FactNamespace`, `FactId`, `FactValueKind`, `FactValueRaw`, `AuxId`
- `StoryBranchConfig`: `Id`, `GraphId`, `BeatId`, `TargetBeatId`, `ConditionFactId`, `Priority`, `IsFallback`
- `StoryChoiceConfig`: `Id`, `GraphId`, `BeatId`, `SortOrder`, `LabelTextKey`, `TargetBeatId`, `ConditionFactId`, `EffectIds`
- `StoryFactConfig`: `Id`, `Namespace`, `ValueKind`

`texts` is authoring metadata only; it helps validate `TextKey` references and inspect generated drafts. Runtime Story.Config does not consume text content directly.

## CLI Contract

Required commands:

```text
python Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown Tools/MxFrameworkStoryAuthoring/fixtures/markdown/basic_choice_story.md --out Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
python Tools/MxFrameworkStoryAuthoring/story_authoring.py validate Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
python -m unittest discover Tools/MxFrameworkStoryAuthoring/tests
```

CLI behavior:

- `import-markdown`: parse Markdown, emit deterministic pretty JSON, print a concise success summary.
- `validate`: load `.story.json`, run external Story.Config draft validation, print diagnostics and return non-zero on errors.
- `--help` should work for root and subcommands.
- Use POSIX-friendly paths; no Unity, no project-specific absolute path requirement.

## Diagnostics

Diagnostics must be structured and testable:

```json
{
  "code": "MissingTextKey",
  "severity": "Error",
  "message": "Story step references missing text key 442301.",
  "path": "steps[0].TextKey"
}
```

Minimum codes to cover:

- `DuplicateId`
- `MissingEntryBeat`
- `MissingTextKey`
- `InvalidBranchTarget`
- `InvalidChoiceTarget`
- `InvalidTriggerId`
- `InvalidEffectId`
- `UnsupportedStepKind`
- `UnsupportedWaitPolicy`
- `UnsupportedDirective`

## Allowed Changes

- `Tools/MxFrameworkStoryAuthoring/**`
- `Docs/Interfaces/Story.Config.md` only if clarifying generated draft handoff.
- `Docs/INTERFACES.md`
- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- `Docs/README.md` if adding the new tool entry is necessary.
- This task document.

## Out Of Scope

- Runtime Story, Story.Runtime, Story.Unity, Story.Editor, Gameplay, Resources, or Runtime AI Planner code changes.
- Unity Editor hard dependency inside the external CLI.
- Yarn / Ink / Articy full importer.
- Authoring AI Assist implementation, prompt templates that execute, API keys, SDK clients, or LLM dependency wiring.
- WGame production narrative content.
- Generated Unity assets.

## Acceptance Criteria

- Markdown Story Outline v1 fixture imports into a deterministic `.story.json` draft.
- Generated draft includes graph, beats, line steps, choice, set-fact row, fact declarations, text key metadata, and source path metadata.
- Generated draft passes `story_authoring.py validate`.
- CLI emits structured diagnostics for the minimum diagnostic code set above.
- Unit tests cover successful import / validation and at least duplicate ids, missing text key, invalid choice target, unsupported directive, and unsupported step kind / wait policy.
- Docs explain the Markdown draft -> `.story.json` -> Story.Config handoff.
- Source inspection confirms no runtime assembly dependency on Authoring AI Assist or external narrative DSL libraries.
- Required validation commands pass.

## Validation

Required:

```text
git diff --check
python Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown Tools/MxFrameworkStoryAuthoring/fixtures/markdown/basic_choice_story.md --out Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
python Tools/MxFrameworkStoryAuthoring/story_authoring.py validate Tools/MxFrameworkStoryAuthoring/fixtures/generated/basic_choice_story.story.json
python -m unittest discover Tools/MxFrameworkStoryAuthoring/tests
```

If the implementation touches C# docs only, Unity validation is not required. If any Unity or C# assembly file is changed, run targeted Unity compile / tests and report exact results.

## Handoff Notes

The implementation agent must report:

- files read.
- files changed.
- module impact.
- public API / tool protocol impact.
- docs status.
- exact validation commands.
- Authoring AI Assist deferral confirmation.
- remaining Yarn / Ink / Articy / future Authoring AI Assist gaps.
