using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        private static CharacterTestSliceTestHarness CreateHarness(
            CharacterTestRuntimeAiPlannerProfile profile = null,
            bool disableLegacyStartup = false,
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> movableSpecs = null,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> runtimeAiPlannerProfileBindings = null,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null)
        {
            return new CharacterTestSliceTestHarness(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                ActionConfigOptions = actionConfigOptions,
                PhysicsRuntime = physicsRuntime,
                RuntimeAiPlannerProfile = profile,
                RuntimeAiPlannerProfileBindings = runtimeAiPlannerProfileBindings,
                MovableNaturalObjectSpawnSpecs = movableSpecs,
                StartupCharacterSpawnSpecs = disableLegacyStartup
                    ? Array.Empty<CharacterTestCharacterSpawnSpec>()
                    : null
            });
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePlannerCommandPhysicsRuntime()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new MxFramework.Core.Math.FixVector3(
                    MxFramework.Core.Math.Fix64.Half,
                    MxFramework.Core.Math.Fix64.One,
                    MxFramework.Core.Math.Fix64.Half),
                MxFramework.Core.Math.Fix64.FromRatio(1, 100),
                MxFramework.Core.Math.Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "runtime-ai-planner-command-fixture");
        }

        private static bool ContainsEntityRow(
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel,
            int entityId,
            bool selected)
        {
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> entities =
                viewModel?.Entities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel item = entities[i];
                if (item.Visible
                    && item.EntityId == entityId
                    && item.Selected == selected)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSelectedLoadoutCard(
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> loadouts =
                viewModel?.Loadouts;
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel item = loadouts[i];
                if (item.Visible
                    && item.Selected
                    && item.LoadoutId > 0
                    && item.VariantId > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerEvaluator CreateEvaluator(
            ICharacterTestRuntimeAiPlannerPerceptionSource perceptionSource,
            CharacterTestRuntimeAiPlannerProfile profile = null,
            CharacterTestCharacterActionRuntime actionRuntime = null)
        {
            CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                profile ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            Assert.NotNull(compiled, report.ToText());
            return new CharacterTestRuntimeAiPlannerEvaluator(
                compiled,
                CharacterTestDirectAiNavigationService.Instance,
                perceptionSource,
                actionRuntime: actionRuntime);
        }

        private static CharacterTestRuntimeAiPlannerProfile CreatePerceptionMemoryProfile(int memoryFrames)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            return new CharacterTestRuntimeAiPlannerProfile(
                source.ProfileId,
                source.InitialStateId,
                source.TargetSelectionMode,
                source.AttackRange,
                source.States,
                source.Transitions,
                source.Actions,
                new CharacterTestRuntimeAiPlannerPerceptionSettings(
                    CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultVisionRange,
                    memoryFrames,
                    CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultLineOfSightObstacleLayerMask,
                    CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultEnvironmentSampleRadius,
                    CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultThreatDistanceWeight));
        }

        private static CharacterTestRuntimeAiPlannerProfile CreateSingleMovementActionProfile(
            CharacterTestRuntimeAiPlannerActionKind kind,
            CharacterTestRuntimeAiPlannerMovementSettings movementSettings)
        {
            return new CharacterTestRuntimeAiPlannerProfile(
                "test.single-movement-action",
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                CharacterTestAiTargetSelectionMode.NearestOpposingTeam,
                attackRange: 0.5f,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        920900,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        kind,
                        score: 10f,
                        cost: 0f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true)
                        })
                },
                perceptionSettings: CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                movementSettings: movementSettings);
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestSliceTestHarness harness,
            CharacterTestCharacterSpawnSpec spec)
        {
            RuntimeCommandValidationResult result = harness.Enqueue(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    harness.CurrentFrame,
                    CharacterTestRuntimeCommandSources.System,
                    spec,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0044)));
            Assert.IsTrue(result.Success, result.Error.Message);
            harness.Tick();
            IReadOnlyList<CharacterTestCharacterEntity> entities = harness.CharacterEntities;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == spec.ControlOwner && entity.Team.TeamId == spec.TeamId)
                    return entity;
            }

            Assert.Fail("Spawned entity was not found. owner=" + spec.ControlOwner);
            return null;
        }

        private static RuntimeCommand FindRuntimeAiPlannerCommand(CharacterTestSliceTestHarness harness)
        {
            IReadOnlyList<RuntimeCommand> commands = harness.Modules.RuntimeCommands.LastDrainedCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.SourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner)
                    return command;
            }

            Assert.Fail("Runtime AI Planner command was not drained.");
            return default;
        }

        private static bool TryFindRuntimeAiPlannerCommand(
            CharacterTestSliceTestHarness harness,
            out RuntimeCommand runtimeAiPlannerCommand)
        {
            IReadOnlyList<RuntimeCommand> commands = harness.Modules.RuntimeCommands.LastDrainedCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.SourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner)
                {
                    runtimeAiPlannerCommand = command;
                    return true;
                }
            }

            runtimeAiPlannerCommand = default;
            return false;
        }

        private static bool TryFindRuntimeAiPlannerIntent(
            CharacterTestSliceTestHarness harness,
            int targetEntityId,
            out CharacterTestControlIntent intent)
        {
            IReadOnlyList<RuntimeCommand> commands = harness.Modules.RuntimeCommands.LastDrainedCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.SourceId != CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    || command.CommandId != CharacterTestRuntimeCommandIds.SetControlIntent)
                {
                    continue;
                }

                CharacterTestControlIntent candidate =
                    CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                if (candidate.TargetEntityId != targetEntityId)
                    continue;

                intent = candidate;
                return true;
            }

            intent = default;
            return false;
        }

        private static bool TryFindRuntimeAiPlannerAbilitySlotCast(
            CharacterTestSliceTestHarness harness,
            int targetEntityId,
            out CharacterTestAbilitySlotCastRequest request)
        {
            IReadOnlyList<RuntimeCommand> commands = harness.Modules.RuntimeCommands.LastDrainedCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.SourceId != CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                    || command.CommandId != CharacterTestRuntimeCommandIds.CastAbilitySlot)
                {
                    continue;
                }

                CharacterTestAbilitySlotCastRequest candidate =
                    CharacterTestRuntimeCommandFactory.DecodeAbilitySlotCast(command);
                if (candidate.TargetEntityId != targetEntityId)
                    continue;

                request = candidate;
                return true;
            }

            request = default;
            return false;
        }

        private static bool HasRuntimeAiPlannerCommand(IReadOnlyList<RuntimeCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].SourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner)
                    return true;
            }

            return false;
        }

        private static bool HasCondition(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            CharacterTestRuntimeAiPlannerFactKind fact)
        {
            if (action == null)
                return false;

            for (int i = 0; i < action.Conditions.Count; i++)
            {
                if (action.Conditions[i].Fact == fact)
                    return true;
            }

            return false;
        }

        private sealed class FakeRuntimeAiPlannerPerceptionSource
            : ICharacterTestRuntimeAiPlannerPerceptionSource
        {
            public bool LineOfSightBlocked { get; set; }
            public CharacterTestRuntimeAiPlannerEnvironmentSample Environment { get; } =
                new CharacterTestRuntimeAiPlannerEnvironmentSample();

            public void SampleEnvironment(
                CharacterTestCharacterEntity self,
                CharacterTestRuntimeAiPlannerPerceptionSettings settings,
                CharacterTestRuntimeAiPlannerEnvironmentSample sample)
            {
                if (sample == null)
                    return;

                sample.Clear();
                sample.SurfaceKind = Environment.SurfaceKind;
                sample.OnHardSlick = Environment.OnHardSlick;
                sample.NearMovableObject = Environment.NearMovableObject;
                sample.NearObstacle = Environment.NearObstacle;
                sample.NearHazard = Environment.NearHazard;
                sample.NearUsefulObject = Environment.NearUsefulObject;
            }

            public bool TryResolveLineOfSightBlocked(
                CharacterTestCharacterEntity self,
                CharacterTestCharacterEntity target,
                CharacterTestRuntimeAiPlannerPerceptionSettings settings,
                out bool blocked)
            {
                blocked = LineOfSightBlocked;
                return true;
            }
        }

        private static void AssertActionKindExists(
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestRuntimeAiPlannerActionKind kind)
        {
            for (int i = 0; i < profile.Actions.Count; i++)
            {
                if (profile.Actions[i].Kind == kind)
                    return;
            }

            Assert.Fail("Expected action kind was not generated: " + kind);
        }

        private static void AssertActionKindMissing(
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestRuntimeAiPlannerActionKind kind)
        {
            for (int i = 0; i < profile.Actions.Count; i++)
            {
                if (profile.Actions[i].Kind == kind)
                    Assert.Fail("Unexpected action kind was generated: " + kind);
            }
        }

        private static void AssertActionNameContains(
            CharacterTestRuntimeAiPlannerProfile profile,
            string expected)
        {
            for (int i = 0; i < profile.Actions.Count; i++)
            {
                if (profile.Actions[i].DisplayName.Contains(expected))
                    return;
            }

            Assert.Fail("Expected action display name text was not found: " + expected);
        }

        private static void AssertSummaryContains(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            string expected)
        {
            for (int i = 0; i < compile.ModuleSummaries.Count; i++)
            {
                if (compile.ModuleSummaries[i].DiagnosticsText.Contains(expected))
                    return;
            }

            Assert.Fail("Expected behavior summary text was not found: " + expected);
        }

        private static void AssertSummaryEntryExists(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            string entryId)
        {
            for (int i = 0; i < compile.ModuleSummaries.Count; i++)
            {
                if (string.Equals(compile.ModuleSummaries[i].EntryId, entryId, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Expected behavior summary entry was not found: " + entryId);
        }

        private static void EnableModule(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string moduleId,
            bool enabled)
        {
            for (int i = 0; i < profile.Modules.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = profile.Modules[i];
                if (!string.Equals(module.ModuleId, moduleId, StringComparison.Ordinal))
                    continue;

                module.Enabled = enabled;
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                AddBehaviorEntry(profile, moduleId);
            entry.Enabled = enabled;
        }

        private static void SetModuleFloat(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string moduleId,
            string parameterKey,
            float value)
        {
            for (int i = 0; i < profile.Modules.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry module = profile.Modules[i];
                if (!string.Equals(module.ModuleId, moduleId, StringComparison.Ordinal))
                    continue;

                module.SetFloat(parameterKey, value);
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                AddBehaviorEntry(profile, moduleId);
            entry.SetFloat(parameterKey, value);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry FindBehaviorEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string behaviorId)
        {
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                if (string.Equals(profile.Entries[i].BehaviorId, behaviorId, StringComparison.Ordinal))
                    return profile.Entries[i];
            }

            Assert.Fail("Behavior entry not found: " + behaviorId);
            return null;
        }

        private static CharacterTestArenaBehaviorGroupOutcomeRow FindOutcomeRow(
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast,
            CharacterTestArenaBehaviorGroupOutcomeScenario scenario)
        {
            for (int i = 0; forecast != null && i < forecast.Rows.Count; i++)
            {
                if (forecast.Rows[i].Scenario == scenario)
                    return forecast.Rows[i];
            }

            Assert.Fail("Outcome row not found: " + scenario);
            return null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleSummary FindBehaviorModuleSummary(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleSummary> summaries =
                compile.ModuleSummaries;
            for (int i = 0; i < summaries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary = summaries[i];
                if (string.Equals(summary.EntryId, entry.EntryId, StringComparison.Ordinal))
                    return summary;
            }

            Assert.Fail("Behavior module summary not found: " + entry.EntryId);
            return default;
        }

        private static void AssertForecastAction(
            CharacterTestArenaBehaviorGroupOutcomeForecast forecast,
            CharacterTestArenaBehaviorGroupOutcomeScenario scenario,
            CharacterTestRuntimeAiPlannerActionKind expected)
        {
            CharacterTestArenaBehaviorGroupOutcomeRow row = FindOutcomeRow(forecast, scenario);
            Assert.IsTrue(row.HasExpectedAction, row.Notice);
            Assert.AreEqual(expected, row.ExpectedActionKind, row.Reason + " | " + row.Notice);
        }

        private static void AssertCapability(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities,
            CharacterTestRuntimeAiPlannerBehaviorCapability expected)
        {
            for (int i = 0; capabilities != null && i < capabilities.Count; i++)
            {
                if (capabilities[i] == expected)
                    return;
            }

            Assert.Fail("Capability not found: " + expected);
        }

        private static void AssertRisk(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorRiskTag> risks,
            CharacterTestRuntimeAiPlannerBehaviorRiskTag expected)
        {
            for (int i = 0; risks != null && i < risks.Count; i++)
            {
                if (risks[i] == expected)
                    return;
            }

            Assert.Fail("Risk not found: " + expected);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry AddBehaviorEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string behaviorId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                behaviorId,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);
            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                behaviorId,
                true,
                profile.Entries.Count * 10)
            {
                EntryId = behaviorId + "#test",
                BehaviorId = behaviorId
            };
            if (definition != null)
            {
                for (int i = 0; i < definition.Parameters.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                        definition.Parameters[i];
                    switch (parameter.Kind)
                    {
                        case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                            entry.SetBool(parameter.Key, parameter.DefaultBool);
                            break;
                        case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                            entry.SetInt(parameter.Key, parameter.DefaultInt);
                            break;
                        default:
                            entry.SetFloat(parameter.Key, parameter.DefaultFloat);
                            break;
                    }
                }
            }

            profile.Entries.Add(entry);
            return entry;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog CreateLargeBehaviorDefinitionCatalog(
            int extraCount)
        {
            var definitions = new List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>();
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> builtIns =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions;
            for (int i = 0; i < builtIns.Count; i++)
                definitions.Add(builtIns[i]);
            for (int i = 0; i < extraCount; i++)
            {
                definitions.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleDefinition(
                    "Test.Generated." + i.ToString(),
                    "Generated " + i.ToString(),
                    CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Fallback,
                    "Generated catalog scalability behavior.",
                    Array.Empty<CharacterTestRuntimeAiPlannerFactKind>(),
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                            "Utility score",
                            CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float,
                            defaultFloat: 1f,
                            minFloat: 0f,
                            maxFloat: 500f),
                        new CharacterTestRuntimeAiPlannerBehaviorParameterDefinition(
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey,
                            "Cost",
                            CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float,
                            defaultFloat: 0f,
                            minFloat: 0f,
                            maxFloat: 100f)
                    },
                    CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait,
                    "fallback"));
            }

            return new CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog(definitions);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog CreateSingleVariantLoadoutCatalog(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile composition)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog(
                "character-test.behavior.test",
                revision: 1,
                new[]
                {
                    new CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition(
                        1,
                        "character-test.behavior.test.default",
                        "Test",
                        "Test",
                        new[]
                        {
                            new CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
                                1,
                                "default",
                                "Default",
                                composition)
                        })
                });
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot FindAgentSnapshot(
            CharacterTestRuntimeAiPlannerSnapshot snapshot,
            int entityId)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                if (snapshot.Agents[i].EntityId == entityId)
                    return snapshot.Agents[i];
            }

            Assert.Fail("Runtime AI Planner agent snapshot was not found: " + entityId);
            return default;
        }

    }
}
