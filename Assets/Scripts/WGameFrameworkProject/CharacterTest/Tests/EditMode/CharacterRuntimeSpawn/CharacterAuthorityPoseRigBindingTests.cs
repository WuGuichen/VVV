using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterAuthorityPoseRigBindingTests
    {
        private const string DragonGeometryBindingPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/000003/config/geometry_binding.json";

        [Test]
        public void GeometryWithoutRigAndPositionOnlySocketBakeRemainValid()
        {
            CharacterImportedGeometryBinding legacy = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v8\",\"schemaVersion\":\"8.0\"," +
                "\"packageId\":\"legacy\",\"bodyColliders\":[],\"hitZoneBindings\":[]," +
                "\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            Assert.IsFalse(legacy.AuthorityPoseRig.IsAuthored);
            Assert.IsEmpty(legacy.AuthorityPoseRig.Channels);

            var positionOnly = new CharacterSocketBakeCatalog();
            positionOnly.RegisterTrack(
                "clip.idle",
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000079,
                1000,
                new[]
                {
                    new MxAnimationBakedSocketFrame(
                        0,
                        MxFramework.Tests.CharacterCoreFixtureIds.Id_300000079,
                        "root/hand",
                        new MxAnimationBakedVector3(1, 2, 3),
                        new MxAnimationBakedVector3(0, 0, 0))
                });
            byte[] binary = CharacterSocketBakeCatalogBinary.Serialize("legacy", positionOnly);
            string json = CharacterSocketBakeCatalogBinary.SerializeIndex("legacy", positionOnly, binary);
            Assert.IsTrue(CharacterSocketBakeCatalogBinary.TryLoad(json, binary, "legacy",
                out CharacterSocketBakeCatalog loaded, out string[] issues), string.Join(" | ", issues));
            Assert.IsEmpty(issues);
            Assert.IsTrue(loaded.TryGetFrame("clip.idle", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000079, 0, out _));
            Assert.IsTrue(loaded.TryGetTrack("clip.idle", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000079, out CharacterSocketBakeTrack track));
            Assert.IsFalse(track.HasBasis);
            Assert.IsFalse(loaded.TryGetBasis("clip.idle", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000079, 0, out _));
        }

        [Test]
        public void OptionalSocketBasisRoundTripsWithoutChangingPositionFrames()
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack(
                "clip.walk",
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081,
                1000,
                new[]
                {
                    new MxAnimationBakedSocketFrame(
                        0,
                        MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081,
                        "root/leg/foot",
                        new MxAnimationBakedVector3(10, 20, 30),
                        new MxAnimationBakedVector3(0, 0, 0))
                },
                32767,
                new[]
                {
                    new CharacterSocketBakeBasisFrame(
                        0,
                        new MxAnimationBakedVector3(32767, 0, 0),
                        new MxAnimationBakedVector3(0, 32767, 0),
                        new MxAnimationBakedVector3(0, 0, 32767))
                });

            byte[] binary = CharacterSocketBakeCatalogBinary.Serialize("rig", catalog);
            string json = CharacterSocketBakeCatalogBinary.SerializeIndex("rig", catalog, binary);
            Assert.IsTrue(CharacterSocketBakeCatalogBinary.TryLoad(json, binary, "rig",
                out CharacterSocketBakeCatalog loaded, out string[] issues), string.Join(" | ", issues));
            Assert.IsEmpty(issues);
            Assert.IsTrue(loaded.TryGetFrame("clip.walk", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081, 0, out MxAnimationBakedSocketFrame position));
            Assert.AreEqual(10, position.Position.X);
            Assert.IsTrue(loaded.TryGetBasis("clip.walk", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081, 0, out CharacterSocketBakeBasisFrame basis));
            Assert.AreEqual(32767, basis.Right.X);
            Assert.AreEqual(32767, basis.Up.Y);
            Assert.AreEqual(32767, basis.Forward.Z);
        }

        [Test]
        public void LegacyNonOrthogonalSocketBasisDropsOnlyBasisForBaselineComparison()
        {
            const string malformed =
                "{\"format\":\"mx.character.socketBakeCatalog.v2\",\"packageId\":\"rig\",\"tracks\":[{" +
                "\"clipKey\":\"clip.walk\",\"socketId\":300000081,\"quantizationScale\":1000," +
                "\"frames\":[{\"localFrame\":0,\"socketPath\":\"root/foot\"," +
                "\"position\":{\"x\":1,\"y\":2,\"z\":3},\"deltaPosition\":{\"x\":0,\"y\":0,\"z\":0}}]," +
                "\"basisQuantizationScale\":32767,\"basisFrames\":[{\"localFrame\":0," +
                "\"right\":{\"x\":32767,\"y\":0,\"z\":0}," +
                "\"up\":{\"x\":32767,\"y\":0,\"z\":0}," +
                "\"forward\":{\"x\":0,\"y\":0,\"z\":32767}}]}]}";

            CharacterSocketBakeCatalog loaded = CharacterSocketBakeLegacyJson.Load(
                malformed,
                out string[] issues);
            CollectionAssert.Contains(
                issues,
                "Socket bake basis orthogonality is invalid for clip.walk/300000081 at frame 0.");
            Assert.IsTrue(loaded.TryGetFrame("clip.walk", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081, 0, out _));
            Assert.IsTrue(loaded.TryGetTrack(
                "clip.walk",
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081,
                out CharacterSocketBakeTrack track));
            Assert.IsFalse(track.HasBasis);
            Assert.IsFalse(loaded.TryGetBasis("clip.walk", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000081, 0, out _));
        }

        [Test]
        public void Dragon14V8BindingBuildsFourLimbDefinitionAndRejectsHashDrift()
        {
            string json = File.ReadAllText(Path.GetFullPath(DragonGeometryBindingPath));
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(json);
            Assert.AreEqual("mx.characterGeometryBinding.v8", binding.Format);
            Assert.IsTrue(binding.AuthorityPoseRig.IsAuthored);
            Assert.AreEqual(12, binding.AuthorityPoseRig.Channels.Length);
            Assert.AreEqual(4, binding.AuthorityPoseRig.Limbs.Length);
            Assert.AreEqual(12, binding.AuthorityPoseRig.Attachments.Length);

            CharacterAuthorityPoseRigDefinition definition =
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(binding.AuthorityPoseRig);
            Assert.AreEqual(12, definition.Channels.Count);
            Assert.AreEqual(4, definition.GroundProbes.Count);
            Assert.AreEqual(4, definition.TwoBoneIkBindings.Count);
            Assert.AreEqual(12, definition.Attachments.Count);
            Assert.AreEqual(1, definition.SourceRevision);
            StringAssert.StartsWith("sha256:", definition.ContentHash);

            Assert.IsTrue(CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                Path.GetFullPath("Assets/MxFrameworkGenerated/CharacterPackages/000003"), "000003",
                out CharacterSocketBakeCatalog socketCatalog, out _, out string[] socketIssues),
                string.Join(" | ", socketIssues));
            Assert.IsEmpty(socketIssues, "formal Dragon14 basis tracks must pass the runtime integrity gate.");
            Assert.IsTrue(socketCatalog.TryGetBasis(
                "char.000003.animation.idle1",
                MxFramework.Tests.CharacterCoreFixtureIds.Id_115,
                0,
                out _));

            string tampered = json.Replace(
                "\"rootToMidLengthMillimeters\": 893",
                "\"rootToMidLengthMillimeters\": 894");
            Assert.AreNotEqual(json, tampered, "fixture must contain the formal 893mm forelimb measurement.");
            CharacterImportedPackageJsonException exception =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(tampered));
            StringAssert.Contains("content hash", exception.Message);
        }

        [Test]
        public void TwoBoneIkHonorsMinimumReachAndDeterministicModelRootFallback()
        {
            var binding = new CharacterAuthorityTwoBoneIkBinding(
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000086,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000029,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000087,
                MxFramework.Tests.CharacterCoreFixtureIds.Id_300000088,
                Fix64.One,
                Fix64.One,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(2),
                Fix64.FromRatio(1, 2));

            Assert.IsTrue(CharacterAuthorityTwoBoneIkSolver.TrySolve(
                binding,
                FixVector3.Zero,
                new FixVector3(Fix64.FromRatio(1, 10), Fix64.Zero, Fix64.Zero),
                out CharacterAuthorityTwoBoneIkSolveResult result));
            Assert.IsTrue(result.Solved);
            Assert.IsTrue(result.TargetClamped);
            Assert.AreEqual(Fix64.FromRatio(1, 2).RawValue, result.TipPosition.X.RawValue);
            Assert.Greater(result.MidPosition.Z.RawValue, Fix64.Zero.RawValue,
                "authored pole and ModelRoot +X are degenerate, so deterministic ModelRoot +Z must be used.");
        }
    }
}
