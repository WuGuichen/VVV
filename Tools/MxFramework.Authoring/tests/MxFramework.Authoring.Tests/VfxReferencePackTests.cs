using System;
using System.IO;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class VfxReferencePackTests
{
    public static void RunAll()
    {
        MissingPath_IsRejected();
        Scan_IsStableAndContentChangesStaleHash();
        LicensedReusable_RequiresExplicitAuthorizationAndBuildsFingerprints();
    }

    private static void LicensedReusable_RequiresExplicitAuthorizationAndBuildsFingerprints()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-vfx-licensed-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets", "Piloto Studio", "Super Realistic FX Bundle", "ARPG_Realistic Essentials Fire");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.5.0f1\n");
        string prefab = Path.Combine(source, "Projectile Hit Impact.prefab");
        File.WriteAllText(prefab, "--- !u!1 &1\nGameObject:\n  m_Name: Flare\n--- !u!198 &2\nParticleSystem:\n--- !u!199 &3\nParticleSystemRenderer:\n");
        File.WriteAllText(prefab + ".meta", "guid: 0123456789abcdef0123456789abcdef\n");
        try
        {
            VfxReferencePackScanResult blocked = VfxReferencePackScanner.ScanPiloto(source, new VfxReferencePackScanOptions { UsagePolicy = VfxReferenceUsagePolicies.LicensedReusable });
            Require(blocked.HasErrors && HasCode(blocked, VfxReferencePackDiagnosticCodes.UsageViolation), "Reusable sources must not infer authorization.");
            var options = new VfxReferencePackScanOptions { UsagePolicy = VfxReferenceUsagePolicies.LicensedReusable, SourcePackageId = "piloto.test", LicenseId = "license.test", PublicRedistributionAllowed = true };
            VfxReferencePackScanResult result = VfxReferencePackScanner.ScanPiloto(source, options);
            Require(!result.HasErrors && result.Manifest.PublicRedistributionAllowed && result.Manifest.SourcePackageId == "piloto.test", "Explicit reusable authorization should be preserved.");
            Require(result.Manifest.Effects.Count == 1 && result.Manifest.Effects[0].CollectionId == "Fire" && result.Manifest.Effects[0].QualityTier == "Normal", "Licensed source semantics should classify collection and quality.");
            Require(result.Manifest.Effects[0].LayerRoles.Contains("Flash") && VfxCreativeFlywheelHash.IsSha256(result.Manifest.Effects[0].TechnicalFingerprint) && VfxCreativeFlywheelHash.IsSha256(result.Manifest.Effects[0].CreativeFingerprint), "Source effects should expose stable technical and creative fingerprints.");
            Require(VfxReferencePackScanner.ValidateManifest(result.Manifest, result.Manifest.ManifestHash, out _), "Licensed manifest should validate with explicit authorization evidence.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void MissingPath_IsRejected()
    {
        VfxReferencePackScanResult result = VfxReferencePackScanner.ScanPiloto(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Require(result.HasErrors && HasCode(result, VfxReferencePackDiagnosticCodes.SourceMissing), "Missing local reference pack must be rejected.");
    }

    private static void Scan_IsStableAndContentChangesStaleHash()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-vfx-reference-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets", "Piloto Studio", "ARPG_Realistic Demon VS Angels");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        File.WriteAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.61f1\n");
        string prefab = Path.Combine(source, "Hit Original.prefab");
        File.WriteAllText(prefab, "--- !u!1 &1\nGameObject:\n--- !u!198 &2\nParticleSystem:\n");
        try
        {
            VfxReferencePackScanResult first = VfxReferencePackScanner.ScanPiloto(source);
            VfxReferencePackScanResult second = VfxReferencePackScanner.ScanPiloto(source);
            Require(!first.HasErrors && first.Manifest != null, "Synthetic reference pack should scan without blocking errors.");
            Require(first.Manifest.ManifestHash == second.Manifest.ManifestHash, "Unchanged reference pack scan must be deterministic.");
            Require(first.Manifest.ReferencePackId == VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels && first.Manifest.UsagePolicy == VfxReferenceUsagePolicies.ReferenceOnly, "Manifest must enforce the approved logical id and ReferenceOnly policy.");
            Require(first.Manifest.Effects.Count == 1 && first.Manifest.Effects[0].SemanticRoles.Contains(VfxAuthoringRoles.Impact), "Hit reference should map to Impact semantics.");
            Require(VfxReferencePackScanner.ValidateManifest(first.Manifest, first.Manifest.ManifestHash, out _), "Fresh manifest should validate.");

            File.AppendAllText(prefab, "--- !u!198 &3\nParticleSystem:\n");
            VfxReferencePackScanResult changed = VfxReferencePackScanner.ScanPiloto(source);
            Require(changed.Manifest.ManifestHash != first.Manifest.ManifestHash, "Reference content changes must stale the manifest hash.");
            Require(!VfxReferencePackScanner.ValidateManifest(changed.Manifest, first.Manifest.ManifestHash, out _), "Old expected hash must reject changed reference evidence.");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static bool HasCode(VfxReferencePackScanResult result, string code) => result.Diagnostics.Exists(item => item.Code == code);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
