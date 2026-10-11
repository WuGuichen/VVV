using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterRuntimeSpawn;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterBodyPartDamageRuntimeBindingTests
    {
        private const string DefinitionsJson =
            "[{\"definitionIndex\":0,\"partStateId\":\"partstate.head\"," +
            "\"memberPartIds\":[\"head\"],\"damageZoneId\":\"\"," +
            "\"woundCapacity\":300,\"openDamageScaleMilli\":1250," +
            "\"focusConsumeTag\":\"action.focus.head\",\"maxIntegrity\":500," +
            "\"integrityDamageScaleMilli\":1000,\"breakReactionId\":\"\"," +
            "\"authoredPriorityDeltaOpen\":100,\"authoredPriorityDeltaBroken\":200," +
            "\"severable\":false,\"severRequiredTag\":\"\",\"presentationEventId\":\"\"}," +
            "{\"definitionIndex\":1,\"partStateId\":\"partstate.wings\"," +
            "\"memberPartIds\":[\"left_wing\",\"right_wing\"],\"damageZoneId\":\"\"," +
            "\"woundCapacity\":400,\"openDamageScaleMilli\":1100," +
            "\"focusConsumeTag\":\"\",\"maxIntegrity\":600," +
            "\"integrityDamageScaleMilli\":900,\"breakReactionId\":\"reaction.wing.break\"," +
            "\"authoredPriorityDeltaOpen\":50,\"authoredPriorityDeltaBroken\":150," +
            "\"severable\":true,\"severRequiredTag\":\"action.test.wings.sever\"," +
            "\"presentationEventId\":\"event.test.wings.severed\"}]";

        private const string LookupJson =
            "[{\"partId\":\"head\",\"definitionIndex\":0}," +
            "{\"partId\":\"left_wing\",\"definitionIndex\":1}," +
            "{\"partId\":\"right_wing\",\"definitionIndex\":1}]";

        [Test]
        public void GeometryBindingV5_ParsesCanonicalDefinitionsLookupAndSourceIdentity()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                BuildV5GeometryBinding());

            Assert.AreEqual("mx.characterGeometryBinding.v5", binding.Format);
            Assert.AreEqual("5.0", binding.SchemaVersion);
            Assert.AreEqual(CharacterBodyPartDamageMode.Authored, binding.BodyPartDamageMode);
            Assert.AreEqual("mx.characterBodyPartDamage.v1", binding.BodyPartDamageFormat);
            Assert.AreEqual("1.0", binding.BodyPartDamageSchemaVersion);
            Assert.AreEqual(7, binding.BodyPartDamageRevision);
            Assert.AreEqual(BodyPartDamageHash(), binding.BodyPartDamageContentHash);
            Assert.AreEqual(2, binding.BodyPartDamageDefinitions.Length);
            Assert.AreEqual(0, binding.BodyPartDamageDefinitions[0].DefinitionIndex);
            Assert.AreEqual("partstate.head", binding.BodyPartDamageDefinitions[0].PartStateId);
            Assert.AreEqual("action.focus.head", binding.BodyPartDamageDefinitions[0].FocusConsumeTag);
            Assert.IsFalse(binding.BodyPartDamageDefinitions[0].Severable);
            Assert.AreEqual(string.Empty, binding.BodyPartDamageDefinitions[0].SeverRequiredTag);
            Assert.AreEqual(string.Empty, binding.BodyPartDamageDefinitions[0].PresentationEventId);
            CollectionAssert.AreEqual(
                new[] { "left_wing", "right_wing" },
                binding.BodyPartDamageDefinitions[1].MemberPartIds);
            Assert.IsTrue(binding.BodyPartDamageDefinitions[1].Severable);
            Assert.AreEqual(
                "action.test.wings.sever",
                binding.BodyPartDamageDefinitions[1].SeverRequiredTag);
            Assert.AreEqual(
                "event.test.wings.severed",
                binding.BodyPartDamageDefinitions[1].PresentationEventId);
            Assert.AreEqual(3, binding.BodyPartDamagePartLookup.Length);
            Assert.AreEqual("head", binding.BodyPartDamagePartLookup[0].PartId);
            Assert.AreEqual(1, binding.BodyPartDamagePartLookup[2].DefinitionIndex);
        }

        [Test]
        public void GeometryBindingV5_LegacyDefinitionsRemainNonSeverable()
        {
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                BuildLegacyV5GeometryBinding());

            Assert.AreEqual(2, binding.BodyPartDamageDefinitions.Length);
            for (int i = 0; i < binding.BodyPartDamageDefinitions.Length; i++)
            {
                Assert.IsFalse(binding.BodyPartDamageDefinitions[i].Severable);
                Assert.AreEqual(string.Empty, binding.BodyPartDamageDefinitions[i].SeverRequiredTag);
                Assert.AreEqual(string.Empty, binding.BodyPartDamageDefinitions[i].PresentationEventId);
            }
            Assert.AreEqual(LegacyBodyPartDamageHash(), binding.BodyPartDamageContentHash);
        }

        [Test]
        public void GeometryBindingV1ToV4_RemainsBodyPartDamageMissing()
        {
            string anchorHash = Sha256("revision|1\n");
            string presenceHash = Sha256(
                "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\"," +
                "\"revision\":1,\"volumes\":[]}");
            string[] legacyJson =
            {
                BuildLegacyBinding("v1", "1.0", string.Empty),
                BuildLegacyBinding("v2", "2.0", "\"damageZones\":[],"),
                BuildLegacyBinding(
                    "v3", "3.0",
                    "\"damageZones\":[],\"engagementAnchorRevision\":1," +
                    "\"engagementAnchorContentHash\":\"" + anchorHash + "\"," +
                    "\"engagementAnchors\":[],"),
                BuildLegacyBinding(
                    "v4", "4.0",
                    "\"damageZones\":[],\"bodyPresenceMode\":\"Authored\"," +
                    "\"bodyPresenceRevision\":1,\"bodyPresenceContentHash\":\"" + presenceHash + "\"," +
                    "\"bodyPresenceVolumes\":[],")
            };

            for (int i = 0; i < legacyJson.Length; i++)
            {
                CharacterImportedGeometryBinding binding =
                    CharacterImportedPackageJson.LoadGeometryBinding(legacyJson[i]);
                Assert.AreEqual(CharacterBodyPartDamageMode.Missing, binding.BodyPartDamageMode);
                Assert.IsEmpty(binding.BodyPartDamageDefinitions);
                Assert.IsEmpty(binding.BodyPartDamagePartLookup);
                Assert.AreEqual(string.Empty, binding.BodyPartDamageContentHash);
            }
        }

        [Test]
        public void GeometryBindingV5_InvalidDefinitionAndHashFailClosed()
        {
            CharacterImportedPackageJsonException empty =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding()
                            .Replace(DefinitionsJson, "[]")
                            .Replace(LookupJson, "[]")));
            StringAssert.Contains("CHARBPD101_INVALID_BINDING", empty.Message);
            StringAssert.Contains("definition capacity must be in [1,8]", empty.Message);

            CharacterImportedPackageJsonException invalid =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding().Replace(
                            "\"definitionIndex\":1,\"partStateId\":\"partstate.wings\"",
                            "\"definitionIndex\":0,\"partStateId\":\"partstate.wings\"")));
            StringAssert.Contains("CHARBPD101_INVALID_BINDING", invalid.Message);

            CharacterImportedPackageJsonException damageTag =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding().Replace(
                            "action.focus.head",
                            "damage.focus.head")));
            StringAssert.Contains("CHARBPD101_INVALID_BINDING", damageTag.Message);

            CharacterImportedPackageJsonException partialExtension =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding().Replace(
                            ",\"presentationEventId\":\"\"",
                            string.Empty)));
            StringAssert.Contains("CHARBPD101_INVALID_BINDING", partialExtension.Message);

            CharacterImportedPackageJsonException duplicateEvent =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding().Replace(
                            "\"severable\":false,\"severRequiredTag\":\"\",\"presentationEventId\":\"\"",
                            "\"severable\":true,\"severRequiredTag\":\"action.test.head.sever\",\"presentationEventId\":\"event.test.wings.severed\"")));
            StringAssert.Contains("CHARBPD101_INVALID_BINDING", duplicateEvent.Message);

            CharacterImportedPackageJsonException mismatch =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(
                        BuildV5GeometryBinding("sha256:" + new string('0', 64))));
            StringAssert.Contains("CHARBPD102_CONTENT_HASH_MISMATCH", mismatch.Message);
        }

        [Test]
        public void ImportedDragon14_HasFourDefinitionsCompleteLookupAndVerifiedHash()
        {
            string path = Path.GetFullPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/000003/config/geometry_binding.json");
            CharacterImportedGeometryBinding binding =
                CharacterImportedPackageJson.LoadGeometryBinding(File.ReadAllText(path));

            Assert.AreEqual(CharacterBodyPartDamageMode.Authored, binding.BodyPartDamageMode);
            Assert.AreEqual(4, binding.BodyPartDamageDefinitions.Length);
            Assert.AreEqual(10, binding.BodyPartDamagePartLookup.Length);
            Assert.AreEqual("mx.characterBodyPartDamage.v3", binding.BodyPartDamageFormat);
            Assert.AreEqual("3.0", binding.BodyPartDamageSchemaVersion);
            Assert.AreEqual("partstate.000003.head", binding.BodyPartDamageDefinitions[0].PartStateLabel);
            Assert.AreEqual("partstate.000003.tail", binding.BodyPartDamageDefinitions[3].PartStateLabel);
            for (int i = 1; i < binding.BodyPartDamagePartLookup.Length; i++)
                Assert.Less(binding.BodyPartDamagePartLookup[i - 1].PartId,
                    binding.BodyPartDamagePartLookup[i].PartId);
            Assert.IsFalse(binding.BodyPartDamageDefinitions[0].Severable);
            Assert.IsFalse(binding.BodyPartDamageDefinitions[1].Severable);
            Assert.IsFalse(binding.BodyPartDamageDefinitions[2].Severable);
            Assert.IsTrue(binding.BodyPartDamageDefinitions[3].Severable);
            Assert.AreEqual(
                "action.000003.tail.sever",
                binding.BodyPartDamageDefinitions[3].SeverRequiredTag);
            Assert.AreEqual(
                "event.000003.tail.severed",
                binding.BodyPartDamageDefinitions[3].PresentationEventId);
            StringAssert.StartsWith("sha256:", binding.BodyPartDamageContentHash);
            Assert.AreEqual(71, binding.BodyPartDamageContentHash.Length);
        }

        private static string BuildV5GeometryBinding(string contentHash = null)
        {
            return "{\"format\":\"mx.characterGeometryBinding.v5\",\"schemaVersion\":\"5.0\"," +
                "\"packageId\":\"test.v5\",\"bodyColliders\":[],\"hitZoneBindings\":[]," +
                "\"damageZones\":[],\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]," +
                "\"bodyPartDamageMode\":\"Authored\"," +
                "\"bodyPartDamageFormat\":\"mx.characterBodyPartDamage.v1\"," +
                "\"bodyPartDamageSchemaVersion\":\"1.0\",\"bodyPartDamageRevision\":7," +
                "\"bodyPartDamageContentHash\":\"" + (contentHash ?? BodyPartDamageHash()) + "\"," +
                "\"bodyPartDamageDefinitions\":" + DefinitionsJson + "," +
                "\"bodyPartDamagePartLookup\":" + LookupJson + "}";
        }

        private static string BuildLegacyV5GeometryBinding()
        {
            string legacyDefinitions = DefinitionsJson
                .Replace(",\"severable\":false,\"severRequiredTag\":\"\",\"presentationEventId\":\"\"", string.Empty)
                .Replace(",\"severable\":true,\"severRequiredTag\":\"action.test.wings.sever\",\"presentationEventId\":\"event.test.wings.severed\"", string.Empty);
            return BuildV5GeometryBinding(LegacyBodyPartDamageHash())
                .Replace(DefinitionsJson, legacyDefinitions);
        }

        private static string BuildLegacyBinding(string version, string schema, string additions)
        {
            return "{\"format\":\"mx.characterGeometryBinding." + version +
                "\",\"schemaVersion\":\"" + schema + "\",\"packageId\":\"legacy\"," +
                additions + "\"bodyColliders\":[],\"hitZoneBindings\":[]," +
                "\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}";
        }

        private static string BodyPartDamageHash()
        {
            return Sha256(
                "format|mx.characterBodyPartDamage.v1\n" +
                "schemaVersion|1.0\n" +
                "revision|7\n" +
                "partState|partstate.head||300|1250|action.focus.head|500|1000||100|200|False||\n" +
                "member|head\n" +
                "partState|partstate.wings||400|1100||600|900|reaction.wing.break|50|150|True|action.test.wings.sever|event.test.wings.severed\n" +
                "member|left_wing\n" +
                "member|right_wing\n");
        }

        private static string LegacyBodyPartDamageHash()
        {
            return Sha256(
                "format|mx.characterBodyPartDamage.v1\n" +
                "schemaVersion|1.0\n" +
                "revision|7\n" +
                "partState|partstate.head||300|1250|action.focus.head|500|1000||100|200\n" +
                "member|head\n" +
                "partState|partstate.wings||400|1100||600|900|reaction.wing.break|50|150\n" +
                "member|left_wing\n" +
                "member|right_wing\n");
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(hash.Length * 2 + 7).Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }
    }
}
