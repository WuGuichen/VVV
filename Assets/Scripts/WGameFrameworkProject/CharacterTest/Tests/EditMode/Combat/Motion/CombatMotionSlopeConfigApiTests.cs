using System;
using System.Linq;
using System.Reflection;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Motion
{
    public sealed class CombatMotionSlopeConfigApiTests
    {
        private static readonly FixVector3 HalfExtents =
            new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);

        [Test]
        public void CombatMotionConfig_LegacyConstructorSignaturesAndOptionalDefaultsRemainAvailable()
        {
            ConstructorInfo direct = RequireConstructor(
                typeof(int),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(int),
                typeof(FixVector3),
                typeof(CombatPhysicsLayerMask),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64));
            ConstructorInfo stepConfig = RequireConstructor(
                typeof(CombatStepConfig),
                typeof(FixVector3),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(int),
                typeof(int),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64));
            ConstructorInfo directWithSnap = RequireConstructor(
                typeof(int),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(int),
                typeof(FixVector3),
                typeof(CombatPhysicsLayerMask),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64));
            ConstructorInfo stepConfigWithSnap = RequireConstructor(
                typeof(CombatStepConfig),
                typeof(FixVector3),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(int),
                typeof(int),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64),
                typeof(Fix64));

            AssertLegacyTrailingOptionalDefaults(direct);
            AssertLegacyTrailingOptionalDefaults(stepConfig);
            AssertGroundSnapConstructorShape(directWithSnap);
            AssertGroundSnapConstructorShape(stepConfigWithSnap);
        }

        [Test]
        public void CombatMotionConfig_GroundSnapDistanceDefaultsValidatesAndParticipatesInValueIdentity()
        {
            CombatMotionConfig legacy = CreateLegacyConfig();
            CombatMotionConfig snapped = CreateConfig(Fix64.FromRatio(1, 10));

            Assert.AreEqual(Fix64.Zero, CombatMotionConfig.Default.GroundSnapDistance);
            Assert.AreEqual(Fix64.Zero, CombatMotionConfig.FromStepConfig(CombatStepConfig.Default).GroundSnapDistance);
            Assert.AreEqual(Fix64.Zero, legacy.GroundSnapDistance);
            Assert.AreEqual(Fix64.FromRatio(1, 10), snapped.GroundSnapDistance);
            Assert.AreEqual(legacy.CapsuleProxy, snapped.CapsuleProxy);
            Assert.AreNotEqual(legacy, snapped);
            Assert.AreNotEqual(legacy.GetHashCode(), snapped.GetHashCode());
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateConfig(Fix64.FromRaw(-1)));
        }

        [Test]
        public void CombatMotionConfig_NewStepConfigConstructorCarriesGroundSnapDistance()
        {
            var config = new CombatMotionConfig(
                CombatStepConfig.Default,
                HalfExtents,
                Fix64.FromInt(5),
                Fix64.FromInt(-30),
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRatio(1, 1000),
                Fix64.Half,
                Fix64.Half,
                CombatPhysicsLayerMask.All.Value,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                Fix64.Zero,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                Fix64.FromInt(30),
                Fix64.FromRatio(1, 10));

            Assert.AreEqual(Fix64.FromRatio(1, 10), config.GroundSnapDistance);
        }

        [Test]
        public void CollisionFlags_AppendSlopeBitsWithoutChangingLegacyValues()
        {
            Assert.AreEqual(1, (int)CombatMotionCollisionFlags.BlockedX);
            Assert.AreEqual(2, (int)CombatMotionCollisionFlags.BlockedY);
            Assert.AreEqual(4, (int)CombatMotionCollisionFlags.BlockedZ);
            Assert.AreEqual(8, (int)CombatMotionCollisionFlags.Grounded);
            Assert.AreEqual(16, (int)CombatMotionCollisionFlags.Ceiling);
            Assert.AreEqual(32, (int)CombatMotionCollisionFlags.Wall);
            Assert.AreEqual(64, (int)CombatMotionCollisionFlags.IterationLimit);
            Assert.AreEqual(128, (int)CombatMotionCollisionFlags.StepUp);
            Assert.AreEqual(256, (int)CombatMotionCollisionFlags.SteepSlope);
            Assert.AreEqual(512, (int)CombatMotionCollisionFlags.GroundSnap);
        }

        [Test]
        public void StepOptions_DefaultFalseAndExplicitTrueAreObservable()
        {
            Assert.IsFalse(default(CombatMotionStepOptions).SuppressGroundSnap);
            Assert.IsFalse(new CombatMotionStepOptions(suppressGroundSnap: false).SuppressGroundSnap);
            Assert.IsTrue(new CombatMotionStepOptions(suppressGroundSnap: true).SuppressGroundSnap);
        }

        [Test]
        public void CombatKinematicMotor_OldStepSignaturesRemainAndOnlyOneOptionsOverloadIsAdded()
        {
            Type motor = typeof(CombatKinematicMotor);
            AssertStepExists(motor, typeof(CombatMotionState), typeof(CombatMotionInput));
            AssertStepExists(motor, typeof(CombatPhysicsWorld), typeof(CombatBodyId), typeof(CombatMotionState), typeof(CombatMotionInput));
            AssertStepExists(motor, typeof(CombatPhysicsWorld), typeof(CombatBodyId), typeof(CombatMotionState), typeof(CombatMotionInput), typeof(FixVector3?));
            AssertStepExists(motor, typeof(CombatPhysicsWorld), typeof(CombatBodyId), typeof(CombatMotionState), typeof(CombatMotionInput), typeof(FixVector3?), typeof(CombatMotionStepBuffer));
            AssertStepExists(motor, typeof(CombatPhysicsWorld), typeof(CombatBodyId), typeof(CombatMotionState), typeof(CombatMotionInput), typeof(FixVector3?), typeof(CombatMotionStepBuffer), typeof(CombatKinematicMotorCandidateCache));
            AssertStepExists(motor, typeof(CombatMotionState), typeof(CombatMotionInput), typeof(CombatPhysicsWorld), typeof(CombatEntityId), typeof(CombatBodyId), typeof(bool), typeof(FixVector3?), typeof(CombatMotionStepBuffer));
            AssertStepExists(motor, typeof(CombatMotionState), typeof(CombatMotionInput), typeof(CombatPhysicsWorld), typeof(CombatEntityId), typeof(CombatBodyId), typeof(bool), typeof(FixVector3?), typeof(CombatMotionStepBuffer), typeof(CombatKinematicMotorCandidateCache));

            MethodInfo[] optionsOverloads = motor.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == nameof(CombatKinematicMotor.Step))
                .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CombatMotionStepOptions)))
                .ToArray();
            Assert.AreEqual(1, optionsOverloads.Length);
            ParameterInfo[] parameters = optionsOverloads[0].GetParameters();
            Assert.AreEqual(8, parameters.Length);
            Assert.AreEqual(typeof(CombatMotionStepOptions), parameters[7].ParameterType);
            Assert.IsFalse(parameters[7].IsOptional);
        }

        [Test]
        public void CombatKinematicMotor_OptionsOverloadDoesNotChangeT1MotionBehavior()
        {
            var motor = new CombatKinematicMotor(CreateConfig(Fix64.FromRatio(1, 10)));
            var state = new CombatMotionState(
                CombatFrame.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                grounded: false,
                FixVector3.Zero,
                CombatMotionCollisionFlags.None);

            CombatMotionStepResult legacy = motor.Step(
                null,
                CombatBodyId.None,
                state,
                CombatMotionInput.None,
                null,
                null,
                null);
            CombatMotionStepResult options = motor.Step(
                null,
                CombatBodyId.None,
                state,
                CombatMotionInput.None,
                null,
                null,
                null,
                new CombatMotionStepOptions(suppressGroundSnap: true));

            Assert.AreEqual(legacy, options);
        }

        private static ConstructorInfo RequireConstructor(params Type[] parameterTypes)
        {
            ConstructorInfo constructor = typeof(CombatMotionConfig).GetConstructor(parameterTypes);
            Assert.IsNotNull(constructor);
            return constructor;
        }

        private static void AssertLegacyTrailingOptionalDefaults(ConstructorInfo constructor)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            for (int i = parameters.Length - 3; i < parameters.Length; i++)
            {
                Assert.IsTrue(parameters[i].IsOptional, parameters[i].Name);
                Assert.IsNull(parameters[i].DefaultValue, parameters[i].Name);
            }
        }

        private static void AssertGroundSnapConstructorShape(ConstructorInfo constructor)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            Assert.AreEqual(15, parameters.Length);
            for (int i = parameters.Length - 4; i < parameters.Length; i++)
            {
                Assert.IsFalse(parameters[i].IsOptional, parameters[i].Name);
            }

            Assert.AreEqual("groundSnapDistance", parameters[14].Name);
        }

        private static void AssertStepExists(Type motor, params Type[] parameterTypes)
        {
            Assert.IsNotNull(motor.GetMethod(nameof(CombatKinematicMotor.Step), parameterTypes));
        }

        private static CombatMotionConfig CreateLegacyConfig()
        {
            return new CombatMotionConfig(
                CombatMotionConfig.DefaultTicksPerSecond,
                Fix64.FromInt(5),
                Fix64.FromInt(-30),
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRatio(1, 1000),
                Fix64.Half,
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                HalfExtents,
                CombatPhysicsLayerMask.All);
        }

        private static CombatMotionConfig CreateConfig(Fix64 groundSnapDistance)
        {
            return new CombatMotionConfig(
                CombatMotionConfig.DefaultTicksPerSecond,
                Fix64.FromInt(5),
                Fix64.FromInt(-30),
                Fix64.FromInt(10),
                Fix64.FromInt(-50),
                Fix64.FromRatio(1, 1000),
                Fix64.Half,
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                HalfExtents,
                CombatPhysicsLayerMask.All,
                Fix64.Zero,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                Fix64.FromInt(30),
                groundSnapDistance);
        }
    }
}
