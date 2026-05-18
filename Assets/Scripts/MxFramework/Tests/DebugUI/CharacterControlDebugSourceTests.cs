using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class CharacterControlDebugSourceTests
    {
        private static readonly CharacterControlEntityRef Entity =
            CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(7),
                new CombatBodyId(9),
                stableId: 42);

        [Test]
        public void CreateSnapshot_MapsStateCommandMotionActionPressureAndRecentEvents()
        {
            var machine = new CharacterControlStateMachine(Entity);
            var actionController = new CharacterActionController(machine);
            var pressureController = new CharacterPressureReactionController(machine, actionController);
            var source = new CharacterControlDebugSource(
                machine,
                actionController: actionController,
                pressureReactionController: pressureController,
                maxRecentEvents: 4);
            CharacterCommand command = CreateCommand(3);

            source.RecordCommand(command);
            CharacterMotionResult motion = StepMotion(machine, command);
            source.RecordMotionResult(motion);
            actionController.Submit(CharacterActionRequest.CombatAction(
                new RuntimeFrame(4),
                Entity,
                CharacterActionKind.Attack,
                combatActionId: 1001,
                traceId: "attack-missing-runner"));
            pressureController.Apply(new PostureBreakEvent(
                new RuntimeFrame(5),
                Entity.GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentPressure: 100,
                maxPressure: 100,
                delta: 20,
                traceId: "posture-break"));

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("CharacterControl", snapshot.SourceName);
            Assert.That(FindSection(snapshot, "State"), Does.Contain("state: Reaction"));
            Assert.That(FindSection(snapshot, "State"), Does.Contain("lastTransitionReason: PressureBreak"));
            Assert.That(FindSection(snapshot, "Last Command"), Does.Contain("moveSpeedScale: 1.250000"));
            Assert.That(FindSection(snapshot, "Motion"), Does.Contain("grounded: true"));
            Assert.That(FindSection(snapshot, "Motion"), Does.Contain("moveSpeedScale: 1.250000"));
            Assert.That(FindSection(snapshot, "Action"), Does.Contain("MissingCombatActionRunner"));
            Assert.That(FindSection(snapshot, "Pressure"), Does.Contain("Critical -> Broken"));
            Assert.That(FindSection(snapshot, "Recent Events"), Does.Contain("Pressure frame=5"));

            source.Dispose();
            actionController.Dispose();
        }

        [Test]
        public void CreateSnapshot_NullStateMachineReturnsStableUnavailableSnapshot()
        {
            var source = new CharacterControlDebugSource(null);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.IsFalse(source.IsAvailable);
            Assert.AreEqual("CharacterControl", snapshot.SourceName);
            Assert.AreEqual("unavailable", FindSection(snapshot, "Status"));
        }

        [Test]
        public void CreateSnapshot_MissingOptionalControllersKeepsSourceAvailable()
        {
            var source = new CharacterControlDebugSource(new CharacterControlStateMachine(Entity));

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.IsTrue(source.IsAvailable);
            Assert.That(FindSection(snapshot, "Action"), Does.Contain("controller: unavailable"));
            Assert.That(FindSection(snapshot, "Pressure"), Does.Contain("controller: unavailable"));
        }

        [Test]
        public void RegistryAndTextReport_CanConsumeCharacterControlSource()
        {
            var registry = new FrameworkDebugSourceRegistry();
            var source = new CharacterControlDebugSource(new CharacterControlStateMachine(Entity));

            bool registered = registry.Register(source);
            DebugUiDashboardViewModel model = new DebugUiSnapshotAggregator().Refresh(registry);
            string report = FrameworkDebugReportExporter.ExportText(source.CreateSnapshot());

            Assert.IsTrue(registered);
            Assert.AreEqual(1, model.SourceCount);
            Assert.AreEqual(DebugUiSourceStatus.Available, model.Sources[0].Status);
            Assert.That(report, Does.Contain("source: CharacterControl"));
            Assert.That(report, Does.Contain("- State"));
        }

        [Test]
        public void LockChange_DoesNotOverwriteLastTransitionReason()
        {
            var machine = new CharacterControlStateMachine(Entity);
            var source = new CharacterControlDebugSource(machine);

            machine.BeginAction(RuntimeFrame.Zero, CharacterControlLockMask.Jump);
            machine.SetControlLockMask(CharacterControlLockMask.Move, new RuntimeFrame(1), "manual lock");

            string state = FindSection(source.CreateSnapshot(), "State");

            Assert.That(state, Does.Contain("lastTransitionReason: ActionStarted"));
            Assert.That(state, Does.Contain("lastControlEventType: LockChanged"));
            Assert.That(state, Does.Contain("lastControlEventReason: Manual"));
        }

        [Test]
        public void Dispose_DetachesStateMachineSubscriptions()
        {
            var machine = new CharacterControlStateMachine(Entity);
            var source = new CharacterControlDebugSource(machine);

            source.Dispose();
            machine.BeginAction(RuntimeFrame.Zero);

            string state = FindSection(source.CreateSnapshot(), "State");

            Assert.That(state, Does.Contain("state: Action"));
            Assert.That(state, Does.Contain("lastTransitionReason: None"));
            Assert.That(state, Does.Contain("lastControlEventType: none"));
        }

        private static CharacterMotionResult StepMotion(CharacterControlStateMachine machine, CharacterCommand command)
        {
            var world = new CombatPhysicsWorld();
            FixVector3 groundedPosition = new FixVector3(Fix64.Zero, Fix64.FromRatio(9, 10), Fix64.Zero);
            world.UpsertBody(new CombatPhysicsBody(Entity.CombatEntityId, Entity.CombatBodyId, groundedPosition));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(99), new CombatBodyId(99), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(99),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.FromInt(10), -Fix64.One, -Fix64.FromInt(10)),
                localMax: new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.FromInt(10))));
            var resolver = new CharacterMotionResolver(new CombatKinematicMotor(CombatMotionConfig.Default));
            var state = new CombatMotionState(
                new CombatFrame((int)command.Frame.Value),
                groundedPosition,
                FixVector3.Zero,
                grounded: true,
                lastCollisionNormal: FixVector3.Zero,
                collisionFlags: CombatMotionCollisionFlags.Grounded);
            return resolver.Step(command, machine, state, world);
        }

        private static CharacterCommand CreateCommand(int frame)
        {
            return new CharacterCommand(
                new RuntimeFrame(frame),
                sourceId: 10,
                Entity,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                CharacterFacingBasis.Identity,
                jumpPressed: false,
                sprintHeld: false,
                CharacterActionButtons.Primary,
                CharacterActionRequest.CombatAction(
                    new RuntimeFrame(frame),
                    Entity,
                    CharacterActionKind.Attack,
                    combatActionId: 1001,
                    traceId: "attack"),
                Fix64.FromRatio(5, 4),
                traceId: "command-" + frame);
        }

        private static string FindSection(FrameworkDebugSnapshot snapshot, string title)
        {
            for (int i = 0; i < snapshot.Sections.Count; i++)
            {
                if (snapshot.Sections[i].Title == title)
                    return snapshot.Sections[i].Body;
            }

            Assert.Fail("Missing section: " + title);
            return string.Empty;
        }
    }
}
