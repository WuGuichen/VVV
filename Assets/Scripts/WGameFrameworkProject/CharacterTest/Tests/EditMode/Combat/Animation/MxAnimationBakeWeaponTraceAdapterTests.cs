using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public sealed class MxAnimationBakeWeaponTraceAdapterTests
    {
        [Test]
        public void BuildFrame_AppliesCharacterScaleWeaponProfileAndSocketOffsetDeterministically()
        {
            CombatBakedWeaponTraceReferenceFrame reference = CreateReference();
            CombatBakedWeaponRuntimeProfile profile = CreateProfile();

            WeaponTraceFrame frame = CombatBakedWeaponTraceAdapter.BuildFrame(reference, profile);

            Assert.AreEqual(new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.Zero), frame.RootPrev);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(6)), frame.TipPrev);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.Zero), frame.RootNow);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(10), Fix64.FromInt(2), Fix64.Zero), frame.TipNow);
            Assert.AreEqual(Fix64.One, frame.Radius);
            Assert.AreEqual(CombatPhysicsLayerMask.FromLayer(2), frame.TargetMask);
            Assert.AreEqual("weapon", reference.SocketId);
        }

        [Test]
        public void BuildFrame_UsesOnlyBakedReferenceAndExplicitRuntimeProfile()
        {
            CombatBakedWeaponTraceReferenceFrame reference = CreateReference();
            CombatBakedWeaponRuntimeProfile smallProfile = CreateProfile();
            CombatBakedWeaponRuntimeProfile largeProfile = new CombatBakedWeaponRuntimeProfile(
                characterScale: Fix64.FromInt(3),
                weaponLength: Fix64.FromInt(3),
                weaponRadius: Fix64.Half,
                socketOffset: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                targetMask: CombatPhysicsLayerMask.FromLayer(2));

            WeaponTraceFrame first = CombatBakedWeaponTraceAdapter.BuildFrame(reference, smallProfile);
            WeaponTraceFrame second = CombatBakedWeaponTraceAdapter.BuildFrame(reference, smallProfile);
            WeaponTraceFrame scaled = CombatBakedWeaponTraceAdapter.BuildFrame(reference, largeProfile);

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, scaled);
        }

        [Test]
        public void ReferenceFrameConstructor_PreservesLegacySignature()
        {
            Assert.NotNull(typeof(CombatBakedWeaponTraceReferenceFrame).GetConstructor(new[]
            {
                typeof(int),
                typeof(int),
                typeof(FixVector3),
                typeof(FixVector3),
                typeof(FixVector3),
                typeof(FixVector3)
            }));
        }

        [Test]
        public void BuildCurrentBladeCapsule_OutputsExistingCombatQueryShape()
        {
            CombatCapsuleQuery query = CombatBakedWeaponTraceAdapter.BuildCurrentBladeCapsule(
                CreateReference(),
                CreateProfile(),
                new CombatEntityId(11),
                actionId: 1001,
                queryId: 42,
                sourceOrder: 3);

            Assert.AreEqual(CombatQueryKind.Capsule, query.Header.Kind);
            Assert.AreEqual(42, query.Header.QueryId);
            Assert.AreEqual(11, query.Header.SourceEntityId.Value);
            Assert.AreEqual(1001, query.Header.ActionId);
            Assert.AreEqual(7, query.Header.TraceId);
            Assert.AreEqual(3, query.Header.SourceOrder);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.Zero), query.PointA);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(10), Fix64.FromInt(2), Fix64.Zero), query.PointB);
            Assert.AreEqual(Fix64.One, query.Radius);
        }

        private static CombatBakedWeaponTraceReferenceFrame CreateReference()
        {
            return new CombatBakedWeaponTraceReferenceFrame(
                traceId: 7,
                localFrame: 3,
                socketPrev: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                socketNow: new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                tipDirectionPrev: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                tipDirectionNow: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                socketId: "weapon");
        }

        private static CombatBakedWeaponRuntimeProfile CreateProfile()
        {
            return new CombatBakedWeaponRuntimeProfile(
                characterScale: Fix64.FromInt(2),
                weaponLength: Fix64.FromInt(3),
                weaponRadius: Fix64.Half,
                socketOffset: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                targetMask: CombatPhysicsLayerMask.FromLayer(2));
        }
    }
}
