using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterPressureReactionTests
    {
        private const int QuickAttackId = 1001;

        [Test]
        public void PostureBreak_CancelsCurrentActionBeforeEnteringReaction()
        {
            CombatActionRunner runner = CreateRunner();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var actionController = new CharacterActionController(machine, runner);
            var pressure = new CharacterPressureReactionController(machine, actionController);
            var actionEvents = new List<CharacterActionEvent>();
            var reactionEvents = new List<CharacterPressureReactionEvent>();
            actionController.ActionEvent += actionEvents.Add;
            pressure.ReactionEvent += reactionEvents.Add;

            Assert.IsTrue(actionController.Submit(CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId)).Success);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);

            CharacterActionResult queued = actionController.Submit(CharacterActionRequest.CombatAction(
                new RuntimeFrame(1),
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                queueIfBusy: true));
            Assert.IsTrue(queued.Queued);
            Assert.IsTrue(actionController.HasQueuedRequest);

            CharacterPressureReactionResult result = pressure.Apply(new PostureBreakEvent(
                new RuntimeFrame(2),
                CreateEntity().GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentPressure: 100,
                maxPressure: 100,
                delta: 20,
                traceId: "posture-break"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.ActionCancelRequested);
            Assert.IsTrue(result.ActionCancelSucceeded);
            Assert.IsTrue(result.ReactionStarted);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action, machine.ControlLockMask);
            Assert.IsFalse(actionController.HasQueuedRequest);
            Assert.IsTrue(actionEvents.Exists(evt => evt.Type == CharacterActionEventType.Canceled));
            Assert.AreEqual(1, actionEvents.FindAll(evt => evt.Type == CharacterActionEventType.Started).Count);
            Assert.AreEqual(CharacterPressureReactionEventType.ReactionStarted, reactionEvents[0].Type);
        }

        [Test]
        public void GuardBreak_UsesConfiguredReactionWindowAndFinishesExplicitly()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine, policy: new CharacterPressureReactionPolicy
            {
                GuardBreakReactionFrames = 2,
                GuardBreakLockMask = CharacterControlLockMask.Action
            });

            CharacterPressureReactionResult result = pressure.Apply(new GuardBreakEvent(
                new RuntimeFrame(10),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                previousValue: 50,
                currentPressure: 100,
                maxPressure: 100,
                delta: 50,
                traceId: "guard-break"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.ReactionStarted);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.Action, machine.ControlLockMask);
            Assert.AreEqual(new RuntimeFrame(12), result.ReactionEndFrame);
            Assert.IsFalse(pressure.TryFinishExpiredReaction(new RuntimeFrame(11), out _));
            Assert.IsTrue(pressure.TryFinishExpiredReaction(new RuntimeFrame(12), out CharacterPressureReactionResult finished));
            Assert.IsTrue(finished.ReactionFinished);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void ArmorBreak_DefaultPolicyRecordsWithoutChangingControlState()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine);

            CharacterPressureReactionResult result = pressure.Apply(new ArmorBreakEvent(
                RuntimeFrame.Zero,
                CreateEntity().GameplayEntityId,
                previousIntegrity: 10,
                currentIntegrity: 0,
                maxIntegrity: 10,
                incomingDamage: 12,
                traceId: "armor-break"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.Recorded);
            Assert.IsFalse(result.ReactionStarted);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void MissingGameplayMapping_RejectsWithDiagnostics()
        {
            var machine = new CharacterControlStateMachine(CharacterControlEntityRef.FromCombat(
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1));
            var pressure = new CharacterPressureReactionController(machine);
            var events = new List<CharacterPressureReactionEvent>();
            pressure.ReactionEvent += events.Add;

            CharacterPressureReactionResult result = pressure.Apply(new PostureBreakEvent(
                RuntimeFrame.Zero,
                CreateEntity().GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentPressure: 100,
                maxPressure: 100,
                delta: 20));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterPressureReactionRejectedReason.MissingGameplayEntityMapping, result.RejectedReason);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterPressureReactionEventType.Rejected, events[0].Type);
        }

        [Test]
        public void MismatchedGameplayEntity_RejectsWithoutThrowing()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine);

            CharacterPressureReactionResult result = pressure.Apply(new GuardBreakEvent(
                RuntimeFrame.Zero,
                new GameplayEntityId(99, 1),
                PressureBand.Critical,
                previousValue: 80,
                currentPressure: 100,
                maxPressure: 100,
                delta: 20));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterPressureReactionRejectedReason.EntityMismatch, result.RejectedReason);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
        }

        [Test]
        public void BandChanged_NonEscalatingBrokenBandDoesNotRefreshReactionWindow()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine, policy: new CharacterPressureReactionPolicy
            {
                BrokenBandChangeStartsReaction = true,
                PostureBreakReactionFrames = 3
            });

            CharacterPressureReactionResult started = pressure.Apply(new PressureBandChangedEvent(
                new RuntimeFrame(2),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                PressureBand.Broken,
                previousValue: 75,
                newValue: 100,
                delta: 25,
                reason: GameplayPosturePressureEvents.ApplyPressureReason));
            RuntimeFrame originalEndFrame = pressure.ActiveReactionEndFrame;
            CharacterPressureReactionResult recovering = pressure.Apply(new PressureBandChangedEvent(
                new RuntimeFrame(3),
                CreateEntity().GameplayEntityId,
                PressureBand.Critical,
                PressureBand.Broken,
                previousValue: 100,
                newValue: 90,
                delta: -10,
                reason: GameplayPosturePressureEvents.RecoveryReason));

            Assert.IsTrue(started.ReactionStarted);
            Assert.IsFalse(recovering.ReactionStarted);
            Assert.IsTrue(recovering.Recorded);
            Assert.AreEqual(originalEndFrame, pressure.ActiveReactionEndFrame);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);

            Assert.IsTrue(pressure.TryFinishExpiredReaction(originalEndFrame, out CharacterPressureReactionResult finished));

            Assert.IsTrue(finished.ReactionFinished);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void FinishActiveReaction_ReleasesReactionWindowEarly()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine, policy: new CharacterPressureReactionPolicy
            {
                GuardBreakReactionFrames = 8,
                GuardBreakLockMask = CharacterControlLockMask.Action
            });

            CharacterPressureReactionResult started = pressure.Apply(new GuardBreakEvent(
                new RuntimeFrame(5),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                previousValue: 50,
                currentPressure: 100,
                maxPressure: 100,
                delta: 50,
                traceId: "guard-break"));

            Assert.IsTrue(started.ReactionStarted);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);

            CharacterPressureReactionResult finished = pressure.FinishActiveReaction(new RuntimeFrame(6), "Pressure owner disabled.");

            Assert.IsTrue(finished.ReactionFinished);
            Assert.IsFalse(pressure.HasActiveReaction);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void FinishActiveReaction_DoesNotFinishReenteredReactionOwnedByAnotherSource()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var pressure = new CharacterPressureReactionController(machine, policy: new CharacterPressureReactionPolicy
            {
                GuardBreakReactionFrames = 8,
                GuardBreakLockMask = CharacterControlLockMask.Action
            });

            CharacterPressureReactionResult started = pressure.Apply(new GuardBreakEvent(
                new RuntimeFrame(5),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                previousValue: 50,
                currentPressure: 100,
                maxPressure: 100,
                delta: 50,
                traceId: "guard-break"));
            Assert.IsTrue(started.ReactionStarted);
            Assert.IsTrue(machine.FinishReaction(new RuntimeFrame(6), "External owner ended the pressure reaction.").Success);
            Assert.IsTrue(machine.BeginReaction(
                new RuntimeFrame(7),
                CharacterControlTransitionReason.ReactionStarted,
                CharacterControlLockMask.Action,
                "External reaction.").Success);

            CharacterPressureReactionResult finished = pressure.FinishActiveReaction(new RuntimeFrame(8), "Pressure owner disabled.");

            Assert.IsTrue(finished.Recorded);
            Assert.IsFalse(finished.ReactionFinished);
            Assert.IsFalse(pressure.HasActiveReaction);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.Action, machine.ControlLockMask);
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
            return new CombatActionRunner(registry);
        }
    }
}
