#!/usr/bin/env python3
"""Regenerate CharacterTest's embedded CharacterAction fallback from formal projections."""

from __future__ import annotations

import argparse
from pathlib import Path


HEADER = """namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestCharacterActionConfigSource
    {
        private const string EmbeddedActionSetsSourcePath = \"CharacterTest/embedded/character_action_sets.json\";
        private const string EmbeddedActionsSourcePath = \"CharacterTest/embedded/character_actions.json\";
        private const string EmbeddedReactionProfilesSourcePath = \"CharacterTest/embedded/character_reaction_profiles.json\";
        private const string EmbeddedLandingProfilesSourcePath = \"CharacterTest/embedded/character_landing_profiles.json\";

"""

FOOTER = """    }
}
"""


def verbatim_constant(name: str, source: Path) -> str:
    value = source.read_text(encoding="utf-8-sig").replace("\r\n", "\n").rstrip("\n")
    escaped = value.replace('"', '""')
    return f'        private const string {name} = @"\n{escaped}\n";\n\n'


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = args.project_root.resolve()

    generated = root / "Assets" / "MxFrameworkGenerated" / "CharacterActions"
    sources = (
        ("ActionSetsJson", generated / "ActionSets" / "codex_flow_test" / "character_action_sets.json"),
        ("ReactionProfilesJson", generated / "ActionSets" / "codex_flow_test" / "character_reaction_profiles.json"),
        ("LandingProfilesJson", generated / "ActionSets" / "codex_flow_test" / "character_landing_profiles.json"),
        ("ActionsJson", generated / "Libraries" / "Common" / "character_actions.json"),
    )
    missing = [str(path) for _, path in sources if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing formal CharacterAction projections: " + ", ".join(missing))

    content = HEADER + "".join(verbatim_constant(name, path) for name, path in sources) + FOOTER
    target = (
        root
        / "Assets"
        / "Scripts"
        / "WGameFrameworkProject"
        / "CharacterTest"
        / "Combat"
        / "CharacterTestCharacterActionConfigSource.EmbeddedJson.cs"
    )
    current = target.read_text(encoding="utf-8-sig").replace("\r\n", "\n") if target.is_file() else ""
    if args.check:
        if current != content:
            print(f"OUT_OF_DATE {target}")
            return 1
        print(f"UP_TO_DATE {target}")
        return 0

    target.write_text(content, encoding="utf-8", newline="\n")
    print(f"WROTE {target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
