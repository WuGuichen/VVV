using System.Collections.Generic;
using System.Text.Json;
using MxFramework.CharacterApplication;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunTacticalDefaults()
    {
        var package = LoadSample("character-codex-flow-test");
        var baseline = Compile(package);
        package.TacticalDefaults = new CharacterTacticalDefaults {
            CharacterStableId = package.ApplicationConfig.CharacterStableId,
            ActionIds = new List<string> { "action.fixture" },
            Cards = new List<CharacterTacticalCardReference> { new() { CardId = "card.fixture", Revision = 2, ContentHash = "12345" } },
            Bindings = new List<CharacterTacticalSlotBinding> { new() { CardId = "card.fixture", SlotPath = "segment/action", TacticalActionId = "action.fixture" } }
        };
        var compiled = Compile(package);
        Require(compiled.Status == CharacterAuthoringCompilerStatus.Ready, "Explicit tactical defaults must compile.");
        Require(compiled.Hashes.SourcePackageHash != baseline.Hashes.SourcePackageHash, "Default tactical edits must invalidate the source hash.");
        Require(compiled.TacticalDefaults.Bindings[0].SlotPath == "segment/action", "The compiler must preserve explicit instance paths.");
        package.TacticalDefaults.Bindings.Clear();
        Require(compiled.TacticalDefaults.Bindings.Count == 1, "Compiled defaults must not alias the editable source.");
        var json = JsonSerializer.Serialize(compiled.TacticalDefaults, JsonOptions);
        var reopened = JsonSerializer.Deserialize<CharacterTacticalDefaults>(json, JsonOptions);
        Require(reopened.TryValidate(out _) && reopened.Cards[0].ContentHash == "12345", "Source roundtrip must preserve exact versions and hashes.");
        reopened.Bindings[0].TacticalActionId = "missing";
        Require(!reopened.TryValidate(out _), "Dangling default bindings must fail validation.");
        package.TacticalDefaults = reopened;
        Require(Compile(package).Status != CharacterAuthoringCompilerStatus.Ready, "Invalid defaults must block compilation.");
        package.TacticalDefaults = new CharacterTacticalDefaults { CharacterStableId = "another-character" };
        Require(Compile(package).Status != CharacterAuthoringCompilerStatus.Ready, "Defaults may not target another character.");
    }
}
