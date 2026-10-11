using System;
using MxFramework.Audio.Physical;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class PhysicalAudioContactDynamicsTests
    {
        private static readonly PhysicalAudioDirection Up =
            new PhysicalAudioDirection(0f, 1f, 0f);

        [Test]
        public void TryResolve_StaticReceiver_UsesSourceMassAndSiUnits()
        {
            PhysicalAudioContactDynamicsInput input = CreateEstimatedInput(
                preX: 4f,
                preY: -3f,
                sourceMass: 10f,
                receiverMass: 0f,
                receiverIsStatic: true,
                friction: 0.5f,
                elasticity: 0.5f,
                area: 0.1f,
                tickDuration: 0.02f);

            Assert.That(
                PhysicalAudioContactDynamicsResolver.TryResolve(input, out PhysicalAudioContactDynamics result),
                Is.True);
            Assert.That(result.Source, Is.EqualTo(PhysicalAudioContactDynamicsSource.ContactPointEstimated));
            Assert.That(result.RelativeSpeedMetersPerSecond, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(result.NormalSpeedMetersPerSecond, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(result.TangentialSpeedMetersPerSecond, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(result.EffectiveMassKilograms, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.NormalImpulseNewtonSeconds, Is.EqualTo(45f).Within(0.0001f));
            Assert.That(result.TangentialImpulseNewtonSeconds, Is.EqualTo(22.5f).Within(0.0001f));
            Assert.That(result.AveragePressurePascals, Is.EqualTo(22500f).Within(0.1f));
            Assert.That(result.AngleDegrees, Is.EqualTo(53.1301f).Within(0.001f));
        }

        [Test]
        public void TryResolve_DynamicReceiver_UsesReducedMass()
        {
            PhysicalAudioContactDynamicsInput input = CreateEstimatedInput(
                preX: 0f,
                preY: -2f,
                sourceMass: 10f,
                receiverMass: 30f,
                receiverIsStatic: false,
                friction: 1f,
                elasticity: 0f,
                area: 0.2f,
                tickDuration: 0.02f);

            Assert.That(
                PhysicalAudioContactDynamicsResolver.TryResolve(input, out PhysicalAudioContactDynamics result),
                Is.True);
            Assert.That(result.EffectiveMassKilograms, Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(result.NormalImpulseNewtonSeconds, Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void TryResolve_ElasticityAndFrictionCap_AreApplied()
        {
            PhysicalAudioContactDynamicsInput input = CreateEstimatedInput(
                preX: 10f,
                preY: -2f,
                sourceMass: 5f,
                receiverMass: 0f,
                receiverIsStatic: true,
                friction: 0.25f,
                elasticity: 1f,
                area: 0.1f,
                tickDuration: 0.02f);

            Assert.That(
                PhysicalAudioContactDynamicsResolver.TryResolve(input, out PhysicalAudioContactDynamics result),
                Is.True);
            Assert.That(result.NormalImpulseNewtonSeconds, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(result.TangentialImpulseNewtonSeconds, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void TryResolve_SolverVelocityDelta_UsesPrePostNormalDelta()
        {
            var input = new PhysicalAudioContactDynamicsInput(
                PhysicalAudioContactDynamicsSource.SolverVelocityDelta,
                0f,
                -5f,
                0f,
                0f,
                -1f,
                0f,
                Up,
                sourceMassKilograms: 20f,
                receiverMassKilograms: 0f,
                receiverIsStatic: true,
                friction: 0.5f,
                elasticity: 0f,
                contactAreaSquareMeters: 0.1f,
                tickDurationSeconds: 0.02f);

            Assert.That(
                PhysicalAudioContactDynamicsResolver.TryResolve(input, out PhysicalAudioContactDynamics result),
                Is.True);
            Assert.That(result.Source, Is.EqualTo(PhysicalAudioContactDynamicsSource.SolverVelocityDelta));
            Assert.That(result.NormalImpulseNewtonSeconds, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(result.AveragePressurePascals, Is.EqualTo(40000f).Within(0.1f));
        }

        [Test]
        public void TryResolve_InvalidNormalOrTickDuration_ReturnsFalse()
        {
            var invalidNormal = new PhysicalAudioContactDynamicsInput(
                PhysicalAudioContactDynamicsSource.ContactPointEstimated,
                0f,
                -2f,
                0f,
                0f,
                0f,
                0f,
                PhysicalAudioDirection.Zero,
                sourceMassKilograms: 5f,
                receiverMassKilograms: 0f,
                receiverIsStatic: true,
                friction: 0.5f,
                elasticity: 0f,
                contactAreaSquareMeters: 0.1f,
                tickDurationSeconds: 0.02f);
            PhysicalAudioContactDynamicsInput invalidTick = CreateEstimatedInput(
                preX: 0f,
                preY: -2f,
                sourceMass: 5f,
                receiverMass: 0f,
                receiverIsStatic: true,
                friction: 0.5f,
                elasticity: 0f,
                area: 0.1f,
                tickDuration: 0f);

            Assert.That(PhysicalAudioContactDynamicsResolver.TryResolve(invalidNormal, out _), Is.False);
            Assert.That(PhysicalAudioContactDynamicsResolver.TryResolve(invalidTick, out _), Is.False);
        }

        [Test]
        public void TryResolve_AfterWarmup_AllocatesZeroBytesFor4096Calls()
        {
            PhysicalAudioContactDynamicsInput input = CreateEstimatedInput(
                preX: 1f,
                preY: -3f,
                sourceMass: 40f,
                receiverMass: 0f,
                receiverIsStatic: true,
                friction: 0.5f,
                elasticity: 0f,
                area: 0.02f,
                tickDuration: 1f / 60f);
            Assert.That(PhysicalAudioContactDynamicsResolver.TryResolve(input, out _), Is.True);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 4096; i++)
                PhysicalAudioContactDynamicsResolver.TryResolve(input, out _);

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0L));
        }

        private static PhysicalAudioContactDynamicsInput CreateEstimatedInput(
            float preX,
            float preY,
            float sourceMass,
            float receiverMass,
            bool receiverIsStatic,
            float friction,
            float elasticity,
            float area,
            float tickDuration,
            PhysicalAudioDirection normal = default)
        {
            PhysicalAudioDirection resolvedNormal = normal.IsZero ? Up : normal;
            return new PhysicalAudioContactDynamicsInput(
                PhysicalAudioContactDynamicsSource.ContactPointEstimated,
                preX,
                preY,
                0f,
                0f,
                0f,
                0f,
                resolvedNormal,
                sourceMass,
                receiverMass,
                receiverIsStatic,
                friction,
                elasticity,
                area,
                tickDuration);
        }
    }
}
