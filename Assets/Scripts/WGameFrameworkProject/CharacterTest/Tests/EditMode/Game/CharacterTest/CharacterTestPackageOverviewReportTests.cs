using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestPackageOverviewReportTests
    {
        private const string Bundle = "MxFramework/Resources/Bundles/StandaloneWindows64/global.character.sample.assetbundle";
        private const string Unlisted = "MxFramework/Resources/Bundles/StandaloneWindows64/unknown.assetbundle";

        [Test]
        public void Review_DistinguishesCandidatesFromDefectsAndPreservesUnknownOwnership()
        {
            var inventory = new CharacterTestPackageOverviewReport.Inventory { TotalBytes = 100 };
            inventory.Files.Add(new CharacterTestPackageOverviewReport.FileEntry { RelativePath = "GameAssembly.dll", Bytes = 40 });
            inventory.Files.Add(new CharacterTestPackageOverviewReport.FileEntry
                { RelativePath = "CharacterTestPerformance_Data/il2cpp_data/Metadata/global-metadata.dat", Bytes = 20 });
            inventory.Files.Add(new CharacterTestPackageOverviewReport.FileEntry
                { RelativePath = Unlisted, Category = CharacterTestPackageOverviewReport.UnlistedBundles, Ownership = "unknown", Bytes = 1 });
            var configuration = new JObject { ["configuration"] = "Release", ["scriptingBackend"] = "IL2CPP",
                ["developmentBuild"] = false, ["scriptDebugging"] = false, ["autoconnectProfiler"] = false, ["deepProfiling"] = false };
            var details = new JObject { ["assets"] = new JArray(
                new JObject { ["path"] = "Assets/Resources/Fonts/Font.otf", ["bytes"] = 30 },
                new JObject { ["path"] = "Assets/Model.fbx", ["bytes"] = 10, ["bundleDeclaration"] = "formal.bundle",
                    ["referenceChain"] = new JArray("Assets/Scene.unity", "Assets/hero_character_preview.prefab", "Assets/Model.fbx") }) };
            var result = CharacterTestPackageReview.Evaluate(inventory, configuration, details, "Passed", "Succeeded");
            Assert.IsFalse(result.Exists(f => f.Status == CharacterTestPackageReview.Kind.Fix));
            Assert.IsTrue(result.Exists(f => f.Status == CharacterTestPackageReview.Kind.Candidate && f.Title.Contains("双重入口")));
            Assert.IsTrue(result.Exists(f => f.Status == CharacterTestPackageReview.Kind.Confirm && f.Title.Contains("归属")));
            Assert.IsTrue(result.Exists(f => f.Status == CharacterTestPackageReview.Kind.Unknown && f.Title.Contains("是否达标")));
            configuration["developmentBuild"] = true;
            result = CharacterTestPackageReview.Evaluate(inventory, configuration, details, "Passed", "Succeeded");
            Assert.IsTrue(result.Exists(f => f.Status == CharacterTestPackageReview.Kind.Fix && f.Title.Contains("配置")));
        }

        [Test]
        public void Review_DoesNotCallBuildOnlyOrMissingEvidenceFullyAccepted()
        {
            var html = new System.Text.StringBuilder();
            CharacterTestPackageReview.Append(html, new CharacterTestPackageOverviewReport.Inventory(), null, null, "BuildOnly", "Succeeded");
            StringAssert.Contains("完整验收尚未确认", html.ToString());
            StringAssert.DoesNotContain("运行验收已通过", html.ToString());
            var inventory = new CharacterTestPackageOverviewReport.Inventory();
            inventory.Files.Add(new CharacterTestPackageOverviewReport.FileEntry { RelativePath = "GameAssembly.pdb", Bytes = 20 });
            var result = CharacterTestPackageReview.Evaluate(inventory, null, null, "Failed", "Failed");
            Assert.AreEqual(2, result.FindAll(f => f.Status == CharacterTestPackageReview.Kind.Fix).Count);
        }

        [Test]
        public void DetailCaptureFailure_IsAReportFailureAndDoesNotRewriteRunEvidence()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player"); WriteManifest(player);
                string run = Path.Combine(root, "Run.txt"); File.WriteAllText(run, "buildResult: Succeeded\nstatus: finished\n");
                File.WriteAllText(CharacterTestPackageDetailEvidence.ErrorPathFor(run), "injected detail failure");
                string report = Path.Combine(root, CharacterTestPackageOverviewReport.FileName);
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageOverviewReport.Write(report,
                    player, true, "Passed", "Succeeded", null, run));
                Assert.AreEqual("buildResult: Succeeded\nstatus: finished\n", File.ReadAllText(run));
                Assert.DoesNotThrow(() => CharacterTestPackageOverviewReport.Write(report,
                    player, false, "Failed", "Failed", null, run));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DetailedReport_BindsEvidenceToPlayerAndEscapesAssetNames()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player"); WriteManifest(player);
                string binary = Path.Combine(player, "GameAssembly.dll"); File.WriteAllText(binary, "native");
                string hash = CharacterTestPackageDetailEvidence.Hash(binary);
                var evidence = new JObject
                {
                    ["identities"] = new JArray(new JObject { ["path"] = "GameAssembly.dll", ["bytes"] = 6, ["sha256"] = hash }),
                    ["assetBytes"] = 12, ["assetRecordCount"] = 1, ["referenceEvidence"] = "captured",
                    ["assetTypes"] = new JArray(new JObject { ["name"] = "Texture2D", ["bytes"] = 12 }),
                    ["assets"] = new JArray(new JObject { ["path"] = "Assets/<unsafe>.png", ["bytes"] = 12,
                        ["types"] = "Texture2D", ["origins"] = new JArray("Resources 目录") }),
                    ["native"] = new JObject { ["status"] = "Verified", ["binaryBytes"] = 6, ["binarySha256"] = hash,
                        ["groups"] = new JArray(new JObject { ["name"] = "Shared generic code", ["bytes"] = 6 }) }
                };
                string run = Path.Combine(root, "Run.txt");
                File.WriteAllText(CharacterTestPackageDetailEvidence.PathFor(run), evidence.ToString());
                string report = Path.Combine(root, CharacterTestPackageOverviewReport.FileName);
                CharacterTestPackageOverviewReport.Write(report, player, true, "BuildOnly", "Succeeded", null, run);
                string html = File.ReadAllText(report);
                StringAssert.Contains("代码与数据归属", html);
                StringAssert.Contains("压缩前构成", html);
                StringAssert.Contains("Assets/&lt;unsafe&gt;.png", html);
                StringAssert.Contains("共享泛型", html);
                File.WriteAllText(binary, "change"); // Same length, different build identity.
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageOverviewReport.Write(report,
                    player, true, "Passed", "Succeeded", null, run));
                CharacterTestPackageOverviewReport.Write(report, player, false, "Failed", "Failed", null, run);
                Assert.IsFalse(File.Exists(Path.Combine(root, CharacterTestPackageDetailEvidence.SnapshotName)));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DetailEvidence_RejectsContradictoryTotalsAndEscapingIdentity()
        {
            string root = NewRoot();
            try
            {
                string binary = Path.Combine(root, "GameAssembly.dll"); File.WriteAllText(binary, "native");
                var evidence = new JObject { ["identities"] = new JArray(new JObject
                    { ["path"] = "GameAssembly.dll", ["bytes"] = 6, ["sha256"] = CharacterTestPackageDetailEvidence.Hash(binary) }),
                    ["assetBytes"] = 2, ["assets"] = new JArray(new JObject { ["bytes"] = 1 }),
                    ["assetTypes"] = new JArray(new JObject { ["bytes"] = 2 }) };
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageDetailEvidence.Validate(evidence, root));
                evidence["identities"][0]["path"] = "../GameAssembly.dll";
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageDetailEvidence.Validate(evidence, root));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ConfigurationSnapshot_ResolvesSymbolsFromWarmRunReportDirectory()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player");
                WriteManifest(player);
                string editorReport = Path.Combine(root, "BuildRun.txt");
                string symbols = Path.Combine(root, "Symbols", "Development-test");
                Directory.CreateDirectory(symbols);
                File.WriteAllText(CharacterTestWindowsBuildConfiguration.ReceiptPath(editorReport),
                    new JObject { ["configuration"] = "Development", ["scriptingBackend"] = "IL2CPP",
                        ["symbolsDirectory"] = "Symbols/Development-test" }.ToString());
                string reports = Path.Combine(root, "Reports");
                CharacterTestPackageOverviewReport.Write(Path.Combine(reports, CharacterTestPackageOverviewReport.FileName),
                    player, true, "BuildOnly", "Succeeded", null, editorReport);
                var snapshot = JObject.Parse(File.ReadAllText(Path.Combine(reports,
                    CharacterTestPackageOverviewReport.ConfigurationSnapshotFileName)));
                Assert.AreEqual(Path.GetFullPath(symbols), Path.GetFullPath(Path.Combine(reports,
                    (string)snapshot["symbolsDirectory"])));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void Inventory_PartitionsEveryFileAndIdentifiesManifestAndResidueOwnership()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player");
                WriteManifest(player, Bundle);
                WritePlayerFile(player, Bundle, 100);
                WritePlayerFile(player, Unlisted, 25);
                WritePlayerFile(player, "MxFramework/Resources/ExternalBundles/StandaloneWindows64/external.bundle", 50);
                WritePlayerFile(player, "MxFramework/Generated/CharacterPackages/000001/config.json", 30);
                WritePlayerFile(player, "MxFramework/CharacterTest/RuntimeCatalogs/catalog.bytes", 20);
                WritePlayerFile(player, "MxFramework/Other/config.json", 10);
                File.WriteAllBytes(Path.Combine(player, "UnityPlayer.dll"), new byte[40]);
                File.WriteAllBytes(Path.Combine(player, "CharacterTestPerformance_Data", "sharedassets0.assets"), new byte[80]);
                string residue = Path.Combine(root, "residue-report.txt");
                File.WriteAllText(residue, "file: unknown " + Unlisted + " bytes=25\n");
                var groups = new Dictionary<string, string> { { "global.character.sample.assetbundle", "character.sample" } };

                CharacterTestPackageOverviewReport.Inventory inventory =
                    CharacterTestPackageOverviewReport.Analyze(player, residue, groups);

                Assert.AreEqual(inventory.Files.Count, 9);
                Assert.AreEqual(inventory.TotalBytes,
                    inventory.CategoryBytes(CharacterTestPackageOverviewReport.PlayerFiles)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.PlayerData)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.CurrentBundles)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.UnlistedBundles)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.ExternalBundles)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.CharacterPackages)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.RuntimeCatalogs)
                    + inventory.CategoryBytes(CharacterTestPackageOverviewReport.OtherStreaming));
                Assert.AreEqual(100, inventory.CategoryBytes(CharacterTestPackageOverviewReport.CurrentBundles));
                Assert.AreEqual(25, inventory.CategoryBytes(CharacterTestPackageOverviewReport.UnlistedBundles));
                Assert.AreEqual("unknown", inventory.Files.Find(file => file.Category == CharacterTestPackageOverviewReport.UnlistedBundles).Ownership);
                Assert.AreEqual("角色", inventory.Files.Find(file => file.Category == CharacterTestPackageOverviewReport.CurrentBundles).BundleGroup);
                Assert.AreEqual(inventory.TotalBytes - 120, inventory.StreamingBytes);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void Inventory_RejectsMissingDuplicateAndUnsafeManifestBundles()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player");
                WriteManifest(player, Bundle);
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageOverviewReport.Analyze(player));
                WritePlayerFile(player, Bundle, 1);
                WriteManifest(player, Bundle, Bundle);
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageOverviewReport.Analyze(player));
                WriteManifest(player, "MxFramework/Resources/Bundles/../escape.assetbundle");
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageOverviewReport.Analyze(player));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void FailedAndBuildOnlyReports_DoNotUseOldPlayerFilesAndEscapeContent()
        {
            string root = NewRoot();
            try
            {
                string report = Path.Combine(root, CharacterTestPackageOverviewReport.FileName);
                File.WriteAllText(report, "old report");
                string residueSnapshot = Path.Combine(root, CharacterTestPackageOverviewReport.ResidueSnapshotFileName);
                File.WriteAllText(residueSnapshot, "old residue");
                CharacterTestPackageOverviewReport.Write(report, Path.Combine(root, "OldPlayer"), false,
                    "Failed", "Exception", "<unsafe>&", null);
                string failed = File.ReadAllText(report);
                StringAssert.Contains("本次没有成功构建 Player", failed);
                StringAssert.Contains("&lt;unsafe&gt;&amp;", failed);
                Assert.IsFalse(failed.Contains("old report"));
                Assert.IsFalse(File.Exists(residueSnapshot));

                CharacterTestPackageOverviewReport.Write(report, null, false,
                    "BuildOnly", "Succeeded", null, null);
                string buildOnly = File.ReadAllText(report);
                StringAssert.Contains("BuildOnly", buildOnly);
                StringAssert.Contains("本次没有成功构建 Player", buildOnly);
                Assert.IsFalse(buildOnly.Contains("&lt;unsafe&gt;"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void BuildOnly_WithCurrentPlayer_ShowsMeasuredSizeWithoutPassedVerdict()
        {
            string root = NewRoot();
            try
            {
                string player = Path.Combine(root, "Player");
                WriteManifest(player);
                File.WriteAllBytes(Path.Combine(player, "UnityPlayer.dll"), new byte[1048576]);
                string report = Path.Combine(root, CharacterTestPackageOverviewReport.FileName);
                string residue = Path.Combine(root, "residue-source.txt");
                File.WriteAllText(residue, "ownershipState: trusted\n");

                CharacterTestPackageOverviewReport.Write(report, player, true,
                    "BuildOnly", "Succeeded", null, null, residueReportPath: residue);

                string html = File.ReadAllText(report);
                StringAssert.Contains("class=\"badge BuildOnly\"", html);
                StringAssert.Contains("Player 文件总量", html);
                StringAssert.Contains("整包构成", html);
                StringAssert.Contains("href=\"CharacterTestBundleResidueReport.txt\"", html);
                Assert.AreEqual("ownershipState: trusted\n", File.ReadAllText(Path.Combine(root,
                    CharacterTestPackageOverviewReport.ResidueSnapshotFileName)));
                Assert.IsFalse(html.Contains("class=\"badge Passed\""));
            }
            finally { Directory.Delete(root, true); }
        }

        private static string NewRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "CharacterTestPackageOverview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteManifest(string player, params string[] bundles)
        {
            string path = Path.Combine(player, "CharacterTestPerformance_Data", "StreamingAssets",
                "MxFramework", "Resources", "global_resource_artifact_set.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var artifacts = new JArray();
            foreach (string bundle in bundles)
                artifacts.Add(new JObject { ["kind"] = "bundle", ["relativePath"] = bundle });
            File.WriteAllText(path, new JObject
            {
                ["buildTarget"] = "StandaloneWindows64", ["artifacts"] = artifacts
            }.ToString());
        }

        private static void WritePlayerFile(string player, string streamRelativePath, int bytes)
        {
            string path = Path.Combine(player, "CharacterTestPerformance_Data", "StreamingAssets",
                streamRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[bytes]);
        }
    }
}
