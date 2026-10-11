using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using NUnit.Framework;

namespace MxFramework.Tests
{
    public sealed class CharacterBodyPresenceV2RuntimeBindingTests
    {
        private const string DragonGeometryBindingPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/000003/config/geometry_binding.json";

        [Test]
        public void ImportedDragon14V7_ResolvesThreeCanonicalAnimatedHurtboxReferences()
        {
            CharacterImportedGeometryBinding binding = LoadDragon();
            Assert.AreEqual("mx.characterGeometryBinding.v7", binding.Format);
            Assert.AreEqual("7.0", binding.SchemaVersion);
            Assert.AreEqual(CharacterBodyPresenceMode.Authored, binding.BodyPresenceMode);
            Assert.AreEqual(3, binding.AnimatedHurtboxSources.Length);
            Assert.AreEqual("presence.animated.head", binding.AnimatedHurtboxSources[0].SourceId);
            Assert.AreEqual("hurt.head.obb.0", binding.AnimatedHurtboxSources[0].ColliderId);
            Assert.AreEqual("hurt.tail_mid.capsule.0", binding.AnimatedHurtboxSources[1].ColliderId);
            Assert.AreEqual("hurt.tail_tip.capsule.0", binding.AnimatedHurtboxSources[2].ColliderId);
            Assert.AreEqual(50, binding.AnimatedHurtboxSources[0].SkinMillimeters);
            Assert.AreEqual(1000, binding.AnimatedHurtboxSources[0].PressureScaleMilli);
            Assert.IsTrue(binding.AnimatedHurtboxSources[0].Enabled);
        }

        [Test]
        public void GeometryBindingV1_RemainsCompatibleWithZeroAnimatedSources()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v1\",\"schemaVersion\":\"1.0\"," +
                "\"packageId\":\"legacy\",\"bodyColliders\":[],\"hitZoneBindings\":[]," +
                "\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            Assert.AreEqual(CharacterBodyPresenceMode.CompatibilityHardRoot, binding.BodyPresenceMode);
            Assert.IsEmpty(binding.AnimatedHurtboxSources);
        }

        [Test]
        public void GeometryBindingV7_DuplicateMissingAndHashDriftFailClosed()
        {
            string json = File.ReadAllText(Path.GetFullPath(DragonGeometryBindingPath));
            string duplicate = json.Replace(
                "\"sourceId\": \"presence.animated.tail_mid\"",
                "\"sourceId\": \"presence.animated.head\"");
            Assert.AreNotEqual(json, duplicate);
            CharacterImportedPackageJsonException duplicateError =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(duplicate));
            StringAssert.Contains("CHARPRES101_INVALID_BINDING", duplicateError.Message);

            string missing = ReplaceLast(json,
                "\"colliderId\": \"hurt.tail_tip.capsule.0\"",
                "\"colliderId\": \"hurt.missing\"");
            Assert.AreNotEqual(json, missing);
            CharacterImportedPackageJsonException missingError =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(missing));
            StringAssert.Contains("CHARPRES101_INVALID_BINDING", missingError.Message);

            string hashDrift = ReplaceLast(json, "\"skinMillimeters\": 50", "\"skinMillimeters\": 51");
            Assert.AreNotEqual(json, hashDrift);
            CharacterImportedPackageJsonException hashError =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(hashDrift));
            StringAssert.Contains("CHARPRES102_CONTENT_HASH_MISMATCH", hashError.Message);
        }

        private static CharacterImportedGeometryBinding LoadDragon()
        {
            return CharacterImportedPackageJson.LoadGeometryBinding(
                File.ReadAllText(Path.GetFullPath(DragonGeometryBindingPath)));
        }

        private static string ReplaceLast(string value, string oldValue, string newValue)
        {
            int index = value.LastIndexOf(oldValue, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "fixture must contain the requested canonical token.");
            return value.Substring(0, index) + newValue + value.Substring(index + oldValue.Length);
        }
    }
}
