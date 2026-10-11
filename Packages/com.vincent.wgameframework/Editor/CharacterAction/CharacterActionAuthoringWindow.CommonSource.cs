using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private string _commonSourceBaselineHash = string.Empty;
        private string _commonProjectionBaselineHash = string.Empty;

        private static string CommonSourcePath => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "CharacterActionSource", "Libraries", "Common", "character_actions.json");
        private static string CommonProjectionPath => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Assets", "MxFrameworkGenerated", "CharacterActions", "Libraries", "Common", "character_actions.json");

        private static bool IsGeneratedCommonLibrary(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            return normalized.EndsWith(
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json",
                StringComparison.OrdinalIgnoreCase);
        }

        private void CaptureCommonSourceBaseline()
        {
            _commonSourceBaselineHash = File.Exists(CommonSourcePath)
                ? HashFile(CommonSourcePath) : string.Empty;
            _commonProjectionBaselineHash = File.Exists(CommonProjectionPath)
                ? HashFile(CommonProjectionPath) : string.Empty;
        }

        private CharacterActionAuthoringPreparedDocumentWrite[] PrepareCommonSourceWrites(
            CharacterActionAuthoringDocumentState state,
            CharacterActionAuthoringPreparedDocumentWrite[] projectionWrites)
        {
            if (projectionWrites == null || projectionWrites.Length != 1)
                throw new InvalidOperationException("Common library save must produce one projection.");
            string generatedPath = projectionWrites[0].Path;
            if (string.IsNullOrWhiteSpace(_commonSourceBaselineHash)
                || string.IsNullOrWhiteSpace(_commonProjectionBaselineHash)
                || !string.Equals(HashFile(CommonSourcePath), _commonSourceBaselineHash, StringComparison.Ordinal)
                || !string.Equals(HashFile(generatedPath), _commonProjectionBaselineHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Common source or projection changed externally; reload before saving. Draft kept.");

            string sourceCandidate = CharacterActionCommonSourceProjection.MergeChangedActions(
                File.ReadAllText(CommonSourcePath),
                state.BaselineCanonicalJson,
                projectionWrites[0].Contents);
            return new[]
            {
                new CharacterActionAuthoringPreparedDocumentWrite(
                    CommonSourcePath, sourceCandidate, _commonSourceBaselineHash),
                new CharacterActionAuthoringPreparedDocumentWrite(
                    generatedPath, projectionWrites[0].Contents, _commonProjectionBaselineHash),
            };
        }

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(stream));
        }
    }
}
