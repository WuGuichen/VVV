using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryCombatEffectsTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1);
        private static readonly GameplayEntityId Enemy = new GameplayEntityId(2, 1);
        private static readonly FixVector3 East = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        // Deliberately test-only numbers: no balance values or lifetime defaults are published by this fixture.
        private static readonly TacticalAccessoryPushParameters Push = new TacticalAccessoryPushParameters(4000, 80000, 100, 4000, 12000);
        private static TacticalAccessoryActivation Identity(ulong lastTurn = 2) =>
            new TacticalAccessoryActivation(7, 2, lastTurn, Actor, 40, 81, 91, 1, 100, 10);
        private static TacticalAccessoryCombatState Arm(TacticalAccessoryCombatKind kind, int amount = 7, ulong lastTurn = 2)
        {
            Assert.IsTrue(TacticalAccessoryCombatState.TryArm(kind, Identity(lastTurn),
                kind == TacticalAccessoryCombatKind.PushAway ? 0 : amount,
                kind == TacticalAccessoryCombatKind.PushAway ? Push : default, out var state));
            return state;
        }
        private static TacticalAccessoryCombatReceipt Receipt(HitResolveKind outcome = HitResolveKind.Damage,
            bool incoming = false, TacticalAccessoryDamageOrigin origin = TacticalAccessoryDamageOrigin.DirectAttack,
            DamageProfileSourceKind source = DamageProfileSourceKind.WeaponTrace, ulong stage = 7, ulong turn = 2,
            long frame = 100, ulong sequence = 11, GameplayEntityId? attacker = null, GameplayEntityId? target = null,
            FixVector3? direction = null, int mass = 80000, int pushScale = 1000, bool remainsAlive = true,
            bool attackerWasAlive = true, bool targetWasAlive = true, int actionInstance = 12, int trace = 13)
            => new TacticalAccessoryCombatReceipt(stage, turn, frame, sequence,
                attacker ?? (incoming ? Enemy : Actor), target ?? (incoming ? Actor : Enemy), new CharacterActionId(17),
                actionInstance, trace, source, origin, outcome, attackerWasAlive, targetWasAlive,
                remainsAlive, direction ?? East, mass, pushScale);

        [TestCase(HitResolveKind.Blocked)]
        [TestCase(HitResolveKind.Parried)]
        public void Borrow_SuccessfulDefenseGrantsFixedMomentumOnce(HitResolveKind kind)
        {
            var state = Arm(TacticalAccessoryCombatKind.BorrowMomentum);
            var ledger = new MomentumLedger(20);
            var receipt = Receipt(kind, incoming: true);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed, TacticalAccessoryCombatRules.Resolve(ref state, receipt, ledger, null, out var response));
            Assert.AreEqual(27, ledger.Current);
            Assert.AreEqual(7, response.MomentumGranted);
            Assert.AreEqual(0, response.MomentumSpent);
            Assert.IsFalse(response.Push.HasValue);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref state, receipt, ledger, null, out _));
            Assert.AreEqual(27, ledger.Current);
            Assert.AreEqual(11, state.ConsumedSequence);
        }
        [TestCase(100, 0)]
        [TestCase(99, 1)]
        [TestCase(95, 5)]
        public void Borrow_ResourceSaturationStillConsumes(int balance, int grant)
        {
            var state = Arm(TacticalAccessoryCombatKind.BorrowMomentum);
            var ledger = new MomentumLedger(balance);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(HitResolveKind.Blocked, incoming: true), ledger, null, out var response));
            Assert.AreEqual(grant, response.MomentumGranted);
            Assert.AreEqual(100, ledger.Current);
        }
        [TestCase(HitResolveKind.Damage)]
        [TestCase(HitResolveKind.Evaded)]
        [TestCase(HitResolveKind.Invincible)]
        [TestCase(HitResolveKind.Friendly)]
        [TestCase(HitResolveKind.Duplicate)]
        [TestCase(HitResolveKind.TargetDead)]
        public void Borrow_OtherOutcomesDoNotConsume(HitResolveKind kind)
        {
            var state = Arm(TacticalAccessoryCombatKind.BorrowMomentum);
            var ledger = new MomentumLedger(20);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(kind, incoming: true), ledger, null, out _));
            Assert.AreEqual(20, ledger.Current);
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
        }
        [TestCase((int)TacticalAccessoryDamageOrigin.Unclassified, DamageProfileSourceKind.WeaponTrace)]
        [TestCase((int)TacticalAccessoryDamageOrigin.Periodic, DamageProfileSourceKind.ProjectileImpact)]
        [TestCase((int)TacticalAccessoryDamageOrigin.Derived, DamageProfileSourceKind.WeaponTrace)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.StatusTick)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.Scripted)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.Unknown)]
        public void AllThree_RejectPeriodicDerivedUnknownAndInconsistentOrigins(int origin, DamageProfileSourceKind source)
        {
            foreach (TacticalAccessoryCombatKind kind in Enum.GetValues(typeof(TacticalAccessoryCombatKind)))
            {
                var state = Arm(kind); var ledger = new MomentumLedger(30);
                var receipt = Receipt(kind == TacticalAccessoryCombatKind.PushAway ? HitResolveKind.Damage : HitResolveKind.Blocked,
                    incoming: kind == TacticalAccessoryCombatKind.BorrowMomentum, origin: (TacticalAccessoryDamageOrigin)origin, source: source);
                Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref state, receipt, ledger, ledger, out _));
                Assert.AreEqual(30, ledger.Current);
                Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
            }
        }
        [TestCase(DamageProfileSourceKind.WeaponTrace)]
        [TestCase(DamageProfileSourceKind.ProjectileImpact)]
        [TestCase(DamageProfileSourceKind.AreaPulse)]
        public void Borrow_ExplicitlyProvenDirectSourceKindsQualify(DamageProfileSourceKind source)
        {
            var state = Arm(TacticalAccessoryCombatKind.BorrowMomentum); var ledger = new MomentumLedger(30);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(HitResolveKind.Blocked, incoming: true, source: source), ledger, null, out _));
            Assert.AreEqual(37, ledger.Current);
        }
        [TestCase(10, 8, 2)]
        [TestCase(10, 10, 0)]
        [TestCase(10, 2, 7)]
        public void Break_DrainsUpToFixedAmountWithoutTouchingReservation(int balance, int reserved, int spent)
        {
            var state = Arm(TacticalAccessoryCombatKind.BreakMomentum);
            var defender = new MomentumLedger(balance);
            Assert.IsTrue(defender.TryReserve(reserved, out ulong token));
            var attacker = new MomentumLedger(60);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(HitResolveKind.Blocked), attacker, defender, out var response));
            Assert.AreEqual(spent, response.MomentumSpent);
            Assert.AreEqual(balance - spent, defender.Current);
            Assert.AreEqual(reserved, defender.Reserved);
            Assert.AreEqual(60, attacker.Current);
            Assert.IsFalse(response.Push.HasValue);
            Assert.IsTrue(defender.CommitReservation(token));
            Assert.AreEqual(balance - spent - reserved, defender.Current);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(HitResolveKind.Blocked, sequence: 12), attacker, defender, out _));
        }
        [Test]
        public void Break_ZeroBalanceStillConsumesWithoutAddingPressureDamageOrPush()
        {
            var state = Arm(TacticalAccessoryCombatKind.BreakMomentum); var defender = new MomentumLedger(0);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(HitResolveKind.Parried), null, defender, out var response));
            Assert.AreEqual(0, response.MomentumSpent); Assert.AreEqual(0, response.MomentumGranted);
            Assert.IsFalse(response.Push.HasValue); Assert.AreEqual(0, defender.Current);
        }
        [Test]
        public void LedgerDependentEffects_FailWithoutLedgerAndCanRetrySameCommittedReceipt()
        {
            foreach (var kind in new[] { TacticalAccessoryCombatKind.BorrowMomentum, TacticalAccessoryCombatKind.BreakMomentum })
            {
                var state = Arm(kind); var receipt = Receipt(HitResolveKind.Blocked, incoming: kind == TacticalAccessoryCombatKind.BorrowMomentum);
                Assert.AreEqual(TacticalAccessoryCombatResult.MissingLedger, TacticalAccessoryCombatRules.Resolve(ref state, receipt, null, null, out _));
                Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
                var ledger = new MomentumLedger(10);
                Assert.AreEqual(TacticalAccessoryCombatResult.Consumed, TacticalAccessoryCombatRules.Resolve(ref state, receipt, ledger, ledger, out _));
            }
        }
        [TestCase(80000, 1000, 4000)]
        [TestCase(160000, 1000, 2000)]
        [TestCase(80000, 250, 1000)]
        [TestCase(20000, 1000, 12000)]
        public void Push_AppliesMassPushResistanceAndConfiguredSpeedCap(int mass, int pushScale, int expectedSpeed)
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(mass: mass, pushScale: pushScale), null, null, out var response));
            Assert.AreEqual(CharacterMotionRequestKind.Impulse, response.Push.Kind);
            Assert.AreEqual(expectedSpeed, response.Push.XRaw);
            Assert.AreEqual(0, response.Push.YRaw); Assert.AreEqual(0, response.Push.ZRaw);
            Assert.IsFalse(response.Push.LocksMovement);
            Assert.AreEqual(CharacterMotionObstaclePolicy.Default, response.Push.ObstaclePolicy);
        }
        [Test]
        public void Push_UsesAttackDirectionRatherThanFacingOrActorToTargetDirection()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            var northwest = new FixVector3(-Fix64.One, Fix64.Half, Fix64.One);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(direction: northwest), null, null, out var response));
            Assert.Less(response.Push.XRaw, 0); Assert.Greater(response.Push.ZRaw, 0); Assert.AreEqual(0, response.Push.YRaw);
            Assert.AreEqual(-response.Push.XRaw, response.Push.ZRaw);
            Assert.AreEqual(17, response.Push.Source.SourceActionId.Value); Assert.AreEqual(13, response.Push.Source.StableEventId);
        }
        [Test]
        public void Push_DamageKindAfterArmorAbsorptionStillQualifies()
        {
            // HP damage is intentionally not the trigger. The owner supplies the committed Damage-kind hit.
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(), null, null, out var response));
            Assert.IsTrue(response.Push.HasValue);
        }
        [TestCase(HitResolveKind.Blocked)]
        [TestCase(HitResolveKind.Parried)]
        [TestCase(HitResolveKind.Invincible)]
        [TestCase(HitResolveKind.Evaded)]
        public void Push_OtherOutcomesDoNotConsume(HitResolveKind kind)
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref state, Receipt(kind), null, null, out _));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
        }
        [TestCase(true, 0)]
        [TestCase(false, 1000)]
        public void Push_ImmunityOrLethalHitConsumesWithoutMotion(bool remainsAlive, int pushScale)
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(remainsAlive: remainsAlive, pushScale: pushScale), null, null, out var response));
            Assert.IsFalse(response.Push.HasValue);
        }
        [Test]
        public void Push_MissingDirectionOrInvalidMassDoesNotGuessOrConsume()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.InvalidPushEvidence,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(direction: FixVector3.Zero), null, null, out _));
            Assert.AreEqual(TacticalAccessoryCombatResult.InvalidPushEvidence,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(mass: 0), null, null, out _));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
        }
        [Test]
        public void Push_AddsToExistingKnockbackThroughRealMotionAccumulator()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            TacticalAccessoryCombatRules.Resolve(ref state, Receipt(), null, null, out var response);
            var original = new CharacterMotionRequest(CharacterMotionRequestKind.Impulse, 1200, 500, -300,
                CharacterMotionRequestSpace.World, 0, false, 1000, 1000, false, false, new CharacterActionId(18), 14);
            var accumulator = new CharacterMotionRequestAccumulator(); accumulator.Reset();
            accumulator.Accumulate(original, 0, null); accumulator.Accumulate(response.Push, 0, null);
            var frame = accumulator.ToFrame();
            Assert.AreEqual(5200, frame.ImpulseVelocityX); Assert.AreEqual(500, frame.ImpulseVelocityY); Assert.AreEqual(-300, frame.ImpulseVelocityZ);
            Assert.IsFalse(frame.HasVelocityOverride); Assert.IsFalse(frame.HasHorizontalVelocityOverride);
        }
        [Test]
        public void Push_RealCollisionMotorStopsCombinedKnockbackAtWall()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            TacticalAccessoryCombatRules.Resolve(ref state, Receipt(), null, null, out var response);
            var accumulator = new CharacterMotionRequestAccumulator(); accumulator.Reset(); accumulator.Accumulate(response.Push, 0, null);
            var impulses = accumulator.ToFrame();
            var world = new CombatPhysicsWorld(); var actorBody = new CombatBodyId(1);
            var start = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1), actorBody, start));
            var wallBody = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), wallBody, new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(wallBody, new CombatColliderId(2), 1,
                new FixVector3(-Fix64.Half, -Fix64.One, -Fix64.One), new FixVector3(Fix64.Half, Fix64.One, Fix64.One), CombatSurfaceMaterial.Default));
            var config = new CombatMotionConfig(60, Fix64.One, Fix64.Zero, Fix64.Zero, Fix64.Zero,
                Fix64.FromRatio(1, 1000), Fix64.Half, Fix64.Half, 3,
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), CombatPhysicsLayerMask.FromLayer(1));
            var motor = new CombatKinematicMotor(config);
            var adapter = new CharacterKinematicPhysicsAdapter(); var buffer = new CombatMotionStepBuffer();
            int x = 0, velocityX = impulses.ImpulseVelocityX + 1200;
            // Apply the summed impulse once. Use the real CharacterTest velocity-to-motor adapter,
            // retaining its collision-adjusted velocity instead of feeding ordinary locomotion input.
            for (int i = 0; i < 60; i++)
            {
                var input = new CharacterKinematicPhysicsStepInput(new CombatEntityId(1), actorBody, new CombatFrame(i),
                    positionXMillimeters: x, positionYMillimeters: 1000, positionZMillimeters: 0,
                    previousXMillimeters: x, previousYMillimeters: 1000, previousZMillimeters: 0, centerOffsetY: Fix64.Zero,
                    horizontalVelocityX: velocityX, horizontalVelocityY: 0, verticalVelocity: 0, impactVerticalVelocity: 0,
                    wasGrounded: false, applyGroundSurfaceFriction: true, hasRootMotion: false,
                    persistentHorizontalVelocityX: velocityX, persistentHorizontalVelocityY: 0, persistentVerticalVelocity: 0,
                    rootMotionHorizontalVelocityX: 0, rootMotionHorizontalVelocityY: 0, rootMotionVerticalVelocity: 0,
                    surfaceBounceSpeedCapY: 0, maxStepHeightMillimeters: 0,
                    groundContactProbeVelocityMillimetersPerSecond: 0, moveFacingEpsilonMilli: 0, inputAxisScale: 1000);
                var result = adapter.Step(motor, world, buffer, input);
                x = result.PositionXMillimeters; velocityX = result.HorizontalVelocityX;
            }
            Assert.Greater(x, 0); Assert.LessOrEqual(x, 1000);
            Assert.AreEqual(Fix64.FromRatio(x, 1000), GetBodyPosition(world, actorBody).X);
        }
        private static FixVector3 GetBodyPosition(CombatPhysicsWorld world, CombatBodyId body)
        { Assert.IsTrue(world.TryGetBody(body, out var value)); return value.Position; }

        [Test]
        public void Push_SmallDirectionDoesNotUnderflowAndExtremeMassScalesStayBounded()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway);
            var tinyEast = new FixVector3(Fix64.FromRaw(1), Fix64.Zero, Fix64.Zero);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(direction: tinyEast, mass: 1, pushScale: int.MaxValue), null, null, out var response));
            Assert.AreEqual(12000, response.Push.XRaw);
            var invalidDirection = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.InvalidPushEvidence,
                TacticalAccessoryCombatRules.Resolve(ref invalidDirection, Receipt(direction: new FixVector3(Fix64.MinValue, Fix64.Zero, Fix64.Zero)), null, null, out _));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, invalidDirection.Status);
        }

        [Test]
        public void IdentityGate_RejectsWrongActorGenerationStageFrameTurnAndOldSequence()
        {
            var invalid = new[] {
                Receipt(stage: 8), Receipt(turn: 1), Receipt(frame: 99), Receipt(sequence: 10),
                Receipt(attacker: new GameplayEntityId(1, 2)), Receipt(target: Actor),
                Receipt(attackerWasAlive: false), Receipt(targetWasAlive: false),
                Receipt(actionInstance: 0), Receipt(trace: 0) };
            foreach (var receipt in invalid)
            {
                var state = Arm(TacticalAccessoryCombatKind.PushAway);
                Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref state, receipt, null, null, out _));
                Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
            }
        }
        [Test]
        public void CorrectRoleIsRequiredForBorrowAndBreak()
        {
            var borrow = Arm(TacticalAccessoryCombatKind.BorrowMomentum); var breaking = Arm(TacticalAccessoryCombatKind.BreakMomentum);
            var ledger = new MomentumLedger(10);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref borrow, Receipt(HitResolveKind.Blocked), ledger, ledger, out _));
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref breaking, Receipt(HitResolveKind.Blocked, incoming: true), ledger, ledger, out _));
            Assert.AreEqual(10, ledger.Current);
        }
        [Test]
        public void Lifetime_UsesExplicitTurnsAndAllowsMainActionHitInActivationFrame()
        {
            var state = Arm(TacticalAccessoryCombatKind.PushAway, lastTurn: 4);
            state.AdvanceTurn(7, 3); Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, state.Status);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed,
                TacticalAccessoryCombatRules.Resolve(ref state, Receipt(turn: 4, frame: 10000), null, null, out _));
            var sameFrame = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.Consumed, TacticalAccessoryCombatRules.Resolve(ref sameFrame, Receipt(frame: 100), null, null, out _));
            var expired = Arm(TacticalAccessoryCombatKind.PushAway);
            Assert.AreEqual(TacticalAccessoryCombatResult.NoTrigger, TacticalAccessoryCombatRules.Resolve(ref expired, Receipt(turn: 3), null, null, out _));
            Assert.AreEqual(TacticalAccessoryCombatStatus.Expired, expired.Status);
            expired.Clear(); Assert.AreEqual(TacticalAccessoryCombatStatus.Unarmed, expired.Status);
        }
        [Test]
        public void Arming_RejectsMissingIdentityUnknownKindAndInvalidParameters()
        {
            Assert.IsFalse(TacticalAccessoryCombatState.TryArm(TacticalAccessoryCombatKind.BorrowMomentum, default, 7, default, out _));
            Assert.IsFalse(TacticalAccessoryCombatState.TryArm((TacticalAccessoryCombatKind)99, Identity(), 7, default, out _));
            Assert.IsFalse(TacticalAccessoryCombatState.TryArm(TacticalAccessoryCombatKind.BorrowMomentum, Identity(), 0, default, out _));
            Assert.IsFalse(TacticalAccessoryCombatState.TryArm(TacticalAccessoryCombatKind.PushAway, Identity(), 0, default, out _));
            Assert.IsFalse(TacticalAccessoryCombatState.TryArm(TacticalAccessoryCombatKind.PushAway, Identity(), 7, Push, out _));
        }
        [Test]
        public void MechanismResolution_AllocatesZeroBytesAfterWarmup()
        {
            var identity = Identity(); var receipt = Receipt();
            for (int i = 0; i < 64; i++) ResolvePushWithoutAssertions(identity, receipt);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) ResolvePushWithoutAssertions(identity, receipt);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, bytes);
        }
        private static void ResolvePushWithoutAssertions(TacticalAccessoryActivation identity, TacticalAccessoryCombatReceipt receipt)
        {
            TacticalAccessoryCombatState.TryArm(TacticalAccessoryCombatKind.PushAway, identity, 0, Push, out var state);
            TacticalAccessoryCombatRules.Resolve(ref state, receipt, null, null, out _);
        }
    }
}
