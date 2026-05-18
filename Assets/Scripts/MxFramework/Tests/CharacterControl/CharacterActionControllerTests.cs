using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterActionControllerTests
    {
        private const int QuickAttackId = 1001;
        private const int HeavyAttackId = 2002;
        private const int AbilityStrike = 300001;

        [Test]
        public void CombatActionLifecycle_SynchronizesControlStateAndEvents()
        {
            CombatActionRunner runner = CreateRunner();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, runner);
            var events = new List<CharacterActionEvent>();
            controller.ActionEvent += events.Add;
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: "attack");

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);
            Assert.AreEqual(CharacterActionEventType.Accepted, events[0].Type);
            Assert.AreEqual(CharacterActionEventType.Started, events[1].Type);

            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterActionEventType.Finished, events[events.Count - 1].Type);
        }

        [Test]
        public void SameFrameDuplicateAction_IsRejectedBeforeQueueOrRunner()
        {
            CombatActionRunner runner = CreateRunner();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, runner);
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: "attack");

            Assert.IsTrue(controller.Submit(request).Success);
            CharacterActionResult duplicate = controller.Submit(request);

            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual(CharacterActionRejectedReason.DuplicateSameFrame, duplicate.RejectedReason);
        }

        [Test]
        public void QueuedCombatAction_StartsAfterCurrentActionFinishes()
        {
            CombatActionRunner runner = CreateRunner();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, runner);
            var events = new List<CharacterActionEvent>();
            controller.ActionEvent += events.Add;
            CharacterActionRequest first = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: "attack-1");
            CharacterActionRequest second = CharacterActionRequest.CombatAction(
                new RuntimeFrame(1),
                CreateEntity(),
                CharacterActionKind.Attack,
                HeavyAttackId,
                sourceId: 4,
                traceId: "attack-2",
                queueIfBusy: true);

            Assert.IsTrue(controller.Submit(first).Success);
            CharacterActionResult queued = controller.Submit(second);

            Assert.IsTrue(queued.Success);
            Assert.IsTrue(queued.Queued);
            Assert.IsTrue(controller.HasQueuedRequest);

            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));

            Assert.IsFalse(controller.HasQueuedRequest);
            Assert.AreEqual(HeavyAttackId, runner.GetActionState(CreateEntity().CombatEntityId).Value.ActionId);
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Queued));
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Finished && evt.Request.CombatActionId == QuickAttackId));
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Started && evt.Request.CombatActionId == HeavyAttackId));
        }

        [Test]
        public void GameplayAbility_EnqueuesStableRuntimeCommand()
        {
            var buffer = new RuntimeCommandBuffer();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, gameplayCommandBuffer: buffer);
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                new RuntimeFrame(7),
                CreateEntity(),
                AbilityStrike,
                sourceId: 9,
                traceId: "skill");

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.HasRuntimeCommand);
            IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(7));
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(GameplayRuntimeCommandIds.CastComponentAbility, commands[0].CommandId);
            Assert.AreEqual(9, commands[0].SourceId);
            Assert.AreEqual(CreateEntity().GameplayEntityId.Index, commands[0].TargetId);
            Assert.AreEqual(CreateEntity().GameplayEntityId.Generation, commands[0].Payload0);
            Assert.AreEqual(AbilityStrike, commands[0].Payload1);
            Assert.AreEqual("skill", commands[0].TraceId);
        }

        [Test]
        public void ExplicitTargetGameplayAbility_UsesRequestStoreAndRequestCommand()
        {
            var buffer = new RuntimeCommandBuffer();
            var store = new GameplayComponentAbilityRequestStore();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, gameplayCommandBuffer: buffer, abilityRequestStore: store);
            GameplayEntityId target = new GameplayEntityId(2, 1);
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                CreateEntity(),
                AbilityStrike,
                target,
                sourceId: 9,
                traceId: "targeted");

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, store.Count);
            IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(RuntimeFrame.Zero);
            Assert.AreEqual(GameplayRuntimeCommandIds.CastComponentAbilityRequest, commands[0].CommandId);
            Assert.AreEqual(AbilityStrike, commands[0].Payload2);
        }

        [Test]
        public void ConstraintCanRejectCooldownResourceOrStatusRules()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(
                machine,
                constraints: new ICharacterActionConstraint[]
                {
                    new RejectingConstraint()
                });
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                CreateEntity(),
                AbilityStrike,
                sourceId: 9,
                traceId: "cooldown");

            CharacterActionResult result = controller.Submit(request);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionRejectedReason.ConstraintRejected, result.RejectedReason);
            Assert.AreEqual("cooldown", result.Message);
        }

        [Test]
        public void ActionLockRejectsNonCancelAction()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            machine.SetControlLockMask(CharacterControlLockMask.Action, RuntimeFrame.Zero);
            var controller = new CharacterActionController(machine);
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                CreateEntity(),
                AbilityStrike);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionRejectedReason.ActionLocked, result.RejectedReason);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1);
        }

        private static CombatActionRunner CreateRunner()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(QuickAttackId, new CombatActionTimeline(
                QuickAttackId,
                totalFrames: 3,
                startup: new CombatFrameRange(0, 0),
                active: new CombatFrameRange(1, 1),
                recovery: new CombatFrameRange(2, 2),
                windows: null,
                events: null));
            registry.RegisterTimeline(HeavyAttackId, new CombatActionTimeline(
                HeavyAttackId,
                totalFrames: 4,
                startup: new CombatFrameRange(0, 1),
                active: new CombatFrameRange(2, 2),
                recovery: new CombatFrameRange(3, 3),
                windows: null,
                events: null));
            return new CombatActionRunner(registry);
        }

        private sealed class RejectingConstraint : ICharacterActionConstraint
        {
            public CharacterActionConstraintResult Evaluate(CharacterActionContext context)
            {
                return CharacterActionConstraintResult.Rejected(CharacterActionRejectedReason.ConstraintRejected, "cooldown");
            }
        }
    }
}
