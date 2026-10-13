using System;
using System.IO;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestWindowsBuildConfigurationTests
    {
        [Test]
        public void RuntimePaths_RejectLongWindowsOutputBeforeBuild()
        {
            Assert.DoesNotThrow(() => CharacterTestWindowsBuildConfiguration.RequirePlayerPaths(
                "E:/WGFBuilds/Player", new[] { "MxFramework/Resources/Bundles/StandaloneWindows64/sample.assetbundle" }));
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => CharacterTestWindowsBuildConfiguration.RequirePlayerPaths(
                "E:/" + new string('x', 230), new[] { "sample.assetbundle" }));
        }

        [Test]
        public void JsonPreservation_CoversRuntimePayloadGraphAndAttributeConverters()
        {
            string xml = CharacterTestJsonPreservation.CreateDocument().DocumentElement.OuterXml;
            StringAssert.Contains("StreamingResourceCatalogLoader/ResourceCatalogDto", xml);
            StringAssert.Contains("AudioRuntimeCatalogJson/AudioRuntimeCatalogDto", xml);
            StringAssert.Contains("PhysicalAudioRuntimeCatalogJson/ResponsePointDto", xml);
            StringAssert.Contains("GameplayTriggerAuthoringDocument", xml);
            StringAssert.Contains("CharacterActionIdJsonConverter", xml);
            StringAssert.DoesNotContain("<assembly fullname=\"MxFramework.Core\" preserve=\"all\"", xml);
            var actual = new System.Xml.XmlDocument();
            actual.Load(CharacterTestJsonPreservation.OutputPath);
            Assert.AreEqual(xml, actual.DocumentElement.OuterXml, "Regenerate runtime JSON preservation after adding a JSON entry point.");
        }

        [Test]
        public void Profiles_DefaultToReleaseAndKeepDebugOptionsOutOfDistributable()
        {
            Assert.AreEqual(CharacterTestWindowsBuildProfile.Release, CharacterTestWindowsBuildConfiguration.Parse(null));
            Assert.AreEqual(CharacterTestWindowsBuildProfile.Development, CharacterTestWindowsBuildConfiguration.Parse("development"));
            Assert.Throws<ArgumentException>(() => CharacterTestWindowsBuildConfiguration.Parse("Mono"));
            BuildOptions release = CharacterTestWindowsBuildConfiguration.Options(CharacterTestWindowsBuildProfile.Release);
            BuildOptions development = CharacterTestWindowsBuildConfiguration.Options(CharacterTestWindowsBuildProfile.Development);
            Assert.AreEqual(BuildOptions.CompressWithLz4HC, release);
            Assert.AreEqual(BuildOptions.Development | BuildOptions.CompressWithLz4, development);
            Assert.AreEqual(Il2CppCompilerConfiguration.Release,
                CharacterTestWindowsBuildConfiguration.Compiler(CharacterTestWindowsBuildProfile.Release));
            Assert.AreEqual(0, (int)(release & (BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler | BuildOptions.EnableDeepProfilingSupport)));
            var options = CharacterTestPackageTestCommand.ResolveOptionsFromCommandLine(new[]
            { CharacterTestWindowsBuildConfiguration.Argument, "Development" });
            Assert.AreEqual(CharacterTestWindowsBuildProfile.Development, options.Configuration);
        }

#if UNITY_6000_6_OR_NEWER
        [Test]
        public void BuildScope_SelectsAndRestoresManagedDiagnostics()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            ManagedCodeVariant original = PlayerSettings.GetManagedCodeVariant(target);
            try
            {
                PlayerSettings.SetManagedCodeVariant(target, ManagedCodeVariant.Instrumented);
                using (new CharacterTestWindowsBuildConfiguration.Scope(CharacterTestWindowsBuildProfile.Development))
                    Assert.AreEqual(ManagedCodeVariant.Checked, PlayerSettings.GetManagedCodeVariant(target));
                Assert.AreEqual(ManagedCodeVariant.Instrumented, PlayerSettings.GetManagedCodeVariant(target));

                using (new CharacterTestWindowsBuildConfiguration.Scope(CharacterTestWindowsBuildProfile.Release))
                    Assert.AreEqual(ManagedCodeVariant.Release, PlayerSettings.GetManagedCodeVariant(target));
                Assert.AreEqual(ManagedCodeVariant.Instrumented, PlayerSettings.GetManagedCodeVariant(target));
            }
            finally
            {
                PlayerSettings.SetManagedCodeVariant(target, original);
            }
        }
#endif

        [Test]
        public void SymbolArchive_PreservesRuntimeFilesAndRecordsMovedSymbols()
        {
            string root = Path.Combine(Path.GetTempPath(), "WgfSymbols-" + Guid.NewGuid().ToString("N"));
            string player = Path.Combine(root, "Player"), symbols = Path.Combine(root, "Symbols");
            try
            {
                Directory.CreateDirectory(player);
                File.WriteAllText(Path.Combine(player, "GameAssembly.dll"), "runtime");
                File.WriteAllText(Path.Combine(player, "GameAssembly.pdb"), "symbols");
                string backup = Path.Combine(player, "Game_BackUpThisFolder_ButDontShipItWithYourGame", "il2cppOutput");
                Directory.CreateDirectory(backup);
                File.WriteAllText(Path.Combine(backup, "Assembly.cpp"), "generated code");
                var result = CharacterTestWindowsBuildConfiguration.ArchiveSymbols(player, symbols);
                Assert.AreEqual(2, result.Count);
                Assert.IsTrue(File.Exists(Path.Combine(player, "GameAssembly.dll")));
                Assert.IsFalse(File.Exists(Path.Combine(player, "GameAssembly.pdb")));
                Assert.AreEqual("symbols", File.ReadAllText(Path.Combine(symbols, "GameAssembly.pdb")));
                Assert.AreEqual("generated code", File.ReadAllText(Path.Combine(symbols, "Il2CppSources/il2cppOutput/Assembly.cpp")));
                foreach (var entry in result)
                {
                    Assert.AreEqual(64, ((string)entry["sha256"]).Length);
                    Assert.IsTrue(File.Exists(Path.Combine(symbols, (string)entry["archivePath"])));
                }
                Assert.Throws<InvalidOperationException>(() =>
                    CharacterTestWindowsBuildConfiguration.ArchiveSymbols(player, Path.Combine(player, "Symbols")));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
