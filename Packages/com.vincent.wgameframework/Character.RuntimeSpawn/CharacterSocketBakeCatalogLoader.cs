using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>
    /// Directory-level entry point for the socket bake v3 pair. Runtime must use this instead of
    /// touching the two files directly, so path containment, size caps and hash verification cannot
    /// be skipped by a caller.
    /// </summary>
    public static class CharacterSocketBakeCatalogLoader
    {
        private const int LegacySniffBytes = 4096;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// Loads and fully verifies <c>generated/character_socket_bake_catalog.json</c> together with
        /// <c>generated/character_socket_bake_catalog.bytes</c> under <paramref name="packageRoot"/>.
        /// On failure returns false with a null catalog and a <c>default</c> evidence; the evidence is
        /// only meaningful when the return value is true.
        /// </summary>
        public static bool TryLoadFromDirectory(
            string packageRoot,
            string expectedPackageId,
            out CharacterSocketBakeCatalog catalog,
            out CharacterSocketBakeCatalogEvidence evidence,
            out string[] issues)
        {
            var diagnostics = new List<string>();
            catalog = null;
            evidence = default;
            try
            {
                if (string.IsNullOrWhiteSpace(packageRoot))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadMissing + ": package root is required.");
                    issues = diagnostics.ToArray();
                    return false;
                }
                if (string.IsNullOrEmpty(expectedPackageId))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.IdentityMismatch + ": an expected packageId is required.");
                    issues = diagnostics.ToArray();
                    return false;
                }

                string rootFull = Path.GetFullPath(packageRoot);
                string indexFull = ResolveContained(rootFull, CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath, diagnostics);
                string binaryFull = ResolveContained(rootFull, CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath, diagnostics);
                if (indexFull == null || binaryFull == null)
                {
                    issues = diagnostics.ToArray();
                    return false;
                }

                if (!File.Exists(indexFull))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadMissing + ": socket bake index is missing at "
                        + CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath + ".");
                    issues = diagnostics.ToArray();
                    return false;
                }
                if (IsReparsePoint(indexFull) || IsReparsePoint(binaryFull))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": socket bake files must not be symbolic links.");
                    issues = diagnostics.ToArray();
                    return false;
                }

                if (!TryReadIndexText(indexFull, expectedPackageId, diagnostics, out string indexJson))
                {
                    issues = diagnostics.ToArray();
                    return false;
                }

                // Legacy packages have no binary companion, including valid empty v2 catalogs.
                // Diagnose their format before testing for a companion required only by v3.
                if (CharacterSocketBakeCatalogBinary.IsLegacyFormat(indexJson))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.ReimportRequired
                        + ": package " + expectedPackageId + " carries legacy socket bake JSON and must be re-imported.");
                    issues = diagnostics.ToArray();
                    return false;
                }
                if (!File.Exists(binaryFull))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadMissing + ": socket bake binary payload is missing at "
                        + CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath + ".");
                    issues = diagnostics.ToArray();
                    return false;
                }

                long binaryLength = new FileInfo(binaryFull).Length;
                if (binaryLength > CharacterSocketBakeCatalogBinary.MaxBinaryBytes)
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.LimitExceeded + ": socket bake binary is " + binaryLength
                        + " bytes, above the " + CharacterSocketBakeCatalogBinary.MaxBinaryBytes + " byte cap.");
                    issues = diagnostics.ToArray();
                    return false;
                }
                if (binaryLength == 0)
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadMissing + ": socket bake binary is empty.");
                    issues = diagnostics.ToArray();
                    return false;
                }

                byte[] indexBytes = File.ReadAllBytes(indexFull);
                byte[] binary = File.ReadAllBytes(binaryFull);

                if (!CharacterSocketBakeCatalogBinary.TryLoad(indexJson, binary, expectedPackageId, out catalog, out string[] loadIssues))
                {
                    for (int i = 0; i < loadIssues.Length; i++)
                        diagnostics.Add(loadIssues[i]);
                    catalog = null;
                    issues = diagnostics.ToArray();
                    return false;
                }

                string indexHash = CharacterSocketBakeCatalogBinary.ComputeSha256(indexBytes);
                string binaryHash = CharacterSocketBakeCatalogBinary.ComputeSha256(binary);
                int trackCount = 0;
                int frameCount = 0;
                int basisFrameCount = 0;
                foreach (KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack> pair in catalog.EnumerateTracks())
                {
                    trackCount++;
                    frameCount = checked(frameCount + pair.Value.Frames.Count);
                    if (pair.Value.HasBasis)
                        basisFrameCount = checked(basisFrameCount + pair.Value.BasisFrames.Count);
                }

                evidence = new CharacterSocketBakeCatalogEvidence(
                    CharacterSocketBakeCatalogBinary.Format,
                    indexHash,
                    binaryHash,
                    binaryLength,
                    trackCount,
                    frameCount,
                    basisFrameCount);
                issues = diagnostics.ToArray();
                return true;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is OverflowException
                || ex is DecoderFallbackException)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": socket bake pair could not be read: " + ex.Message);
                catalog = null;
                evidence = default;
                issues = diagnostics.ToArray();
                return false;
            }
        }

        private static bool TryReadIndexText(
            string indexFull,
            string expectedPackageId,
            List<string> diagnostics,
            out string indexJson)
        {
            indexJson = null;
            long length = new FileInfo(indexFull).Length;
            if (length == 0)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadMissing + ": socket bake index is empty.");
                return false;
            }

            if (length > CharacterSocketBakeCatalogBinary.MaxIndexBytes)
            {
                // A legacy full JSON document blows the index cap. Sniff it so the caller gets
                // "re-import required" instead of a misleading size error.
                string prefix = ReadPrefix(indexFull, LegacySniffBytes);
                if (CharacterSocketBakeCatalogBinary.IsLegacyFormat(prefix))
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.ReimportRequired
                        + ": package " + expectedPackageId + " still carries the legacy full socket bake JSON ("
                        + length + " bytes) and must be re-imported.");
                }
                else
                {
                    diagnostics.Add(CharacterSocketBakeDiagnosticCodes.LimitExceeded + ": socket bake index is " + length
                        + " bytes, above the " + CharacterSocketBakeCatalogBinary.MaxIndexBytes + " byte cap.");
                }
                return false;
            }

            byte[] bytes = File.ReadAllBytes(indexFull);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": socket bake index must not carry a UTF-8 BOM.");
                return false;
            }
            try
            {
                indexJson = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid + ": socket bake index is not valid UTF-8.");
                return false;
            }
            return true;
        }

        private static string ReadPrefix(string path, int byteCount)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var buffer = new byte[byteCount];
                int read = stream.Read(buffer, 0, byteCount);
                if (read <= 0)
                    return string.Empty;
                return Encoding.ASCII.GetString(buffer, 0, read);
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private static string ResolveContained(string rootFull, string relativePath, List<string> diagnostics)
        {
            string combined = Path.GetFullPath(Path.Combine(rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;
            if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CharacterSocketBakeDiagnosticCodes.PayloadInvalid
                    + ": socket bake path '" + relativePath + "' escapes the package root.");
                return null;
            }
            return combined;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
