using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string ReadinessStatusReady = "Ready";
    private const string ReadinessStatusWarning = "Warning";
    private const string ReadinessStatusBlocked = "Blocked";
    private const string ReadinessStatusDeferred = "Deferred";
    private const string ReadinessStatusUnknown = "Unknown";

    internal static CharacterAuthoringReadinessSnapshot ReadCharacterReadiness(
        string rootPath,
        string packageRelative,
        string defaultPackage,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackageRelative = string.IsNullOrWhiteSpace(packageRelative)
            ? DefaultCharacterPackageRelativePath
            : packageRelative;
        var snapshot = new CharacterAuthoringReadinessSnapshot
        {
            PackageRelative = resolvedPackageRelative,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        string encodedPackage = Uri.EscapeDataString(resolvedPackageRelative);
        string characterStudioUrl = "/Tools/MxFramework.CharacterStudio/web/?package=" + encodedPackage;
        string resourceLibraryUrl = "/Tools/MxFramework.ResourceLibrary/web/?package=" + encodedPackage;
        string animationEditorUrl = "/Tools/MxFramework.AnimationEditor/web/?package=" + encodedPackage;
        string actionToolUrl = characterStudioUrl;
        string hubUrl = "/Tools/MxFramework.EditorHub/web/?package=" + encodedPackage;

        string packagePath;
        CharacterResourcePackage package;
        try
        {
            packagePath = ResolveSafePath(rootPath, resolvedPackageRelative);
            package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
            snapshot.PackageId = package?.Manifest?.PackageId ?? string.Empty;
        }
        catch (Exception ex)
        {
            var issue = CreateIssue(
                "Error",
                "CHARACTER_PACKAGE_LOAD_FAILED",
                "Character package could not be loaded: " + ex.Message,
                resolvedPackageRelative,
                string.Empty,
                characterStudioUrl);
            AddStage(snapshot, new CharacterAuthoringReadinessStage
            {
                Id = "CharacterPackage",
                Label = "角色包",
                Status = ReadinessStatusBlocked,
                Summary = "角色包无法读取。",
                PrimaryToolUrl = characterStudioUrl,
                ActionLabel = "打开 CharacterStudio",
                SourcePaths = { resolvedPackageRelative },
                Blockers = { issue }
            });
            AddUnavailableStages(snapshot, resourceLibraryUrl, animationEditorUrl, actionToolUrl, hubUrl);
            FinalizeReadiness(snapshot);
            return snapshot;
        }

        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(
            package,
            new CharacterResourcePackageValidationOptions
            {
                PackageRootPath = packagePath,
                ValidateResourceFiles = true,
                ValidateResourceHashes = false
            });
        AddCharacterPackageStage(snapshot, package, validation, resolvedPackageRelative, characterStudioUrl);

        CharacterResourcePlanCompileResult resourcePlan = AddResourceBindingStage(
            snapshot,
            rootPath,
            resolvedPackageRelative,
            jsonOptions,
            resourceLibraryUrl);

        CharacterAuthoringCompileResult compile = AddCompileBackedStages(
            snapshot,
            rootPath,
            resolvedPackageRelative,
            package,
            jsonOptions,
            hubUrl);

        AddAnimationBindingStage(snapshot, packagePath, resolvedPackageRelative, animationEditorUrl);
        AddReactionBindingStage(snapshot, rootPath, package, resolvedPackageRelative, characterStudioUrl);
        AddActionTimelineStage(snapshot, rootPath, actionToolUrl);

        UnityImportReadiness importReadiness = AddUnityImportStage(
            snapshot,
            rootPath,
            package,
            compile,
            jsonOptions,
            hubUrl);

        AddRuntimeValidationStage(snapshot, compile, importReadiness, hubUrl);
        FinalizeReadiness(snapshot);
        return snapshot;
    }

    private static void AddCharacterPackageStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        CharacterResourcePackage package,
        CharacterAuthoringValidationReport validation,
        string packageRelative,
        string toolUrl)
    {
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "CharacterPackage",
            Label = "角色包",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "打开 CharacterStudio",
            Summary = "角色包可读取，已完成基础结构校验。"
        };
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "manifest.json"));
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "resource_catalog.json"));
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "config/character_application.json"));
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "geometry"));
        stage.ReportRefs.Add(new CharacterAuthoringReadinessReportRef
        {
            Id = "characterValidation",
            Label = "Character validation",
            Path = JoinProjectPath(packageRelative, "validation/last_report.json"),
            ToolUrl = toolUrl
        });

        AddValidationIssues(stage, validation?.Issues, toolUrl);
        int resourceCount = package?.ResourceCatalog?.Entries?.Count ?? 0;
        int colliderCount = package?.Geometry?.Colliders?.Count ?? 0;
        int socketCount = package?.Geometry?.Sockets?.Count ?? 0;
        int weaponCount = package?.Geometry?.WeaponAttachments?.Count ?? 0;

        if (stage.Blockers.Count > 0)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "角色包存在阻断项。";
        }
        else if (stage.Warnings.Count > 0)
        {
            stage.Status = ReadinessStatusWarning;
            stage.Summary = "角色包可读取，但存在需要处理的警告。";
        }
        else
        {
            stage.Status = ReadinessStatusReady;
        }

        stage.Metrics["resourceCount"] = resourceCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["colliderCount"] = colliderCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["socketCount"] = socketCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["weaponCount"] = weaponCount.ToString(CultureInfo.InvariantCulture);
        AddStage(snapshot, stage);
    }

    private static CharacterResourcePlanCompileResult AddResourceBindingStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions,
        string toolUrl)
    {
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "ResourceBinding",
            Label = "资源绑定",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "打开资源管理器",
            Summary = "读取 Resource Plan。"
        };
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "resource_catalog.json"));
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "config/character_application.json"));
        stage.ReportRefs.Add(new CharacterAuthoringReadinessReportRef
        {
            Id = "resourcePlan",
            Label = "Character resource plan",
            Path = "Assets/MxFrameworkGenerated/CharacterPackages/" + SafePackageId(snapshot.PackageId) + "/config/character_resource_plan.json",
            ToolUrl = toolUrl
        });
        stage.ReportRefs.Add(new CharacterAuthoringReadinessReportRef
        {
            Id = "resourceValidation",
            Label = "Resource validation",
            Path = "Assets/MxFrameworkGenerated/CharacterPackages/" + SafePackageId(snapshot.PackageId) + "/config/resource_validation_report.json",
            ToolUrl = toolUrl
        });

        try
        {
            CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
            ResourceValidationReportDocument validation = plan.ResourceValidationReport;
            AddResourcePlanDiagnostics(stage, validation?.Diagnostics, toolUrl);
            AddResourcePlanDiagnostics(stage, plan.CharacterResourcePlan?.Diagnostics, toolUrl);
            AddResourcePlanDiagnostics(stage, plan.AudioCueManifest?.Diagnostics, toolUrl);

            int runtimeEntryCount = plan.RuntimeResourceCatalog?.Entries?.Count ?? 0;
            stage.Metrics["runtimeResourceCount"] = runtimeEntryCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["spawnCriticalCount"] = CountPlanResources(plan.CharacterResourcePlan?.SpawnCritical).ToString(CultureInfo.InvariantCulture);
            stage.Metrics["animationWarmupCount"] = CountPlanResources(plan.CharacterResourcePlan?.AnimationWarmup).ToString(CultureInfo.InvariantCulture);

            if (validation != null && validation.HasErrors)
            {
                stage.Status = ReadinessStatusBlocked;
                stage.Summary = "Resource Plan 存在阻断项。";
            }
            else if (stage.Blockers.Count > 0)
            {
                stage.Status = ReadinessStatusBlocked;
                stage.Summary = "资源绑定存在阻断项。";
            }
            else if (stage.Warnings.Count > 0)
            {
                stage.Status = ReadinessStatusWarning;
                stage.Summary = "资源绑定可用，但存在警告。";
            }
            else
            {
                stage.Status = ReadinessStatusReady;
                stage.Summary = "Resource Plan 可用于运行时加载。";
            }

            AddStage(snapshot, stage);
            return plan;
        }
        catch (Exception ex)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "Resource Plan 编译失败。";
            stage.Blockers.Add(CreateIssue(
                "Error",
                "RESOURCE_PLAN_COMPILE_FAILED",
                ex.Message,
                JoinProjectPath(packageRelative, "resource_catalog.json"),
                string.Empty,
                toolUrl));
            AddStage(snapshot, stage);
            return null;
        }
    }

    private static CharacterAuthoringCompileResult AddCompileBackedStages(
        CharacterAuthoringReadinessSnapshot snapshot,
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions,
        string toolUrl)
    {
        try
        {
            CharacterAuthoringCompileResult compile = CompileCharacterPackage(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
            snapshot.ReportRefs.Add(new CharacterAuthoringReadinessReportRef
            {
                Id = "compileGate",
                Label = "Compile gate",
                Path = "Assets/MxFrameworkGenerated/CharacterPackages/" + SafePackageId(package?.Manifest?.PackageId) + "/package_cache/gate_report.txt",
                ToolUrl = toolUrl
            });
            return compile;
        }
        catch (Exception ex)
        {
            var stage = new CharacterAuthoringReadinessStage
            {
                Id = "RuntimeValidation",
                Label = "Runtime 验证",
                Status = ReadinessStatusBlocked,
                PrimaryToolUrl = toolUrl,
                ActionLabel = "查看交付流",
                Summary = "Character compile 失败，无法评估 Runtime 验证。",
                SourcePaths =
                {
                    JoinProjectPath(packageRelative, "config/character_application.json"),
                    JoinProjectPath(packageRelative, "geometry")
                },
                Blockers =
                {
                    CreateIssue(
                        "Error",
                        "CHARACTER_COMPILE_FAILED",
                        ex.Message,
                        JoinProjectPath(packageRelative, "config/character_application.json"),
                        string.Empty,
                        toolUrl)
                }
            };
            AddStage(snapshot, stage);
            return null;
        }
    }

    private static void AddAnimationBindingStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        string packagePath,
        string packageRelative,
        string toolUrl)
    {
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "AnimationBinding",
            Label = "动画绑定",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "打开动画编辑器",
            Summary = "读取动画作者包。"
        };
        string relativePath = JoinProjectPath(packageRelative, AnimationAuthoringDocumentRelativePath);
        stage.SourcePaths.Add(relativePath);
        stage.SourcePaths.Add(JoinProjectPath(packageRelative, "config/character_application.json"));

        string fullPath = Path.Combine(packagePath, AnimationAuthoringDocumentRelativePath);
        if (!File.Exists(fullPath))
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "缺少动画作者包。";
            stage.Blockers.Add(CreateIssue(
                "Error",
                "ANIMATION_AUTHORING_MISSING",
                "config/animation_authoring.json was not found.",
                relativePath,
                string.Empty,
                toolUrl));
            AddStage(snapshot, stage);
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
            int setCount = CountJsonArray(document.RootElement, "sets");
            int groupCount = CountJsonArrayItemsRecursive(document.RootElement, "groups");
            int clipCount = CountJsonArrayItemsRecursive(document.RootElement, "clips");
            stage.Metrics["setCount"] = setCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["groupCount"] = groupCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["clipCount"] = clipCount.ToString(CultureInfo.InvariantCulture);

            if (setCount == 0 || clipCount == 0)
            {
                stage.Status = ReadinessStatusBlocked;
                stage.Summary = "动画包缺少 Set 或 Clip 绑定。";
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    "ANIMATION_AUTHORING_EMPTY",
                    "Animation authoring package has no playable set or clip bindings.",
                    relativePath,
                    string.Empty,
                    toolUrl));
            }
            else
            {
                stage.Status = ReadinessStatusReady;
                stage.Summary = "动画包已绑定可用 Set/Clip。";
            }
        }
        catch (JsonException ex)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "动画作者包 JSON 无效。";
            stage.Blockers.Add(CreateIssue(
                "Error",
                "ANIMATION_AUTHORING_JSON_INVALID",
                ex.Message,
                relativePath,
                string.Empty,
                toolUrl));
        }

        AddStage(snapshot, stage);
    }

    private static void AddReactionBindingStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        string rootPath,
        CharacterResourcePackage package,
        string packageRelative,
        string toolUrl)
    {
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "ReactionBinding",
            Label = "Reaction Binding",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "Open CharacterStudio",
            Summary = "Read referenced global reaction configuration animation selections."
        };
        string sourcePath = GlobalWeaponLibraryRelativePath;
        stage.SourcePaths.Add(sourcePath);
        CharacterActionHandoffConsistencyReport handoffReport =
            ReadCharacterActionHandoffConsistency(rootPath, package?.Manifest?.PackageId);
        AddCharacterActionHandoffConsistency(stage, handoffReport, toolUrl);

        var configurations = new List<GlobalReactionConfiguration>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        List<CombinedWeaponConfigurationAuthoringSummary> combinations =
            package?.ApplicationConfig?.CombinedWeaponConfigurations;
        for (int i = 0; combinations != null && i < combinations.Count; i++)
        {
            string id = combinations[i]?.ReactionConfigurationId ?? string.Empty;
            GlobalReactionConfiguration configuration = GlobalWeaponConfigurationResolver.FindReaction(
                package.GlobalWeaponLibrary,
                id);
            if (configuration != null && ids.Add(id))
                configurations.Add(configuration);
        }
        if (configurations.Count == 0)
        {
            stage.Warnings.Add(CreateIssue(
                "Warning",
                CharacterAuthoringValidationCodes.GlobalReactionConfigurationNotFound,
                "No referenced global reaction configuration exists yet.",
                sourcePath,
                string.Empty,
                toolUrl));
            stage.Status = stage.Blockers.Count > 0 ? ReadinessStatusBlocked : ReadinessStatusWarning;
            stage.Summary = "No global reaction configuration is ready.";
            AddStage(snapshot, stage);
            return;
        }

        int slotCount = 0;
        int selectedCount = 0;
        int requiredCount = 0;
        for (int profileIndex = 0; profileIndex < configurations.Count; profileIndex++)
        {
            GlobalReactionConfiguration profile = configurations[profileIndex];
            if (profile == null)
                continue;

            string profilePath = "reactionConfigurations/" + profileIndex.ToString(CultureInfo.InvariantCulture);
            bool hasSlots = profile.Slots != null && profile.Slots.Exists(slot => slot != null);
            if (hasSlots && string.IsNullOrWhiteSpace(profile.DefaultSlotId))
            {
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    "REACTION_DEFAULT_SLOT_MISSING",
                    "Reaction configuration '" + profile.ReactionConfigurationId + "' must select an explicit default Slot.",
                    sourcePath,
                    profilePath,
                    toolUrl));
            }
            else if (!string.IsNullOrWhiteSpace(profile.DefaultSlotId) &&
                (profile.Slots == null || !profile.Slots.Exists(slot =>
                    slot != null && string.Equals(slot.SlotId, profile.DefaultSlotId, StringComparison.Ordinal))))
            {
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    "REACTION_DEFAULT_SLOT_NOT_FOUND",
                    "Reaction default Slot '" + profile.DefaultSlotId + "' does not exist in configuration '" + profile.ReactionConfigurationId + "'.",
                    sourcePath,
                    profilePath + "/defaultSlotId",
                    toolUrl));
            }

            if (profile.Slots == null)
                continue;

            for (int slotIndex = 0; slotIndex < profile.Slots.Count; slotIndex++)
            {
                GlobalReactionSlotConfiguration slot = profile.Slots[slotIndex];
                if (slot == null)
                    continue;

                slotCount++;
                bool selected = HasReactionSlotSelection(slot);
                if (selected)
                    selectedCount++;
                if (!slot.Required)
                    continue;

                requiredCount++;
                string sourceObjectPath = "reactionConfigurations/" + profileIndex.ToString(CultureInfo.InvariantCulture) + "/slots/" + slotIndex.ToString(CultureInfo.InvariantCulture);
                if (!selected)
                {
                    stage.Blockers.Add(CreateIssue(
                        "Error",
                        "REACTION_SLOT_ANIMATION_MISSING",
                        "Required reaction slot '" + slot.SlotId + "' has no animation clip selection.",
                        sourcePath,
                        sourceObjectPath,
                        toolUrl));
                    continue;
                }

                if (IsEditorOnlyReactionSelection(slot.ResourceSelection))
                {
                    stage.Blockers.Add(CreateIssue(
                        "Error",
                        "REACTION_SLOT_ANIMATION_EDITOR_ONLY",
                        "Required reaction slot '" + slot.SlotId + "' selects an editor-only animation. Compile or import it into a runtime/package animation clip resource.",
                        sourcePath,
                        sourceObjectPath,
                        toolUrl));
                }
            }
        }

        stage.Metrics["reactionSlotCount"] = slotCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["reactionRequiredSlotCount"] = requiredCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["reactionSelectedSlotCount"] = selectedCount.ToString(CultureInfo.InvariantCulture);
        stage.Status = stage.Blockers.Count > 0 ? ReadinessStatusBlocked : ReadinessStatusReady;
        stage.Summary = stage.Status == ReadinessStatusReady
            ? "Reaction animation slots are ready for Unity import."
            : (handoffReport != null && handoffReport.HasBlockingIssues
                ? "CharacterAction reaction profile generated assets need regeneration."
                : "Reaction animation slots need runtime-ready animation selections.");
        AddStage(snapshot, stage);
    }

    private static void AddCharacterActionHandoffConsistency(
        CharacterAuthoringReadinessStage stage,
        CharacterActionHandoffConsistencyReport report,
        string toolUrl)
    {
        if (stage == null || report == null)
            return;

        for (int i = 0; i < report.SourcePaths.Count; i++)
        {
            string path = report.SourcePaths[i];
            if (!string.IsNullOrWhiteSpace(path) && !stage.SourcePaths.Contains(path))
                stage.SourcePaths.Add(path);
        }

        stage.Metrics["actionSetReactionBindingCount"] = report.ActionSetReactionBindingCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["reactionProfileCount"] = report.ReactionProfileCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["reactionActionReferenceCount"] = report.ReactionActionReferenceCount.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < report.Issues.Count; i++)
        {
            CharacterActionHandoffConsistencyIssue diagnostic = report.Issues[i];
            CharacterAuthoringReadinessIssue issue = CreateIssue(
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourcePath,
                diagnostic.SourceObjectPath,
                toolUrl);
            issue.Field = diagnostic.Field;
            issue.SuggestedFix = diagnostic.SuggestedFix;
            if (IsErrorSeverity(diagnostic.Severity))
                stage.Blockers.Add(issue);
            else
                stage.Warnings.Add(issue);
        }
    }

    private static bool HasReactionSlotSelection(GlobalReactionSlotConfiguration slot)
    {
        if (slot == null)
            return false;
        if (!string.IsNullOrWhiteSpace(slot.ResourceKey))
            return true;

        AuthoringResourceSelectionRef selection = slot.ResourceSelection;
        return selection != null &&
            (!string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.PackageResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.ProviderResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.ResourceStableId) ||
             !string.IsNullOrWhiteSpace(selection.UnityAssetPath));
    }

    private static bool IsEditorOnlyReactionSelection(AuthoringResourceSelectionRef selection)
    {
        if (selection == null)
            return false;
        if (!string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) ||
            !string.IsNullOrWhiteSpace(selection.PackageResourceKey))
            return false;

        return selection.BindingKind == AuthoringResourceBindingKind.UnityEditorOnlyAsset ||
            selection.BindingKind == AuthoringResourceBindingKind.UnityAsset;
    }

    private static void AddActionTimelineStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        string rootPath,
        string toolUrl)
    {
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "ActionTimeline",
            Label = "动作时间轴",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "打开伤害分析",
            Summary = "读取 CharacterAction 时间轴。"
        };
        stage.SourcePaths.Add(CharacterActionDamageAnalysisRelativePath);
        stage.ReportRefs.Add(new CharacterAuthoringReadinessReportRef
        {
            Id = "actionDamageAnalysis",
            Label = "Action damage analysis",
            Path = CharacterActionDamageAnalysisRelativePath,
            ToolUrl = toolUrl
        });

        string sourcePath = Path.Combine(rootPath, CharacterActionDamageAnalysisRelativePath);
        if (!File.Exists(sourcePath))
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "缺少 CharacterAction JSON。";
            stage.Blockers.Add(CreateIssue(
                "Error",
                "ACTION_DAMAGE_SOURCE_MISSING",
                "Character action JSON was not found.",
                CharacterActionDamageAnalysisRelativePath,
                string.Empty,
                toolUrl));
            AddStage(snapshot, stage);
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sourcePath));
            JsonElement root = document.RootElement;
            int actionCount = CountJsonArray(root, "actions");
            int damageEventCount = CountActionDamageEvents(root);
            stage.Metrics["actionCount"] = actionCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["damageEventCount"] = damageEventCount.ToString(CultureInfo.InvariantCulture);
            stage.Status = actionCount > 0 ? ReadinessStatusReady : ReadinessStatusWarning;
            stage.Summary = actionCount > 0
                ? "CharacterAction 时间轴可读取。"
                : "CharacterAction JSON 可读取，但没有动作。";
            if (actionCount == 0)
            {
                stage.Warnings.Add(CreateIssue(
                    "Warning",
                    "ACTION_TIMELINE_EMPTY",
                    "Character action JSON contains no actions.",
                    CharacterActionDamageAnalysisRelativePath,
                    string.Empty,
                    toolUrl));
            }
        }
        catch (JsonException ex)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "CharacterAction JSON 无效。";
            stage.Blockers.Add(CreateIssue(
                "Error",
                "ACTION_DAMAGE_JSON_INVALID",
                ex.Message,
                CharacterActionDamageAnalysisRelativePath,
                string.Empty,
                toolUrl));
        }

        AddStage(snapshot, stage);
    }

    private static UnityImportReadiness AddUnityImportStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        string rootPath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult compile,
        JsonSerializerOptions jsonOptions,
        string toolUrl)
    {
        string packageId = SafePackageId(package?.Manifest?.PackageId);
        string importReportRelative = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/package_cache/import_report.json";
        string unityCatalogRelative = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/config/unity_resource_catalog.json";
        string receiptRelative = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/package_cache/unity_editor_import_receipt.json";
        string traceBakeRelative = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/generated/weapon_trace_bake_catalog.json";
        string editorStatusDirectoryRelative = CharacterUnityImportEditorStatusDirectoryRelativePath;
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "UnityImport",
            Label = "Unity 导入",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "查看导入报告",
            Summary = "读取 Unity 导入报告。",
            SourcePaths =
            {
                importReportRelative,
                unityCatalogRelative,
                receiptRelative,
                editorStatusDirectoryRelative
            },
            ReportRefs =
            {
                new CharacterAuthoringReadinessReportRef
                {
                    Id = "unityImportReport",
                    Label = "Unity import report",
                    Path = importReportRelative,
                    ToolUrl = toolUrl
                },
                new CharacterAuthoringReadinessReportRef
                {
                    Id = "unityResourceCatalog",
                    Label = "Unity resource catalog",
                    Path = unityCatalogRelative,
                    ToolUrl = toolUrl
                },
                new CharacterAuthoringReadinessReportRef
                {
                    Id = "unityEditorImportReceipt",
                    Label = "Unity Editor import receipt",
                    Path = receiptRelative,
                    ToolUrl = toolUrl
                }
            }
        };

        string importReportPath = Path.Combine(rootPath, importReportRelative);
        string unityCatalogPath = Path.Combine(rootPath, unityCatalogRelative);
        string receiptPath = Path.Combine(rootPath, receiptRelative);
        CharacterCompilerHashSet currentHashes = compile?.Hashes;
        string currentSourcePackageHash = currentHashes?.SourcePackageHash ?? string.Empty;
        bool hardFailure = false;
        bool ready = false;
        bool canSpawnAfterImport = false;
        bool hasCurrentSession = false;
        string evidenceSource = "none";

        stage.Metrics["evidenceSource"] = evidenceSource;
        stage.Metrics["receiptPath"] = receiptRelative;
        stage.Metrics["receiptStatus"] = File.Exists(receiptPath) ? "Unknown" : "Missing";
        stage.Metrics["currentSourcePackageHash"] = currentSourcePackageHash;
        stage.Metrics["currentGeneratedConfigHash"] = currentHashes?.GeneratedConfigHash ?? string.Empty;
        stage.Metrics["currentGeometryBindingHash"] = currentHashes?.GeometryBindingHash ?? string.Empty;
        stage.Metrics["currentResourceMappingHash"] = currentHashes?.ResourceMappingHash ?? string.Empty;
        stage.Metrics["currentWritePlanHash"] = currentHashes?.WritePlanHash ?? string.Empty;
        UnityImportReceiptEvidence receiptEvidence = EvaluateUnityImportReceipt(
            rootPath,
            packageId,
            receiptRelative,
            unityCatalogRelative,
            traceBakeRelative,
            currentHashes,
            jsonOptions);
        stage.Metrics["receiptStatus"] = receiptEvidence.Status;

        bool currentImportReportReady = false;
        if (!File.Exists(importReportPath))
        {
            stage.Metrics["importReportStatus"] = "Missing";
        }
        else
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(importReportPath));
                JsonElement report = document.RootElement;
                string status = GetJsonString(report, "status");
                int errorCount = GetJsonInt(report, "errorCount");
                int conflictCount = GetJsonInt(report, "conflictCount");
                bool canWriteToUnity = GetJsonBool(report, "canWriteToUnityProject", true);
                bool reportCanSpawn = GetJsonBool(report, "canSpawnAfterImport", false);
                string reportPackageId = GetJsonString(report, "packageId");
                string reportSourceHash = GetJsonString(report, "sourcePackageHash");
                string reportGeneratedConfigHash = GetJsonString(report, "generatedConfigHash");
                string reportGeometryBindingHash = GetJsonString(report, "geometryBindingHash");
                string reportResourceMappingHash = GetJsonString(report, "resourceMappingHash");
                string reportWritePlanHash = GetJsonString(report, "writePlanHash");
                bool reportIsCurrent = currentHashes != null &&
                    string.Equals(reportPackageId, packageId, StringComparison.Ordinal) &&
                    HashEquals(reportSourceHash, currentHashes.SourcePackageHash) &&
                    HashEquals(reportGeneratedConfigHash, currentHashes.GeneratedConfigHash) &&
                    HashEquals(reportGeometryBindingHash, currentHashes.GeometryBindingHash) &&
                    HashEquals(reportResourceMappingHash, currentHashes.ResourceMappingHash) &&
                    HashEquals(reportWritePlanHash, currentHashes.WritePlanHash);

                stage.Metrics["importReportStatus"] = status;
                stage.Metrics["operationCount"] = CountJsonArray(report, "operations").ToString(CultureInfo.InvariantCulture);
                stage.Metrics["errorCount"] = errorCount.ToString(CultureInfo.InvariantCulture);
                stage.Metrics["conflictCount"] = conflictCount.ToString(CultureInfo.InvariantCulture);
                stage.Metrics["importPackageId"] = reportPackageId;
                stage.Metrics["importSourcePackageHash"] = reportSourceHash;
                stage.Metrics["importGeneratedConfigHash"] = reportGeneratedConfigHash;
                stage.Metrics["importGeometryBindingHash"] = reportGeometryBindingHash;
                stage.Metrics["importResourceMappingHash"] = reportResourceMappingHash;
                stage.Metrics["importWritePlanHash"] = reportWritePlanHash;

                if (reportIsCurrent)
                {
                    bool reportStatusAllowsSpawn =
                        string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "WarningOnly", StringComparison.OrdinalIgnoreCase);
                    currentImportReportReady = reportStatusAllowsSpawn &&
                        errorCount == 0 && conflictCount == 0 && canWriteToUnity && reportCanSpawn;
                    if (!currentImportReportReady)
                    {
                        hardFailure = true;
                        stage.Blockers.Add(CreateIssue(
                            "Error",
                            "UNITY_IMPORT_REPORT_FAILED",
                            "The current Unity import report contains errors, conflicts, or a blocked spawn gate.",
                            importReportRelative,
                            string.Empty,
                            toolUrl));
                    }
                }
                else
                {
                    stage.Metrics["importReportStatus"] = "Stale";
                }
            }
            catch (JsonException ex)
            {
                hardFailure = true;
                stage.Metrics["importReportStatus"] = "Invalid";
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    "UNITY_IMPORT_REPORT_JSON_INVALID",
                    ex.Message,
                    importReportRelative,
                    string.Empty,
                    toolUrl));
            }
        }

        CharacterUnityImportEditorStatus editorStatus = ReadLatestCharacterUnityImportEditorStatusForCompilerIdentity(
            rootPath,
            snapshot.PackageRelative,
            packageId,
            currentHashes,
            jsonOptions);
        if (editorStatus != null)
        {
            hasCurrentSession = true;
            evidenceSource = "currentUnitySession";
            stage.Metrics["evidenceSource"] = evidenceSource;
            stage.Metrics["editorStatus"] = editorStatus.status ?? string.Empty;
            stage.Metrics["editorRequestId"] = editorStatus.requestId ?? string.Empty;
            stage.Metrics["weaponTraceBakeStatus"] = editorStatus.weaponTraceBakeStatus ?? string.Empty;
            stage.Metrics["weaponTraceBakeArtifactCount"] = editorStatus.weaponTraceBakeArtifactCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["weaponTraceBakeCheckedFrameCount"] = editorStatus.weaponTraceBakeCheckedFrameCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["weaponTraceBakeIssueCount"] = editorStatus.weaponTraceBakeIssueCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["socketBakeStatus"] = editorStatus.socketBakeStatus ?? string.Empty;
            stage.Metrics["socketBakeClipCount"] = editorStatus.socketBakeClipCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["socketBakeSocketTrackCount"] = editorStatus.socketBakeSocketTrackCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["socketBakeCheckedFrameCount"] = editorStatus.socketBakeCheckedFrameCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["socketBakeIssueCount"] = editorStatus.socketBakeIssueCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["hurtboxBakeStatus"] = editorStatus.hurtboxBakeStatus ?? string.Empty;
            stage.Metrics["hurtboxBakeClipCount"] = editorStatus.hurtboxBakeClipCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["hurtboxBakeColliderCount"] = editorStatus.hurtboxBakeColliderCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["hurtboxBakeShapeCount"] = editorStatus.hurtboxBakeShapeCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["hurtboxBakeCheckedFrameCount"] = editorStatus.hurtboxBakeCheckedFrameCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["hurtboxBakeIssueCount"] = editorStatus.hurtboxBakeIssueCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["abstractBoneStatus"] = editorStatus.abstractBoneStatus ?? string.Empty;
            stage.Metrics["abstractBoneDeclaredCount"] = editorStatus.abstractBoneDeclaredCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["abstractBoneResolvedCount"] = editorStatus.abstractBoneResolvedCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["abstractBoneIssueCount"] = editorStatus.abstractBoneIssueCount.ToString(CultureInfo.InvariantCulture);
            stage.Metrics["characterActionProfileReady"] = editorStatus.characterActionProfileReady ? "true" : "false";
            stage.Metrics["characterActionActionSetReady"] = editorStatus.characterActionActionSetReady ? "true" : "false";

            string sessionStatus = editorStatus.status ?? string.Empty;
            if (string.Equals(sessionStatus, "failed", StringComparison.OrdinalIgnoreCase))
            {
                hardFailure = true;
                string topErrorCode = !string.IsNullOrWhiteSpace(editorStatus.errorCode)
                    ? editorStatus.errorCode
                    : "UNITY_EDITOR_REBUILD_FAILED";
                string topMessage = !string.IsNullOrWhiteSpace(editorStatus.message)
                    ? editorStatus.message
                    : "The current Unity Editor import session failed.";

                stage.Blockers.Add(CreateIssue(
                    "Error",
                    topErrorCode,
                    topMessage,
                    editorStatusDirectoryRelative,
                    editorStatus.requestId,
                    toolUrl));

                if (!string.Equals(topErrorCode, "UNITY_IMPORTED_PACKAGE_PARSE_FAILED", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(topErrorCode, "UNITY_IMPORTED_PACKAGE_PREFLIGHT_FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    AddWeaponTraceBakeReadinessIssues(stage, editorStatus, editorStatusDirectoryRelative, toolUrl);
                    AddSocketBakeReadinessIssues(stage, editorStatus, editorStatusDirectoryRelative, toolUrl);
                    AddHurtboxBakeReadinessIssues(stage, editorStatus, editorStatusDirectoryRelative, toolUrl);
                }
            }
            else if (string.Equals(sessionStatus, "completed", StringComparison.OrdinalIgnoreCase))
            {
                bool sessionIdentityMatches = MatchesCharacterCompilerIdentity(editorStatus, packageId, currentHashes);
                string sessionArtifactError = string.Empty;
                bool sessionReady = sessionIdentityMatches &&
                    editorStatus.characterActionProfileReady &&
                    editorStatus.characterActionActionSetReady &&
                    editorStatus.weaponTraceBakeReady &&
                    editorStatus.weaponTraceBakeIssueCount == 0 &&
                    editorStatus.socketBakeReady &&
                    editorStatus.socketBakeIssueCount == 0 &&
                    editorStatus.hurtboxBakeReady &&
                    editorStatus.hurtboxBakeIssueCount == 0 &&
                    string.Equals(editorStatus.abstractBoneStatus, "Ready", StringComparison.Ordinal) &&
                    editorStatus.abstractBoneDeclaredCount > 0 &&
                    editorStatus.abstractBoneResolvedCount == editorStatus.abstractBoneDeclaredCount &&
                    editorStatus.abstractBoneIssueCount == 0 &&
                    (editorStatus.issues == null || editorStatus.issues.Length == 0) &&
                    ValidateCurrentUnitySessionArtifacts(
                        rootPath,
                        packageId,
                        unityCatalogRelative,
                        traceBakeRelative,
                        editorStatus,
                        out sessionArtifactError);
                if (sessionReady)
                {
                    ready = true;
                    canSpawnAfterImport = true;
                }
                else
                {
                    hardFailure = true;
                    stage.Blockers.Add(CreateIssue(
                        "Error",
                        "UNITY_EDITOR_REBUILD_EVIDENCE_INVALID",
                        string.IsNullOrWhiteSpace(sessionArtifactError)
                            ? "The current Unity Editor session completed without all required hashes, CharacterAction, trace bake, catalog, or spawn evidence."
                            : sessionArtifactError,
                        editorStatusDirectoryRelative,
                        editorStatus.requestId,
                        toolUrl));
                    AddSocketBakeReadinessIssues(stage, editorStatus, editorStatusDirectoryRelative, toolUrl);
                    AddHurtboxBakeReadinessIssues(stage, editorStatus, editorStatusDirectoryRelative, toolUrl);
                }
            }
            else if (!string.Equals(sessionStatus, "pending", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(sessionStatus, "running", StringComparison.OrdinalIgnoreCase))
            {
                hardFailure = true;
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    "UNITY_EDITOR_REBUILD_STATUS_INVALID",
                    "The current Unity Editor session has an unsupported status '" + sessionStatus + "'.",
                    editorStatusDirectoryRelative,
                    editorStatus.requestId,
                    toolUrl));
            }
        }

        if (!hasCurrentSession && !hardFailure)
        {
            if (receiptEvidence.Ready)
            {
                evidenceSource = "persistentReceipt";
                stage.Metrics["evidenceSource"] = evidenceSource;
                ready = true;
                canSpawnAfterImport = true;
                CopyReceiptMetrics(stage, receiptEvidence.Receipt);
            }
            else if (receiptEvidence.HardFailure)
            {
                hardFailure = true;
                stage.Blockers.Add(CreateIssue(
                    "Error",
                    receiptEvidence.ErrorCode,
                    receiptEvidence.Message,
                    receiptRelative,
                    string.Empty,
                    toolUrl));
                AddReceiptBakeReadinessIssues(stage, receiptEvidence.Receipt, receiptRelative, toolUrl);
            }
        }

        if (compile?.GateReport != null && compile.GateReport.ImportBlocked)
        {
            AddValidationIssues(stage, compile.GateReport.Issues, toolUrl);
            hardFailure = true;
        }

        if (hardFailure || stage.Blockers.Count > 0)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "Unity 导入或 Editor 验收明确失败。";
        }
        else if (ready)
        {
            stage.Status = ReadinessStatusReady;
            stage.Summary = evidenceSource == "currentUnitySession"
                ? "当前 Unity 会话已完成验收。"
                : "当前 Compiler 身份已由持久验收凭据证明可运行。";
        }
        else
        {
            stage.Status = ReadinessStatusDeferred;
            stage.Summary = hasCurrentSession
                ? "Unity Editor 正在重新验收当前角色。"
                : "待执行 Unity 导入；当前没有与 Compiler 身份匹配的验收证据。";
            stage.ActionLabel = "执行 Unity 导入";
        }

        stage.Metrics["canSpawnAfterImport"] = canSpawnAfterImport ? "true" : "false";

        AddStage(snapshot, stage);
        return new UnityImportReadiness(ready, canSpawnAfterImport, hardFailure, evidenceSource);
    }

    private static UnityImportReceiptEvidence EvaluateUnityImportReceipt(
        string rootPath,
        string packageId,
        string receiptRelative,
        string unityCatalogRelative,
        string traceBakeRelative,
        CharacterCompilerHashSet currentHashes,
        JsonSerializerOptions jsonOptions)
    {
        string receiptPath = Path.Combine(rootPath, receiptRelative);
        if (!File.Exists(receiptPath))
            return UnityImportReceiptEvidence.Deferred("Missing");

        CharacterUnityEditorImportReceipt receipt;
        try
        {
            receipt = JsonSerializer.Deserialize<CharacterUnityEditorImportReceipt>(File.ReadAllText(receiptPath), jsonOptions);
        }
        catch (JsonException ex)
        {
            return UnityImportReceiptEvidence.Failed("Invalid", "UNITY_EDITOR_IMPORT_RECEIPT_JSON_INVALID", ex.Message);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return UnityImportReceiptEvidence.Failed(
                "Invalid",
                "UNITY_EDITOR_IMPORT_RECEIPT_READ_FAILED",
                "Unity Editor import receipt could not be read: " + ex.Message);
        }

        bool receiptV1 = receipt != null
            && string.Equals(receipt.format, "mx.characterUnityEditorImportReceipt.v1", StringComparison.Ordinal)
            && string.Equals(receipt.schemaVersion, "1.0", StringComparison.Ordinal);
        bool receiptV2 = receipt != null
            && string.Equals(receipt.format, "mx.characterUnityEditorImportReceipt.v2", StringComparison.Ordinal)
            && string.Equals(receipt.schemaVersion, "2.0", StringComparison.Ordinal);
        bool receiptV3 = receipt != null
            && string.Equals(receipt.format, "mx.characterUnityEditorImportReceipt.v3", StringComparison.Ordinal)
            && string.Equals(receipt.schemaVersion, "3.0", StringComparison.Ordinal);
        if (receiptV1)
            return UnityImportReceiptEvidence.Deferred("LegacyNeedsAbstractBoneReimport");
        // A v2 receipt predates the socket bake v3 index + payload pair and must not be treated as Ready.
        if (receiptV2)
            return UnityImportReceiptEvidence.Deferred("LegacyNeedsSocketBakeReimport");
        if (!receiptV3)
        {
            return UnityImportReceiptEvidence.Failed(
                "Invalid",
                "UNITY_EDITOR_IMPORT_RECEIPT_INVALID",
                "Unity Editor import receipt format, schemaVersion, or ready flag is invalid.");
        }

        if (!receipt.ready)
        {
            return UnityImportReceiptEvidence.Rejected(
                receipt,
                "UNITY_EDITOR_IMPORT_REJECTED",
                "The latest persistent Unity Editor import attempt was rejected. Structured stage diagnostics are attached.");
        }

        if (!string.Equals(receipt.packageId, packageId, StringComparison.Ordinal))
        {
            return UnityImportReceiptEvidence.Failed(
                "Invalid",
                "UNITY_EDITOR_IMPORT_RECEIPT_PACKAGE_MISMATCH",
                "Unity Editor import receipt packageId does not match the selected character package.");
        }

        CharacterUnityEditorImportReceiptHashes hashes = receipt.hashes;
        if (hashes == null || currentHashes == null ||
            !HashEquals(hashes.sourcePackageHash, currentHashes.SourcePackageHash) ||
            !HashEquals(hashes.generatedConfigHash, currentHashes.GeneratedConfigHash) ||
            !HashEquals(hashes.geometryBindingHash, currentHashes.GeometryBindingHash) ||
            !HashEquals(hashes.resourceMappingHash, currentHashes.ResourceMappingHash) ||
            !HashEquals(hashes.writePlanHash, currentHashes.WritePlanHash))
        {
            return UnityImportReceiptEvidence.Deferred("Stale");
        }

        if (string.IsNullOrWhiteSpace(receipt.builderVersion) ||
            string.IsNullOrWhiteSpace(hashes.unityArtifactHash) ||
            receipt.prefab == null ||
            receipt.avatar == null ||
            !IsValidReceiptAvatar(receipt.avatar, receiptV1) ||
            string.IsNullOrWhiteSpace(receipt.avatar.guid) ||
            receipt.material == null ||
            receipt.material.rendererCount < 0 ||
            (receiptV3 && (receipt.materials == null || receipt.materials.Length == 0)) ||
            receipt.geometry == null ||
            receipt.geometry.socketCount < 0 ||
            receipt.geometry.weaponAttachmentCount < 0 ||
            receipt.geometry.mountedWeaponCount < 0 ||
            receipt.characterAction == null ||
            !receipt.characterAction.profileReady ||
            !receipt.characterAction.actionSetReady ||
            receipt.weaponTraceBake == null ||
            !string.Equals(receipt.weaponTraceBake.status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(receipt.weaponTraceBake.hash) ||
            receipt.weaponTraceBake.requestCount < 0 ||
            receipt.weaponTraceBake.artifactCount < 0 ||
            receipt.weaponTraceBake.checkedFrameCount < 0
            || (receiptV3 && (receipt.socketBake == null
                || !string.Equals(receipt.socketBake.status, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(receipt.socketBake.hash)
                || receipt.socketBake.requestCount < 0
                || receipt.socketBake.clipCount < 0
                || receipt.socketBake.socketTrackCount < 0
                || receipt.socketBake.checkedFrameCount < 0
                || receipt.socketBake.issueCount != 0
                || !string.Equals(receipt.socketBake.format, "mx.character.socketBakeCatalog.v3", StringComparison.Ordinal)
                || !string.Equals(receipt.socketBake.indexPath, "generated/character_socket_bake_catalog.json", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(receipt.socketBake.indexHash)
                || !string.Equals(receipt.socketBake.binaryPath, "generated/character_socket_bake_catalog.bytes", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(receipt.socketBake.binaryHash)
                || receipt.socketBake.binarySize <= 0
                || receipt.hurtboxBake == null
                || !string.Equals(receipt.hurtboxBake.status, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(receipt.hurtboxBake.hash)
                || receipt.hurtboxBake.clipCount < 0
                || receipt.hurtboxBake.colliderCount < 0
                || receipt.hurtboxBake.shapeCount < 0
                || receipt.hurtboxBake.checkedFrameCount < 0
                || receipt.hurtboxBake.issueCount != 0
                || receipt.abstractBones == null
                || !string.Equals(receipt.abstractBones.status, "Ready", StringComparison.Ordinal)
                || receipt.abstractBones.declaredCount <= 0
                || receipt.abstractBones.resolvedCount != receipt.abstractBones.declaredCount
                || receipt.abstractBones.issueCount != 0
                || receipt.abstractBones.requiredIds == null
                || Array.IndexOf(receipt.abstractBones.requiredIds, "head") < 0)))
        {
            return UnityImportReceiptEvidence.Failed(
                "Invalid",
                "UNITY_EDITOR_IMPORT_RECEIPT_ACCEPTANCE_INVALID",
                "Unity Editor import receipt does not contain complete artifact, CharacterAction, weapon trace, or socket bake acceptance evidence.");
        }

        string[] requiredPaths =
        {
            receipt.prefab?.prefabPath ?? string.Empty,
            receipt.prefab?.runtimePrefabPath ?? string.Empty,
            receipt.avatar?.assetPath ?? string.Empty,
            receipt.characterAction.profilePath,
            receipt.characterAction.actionSetPath,
            unityCatalogRelative,
            traceBakeRelative,
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/"
                + (receipt.socketBake?.indexPath ?? "generated/character_socket_bake_catalog.json"),
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/"
                + (receipt.socketBake?.binaryPath ?? "generated/character_socket_bake_catalog.bytes")
        };
        for (int i = 0; i < requiredPaths.Length; i++)
        {
            ReceiptArtifactPathStatus pathStatus = ValidateReceiptArtifactPath(rootPath, requiredPaths[i]);
            if (pathStatus == ReceiptArtifactPathStatus.Invalid)
            {
                return UnityImportReceiptEvidence.Failed(
                    "Invalid",
                    "UNITY_EDITOR_IMPORT_RECEIPT_PATH_INVALID",
                    "Unity Editor import receipt contains an empty, absolute, or project-escaping artifact path.");
            }
            if (pathStatus == ReceiptArtifactPathStatus.Missing)
            {
                return UnityImportReceiptEvidence.Failed(
                    "CriticalArtifactMissing",
                    "UNITY_EDITOR_IMPORT_RECEIPT_ARTIFACT_MISSING",
                    "A critical artifact referenced by the Unity Editor import receipt is missing: " + requiredPaths[i]);
            }
        }

        if (!string.IsNullOrWhiteSpace(receipt.material?.materialPath))
        {
            ReceiptArtifactPathStatus materialPathStatus = ValidateReceiptArtifactPath(rootPath, receipt.material.materialPath);
            if (materialPathStatus != ReceiptArtifactPathStatus.Ready)
            {
                return UnityImportReceiptEvidence.Failed(
                    materialPathStatus == ReceiptArtifactPathStatus.Invalid ? "Invalid" : "CriticalArtifactMissing",
                    materialPathStatus == ReceiptArtifactPathStatus.Invalid
                        ? "UNITY_EDITOR_IMPORT_RECEIPT_PATH_INVALID"
                        : "UNITY_EDITOR_IMPORT_RECEIPT_ARTIFACT_MISSING",
                    "The material artifact referenced by the Unity Editor import receipt is invalid or missing.");
            }
        }

        if (receiptV3)
        {
            for (int i = 0; receipt.materials != null && i < receipt.materials.Length; i++)
            {
                CharacterUnityEditorImportReceiptMaterial material = receipt.materials[i];
                if (material == null
                    || material.assignedSlotCount <= 0
                    || (string.Equals(material.status, "Assigned", StringComparison.Ordinal) == false
                        && string.Equals(material.status, "bodyMaterialBound", StringComparison.Ordinal) == false)
                    || ValidateReceiptArtifactPath(rootPath, material.materialPath) != ReceiptArtifactPathStatus.Ready)
                {
                    return UnityImportReceiptEvidence.Failed(
                        "Invalid",
                        "UNITY_EDITOR_IMPORT_RECEIPT_MATERIALS_INVALID",
                        "Unity Editor import receipt materials[] is incomplete, unassigned, or references a missing material.");
                }

                string surfaceMode = CharacterMaterialSurfaceModeIds.Normalize(material.surfaceMode);
                if (string.IsNullOrWhiteSpace(surfaceMode)
                    || (string.Equals(surfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                        && (float.IsNaN(material.alphaCutoff)
                            || float.IsInfinity(material.alphaCutoff)
                            || material.alphaCutoff < 0f
                            || material.alphaCutoff > 1f)))
                {
                    return UnityImportReceiptEvidence.Failed(
                        "Invalid",
                        "UNITY_EDITOR_IMPORT_RECEIPT_MATERIAL_SURFACE_INVALID",
                        "Unity Editor import receipt material surface evidence is unsupported or has an invalid alpha cutoff.");
                }
            }
        }

        if (!ValidateSocketBakeArtifacts(rootPath, packageId,
                receipt.socketBake.indexPath, receipt.socketBake.indexHash,
                receipt.socketBake.binaryPath, receipt.socketBake.binaryHash, receipt.socketBake.binarySize,
                out string socketBakeError))
        {
            return UnityImportReceiptEvidence.Failed("Invalid",
                "UNITY_SOCKET_BAKE_ARTIFACT_MISMATCH", socketBakeError);
        }
        return UnityImportReceiptEvidence.Current(receipt);
    }

    private static bool ValidateCurrentUnitySessionArtifacts(
        string rootPath,
        string packageId,
        string unityCatalogRelative,
        string traceBakeRelative,
        CharacterUnityImportEditorStatus status,
        out string error)
    {
        error = string.Empty;
        if (status == null ||
            !string.Equals(status.packageId, packageId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(status.builderVersion) ||
            string.IsNullOrWhiteSpace(status.artifactHash) ||
            !IsValidSessionAvatar(status) ||
            string.IsNullOrWhiteSpace(status.avatarSourceGuid) ||
            !string.Equals(status.weaponTraceBakeStatus, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(status.weaponTraceBakeHash) ||
            !string.Equals(status.socketBakeStatus, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(status.socketBakeHash) ||
            status.socketBakeIssueCount != 0 ||
            !string.Equals(status.socketBakeFormat, "mx.character.socketBakeCatalog.v3", StringComparison.Ordinal) ||
            !string.Equals(status.socketBakeIndexPath, "generated/character_socket_bake_catalog.json", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(status.socketBakeIndexHash) ||
            !string.Equals(status.socketBakeBinaryPath, "generated/character_socket_bake_catalog.bytes", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(status.socketBakeBinaryHash) ||
            status.socketBakeBinarySize <= 0 ||
            !string.Equals(status.hurtboxBakeStatus, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(status.hurtboxBakeHash) ||
            status.hurtboxBakeIssueCount != 0)
        {
            error = "The current Unity Editor session is missing package, builder, artifact, Avatar, Weapon Trace Bake, Socket Bake, or Hurtbox Bake acceptance fields.";
            return false;
        }

        string[] requiredPaths =
        {
            status.prefabPath,
            status.runtimePrefabPath,
            status.avatarSourceAssetPath,
            status.characterActionProfilePath,
            status.characterActionSetPath,
            unityCatalogRelative,
            traceBakeRelative,
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/" + status.socketBakeIndexPath,
            "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/" + status.socketBakeBinaryPath
            ,"Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/generated/character_hurtbox_bake_catalog.json"
        };
        for (int i = 0; i < requiredPaths.Length; i++)
        {
            ReceiptArtifactPathStatus pathStatus = ValidateReceiptArtifactPath(rootPath, requiredPaths[i]);
            if (pathStatus != ReceiptArtifactPathStatus.Ready)
            {
                error = pathStatus == ReceiptArtifactPathStatus.Invalid
                    ? "The current Unity Editor session contains an invalid project artifact path."
                    : "A critical artifact accepted by the current Unity Editor session is missing: " + requiredPaths[i];
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(status.bodyMaterialPath) &&
            ValidateReceiptArtifactPath(rootPath, status.bodyMaterialPath) != ReceiptArtifactPathStatus.Ready)
        {
            error = "The material accepted by the current Unity Editor session is invalid or missing.";
            return false;
        }

        return ValidateSocketBakeArtifacts(rootPath, packageId,
            status.socketBakeIndexPath, status.socketBakeIndexHash,
            status.socketBakeBinaryPath, status.socketBakeBinaryHash, status.socketBakeBinarySize,
            out error);
    }

    private static bool IsValidReceiptAvatar(CharacterUnityEditorImportReceiptAvatar avatar, bool legacyV1)
    {
        if (avatar == null || !string.Equals(avatar.status, "Valid", StringComparison.Ordinal))
            return false;
        if (legacyV1)
            return string.Equals(avatar.humanoidStatus, "ValidHuman", StringComparison.Ordinal);
        if (string.Equals(avatar.rigType, "Humanoid", StringComparison.Ordinal))
            return string.Equals(avatar.rigStatus, "ValidHuman", StringComparison.Ordinal);
        if (string.Equals(avatar.rigType, "Generic", StringComparison.Ordinal))
            return string.Equals(avatar.rigStatus, "ValidGeneric", StringComparison.Ordinal);
        return false;
    }

    private static bool IsValidSessionAvatar(CharacterUnityImportEditorStatus status)
    {
        if (status == null || !string.Equals(status.avatarSourceStatus, "Valid", StringComparison.Ordinal))
            return false;
        string rigType = status.rigType;
        string rigStatus = string.IsNullOrWhiteSpace(status.avatarSourceRigStatus)
            ? status.avatarSourceHumanoidStatus
            : status.avatarSourceRigStatus;
        if (string.IsNullOrWhiteSpace(rigType) && string.Equals(status.avatarSourceHumanoidStatus, "ValidHuman", StringComparison.Ordinal))
            rigType = "Humanoid";
        return string.Equals(rigType, "Humanoid", StringComparison.Ordinal)
            ? string.Equals(rigStatus, "ValidHuman", StringComparison.Ordinal)
            : string.Equals(rigType, "Generic", StringComparison.Ordinal)
                && string.Equals(rigStatus, "ValidGeneric", StringComparison.Ordinal);
    }

    private static ReceiptArtifactPathStatus ValidateReceiptArtifactPath(string rootPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return ReceiptArtifactPathStatus.Invalid;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return ReceiptArtifactPathStatus.Invalid;
        }

        if (!IsPathAtOrUnderRoot(rootPath, fullPath))
            return ReceiptArtifactPathStatus.Invalid;
        return File.Exists(fullPath) ? ReceiptArtifactPathStatus.Ready : ReceiptArtifactPathStatus.Missing;
    }

    private static bool HashEquals(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyReceiptMetrics(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityEditorImportReceipt receipt)
    {
        if (stage == null || receipt == null)
            return;

        stage.Metrics["builderVersion"] = receipt.builderVersion ?? string.Empty;
        stage.Metrics["artifactHash"] = receipt.hashes?.unityArtifactHash ?? string.Empty;
        stage.Metrics["rigType"] = receipt.avatar?.rigType ?? (string.Equals(receipt.avatar?.humanoidStatus, "ValidHuman", StringComparison.Ordinal) ? "Humanoid" : string.Empty);
        stage.Metrics["avatarRigStatus"] = receipt.avatar?.rigStatus ?? receipt.avatar?.humanoidStatus ?? string.Empty;
        stage.Metrics["materialCount"] = (receipt.materials?.Length ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["characterActionProfileReady"] = receipt.characterAction?.profileReady == true ? "true" : "false";
        stage.Metrics["characterActionActionSetReady"] = receipt.characterAction?.actionSetReady == true ? "true" : "false";
        stage.Metrics["weaponTraceBakeStatus"] = receipt.weaponTraceBake?.status ?? string.Empty;
        stage.Metrics["weaponTraceBakeArtifactCount"] = (receipt.weaponTraceBake?.artifactCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["weaponTraceBakeCheckedFrameCount"] = (receipt.weaponTraceBake?.checkedFrameCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["weaponTraceBakeIssueCount"] = (receipt.weaponTraceBake?.issueCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["socketBakeStatus"] = receipt.socketBake?.status ?? string.Empty;
        stage.Metrics["socketBakeClipCount"] = (receipt.socketBake?.clipCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["socketBakeSocketTrackCount"] = (receipt.socketBake?.socketTrackCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["socketBakeCheckedFrameCount"] = (receipt.socketBake?.checkedFrameCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["socketBakeIssueCount"] = (receipt.socketBake?.issueCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["hurtboxBakeStatus"] = receipt.hurtboxBake?.status ?? string.Empty;
        stage.Metrics["hurtboxBakeClipCount"] = (receipt.hurtboxBake?.clipCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["hurtboxBakeColliderCount"] = (receipt.hurtboxBake?.colliderCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["hurtboxBakeShapeCount"] = (receipt.hurtboxBake?.shapeCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["hurtboxBakeCheckedFrameCount"] = (receipt.hurtboxBake?.checkedFrameCount ?? 0).ToString(CultureInfo.InvariantCulture);
        stage.Metrics["hurtboxBakeIssueCount"] = (receipt.hurtboxBake?.issueCount ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    private static void AddSocketBakeReadinessIssues(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityImportEditorStatus status,
        string sourcePath,
        string toolUrl)
    {
        CharacterUnityImportEditorWeaponTraceBakeIssue[] issues = status?.socketBakeIssues;
        for (int i = 0; issues != null && i < issues.Length; i++)
            AddBakeReadinessIssue(stage, issues[i], "UNITY_SOCKET_BAKE_FAILED", "Socket Bake failed.", sourcePath, toolUrl);
    }

    private static void AddWeaponTraceBakeReadinessIssues(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityImportEditorStatus status,
        string sourcePath,
        string toolUrl)
    {
        CharacterUnityImportEditorWeaponTraceBakeIssue[] issues = status?.weaponTraceBakeIssues;
        for (int i = 0; issues != null && i < issues.Length; i++)
            AddBakeReadinessIssue(stage, issues[i], "UNITY_WEAPON_TRACE_BAKE_FAILED", "Weapon Trace Bake failed.", sourcePath, toolUrl);
    }

    private static void AddHurtboxBakeReadinessIssues(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityImportEditorStatus status,
        string sourcePath,
        string toolUrl)
    {
        CharacterUnityImportEditorWeaponTraceBakeIssue[] issues = status?.hurtboxBakeIssues;
        for (int i = 0; issues != null && i < issues.Length; i++)
            AddBakeReadinessIssue(stage, issues[i], "UNITY_HURTBOX_BAKE_FAILED", "Hurtbox Bake failed.", sourcePath, toolUrl);
    }

    private static void AddReceiptBakeReadinessIssues(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityEditorImportReceipt receipt,
        string sourcePath,
        string toolUrl)
    {
        CharacterUnityImportEditorWeaponTraceBakeIssue[] weaponIssues = receipt?.weaponTraceBake?.issues;
        for (int i = 0; weaponIssues != null && i < weaponIssues.Length; i++)
            AddBakeReadinessIssue(stage, weaponIssues[i], "UNITY_WEAPON_TRACE_BAKE_FAILED", "Weapon Trace Bake failed.", sourcePath, toolUrl);

        CharacterUnityImportEditorWeaponTraceBakeIssue[] socketIssues = receipt?.socketBake?.issues;
        for (int i = 0; socketIssues != null && i < socketIssues.Length; i++)
            AddBakeReadinessIssue(stage, socketIssues[i], "UNITY_SOCKET_BAKE_FAILED", "Socket Bake failed.", sourcePath, toolUrl);

        CharacterUnityImportEditorWeaponTraceBakeIssue[] hurtboxIssues = receipt?.hurtboxBake?.issues;
        for (int i = 0; hurtboxIssues != null && i < hurtboxIssues.Length; i++)
            AddBakeReadinessIssue(stage, hurtboxIssues[i], "UNITY_HURTBOX_BAKE_FAILED", "Hurtbox Bake failed.", sourcePath, toolUrl);
    }

    private static void AddBakeReadinessIssue(
        CharacterAuthoringReadinessStage stage,
        CharacterUnityImportEditorWeaponTraceBakeIssue issue,
        string fallbackCode,
        string fallbackMessage,
        string sourcePath,
        string toolUrl)
    {
        if (stage == null || issue == null)
            return;
        CharacterAuthoringReadinessIssue readinessIssue = CreateIssue(
            "Error",
            string.IsNullOrWhiteSpace(issue.code) ? fallbackCode : issue.code,
            string.IsNullOrWhiteSpace(issue.message) ? fallbackMessage : issue.message,
            string.IsNullOrWhiteSpace(issue.sourcePath) ? sourcePath : issue.sourcePath,
            issue.sourceObjectPath,
            toolUrl);
        readinessIssue.SuggestedFix = BuildBakeIssueSuggestedFix(issue);
        stage.Blockers.Add(readinessIssue);
    }

    private static string BuildBakeIssueSuggestedFix(CharacterUnityImportEditorWeaponTraceBakeIssue issue)
    {
        if (issue == null)
            return string.Empty;
        if (string.Equals(issue.code, "HurtboxBakeBindingMissing", StringComparison.OrdinalIgnoreCase))
            return "Open the character body collider binding, select collider '" + issue.traceId + "', and bind it to a bone that exists in the imported model; then retry Unity Editor acceptance.";
        if (issue.code != null && issue.code.IndexOf("BindingMissing", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Repair the referenced bone, socket, locator, or trace binding in the Authoring source and retry Unity Editor acceptance.";
        return "Open the referenced Authoring source object, repair the reported bake input, and retry Unity Editor acceptance.";
    }

    private static void AddRuntimeValidationStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        CharacterAuthoringCompileResult compile,
        UnityImportReadiness importReadiness,
        string toolUrl)
    {
        if (snapshot.Stages.Exists(stage => string.Equals(stage.Id, "RuntimeValidation", StringComparison.Ordinal)))
            return;

        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "RuntimeValidation",
            Label = "Runtime 验证",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "查看交付流",
            Summary = "检查 CharacterPackageResolver 和导入报告是否允许运行时验证。"
        };
        stage.SourcePaths.Add("Assets/Scripts/WGameFrameworkProject/CharacterTest");
        stage.SourcePaths.Add("Assets/MxFrameworkGenerated/CharacterPackages/" + SafePackageId(snapshot.PackageId));

        if (compile == null)
        {
            stage.Status = ReadinessStatusUnknown;
            stage.Summary = "未能读取 Character compile 结果。";
            AddStage(snapshot, stage);
            return;
        }

        CharacterCompilerGateReport gate = compile.GateReport;
        AddValidationIssues(stage, compile.ResolverVerificationPlan?.Diagnostics, toolUrl);
        if (gate != null && gate.SpawnBlocked)
            AddValidationIssues(stage, gate.Issues, toolUrl);

        string resolverStatus = compile.ResolverVerificationPlan?.Status ?? ReadinessStatusUnknown;
        stage.Metrics["compileStatus"] = compile.Status.ToString();
        stage.Metrics["resolverStatus"] = resolverStatus;
        stage.Metrics["knownAbilityCount"] = (compile.ResolverVerificationPlan?.KnownAbilityIds?.Count ?? 0).ToString(CultureInfo.InvariantCulture);

        if ((gate != null && gate.SpawnBlocked) || stage.Blockers.Count > 0)
        {
            stage.Status = ReadinessStatusBlocked;
            stage.Summary = "运行时 resolver 或 Spawn gate 存在阻断项。";
        }
        else if (importReadiness.Blocked)
        {
            stage.Status = ReadinessStatusDeferred;
            stage.Summary = "等待上游 Unity 导入阻塞解除后再执行 Runtime 验证。";
            stage.Metrics["dependencyStatus"] = "BlockedByUnityImport";
        }
        else if (!importReadiness.Ready || !importReadiness.CanSpawnAfterImport)
        {
            stage.Status = ReadinessStatusDeferred;
            stage.Summary = "等待与当前 Compiler 身份匹配的 Unity 导入验收证据。";
            stage.ActionLabel = "执行 Unity 导入";
            stage.Metrics["evidenceSource"] = importReadiness.EvidenceSource;
        }
        else if (stage.Warnings.Count > 0)
        {
            stage.Status = ReadinessStatusWarning;
            stage.Summary = "Runtime resolver 可用，但存在警告。";
        }
        else
        {
            stage.Status = ReadinessStatusReady;
            stage.Summary = "导入报告允许 Spawn；可打开 CharacterTest/校准场景验证。";
        }

        AddStage(snapshot, stage);
    }

    private static void AddAbilityRuntimeBindingStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        CharacterAuthoringCompileResult compile,
        string rootPath,
        string toolUrl)
    {
        List<int> knownAbilityIds = compile?.ResolverVerificationPlan?.KnownAbilityIds ?? new List<int>();
        List<int> gameplayRuntimeAbilityIds = compile?.ResolverVerificationPlan?.GameplayRuntimeAbilityIds ?? new List<int>();
        var knownAbilitySet = new HashSet<int>(knownAbilityIds);
        AbilityActionBindingReadiness actionBindings = ReadAbilityActionBindings(rootPath, snapshot.PackageId);
        int abilityCount = knownAbilityIds.Count;
        var stage = new CharacterAuthoringReadinessStage
        {
            Id = "AbilityRuntimeBinding",
            Label = "能力运行时绑定",
            PrimaryToolUrl = toolUrl,
            ActionLabel = "查看入游戏状态",
            SourcePaths =
            {
                "Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterGameplayRuntimeFoundation.cs",
                "Packages/com.vincent.wgameframework/Character.Application/CharacterPackageResolver.cs"
            }
        };
        for (int i = 0; i < actionBindings.SourcePaths.Count; i++)
            stage.SourcePaths.Add(actionBindings.SourcePaths[i]);

        stage.Metrics["knownAbilityCount"] = abilityCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["gameplayRuntimeAbilityCount"] = gameplayRuntimeAbilityIds.Count.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["actionBoundAbilityCount"] = actionBindings.BoundAbilityIds.Count.ToString(CultureInfo.InvariantCulture);

        int missingActionBindingCount = 0;

        var ungrantedBindings = new List<AbilityActionBindingRecord>();
        foreach (AbilityActionBindingRecord binding in actionBindings.Bindings)
        {
            if (binding.AbilityId <= 0 || knownAbilitySet.Contains(binding.AbilityId))
                continue;
            ungrantedBindings.Add(binding);
        }
        ungrantedBindings.Sort((a, b) => a.AbilityId.CompareTo(b.AbilityId));
        for (int i = 0; i < ungrantedBindings.Count && stage.Warnings.Count < 10; i++)
        {
            AbilityActionBindingRecord binding = ungrantedBindings[i];
            stage.Warnings.Add(CreateIssue(
                "Warning",
                "ABILITY_ACTION_BINDING_UNGRANTED",
                "abilityBindings[] maps CharacterAbilityId " + binding.AbilityId.ToString(CultureInfo.InvariantCulture) + " but the current character does not grant it.",
                binding.SourcePath,
                binding.SourceObjectPath,
                toolUrl));
        }

        for (int i = 0; i < actionBindings.MissingActionTargets.Count && stage.Warnings.Count < 12; i++)
        {
            AbilityActionBindingRecord binding = actionBindings.MissingActionTargets[i];
            stage.Warnings.Add(CreateIssue(
                "Warning",
                "ABILITY_ACTION_TARGET_MISSING",
                "abilityBindings[] maps CharacterAbilityId " + binding.AbilityId.ToString(CultureInfo.InvariantCulture) + " to missing actionId '" + binding.ActionId + "'.",
                binding.SourcePath,
                binding.SourceObjectPath,
                toolUrl));
        }

        stage.Metrics["missingActionBindingCount"] = missingActionBindingCount.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["ungrantedActionBindingCount"] = ungrantedBindings.Count.ToString(CultureInfo.InvariantCulture);
        stage.Metrics["missingActionTargetCount"] = actionBindings.MissingActionTargets.Count.ToString(CultureInfo.InvariantCulture);

        if (gameplayRuntimeAbilityIds.Count > 0)
        {
            stage.Status = ReadinessStatusReady;
            stage.Summary = missingActionBindingCount > 0
                ? "角色已授予能力，但部分 abilityId 还没有绑定 CharacterAction；Runtime registry 仍需要显式映射。"
                : "能力已绑定 CharacterAction；Gameplay registry 仍需要显式 CharacterGameplayAbilityBindingCatalog 映射。";
            stage.Warnings.Add(CreateIssue(
                "Info",
                "ABILITY_GAMEPLAY_RUNTIME_BINDING_DEFERRED",
                "Only abilities explicitly marked GameplayRuntime require CharacterGameplayAbilityBindingCatalog mappings. This optional integration does not block authoring compile or Unity import.",
                "Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterGameplayRuntimeFoundation.cs",
                "CharacterGameplayAbilityRegistryBuilder.BuildDeferred",
                toolUrl));
        }
        else if (abilityCount == 0 && actionBindings.BoundAbilityIds.Count > 0)
        {
            stage.Status = ReadinessStatusWarning;
            stage.Summary = "动作配置里存在 abilityBindings[]，但当前角色没有解析到授予能力。";
        }
        else
        {
            stage.Status = ReadinessStatusReady;
            stage.Summary = "当前角色没有解析到需要运行时注册的能力 ID。";
        }

        AddStage(snapshot, stage);
    }

    internal static AbilityActionBindingReadiness ReadAbilityActionBindings(string rootPath, string packageId)
    {
        var readiness = new AbilityActionBindingReadiness();
        string actionSetRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets");
        if (!string.IsNullOrWhiteSpace(packageId))
        {
            string candidate = Path.Combine(actionSetRoot, packageId);
            if (!Directory.Exists(candidate) && string.Equals(packageId, "codex_flow_test", StringComparison.OrdinalIgnoreCase))
                candidate = Path.Combine(actionSetRoot, "000001");
            actionSetRoot = candidate;
        }
        if (!Directory.Exists(actionSetRoot))
            return readiness;

        HashSet<int> knownActions = ReadGeneratedCharacterActionIds(rootPath);
        string[] files = Directory.GetFiles(actionSetRoot, "character_action_sets.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string sourcePath = NormalizeProjectPath(Path.GetRelativePath(rootPath, file));
            readiness.SourcePaths.Add(sourcePath);
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("actionSets", out JsonElement actionSets) || actionSets.ValueKind != JsonValueKind.Array)
                    continue;

                int setIndex = 0;
                foreach (JsonElement actionSet in actionSets.EnumerateArray())
                {
                    string actionSetStableId = GetJsonString(actionSet, "stableId");
                    if (!actionSet.TryGetProperty("abilityBindings", out JsonElement bindings) || bindings.ValueKind != JsonValueKind.Array)
                    {
                        setIndex++;
                        continue;
                    }

                    int bindingIndex = 0;
                    foreach (JsonElement binding in bindings.EnumerateArray())
                    {
                        int abilityId = GetJsonFlexibleInt(binding, "abilityId");
                        int actionId = GetJsonFlexibleInt(binding, "actionId");
                        var record = new AbilityActionBindingRecord
                        {
                            AbilityId = abilityId,
                            ActionId = actionId,
                            ActionSetStableId = actionSetStableId,
                            ActionExists = actionId > 0 && (knownActions.Count == 0 || knownActions.Contains(actionId)),
                            SourcePath = sourcePath,
                            SourceObjectPath = "actionSets[" + setIndex.ToString(CultureInfo.InvariantCulture) + "].abilityBindings[" + bindingIndex.ToString(CultureInfo.InvariantCulture) + "]"
                        };
                        readiness.Bindings.Add(record);
                        if (abilityId > 0)
                            readiness.BoundAbilityIds.Add(abilityId);
                        if (!record.ActionExists)
                            readiness.MissingActionTargets.Add(record);

                        bindingIndex++;
                    }

                    setIndex++;
                }
            }
            catch (JsonException)
            {
                // ActionTimeline stage reports invalid action JSON. This stage only correlates valid abilityBindings.
            }
            catch (IOException)
            {
                // Treat transient reads as unavailable instead of blocking the whole readiness snapshot.
            }
        }

        return readiness;
    }

    private static HashSet<int> ReadGeneratedCharacterActionIds(string rootPath)
    {
        var result = new HashSet<int>();
        string characterActionRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterActions");
        if (!Directory.Exists(characterActionRoot))
            return result;

        string[] files = Directory.GetFiles(characterActionRoot, "character_actions.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(files[i]));
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("actions", out JsonElement actions) || actions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement action in actions.EnumerateArray())
                {
                    int actionId = GetJsonFlexibleInt(action, "actionId");
                    if (actionId > 0)
                        result.Add(actionId);
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return result;
    }

    private static void AddUnavailableStages(
        CharacterAuthoringReadinessSnapshot snapshot,
        string resourceLibraryUrl,
        string animationEditorUrl,
        string actionToolUrl,
        string hubUrl)
    {
        AddStage(snapshot, CreateUnavailableStage("ResourceBinding", "资源绑定", resourceLibraryUrl, "打开资源管理器"));
        AddStage(snapshot, CreateUnavailableStage("AnimationBinding", "动画绑定", animationEditorUrl, "打开动画编辑器"));
        AddStage(snapshot, CreateUnavailableStage("ReactionBinding", "Reaction Binding", hubUrl, "Open CharacterStudio"));
        AddStage(snapshot, CreateUnavailableStage("ActionTimeline", "动作时间轴", actionToolUrl, "打开伤害分析"));
        AddStage(snapshot, CreateUnavailableStage("UnityImport", "Unity 导入", hubUrl, "查看交付流"));
        AddStage(snapshot, CreateUnavailableStage("RuntimeValidation", "Runtime 验证", hubUrl, "查看交付流"));
    }

    private static CharacterAuthoringReadinessStage CreateUnavailableStage(
        string id,
        string label,
        string toolUrl,
        string actionLabel)
    {
        return new CharacterAuthoringReadinessStage
        {
            Id = id,
            Label = label,
            Status = ReadinessStatusUnknown,
            Summary = "等待角色包读取成功后评估。",
            PrimaryToolUrl = toolUrl,
            ActionLabel = actionLabel
        };
    }

    private static void AddStage(
        CharacterAuthoringReadinessSnapshot snapshot,
        CharacterAuthoringReadinessStage stage)
    {
        if (snapshot == null || stage == null)
            return;

        DeduplicateReadinessIssues(stage.Blockers, includeObjectPath: true);
        DeduplicateReadinessIssues(stage.Warnings, includeObjectPath: true);
        snapshot.Stages.Add(stage);
        for (int i = 0; i < stage.Blockers.Count; i++)
            AddUniqueReadinessIssue(snapshot.Blockers, stage.Blockers[i], includeObjectPath: false);
        for (int i = 0; i < stage.Warnings.Count; i++)
            if (!string.Equals(stage.Warnings[i]?.Severity, "Info", StringComparison.OrdinalIgnoreCase))
                AddUniqueReadinessIssue(snapshot.Warnings, stage.Warnings[i], includeObjectPath: false);
        for (int i = 0; i < stage.ReportRefs.Count; i++)
            snapshot.ReportRefs.Add(stage.ReportRefs[i]);
    }

    private static void DeduplicateReadinessIssues(
        List<CharacterAuthoringReadinessIssue> issues,
        bool includeObjectPath)
    {
        if (issues == null || issues.Count < 2)
            return;

        var unique = new List<CharacterAuthoringReadinessIssue>(issues.Count);
        for (int i = 0; i < issues.Count; i++)
            AddUniqueReadinessIssue(unique, issues[i], includeObjectPath);
        issues.Clear();
        issues.AddRange(unique);
    }

    private static void AddUniqueReadinessIssue(
        List<CharacterAuthoringReadinessIssue> issues,
        CharacterAuthoringReadinessIssue issue,
        bool includeObjectPath)
    {
        if (issues == null || issue == null)
            return;

        string identity = BuildReadinessIssueIdentity(issue, includeObjectPath);
        for (int i = 0; i < issues.Count; i++)
        {
            if (string.Equals(
                BuildReadinessIssueIdentity(issues[i], includeObjectPath),
                identity,
                StringComparison.Ordinal))
            {
                return;
            }
        }
        issues.Add(issue);
    }

    private static string BuildReadinessIssueIdentity(
        CharacterAuthoringReadinessIssue issue,
        bool includeObjectPath)
    {
        if (issue == null)
            return string.Empty;

        return string.Join("|",
            issue.Code ?? string.Empty,
            issue.Message ?? string.Empty,
            issue.SourcePath ?? string.Empty,
            includeObjectPath ? issue.SourceObjectPath ?? string.Empty : string.Empty);
    }

    private static void FinalizeReadiness(CharacterAuthoringReadinessSnapshot snapshot)
    {
        if (snapshot.Blockers.Count > 0 || snapshot.Stages.Exists(stage => string.Equals(stage.Status, ReadinessStatusBlocked, StringComparison.Ordinal)))
        {
            snapshot.OverallStatus = ReadinessStatusBlocked;
        }
        else if (snapshot.Stages.Exists(stage => string.Equals(stage.Status, ReadinessStatusDeferred, StringComparison.Ordinal)))
        {
            snapshot.OverallStatus = ReadinessStatusDeferred;
        }
        else if (snapshot.Warnings.Count > 0 || snapshot.Stages.Exists(stage => string.Equals(stage.Status, ReadinessStatusWarning, StringComparison.Ordinal)))
        {
            snapshot.OverallStatus = ReadinessStatusWarning;
        }
        else if (snapshot.Stages.Exists(stage => string.Equals(stage.Status, ReadinessStatusUnknown, StringComparison.Ordinal)))
        {
            snapshot.OverallStatus = ReadinessStatusUnknown;
        }
        else
        {
            snapshot.OverallStatus = ReadinessStatusReady;
        }

        snapshot.NextAction = BuildReadinessNextAction(snapshot);
    }

    private static CharacterAuthoringReadinessNextAction BuildReadinessNextAction(
        CharacterAuthoringReadinessSnapshot snapshot)
    {
        if (snapshot == null)
            return new CharacterAuthoringReadinessNextAction();

        for (int i = 0; i < snapshot.Stages.Count; i++)
        {
            CharacterAuthoringReadinessStage stage = snapshot.Stages[i];
            if (stage == null || stage.Blockers == null || stage.Blockers.Count == 0)
                continue;
            CharacterAuthoringReadinessIssue issue = stage.Blockers.Find(
                candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.SuggestedFix))
                ?? stage.Blockers[0];
            return new CharacterAuthoringReadinessNextAction
            {
                Id = string.Equals(stage.Id, "UnityImport", StringComparison.Ordinal)
                    ? "repair_unity_import"
                    : string.Equals(stage.Id, "ResourceBinding", StringComparison.Ordinal)
                        ? "repair_resource_binding"
                        : "repair_authoring_issue",
                Label = string.IsNullOrWhiteSpace(issue?.SuggestedFix)
                    ? (string.IsNullOrWhiteSpace(stage.ActionLabel) ? "处理当前阻塞项" : stage.ActionLabel)
                    : issue.SuggestedFix,
                Owner = string.Equals(stage.Id, "ResourceBinding", StringComparison.Ordinal)
                    ? "ResourceManager"
                    : string.Equals(stage.Id, "UnityImport", StringComparison.Ordinal)
                        ? "UnityImportBridge"
                        : "CharacterStudio",
                CanRun = false,
                StageId = stage.Id,
                ReasonCode = issue?.Code ?? string.Empty,
                Reason = issue?.Message ?? stage.Summary,
                TargetPath = FirstNonEmpty(issue?.SourceObjectPath, issue?.SourcePath),
                ToolUrl = FirstNonEmpty(issue?.ToolUrl, stage.PrimaryToolUrl),
                FollowUpId = string.Equals(stage.Id, "UnityImport", StringComparison.Ordinal)
                    ? "import_unity"
                    : "compile_character"
            };
        }

        CharacterAuthoringReadinessStage unityStage = snapshot.Stages.Find(
            stage => stage != null && string.Equals(stage.Id, "UnityImport", StringComparison.Ordinal));
        if (unityStage != null && string.Equals(unityStage.Status, ReadinessStatusDeferred, StringComparison.Ordinal))
        {
            return new CharacterAuthoringReadinessNextAction
            {
                Id = "import_unity",
                Label = "执行完整 Unity 导入与 Editor 验收",
                Owner = "UnityImportBridge",
                CanRun = true,
                StageId = unityStage.Id,
                Method = "POST",
                Endpoint = "/api/character/import-unity",
                Reason = unityStage.Summary,
                ToolUrl = unityStage.PrimaryToolUrl
            };
        }

        if (string.Equals(snapshot.OverallStatus, ReadinessStatusReady, StringComparison.Ordinal))
        {
            return new CharacterAuthoringReadinessNextAction
            {
                Id = "validate_runtime",
                Label = "进入游戏验证",
                Owner = "CharacterTest",
                CanRun = true,
                StageId = "RuntimeValidation",
                Reason = "Authoring、Unity Editor 和 Spawn 门禁均已通过。"
            };
        }

        return new CharacterAuthoringReadinessNextAction
        {
            Id = "save_and_compile",
            Label = "保存全部草稿并重新预检",
            Owner = "CharacterStudio",
            CanRun = true,
            Method = "POST",
            Endpoint = "/api/character/compile",
            Reason = "当前角色尚未达到 Unity 导入条件。"
        };
    }

    private static void AddValidationIssues(
        CharacterAuthoringReadinessStage stage,
        IList<CharacterAuthoringValidationIssue> issues,
        string toolUrl)
    {
        if (stage == null || issues == null)
            return;

        for (int i = 0; i < issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = issues[i];
            if (issue == null)
                continue;

            CharacterAuthoringReadinessIssue readinessIssue = CreateIssue(
                issue.Severity.ToString(),
                issue.Code,
                issue.Message,
                issue.SourcePath,
                issue.SourceObjectPath,
                toolUrl);
            readinessIssue.Field = issue.Field;
            readinessIssue.SuggestedFix = issue.SuggestedFix;

            if (issue.Severity == CharacterAuthoringValidationSeverity.Error ||
                issue.Gate == CharacterAuthoringValidationGate.ExportBlocked ||
                issue.Gate == CharacterAuthoringValidationGate.ImportBlocked ||
                issue.Gate == CharacterAuthoringValidationGate.SpawnBlocked)
            {
                stage.Blockers.Add(readinessIssue);
            }
            else
            {
                stage.Warnings.Add(readinessIssue);
            }
        }
    }

    private static void AddResourcePlanDiagnostics(
        CharacterAuthoringReadinessStage stage,
        IList<CharacterResourcePlanDiagnostic> diagnostics,
        string toolUrl)
    {
        if (stage == null || diagnostics == null)
            return;

        for (int i = 0; i < diagnostics.Count; i++)
        {
            CharacterResourcePlanDiagnostic diagnostic = diagnostics[i];
            if (diagnostic == null)
                continue;

            CharacterAuthoringReadinessIssue issue = CreateIssue(
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourceConfigKind,
                diagnostic.SourceField,
                toolUrl);
            issue.SuggestedFix = diagnostic.SuggestedFix;
            if (IsErrorSeverity(diagnostic.Severity))
                stage.Blockers.Add(issue);
            else
                stage.Warnings.Add(issue);
        }
    }

    private static CharacterAuthoringReadinessIssue CreateIssue(
        string severity,
        string code,
        string message,
        string sourcePath,
        string sourceObjectPath,
        string toolUrl)
    {
        return new CharacterAuthoringReadinessIssue
        {
            Severity = string.IsNullOrWhiteSpace(severity) ? "Info" : severity,
            Code = string.IsNullOrWhiteSpace(code) ? "READINESS_DIAGNOSTIC" : code,
            Message = message ?? string.Empty,
            SourcePath = sourcePath ?? string.Empty,
            SourceObjectPath = sourceObjectPath ?? string.Empty,
            ToolUrl = toolUrl ?? string.Empty
        };
    }

    private static bool IsErrorSeverity(string severity)
    {
        return !string.IsNullOrWhiteSpace(severity) &&
            severity.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountPlanResources(CharacterResourcePlanGroup group)
    {
        return group?.Resources?.Count ?? 0;
    }

    private static int CountJsonArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return element.GetArrayLength();
    }

    private static int CountJsonArrayItemsRecursive(JsonElement root, string propertyName)
    {
        int count = 0;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.Ordinal) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    count += property.Value.GetArrayLength();
                }

                count += CountJsonArrayItemsRecursive(property.Value, propertyName);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in root.EnumerateArray())
                count += CountJsonArrayItemsRecursive(item, propertyName);
        }

        return count;
    }

    private static int CountActionDamageEvents(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("actions", out JsonElement actions) ||
            actions.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        int count = 0;
        foreach (JsonElement action in actions.EnumerateArray())
        {
            count += CountTrackEvents(action, "combatTrack", "StartHitTrace");
            count += CountTrackEvents(action, "gameplayTrack", "ApplyGameplayEffect");
        }

        return count;
    }

    private static int CountTrackEvents(JsonElement action, string trackName, string kind)
    {
        if (action.ValueKind != JsonValueKind.Object ||
            !action.TryGetProperty(trackName, out JsonElement track) ||
            track.ValueKind != JsonValueKind.Object ||
            !track.TryGetProperty("events", out JsonElement events) ||
            events.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        int count = 0;
        foreach (JsonElement evt in events.EnumerateArray())
        {
            if (evt.ValueKind == JsonValueKind.Object &&
                string.Equals(GetJsonString(evt, "kind"), kind, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetJsonString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty
                : string.Empty;
    }

    private static int GetJsonInt(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out int value)
                ? value
                : 0;
    }

    private static int GetJsonFlexibleInt(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement element))
        {
            return 0;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number))
            return number;
        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int text))
            return text;
        return 0;
    }

    private static bool GetJsonBool(JsonElement root, string propertyName, bool fallback)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement element))
        {
            return fallback;
        }

        if (element.ValueKind == JsonValueKind.True)
            return true;
        if (element.ValueKind == JsonValueKind.False)
            return false;
        return fallback;
    }

    private static string JoinProjectPath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right ?? string.Empty;
        if (string.IsNullOrWhiteSpace(right))
            return left ?? string.Empty;
        return left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\');
    }

    private static string SafePackageId(string packageId)
    {
        return string.IsNullOrWhiteSpace(packageId) ? "<packageId>" : packageId;
    }

    private static string NormalizeProjectPath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private readonly struct UnityImportReadiness
    {
        public UnityImportReadiness(
            bool ready,
            bool canSpawnAfterImport,
            bool blocked,
            string evidenceSource)
        {
            Ready = ready;
            CanSpawnAfterImport = canSpawnAfterImport;
            Blocked = blocked;
            EvidenceSource = evidenceSource ?? string.Empty;
        }

        public bool Ready { get; }
        public bool CanSpawnAfterImport { get; }
        public bool Blocked { get; }
        public string EvidenceSource { get; }
    }

    private enum ReceiptArtifactPathStatus
    {
        Ready,
        Invalid,
        Missing
    }

    private readonly struct UnityImportReceiptEvidence
    {
        private UnityImportReceiptEvidence(
            string status,
            bool ready,
            bool hardFailure,
            string errorCode,
            string message,
            CharacterUnityEditorImportReceipt receipt)
        {
            Status = status ?? string.Empty;
            Ready = ready;
            HardFailure = hardFailure;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Receipt = receipt;
        }

        public string Status { get; }
        public bool Ready { get; }
        public bool HardFailure { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public CharacterUnityEditorImportReceipt Receipt { get; }

        public static UnityImportReceiptEvidence Current(CharacterUnityEditorImportReceipt receipt)
        {
            return new UnityImportReceiptEvidence("Current", true, false, string.Empty, string.Empty, receipt);
        }

        public static UnityImportReceiptEvidence Deferred(string status)
        {
            return new UnityImportReceiptEvidence(status, false, false, string.Empty, string.Empty, null);
        }

        public static UnityImportReceiptEvidence Failed(string status, string errorCode, string message)
        {
            return new UnityImportReceiptEvidence(status, false, true, errorCode, message, null);
        }

        public static UnityImportReceiptEvidence Rejected(
            CharacterUnityEditorImportReceipt receipt,
            string errorCode,
            string message)
        {
            return new UnityImportReceiptEvidence("Rejected", false, true, errorCode, message, receipt);
        }
    }

    internal sealed class AbilityActionBindingReadiness
    {
        public List<string> SourcePaths { get; } = new List<string>();
        public List<AbilityActionBindingRecord> Bindings { get; } = new List<AbilityActionBindingRecord>();
        public HashSet<int> BoundAbilityIds { get; } = new HashSet<int>();
        public List<AbilityActionBindingRecord> MissingActionTargets { get; } = new List<AbilityActionBindingRecord>();
    }

    internal sealed class AbilityActionBindingRecord
    {
        public int AbilityId { get; set; }
        public int ActionId { get; set; }
        public string ActionSetStableId { get; set; } = string.Empty;
        public bool ActionExists { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
    }
}

internal sealed class CharacterAuthoringReadinessSnapshot
{
    public string PackageRelative { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string OverallStatus { get; set; } = "Unknown";
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public List<CharacterAuthoringReadinessStage> Stages { get; set; } = new List<CharacterAuthoringReadinessStage>();
    public List<CharacterAuthoringReadinessIssue> Blockers { get; set; } = new List<CharacterAuthoringReadinessIssue>();
    public List<CharacterAuthoringReadinessIssue> Warnings { get; set; } = new List<CharacterAuthoringReadinessIssue>();
    public List<CharacterAuthoringReadinessReportRef> ReportRefs { get; set; } = new List<CharacterAuthoringReadinessReportRef>();
    public CharacterAuthoringReadinessNextAction NextAction { get; set; } = new CharacterAuthoringReadinessNextAction();
}

internal sealed class CharacterAuthoringReadinessNextAction
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool CanRun { get; set; }
    public string StageId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string ToolUrl { get; set; } = string.Empty;
    public string FollowUpId { get; set; } = string.Empty;
}

internal sealed class CharacterAuthoringReadinessStage
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string Summary { get; set; } = string.Empty;
    public string PrimaryToolUrl { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public List<string> SourcePaths { get; set; } = new List<string>();
    public List<CharacterAuthoringReadinessReportRef> ReportRefs { get; set; } = new List<CharacterAuthoringReadinessReportRef>();
    public List<CharacterAuthoringReadinessIssue> Blockers { get; set; } = new List<CharacterAuthoringReadinessIssue>();
    public List<CharacterAuthoringReadinessIssue> Warnings { get; set; } = new List<CharacterAuthoringReadinessIssue>();
    public Dictionary<string, string> Metrics { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

internal sealed class CharacterAuthoringReadinessIssue
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceObjectPath { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public string ToolUrl { get; set; } = string.Empty;
}

internal sealed class CharacterAuthoringReadinessReportRef
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ToolUrl { get; set; } = string.Empty;
}
