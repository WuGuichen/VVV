using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalExecutionIdentityTests
    {
        [Test]
        public void ExecutionIdentityUsesTimelineAndActionOwnerInsteadOfEmptyLegacyStages()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
                Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, fixture.Inputs, out var compiled, out var issues), string.Join("\n", issues));
                Assert.IsEmpty(compiled.Program.Stages);
                var execution = compiled.Program.Execution;
                int action = execution.Events.ToArray().Select((e, i) => new { e, i })
                    .Single(v => v.e.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction).i;
                var state = new CharacterTestBehaviorExecutionState { ProgramIdentity = execution.Identity,
                    EntityId = fixture.Source.Id.Value, Timeline = 0, ActionOwner = action };
                var agent = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot { ExecutionState = state,
                    PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running, PrimaryActionInstanceId = 7 };
                var result = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
                { CommittedProgram = compiled.Program, SelectedEntityId = fixture.Source.Id.Value };
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveExecutionIdentity(result, agent);
                Assert.AreEqual(CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved, result.Status);
                Assert.AreEqual("single-primary", result.CurrentStageId);
                Assert.AreEqual("attack", result.PrimaryAction.StageId);
                Assert.AreEqual(301, result.PrimaryAction.CharacterActionId.Value);
                Assert.IsTrue(result.PrimaryAction.ActionObserved);

                state.Timeline = 999;
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveExecutionIdentity(result, agent);
                Assert.AreEqual(CharacterTestRuntimeAiLabIdentityResolutionReason.StageIndexOutOfRange, result.Reason);
                state.Timeline = 0;
                state.EntityId++;
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveExecutionIdentity(result, agent);
                Assert.AreEqual(CharacterTestRuntimeAiLabIdentityResolutionReason.ProgramSemanticMismatch, result.Reason);
            }
        }
    }
}

