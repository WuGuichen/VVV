using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class ProjectileAuthoringTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static void RunAll()
    {
        M1T01_T07_WorkspaceInventoryAndReferences();
        M1T02_T03_NoopApplyPreservesSourceBytesAndSemantics();
        M1T04_PresentationProfileRoundTripsFromFixture();
        M1T08_T10_CompileIsScopedDeterministicAndBinaryValid();
        M1T11_UnknownFieldIsPreservedOrBlocked();
        M1T12_HashConflictWritesNothing();
        M1T12_CommitRechecksExpectedHash();
        M1T13_ValidationFailureWritesNothing();
        M1T14_InjectedFailureRollsBackEverything();
        M1T14_RollbackFailureRetainsRecoveryBackup();
        M1T14_RollbackPreservesPostCommitExternalChange();
        M1T15_ApplyDoesNotTouchUnrelatedPackageFiles();
        M1T16_CliHelpArgumentsAndDryRun();
        M7A04_ApplySynchronizesPackageUnityAndPlayerInOneTransaction();
        M7A02_A03_UnityImportUpdatesMirrorAtomicallyAndRollsBackBothRoots();
        ProjectileAuthoringM2Tests.RunAll();
        ProjectileAuthoringM3Tests.RunAll();
        ProjectileAuthoringM4Tests.RunAll();
    }

    public static void RunSynchronizedApply()
    {
        M7A04_ApplySynchronizesPackageUnityAndPlayerInOneTransaction();
    }

    private static void M7A02_A03_UnityImportUpdatesMirrorAtomicallyAndRollsBackBothRoots()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            string generated = Path.Combine(temp, "Assets", "MxFrameworkGenerated", "Combat", "Projectiles");
            string mirror = Path.Combine(temp, "Assets", "StreamingAssets", "MxFramework", "CharacterTest", "RuntimeCatalogs", "Combat", "Projectiles");
            Directory.CreateDirectory(generated);
            Directory.CreateDirectory(mirror);
            File.WriteAllText(Path.Combine(generated, "projectile_definitions.json"), "generated-before");
            File.WriteAllText(Path.Combine(mirror, "projectile_definitions.json"), "mirror-before");

            string[] args =
            {
                "projectile", "import-unity",
                "--package", package,
                "--project-root", temp,
                "--vfx-definitions", Path.Combine(RepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json"),
            };
            int failed = MxFramework.Authoring.Cli.ProjectilePackageCommands.ImportUnityForTests(args, Options, new FailAfterSeven());
            Require(failed == MxFramework.Authoring.Cli.Program.ExitToolError, "M7-A03: injected cross-root import should fail.");
            Require(File.ReadAllText(Path.Combine(generated, "projectile_definitions.json")) == "generated-before",
                "M7-A03: generated root was not rolled back.");
            Require(File.ReadAllText(Path.Combine(mirror, "projectile_definitions.json")) == "mirror-before",
                "M7-A03: Player mirror root was not rolled back.");
            Require(Directory.GetFiles(generated).Length == 1 && Directory.GetFiles(mirror).Length == 1,
                "M7-A03: failed import left partial artifacts in either root.");

            int ready = MxFramework.Authoring.Cli.ProjectilePackageCommands.ImportUnityForTests(args, Options, null);
            Require(ready == MxFramework.Authoring.Cli.Program.ExitReady, "M7-A02: atomic generated + mirror import should succeed.");
            Dictionary<string, string> generatedHashes = HashFiles(generated);
            Dictionary<string, string> mirrorHashes = HashFiles(mirror);
            Require(generatedHashes.Count == 6 && EqualHashes(generatedHashes, mirrorHashes),
                "M7-A02: generated JSON/bytes and Player mirror must be byte-identical.");

            string[] overlappingArgs =
            {
                "projectile", "import-unity",
                "--package", package,
                "--project-root", temp,
                "--vfx-definitions", Path.Combine(RepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json"),
                "--projectile-root", "Assets/SameRoot",
                "--player-mirror-root", "Assets/SameRoot/NestedMirror",
            };
            int overlap = MxFramework.Authoring.Cli.ProjectilePackageCommands.ImportUnityForTests(
                overlappingArgs, Options, null);
            Require(overlap == MxFramework.Authoring.Cli.Program.ExitToolError,
                "M7-A02: overlapping generated and Player mirror roots must fail before transaction creation.");
            Require(!Directory.Exists(Path.Combine(temp, "Assets", "SameRoot")),
                "M7-A02: overlap rejection must not create or write either root.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M7A04_ApplySynchronizesPackageUnityAndPlayerInOneTransaction()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            string packageGenerated = Path.Combine(package, "generated");
            DeleteTemp(packageGenerated);
            string unityGenerated = Path.Combine(temp, "Assets", "MxFrameworkGenerated", "Combat", "Projectiles");
            string playerMirror = Path.Combine(temp, "Assets", "StreamingAssets", "MxFramework", "CharacterTest", "RuntimeCatalogs", "Combat", "Projectiles");
            Directory.CreateDirectory(unityGenerated);
            Directory.CreateDirectory(playerMirror);
            File.WriteAllText(Path.Combine(unityGenerated, "projectile_definitions.json"), "unity-before");
            File.WriteAllText(Path.Combine(playerMirror, "projectile_definitions.json"), "player-before");

            string relative = "config/projectiles/character_test.fireball_burst.projectile.json";
            string sourcePath = Path.Combine(package, relative.Replace('/', Path.DirectorySeparatorChar));
            byte[] originalSource = File.ReadAllBytes(sourcePath);
            JsonObject proposal = JsonNode.Parse(originalSource).AsObject();
            proposal["presentationBinding"]["viewNamePrefix"] = "SynchronizedProjectileVFX";
            ProjectileAuthoringDraftBundle bundle = Bundle(
                relative,
                ProjectileAuthoringHashes.ComputeSha256(originalSource),
                proposal);
            string draft = Path.Combine(temp, "draft.json");
            File.WriteAllText(draft, JsonSerializer.Serialize(bundle, Options));
            string vfxDefinitions = Path.Combine(RepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json");
            string[] args =
            {
                "projectile", "apply",
                "--package", package,
                "--draft", draft,
                "--out", packageGenerated,
                "--project-root", temp,
                "--vfx-definitions", vfxDefinitions,
            };

            int failed = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(args, Options, new FailAfterTen());
            Require(failed == MxFramework.Authoring.Cli.Program.ExitToolError, "M7-A04: injected synchronized apply should fail.");
            Require(originalSource.SequenceEqual(File.ReadAllBytes(sourcePath)), "M7-A04: failed synchronized apply did not roll back source.");
            Require(!Directory.Exists(packageGenerated) || Directory.GetFiles(packageGenerated).Length == 0, "M7-A04: failed synchronized apply left package artifacts.");
            Require(File.ReadAllText(Path.Combine(unityGenerated, "projectile_definitions.json")) == "unity-before", "M7-A04: failed synchronized apply did not roll back Unity artifacts.");
            Require(File.ReadAllText(Path.Combine(playerMirror, "projectile_definitions.json")) == "player-before", "M7-A04: failed synchronized apply changed Player artifacts.");

            int ready = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(args, Options, null);
            Require(ready == MxFramework.Authoring.Cli.Program.ExitReady, "M7-A04: synchronized apply should succeed.");
            Require(File.ReadAllText(sourcePath).Contains("SynchronizedProjectileVFX", StringComparison.Ordinal), "M7-A04: synchronized apply did not commit source.");
            Dictionary<string, string> packageHashes = HashFiles(
                packageGenerated,
                path => !string.Equals(Path.GetFileName(path), "projectile_compile_report.json", StringComparison.Ordinal));
            Dictionary<string, string> unityHashes = HashFiles(unityGenerated);
            Dictionary<string, string> playerHashes = HashFiles(playerMirror);
            Require(packageHashes.Count == 6 && EqualHashes(packageHashes, unityHashes) && EqualHashes(packageHashes, playerHashes),
                "M7-A04: package, Unity and Player runtime artifacts must have identical hashes.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T01_T07_WorkspaceInventoryAndReferences()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        Require(!workspace.HasBlockingIssues, "M1-T01: sample workspace should be ready.");
        Require(workspace.SourceDocuments.Count == 29, "M1-T01: expected 29 source documents.");
        Require(workspace.ReferenceIndex.SourceDocumentsByStableId.Count == 29, "M1-T01: expected 29 source identities.");
        Require(workspace.ReferenceIndex.RequestsByStableId.Count == 28, "M1-T01: expected 28 request identities.");
        Require(workspace.ReferenceIndex.DefinitionsByStableId.Count == 23, "M1-T01: expected 23 definitions.");
        Require(workspace.ReferenceIndex.SpawnProfilesByStableId.Count == 28, "M1-T01: expected 28 spawn profiles.");
        Require(workspace.ReferenceIndex.BindingsByDefinitionId.Count == 23, "M1-T01: expected 23 bindings.");

        ProjectileAuthoringCompileResult first = workspace.Compile();
        Require(!first.HasBlockingIssues, "M1-T03: sample should compile.");
        ProjectileDefinitionConfig burst = first.Definitions.Definitions.Find(value => value.StableId == "character_test.fireball_burst.projectile");
        ProjectileDefinitionConfig explosion = first.Definitions.Definitions.Find(value => value.StableId == "character_test.fireball_explosion.projectile");
        Require(burst?.Impact?.EffectTimelineLabel == "character_test.double_explosion", "M1-T04: Fireball Burst effect timeline was lost.");
        Require(explosion?.Impact?.EffectTimelineLabel == "character_test.fireball_explosion.afterblast", "M1-T04: Fireball Explosion effect timeline was lost.");
        Require(explosion.Impact.AreaHitEvent?.Damage != null, "M1-T05: explicit area damage was lost.");
        Require(explosion.Impact.AreaHitEvent.Damage.AnalysisCategoryLabel == "FireballExplosionImpact", "M1-T05: explicit area damage payload changed.");

        IReadOnlyList<string> meteorRequests = workspace.ReferenceIndex.RequestIdsByDefinitionStableId["character_test.meteor_barrage.projectile"];
        Require(meteorRequests.Count == 6, "M1-T06: Meteor should have six request aliases.");
        Require(!workspace.ReferenceIndex.RequestsByStableId.ContainsKey("character_test.meteor_barrage.projectile"), "M1-T06: Meteor canonical source must not be indexed as a request.");
        Require(meteorRequests.Contains("character_test.meteor_barrage.projectile.0"), "M1-T06: Meteor alias .0 missing.");
        Require(workspace.ReferenceIndex.SpawnProfilesByStableId["character_test.arc_mine.deploy"].ProjectileStableId == "character_test.arc_mine.projectile", "M1-T07: Arc Mine request/definition identity was conflated.");
    }

    private static void M1T02_T03_NoopApplyPreservesSourceBytesAndSemantics()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            Dictionary<string, string> before = HashFiles(Path.Combine(package, "config", "projectiles"));
            string baseline = Path.Combine(temp, "baseline");
            Require(InvokeCli("projectile", "compile", "--package", package, "--out", baseline) == 0, "M1-T02: baseline compile failed.");
            string draft = Path.Combine(temp, "draft.json");
            using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(package))
                File.WriteAllText(draft, JsonSerializer.Serialize(FullProposalBundle(workspace), Options));
            string output = Path.Combine(temp, "generated");
            int code = InvokeCli("projectile", "apply", "--package", package, "--draft", draft, "--out", output);
            Require(code == 0, "M1-T02: no-op apply should succeed.");
            Dictionary<string, string> after = HashFiles(Path.Combine(package, "config", "projectiles"));
            Require(EqualHashes(before, after), "M1-T02: no-op apply changed source bytes.");
            Require(EqualRuntimeArtifactHashes(baseline, output), "M1-T03: full no-op proposal changed runtime artifact hashes.");
            using ProjectileAuthoringWorkspace reloaded = LoadWorkspace(package);
            Require(reloaded.SourceDocuments.Count == 29 && !reloaded.HasBlockingIssues, "M1-T03: no-op reload changed source semantics.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T04_PresentationProfileRoundTripsFromFixture()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            string relative = "config/projectiles/character_test.fireball_burst.projectile.json";
            string path = Path.Combine(package, relative.Replace('/', Path.DirectorySeparatorChar));
            JsonObject root = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            root["definition"]["impact"]["presentationProfileId"] = "character_test.fireball_burst.impact";
            File.WriteAllText(path, root.ToJsonString(Options));
            string before = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path));
            string draft = Path.Combine(temp, "draft.json");
            using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(package))
                File.WriteAllText(draft, JsonSerializer.Serialize(FullProposalBundle(workspace), Options));
            string output = Path.Combine(temp, "generated");
            Require(InvokeCli("projectile", "apply", "--package", package, "--draft", draft, "--out", output) == 0, "M1-T04: presentation profile fixture apply failed.");
            Require(ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path)) == before, "M1-T04: fixture source bytes changed on semantic no-op.");
            using ProjectileAuthoringWorkspace reloaded = LoadWorkspace(package);
            ProjectileDefinitionConfig definition = reloaded.Compile().Definitions.Definitions.Find(value => value.StableId == "character_test.fireball_burst.projectile");
            Require(definition.Impact.EffectTimelineLabel == "character_test.double_explosion", "M1-T04: effectTimelineId was lost in fixture round trip.");
            Require(definition.Impact.PresentationProfileLabel == "character_test.fireball_burst.impact", "M1-T04: presentationProfileId was lost in fixture round trip.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T08_T10_CompileIsScopedDeterministicAndBinaryValid()
    {
        string temp = NewTemp();
        try
        {
            string first = Path.Combine(temp, "first");
            string second = Path.Combine(temp, "second");
            Require(InvokeCli("projectile", "compile", "--package", SamplePath(), "--out", first) == 0, "M1-T08: compile failed.");
            Require(InvokeCli("projectile", "compile", "--package", SamplePath(), "--out", second) == 0, "M1-T09: repeat compile failed.");
            string[] files = Directory.GetFiles(first).Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Require(files.Length == 7, "M1-T08: compile must write six runtime artifacts and one report.");
            Require(files.All(value => value.StartsWith("projectile_", StringComparison.Ordinal)), "M1-T08: compile wrote a non-projectile artifact.");
            Dictionary<string, string> firstHashes = HashFiles(first);
            Dictionary<string, string> secondHashes = HashFiles(second);
            Require(EqualHashes(firstHashes, secondHashes), "M1-T09: deterministic compile hashes differ.");
            using JsonDocument definitions = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(first, "projectile_definitions.json")));
            using JsonDocument spawns = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(first, "projectile_spawn_profiles.json")));
            using JsonDocument bindings = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(first, "projectile_presentation_bindings.json")));
            Require(definitions.RootElement.GetProperty("schemaVersion").GetString() == "5", "M1-T08: definition schema must be 5.");
            Require(spawns.RootElement.GetProperty("schemaVersion").GetString() == "2", "M1-T08: spawn schema must be 2.");
            Require(bindings.RootElement.GetProperty("schemaVersion").GetString() == "2", "M4-C08: binding schema must be 2.");
            Require(new FileInfo(Path.Combine(first, "projectile_definitions.bytes")).Length > 32, "M1-T10: definitions binary is empty.");
            Require(new FileInfo(Path.Combine(first, "projectile_spawn_profiles.bytes")).Length > 32, "M1-T10: spawns binary is empty.");
            Require(new FileInfo(Path.Combine(first, "projectile_presentation_bindings.bytes")).Length > 32, "M1-T10: bindings binary is empty.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T11_UnknownFieldIsPreservedOrBlocked()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            string path = Path.Combine(package, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject root = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            root["futureField"] = new JsonObject { ["nested"] = 42 };
            File.WriteAllText(path, root.ToJsonString(Options));
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(package);
            ProjectileAuthoringSourceDocument source = workspace.SourceDocuments.First(value => value.SourceStableId == "character_test.fireball.projectile");
            JsonObject removed = JsonNode.Parse(source.PreservedJson.RootElement.GetRawText()).AsObject();
            removed.Remove("futureField");
            using JsonDocument removedDocument = JsonDocument.Parse(removed.ToJsonString(Options));
            Require(!workspace.TryBuildLosslessProposal(source, removedDocument.RootElement, out _, out ProjectileAuthoringDiagnostic removedDiagnostic), "M1-T11: unknown field removal should be blocked.");
            Require(removedDiagnostic.Code == "PROJAUTH005", "M1-T11: expected removal diagnostic.");

            JsonObject modifiedUnknown = JsonNode.Parse(source.PreservedJson.RootElement.GetRawText()).AsObject();
            modifiedUnknown["futureField"]["nested"] = 43;
            using JsonDocument modifiedUnknownDocument = JsonDocument.Parse(modifiedUnknown.ToJsonString(Options));
            Require(!workspace.TryBuildLosslessProposal(source, modifiedUnknownDocument.RootElement, out _, out ProjectileAuthoringDiagnostic changedDiagnostic), "M1-T11: unknown field value change should be blocked.");
            Require(changedDiagnostic.Code == "PROJAUTH005", "M1-T11: expected unknown value diagnostic.");

            JsonObject modifiedKnown = JsonNode.Parse(source.PreservedJson.RootElement.GetRawText()).AsObject();
            modifiedKnown["definition"]["motion"]["speedPerFrame"] = 0.5;
            using JsonDocument modifiedKnownDocument = JsonDocument.Parse(modifiedKnown.ToJsonString(Options));
            Require(workspace.TryBuildLosslessProposal(source, modifiedKnownDocument.RootElement, out byte[] changedBytes, out _), "M1-T11: known field change should be accepted.");
            Require(!changedBytes.SequenceEqual(source.OriginalUtf8Bytes), "M1-T11: known field change should produce changed bytes.");
            Require(source.PreservedJson.RootElement.GetProperty("futureField").GetProperty("nested").GetInt32() == 42, "M1-T11: preserved tree lost unknown field.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T12_HashConflictWritesNothing()
    {
        string temp = NewTemp();
        try
        {
            string first = Path.Combine(temp, "source.json");
            string second = Path.Combine(temp, "projectile_definitions.json");
            string third = Path.Combine(temp, "projectile_definitions.bytes");
            File.WriteAllText(first, "before-source");
            File.WriteAllText(second, "before-json");
            File.WriteAllBytes(third, new byte[] { 1, 2, 3 });
            var items = new[]
            {
                Item("source.json", first, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(first)), "after-source"),
                Item("projectile_definitions.json", second, "sha256:wrong", "after-json"),
                Item("projectile_definitions.bytes", third, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(third)), "after-bytes"),
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items);
            Require(receipt.Status == "Blocked" && receipt.DiagnosticCodes.Contains("PROJAUTH006"), "M1-T12: expected hash conflict.");
            Require(File.ReadAllText(first) == "before-source" && File.ReadAllText(second) == "before-json" && File.ReadAllBytes(third).SequenceEqual(new byte[] { 1, 2, 3 }), "M1-T12: conflict changed source, JSON, or bytes.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T12_CommitRechecksExpectedHash()
    {
        string temp = NewTemp();
        try
        {
            string first = Path.Combine(temp, "source.json");
            string second = Path.Combine(temp, "projectile_definitions.json");
            File.WriteAllText(first, "before-source");
            File.WriteAllText(second, "before-generated");
            var items = new[]
            {
                Item("source.json", first, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(first)), "after-source"),
                Item("projectile_definitions.json", second, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(second)), "after-generated"),
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items, new RewriteSecondBeforeCommit(second));
            Require(receipt.Status == "Blocked" && receipt.RollbackResult == "Succeeded" && receipt.DiagnosticCodes.Contains("PROJAUTH006"), "M1-T12: commit-time conflict should block and roll back.");
            Require(File.ReadAllText(first) == "before-source", "M1-T12: prior committed source was not rolled back after commit-time conflict.");
            Require(File.ReadAllText(second) == "external-change", "M1-T12: transaction overwrote the external change.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T13_ValidationFailureWritesNothing()
    {
        string temp = NewTemp();
        try
        {
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            string relative = "config/projectiles/character_test.fireball.projectile.json";
            string sourcePath = Path.Combine(package, relative.Replace('/', Path.DirectorySeparatorChar));
            byte[] original = File.ReadAllBytes(sourcePath);
            JsonObject proposal = JsonNode.Parse(original).AsObject();
            proposal["definition"]["stableId"] = "missing.definition";
            ProjectileAuthoringDraftBundle bundle = Bundle(relative, ProjectileAuthoringHashes.ComputeSha256(original), proposal);
            string draft = Path.Combine(temp, "draft.json");
            File.WriteAllText(draft, JsonSerializer.Serialize(bundle, Options));
            string output = Path.Combine(temp, "generated");
            Require(InvokeCli("projectile", "apply", "--package", package, "--draft", draft, "--out", output) == 2, "M1-T13: invalid proposal should return validation blocked.");
            Require(original.SequenceEqual(File.ReadAllBytes(sourcePath)), "M1-T13: validation failure changed source.");
            Require(!Directory.Exists(output), "M1-T13: validation failure wrote artifacts.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T14_InjectedFailureRollsBackEverything()
    {
        string temp = NewTemp();
        try
        {
            string existing = Path.Combine(temp, "source.json");
            string existingJson = Path.Combine(temp, "projectile_definitions.json");
            string created = Path.Combine(temp, "projectile_definitions.bytes");
            string final = Path.Combine(temp, "projectile_compile_report.json");
            File.WriteAllText(existing, "before-source");
            File.WriteAllText(existingJson, "before-json");
            string callerTargetPath = Path.Combine(temp, ".", "source.json");
            var items = new[]
            {
                Item("source.json", callerTargetPath, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(existing)), "after-source"),
                Item("projectile_definitions.json", existingJson, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(existingJson)), "after-json"),
                Item("projectile_definitions.bytes", created, ProjectileAuthoringHashes.Missing, "new-bytes"),
                Item("projectile_compile_report.json", final, ProjectileAuthoringHashes.Missing, "new-report"),
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items, new FailAfterThree());
            Require(receipt.Status == "RolledBack" && receipt.RollbackResult == "Succeeded", "M1-T14: rollback receipt is incorrect.");
            Require(File.ReadAllText(existing) == "before-source" && File.ReadAllText(existingJson) == "before-json" && !File.Exists(created) && !File.Exists(final), "M1-T14: rollback did not restore source, JSON, and bytes targets.");
            Require(items[0].TargetPath == callerTargetPath, "M1-T14: transaction mutated the caller-owned target path.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T14_RollbackFailureRetainsRecoveryBackup()
    {
        string temp = NewTemp();
        try
        {
            string existing = Path.Combine(temp, "source.json");
            string blocker = Path.Combine(temp, "blocker.json");
            File.WriteAllText(existing, "original-source");
            File.WriteAllText(blocker, "original-blocker");
            var items = new[]
            {
                Item("source.json", existing, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(existing)), "new-source"),
                Item("blocker.json", blocker, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(blocker)), "new-blocker"),
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items, new FailCommitAndRollback());
            Require(receipt.Status == "RecoveryRequired" && receipt.RollbackResult == "Failed", "M1-T14: rollback failure should require recovery.");
            Require(receipt.RecoveryFiles.Count == 1 && !Path.IsPathRooted(receipt.RecoveryFiles[0]), "M1-T14: receipt must expose one relative recovery path.");
            string[] backups = Directory.GetFiles(temp, "*.backup", SearchOption.TopDirectoryOnly);
            Require(backups.Length == 1, "M1-T14: original backup must be retained after rollback failure.");
            Require(File.ReadAllText(existing) == "new-source" && File.ReadAllText(backups[0]) == "original-source", "M1-T14: current data and recovery backup must not both be lost.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T14_RollbackPreservesPostCommitExternalChange()
    {
        string temp = NewTemp();
        try
        {
            string first = Path.Combine(temp, "source.json");
            string second = Path.Combine(temp, "generated.json");
            File.WriteAllText(first, "original-source");
            File.WriteAllText(second, "original-generated");
            var items = new[]
            {
                Item("source.json", first, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(first)), "transaction-source"),
                Item("generated.json", second, ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(second)), "transaction-generated"),
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items, new RewriteCommittedThenFail(first));
            Require(receipt.Status == "RecoveryRequired" && receipt.RollbackResult == "Failed", "M1-T14: post-commit external change must require recovery.");
            Require(File.ReadAllText(first) == "external-after-commit", "M1-T14: rollback overwrote the post-commit external change.");
            Require(File.ReadAllText(second) == "original-generated", "M1-T14: uncommitted second target changed.");
            string[] backups = Directory.GetFiles(temp, "*.backup", SearchOption.TopDirectoryOnly);
            Require(backups.Length == 1 && File.ReadAllText(backups[0]) == "original-source", "M1-T14: original backup was not retained after external rewrite conflict.");
            Require(receipt.RecoveryFiles.Count == 1 && !Path.IsPathRooted(receipt.RecoveryFiles[0]), "M1-T14: recovery receipt must contain the relative original backup path.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T15_ApplyDoesNotTouchUnrelatedPackageFiles()
    {
        string temp = NewTemp();
        try
        {
            string repo = RepoRoot();
            string[] protectedRoots =
            {
                Path.Combine(repo, "Tools", "MxFramework.Authoring", "samples", "ability-character-test", "config", "projectiles"),
                Path.Combine(repo, "Assets", "MxFrameworkGenerated", "Combat", "Projectiles"),
                Path.Combine(repo, "Assets", "StreamingAssets", "MxFramework", "CharacterTest", "RuntimeCatalogs", "Combat", "Projectiles"),
            };
            string[] forbiddenImplementationPaths =
            {
                "Packages/com.vincent.wgameframework/Editor/CharacterAction",
                "Packages/com.vincent.wgameframework/Runtime",
                "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/VfxAuthoring",
            };
            Dictionary<string, string> protectedBefore = HashScopes(protectedRoots);
            string forbiddenStatusBefore = GitScopedStatus(repo, forbiddenImplementationPaths);
            string package = Path.Combine(temp, "package");
            CopyDirectory(SamplePath(), package);
            Dictionary<string, string> before = HashFiles(package, path => !path.Replace('\\', '/').Contains("/config/projectiles/", StringComparison.Ordinal));
            string draft = Path.Combine(temp, "draft.json");
            File.WriteAllText(draft, JsonSerializer.Serialize(new ProjectileAuthoringDraftBundle(), Options));
            string generated = Path.Combine(temp, "generated");
            Require(InvokeCli("projectile", "apply", "--package", package, "--draft", draft, "--out", generated) == 0, "M1-T15: scoped apply failed.");
            Dictionary<string, string> after = HashFiles(package, path => !path.Replace('\\', '/').Contains("/config/projectiles/", StringComparison.Ordinal));
            Require(EqualHashes(before, after), "M1-T15: apply changed unrelated package files.");
            Require(EqualHashes(protectedBefore, HashScopes(protectedRoots)), "M1-T15: apply changed real sample or exact projectile generated/mirror files.");
            Require(forbiddenStatusBefore == GitScopedStatus(repo, forbiddenImplementationPaths), "M1-T15: apply changed CharacterAction, Runtime, or VFX implementation status.");
            Require(Directory.GetFiles(generated).All(path => Path.GetFileName(path).StartsWith("projectile_", StringComparison.Ordinal)), "M1-T15: generated output escaped projectile scope.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static void M1T16_CliHelpArgumentsAndDryRun()
    {
        Require(InvokeCli("projectile", "--help") == 0, "M1-T16: help should succeed.");
        Require(InvokeCli("projectile", "compile", "--package", SamplePath()) == 1, "M1-T16: missing --out should be a tool error.");
        string temp = NewTemp();
        try
        {
            string project = Path.Combine(temp, "project");
            Directory.CreateDirectory(project);
            Require(InvokeCli("projectile", "import-unity", "--package", SamplePath(), "--project-root", project, "--dry-run") == 0, "M1-T16: dry-run should succeed.");
            Require(Directory.GetFiles(project, "*", SearchOption.AllDirectories).Length == 0, "M1-T16: dry-run wrote Unity files.");
        }
        finally
        {
            DeleteTemp(temp);
        }
    }

    private static ProjectileAuthoringDraftBundle Bundle(string relative, string hash, JsonObject proposal)
    {
        using JsonDocument document = JsonDocument.Parse(proposal.ToJsonString(Options));
        var bundle = new ProjectileAuthoringDraftBundle();
        bundle.Documents.Add(new ProjectileAuthoringDraftDocument
        {
            SourceRelativePath = relative,
            ExpectedSha256 = hash,
            Json = document.RootElement.Clone(),
        });
        return bundle;
    }

    private static ProjectileAuthoringDraftBundle FullProposalBundle(ProjectileAuthoringWorkspace workspace)
    {
        var bundle = new ProjectileAuthoringDraftBundle();
        for (int i = 0; i < workspace.SourceDocuments.Count; i++)
        {
            ProjectileAuthoringSourceDocument source = workspace.SourceDocuments[i];
            bundle.Documents.Add(new ProjectileAuthoringDraftDocument
            {
                SourceRelativePath = source.RelativePath,
                ExpectedSha256 = source.Sha256,
                Json = source.PreservedJson.RootElement.Clone(),
            });
        }
        return bundle;
    }

    private static bool EqualRuntimeArtifactHashes(string left, string right)
    {
        string[] names =
        {
            "projectile_definitions.json",
            "projectile_definitions.bytes",
            "projectile_spawn_profiles.json",
            "projectile_spawn_profiles.bytes",
            "projectile_presentation_bindings.json",
            "projectile_presentation_bindings.bytes",
        };
        for (int i = 0; i < names.Length; i++)
        {
            string leftHash = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(Path.Combine(left, names[i])));
            string rightHash = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(Path.Combine(right, names[i])));
            if (!string.Equals(leftHash, rightHash, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static ProjectileAuthoringTransactionItem Item(string receipt, string target, string expected, string content)
    {
        return new ProjectileAuthoringTransactionItem
        {
            ReceiptPath = receipt,
            TargetPath = target,
            ExpectedSha256 = expected,
            Content = System.Text.Encoding.UTF8.GetBytes(content),
        };
    }

    private static int InvokeCli(params string[] args)
    {
        if (args.Length > 1
            && string.Equals(args[0], "projectile", StringComparison.Ordinal)
            && !string.Equals(args[1], "--help", StringComparison.Ordinal)
            && !args.Contains("--vfx-definitions", StringComparer.Ordinal))
        {
            var augmented = new List<string>(args)
            {
                "--vfx-definitions",
                Path.Combine(RepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json")
            };
            args = augmented.ToArray();
        }
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        try
        {
            Console.SetOut(new StringWriter());
            Console.SetError(new StringWriter());
            return MxFramework.Authoring.Cli.Program.Main(args);
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string SamplePath() => Path.Combine(RepoRoot(), "Tools", "MxFramework.Authoring", "samples", "ability-character-test");

    private static ProjectileAuthoringWorkspace LoadWorkspace(string packagePath) =>
        ProjectileAuthoringWorkspace.Load(
            packagePath,
            null,
            Path.Combine(RepoRoot(), "Assets", "MxFrameworkGenerated", "Vfx", "vfx_definitions.json"),
            Options);

    private static string RepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) && Directory.Exists(Path.Combine(directory.FullName, "Tools")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string NewTemp()
    {
        string path = Path.Combine(Path.GetTempPath(), "MxFramework.ProjectileAuthoring.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static Dictionary<string, string> HashFiles(string root, Func<string, bool> include = null)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
            return result;
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (include != null && !include(path))
                continue;
            string key = Path.GetRelativePath(root, path).Replace('\\', '/');
            result[key] = TryHashFile(path, out string hash) ? hash : ProjectileAuthoringHashes.Missing;
        }
        return result;
    }

    private static bool TryHashFile(string path, out string hash)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                hash = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path));
                return true;
            }
            catch (FileNotFoundException)
            {
                System.Threading.Thread.Yield();
            }
        }
        hash = string.Empty;
        return false;
    }

    private static string GitScopedStatus(string repo, string[] paths)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("status");
        start.ArgumentList.Add("--short");
        start.ArgumentList.Add("--");
        for (int i = 0; i < paths.Length; i++)
            start.ArgumentList.Add(paths[i]);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("git status could not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git status failed: " + error);
        return output;
    }

    private static Dictionary<string, string> HashScopes(string[] roots)
    {
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < roots.Length; i++)
        {
            string root = roots[i];
            if (!Directory.Exists(root))
                continue;
            foreach (KeyValuePair<string, string> pair in HashFiles(root))
                output[i + "/" + pair.Key] = pair.Value;
        }
        return output;
    }

    private static bool EqualHashes(Dictionary<string, string> left, Dictionary<string, string> right)
    {
        return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string value) && value == pair.Value);
    }

    private static void DeleteTemp(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private sealed class FailAfterThree : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 3)
                throw new IOException("Injected transaction failure.");
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }

    private sealed class FailAfterSeven : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 7)
                throw new IOException("Injected cross-root transaction failure.");
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }

    private sealed class FailAfterTen : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 10)
                throw new IOException("Injected synchronized apply failure.");
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }

    private sealed class RewriteSecondBeforeCommit : IProjectileAuthoringTransactionFaultInjector
    {
        private readonly string target;

        public RewriteSecondBeforeCommit(string target)
        {
            this.target = target;
        }

        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 1)
                File.WriteAllText(target, "external-change");
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }

    private sealed class FailCommitAndRollback : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 1)
                throw new IOException("Injected commit failure.");
        }

        public void BeforeRollback(string receiptPath)
        {
            throw new IOException("Injected rollback restore failure.");
        }
    }

    private sealed class RewriteCommittedThenFail : IProjectileAuthoringTransactionFaultInjector
    {
        private readonly string committedTarget;

        public RewriteCommittedThenFail(string committedTarget)
        {
            this.committedTarget = committedTarget;
        }

        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 1)
            {
                File.WriteAllText(committedTarget, "external-after-commit");
                throw new IOException("Injected failure after external rewrite.");
            }
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }
}
