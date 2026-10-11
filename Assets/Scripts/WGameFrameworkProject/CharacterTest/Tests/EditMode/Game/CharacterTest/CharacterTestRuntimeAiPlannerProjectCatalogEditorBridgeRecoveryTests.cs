using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiPlannerProjectCatalogEditorBridgeRecoveryTests
    {
        private const string OldCardIndex = "old-card-index\n";
        private const string OldGroupCatalog = "old-group-catalog\n";
        private const string OldCardA = "old-card-a\n";
        private const string OldCardB = "old-card-b\n";

        private string _root;
        private string _streamingRoot;
        private string _recoveryRoot;
        private string _catalogDirectory;
        private string _cardPath;
        private string _groupPath;
        private string _cardFilesDirectory;
        private CharacterTestRuntimeAiPlannerProjectCatalogSeedResult _seed;
        private CharacterTestRuntimeAiPlannerProjectCardStoragePayload _candidatePayload;
        private string _candidateGroupJson;

        [SetUp]
        public void SetUp()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            _root = Path.Combine(
                projectRoot,
                "Temp",
                "RtAiCatTxn",
                Guid.NewGuid().ToString("N").Substring(0, 8));
            _streamingRoot = Path.Combine(_root, "StreamingAssets");
            _recoveryRoot = Path.Combine(
                _root,
                "UserSettings",
                "RuntimeAiLab",
                "ProjectCatalogRecovery");
            _catalogDirectory = Path.Combine(
                _streamingRoot,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
            _cardPath = Path.Combine(
                _catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            _groupPath = Path.Combine(
                _catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            _cardFilesDirectory = Path.Combine(
                _catalogDirectory,
                CharacterTestRuntimeAiPlannerProjectCardStorage.CardsDirectoryName);

            Directory.CreateDirectory(_cardFilesDirectory);
            WriteExact(_cardPath, OldCardIndex);
            WriteExact(_groupPath, OldGroupCatalog);
            WriteExact(Path.Combine(_cardFilesDirectory, "legacy-a.json"), OldCardA);
            WriteExact(Path.Combine(_cardFilesDirectory, "legacy-b.json"), OldCardB);
            WriteExact(Path.Combine(_cardFilesDirectory, "unmanaged.keep"), "untouched\n");

            _seed = CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.IsTrue(_seed.Success, _seed.Diagnostic);
            _candidatePayload = CharacterTestRuntimeAiPlannerProjectCardStorage.CreatePayload(_seed.Cards);
            _candidateGroupJson =
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(_seed.Groups);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestCase((int)CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
            .AfterCardFilesInstalled)]
        [TestCase((int)CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
            .AfterCardIndexInstalled)]
        [TestCase((int)CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
            .AfterGroupCatalogInstalled)]
        public void IncompleteTransaction_OnNextLoad_RestoresAllOriginalFiles(int faultPointValue)
        {
            var faultPoint =
                (CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint)faultPointValue;
            using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.BeginRecoveryTestScope(
                       _streamingRoot,
                       _recoveryRoot,
                       point => ThrowProcessInterruptionAt(point, faultPoint)))
            {
                Assert.Throws<CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException>(
                    () => CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteForRecoveryTests(
                        CreateWriteRequest()));
                Assert.AreEqual(1, CountJournals(), "The interrupted transaction must remain durable.");

                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
                        .RecoverPendingTransactionsForTests(out string diagnostic),
                    diagnostic);

                AssertOriginalState();
                Assert.AreEqual(0, CountJournals());
            }
        }

        [Test]
        public void CommittedMarker_OnNextLoad_PreservesNewGenerationAndCleansRecoveryState()
        {
            const CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint faultPoint =
                CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                    .AfterCommittedMarkerPersisted;
            using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.BeginRecoveryTestScope(
                       _streamingRoot,
                       _recoveryRoot,
                       point => ThrowProcessInterruptionAt(point, faultPoint)))
            {
                Assert.Throws<CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException>(
                    () => CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteForRecoveryTests(
                        CreateWriteRequest()));
                Assert.AreEqual(1, CountJournals());

                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
                        .RecoverPendingTransactionsForTests(out string diagnostic),
                    diagnostic);

                AssertCommittedState();
                Assert.AreEqual(0, CountJournals());
            }
        }

        [Test]
        public void OrdinaryWriteException_UsesPersistentRecoveryPathBeforeReturningFailure()
        {
            using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.BeginRecoveryTestScope(
                       _streamingRoot,
                       _recoveryRoot,
                       point =>
                       {
                           if (point == CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                                   .AfterCardIndexInstalled)
                           {
                               throw new IOException("Injected ordinary I/O failure.");
                           }
                       }))
            {
                CharacterTestRuntimeAiPlannerProjectCatalogWriteResult result =
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteForRecoveryTests(
                        CreateWriteRequest());

                Assert.IsFalse(result.Success);
                StringAssert.Contains("完整回滚", result.Diagnostic);
                AssertOriginalState();
                Assert.AreEqual(0, CountJournals());
            }
        }

        [Test]
        public void TamperedBackup_CannotProveRecovery_LeavesJournalAndFailsExplicitly()
        {
            using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.BeginRecoveryTestScope(
                       _streamingRoot,
                       _recoveryRoot,
                       point => ThrowProcessInterruptionAt(
                           point,
                           CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                               .AfterCardIndexInstalled)))
            {
                Assert.Throws<CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException>(
                    () => CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteForRecoveryTests(
                        CreateWriteRequest()));
                string journalPath = FindSingleJournal();
                WriteExact(
                    Path.Combine(Path.GetDirectoryName(journalPath), "backup", "card-index.json"),
                    "tampered\n");

                Assert.IsFalse(
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
                        .RecoverPendingTransactionsForTests(out string diagnostic));
                StringAssert.Contains("无法证明", diagnostic);
                Assert.IsTrue(File.Exists(journalPath), "Unproven recovery must retain its journal.");
            }
        }

        [Test]
        public void JournalTargetOutsideStreamingAssets_IsRejectedAndRetained()
        {
            using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.BeginRecoveryTestScope(
                       _streamingRoot,
                       _recoveryRoot,
                       point => ThrowProcessInterruptionAt(
                           point,
                           CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint
                               .AfterCardFilesInstalled)))
            {
                Assert.Throws<CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException>(
                    () => CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteForRecoveryTests(
                        CreateWriteRequest()));
                string journalPath = FindSingleJournal();
                JObject journal = JObject.Parse(File.ReadAllText(journalPath, Encoding.UTF8));
                journal["cardCatalogPath"] = Path.Combine(_root, "outside", "card-index.json");
                WriteExact(journalPath, journal.ToString());

                Assert.IsFalse(
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
                        .RecoverPendingTransactionsForTests(out string diagnostic));
                StringAssert.Contains("StreamingAssets", diagnostic);
                Assert.IsTrue(File.Exists(journalPath));
            }
        }

        private CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest CreateWriteRequest()
        {
            return new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                _cardPath,
                _groupPath,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(_cardPath),
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(_groupPath),
                expectedCardCatalogHash: 0L,
                cardCatalog: _seed.Cards,
                groupCatalogJson: _candidateGroupJson,
                explicitLegacyMigration: true);
        }

        private static void ThrowProcessInterruptionAt(
            CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint actual,
            CharacterTestRuntimeAiPlannerProjectCatalogTransactionFaultPoint expected)
        {
            if (actual == expected)
            {
                throw new CharacterTestRuntimeAiPlannerProjectCatalogSimulatedInterruptionException(
                    "Injected process interruption at " + actual + ".");
            }
        }

        private void AssertOriginalState()
        {
            Assert.AreEqual(OldCardIndex, File.ReadAllText(_cardPath, Encoding.UTF8));
            Assert.AreEqual(OldGroupCatalog, File.ReadAllText(_groupPath, Encoding.UTF8));
            Assert.AreEqual(
                OldCardA,
                File.ReadAllText(Path.Combine(_cardFilesDirectory, "legacy-a.json"), Encoding.UTF8));
            Assert.AreEqual(
                OldCardB,
                File.ReadAllText(Path.Combine(_cardFilesDirectory, "legacy-b.json"), Encoding.UTF8));
            CollectionAssert.AreEquivalent(
                new[] { "legacy-a.json", "legacy-b.json" },
                Directory.GetFiles(_cardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .ToArray());
            Assert.AreEqual(
                "untouched\n",
                File.ReadAllText(Path.Combine(_cardFilesDirectory, "unmanaged.keep"), Encoding.UTF8));
        }

        private void AssertCommittedState()
        {
            Assert.AreEqual(_candidatePayload.IndexJson, File.ReadAllText(_cardPath, Encoding.UTF8));
            Assert.AreEqual(_candidateGroupJson, File.ReadAllText(_groupPath, Encoding.UTF8));
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _candidatePayload.Files.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProjectCardFile file = _candidatePayload.Files[i];
                string targetPath = Path.Combine(
                    _catalogDirectory,
                    file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                expected.Add(Path.GetFileName(targetPath));
                Assert.AreEqual(file.Json, File.ReadAllText(targetPath, Encoding.UTF8));
            }
            CollectionAssert.AreEquivalent(
                expected,
                Directory.GetFiles(_cardFilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .ToArray());
            Assert.AreEqual(
                "untouched\n",
                File.ReadAllText(Path.Combine(_cardFilesDirectory, "unmanaged.keep"), Encoding.UTF8));
        }

        private int CountJournals()
        {
            return Directory.Exists(_recoveryRoot)
                ? Directory.GetFiles(_recoveryRoot, "transaction.json", SearchOption.AllDirectories).Length
                : 0;
        }

        private string FindSingleJournal()
        {
            string[] journals = Directory.GetFiles(
                _recoveryRoot,
                "transaction.json",
                SearchOption.AllDirectories);
            Assert.AreEqual(1, journals.Length);
            return journals[0];
        }

        private static void WriteExact(string path, string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(value ?? string.Empty));
        }
    }
}
