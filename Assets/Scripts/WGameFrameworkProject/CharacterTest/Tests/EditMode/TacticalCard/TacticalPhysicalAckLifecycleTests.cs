using System;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.TacticalCard
{
    [TestFixture]
    internal sealed class TacticalPhysicalAckLifecycleTests
    {
        private static readonly CharacterTestCharacterEntityId Entity = new CharacterTestCharacterEntityId(1);
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1);

        private static CharacterTestCharacterActionRuntime Runtime()
            => new CharacterTestCharacterActionRuntime(new CharacterTestCharacterActionCatalog(
                new CharacterActionJsonImportResult(null, null, null), new CharacterActionConfigRegistry(null, null),
                new CharacterActionValidationReport(null), null, default, "physical ACK lifecycle fixture", null, null, null));

        private static CharacterTestCharacterActionExecutionStore Store(CharacterTestCharacterActionRuntime runtime)
            => (CharacterTestCharacterActionExecutionStore)typeof(CharacterTestCharacterActionRuntime)
                .GetField("_executions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);

        private static CharacterTestComponentWorld Components()
        {
            var components = new CharacterTestComponentWorld();
            components.RegisterCharacter(Entity, default, default, default, default, default, Actor, default);
            return components;
        }

        private static TacticalDoubleEdgeBattleState State()
        {
            var state = new TacticalDoubleEdgeBattleState(); state.Bind(7, 1, 73); return state;
        }

        // The real gateway allocates the physical ID; the fixture forwards its accepted ActionStarted
        // receipt to the same Stage observer used by the production projection writer.
        private static TacticalThirdBatchAttack Start(CharacterTestCharacterActionRuntime runtime,
            TacticalDoubleEdgeBattleState state, long frame, bool allowCancel = false,
            GameplayEntityId actor = default, int duration = 79, bool eligible = true)
        {
            if (!actor.IsValid) actor = Actor;
            var config = new CharacterActionConfig(new CharacterActionId(455), "ACK fixture", CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored, null, 10, duration, null,
                new[] { new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, duration - 1) },
                allowCancel ? new[] { new CharacterCancelRule(0, duration - 1, CharacterActionRequestType.Movement, CharacterCancelInputMask.Any) } : null, null, null, null, null, null, null, null);
            var plan = new CharacterActionPlan(455, config.ActionId, config.Category, config.Priority, duration,
                config.Phases, CharacterActionTrackPlan.FromConfig(config), default);
            var request = CharacterActionExecutionRequest.ReplaceForOrchestration(CharacterActionResolveResult.Success(plan),
                CharacterActionRunnerActionDefinition.FromConfig(config), CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump);
            var store = Store(runtime);
            var accepted = store.GetOrCreate(Entity).Execute(request, store.EventBuffer);
            Assert.That(accepted.Accepted, Is.True);
            for (int i = 0; i < accepted.Events.Count; i++)
                if (accepted.Events[i].Kind == CharacterActionRunnerEventKind.ActionStarted)
                    return state.ObserveThirdAttack(actor, checked((int)accepted.Events[i].InstanceId), frame, eligible);
            Assert.Fail("Real gateway did not produce ActionStarted."); return default;
        }

        [Test]
        public void NaturalSecondTurnKeepsDistinctPhysicalAckAfterGatewayCleanup()
        {
            var runtime = Runtime(); var components = Components(); var state = State();
            var first = Start(runtime, state, 32);
            for (long frame = 33; frame <= 111; frame++) runtime.TickEntity(components, Entity, new RuntimeFrame(frame));
            state.EndThirdAttack(Actor, first.ActionInstance, 111);
            Assert.That(state.AdvanceTurn(7, 2), Is.True);
            var second = Start(runtime, state, 143);
            Assert.That(second.ActionInstance, Is.GreaterThan(first.ActionInstance));
            Assert.That(second.Revision, Is.EqualTo(first.Revision + 1));
            Assert.That(second.Execution, Is.Not.EqualTo(first.Execution));
            Assert.That(second.Stage, Is.EqualTo(first.Stage));
            Assert.That(second.Actor, Is.EqualTo(first.Actor));
            Assert.That(state.ThirdAttack(Actor, first.ActionInstance).Matches(first), Is.True);
        }

        [Test]
        public void ExactAckReplayIsReadOnlyBeforeAndAfterTerminal()
        {
            var state = State(); var first = state.ObserveThirdAttack(Actor, 1, 32, true);
            var active = state.CaptureProjection();
            Assert.That(state.ObserveThirdAttack(Actor, 1, 32, true).Matches(first), Is.True);
            Assert.That(state.CaptureProjection(), Is.EqualTo(active));
            state.EndThirdAttack(Actor, 1, 111);
            var ended = state.CaptureProjection();
            Assert.That(state.ObserveThirdAttack(Actor, 1, 32, true).Matches(first), Is.True);
            Assert.That(state.CaptureProjection(), Is.EqualTo(ended));
            Assert.That(state.CurrentThirdAttack(Actor).IsValid, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConflictingAckPayloadStillRejectsWithoutMutation(bool ended)
        {
            var state = State(); state.ObserveThirdAttack(Actor, 1, 32, true);
            if (ended) state.EndThirdAttack(Actor, 1, 111);
            var before = state.CaptureProjection();
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.ObserveThirdAttack(Actor, 1, 143, true)).Message,
                Is.EqualTo("third-batch:ConflictingPhysicalAck"));
            Assert.That(Assert.Throws<InvalidOperationException>(() => state.ObserveThirdAttack(Actor, 1, 32, false)).Message,
                Is.EqualTo("third-batch:ConflictingPhysicalAck"));
            Assert.That(state.CaptureProjection(), Is.EqualTo(before));
        }

        [TestCase("external")]
        [TestCase("cancel")]
        [TestCase("orchestration")]
        [TestCase("lifecycle")]
        public void OtherAcceptedTerminalRoutesRetainAllocatorAndClearPayload(string route)
        {
            var runtime = Runtime(); var world = new CharacterTestCharacterRuntimeWorld();
            var components = world.CharacterComponents;
            components.RegisterCharacter(Entity, default, default, default, default, default, Actor, default);
            var projection = (CharacterTestCharacterActionProjectionWriter)typeof(CharacterTestCharacterActionRuntime)
                .GetField("_projection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
            projection.BindRuntimeWorld(world);
            var state = State(); var first = Start(runtime, state, 32, allowCancel: true);
            if (route == "external")
                Assert.That(runtime.CompleteExternal(Entity, new CharacterActionId(455), CharacterActionExternalCompletionReason.Landed, new RuntimeFrame(40)), Is.True);
            else if (route == "cancel")
                Assert.That(runtime.TryCancelOnly(Entity, CharacterActionRequestType.Movement, new RuntimeFrame(40), CharacterActionSourceKind.Command, default), Is.True);
            else if (route == "orchestration")
                Assert.That(runtime.TerminateForOrchestration(Entity, new RuntimeFrame(40), CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner), Is.True);
            else runtime.TerminateAndReleaseEntityAction(components, Entity, new RuntimeFrame(40), CharacterActionLifecycleReason.Death);
            state.EndThirdAttack(Actor, first.ActionInstance, 40);
            Assert.That(runtime.TryCreateRunnerRestoreState(Entity, out var idle), Is.True);
            Assert.That(idle.HasActiveAction, Is.False);
            Assert.That(idle.NextInstanceId, Is.EqualTo(first.ActionInstance + 1));
            Assert.That(Store(runtime).TryGetRunning(Entity, out _), Is.False);
            var second = Start(runtime, state, 143);
            Assert.That(second.ActionInstance, Is.EqualTo(first.ActionInstance + 1));
        }

        [Test]
        public void IdleRunnerRestoreAndTrustedStageCloneKeepIdenticalNextAck()
        {
            var runtime = Runtime(); var components = Components(); var state = State();
            var first = Start(runtime, state, 32);
            for (long frame = 33; frame <= 111; frame++) runtime.TickEntity(components, Entity, new RuntimeFrame(frame));
            state.EndThirdAttack(Actor, first.ActionInstance, 111);
            Assert.That(runtime.TryCreateRunnerRestoreState(Entity, out var idle), Is.True);
            Assert.That(idle.HasActiveAction, Is.False);
            var restored = Runtime(); var clone = state.CloneTrusted();
            Assert.That(restored.TryRestoreRunner(Components(), Entity, idle, out var issue), Is.True, issue);
            var next = Start(runtime, state, 143); var recoveredNext = Start(restored, clone, 143);
            Assert.That(recoveredNext.Matches(next), Is.True);
            Assert.That(clone.ObserveThirdAttack(Actor, first.ActionInstance, 32, true).Matches(first), Is.True);
            Assert.That(clone.CurrentThirdAttack(Actor).Matches(next), Is.True);
            Assert.That(clone.CaptureProjection(), Is.EqualTo(state.CaptureProjection()));
            var left = new RuntimeHashAccumulator(1, 1); var right = new RuntimeHashAccumulator(1, 1);
            state.AddHash(ref left); clone.AddHash(ref right);
            Assert.That(left.ToHash(), Is.EqualTo(right.ToHash()));
        }

        [Test]
        public void ActorGenerationAndStageRemainPartOfPhysicalIdentity()
        {
            var state = State(); var old = state.ObserveThirdAttack(Actor, 1, 32, true);
            var nextGeneration = state.ObserveThirdAttack(new GameplayEntityId(Actor.Index, Actor.Generation + 1), 1, 143, true);
            var otherStage = new TacticalDoubleEdgeBattleState(); otherStage.Bind(8, 1, 73);
            var nextStage = otherStage.ObserveThirdAttack(Actor, 1, 32, true);
            Assert.That(nextGeneration.IsValid, Is.True); Assert.That(nextStage.IsValid, Is.True);
            Assert.That(nextGeneration.Matches(old), Is.False); Assert.That(nextStage.Matches(old), Is.False);
            Assert.That(state.ThirdAttack(Actor, 1).Matches(old), Is.True);
        }

        [Test]
        public void NewCommittedExecutionCannotRetagHistoricalAckOrProjectile()
        {
            var runtime = Runtime(); var components = Components(); var state = State();
            var firstKey = new TacticalDoubleEdgeCommitKey(7, 1, 1, Actor, 1001, 31);
            Assert.That(state.TryPrepare(firstKey, 1, new GameplayEntityId(2, 1), default, default,
                new TacticalThirdBatchParameters(true, false, true), true, true, out var firstPrepared), Is.True);
            Assert.That(firstPrepared.TryPublish(), Is.True);
            var first = Start(runtime, state, 32);
            Assert.That(first.Execution, Is.EqualTo(1));
            Assert.That(state.BindThirdSource(1, 88, 0, first, 33), Is.True);
            var projectile = state.ThirdSource(1, 88, 0);
            for (long frame = 33; frame <= 111; frame++) runtime.TickEntity(components, Entity, new RuntimeFrame(frame));
            state.EndThirdAttack(Actor, first.ActionInstance, 111);
            Assert.That(state.CompleteHost(firstKey, true, 111, 0, 0, 0), Is.True);
            Assert.That(state.AdvanceTurn(7, 2), Is.True);
            var secondKey = new TacticalDoubleEdgeCommitKey(7, 2, 2, Actor, 1002, 142);
            Assert.That(state.TryPrepare(secondKey, 2, new GameplayEntityId(2, 1), default, default,
                new TacticalThirdBatchParameters(true, false, true), true, true, out var secondPrepared), Is.True);
            Assert.That(secondPrepared.TryPublish(), Is.True);
            var second = Start(runtime, state, 143);
            Assert.That(second.Execution, Is.EqualTo(2));
            Assert.That(second.ActionInstance, Is.GreaterThan(first.ActionInstance));
            Assert.That(second.Revision, Is.EqualTo(first.Revision + 1));
            var frozen = state.CaptureProjection();
            Assert.That(state.ObserveThirdAttack(Actor, first.ActionInstance, 32, true).Matches(first), Is.True);
            Assert.That(state.ThirdSource(1, 88, 0).Attack.Matches(projectile.Attack), Is.True);
            Assert.That(state.BindThirdSource(1, 88, 0, second, 144), Is.False);
            Assert.That(state.CurrentThirdAttack(Actor).Matches(second), Is.True);
            Assert.That(state.CaptureProjection(), Is.EqualTo(frozen));
            Assert.That(state.ThirdCoinReceiptCount, Is.Zero);
        }

        // Pinned coordinator receipt: stage1 / actor(2,1) / instance1 / revision1 / execution1,
        // accepted F32, alive at paused F112, ended F113, conflicting new ActionStarted F143.
        // The second eligibility was NOT captured. Both values must work with a genuinely new physical ID.
        [TestCase(false)]
        [TestCase(true)]
        public void ObservedActor2Frame113TerminalAllowsFrame143ForEitherUnknownEligibility(bool nextEligible)
        {
            var runtime = Runtime(); var components = Components(); var state = new TacticalDoubleEdgeBattleState();
            state.Bind(1, 1, 3223276712u);
            var actor = new GameplayEntityId(2, 1);
            var key = new TacticalDoubleEdgeCommitKey(1, 1, 1, actor, 1001, 31);
            Assert.That(state.TryPrepare(key, 1, Actor, default, default,
                new TacticalThirdBatchParameters(true, true, true), true, true, out var prepared), Is.True);
            Assert.That(prepared.TryPublish(), Is.True);
            var first = Start(runtime, state, 32, actor: actor, duration: 81);
            Assert.That(first.Stage, Is.EqualTo(1)); Assert.That(first.Execution, Is.EqualTo(1));
            Assert.That(first.ActionInstance, Is.EqualTo(1)); Assert.That(first.Revision, Is.EqualTo(1));
            for (long frame = 33; frame <= 112; frame++) runtime.TickEntity(components, Entity, new RuntimeFrame(frame));
            Assert.That(Store(runtime).TryGetRunning(Entity, out _), Is.True);
            Assert.That(state.CompleteHost(key, true, 111, 0, 0, 0), Is.True);
            Assert.That(state.AdvanceTurn(1, 2), Is.True);
            var rng = state.ThirdRandomState;
            runtime.TickEntity(components, Entity, new RuntimeFrame(113));
            state.EndThirdAttack(actor, first.ActionInstance, 113);
            var next = Start(runtime, state, 143, actor: actor, eligible: nextEligible);
            Assert.That(next.ActionInstance, Is.EqualTo(2)); Assert.That(next.Revision, Is.EqualTo(2));
            Assert.That(next.Actor, Is.EqualTo(actor)); Assert.That(next.Stage, Is.EqualTo(1));
            Assert.That(next.Execution, Is.Not.EqualTo(first.Execution));
            Assert.That(state.ObserveThirdAttack(actor, 1, 32, true).Matches(first), Is.True);
            Assert.That(state.CurrentThirdAttack(actor).Matches(next), Is.True);
            Assert.That(state.ThirdCoinReceiptCount, Is.EqualTo(1));
            Assert.That(state.ThirdRandomState, Is.EqualTo(rng));
        }
    }
}
