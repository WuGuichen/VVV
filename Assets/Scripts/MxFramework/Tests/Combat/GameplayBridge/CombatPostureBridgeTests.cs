using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatPostureBridgeTests
    {
        [Test]
        public void BreakLine_UsesMaxOfBaseActionAndMinLines()
        {
            Assert.AreEqual(100, CombatBreakLineAdapter.CalculateBreakLine(100, null));
            Assert.AreEqual(
                150,
                CombatBreakLineAdapter.CalculateBreakLine(
                    100,
                    new CombatActionSupportProfile(
                        Fix64.FromRatio(3, 2),
                        Fix64.FromRatio(5, 4))));
            Assert.AreEqual(
                180,
                CombatBreakLineAdapter.CalculateBreakLine(
                    100,
                    new CombatActionSupportProfile(
                        Fix64.FromRatio(6, 5),
                        Fix64.FromRatio(9, 5))));
        }

        [Test]
        public void BreakLine_QuantizesFix64TowardZero()
        {
            int breakLine = CombatBreakLineAdapter.CalculateBreakLine(
                100,
                new CombatActionSupportProfile(
                    Fix64.FromRatio(4, 3),
                    Fix64.One));

            Assert.AreEqual(133, breakLine);
        }

        [Test]
        public void HyperArmorWindow_RequiresFlagAndContainingFrame()
        {
            var profile = new CombatActionSupportProfile(
                Fix64.One,
                Fix64.One,
                hasHyperArmorWindow: true,
                hyperArmorWindow: new CombatFrameRange(2, 4));
            var disabledProfile = new CombatActionSupportProfile(
                Fix64.One,
                Fix64.One,
                hasHyperArmorWindow: false,
                hyperArmorWindow: new CombatFrameRange(2, 4));

            Assert.IsFalse(CombatBreakLineAdapter.IsInHyperArmorWindow(profile, 1));
            Assert.IsTrue(CombatBreakLineAdapter.IsInHyperArmorWindow(profile, 2));
            Assert.IsTrue(CombatBreakLineAdapter.IsInHyperArmorWindow(profile, 4));
            Assert.IsFalse(CombatBreakLineAdapter.IsInHyperArmorWindow(profile, 5));
            Assert.IsFalse(CombatBreakLineAdapter.IsInHyperArmorWindow(disabledProfile, 3));
        }

        [Test]
        public void PostureBreak_ForceCancelsMappedRunningAction()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var map = new CombatEntityGameplayMap();
            var postureSystem = new GameplayPosturePressureSystem();
            var canceled = new List<ActionCanceledEvent>();
            var combatId = new CombatEntityId(10);
            var gameplayId = new GameplayEntityId(2, 1);
            map.Register(combatId, gameplayId);
            runner.StartAction(combatId, 1001, CombatFrame.Zero);
            runner.ActionCanceled += canceled.Add;

            using (var adapter = new CombatPostureBreakAdapter(postureSystem, runner, map))
            {
                adapter.Enable();
                postureSystem.PostureBreakEvents.Publish(CreateBreakEvent(gameplayId));
            }

            Assert.AreEqual(1, canceled.Count);
            Assert.IsNull(runner.GetActionState(combatId));
        }

        [Test]
        public void PostureBreak_MissingMappingDoesNotCancelRunningAction()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var postureSystem = new GameplayPosturePressureSystem();
            var combatId = new CombatEntityId(10);
            runner.StartAction(combatId, 1001, CombatFrame.Zero);

            using (var adapter = new CombatPostureBreakAdapter(postureSystem, runner, new CombatEntityGameplayMap()))
            {
                adapter.Enable();
                postureSystem.PostureBreakEvents.Publish(CreateBreakEvent(new GameplayEntityId(3, 1)));
            }

            Assert.IsNotNull(runner.GetActionState(combatId));
        }

        [Test]
        public void PostureBreak_NoRunningActionDoesNotThrow()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var map = new CombatEntityGameplayMap();
            var postureSystem = new GameplayPosturePressureSystem();
            var combatId = new CombatEntityId(10);
            var gameplayId = new GameplayEntityId(2, 1);
            map.Register(combatId, gameplayId);

            using (var adapter = new CombatPostureBreakAdapter(postureSystem, runner, map))
            {
                adapter.Enable();
                Assert.DoesNotThrow(() => postureSystem.PostureBreakEvents.Publish(CreateBreakEvent(gameplayId)));
            }
        }

        [Test]
        public void PostureBreak_DisabledAdapterDoesNotCancel()
        {
            CombatActionRunner runner = CreateRunner(out _);
            var map = new CombatEntityGameplayMap();
            var postureSystem = new GameplayPosturePressureSystem();
            var combatId = new CombatEntityId(10);
            var gameplayId = new GameplayEntityId(2, 1);
            map.Register(combatId, gameplayId);
            runner.StartAction(combatId, 1001, CombatFrame.Zero);
            var adapter = new CombatPostureBreakAdapter(postureSystem, runner, map);

            adapter.Enable();
            adapter.Disable();
            postureSystem.PostureBreakEvents.Publish(CreateBreakEvent(gameplayId));

            Assert.IsNotNull(runner.GetActionState(combatId));
            adapter.Dispose();
        }

        private static CombatActionRunner CreateRunner(
            out CombatActionRegistry registry,
            CombatActionSupportProfile? supportProfile = null)
        {
            registry = new CombatActionRegistry();
            registry.RegisterTimeline(
                1001,
                new CombatActionTimeline(
                    1001,
                    5,
                    new CombatFrameRange(0, 1),
                    new CombatFrameRange(2, 3),
                    new CombatFrameRange(4, 4),
                    null,
                    null,
                    supportProfile));
            return new CombatActionRunner(registry);
        }

        private static PostureBreakEvent CreateBreakEvent(GameplayEntityId entityId)
        {
            return new PostureBreakEvent(
                RuntimeFrame.Zero,
                entityId,
                PressureBand.Critical,
                previousValue: 90,
                currentPressure: 100,
                maxPressure: 100,
                delta: 10);
        }
    }
}
