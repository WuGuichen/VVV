using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class CharacterTestAudioCatalogBuildPreflightTests
    {
        [Test]
        public void Validate_RejectsIncompleteCatalogPairBeforePlayerBuild()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-character-test-audio-preflight-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Audio"));
                File.WriteAllText(
                    Path.Combine(root, CharacterTestAudioCatalogBuildPreflight.GeneratedAudioRelativePath),
                    "{}");

                CharacterTestAudioCatalogPreflightResult result =
                    CharacterTestAudioCatalogBuildPreflight.Validate(root);

                Assert.IsFalse(result.Success);
                Assert.AreEqual("CHARACTER_TEST_AUDIO_CATALOG_PAIR_INCOMPLETE", result.Code);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Validate_AcceptsCompleteMirroredLinkedCatalogPair()
        {
            string root = CreateTempRoot();
            try
            {
                const string audioJson =
                    "{\"format\":\"mx.audioRuntimeCatalog.v1\",\"manifestHash\":\"audio-manifest\",\"snapshotHash\":\"snapshot\",\"buses\":[],\"events\":[],\"cues\":[]}";
                string physicalJson =
                    "{\"format\":\"mx.audioPhysicalRuntimeCatalog.v2\",\"schemaVersion\":2,\"physicalManifestHash\":\"physical-manifest\",\"audioCatalogHash\":\""
                    + ComputeTextSha256(audioJson)
                    + "\",\"materials\":[],\"materialPairs\":[],\"families\":[],\"basisSounds\":[],\"responseProfiles\":[]}";
                WriteCatalogPair(root, audioJson, physicalJson, audioJson, physicalJson);

                CharacterTestAudioCatalogPreflightResult result =
                    CharacterTestAudioCatalogBuildPreflight.Validate(root);

                Assert.IsTrue(result.Success, result.Code + ": " + result.Message);
                Assert.AreEqual("CHARACTER_TEST_AUDIO_CATALOG_GENERATED_LINKED", result.Code);
                Assert.AreEqual(ComputeTextSha256(audioJson), result.AudioCatalogHash);
                Assert.AreEqual("physical-manifest", result.PhysicalManifestHash);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Validate_RejectsGeneratedAndPlayerMirrorMismatch()
        {
            string root = CreateTempRoot();
            try
            {
                const string audioJson =
                    "{\"format\":\"mx.audioRuntimeCatalog.v1\",\"manifestHash\":\"audio-manifest\",\"snapshotHash\":\"snapshot\",\"buses\":[],\"events\":[],\"cues\":[]}";
                string physicalJson =
                    "{\"format\":\"mx.audioPhysicalRuntimeCatalog.v2\",\"schemaVersion\":2,\"physicalManifestHash\":\"physical-manifest\",\"audioCatalogHash\":\""
                    + ComputeTextSha256(audioJson)
                    + "\",\"materials\":[],\"materialPairs\":[],\"families\":[],\"basisSounds\":[],\"responseProfiles\":[]}";
                WriteCatalogPair(root, audioJson, physicalJson, audioJson + " ", physicalJson);

                CharacterTestAudioCatalogPreflightResult result =
                    CharacterTestAudioCatalogBuildPreflight.Validate(root);

                Assert.IsFalse(result.Success);
                Assert.AreEqual("CHARACTER_TEST_AUDIO_CATALOG_MIRROR_MISMATCH", result.Code);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Validate_CurrentProjectRequiresPublishedPhysicalPair()
        {
            CharacterTestAudioCatalogPreflightResult result =
                CharacterTestAudioCatalogBuildPreflight.Validate(Directory.GetCurrentDirectory());

            if (File.Exists(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CharacterTestAudioCatalogBuildPreflight.GeneratedPhysicalRelativePath)))
            {
                Assert.IsTrue(result.Success, result.Code + ": " + result.Message);
            }
            else
            {
                Assert.IsFalse(result.Success);
                Assert.AreEqual("CHARACTER_TEST_AUDIO_CATALOG_PAIR_INCOMPLETE", result.Code);
            }
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-character-test-audio-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteCatalogPair(
            string root,
            string generatedAudio,
            string generatedPhysical,
            string playerAudio,
            string playerPhysical)
        {
            Write(root, CharacterTestAudioCatalogBuildPreflight.GeneratedAudioRelativePath, generatedAudio);
            Write(root, CharacterTestAudioCatalogBuildPreflight.GeneratedPhysicalRelativePath, generatedPhysical);
            Write(root, CharacterTestAudioCatalogBuildPreflight.PlayerAudioRelativePath, playerAudio);
            Write(root, CharacterTestAudioCatalogBuildPreflight.PlayerPhysicalRelativePath, playerPhysical);
        }

        private static void Write(string root, string relativePath, string content)
        {
            string path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private static string ComputeTextSha256(string text)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(hash.Length * 2 + 7).Append("sha256:");
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }
    }
}
