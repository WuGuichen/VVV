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
    public sealed class CombatScatterPressureBridgeTests
    {
        [Test]
        public void CohesionLimit_UsesMaxOfBaseActionAndMinLines()
        {
            Assert.AreEqual(100, CombatCohesionLimitAdapter.CalculateCohesionLimit(100, null));
            Assert.AreEqual(
                150,
                CombatCohesionLimitAdapter.CalculateCohesionLimit(
                    100,
                    new CombatActionSupportProfile(
                        Fix64.FromRatio(3, 2),
                        Fix64.FromRatio(5, 4))));
            Assert.AreEqual(
                180,
                CombatCohesionLimitAdapter.CalculateCohesionLimit(
                    100,
                    new CombatActionSupportProfile(
                        Fix64.FromRatio(6, 5),
                        Fix64.FromRatio(9, 5))));
        }

        [Test]
        public void CohesionLimit_QuantizesFix64TowardZero()
        {
            int cohesionLimit = CombatCohesionLimitAdapter.CalculateCohesionLimit(
                100,
                new CombatActionSupportProfile(
                    Fix64.FromRatio(4, 3),
                    Fix64.One));

            Assert.AreEqual(133, cohesionLimit);
        }

        [Test]
        public void CohesionSupportWindow_RequiresFlagAndContainingFrame()
        {
            var profile = new CombatActionSupportProfile(
                Fix64.One,
                Fix64.One,
                hasCohesionSupportWindow: true,
                cohesionSupportWindow: new CombatFrameRange(2, 4));
            var disabledProfile = new CombatActionSupportProfile(
                Fix64.One,
                Fix64.One,
                hasCohesionSupportWindow: false,
                cohesionSupportWindow: new CombatFrameRange(2, 4));

            Assert.IsFalse(CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, 1));
            Assert.IsTrue(CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, 2));
            Assert.IsTrue(CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, 4));
            Assert.IsFalse(CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, 5));
            Assert.IsFalse(CombatCohesionLimitAdapter.IsInCohesionSupportWindow(disabledProfile, 3));
        }

    }
}
