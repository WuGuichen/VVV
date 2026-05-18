using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterPressureReactionAdapterTests
    {
        private const int QuickAttackId = 1001;

        [Test]
        public void PostureBreak_EntersReactionAndRequestsCancel()
        {
            CombatActionRunner runner = CreateRunner();
            var machine = new CharacterControlStateMachine(CreateEntity());
            var controller = new CharacterActionController(machine, runner);
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine, controller);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();
            CharacterActionRequest attack = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                traceId: "attack");

            Assert.IsTrue(controller.Submit(attack).Success);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);

            bool applied = adapter.ConsumePostureBreak(CreatePostureBreak(new RuntimeFrame(2), CreateEntity().GameplayEntityId));

            Assert.IsTrue(applied);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(
                CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action,
                machine.ControlLockMask);
            Assert.IsTrue(adapter.LastRecord.CancelRequested);
            Assert.IsTrue(adapter.LastRecord.CancelSucceeded);
            Assert.IsFalse(runner.GetActionState(CreateEntity().CombatEntityId).HasValue);
        }

        [Test]
        public void GuardBreak_UsesPolicyLockAndExpiresReactionWindow()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var policy = new CharacterPressureReactionPolicy
            {
                GuardBreak = CharacterPressureReactionPolicyEntry.Reaction(
                    durationFrames: 2,
                    lockMask: CharacterControlLockMask.Action,
                    cancelAction: false,
                    minimumBand: PressureBand.Broken)
            };
            var adapter = new CharacterPressureReactionAdapter(registry, policy);
            adapter.Enable();

            bool applied = adapter.ConsumeGuardBreak(CreateGuardBreak(new RuntimeFrame(5), CreateEntity().GameplayEntityId));

            Assert.IsTrue(applied);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.Action, machine.ControlLockMask);
            Assert.AreEqual(new RuntimeFrame(7), adapter.LastRecord.EndFrame);
            adapter.Tick(new RuntimeFrame(6));
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);

            adapter.Tick(new RuntimeFrame(7));

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void ArmorBreak_DefaultPolicyRecordsFeedbackOnly()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();

            bool applied = adapter.ConsumeArmorBreak(new ArmorBreakEvent(
                new RuntimeFrame(8),
                CreateEntity().GameplayEntityId,
                previousIntegrity: 10,
                currentIntegrity: 0,
                maxIntegrity: 20,
                incomingDamage: 12,
                traceId: "armor"));

            Assert.IsFalse(applied);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterPressureReactionSuppressedReason.PolicyIgnored, adapter.LastSuppressedReason);
            Assert.AreEqual(CharacterPressureReactionKind.ArmorBreak, adapter.LastRecord.Kind);
        }

        [Test]
        public void MissingEntityMapping_RecordsDiagnosticsWithoutThrowing()
        {
            var adapter = new CharacterPressureReactionAdapter(new CharacterPressureReactionTargetRegistry());
            adapter.Enable();

            bool applied = adapter.ConsumePostureBreak(CreatePostureBreak(new RuntimeFrame(3), CreateEntity().GameplayEntityId));

            Assert.IsFalse(applied);
            Assert.AreEqual(CharacterPressureReactionSuppressedReason.MissingEntityMapping, adapter.LastSuppressedReason);
            Assert.AreEqual(CreateEntity().GameplayEntityId, adapter.LastRecord.GameplayEntityId);
        }

        [Test]
        public void BandChanged_UsesConfigurableMinimumBand()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();

            bool pressed = adapter.ConsumePostureBandChanged(CreateBandChanged(
                new RuntimeFrame(1),
                CreateEntity().GameplayEntityId,
                PressureBand.Stable,
                PressureBand.Pressed));
            bool critical = adapter.ConsumePostureBandChanged(CreateBandChanged(
                new RuntimeFrame(2),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                PressureBand.Critical));

            Assert.IsFalse(pressed);
            Assert.IsTrue(critical);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(CharacterPressureReactionKind.PostureBandChanged, adapter.LastRecord.Kind);
        }

        [Test]
        public void BandChanged_RecoveryDoesNotRefreshReactionWindow()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();

            bool critical = adapter.ConsumePostureBandChanged(CreateBandChanged(
                new RuntimeFrame(2),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                PressureBand.Critical,
                previousValue: 70,
                newValue: 90,
                delta: 20));
            RuntimeFrame originalEndFrame = adapter.LastRecord.EndFrame;
            bool recovering = adapter.ConsumePostureBandChanged(CreateBandChanged(
                new RuntimeFrame(3),
                CreateEntity().GameplayEntityId,
                PressureBand.Broken,
                PressureBand.Critical,
                previousValue: 100,
                newValue: 80,
                delta: -20,
                reason: GameplayPosturePressureEvents.RecoveryReason));

            Assert.IsTrue(critical);
            Assert.IsFalse(recovering);
            Assert.AreEqual(CharacterPressureReactionSuppressedReason.NonEscalatingBandChange, adapter.LastSuppressedReason);
            Assert.AreEqual(1, adapter.ActiveReactionCount);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);

            adapter.Tick(originalEndFrame);

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void Disable_ReleasesActiveReactionWindow()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();

            Assert.IsTrue(adapter.ConsumeGuardBreak(CreateGuardBreak(new RuntimeFrame(5), CreateEntity().GameplayEntityId)));
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);

            adapter.Disable();

            Assert.IsFalse(adapter.IsEnabled);
            Assert.AreEqual(0, adapter.ActiveReactionCount);
            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
        }

        [Test]
        public void Dispose_ReleasesActiveReactionWindow()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var registry = new CharacterPressureReactionTargetRegistry();
            registry.Register(machine);
            var adapter = new CharacterPressureReactionAdapter(registry);
            adapter.Enable();

            Assert.IsTrue(adapter.ConsumePostureBandChanged(CreateBandChanged(
                new RuntimeFrame(4),
                CreateEntity().GameplayEntityId,
                PressureBand.Cracked,
                PressureBand.Critical)));

            adapter.Dispose();

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterControlLockMask.None, machine.ControlLockMask);
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
                totalFrames: 12,
                startup: new CombatFrameRange(0, 1),
                active: new CombatFrameRange(2, 4),
                recovery: new CombatFrameRange(5, 11),
                windows: null,
                events: null));
            return new CombatActionRunner(registry);
        }

        private static PostureBreakEvent CreatePostureBreak(RuntimeFrame frame, GameplayEntityId entityId)
        {
            return new PostureBreakEvent(
                frame,
                entityId,
                PressureBand.Critical,
                previousValue: 90,
                currentPressure: 100,
                maxPressure: 100,
                delta: 10,
                traceId: "posture-break");
        }

        private static GuardBreakEvent CreateGuardBreak(RuntimeFrame frame, GameplayEntityId entityId)
        {
            return new GuardBreakEvent(
                frame,
                entityId,
                PressureBand.Critical,
                previousValue: 90,
                currentPressure: 100,
                maxPressure: 100,
                delta: 10,
                traceId: "guard-break");
        }

        private static PressureBandChangedEvent CreateBandChanged(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            PressureBand newBand,
            int previousValue = 40,
            int newValue = 75,
            int delta = 35,
            string reason = "",
            string traceId = "band")
        {
            return new PressureBandChangedEvent(
                frame,
                entityId,
                previousBand,
                newBand,
                previousValue,
                newValue,
                delta,
                reason: reason,
                traceId: traceId);
        }
    }
}
