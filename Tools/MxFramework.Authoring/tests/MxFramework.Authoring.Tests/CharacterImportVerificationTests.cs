using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class CharacterImportVerificationTests
{
    internal static void Run()
    {
        int failures = 0;
        foreach (var test in new (string Name, Action Run)[]
        {
            ("missing target fails report", () => VerifyReport(missing: true)),
            ("changed target fails report", () => VerifyReport(missing: false)),
            ("converted artifact uses output hash", VerifyArtifactHash),
            ("CLI failure preserves mapping, cleanup and summary", VerifyCliFailure)
        })
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception exception) { failures++; Console.WriteLine("FAIL " + test.Name + ": " + exception.Message); }
        }
        if (failures != 0) throw new InvalidOperationException("Import verification failures: " + failures);
        Console.WriteLine("Import verification: 4/4 passed.");
    }

    private static void VerifyReport(bool missing)
    {
        string root = NewRoot();
        string path = Path.Combine(root, "resource.bin");
        if (!missing) File.WriteAllText(path, "changed");
        var plan = new CharacterUnityImportWritePlan();
        plan.Writes.Add(new CharacterUnityImportWriteEntry
        {
            Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
            TargetPath = "resource.bin", ContentHash = CharacterPackageHashUtility.ComputeTextSha256("expected")
        });
        var report = ReadyReport();
        Verify(root, plan, report);
        Require(report.Status == "Failed" && report.ErrorCount == 1 && !report.CanSpawnAfterImport,
            "Write-after-verify must propagate failure and count its diagnostic.");
        Require(report.Issues.Single().Code == (missing
            ? "CHARPKG_IMPORT_VERIFY_FILE_MISSING" : "CHARPKG_IMPORT_VERIFY_HASH_MISMATCH"), "Wrong diagnostic.");
    }

    private static void VerifyArtifactHash()
    {
        string root = NewRoot();
        string path = Path.Combine(root, "resource.glb");
        byte[] json = System.Text.Encoding.UTF8.GetBytes(
            "{\"asset\":{\"version\":\"2.0\"},\"meshes\":[{\"primitives\":[{\"attributes\":{\"JOINTS_0\":0,\"WEIGHTS_0\":1,\"COLOR_0\":2}}]}]}");
        int paddedLength = (json.Length + 3) / 4 * 4;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write((uint)(20 + paddedLength));
        writer.Write((uint)paddedLength); writer.Write(0x4E4F534Au); writer.Write(json);
        for (int i = json.Length; i < paddedLength; i++) writer.Write((byte)' ');
        byte[] sourceBytes = stream.ToArray();
        string sourceHash = CharacterPackageHashUtility.ComputeSha256(sourceBytes);
        var input = new CharacterUnityImportWriteInput
        {
            Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
            SourcePath = "source.glb", TargetPath = "resource.glb", ContentHash = sourceHash
        };
        CharacterUnityImportGlbCompatibility.BuildResourceArtifact(input, sourceBytes, out byte[] artifact, out var info);
        Require(info.ArtifactStatus == "Generated" && info.ArtifactHash != sourceHash,
            "Fixture must exercise the real GLB compatibility transform.");
        File.WriteAllBytes(path, artifact);
        var plan = new CharacterUnityImportWritePlan();
        plan.Writes.Add(new CharacterUnityImportWriteEntry
        {
            Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile, SourcePath = "source.glb",
            TargetPath = "resource.glb", ContentHash = sourceHash
        });
        var report = ReadyReport();
        report.Operations.Add(new CharacterUnityImportOperation
        {
            SourcePath = "source.glb", TargetPath = "resource.glb", ArtifactStatus = info.ArtifactStatus,
            ArtifactValidationStatus = info.ValidationStatus, ArtifactHash = info.ArtifactHash
        });
        Verify(root, plan, report);
        Require(report.ErrorCount == 0 && report.Status == "Ready", "Valid conversion must remain accepted.");
        File.WriteAllText(path, "corrupted artifact");
        Verify(root, plan, report);
        Require(report.ErrorCount == 1 && report.Status == "Failed", "Converted output must still be verified.");
    }

    private static void VerifyCliFailure()
    {
        string repo = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(repo, "AGENTS.md")))
            repo = Path.GetDirectoryName(repo) ?? throw new InvalidOperationException("Repository root unavailable.");
        string root = NewRoot();
        string package = Path.Combine(root, "package");
        string sample = Path.Combine(repo, "Tools/MxFramework.Authoring/samples/character-codex-flow-test");
        foreach (string source in Directory.GetFiles(sample, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(package, Path.GetRelativePath(sample, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
        const string profileRelative = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        string profile = Path.Combine(root, profileRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        File.Copy(Path.Combine(repo, profileRelative), profile);
        foreach (string relative in new[]
        {
            "Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json",
            "Assets/MxFrameworkGenerated/Abilities/ability_catalog.json",
            "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json",
            "CharacterActionSource/Libraries/Common/character_actions.json",
            "WeaponSource/global_weapon_library.json"
        })
        {
            string target = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(repo, relative), target);
        }
        var options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        bool originalAutoRegister = CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests;
        CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests = true;
        try
        {
            string[] args = { "character", "import-unity", "--package", package, "--project-root", root,
                "--build-target", "StandaloneWindows64", "--check-files" };
            Require(CharacterPackageCommands.Dispatch(args, options) == MxFramework.Authoring.Cli.Program.ExitReady,
                "Normal import must succeed.");
            Require(CharacterPackageCommands.Dispatch(args, options) == MxFramework.Authoring.Cli.Program.ExitReady,
                "Repeated import must succeed.");
            string generated = Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test");
            string summary = Path.Combine(generated, "package_cache/compile_summary.json");
            string originalProfile = File.ReadAllText(profile);
            string originalSummary = File.ReadAllText(summary);
            string orphan = Path.Combine(generated, "resources/orphan.png");
            File.WriteAllText(orphan, "keep on failure");
            string catalogPath = Path.Combine(package, "resource_catalog.json");
            var catalog = JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(File.ReadAllText(catalogPath), options)!;
            var entry = catalog.Entries.First(x => x.RelativePath.EndsWith(".png", StringComparison.Ordinal));
            entry.Hash = "sha256:" + new string('0', 64);
            entry.Hashes.ContentHash = entry.Hash;
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, options));
            Require(CharacterPackageCommands.Dispatch(args, options) == MxFramework.Authoring.Cli.Program.ExitValidationBlocked,
                "Actual CLI must reject post-write hash mismatch.");
            var report = JsonSerializer.Deserialize<CharacterUnityImportReport>(
                File.ReadAllText(Path.Combine(generated, "package_cache/import_report.json")), options)!;
            Require(report.Status == "Failed" && report.ErrorCount > 0 && !report.CanSpawnAfterImport,
                "CLI failure must be persisted in its report.");
            Require(File.ReadAllText(profile) == originalProfile, "Failed import must preserve mapping and revision.");
            Require(File.ReadAllText(summary) == originalSummary, "Failed import must preserve success summary.");
            Require(File.Exists(orphan), "Failed import must skip success cleanup.");
            Require(!report.Operations.Any(x => x.Kind == "exportCleanup"), "Cleanup must not run on verification failure.");
        }
        finally { CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests = originalAutoRegister; }
    }

    private static CharacterUnityImportReport ReadyReport() => new()
        { Status = "Ready", CanWriteToUnityProject = true, CanSpawnAfterImport = true };

    private static string NewRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-import-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Verify(string root, CharacterUnityImportWritePlan plan, CharacterUnityImportReport report) =>
        typeof(CharacterPackageCommands).GetMethod("PerformWriteAfterVerify", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { root, plan, report });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
