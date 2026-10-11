using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterHurtboxBakeCatalogTests
    {
        [Test]
        public void SerializeLoad_RoundTripsQuantizedRleTracksInStableOrder()
        {
            CharacterHurtboxBakePose poseA = Sphere(100, 200, 300, 50);
            CharacterHurtboxBakePose poseB = Sphere(110, 210, 310, 50);
            var catalog = new CharacterHurtboxBakeCatalog();
            catalog.RegisterTrack("clip.z", 2, CharacterHurtboxBakeTrack.Compress(new[] { poseA, poseA, poseB }));
            catalog.RegisterTrack("clip.a", 1, CharacterHurtboxBakeTrack.Compress(new[] { poseB }));

            string json = CharacterHurtboxBakeCatalogJson.Serialize("000003", catalog);
            CharacterHurtboxBakeCatalog loaded = CharacterHurtboxBakeCatalogJson.Load(json, out string[] issues);

            Assert.That(issues, Is.Empty);
            Assert.That(json.IndexOf("clip.a", System.StringComparison.Ordinal), Is.LessThan(json.IndexOf("clip.z", System.StringComparison.Ordinal)));
            Assert.That(loaded.TryGetTrack("clip.z", 2, out CharacterHurtboxBakeTrack track), Is.True);
            Assert.That(track.Runs.Count, Is.EqualTo(2));
            Assert.That(loaded.TryGetPose("clip.z", 2, 1, out CharacterHurtboxBakePose roundTripA), Is.True);
            Assert.That(roundTripA, Is.EqualTo(poseA));
            Assert.That(loaded.TryGetPose("clip.z", 2, 2, out CharacterHurtboxBakePose roundTripB), Is.True);
            Assert.That(roundTripB, Is.EqualTo(poseB));
        }

        [Test]
        public void Track_RejectsMissingFrameCoverage()
        {
            Assert.Throws<System.ArgumentException>(() => new CharacterHurtboxBakeTrack(
                3,
                new[] { new CharacterHurtboxBakeRun(0, 2, Sphere(0, 0, 0, 10)) }));
        }

        [Test]
        public void BinaryV2RoundTrip_PreservesTracksAndUsesSmallJsonIndex()
        {
            CharacterHurtboxBakePose pose = new CharacterHurtboxBakePose(
                CharacterHurtboxBakeShapeKind.Capsule,
                new CharacterHurtboxBakeVector3(1, 2, 3),
                new CharacterHurtboxBakeVector3(4, 5, 6),
                default,
                default,
                default,
                default,
                250);
            var catalog = new CharacterHurtboxBakeCatalog();
            catalog.RegisterTrack("animation.test", 1, CharacterHurtboxBakeTrack.Compress(new[] { pose, pose, pose }));

            byte[] binary = CharacterHurtboxBakeCatalogBinary.Serialize(catalog);
            string index = CharacterHurtboxBakeCatalogBinary.SerializeIndex(
                "test",
                catalog,
                CharacterHurtboxBakeCatalogBinary.DefaultBinaryRelativePath,
                "sha256:test",
                binary.Length);
            CharacterHurtboxBakeCatalog loaded = CharacterHurtboxBakeCatalogBinary.Load(index, binary, out string[] issues);

            Assert.That(issues, Is.Empty);
            Assert.That(index, Does.Not.Contain("runs"));
            Assert.That(loaded.TryGetPose("animation.test", 1, 2, out CharacterHurtboxBakePose restored), Is.True);
            Assert.That(restored, Is.EqualTo(pose));
        }

        [Test]
        public void RuntimeHurtboxRotation_ConvertsTrigScaleToUnitFix64()
        {
            Assert.That(
                CharacterTestCharacterHurtboxSystem.TrigToFix64(CharacterTestDeterministicMath.TrigScale),
                Is.EqualTo(Fix64.One));
            Assert.That(
                CharacterTestCharacterHurtboxSystem.TrigToFix64(-CharacterTestDeterministicMath.TrigScale),
                Is.EqualTo(-Fix64.One));
        }

        private static CharacterHurtboxBakePose Sphere(long x, long y, long z, long radius)
        {
            return new CharacterHurtboxBakePose(
                CharacterHurtboxBakeShapeKind.Sphere,
                new CharacterHurtboxBakeVector3(x, y, z),
                default,
                default,
                default,
                default,
                default,
                radius);
        }
    }
}
