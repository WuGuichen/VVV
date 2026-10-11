using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using UnityEditor.Build;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct CharacterTestAudioCatalogPreflightResult
    {
        public CharacterTestAudioCatalogPreflightResult(
            bool success,
            string code,
            string message,
            string audioCatalogHash,
            string physicalManifestHash)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AudioCatalogHash = audioCatalogHash ?? string.Empty;
            PhysicalManifestHash = physicalManifestHash ?? string.Empty;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public string AudioCatalogHash { get; }
        public string PhysicalManifestHash { get; }
    }

    internal static class CharacterTestAudioCatalogBuildPreflight
    {
        internal const string GeneratedAudioRelativePath =
            "Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json";
        internal const string GeneratedPhysicalRelativePath =
            "Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json";
        internal const string PlayerAudioRelativePath =
            "Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json";
        internal const string PlayerPhysicalRelativePath =
            "Assets/StreamingAssets/MxFramework/Audio/audio_physical_runtime_catalog.json";

        /// <remarks>AllocByDesign. Player build preflight only.</remarks>
        internal static CharacterTestAudioCatalogPreflightResult Validate(string repositoryRoot)
        {
            string root = Path.GetFullPath(repositoryRoot ?? ".");
            string generatedAudioPath = Path.Combine(root, GeneratedAudioRelativePath);
            string generatedPhysicalPath = Path.Combine(root, GeneratedPhysicalRelativePath);
            string playerAudioPath = Path.Combine(root, PlayerAudioRelativePath);
            string playerPhysicalPath = Path.Combine(root, PlayerPhysicalRelativePath);
            if (!File.Exists(generatedAudioPath)
                || !File.Exists(generatedPhysicalPath)
                || !File.Exists(playerAudioPath)
                || !File.Exists(playerPhysicalPath))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_CATALOG_PAIR_INCOMPLETE",
                    "CharacterTest Player requires Generated and StreamingAssets Global/Physical audio catalog pairs. Publish Audio Workbench after all adoption, audition and coverage gates pass.");
            }

            string generatedAudioJson = File.ReadAllText(generatedAudioPath);
            string generatedPhysicalJson = File.ReadAllText(generatedPhysicalPath);
            string playerAudioJson = File.ReadAllText(playerAudioPath);
            string playerPhysicalJson = File.ReadAllText(playerPhysicalPath);
            if (!string.Equals(ComputeTextSha256(generatedAudioJson), ComputeTextSha256(playerAudioJson), StringComparison.Ordinal)
                || !string.Equals(ComputeTextSha256(generatedPhysicalJson), ComputeTextSha256(playerPhysicalJson), StringComparison.Ordinal))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_CATALOG_MIRROR_MISMATCH",
                    "Generated and StreamingAssets audio catalog mirrors are from different publications.");
            }

            if (!AudioRuntimeCatalogJson.TryLoad(
                    generatedAudioJson,
                    out AudioDefinitionCatalog audioCatalog,
                    out string audioError))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_GLOBAL_CATALOG_INVALID",
                    "Generated Global Audio Catalog is invalid: " + audioError);
            }
            if (!PhysicalAudioRuntimeCatalogJson.TryLoad(
                    generatedPhysicalJson,
                    out PhysicalAudioDefinitionCatalog physicalCatalog,
                    out string physicalError))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_PHYSICAL_CATALOG_INVALID",
                    "Generated Physical Audio Catalog is invalid: " + physicalError);
            }

            string audioCatalogHash = ComputeTextSha256(generatedAudioJson);
            if (!string.Equals(physicalCatalog.AudioCatalogHash, audioCatalogHash, StringComparison.Ordinal))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_CATALOG_HASH_MISMATCH",
                    "Physical Audio Catalog links " + physicalCatalog.AudioCatalogHash
                    + " but the current Global Audio Catalog hash is " + audioCatalogHash + ".");
            }
            if (!CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                    audioCatalog,
                    physicalCatalog,
                    out _,
                    out string linkError))
            {
                return Failed(
                    "CHARACTER_TEST_AUDIO_CATALOG_LINK_INVALID",
                    "CharacterTest cannot link the generated audio catalog pair: " + linkError);
            }

            return new CharacterTestAudioCatalogPreflightResult(
                true,
                "CHARACTER_TEST_AUDIO_CATALOG_GENERATED_LINKED",
                "Generated and Player audio catalog pairs are complete, mirrored and linked.",
                audioCatalogHash,
                physicalCatalog.PhysicalManifestHash);
        }

        /// <remarks>AllocByDesign. Player build preflight only.</remarks>
        internal static void ThrowIfInvalid(string repositoryRoot)
        {
            CharacterTestAudioCatalogPreflightResult result = Validate(repositoryRoot);
            if (!result.Success)
                throw new BuildFailedException(result.Code + ": " + result.Message);
        }

        private static CharacterTestAudioCatalogPreflightResult Failed(string code, string message)
        {
            return new CharacterTestAudioCatalogPreflightResult(false, code, message, string.Empty, string.Empty);
        }

        private static string ComputeTextSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            byte[] hash = SHA256.Create().ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2 + 7).Append("sha256:");
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }
    }

}
