using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabBehaviorCardPayload_ActionChainIsTypedAndValid()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardValidator.Validate(payload);

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics));
            Assert.AreEqual("dash", payload.EntryStepId);
            Assert.AreEqual(4, payload.Steps.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.CharacterAction, payload.Steps[0].Kind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeActionId, payload.Steps[0].CharacterActionId);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.CharacterAction, payload.Steps[2].Kind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, payload.Steps[2].CharacterActionId);
            Assert.IsTrue(payload.Steps[3].Branches.Any(
                branch => branch.TargetStepId == CharacterTestRuntimeAiLabCardBranch.EndTarget));
        }

        [Test]
        public void RuntimeAiLabBehaviorCardPayload_JsonRoundTripIsStable()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            string first = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(source);
            CharacterTestRuntimeAiLabBehaviorCardPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(first);
            string second = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(restored);

            Assert.AreEqual(first, second);
            Assert.AreEqual(source.Steps.Count, restored.Steps.Count);
            Assert.AreEqual(source.TriggerConditions.Count, restored.TriggerConditions.Count);
            Assert.AreEqual("light_attack_2", restored.Steps[2].Branches[0].TargetStepId);
        }

        [Test]
        public void RuntimeAiLabBehaviorCardCompiler_CompilesActionChainToImmutableProgram()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            string before = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(payload);
            CharacterTestRuntimeAiLabBehaviorCardCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(payload, CreateBehaviorCardReferences());

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Diagnostics));
            Assert.NotNull(result.Program);
            Assert.AreEqual("dash", result.Program.EntryStepId);
            Assert.AreEqual(0, result.Program.EntryStepIndex);
            Assert.AreEqual(4, result.Program.Steps.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.CharacterAction, result.Program.Steps[0].Kind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeActionId, result.Program.Steps[0].CharacterActionId);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.CharacterAction, result.Program.Steps[2].Kind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                result.Program.Steps[2].CharacterActionId);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardBranchTrigger.Condition, result.Program.Steps[0].Branches[0].Trigger);
            Assert.AreEqual("attack.first", result.Program.Steps[0].Branches[0].TargetStepId);
            Assert.IsTrue(result.Program.Steps[3].Branches.Any(branch => branch.EndsCard));
            Assert.IsFalse(result.Program.Steps is CharacterTestRuntimeAiLabCompiledStep[]);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CharacterTestRuntimeAiLabCompiledStep>)result.Program.Steps).Add(null));
            Assert.AreEqual(before, CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(payload));
        }

        [Test]
        public void RuntimeAiLabBehaviorCardCompiler_HashIsStableAndTracksSemanticChanges()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = CreateBehaviorCardReferences();
            CharacterTestRuntimeAiLabBehaviorCardCompileResult first =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(source, references);
            CharacterTestRuntimeAiLabBehaviorCardPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(
                    CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(source));
            CharacterTestRuntimeAiLabBehaviorCardCompileResult roundTrip =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(restored, references);
            CharacterTestRuntimeAiLabBehaviorCardPayload changed = restored.Clone();
            changed.Steps[0].Branches[0].WindowEndFrame++;
            CharacterTestRuntimeAiLabBehaviorCardCompileResult changedResult =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(changed, references);

            Assert.IsTrue(first.Success, FormatDiagnostics(first.Diagnostics));
            Assert.IsTrue(roundTrip.Success, FormatDiagnostics(roundTrip.Diagnostics));
            Assert.IsTrue(changedResult.Success, FormatDiagnostics(changedResult.Diagnostics));
            Assert.AreEqual(first.Program.ContentHash, roundTrip.Program.ContentHash);
            Assert.AreNotEqual(first.Program.ContentHash, changedResult.Program.ContentHash);
        }

        [Test]
        public void RuntimeAiLabBehaviorCardCompiler_RejectsUnknownReferencesAndDuplicateOrder()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            payload.Steps[1].Order = payload.Steps[0].Order;
            payload.Steps[0].MoveMinDistance = float.NaN;
            var emptyReferences = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                new AbilityDefinitionRegistry(),
                new CharacterActionConfigRegistry(
                    Array.Empty<CharacterActionSetConfig>(),
                    Array.Empty<CharacterActionConfig>()));
            CharacterTestRuntimeAiLabBehaviorCardCompileResult result =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(payload, emptyReferences);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Program);
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "CARD_COMPILE_STEP_ORDER_DUPLICATE"));
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "CARD_COMPILE_MOVE_DISTANCE_NON_FINITE"));
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "CARD_COMPILE_ABILITY_UNKNOWN"));
            Assert.IsTrue(result.Diagnostics.Any(item => item.Code == "CARD_COMPILE_ACTION_UNKNOWN"));
            Assert.IsTrue(result.Diagnostics.Any(item => item.Path.Contains("abilityId")));
            Assert.IsTrue(result.Diagnostics.Any(item => item.Path.Contains("characterActionId")));
        }

        [Test]
        public void RuntimeAiLabBehaviorCardCompiler_CompilesLegacyMoveAndWaitSteps()
        {
            CharacterTestRuntimeAiLabBehaviorCardCompileResult legacy =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                        "Fallback.WaitOrHold",
                        CharacterTestRuntimeAiPlannerActionKind.Wait),
                    references: null);
            CharacterTestRuntimeAiLabBehaviorCardPayload moveAndWait = CreateMoveAndWaitCard();
            CharacterTestRuntimeAiLabBehaviorCardCompileResult typed =
                CharacterTestRuntimeAiLabBehaviorCardCompiler.Compile(moveAndWait, references: null);

            Assert.IsTrue(legacy.Success, FormatDiagnostics(legacy.Diagnostics));
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.Wait, legacy.Program.Steps[0].Kind);
            Assert.IsTrue(typed.Success, FormatDiagnostics(typed.Diagnostics));
            Assert.AreEqual(2, typed.Program.Steps.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.MoveToTarget, typed.Program.Steps[0].Kind);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.Wait, typed.Program.Steps[1].Kind);
            Assert.AreEqual(12, typed.Program.Steps[1].WaitFrames);
            Assert.IsTrue(typed.Program.Steps[1].Branches[0].EndsCard);
        }

        [Test]
        public void RuntimeAiLabBehaviorCardCompileCache_InvalidatesOnDraftRevision()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            CharacterTestRuntimeAiLabBehaviorCardDraft original = workspace.Cards[group.Entries[0].CardDraftId];
            var cache = new CharacterTestRuntimeAiLabBehaviorCardCompileCache();
            CharacterTestRuntimeAiLabBehaviorCardCompileResult first =
                cache.GetOrCompile(workspace, original, references: null);
            CharacterTestRuntimeAiLabBehaviorCardCompileResult cached =
                cache.GetOrCompile(workspace, original, references: null);

            Assert.AreSame(first, cached);
            Assert.IsTrue(workspace.TryReplaceCardPayload(1, 0, 0, CreateMoveAndWaitCard(), out string message), message);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out group));
            CharacterTestRuntimeAiLabBehaviorCardDraft changed = workspace.Cards[group.Entries[0].CardDraftId];
            CharacterTestRuntimeAiLabBehaviorCardCompileResult recompiled =
                cache.GetOrCompile(workspace, changed, references: null);
            Assert.AreNotSame(first, recompiled);
            Assert.IsTrue(recompiled.Success, FormatDiagnostics(recompiled.Diagnostics));
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateBehaviorCardReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardPayload CreateMoveAndWaitCard()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardPayload
            {
                EntryStepId = "move",
                Intent = "Move into range, then wait."
            };
            var move = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = "move",
                Order = 10,
                Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                Title = "Move",
                MoveMinDistance = 1f,
                MoveMaxDistance = 3f
            };
            move.Branches.Add(new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = "move.success",
                Trigger = CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess,
                Priority = 100,
                TargetStepId = "wait"
            });
            var wait = new CharacterTestRuntimeAiLabCardStep
            {
                StepId = "wait",
                Order = 20,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                Title = "Wait",
                WaitFrames = 12
            };
            wait.Branches.Add(new CharacterTestRuntimeAiLabCardBranch
            {
                BranchId = "wait.success",
                Trigger = CharacterTestRuntimeAiLabCardBranchTrigger.DefaultSuccess,
                Priority = 100,
                TargetStepId = CharacterTestRuntimeAiLabCardBranch.EndTarget,
                EndReason = "Complete"
            });
            payload.Steps.Add(move);
            payload.Steps.Add(wait);
            return payload;
        }

        private static string FormatDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            return string.Join(
                "\n",
                diagnostics.Select(item => item.Code + " @ " + item.Path + ": " + item.Message));
        }

        [Test]
        public void RuntimeAiLabBehaviorCardPayload_ValidatorRejectsBrokenGraphAndPayload()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            payload.Steps[1].StepId = payload.Steps[0].StepId;
            payload.Steps[0].AbilityId = 0;
            payload.Steps[0].Branches[0].TargetStepId = "missing";
            payload.Steps[0].Branches[1].TargetStepId = "dash";

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardValidator.Validate(payload);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics));
            Assert.IsTrue(diagnostics.Any(item => item.Code == "CARD_STEP_ID_DUPLICATE"));
            Assert.IsTrue(diagnostics.Any(item => item.Code == "CARD_ABILITY_ID_MISSING"));
            Assert.IsTrue(diagnostics.Any(item => item.Code == "CARD_BRANCH_TARGET_MISSING"));
            Assert.IsTrue(diagnostics.Any(item => item.Code == "CARD_GRAPH_CYCLE"));
        }

        [Test]
        public void RuntimeAiLabBehaviorCardPayload_JsonRejectsUnknownEnum()
        {
            string json = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            json = json.Replace("\"UseAbility\"", "\"ExecuteScript\"");
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(json));
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_RoundTripsWorkspaceAndStableIds()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-runtime-ai-lab-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
                arena.Team1Slots[1].Enabled = true;
                arena.SetSpawnPosition(1, 1, new Vector2(-19f, 7f));
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(arena);
                Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string mutationMessage), mutationMessage);
                Assert.IsTrue(workspace.TryReplaceCardPayload(
                    1,
                    0,
                    0,
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture(),
                    out string cardMessage), cardMessage);
                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                string workspaceId = workspace.WorkspaceId;
                Assert.IsTrue(persistence.TryLoad(workspaceId, out CharacterTestRuntimeAiLabDraftWorkspace loaded, out string loadMessage), loadMessage);
                Assert.AreEqual(workspaceId, loaded.WorkspaceId);
                Assert.IsFalse(loaded.Dirty);
                Assert.IsTrue(loaded.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft loadedGroup));
                Assert.AreEqual(workspace.TryGetSlot(1, 0).GroupDraftId, loadedGroup.GroupDraftId);
                Assert.Greater(loadedGroup.Entries.Count, 1);
                Assert.IsTrue(loaded.Cards.TryGetValue(
                    loadedGroup.Entries[0].CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft loadedCard));
                Assert.AreEqual(4, loadedCard.Payload.Steps.Count);
                Assert.AreEqual("dash", loadedCard.Payload.EntryStepId);
                CharacterTestArenaMatchConfig restoredArena = CharacterTestArenaMatchConfig.CreateDefault();
                loaded.RestoreSetupToArena(restoredArena);
                Assert.IsTrue(restoredArena.Team1Slots[1].Enabled);
                Assert.IsTrue(restoredArena.TryGetSpawnPosition(1, 1, out Vector2 restoredSpawn));
                Assert.AreEqual(new Vector2(-19f, 7f), restoredSpawn);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_ReadCardRoutesV9LogicThroughV10Migration()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-v9-card-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                    CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
                Assert.IsTrue(seed.Success, seed.Diagnostic);
                Assert.IsTrue(workspace.EnsurePublishedProjectCardDrafts(seed.Cards));
                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                CharacterTestRuntimeAiLabBehaviorCardDraft source = workspace.Cards.Values.First();
                string cardPath = Path.Combine(
                    root,
                    workspace.WorkspaceId,
                    "cards",
                    source.CardDraftId + ".json");
                string v9Json = File.ReadAllText(cardPath)
                    .Replace(
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV9Schema)
                    .Replace("\"behaviorRecipeVersion\": 10", "\"behaviorRecipeVersion\": 9");
                MethodInfo readCard = typeof(CharacterTestRuntimeAiLabDraftPersistence).GetMethod(
                    "ReadCard",
                    BindingFlags.Static | BindingFlags.NonPublic);

                Assert.NotNull(readCard);
                CharacterTestRuntimeAiLabBehaviorCardDraft restored =
                    (CharacterTestRuntimeAiLabBehaviorCardDraft)readCard.Invoke(null, new object[] { v9Json });

                Assert.NotNull(restored);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                    restored.LogicPayload.BehaviorRecipeVersion);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_ReloadsAfterGitNormalizesJsonLineEndings()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-git-normalization-" + Guid.NewGuid().ToString("N"));
            try
            {
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    writer.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                writer.MarkDirty();
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                string workspaceDirectory = Path.Combine(root, workspace.WorkspaceId);
                string[] jsonPaths = Directory.GetFiles(
                    workspaceDirectory,
                    "*.json",
                    SearchOption.AllDirectories);
                for (int i = 0; i < jsonPaths.Length; i++)
                {
                    string normalized = File.ReadAllText(jsonPaths[i])
                        .Replace("\r\n", "\n")
                        .Replace("\r", "\n");
                    File.WriteAllText(
                        jsonPaths[i],
                        normalized,
                        new System.Text.UTF8Encoding(false));
                }

                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                Assert.IsTrue(
                    reader.TryLoad(
                        workspace.WorkspaceId,
                        out CharacterTestRuntimeAiLabDraftWorkspace loaded,
                        out string message),
                    message);
                Assert.AreEqual(workspace.WorkspaceId, loaded.WorkspaceId);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_RepeatedLoadsDoNotGrowFallbackCards()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-idempotent-fallback-" + Guid.NewGuid().ToString("N"));
            try
            {
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace original =
                    writer.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in original.Groups.Values)
                {
                    for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                    {
                        CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                        if (!string.Equals(
                                entry.BehaviorId,
                                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                                StringComparison.Ordinal)
                            || !original.Cards.TryGetValue(
                                entry.CardDraftId,
                                out CharacterTestRuntimeAiLabBehaviorCardDraft fallback))
                        {
                            continue;
                        }

                        // Reproduce the persisted v6/v7 migration residue that caused core-slot
                        // validation to append one replacement fallback on every workspace load.
                        for (int timelineIndex = 0;
                             timelineIndex < fallback.LogicPayload.Timelines.Count;
                             timelineIndex++)
                        {
                            fallback.LogicPayload.Timelines[timelineIndex].Events.Clear();
                        }
                    }
                }
                writer.MarkDirty();
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                string workspaceId = original.WorkspaceId;
                var migrationReader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                Assert.IsTrue(
                    migrationReader.TryLoad(
                        workspaceId,
                        out CharacterTestRuntimeAiLabDraftWorkspace migrated,
                        out string migrationMessage),
                    migrationMessage);
                if (migrated.Dirty)
                {
                    migrationReader.AdoptWorkspace(migrated);
                    migrationReader.MarkDirty();
                    Assert.IsTrue(migrationReader.Flush(), migrationReader.LastStatus);
                }
                var expectedCounts = migrated.Groups.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Entries.Count);

                for (int iteration = 0; iteration < 3; iteration++)
                {
                    var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                    Assert.IsTrue(
                        reader.TryLoad(
                            workspaceId,
                            out CharacterTestRuntimeAiLabDraftWorkspace loaded,
                            out string loadMessage),
                        loadMessage);
                    Assert.IsFalse(
                        loaded.EnsureCoreSlotContracts(markMigrated: true),
                        "Core-slot repair was not idempotent on load " + iteration + ".");
                    foreach (KeyValuePair<string, int> expected in expectedCounts)
                    {
                        Assert.AreEqual(
                            expected.Value,
                            loaded.Groups[expected.Key].Entries.Count,
                            "Group " + expected.Key + " grew during load " + iteration + ".");
                        Assert.AreEqual(
                            1,
                            loaded.Groups[expected.Key].Entries.Count(entry => string.Equals(
                                entry.BehaviorId,
                                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                                StringComparison.Ordinal)),
                            "Group " + expected.Key + " contains duplicate fallback cards.");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_RoundTripsExplicitSaveRevisionWithoutAcknowledgingRecoveryAutosave()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-explicit-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                int explicitSaveRevision = workspace.CurrentRevision;
                workspace.LastExplicitSaveRevision = explicitSaveRevision;
                workspace.CurrentRevision++;
                persistence.MarkDirty();

                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                Assert.IsTrue(
                    persistence.TryLoad(
                        workspace.WorkspaceId,
                        out CharacterTestRuntimeAiLabDraftWorkspace loaded,
                        out string loadMessage),
                    loadMessage);
                Assert.AreEqual(workspace.CurrentRevision, loaded.CurrentRevision);
                Assert.AreEqual(explicitSaveRevision, loaded.LastExplicitSaveRevision);
                Assert.AreNotEqual(
                    loaded.CurrentRevision,
                    loaded.LastExplicitSaveRevision,
                    "Recovery autosave must persist pending explicit edits without acknowledging the Save button.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        [Ignore("Superseded by one-to-one behavior responsibility migration coverage.")]
        public void RuntimeAiLabDraftPersistence_V1MigratesToFourCoreSlotsAndMarksDraftDirty()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-v1-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryGetGroupForSlot(
                    1,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft sourceGroup));
                string stableEntryId = sourceGroup.Entries[0].EntryId;
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);

                string workspaceDirectory = Path.Combine(root, workspace.WorkspaceId);
                string workspacePath = Path.Combine(workspaceDirectory, "workspace.json");
                var workspaceJson = Newtonsoft.Json.Linq.JObject.Parse(
                    File.ReadAllText(workspacePath));
                workspaceJson["schema"] = "mx.character-test.runtime-ai-lab.workspace.v1";
                string[] groupPaths = Directory.GetFiles(
                    Path.Combine(workspaceDirectory, "groups"),
                    "*.json");
                for (int i = 0; i < groupPaths.Length; i++)
                {
                    var groupJson = Newtonsoft.Json.Linq.JObject.Parse(
                        File.ReadAllText(groupPaths[i]));
                    groupJson["schema"] = "mx.character-test.runtime-ai-lab.behavior-group.v1";
                    var entries = groupJson["entries"] as Newtonsoft.Json.Linq.JArray;
                    for (int entryIndex = 0; entries != null && entryIndex < entries.Count; entryIndex++)
                        ((Newtonsoft.Json.Linq.JObject)entries[entryIndex]).Remove("slotKind");
                    string migratedJson = groupJson.ToString();
                    File.WriteAllText(groupPaths[i], migratedJson);
                    string relativePath = "groups/" + Path.GetFileName(groupPaths[i]);
                    var groupReferences = (Newtonsoft.Json.Linq.JArray)workspaceJson["groups"];
                    for (int referenceIndex = 0;
                        groupReferences != null && referenceIndex < groupReferences.Count;
                        referenceIndex++)
                    {
                        var reference = (Newtonsoft.Json.Linq.JObject)groupReferences[referenceIndex];
                        if (!string.Equals(
                            (string)reference["path"],
                            relativePath,
                            StringComparison.Ordinal))
                            continue;
                        using (System.Security.Cryptography.SHA256 sha =
                            System.Security.Cryptography.SHA256.Create())
                        {
                            byte[] bytes = sha.ComputeHash(
                                System.Text.Encoding.UTF8.GetBytes(migratedJson));
                            var hash = new System.Text.StringBuilder(bytes.Length * 2);
                            for (int b = 0; b < bytes.Length; b++)
                                hash.Append(bytes[b].ToString("x2", CultureInfo.InvariantCulture));
                            reference["hash"] = hash.ToString();
                        }
                        break;
                    }
                }
                File.WriteAllText(workspacePath, workspaceJson.ToString());

                Assert.IsTrue(persistence.TryLoad(
                    workspace.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace migrated,
                    out string loadMessage), loadMessage);
                Assert.IsTrue(migrated.Dirty);
                StringAssert.Contains("迁移", migrated.Diagnostic);
                Assert.IsTrue(migrated.TryGetGroupForSlot(
                    1,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft migratedGroup));
                Assert.AreEqual(stableEntryId, migratedGroup.Entries[0].EntryId);
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                        migratedGroup.Profile.Entries,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn()).Success);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        [Ignore("v3 migration preserves authored entries and never creates placeholders.")]
        public void RuntimeAiLabDraftPersistence_V2ReplacesLegacyTrainingDummyCombatWithPlaceholdersAndMarksDirty()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-v2-training-dummy-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                CharacterTestRuntimeAiLabBehaviorGroupDraft dummy = workspace.Groups.Values.Single(
                    group => string.Equals(
                        group.PublishedStableId,
                        CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                        StringComparison.Ordinal));
                CharacterTestRuntimeAiLabBehaviorGroupDraft melee = workspace.Groups.Values.Single(
                    group => string.Equals(
                        group.PublishedStableId,
                        "arena.default-melee",
                        StringComparison.Ordinal));
                for (int i = dummy.Entries.Count - 1; i >= 0; i--)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = dummy.Entries[i];
                    if (entry.CoreSlotKind != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure
                        && entry.CoreSlotKind != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage)
                    {
                        continue;
                    }
                    dummy.Entries.RemoveAt(i);
                }
                AddLegacyCoreEntry(
                    dummy,
                    melee,
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure);
                AddLegacyCoreEntry(
                    dummy,
                    melee,
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage);
                dummy.NormalizeOrders();
                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);

                Assert.IsTrue(persistence.TryLoad(
                    workspace.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace migrated,
                    out string loadMessage), loadMessage);
                Assert.IsTrue(migrated.Dirty);
                StringAssert.Contains("迁移", migrated.Diagnostic);
                CharacterTestRuntimeAiLabBehaviorGroupDraft migratedDummy =
                    migrated.Groups.Values.Single(group => string.Equals(
                        group.PublishedStableId,
                        CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                        StringComparison.Ordinal));
                Assert.AreEqual(4, migratedDummy.Entries.Count);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                    },
                    migratedDummy.Entries.Select(entry => entry.BehaviorId).ToArray());
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabPublishedCardCopyOnWrite_RoundTripsSourceIdentityAndReusesDraft()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-published-cow-" + Guid.NewGuid().ToString("N"));
            try
            {
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload sourceLogic =
                    CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                        CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic());
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult sourceCompile =
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(sourceLogic);
                Assert.IsTrue(sourceCompile.Success);
                var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                    "Reactive Dodge",
                    "ui.card.reactive-dodge",
                    2,
                    "Dodge an incoming threat.",
                    "Survival",
                    "Requires timing",
                    new[] { "Defense" },
                    new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Defense });
                var published = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                    "bc_g6a5fw319a",
                    7,
                    1,
                    1,
                    metadata,
                    sourceLogic,
                    sourceCompile.Program.SemanticHash,
                    0x12345678L);

                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(workspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft originalGroup));
                string originalGroupDraftId = originalGroup.GroupDraftId;
                int originalGroupCount = workspace.Groups.Count;
                int originalCardCount = workspace.Cards.Count;
                var originalRuntimeEntries = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry[
                    originalGroup.Profile.Entries.Count];
                for (int i = 0; i < originalRuntimeEntries.Length; i++)
                    originalRuntimeEntries[i] = originalGroup.Profile.Entries[i].Clone();
                CharacterTestRuntimeAiLabBehaviorCardDraft embeddedPublishedProjection =
                    workspace.Cards[originalGroup.Entries[0].CardDraftId];
                embeddedPublishedProjection.PublishedStableId = published.StableCardId;
                embeddedPublishedProjection.SourcePublishedRevision = published.Revision;
                embeddedPublishedProjection.SourcePublishedSemanticHash = published.SemanticHash;
                embeddedPublishedProjection.SourcePublishedContentHash = published.ContentHash;
                embeddedPublishedProjection.LogicPayload = published.CloneLogic();
                Assert.IsTrue(workspace.TryOpenPublishedCardForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    published,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft,
                    out bool changed,
                    out string message), message);
                Assert.IsTrue(changed);
                Assert.AreEqual(0, entryIndex);
                Assert.IsTrue(draft.HasPublishedSource);
                Assert.AreEqual(7, draft.SourcePublishedRevision);
                Assert.AreEqual(published.SemanticHash, draft.SourcePublishedSemanticHash);
                Assert.AreEqual(published.ContentHash, draft.SourcePublishedContentHash);
                Assert.AreNotSame(sourceLogic, draft.LogicPayload);
                Assert.IsTrue(workspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft firstIsolatedGroup));
                Assert.AreNotEqual(originalGroupDraftId, firstIsolatedGroup.GroupDraftId);
                Assert.AreEqual(1, firstIsolatedGroup.Entries.Count);

                Assert.IsTrue(workspace.TryUndo(out message), message);
                Assert.AreEqual(originalGroupCount, workspace.Groups.Count);
                Assert.AreEqual(originalCardCount, workspace.Cards.Count);
                Assert.IsTrue(workspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft restoredGroup));
                Assert.AreEqual(originalGroupDraftId, restoredGroup.GroupDraftId);
                Assert.AreEqual(originalRuntimeEntries.Length, restoredGroup.Entries.Count);
                Assert.AreEqual(originalRuntimeEntries.Length, restoredGroup.Profile.Entries.Count);
                for (int i = 0; i < originalRuntimeEntries.Length; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry expected = originalRuntimeEntries[i];
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry actual = restoredGroup.Profile.Entries[i];
                    Assert.AreEqual(expected.EntryId, actual.EntryId);
                    Assert.AreEqual(expected.BehaviorId, actual.BehaviorId);
                    Assert.AreEqual(expected.Order, actual.Order);
                    Assert.AreEqual(expected.Disabled, actual.Disabled);
                }

                Assert.IsTrue(workspace.TryOpenPublishedCardForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    published,
                    out entryIndex,
                    out draft,
                    out changed,
                    out message), message);
                Assert.IsTrue(changed);

                Assert.IsTrue(workspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft isolatedGroup));
                string isolatedGroupDraftId = isolatedGroup.GroupDraftId;
                string isolatedCardDraftId = draft.CardDraftId;
                Assert.AreEqual(string.Empty, isolatedGroup.PublishedStableId);
                Assert.IsTrue(isolatedGroup.IsBehaviorTestComposition);
                Assert.AreEqual(
                    isolatedCardDraftId,
                    isolatedGroup.PrimaryBehaviorTestCardDraftId(workspace.Cards));
                Assert.IsFalse(workspace.TryToggleEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    entryIndex,
                    out message));
                StringAssert.Contains("当前行为", message);
                Assert.IsFalse(workspace.TryRemoveEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    entryIndex,
                    out message));
                StringAssert.Contains("当前行为", message);
                CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
                Assert.IsTrue(arena.SetSpawnPosition(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    new Vector2(-3f, 1f)));
                Assert.IsTrue(workspace.SyncSlotFromArena(
                    arena,
                    CharacterTestArenaMatchConfig.Team1Id,
                    0));
                Assert.AreEqual(
                    isolatedGroupDraftId,
                    workspace.TryGetSlot(CharacterTestArenaMatchConfig.Team1Id, 0).GroupDraftId);
                Assert.AreEqual(
                    isolatedCardDraftId,
                    workspace.Groups[isolatedGroupDraftId].Entries[0].CardDraftId);

                Assert.IsTrue(workspace.TryAdoptPublishedRevision(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    entryIndex,
                    published,
                    out message), message);
                Assert.AreEqual(string.Empty, isolatedGroup.PublishedStableId);
                Assert.IsTrue(arena.SetSpawnPosition(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    new Vector2(-2f, 1f)));
                Assert.IsTrue(workspace.SyncSlotFromArena(
                    arena,
                    CharacterTestArenaMatchConfig.Team1Id,
                    0));
                Assert.AreEqual(
                    isolatedGroupDraftId,
                    workspace.TryGetSlot(CharacterTestArenaMatchConfig.Team1Id, 0).GroupDraftId);
                Assert.AreEqual(
                    isolatedCardDraftId,
                    workspace.Groups[isolatedGroupDraftId].Entries[0].CardDraftId);

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload edited = draft.LogicPayload.Clone();
                edited.Stages[0].PlannedDurationFrames += 15;
                Assert.IsTrue(workspace.TryReplaceCardLogicPayload(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    entryIndex,
                    edited,
                    out message), message);
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult immutableSourceCompile =
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(published.CloneLogic());
                Assert.AreEqual(published.SemanticHash, immutableSourceCompile.Program.SemanticHash);

                Assert.IsTrue(workspace.TryOpenPublishedCardForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    published,
                    out int reopenedEntryIndex,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft reopened,
                    out bool reopenedChanged,
                    out message), message);
                Assert.IsFalse(reopenedChanged);
                Assert.AreEqual(entryIndex, reopenedEntryIndex);
                Assert.AreEqual(draft.CardDraftId, reopened.CardDraftId);

                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                Assert.IsTrue(persistence.TryLoad(
                    workspace.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace loaded,
                    out string loadMessage), loadMessage);
                Assert.IsTrue(loaded.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft loadedGroup));
                CharacterTestRuntimeAiLabBehaviorCardDraft loadedCard =
                    loaded.Cards[isolatedCardDraftId];
                Assert.AreEqual(published.StableCardId, loadedCard.PublishedStableId);
                Assert.AreEqual(published.Revision, loadedCard.SourcePublishedRevision);
                Assert.AreEqual(published.SemanticHash, loadedCard.SourcePublishedSemanticHash);
                Assert.AreEqual(published.ContentHash, loadedCard.SourcePublishedContentHash);
                Assert.AreEqual(
                    arena.Team1Slots[0].BehaviorGroupDraftId,
                    loaded.TryGetSlot(CharacterTestArenaMatchConfig.Team1Id, 0).ArenaBehaviorGroupDraftId);
                Assert.IsTrue(arena.SetSpawnPosition(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    new Vector2(-1f, 1f)));
                Assert.IsTrue(loaded.SyncSlotFromArena(
                    arena,
                    CharacterTestArenaMatchConfig.Team1Id,
                    0));
                Assert.AreEqual(
                    isolatedGroupDraftId,
                    loaded.TryGetSlot(CharacterTestArenaMatchConfig.Team1Id, 0).GroupDraftId);
                bool retainedIsolatedCard = false;
                IReadOnlyList<CharacterTestRuntimeAiLabBehaviorEntryDraft> loadedEntries =
                    loaded.Groups[isolatedGroupDraftId].Entries;
                for (int loadedEntryIndex = 0;
                     loadedEntryIndex < loadedEntries.Count;
                     loadedEntryIndex++)
                {
                    if (string.Equals(
                            loadedEntries[loadedEntryIndex].CardDraftId,
                            isolatedCardDraftId,
                            StringComparison.Ordinal))
                    {
                        retainedIsolatedCard = true;
                        break;
                    }
                }
                Assert.IsTrue(
                    retainedIsolatedCard,
                    "Core-slot normalization must retain the isolated Published card by stable draft identity.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_RejectsInvalidDraftAndKeepsLastValidSnapshot()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-runtime-ai-lab-invalid-v2-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                int flexibleEntryIndex = -1;
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    if (group.Entries[entryIndex].CoreSlotKind
                        == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible)
                    {
                        flexibleEntryIndex = entryIndex;
                        break;
                    }
                }
                Assert.GreaterOrEqual(flexibleEntryIndex, 0);
                string stableEntryId = group.Entries[flexibleEntryIndex].EntryId;
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    workspace.Cards[group.Entries[flexibleEntryIndex].CardDraftId];
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload missingAction = card.LogicPayload.Clone();
                missingAction.Timelines[0].Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = "event.character-action-without-reference.persistence-test",
                    TimelineId = missingAction.Timelines[0].TimelineId,
                    Title = "未选择角色动作",
                    StartFrame = 9,
                    TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                    DurationFrames = 1,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                        CharacterActionId = CharacterActionId.None
                    }
                });
                Assert.IsTrue(workspace.TryReplaceCardLogicPayload(
                    1,
                    0,
                    flexibleEntryIndex,
                    missingAction,
                    out string missingActionMutationMessage), missingActionMutationMessage);
                persistence.MarkDirty();
                Assert.IsFalse(persistence.Flush());
                StringAssert.Contains("CARD_V6_EVENT_ACTION_REQUIRED", persistence.LastStatus);
                Assert.IsTrue(workspace.TryUndo(out string undoMissingActionMessage), undoMissingActionMessage);
                card = workspace.Cards[group.Entries[flexibleEntryIndex].CardDraftId];

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload invalid = card.LogicPayload.Clone();
                Assert.That(invalid.Timelines.Count, Is.GreaterThan(0));
                invalid.Timelines[0].Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = "event.unconfigured.persistence-test",
                    TimelineId = invalid.Timelines[0].TimelineId,
                    Title = "未配置事件",
                    StartFrame = 10,
                    DurationFrames = 1,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured
                    }
                });
                Assert.IsTrue(workspace.TryReplaceCardLogicPayload(
                    1,
                    0,
                    flexibleEntryIndex,
                    invalid,
                    out string mutationMessage), mutationMessage);
                persistence.MarkDirty();
                Assert.IsFalse(persistence.Flush());
                Assert.IsTrue(workspace.Dirty);
                StringAssert.Contains("was not saved", persistence.LastStatus);

                Assert.IsTrue(persistence.TryLoad(
                    workspace.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace loaded,
                    out string loadMessage), loadMessage);
                Assert.IsTrue(loaded.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft loadedGroup));
                CharacterTestRuntimeAiLabBehaviorEntryDraft loadedEntry = null;
                for (int entryIndex = 0; entryIndex < loadedGroup.Entries.Count; entryIndex++)
                {
                    if (string.Equals(
                            loadedGroup.Entries[entryIndex].EntryId,
                            stableEntryId,
                            StringComparison.Ordinal))
                    {
                        loadedEntry = loadedGroup.Entries[entryIndex];
                        break;
                    }
                }
                Assert.IsNotNull(loadedEntry);
                CharacterTestRuntimeAiLabBehaviorCardDraft loadedCard =
                    loaded.Cards[loadedEntry.CardDraftId];
                Assert.IsFalse(loadedCard.LogicPayload.Timelines[0].Events.Exists(
                    evt => string.Equals(
                        evt?.EventId,
                        "event.unconfigured.persistence-test",
                        StringComparison.Ordinal)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_CorruptCurrentRecoversPreviousWithoutOverwrite()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-runtime-ai-lab-recovery-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string mutationMessage), mutationMessage);
                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                string workspacePath = Path.Combine(root, workspace.WorkspaceId, "workspace.json");
                File.WriteAllText(workspacePath, "{corrupt");

                Assert.IsTrue(persistence.TryLoad(workspace.WorkspaceId, out CharacterTestRuntimeAiLabDraftWorkspace recovered, out string loadMessage), loadMessage);
                Assert.IsTrue(recovered.RecoveredFromPrevious);
                StringAssert.Contains("Recovered from previous", recovered.Diagnostic);
                Assert.AreEqual("{corrupt", File.ReadAllText(workspacePath));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_NewerWorkspaceOpensReadOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-runtime-ai-lab-readonly-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                string workspacePath = Path.Combine(root, workspace.WorkspaceId, "workspace.json");
                string json = File.ReadAllText(workspacePath)
                    .Replace(CharacterTestRuntimeAiLabDraftWorkspace.Schema, "mx.character-test.runtime-ai-lab.workspace.v4");
                File.WriteAllText(workspacePath, json);

                Assert.IsTrue(persistence.TryLoad(workspace.WorkspaceId, out CharacterTestRuntimeAiLabDraftWorkspace loaded, out string loadMessage), loadMessage);
                Assert.IsTrue(loaded.IsReadOnly);
                Assert.IsFalse(loaded.TryDuplicateEntry(1, 0, 0, out string mutationMessage));
                StringAssert.Contains("read-only", mutationMessage);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_SaveFailureKeepsWorkspaceDirty()
        {
            string parent = Path.Combine(Path.GetTempPath(), "mx-runtime-ai-lab-failure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(parent);
            string invalidRoot = Path.Combine(parent, "file-not-directory");
            File.WriteAllText(invalidRoot, "blocked");
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(invalidRoot);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsFalse(persistence.Flush());
                Assert.IsTrue(workspace.Dirty);
                StringAssert.Contains("save failed", persistence.LastStatus);
            }
            finally
            {
                if (Directory.Exists(parent))
                    Directory.Delete(parent, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_OpenOrCreateRestoresFullLatestSetup()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-explicit-recovery-" + Guid.NewGuid().ToString("N"));
            try
            {
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestArenaMatchConfig savedConfig = CharacterTestArenaMatchConfig.CreateDefault();
                savedConfig.TryGetSlot(1, 0).Enabled = false;
                savedConfig.TryGetSlot(1, 1).Enabled = true;
                savedConfig.TryGetSlot(1, 0).CharacterId = 54321;
                savedConfig.TryGetSlot(1, 0).BehaviorGroupDraftId = 4;
                savedConfig.TryGetSlot(1, 0).LoadoutId =
                    CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(4);
                savedConfig.TryGetSlot(1, 0).VariantId =
                    CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
                Assert.IsTrue(savedConfig.SetSpawnPosition(1, 0, new Vector2(-13f, 8f)));
                CharacterTestRuntimeAiLabDraftWorkspace saved =
                    writer.OpenOrCreate(savedConfig);
                writer.MarkDirty();
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestArenaMatchConfig currentConfig = CharacterTestArenaMatchConfig.CreateDefault();
                CharacterTestRuntimeAiLabDraftWorkspace restored = reader.OpenOrCreate(currentConfig);
                Assert.AreSame(restored, reader.Workspace);
                Assert.AreEqual(saved.WorkspaceId, restored.WorkspaceId);
                restored.RestoreSetupToArena(currentConfig);
                Assert.IsTrue(currentConfig.TryGetSpawnPosition(1, 0, out Vector2 restoredPosition));
                Assert.AreEqual(new Vector2(-13f, 8f), restoredPosition);
                Assert.IsFalse(currentConfig.TryGetSlot(1, 0).Enabled);
                Assert.IsTrue(currentConfig.TryGetSlot(1, 1).Enabled);
                Assert.AreEqual(54321, currentConfig.TryGetSlot(1, 0).CharacterId);
                Assert.AreEqual(4, currentConfig.TryGetSlot(1, 0).BehaviorGroupDraftId);
                Assert.AreEqual(
                    CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(4),
                    currentConfig.TryGetSlot(1, 0).LoadoutId);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_RepeatedOpenOrCreateReusesOneWorkspaceDirectory()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-reuse-" + Guid.NewGuid().ToString("N"));
            try
            {
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace original =
                    writer.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                for (int iteration = 0; iteration < 4; iteration++)
                {
                    var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                    CharacterTestRuntimeAiLabDraftWorkspace restored =
                        reader.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                    Assert.AreEqual(original.WorkspaceId, restored.WorkspaceId);
                }

                Assert.AreEqual(
                    1,
                    Directory.GetDirectories(root)
                        .Count(path => Guid.TryParse(Path.GetFileName(path), out _)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_DefaultRootsSeparateSharedDraftAndLocalRecovery()
        {
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence();

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine("Authoring", "RuntimeAiLab", "Drafts")),
                persistence.DraftsRoot);
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine("UserSettings", "RuntimeAiLab", "DraftRecovery")),
                persistence.RecoveryRoot);
            Assert.IsFalse(
                persistence.DraftsRoot.StartsWith(
                    Path.GetFullPath("UserSettings") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_StandaloneProjectCardSurvivesWithoutGroupReference()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-standalone-card-" + Guid.NewGuid().ToString("N"));
            try
            {
                CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                    CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
                Assert.IsTrue(seed.Success, seed.Diagnostic);
                Assert.Greater(seed.Cards.Cards.Count, 0);

                var workspace = new CharacterTestRuntimeAiLabDraftWorkspace(
                    Guid.NewGuid().ToString("D"),
                    "Shared standalone card test");
                Assert.IsTrue(workspace.EnsurePublishedProjectCardDrafts(seed.Cards));
                CharacterTestRuntimeAiLabBehaviorCardDraft card = workspace.Cards.Values.First();
                card.DisplayName = "跨机器行为卡草稿";
                card.CurrentRevision++;
                workspace.TrackStandaloneProjectCardDraft(card);

                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                writer.AdoptWorkspace(workspace);
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                Assert.IsTrue(
                    reader.TryLoad(workspace.WorkspaceId, out CharacterTestRuntimeAiLabDraftWorkspace restored, out string message),
                    message);
                Assert.IsTrue(restored.TryGetProjectCardDraft(card.PublishedStableId, out CharacterTestRuntimeAiLabBehaviorCardDraft restoredCard));
                Assert.AreEqual("跨机器行为卡草稿", restoredCard.DisplayName);
                Assert.IsFalse(restored.Groups.Values.Any(group =>
                    group.Entries.Any(entry => string.Equals(
                        entry.CardDraftId,
                        restoredCard.CardDraftId,
                        StringComparison.Ordinal))));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_SuccessfulSavePrunesExpiredWorkspaceDirectories()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-retention-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var inactive = new List<string>();
                DateTime baseline = DateTime.UtcNow.AddDays(-10);
                for (int i = 0; i < 7; i++)
                {
                    string directory = Path.Combine(root, Guid.NewGuid().ToString("D"));
                    Directory.CreateDirectory(directory);
                    Directory.SetLastWriteTimeUtc(directory, baseline.AddMinutes(i));
                    inactive.Add(directory);
                }

                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace active =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);

                string[] retained = Directory.GetDirectories(root)
                    .Where(path => Guid.TryParse(Path.GetFileName(path), out _))
                    .ToArray();
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabDraftPersistence.MaximumRetainedWorkspaces,
                    retained.Length);
                Assert.IsTrue(Directory.Exists(Path.Combine(root, active.WorkspaceId)));
                for (int i = 0; i < 3; i++)
                    Assert.IsFalse(Directory.Exists(inactive[i]), "Expired workspace was retained: " + inactive[i]);
                for (int i = 3; i < inactive.Count; i++)
                    Assert.IsTrue(Directory.Exists(inactive[i]), "Recent recovery workspace was removed: " + inactive[i]);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftPersistence_AdoptedTrainingDummyCopySurvivesReloadAndSelectionRestore()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-group-copy-" + Guid.NewGuid().ToString("N"));
            const string copyStableId = "user.behavior-group.training-dummy-copy-test";
            try
            {
                CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();
                CharacterTestArenaBehaviorGroupDraft dummy = config.BehaviorGroups.Drafts.Single(
                    draft => string.Equals(
                        draft.StableId,
                        CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                        StringComparison.Ordinal));
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile copyProfile =
                    dummy.Profile.Clone();
                copyProfile.GroupId = copyStableId;
                copyProfile.DisplayName = "木桩 副本";
                var copy = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                    copyStableId,
                    1,
                    copyProfile.DisplayName,
                    "Training dummy copy",
                    Array.Empty<string>(),
                    4,
                    copyProfile,
                    Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>());
                Assert.IsTrue(config.BehaviorGroups.TryAddProjectGroup(copy, out int copyDraftId));

                CharacterTestArenaSlotConfig selected = config.TryGetSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0);
                selected.BehaviorGroupDraftId = copyDraftId;
                selected.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    copyDraftId);
                selected.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;

                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace original = writer.OpenOrCreate(config);
                CharacterTestRuntimeAiLabDraftWorkspace rebuilt =
                    CharacterTestRuntimeAiLabDraftWorkspace.Import(config, original.WorkspaceId);
                writer.AdoptWorkspace(rebuilt);

                Assert.AreSame(rebuilt, writer.Workspace);
                Assert.AreEqual(original.WorkspaceId, rebuilt.WorkspaceId);
                Assert.IsTrue(writer.Flush(), writer.LastStatus);

                CharacterTestArenaMatchConfig restarted = CharacterTestArenaMatchConfig.CreateDefault();
                Assert.IsTrue(restarted.BehaviorGroups.TryAddProjectGroup(copy, out int restartedDraftId));
                CharacterTestArenaSlotConfig restartedSlot = restarted.TryGetSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0);
                restartedSlot.BehaviorGroupDraftId =
                    CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId;

                var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace restored = reader.OpenOrCreate(restarted);
                restored.RestoreSetupToArena(restarted);

                Assert.AreEqual(restartedDraftId, restartedSlot.BehaviorGroupDraftId);
                Assert.AreEqual(
                    CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(restartedDraftId),
                    restartedSlot.LoadoutId);
                Assert.IsTrue(restarted.BehaviorGroups.TryGetDraft(
                    restartedSlot.BehaviorGroupDraftId,
                    out CharacterTestArenaBehaviorGroupDraft restoredCopy));
                Assert.AreEqual(copyStableId, restoredCopy.StableId);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                    },
                    restoredCopy.Profile.Entries.Select(entry => entry.BehaviorId).ToArray());
                Assert.IsTrue(restarted.BehaviorGroups.TryGetDraftDisplay(
                    restartedDraftId,
                    out _,
                    out string status,
                    out bool compileSuccess), status);
                Assert.IsTrue(compileSuccess, status);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_SyncSlotImportsProjectGroupMissingFromRecoveredDraft()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiLabDraftWorkspace recovered =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(config);
            CharacterTestArenaBehaviorGroupDraft dummy = config.BehaviorGroups.Drafts.Single(
                draft => string.Equals(
                    draft.StableId,
                    CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                    StringComparison.Ordinal));
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile copyProfile =
                dummy.Profile.Clone();
            copyProfile.GroupId = "user.behavior-group.recovered-training-dummy-copy";
            copyProfile.DisplayName = "木桩 副本";
            var copy = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                copyProfile.GroupId,
                1,
                copyProfile.DisplayName,
                "Training dummy copy",
                Array.Empty<string>(),
                4,
                copyProfile,
                Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>());
            Assert.IsTrue(config.BehaviorGroups.TryAddProjectGroup(copy, out int copyDraftId));
            CharacterTestArenaSlotConfig slot = config.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0);
            slot.BehaviorGroupDraftId = copyDraftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(copyDraftId);

            Assert.IsTrue(recovered.SyncSlotFromArena(
                config,
                CharacterTestArenaMatchConfig.Team1Id,
                0));
            Assert.IsTrue(recovered.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft restoredCopy));
            Assert.AreEqual(copy.StableGroupId, restoredCopy.PublishedStableId);
            Assert.AreEqual(copyDraftId, recovered.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0).ArenaBehaviorGroupDraftId);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                restoredCopy.Profile.Entries.Select(entry => entry.BehaviorId).ToArray());
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_SelectingProjectGroupDoesNotReuseMutatedWorkingCopy()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestArenaBehaviorGroupDraft dummy = config.BehaviorGroups.Drafts.Single(
                draft => string.Equals(
                    draft.StableId,
                    CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                    StringComparison.Ordinal));
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile copyProfile =
                dummy.Profile.Clone();
            copyProfile.GroupId = "user.behavior-group.project-selection-test";
            copyProfile.DisplayName = "木桩 副本";
            var copy = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                copyProfile.GroupId,
                1,
                copyProfile.DisplayName,
                "Training dummy copy",
                Array.Empty<string>(),
                4,
                copyProfile,
                Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>());
            Assert.IsTrue(config.BehaviorGroups.TryAddProjectGroup(copy, out int copyDraftId));

            CharacterTestArenaSlotConfig slot = config.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0);
            slot.BehaviorGroupDraftId = copyDraftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(copyDraftId);
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(config);
            Assert.IsTrue(workspace.TryCreateReactiveChaseDodgePreset(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                1,
                out string message), message);
            Assert.IsTrue(workspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft mutated));
            Assert.AreEqual("追击与闪避", mutated.DisplayName);

            slot.BehaviorGroupDraftId = CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId);
            Assert.IsTrue(workspace.SyncSlotFromArena(
                config,
                CharacterTestArenaMatchConfig.Team1Id,
                0));
            slot.BehaviorGroupDraftId = copyDraftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(copyDraftId);
            Assert.IsTrue(workspace.SyncSlotFromArena(
                config,
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                selectExactProjectSource: true));

            Assert.IsTrue(workspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft selected));
            Assert.AreNotSame(mutated, selected);
            Assert.AreEqual("木桩 副本", selected.DisplayName);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                selected.Profile.Entries.Select(entry => entry.BehaviorId).ToArray());
            Assert.AreEqual("追击与闪避", mutated.DisplayName, "Existing local work must be preserved.");
        }

    }
}
