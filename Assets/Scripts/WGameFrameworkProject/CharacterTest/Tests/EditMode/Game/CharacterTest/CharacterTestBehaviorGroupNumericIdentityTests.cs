using System.IO;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBehaviorGroupNumericIdentityTests
    {
        [Test]
        public void FormalCatalog_LoadsAndKeepsEntryNumericIdentityAcrossRevisions()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(state.Success, state.Diagnostic);

            JObject document = JObject.Parse(File.ReadAllText(state.GroupCatalogPath));
            int[] revisions = { 4, 5, 6 };
            for (int i = 0; i < revisions.Length; i++)
            {
                JObject group = FindGroup(document, revisions[i]);
                JArray entries = (JArray)group["entries"];
                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual(1, entries[0].Value<int>("numericId"));
                Assert.AreEqual(2, entries[1].Value<int>("numericId"));
                Assert.AreEqual(1, entries[0].Value<int>("definitionNumericId"));
                Assert.AreEqual(2, entries[1].Value<int>("definitionNumericId"));
                Assert.IsTrue(CharacterTestAuthoredContentIds.IsActive(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.EntryNumericDomainId,
                    entries[0].Value<int>("numericId")));
                Assert.IsTrue(CharacterTestAuthoredContentIds.IsActive(
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.EntryNumericDomainId,
                    entries[1].Value<int>("numericId")));
            }
        }

        [TestCase("unknown")]
        [TestCase("duplicate")]
        public void CatalogLoad_RejectsUnknownOrDuplicateEntryNumericIdentity(string mutation)
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(state.Success, state.Diagnostic);
            JObject document = JObject.Parse(File.ReadAllText(state.GroupCatalogPath));
            JObject group = FindGroup(document, 4);
            JArray entries = (JArray)group["entries"];
            if (mutation == "unknown")
                entries[0]["numericId"] = 999;
            else
                entries[1]["numericId"] = entries[0].Value<int>("numericId");

            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult loaded =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    document.ToString(),
                    state.CardCatalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            Assert.IsFalse(loaded.Success);
            StringAssert.Contains("numericId", loaded.Diagnostic);
        }

        [Test]
        public void CatalogLoad_RejectsDefinitionNumericIdentityThatDiffersFromPinnedCard()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(state.Success, state.Diagnostic);
            JObject document = JObject.Parse(File.ReadAllText(state.GroupCatalogPath));
            JObject group = FindGroup(document, 4);
            ((JArray)group["entries"])[0]["definitionNumericId"] = 2;

            CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult loaded =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                    document.ToString(),
                    state.CardCatalog,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            Assert.IsFalse(loaded.Success);
            StringAssert.Contains("definitionNumericId", loaded.Diagnostic);
        }

        private static JObject FindGroup(JObject document, int revision)
        {
            foreach (JToken token in (JArray)document["groups"])
            {
                JObject group = (JObject)token;
                if (group.Value<int>("revision") == revision)
                    return group;
            }

            Assert.Fail("Missing formal behavior group revision " + revision + ".");
            return null;
        }
    }
}
