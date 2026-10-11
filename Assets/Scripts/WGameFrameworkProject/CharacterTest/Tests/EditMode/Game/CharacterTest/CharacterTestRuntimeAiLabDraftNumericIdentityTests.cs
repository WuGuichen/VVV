using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabDraftNumericIdentityTests
    {
        [Test]
        public void StandalonePresentation_PreparesIdentityBeforeCandidateCapture()
        {
            var workspace = CreateWorkspace();
            var presentation = workspace.Groups.Values.Single().Clone(newIdentity: true);
            presentation.Profile.Entries.Clear();
            presentation.Profile.Entries.Add(presentation.Entries.Single().RuntimeEntry.Clone());
            var allocated = new Dictionary<string, int>(StringComparer.Ordinal);
            var previousBatch = CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring;
            var previous = CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring;
            try
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = null;
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring =
                    (domain, source) => ResolveDeterministically(allocated, domain, source);
                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace, presentation);
                var entry = presentation.Entries.Single().RuntimeEntry;
                Assert.Greater(entry.NumericId, 0);
                Assert.Greater(entry.DefinitionNumericId, 0);
                Assert.AreEqual(entry.NumericId, presentation.Profile.Entries.Single().NumericId);
                Assert.AreEqual(entry.DefinitionNumericId, presentation.Profile.Entries.Single().DefinitionNumericId);
                Assert.AreNotEqual(workspace.Groups.Values.Single().Entries.Single().RuntimeEntry.NumericId, entry.NumericId);
                int id = entry.NumericId;
                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace, presentation);
                Assert.AreEqual(id, entry.NumericId);
                Assert.IsFalse(workspace.Groups.ContainsKey(presentation.GroupDraftId));
            }
            finally
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = previousBatch;
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = previous;
            }
        }

        [Test]
        public void PrepareForSave_AssignsStableEntryAndDefinitionIdentitiesAcrossRepeatedPreparation()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = CreateWorkspace();
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry =
                workspace.Groups.Values.Single().Entries.Single();
            entry.RuntimeEntry.NumericId = 0;
            entry.RuntimeEntry.DefinitionNumericId = 0;

            Dictionary<string, int> allocated = new Dictionary<string, int>(StringComparer.Ordinal);
            var previousBatch = CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring;
            Func<int, string, int> previous = CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring;
            try
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = null;
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring =
                    (domain, sourceKey) => ResolveDeterministically(allocated, domain, sourceKey);

                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace);
                int numericId = entry.RuntimeEntry.NumericId;
                int definitionNumericId = entry.RuntimeEntry.DefinitionNumericId;

                Assert.That(numericId, Is.GreaterThan(0));
                Assert.That(definitionNumericId, Is.GreaterThan(0));
                Assert.That(allocated.Keys, Does.Contain("420:behavior-entry/" + entry.EntryId));
                Assert.That(allocated.Keys, Does.Contain("417:behavior-card/test.card"));

                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace);

                Assert.AreEqual(numericId, entry.RuntimeEntry.NumericId);
                Assert.AreEqual(definitionNumericId, entry.RuntimeEntry.DefinitionNumericId);
                Assert.AreEqual(2, allocated.Count);
            }
            finally
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = previousBatch;
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = previous;
            }
        }

        [Test]
        public void FlushReload_PreservesPreparedNumericIdentities()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "RuntimeAiLabDraftNumericIdentityTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var writer = new CharacterTestRuntimeAiLabDraftPersistence(root);
                CharacterTestRuntimeAiLabDraftWorkspace workspace = CreateWorkspace();
                writer.AdoptWorkspace(workspace);
                CharacterTestRuntimeAiLabBehaviorEntryDraft sourceEntry =
                    workspace.Groups.Values.Single().Entries.Single();
                sourceEntry.RuntimeEntry.NumericId = 0;
                sourceEntry.RuntimeEntry.DefinitionNumericId = 0;

                Dictionary<string, int> allocated = new Dictionary<string, int>(StringComparer.Ordinal);
                var previousBatch = CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring;
                Func<int, string, int> previous = CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring;
                try
                {
                    CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = null;
                    CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring =
                        (domain, sourceKey) => ResolveDeterministically(allocated, domain, sourceKey);

                    Assert.IsTrue(writer.Flush(), writer.LastStatus);
                    int expectedNumericId = sourceEntry.RuntimeEntry.NumericId;
                    int expectedDefinitionNumericId = sourceEntry.RuntimeEntry.DefinitionNumericId;

                    var reader = new CharacterTestRuntimeAiLabDraftPersistence(root);
                    Assert.IsTrue(
                        reader.TryLoad(workspace.WorkspaceId, out CharacterTestRuntimeAiLabDraftWorkspace loaded, out string issue),
                        issue);
                    CharacterTestRuntimeAiLabBehaviorEntryDraft loadedEntry =
                        loaded.Groups[workspace.Groups.Keys.Single()].Entries.Single();

                    Assert.AreEqual(expectedNumericId, loadedEntry.RuntimeEntry.NumericId);
                    Assert.AreEqual(expectedDefinitionNumericId, loadedEntry.RuntimeEntry.DefinitionNumericId);
                }
                finally
                {
                    CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = previousBatch;
                    CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = previous;
                }
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void CloneWithNewIdentity_ClearsEntryNumericIdAndRetainsDefinitionNumericId()
        {
            CharacterTestRuntimeAiLabBehaviorGroupDraft source = CreateWorkspace().Groups.Values.Single();
            CharacterTestRuntimeAiLabBehaviorEntryDraft sourceEntry = source.Entries.Single();
            sourceEntry.RuntimeEntry.NumericId = 701;
            sourceEntry.RuntimeEntry.DefinitionNumericId = 417;

            CharacterTestRuntimeAiLabBehaviorGroupDraft clone = source.Clone(newIdentity: true);
            CharacterTestRuntimeAiLabBehaviorEntryDraft cloneEntry = clone.Entries.Single();

            Assert.AreNotEqual(source.GroupDraftId, clone.GroupDraftId);
            Assert.AreNotEqual(sourceEntry.EntryId, cloneEntry.EntryId);
            Assert.AreEqual(0, cloneEntry.RuntimeEntry.NumericId);
            Assert.AreEqual(sourceEntry.RuntimeEntry.DefinitionNumericId, cloneEntry.RuntimeEntry.DefinitionNumericId);
        }

        [Test]
        public void BatchPath_ResolvesAllRequestsInSingleCall()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = CreateWorkspace();

            var card = workspace.Cards.Values.Single();
            var profile2 = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.group2");
            var runtimeEntry2 = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 10);
            var group2 = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null, "test.group2", "Second group", profile2);
            group2.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null, card.CardDraftId,
                runtimeEntry2.BehaviorId, 10, disabled: false, runtimeEntry2));
            group2.NormalizeOrders();
            workspace.AddLoadedGroup(group2);

            foreach (var g in workspace.Groups.Values)
                foreach (var e in g.Entries)
                {
                    e.RuntimeEntry.NumericId = 0;
                    e.RuntimeEntry.DefinitionNumericId = 0;
                }

            int batchCallCount = 0;
            int singleCallCount = 0;
            var allocated = new Dictionary<string, int>(StringComparer.Ordinal);

            var previousBatch = CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring;
            var previous = CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring;
            try
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = requests =>
                {
                    batchCallCount++;
                    var result = new Dictionary<(int DomainId, string SourceKey), int>();
                    foreach (var req in requests)
                    {
                        string key = req.DomainId + ":" + req.SourceKey;
                        if (!allocated.TryGetValue(key, out int existing))
                        {
                            existing = req.DomainId == 420
                                ? 700 + allocated.Count + 1
                                : 900 + allocated.Count + 1;
                            allocated[key] = existing;
                        }
                        result[req] = existing;
                    }
                    return result;
                };
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = (_, __) =>
                {
                    singleCallCount++;
                    return -1;
                };

                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace);

                Assert.AreEqual(1, batchCallCount, "BatchResolveForAuthoring must be called exactly once.");
                Assert.AreEqual(0, singleCallCount, "Single-request resolver must not be called when batch delegate is set.");

                foreach (var g in workspace.Groups.Values)
                    foreach (var e in g.Entries)
                    {
                        Assert.Greater(e.RuntimeEntry.NumericId, 0);
                        Assert.Greater(e.RuntimeEntry.DefinitionNumericId, 0);
                    }

                var allEntries = workspace.Groups.Values.SelectMany(g => g.Entries).ToList();
                Assert.AreEqual(2, allEntries.Count);
                Assert.AreEqual(
                    allEntries[0].RuntimeEntry.DefinitionNumericId,
                    allEntries[1].RuntimeEntry.DefinitionNumericId,
                    "Entries sharing the same card must share the same DefinitionNumericId.");
                Assert.AreNotEqual(
                    allEntries[0].RuntimeEntry.NumericId,
                    allEntries[1].RuntimeEntry.NumericId,
                    "Distinct entries must have distinct NumericIds.");

                foreach (var g in workspace.Groups.Values)
                    foreach (var pe in g.Profile.Entries)
                    {
                        var ae = g.Entries.Single(e => string.Equals(e.EntryId, pe.EntryId, StringComparison.Ordinal));
                        Assert.AreEqual(ae.RuntimeEntry.NumericId, pe.NumericId);
                        Assert.AreEqual(ae.RuntimeEntry.DefinitionNumericId, pe.DefinitionNumericId);
                    }

                int prevBatchCalls = batchCallCount;
                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(workspace);
                Assert.AreEqual(prevBatchCalls + 1, batchCallCount);
                foreach (var g in workspace.Groups.Values)
                    foreach (var e in g.Entries)
                        Assert.Greater(e.RuntimeEntry.NumericId, 0);
            }
            finally
            {
                CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring = previousBatch;
                CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = previous;
            }
        }

        private static CharacterTestRuntimeAiLabDraftWorkspace CreateWorkspace()
        {
            var workspace = new CharacterTestRuntimeAiLabDraftWorkspace(
                Guid.NewGuid().ToString("D"),
                "Numeric identity test workspace");
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                "test.card",
                "Numeric identity test card",
                "Tactical",
                new CharacterTestRuntimeAiLabBehaviorCardPayload());
            workspace.AddLoadedCard(card);

            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.group");
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 10);
            var group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null,
                "test.group",
                "Numeric identity test group",
                profile);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                10,
                disabled: false,
                runtimeEntry));
            group.NormalizeOrders();
            workspace.AddLoadedGroup(group);
            return workspace;
        }

        private static int ResolveDeterministically(
            IDictionary<string, int> allocated,
            int domain,
            string sourceKey)
        {
            string key = domain + ":" + sourceKey;
            if (allocated.TryGetValue(key, out int existing))
                return existing;

            int value = domain == 420
                ? 700 + allocated.Count + 1
                : 900 + allocated.Count + 1;
            allocated.Add(key, value);
            return value;
        }
    }
}
