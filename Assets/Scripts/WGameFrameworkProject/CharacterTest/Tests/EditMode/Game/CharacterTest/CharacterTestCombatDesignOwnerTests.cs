using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Focused isolated-owner acceptance; does not load scenes, enter Play, allocate global IDs or touch player saves.</summary>
    public sealed class CharacterTestCombatDesignOwnerTests
    {
        [Test]
        public void FixtureAutomaticallyResolvesTwoConsumersAndOneUnrelatedCard()
        {
            string handle = Create(); var snapshot = Snapshot(handle);
            Assert.AreEqual(2, ((JArray)snapshot["cards"]).Count(c => (bool)c["dependsOnFacing"]));
            Assert.AreEqual(1, ((JArray)snapshot["cards"]).Count(c => !(bool)c["dependsOnFacing"]));
            Assert.AreEqual("strict-less-than", (string)snapshot["contractFacts"]["comparison"]);
            Assert.IsTrue((bool)snapshot["evidence"]["current"]);
        }

        [Test]
        public void OwnerEditInvalidatesEvidenceRejectsOldPlanAndKeepsUnrelatedCardRevision()
        {
            string handle = Create(); var before = Snapshot(handle); var plan = Plan(handle, 8, 45);
            var control = ((JArray)before["cards"]).Single(c => !(bool)c["dependsOnFacing"]);
            CharacterTestCombatDesignOwner.Commit(plan);
            var after = Snapshot(handle);
            Assert.AreEqual(8f, (float)after["contractFacts"]["thresholdDegrees"]);
            Assert.AreEqual(45, (int)after["contractFacts"]["maximumFrames"]);
            Assert.IsFalse((bool)after["evidence"]["current"]);
            Assert.AreEqual((int)control["revision"], (int)((JArray)after["cards"]).Single(c => (string)c["id"] == (string)control["id"])["revision"]);
            Assert.Throws<InvalidOperationException>(() => CharacterTestCombatDesignOwner.Commit(plan));
            CharacterTestCombatDesignOwner.ValidateFixture(handle);
            Assert.IsTrue((bool)Snapshot(handle)["evidence"]["current"]);
        }

        [Test]
        public void IsolatedPublicationReloadsRecompilesAndIdenticalRepeatAddsNoRevision()
        {
            string identityPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/RuntimeHash/character_definition_ids.json"));
            string idsBefore = Hash(File.ReadAllBytes(identityPath));
            string handle = Create(); CharacterTestCombatDesignOwner.PublishFixture(handle);
            var first = Snapshot(handle);
            Assert.AreEqual("isolated-catalog-committed-reloaded-recompiled", (string)first["evidence"]["publication"]);
            var revisions = ((JArray)first["cards"]).Select(c => (int)c["publishedRevision"]).ToArray();
            Assert.AreEqual(2, revisions.Count(r => r == 1));
            CharacterTestCombatDesignOwner.PublishFixture(handle);
            CollectionAssert.AreEqual(revisions, ((JArray)Snapshot(handle)["cards"]).Select(c => (int)c["publishedRevision"]).ToArray());
            Assert.AreEqual(idsBefore, Hash(File.ReadAllBytes(identityPath)), "Fixture publication must perform zero global identity writes.");
        }

        [Test]
        public void TypedFacingPatchPreservesFrameBudgetAndStableAuthoredSourceForIdenticalValues()
        {
            string handle = Create(); CharacterTestCombatDesignOwner.Commit(Plan(handle, 8, 45));
            var first = Snapshot(handle);
            CharacterTestCombatDesignOwner.Commit(Plan(handle, 8, 45));
            var second = Snapshot(handle);
            Assert.AreEqual((string)first["sourceFingerprint"], (string)second["sourceFingerprint"]);
            Assert.AreEqual((int)first["workspaceRevision"], (int)second["workspaceRevision"]);
            Assert.AreEqual(45, (int)second["contractFacts"]["maximumFrames"]);
            Assert.AreEqual("not-run", (string)second["lifecycle"]["play"]);
        }

        private static string Create() => CharacterTestCombatDesignOwner.CreateFixture(Hash(Guid.NewGuid().ToString("N")));
        private static string PathFor(string handle) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MxFramework/CombatDesign/snapshots", handle + ".json"));
        private static JObject Snapshot(string handle) => JObject.Parse(File.ReadAllText(PathFor(handle)));
        private static JObject Plan(string handle, float degrees, int frames) => new JObject {
            ["handle"] = handle, ["snapshotHash"] = Hash(Encoding.UTF8.GetBytes(File.ReadAllText(PathFor(handle)))),
            ["thresholdDegrees"] = degrees, ["maximumFrames"] = frames, ["rationale"] = "Agent choice for isolated acceptance",
            ["expiresUtc"] = DateTime.UtcNow.AddMinutes(10).ToString("O") };
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    }
}
