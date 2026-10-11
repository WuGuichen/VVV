using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Uses independent fixture drafts and the existing isolated durable publisher; never publishes a production card.</summary>
    public sealed class CharacterTestCombatDesignAuthoringOwnerTests
    {
        private string _scope;
        private JObject _detail;
        private string _formalBefore, _identityBefore;
        private string Formal => Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/runtime_ai_planner");
        private string Identity => Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/RuntimeHash/character_definition_ids.json"));

        [SetUp]
        public void SetUp()
        {
            _formalBefore = Tree(Formal); _identityBefore = File.ReadAllText(Identity);
            _scope = "fixture:" + CharacterTestCombatDesignOwner.CreateFixture(Id());
            var library = Execute("refresh", new JObject());
            string key = (string)((JArray)library["cards"]).First(c => ((string)c["description"]).Length > 0)["key"];
            _detail = Execute("open", new JObject { ["key"] = key, ["snapshotToken"] = library["snapshotToken"] });
        }
        [TearDown]
        public void TearDown()
        {
            CharacterTestCombatDesignAuthoringOwner.FailFixtureCheckpointForTests = false;
            Assert.AreEqual(_formalBefore, Tree(Formal), "Formal catalog must remain byte-for-byte unchanged.");
            Assert.AreEqual(_identityBefore, File.ReadAllText(Identity), "Isolated tests must allocate no global identities.");
        }

        [Test]
        public void SavesThroughWorkspaceTransactionAndRejectsStaleSecondEditor()
        {
            var edit = Edit(); edit["name"] = "Web isolated edit"; edit["cost"] = 2;
            var saved = Execute("save", edit);
            Assert.AreEqual("Web isolated edit", (string)saved["name"]);
            Assert.Greater((int)saved["revision"], (int)_detail["revision"]);
            Assert.AreNotEqual((string)_detail["snapshotToken"], (string)saved["snapshotToken"]);
            StringAssert.Contains("SOURCE_CONFLICT", Assert.Throws<InvalidOperationException>(() => Execute("save", edit)).Message);
        }

        [Test]
        public void InvalidSourceAndMetadataDoNotChangePersistedDraft()
        {
            var edit = Edit(); edit["source"]["unexpectedField"] = true;
            Assert.Throws<InvalidOperationException>(() => Execute("save", edit));
            edit = Edit(); edit["cost"] = -1;
            Assert.Throws<InvalidOperationException>(() => Execute("save", edit));
            Assert.AreEqual((string)_detail["snapshotToken"], (string)Execute("refresh", new JObject())["snapshotToken"]);
        }

        [Test]
        public void PreflightDoesNotPublishAndSourceChangeInvalidatesConfirmation()
        {
            var plan = Preflight(); Assert.IsTrue((bool)plan["canPublish"]);
            Assert.AreEqual(0, (int)_detail["publishedRevision"]);
            var edit = Edit(); edit["name"] = "changed after preview"; Execute("save", edit);
            StringAssert.Contains("SOURCE_CONFLICT", Assert.Throws<InvalidOperationException>(() => Publish(plan)).Message);
        }

        [Test]
        public void PublicationRequiresConfirmationAndReloadsHashesWithGroupsUnchanged()
        {
            var plan = Preflight();
            Assert.Throws<InvalidOperationException>(() => Execute("publish", new JObject { ["planToken"] = plan["planToken"] }));
            string groupPath = Path.Combine(FixtureRoot(), "StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_group_catalog.json");
            string groups = File.ReadAllText(groupPath);
            var published = Publish(plan);
            Assert.IsTrue((bool)published["published"], published.ToString()); Assert.AreEqual(1, (int)published["revision"]);
            Assert.IsNotEmpty((string)published["contentHash"]); Assert.IsNotEmpty((string)published["semanticHash"]);
            Assert.AreEqual(groups, File.ReadAllText(groupPath));
            _detail = (JObject)published["detail"];
            Assert.IsFalse((bool)Preflight()["canPublish"], "Identical source must not add another revision.");
        }

        [Test]
        public void CommittedCatalogCheckpointFailureRecoversWithoutSecondPublication()
        {
            var plan = Preflight(); CharacterTestCombatDesignAuthoringOwner.FailFixtureCheckpointForTests = true;
            var failed = Publish(plan); Assert.AreEqual("RecoveryRequired", (string)failed["status"]);
            Assert.AreEqual("CatalogCommitted", (string)failed["publicationStage"]);
            StringAssert.Contains("SOURCE_CONFLICT", Assert.Throws<InvalidOperationException>(() => Publish(plan)).Message);
            CharacterTestCombatDesignAuthoringOwner.FailFixtureCheckpointForTests = false;
            var restored = Execute("reconcile", new JObject { ["cardId"] = _detail["cardId"], ["recoveryOperationId"] = failed["recoveryOperationId"] });
            Assert.IsTrue((bool)restored["published"]); Assert.AreEqual(1, (int)restored["revision"]);
        }

        [Test]
        public void SharedDependencyChangesInvalidatePreflight()
        {
            var plan = Preflight();
            string handle = _scope.Substring(8);
            string snapshot = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MxFramework/CombatDesign/snapshots", handle + ".json")));
            CharacterTestCombatDesignOwner.Commit(new JObject { ["handle"] = handle,
                ["snapshotHash"] = CharacterTestCombatDesignAuthoringOwner.Hash(snapshot), ["thresholdDegrees"] = 8,
                ["maximumFrames"] = 45, ["rationale"] = "Isolated shared-dependency change after Web preflight",
                ["expiresUtc"] = DateTime.UtcNow.AddMinutes(10).ToString("O") });
            StringAssert.Contains("SOURCE_CONFLICT", Assert.Throws<InvalidOperationException>(() => Publish(plan)).Message);
        }

        [Test]
        public void UnknownScopeAndFixturePathCannotBecomeProduction()
        {
            Assert.Throws<InvalidOperationException>(() => CharacterTestCombatDesignAuthoringOwner.Execute("refresh", new JObject { ["scope"] = "../../Assets" }, Id()));
            Assert.Throws<InvalidOperationException>(() => CharacterTestCombatDesignAuthoringOwner.Execute("refresh", new JObject { ["scope"] = "fixture:main" }, Id()));
        }

        private JObject Edit() => new JObject { ["cardId"] = _detail["cardId"], ["snapshotToken"] = _detail["snapshotToken"],
            ["name"] = _detail["name"], ["cost"] = _detail["cost"], ["cooldown"] = _detail["cooldown"], ["source"] = _detail["source"].DeepClone() };
        private JObject Preflight() => Execute("preflight", new JObject { ["cardId"] = _detail["cardId"], ["snapshotToken"] = _detail["snapshotToken"],
            ["configuration"] = new JObject { ["characterId"] = 1, ["actions"] = new JObject() } });
        private JObject Publish(JObject plan) => Execute("publish", new JObject { ["planToken"] = plan["planToken"], ["confirmed"] = true });
        private JObject Execute(string operation, JObject request) { request["scope"] = _scope; return CharacterTestCombatDesignAuthoringOwner.Execute(operation, request, Id()); }
        private static string Id() => CharacterTestCombatDesignAuthoringOwner.Hash(Guid.NewGuid().ToString("N"));
        private string FixtureRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MxFramework/CombatDesign/fixtures", _scope.Substring(8, 12)));
        private static string Tree(string directory) => CharacterTestCombatDesignAuthoringOwner.Hash(string.Join("\n", Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => p + ":" + Convert.ToBase64String(File.ReadAllBytes(p)))));
    }
}
