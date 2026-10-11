using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string ProjectBuffPackagesRelativePath = "Authoring/BuffPackages";
    private const string GeneratedBuffRootRelativePath = "Assets/MxFrameworkGenerated/Gameplay/Buffs";
    private const string GeneratedAuraRootRelativePath = "Assets/MxFrameworkGenerated/Combat/Auras";
    private const int BuffDurationFrameRate = 60;
    private const int BuffInstantFrameRateBase = 30;

    private static bool TryHandleBuffAuthoringApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/buffs/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListBuffAuthoringPackages(rootPath, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/options" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadBuffAuthoringOptions(rootPath), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/description-preview" && context.Request.HttpMethod == "POST")
        {
            BuffAuthoringDescriptionPreviewRequest request =
                ReadBuffAuthoringDescriptionPreviewRequest(context, jsonOptions);
            WriteJson(context.Response, PreviewBuffAutoDescriptions(rootPath, request, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/load" && context.Request.HttpMethod == "GET")
        {
            string packageRelative = ResolveBuffPackageQuery(context, rootPath, jsonOptions);
            WriteJson(context.Response, ReadBuffAuthoringState(rootPath, packageRelative, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/save" && context.Request.HttpMethod == "POST")
        {
            BuffAuthoringPackageRequest request = ReadBuffAuthoringPackageRequest(context, jsonOptions);
            BuffAuthoringSaveResponse result = SaveBuffAuthoringPackage(rootPath, request.Package, request.Document, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.Success ? 200 : 400);
            return true;
        }

        if (path == "/api/authoring/buffs/validate" && context.Request.HttpMethod == "POST")
        {
            BuffAuthoringPackageRequest request = ReadBuffAuthoringPackageRequest(context, jsonOptions);
            WriteJson(context.Response, ValidateBuffAuthoringPackage(rootPath, request.Package, request.Document, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/compile" && context.Request.HttpMethod == "POST")
        {
            BuffAuthoringPackageRequest request = ReadBuffAuthoringPackageRequest(context, jsonOptions);
            WriteJson(context.Response, CompileBuffAuthoringPackage(rootPath, request.Package, request.Document, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/buffs/import-unity" && context.Request.HttpMethod == "POST")
        {
            BuffAuthoringImportRequest request = ReadBuffAuthoringImportRequest(context, jsonOptions);
            BuffAuthoringImportResponse result = ImportSavedBuffAuthoringPackage(rootPath, request.Package, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.Success ? 200 : 400);
            return true;
        }

        return false;
    }

    internal static List<BuffAuthoringPackageListItem> ListBuffAuthoringPackages(
        string rootPath,
        JsonSerializerOptions jsonOptions)
    {
        var result = new List<BuffAuthoringPackageListItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddBuffAuthoringPackagesFromRoot(result, seen, rootPath, SamplesRelativePath, "sample", jsonOptions);
        AddBuffAuthoringPackagesFromRoot(result, seen, rootPath, ProjectBuffPackagesRelativePath, "project", jsonOptions);

        result.Sort((left, right) => string.Compare(left.Relative, right.Relative, StringComparison.Ordinal));
        return result;
    }

    internal static BuffAuthoringOptionsResponse ReadBuffAuthoringOptions()
    {
        return ReadBuffAuthoringOptions(null);
    }

    internal static BuffAuthoringOptionsResponse ReadBuffAuthoringOptions(string rootPath)
    {
        return new BuffAuthoringOptionsResponse
        {
            DurationFrameRate = BuffDurationFrameRate,
            InstantFrameRateBase = BuffInstantFrameRateBase,
            InstantFrames = CalculateInstantFrames(BuffDurationFrameRate, BuffInstantFrameRateBase),
            Locales = ReadBuffAuthoringLocaleOptions(rootPath),
            DurationKinds = new[]
            {
                new BuffAuthoringStringOption { Id = "instant", Label = "瞬时", Description = "约 1/30 秒的最短可见生命周期；当前 60fps 保存为 2 帧。" },
                new BuffAuthoringStringOption { Id = "duration", Label = "持续", Description = "按秒编辑持续时间，并写回帧数。" },
                new BuffAuthoringStringOption { Id = "permanent", Label = "永久", Description = "不按持续时间自动过期。" }
            },
            StackingModes = new[]
            {
                new BuffAuthoringStringOption { Id = "single", Label = "不叠层 / 刷新时间", Description = "重复获得时只刷新持续时间；底层保存为 RefreshDuration，最大层数和每次层数为 1。" },
                new BuffAuthoringStringOption { Id = "stacking", Label = "可叠层 / 刷新时间", Description = "重复获得时增加层数并刷新持续时间；底层保存为 Stack，每次获得层数固定为 1。" }
            },
            DescriptionModes = new[]
            {
                new BuffAuthoringStringOption { Id = "Auto", Label = "自动生成 (Auto)", Description = "根据当前效果、时间和叠层配置生成效果描述，不写回 authoring JSON。" },
                new BuffAuthoringStringOption { Id = "Manual", Label = "手写覆盖 (Manual)", Description = "使用 presentation.description 作为手写效果描述 fallback。" }
            },
            BuffTypes = new[]
            {
                new BuffAuthoringStringOption { Id = "empower", Label = "强化", Description = "提升角色能力或正向状态。" },
                new BuffAuthoringStringOption { Id = "weaken", Label = "削弱", Description = "降低角色能力或施加负向状态。" },
                new BuffAuthoringStringOption { Id = "damage", Label = "伤害", Description = "造成伤害或持续伤害的状态。" },
                new BuffAuthoringStringOption { Id = "restore", Label = "恢复", Description = "恢复生命、资源或其他可恢复状态。" },
                new BuffAuthoringStringOption { Id = "control", Label = "控制", Description = "限制行动、输入、移动或技能使用。" },
                new BuffAuthoringStringOption { Id = "damageReduction", Label = "减伤", Description = "降低受到的伤害或提高防护能力。" },
                new BuffAuthoringStringOption { Id = "mechanic", Label = "机制", Description = "不直接归入数值好坏，但承载规则、标记或特殊机制。" }
            },
            Attributes = new[]
            {
                new BuffAuthoringAttributeOption { AttributeId = 1, DisplayName = "HP", Label = "HP(1)", Description = "生命值。通常不作为 Buff v2 数值修改的首选字段。" },
                new BuffAuthoringAttributeOption { AttributeId = 2, DisplayName = "Attack", Label = "Attack(2)", Description = "攻击力。正数提升输出，负数降低输出。" },
                new BuffAuthoringAttributeOption { AttributeId = 3, DisplayName = "Defense", Label = "Defense(3)", Description = "防御力。正数减轻受到的普通伤害，负数提高受到的伤害。" },
                new BuffAuthoringAttributeOption { AttributeId = 4, DisplayName = "Posture", Label = "Posture(4)", Description = "韧性 / 姿态资源。用于 CharacterTest 韧性压力链路。" }
            },
            StackPolicies = new[]
            {
                new BuffAuthoringStringOption { Id = "RefreshDuration", Label = "刷新持续时间 (RefreshDuration)", Description = "重复获得时不叠层，只刷新持续时间。" },
                new BuffAuthoringStringOption { Id = "Replace", Label = "替换并重置 (Replace)", Description = "重复获得时重置开始时间和层数。" },
                new BuffAuthoringStringOption { Id = "Stack", Label = "叠层并刷新 (Stack)", Description = "重复获得时增加层数，最多到最大层数。" }
            },
            Polarities = new[]
            {
                new BuffAuthoringStringOption { Id = string.Empty, Label = "自动推断", Description = "根据效果正负自动推断表现类型。" },
                new BuffAuthoringStringOption { Id = "Neutral", Label = "中性 (Neutral)", Description = "不明显表达增益或减益。" },
                new BuffAuthoringStringOption { Id = "Buff", Label = "增益 (Buff)", Description = "对目标有利的状态。" },
                new BuffAuthoringStringOption { Id = "Debuff", Label = "减益 (Debuff)", Description = "对目标不利的状态。" },
                new BuffAuthoringStringOption { Id = "Aura", Label = "光环 (Aura)", Description = "来自范围刷新或持续光环的状态。" }
            },
            Relations = new[]
            {
                new BuffAuthoringStringOption { Id = "Any", Label = "任意目标 (Any)", Description = "不按阵营关系过滤。" },
                new BuffAuthoringStringOption { Id = "Self", Label = "只作用自己 (Self)", Description = "只匹配施法者自身。" },
                new BuffAuthoringStringOption { Id = "SameTeam", Label = "同队目标 (SameTeam)", Description = "只匹配同队角色。" },
                new BuffAuthoringStringOption { Id = "Enemy", Label = "敌方目标 (Enemy)", Description = "只匹配敌对角色。" }
            },
            AuraLayerMasks = new[]
            {
                new BuffAuthoringLayerMaskOption { Id = "dynamicCharacters", Value = 256, Label = "角色 / 动态目标 (256)", Description = "CharacterTest Guard Aura 当前使用的安全目标层。" }
            },
            Fields = CreateBuffFieldOptions()
        };
    }

    private static int CalculateInstantFrames(int durationFrameRate, int instantFrameRateBase)
    {
        if (durationFrameRate <= 0 || instantFrameRateBase <= 0)
            return 1;

        return Math.Max(1, (int)Math.Ceiling(durationFrameRate / (double)instantFrameRateBase));
    }

    internal static BuffAuthoringDescriptionPreviewResponse PreviewBuffAutoDescriptions(
        string rootPath,
        BuffAuthoringDescriptionPreviewRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new BuffAuthoringDescriptionPreviewRequest();
        BuffAuthoringDocument document = request.Document;
        if (document == null && !string.IsNullOrWhiteSpace(request.Package))
            document = ReadBuffAuthoringDocument(ResolveBuffAuthoringDocumentPath(rootPath, request.Package), jsonOptions);
        document ??= new BuffAuthoringDocument();

        BuffAuthoringLocaleOption[] locales = ReadBuffAuthoringLocaleOptions(rootPath);
        string locale = NormalizeBuffLocale(request.Locale, locales);
        BuffLocalizationTextCatalog catalog = ReadBuffLocalizationTextCatalog(rootPath);
        var previews = new List<BuffAuthoringDescriptionPreviewItem>();
        for (int i = 0; document.Buffs != null && i < document.Buffs.Count; i++)
        {
            BuffDefinitionConfig buff = document.Buffs[i];
            if (buff == null)
                continue;

            previews.Add(new BuffAuthoringDescriptionPreviewItem
            {
                BuffId = buff.BuffId,
                StableId = buff.StableId ?? string.Empty,
                Description = BuffAutoDescriptionFormatter.Format(
                    buff,
                    locale,
                    BuffDurationFrameRate,
                    catalog.TryGetText)
            });
        }

        return new BuffAuthoringDescriptionPreviewResponse
        {
            Success = true,
            Locale = locale,
            Locales = locales,
            Buffs = previews.ToArray()
        };
    }

    internal static BuffAuthoringPackageState ReadBuffAuthoringState(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveBuffAuthoringDocumentPath(rootPath, packageRelative);
        BuffAuthoringDocument document = ReadBuffAuthoringDocument(documentPath, jsonOptions);
        bool hasLegacyPresentationPolarity = HasLegacyPresentationPolarity(document);
        BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);
        AddBuffLocalizationDiagnostics(rootPath, document, report);
        BuffAuthoringDiffReport authoringDiff = BuffAuthoringDiffBuilder.BuildAuthoringDiff(document, document);
        string packageRoot = BuffAuthoringCompiler.ResolvePackageRootPath(documentPath);
        string relativePackage = ToAuthoringRelativePath(rootPath, packageRoot);
        string relativeDocument = ToAuthoringRelativePath(rootPath, documentPath);

        return new BuffAuthoringPackageState
        {
            Success = true,
            Package = relativePackage,
            PackageId = document.PackageId ?? string.Empty,
            SourceKind = GetBuffAuthoringSourceKind(rootPath, packageRoot),
            DocumentPath = relativeDocument,
            CanWrite = CanWriteFile(documentPath),
            Document = document,
            SavedDocumentHash = ComputeFileHash(documentPath),
            DraftRevision = ComputeDocumentRevision(document, jsonOptions),
            ValidationReport = report,
            AuthoringDiff = authoringDiff,
            HasSourceChanges = hasLegacyPresentationPolarity,
            ImportPlan = CreateBuffImportPlan(rootPath)
        };
    }

    internal static BuffAuthoringSaveResponse SaveBuffAuthoringPackage(
        string rootPath,
        string packageRelative,
        BuffAuthoringDocument document,
        JsonSerializerOptions jsonOptions)
    {
        if (document == null)
            throw new ArgumentException("document is required.");

        string documentPath = ResolveBuffAuthoringDocumentPath(rootPath, packageRelative);
        if (!File.Exists(documentPath))
            throw new FileNotFoundException("Buff authoring document was not found.", documentPath);

        BuffAuthoringDocument savedDocument = ReadBuffAuthoringDocument(documentPath, jsonOptions);
        bool hasLegacyPresentationPolarity = HasLegacyPresentationPolarity(savedDocument);
        BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);
        AddBuffLocalizationDiagnostics(rootPath, document, report);
        BuffAuthoringDiffReport authoringDiff = BuffAuthoringDiffBuilder.BuildAuthoringDiff(savedDocument, document);
        bool hasSourceChanges = authoringDiff.HasChanges || hasLegacyPresentationPolarity;
        if (report.HasBlockingIssues)
        {
            return new BuffAuthoringSaveResponse
            {
                Success = false,
                Saved = false,
                Package = ToAuthoringRelativePath(rootPath, BuffAuthoringCompiler.ResolvePackageRootPath(documentPath)),
                DocumentPath = ToAuthoringRelativePath(rootPath, documentPath),
                Document = document,
                SavedDocumentHash = ComputeFileHash(documentPath),
                DraftRevision = ComputeDocumentRevision(document, jsonOptions),
                ValidationReport = report,
                AuthoringDiff = authoringDiff,
                HasSourceChanges = hasSourceChanges,
                Message = "Buff authoring document has blocking validation issues."
            };
        }

        WriteJsonFileAtomic(documentPath, document, jsonOptions);
        BuffAuthoringDiffReport cleanDiff = BuffAuthoringDiffBuilder.BuildAuthoringDiff(document, document);
        return new BuffAuthoringSaveResponse
        {
            Success = true,
            Saved = true,
            Package = ToAuthoringRelativePath(rootPath, BuffAuthoringCompiler.ResolvePackageRootPath(documentPath)),
            DocumentPath = ToAuthoringRelativePath(rootPath, documentPath),
            Document = document,
            SavedDocumentHash = ComputeFileHash(documentPath),
            DraftRevision = ComputeDocumentRevision(document, jsonOptions),
            ValidationReport = report,
            AuthoringDiff = cleanDiff,
            HasSourceChanges = false,
            Message = "Saved buff authoring document."
        };
    }

    internal static BuffAuthoringValidationResponse ValidateBuffAuthoringPackage(
        string rootPath,
        string packageRelative,
        BuffAuthoringDocument document,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveBuffAuthoringDocumentPath(rootPath, packageRelative);
        BuffAuthoringDocument savedDocument = ReadBuffAuthoringDocument(documentPath, jsonOptions);
        bool hasLegacyPresentationPolarity = HasLegacyPresentationPolarity(savedDocument);
        document ??= savedDocument;
        BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);
        AddBuffLocalizationDiagnostics(rootPath, document, report);
        string draftRevision = ComputeDocumentRevision(document, jsonOptions);
        BuffAuthoringDiffReport authoringDiff = BuffAuthoringDiffBuilder.BuildAuthoringDiff(savedDocument, document);
        return new BuffAuthoringValidationResponse
        {
            Success = !report.HasBlockingIssues,
            Document = document,
            SavedDocumentHash = ComputeFileHash(documentPath),
            DraftRevision = draftRevision,
            ValidationReport = report,
            AuthoringDiff = authoringDiff,
            HasSourceChanges = authoringDiff.HasChanges || hasLegacyPresentationPolarity
        };
    }

    internal static BuffAuthoringCompileResponse CompileBuffAuthoringPackage(
        string rootPath,
        string packageRelative,
        BuffAuthoringDocument document,
        JsonSerializerOptions jsonOptions)
    {
        document ??= ReadBuffAuthoringDocument(ResolveBuffAuthoringDocumentPath(rootPath, packageRelative), jsonOptions);
        BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(document);
        AddBuffLocalizationDiagnostics(rootPath, document, result.ValidationReport);
        return new BuffAuthoringCompileResponse
        {
            Success = !result.ValidationReport.HasBlockingIssues,
            CompileResult = result,
            ImportPlan = CreateBuffImportPlan(rootPath),
            GeneratedDiff = CreateGeneratedDiff(rootPath, result, jsonOptions)
        };
    }

    internal static BuffAuthoringImportResponse ImportSavedBuffAuthoringPackage(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveBuffAuthoringDocumentPath(rootPath, packageRelative);
        BuffAuthoringDocument document = ReadBuffAuthoringDocument(documentPath, jsonOptions);
        BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(document);
        AddBuffLocalizationDiagnostics(rootPath, document, result.ValidationReport);
        BuffAuthoringImportWrite[] writes = CreateBuffImportPlan(rootPath);

        if (result.ValidationReport.HasBlockingIssues)
        {
            return new BuffAuthoringImportResponse
            {
                Success = false,
                Imported = false,
                ValidationBlocked = true,
                Package = ToAuthoringRelativePath(rootPath, BuffAuthoringCompiler.ResolvePackageRootPath(documentPath)),
                PackageId = result.PackageId ?? string.Empty,
                ValidationReport = result.ValidationReport,
                Writes = writes,
                Message = "Buff authoring document has blocking validation issues."
            };
        }

        WriteJsonFileAtomic(ResolveSafePath(rootPath, writes[0].RelativePath), result.BuffDefinitions, jsonOptions);
        WriteJsonFileAtomic(ResolveSafePath(rootPath, writes[1].RelativePath), result.BuffPresentationDefinitions, jsonOptions);
        WriteJsonFileAtomic(ResolveSafePath(rootPath, writes[2].RelativePath), result.AuraBuffDefinitions, jsonOptions);

        return new BuffAuthoringImportResponse
        {
            Success = true,
            Imported = true,
            ValidationBlocked = false,
            Package = ToAuthoringRelativePath(rootPath, BuffAuthoringCompiler.ResolvePackageRootPath(documentPath)),
            PackageId = result.PackageId ?? string.Empty,
            ValidationReport = result.ValidationReport,
            Writes = writes,
            Message = "Imported generated Buff runtime JSON."
        };
    }

    private static void AddBuffAuthoringPackagesFromRoot(
        List<BuffAuthoringPackageListItem> result,
        HashSet<string> seen,
        string rootPath,
        string packageRootRelative,
        string sourceKind,
        JsonSerializerOptions jsonOptions)
    {
        string packageRoot;
        try
        {
            packageRoot = ResolveSafePath(rootPath, packageRootRelative);
        }
        catch
        {
            return;
        }

        if (!Directory.Exists(packageRoot))
            return;

        foreach (string dir in Directory.GetDirectories(packageRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            string documentPath = Path.Combine(dir, BuffAuthoringCompiler.BuffAuthoringDocumentRelativePath);
            if (!File.Exists(documentPath))
                continue;

            string relative = ToAuthoringRelativePath(rootPath, dir);
            if (!seen.Add(relative))
                continue;

            string packageId = string.Empty;
            string status = "Unknown";
            bool canWrite = CanWriteFile(documentPath);
            try
            {
                BuffAuthoringDocument document = ReadBuffAuthoringDocument(documentPath, jsonOptions);
                BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);
                packageId = document.PackageId ?? string.Empty;
                status = report.Status ?? string.Empty;
            }
            catch
            {
                status = "InvalidJson";
            }

            result.Add(new BuffAuthoringPackageListItem
            {
                Relative = relative,
                PackageId = packageId,
                SourceKind = sourceKind,
                DocumentPath = ToAuthoringRelativePath(rootPath, documentPath),
                Status = status,
                CanWrite = canWrite
            });
        }
    }

    private static BuffAuthoringPackageRequest ReadBuffAuthoringPackageRequest(
        HttpListenerContext context,
        JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new BuffAuthoringPackageRequest()
            : (JsonSerializer.Deserialize<BuffAuthoringPackageRequest>(body, jsonOptions) ?? new BuffAuthoringPackageRequest());
    }

    private static BuffAuthoringImportRequest ReadBuffAuthoringImportRequest(
        HttpListenerContext context,
        JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new BuffAuthoringImportRequest()
            : (JsonSerializer.Deserialize<BuffAuthoringImportRequest>(body, jsonOptions) ?? new BuffAuthoringImportRequest());
    }

    private static BuffAuthoringDescriptionPreviewRequest ReadBuffAuthoringDescriptionPreviewRequest(
        HttpListenerContext context,
        JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new BuffAuthoringDescriptionPreviewRequest()
            : (JsonSerializer.Deserialize<BuffAuthoringDescriptionPreviewRequest>(body, jsonOptions) ?? new BuffAuthoringDescriptionPreviewRequest());
    }

    private static string ResolveBuffPackageQuery(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions)
    {
        string requested = context.Request.QueryString["package"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requested))
            return requested;

        List<BuffAuthoringPackageListItem> packages = ListBuffAuthoringPackages(rootPath, jsonOptions);
        if (packages.Count > 0)
            return packages[0].Relative;

        throw new FileNotFoundException("No Buff authoring packages were found.");
    }

    private static string ResolveBuffAuthoringDocumentPath(string rootPath, string packageOrDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(packageOrDocumentPath))
            throw new ArgumentException("package is required.");

        string resolved = ResolveSafePath(rootPath, packageOrDocumentPath);
        string documentPath = Directory.Exists(resolved)
            ? Path.Combine(resolved, BuffAuthoringCompiler.BuffAuthoringDocumentRelativePath)
            : resolved;

        if (!IsPathAllowed(rootPath, Path.GetFullPath(documentPath)))
            throw new ArgumentException("path escapes root: " + packageOrDocumentPath);
        return documentPath;
    }

    private static BuffAuthoringDocument ReadBuffAuthoringDocument(
        string documentPath,
        JsonSerializerOptions jsonOptions)
    {
        if (!File.Exists(documentPath))
            throw new FileNotFoundException("Buff authoring document was not found.", documentPath);
        return JsonSerializer.Deserialize<BuffAuthoringDocument>(File.ReadAllText(documentPath), jsonOptions)
            ?? new BuffAuthoringDocument();
    }

    private static BuffAuthoringFieldOption[] CreateBuffFieldOptions()
    {
        return new[]
        {
            new BuffAuthoringFieldOption { Field = "displayName", Label = "Fallback 显示名称", Description = "开发可读名和本地化失败时的 fallback 名称。" },
            new BuffAuthoringFieldOption { Field = "types", Label = "Buff 类型", Description = "玩家/策划感知分类；可多选，不表示来源或运行策略。" },
            new BuffAuthoringFieldOption { Field = "durationKind", Label = "时间类型", Description = "瞬时、持续或永久；由 isPermanent 与持续帧数双向同步。" },
            new BuffAuthoringFieldOption { Field = "defaultDurationFrames", Label = "持续时间", Description = "非永久 Buff 的持续时间；界面按秒编辑，JSON 仍保存帧数。" },
            new BuffAuthoringFieldOption { Field = "maxStackCount", Label = "最大层数", Description = "可叠层时 Buff 最多可以叠到多少层；不叠层时固定为 1。" },
            new BuffAuthoringFieldOption { Field = "applyStackCount", Label = "每次获得层数", Description = "当前策划界面固定为 1，保留为兼容技术字段。" },
            new BuffAuthoringFieldOption { Field = "stackPolicy", Label = "叠层模式", Description = "技术字段；策划界面只编辑不叠层或可叠层，两者都会刷新时间。" },
            new BuffAuthoringFieldOption { Field = "isPermanent", Label = "时间类型", Description = "启用后不依赖持续时间自动过期。" },
            new BuffAuthoringFieldOption { Field = "attributeId", Label = "影响属性", Description = "属性修改效果作用到哪个属性。" },
            new BuffAuthoringFieldOption { Field = "addValue", Label = "数值变化", Description = "正数增加属性，负数减少属性。" },
            new BuffAuthoringFieldOption { Field = "statusId", Label = "状态 ID", Description = "Status 效果使用的状态编号。" },
            new BuffAuthoringFieldOption { Field = "nameKey", Label = "名称多语言 Key", Description = "Localization.tsv 中的显示名 key；建议为 stableId.name。" },
            new BuffAuthoringFieldOption { Field = "shortName", Label = "短名称", Description = "HUD / HeadPad 上的短标签。" },
            new BuffAuthoringFieldOption { Field = "descriptionMode", Label = "描述来源", Description = "Auto 自动生成效果描述；Manual 使用手写描述。" },
            new BuffAuthoringFieldOption { Field = "description", Label = "手写描述", Description = "仅 Manual 模式使用；Auto 模式的生成描述不会写回源 JSON。" },
            new BuffAuthoringFieldOption { Field = "iconResourceKey", Label = "图标资源键", Description = "表现层小徽章图标资源 key；为空会给 Warning，但不阻断保存。" },
            new BuffAuthoringFieldOption { Field = "priority", Label = "显示优先级", Description = "数值越大越优先显示。" },
            new BuffAuthoringFieldOption { Field = "showOnPlayerHud", Label = "显示在玩家 HUD", Description = "是否在玩家自己的 HUD 上显示。" },
            new BuffAuthoringFieldOption { Field = "showOnTargetHud", Label = "显示在目标 HUD", Description = "是否在目标信息区域显示。" },
            new BuffAuthoringFieldOption { Field = "showOnHeadPad", Label = "显示在头顶条", Description = "是否在角色头顶状态条显示。" },
            new BuffAuthoringFieldOption { Field = "radiusMeters", Label = "光环半径", Description = "以米为单位的 Aura 刷新范围。" },
            new BuffAuthoringFieldOption { Field = "layerMask", Label = "目标层预设", Description = "Aura 查询目标所在的物理层；首屏使用安全预设。" },
            new BuffAuthoringFieldOption { Field = "relationFilter", Label = "目标关系", Description = "Aura 按阵营关系筛选目标。" },
            new BuffAuthoringFieldOption { Field = "includeCaster", Label = "包含施法者", Description = "Aura 是否也刷新给施法者自己。" }
        };
    }

    private static BuffAuthoringDiffReport CreateGeneratedDiff(
        string rootPath,
        BuffAuthoringCompileResult result,
        JsonSerializerOptions jsonOptions)
    {
        BuffAuthoringImportWrite[] writes = CreateBuffImportPlan(rootPath);
        BuffDefinitionCatalogDocument currentBuffs =
            ReadOptionalGeneratedJson<BuffDefinitionCatalogDocument>(rootPath, writes[0].RelativePath, jsonOptions);
        BuffPresentationDefinitionCatalogDocument currentPresentation =
            ReadOptionalGeneratedJson<BuffPresentationDefinitionCatalogDocument>(rootPath, writes[1].RelativePath, jsonOptions);
        AuraBuffDefinitionCatalogDocument currentAuras =
            ReadOptionalGeneratedJson<AuraBuffDefinitionCatalogDocument>(rootPath, writes[2].RelativePath, jsonOptions);
        return BuffAuthoringDiffBuilder.BuildGeneratedDiff(currentBuffs, currentPresentation, currentAuras, result);
    }

    private static T ReadOptionalGeneratedJson<T>(
        string rootPath,
        string relativePath,
        JsonSerializerOptions jsonOptions)
        where T : new()
    {
        string path = ResolveSafePath(rootPath, relativePath);
        if (!File.Exists(path))
            return new T();
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), jsonOptions) ?? new T();
    }

    private static string ComputeFileHash(string path)
    {
        if (!File.Exists(path))
            return string.Empty;
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
        return ToLowerHex(hash);
    }

    private static string ComputeDocumentRevision(
        BuffAuthoringDocument document,
        JsonSerializerOptions jsonOptions)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document ?? new BuffAuthoringDocument(), jsonOptions);
        byte[] hash = sha.ComputeHash(bytes);
        return ToLowerHex(hash);
    }

    private static string ToLowerHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static BuffAuthoringImportWrite[] CreateBuffImportPlan(string rootPath)
    {
        return new[]
        {
            new BuffAuthoringImportWrite
            {
                RelativePath = Path.Combine(GeneratedBuffRootRelativePath, "buff_definitions.json").Replace('\\', '/'),
                Kind = "BuffDefinitions"
            },
            new BuffAuthoringImportWrite
            {
                RelativePath = Path.Combine(GeneratedBuffRootRelativePath, "buff_presentation_definitions.json").Replace('\\', '/'),
                Kind = "BuffPresentationDefinitions"
            },
            new BuffAuthoringImportWrite
            {
                RelativePath = Path.Combine(GeneratedAuraRootRelativePath, "aura_buff_definitions.json").Replace('\\', '/'),
                Kind = "AuraBuffDefinitions"
            }
        };
    }

    private static bool HasLegacyPresentationPolarity(BuffAuthoringDocument document)
    {
        if (document?.Buffs == null)
            return false;

        for (int i = 0; i < document.Buffs.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(document.Buffs[i]?.Presentation?.Polarity))
                return true;
        }

        return false;
    }

    private static string GetBuffAuthoringSourceKind(string rootPath, string packageRoot)
    {
        string fullPackage = Path.GetFullPath(packageRoot);
        string samplesRoot;
        try
        {
            samplesRoot = ResolveSafePath(rootPath, SamplesRelativePath);
        }
        catch
        {
            samplesRoot = string.Empty;
        }

        return !string.IsNullOrWhiteSpace(samplesRoot) && IsPathAtOrUnderRoot(samplesRoot, fullPackage)
            ? "sample"
            : "project";
    }

    private static bool CanWriteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                return (File.GetAttributes(path) & FileAttributes.ReadOnly) == 0;

            string directory = Path.GetDirectoryName(path);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
        }
        catch
        {
            return false;
        }
    }

    private static BuffAuthoringLocaleOption[] ReadBuffAuthoringLocaleOptions(string rootPath)
    {
        BuffLocalizationTextCatalog catalog = ReadBuffLocalizationTextCatalog(rootPath);
        string[] localeIds = catalog.LocaleIds.Length > 0
            ? catalog.LocaleIds
            : new[] { "zh-CN", "en-US" };
        var options = new List<BuffAuthoringLocaleOption>();
        for (int i = 0; i < localeIds.Length; i++)
        {
            string localeId = string.IsNullOrWhiteSpace(localeIds[i]) ? "zh-CN" : localeIds[i].Trim();
            if (options.Exists(item => string.Equals(item.Id, localeId, StringComparison.Ordinal)))
                continue;

            options.Add(new BuffAuthoringLocaleOption
            {
                Id = localeId,
                Label = ResolveLocaleLabel(catalog, localeId)
            });
        }

        return options.ToArray();
    }

    private static string NormalizeBuffLocale(string locale, BuffAuthoringLocaleOption[] locales)
    {
        if (!string.IsNullOrWhiteSpace(locale))
        {
            string requested = locale.Trim();
            if (Array.Exists(locales ?? Array.Empty<BuffAuthoringLocaleOption>(), item => string.Equals(item.Id, requested, StringComparison.Ordinal)))
                return requested;
        }

        if (Array.Exists(locales ?? Array.Empty<BuffAuthoringLocaleOption>(), item => string.Equals(item.Id, "zh-CN", StringComparison.Ordinal)))
            return "zh-CN";
        return locales != null && locales.Length > 0 ? locales[0].Id : "zh-CN";
    }

    private static void AddBuffLocalizationDiagnostics(
        string rootPath,
        BuffAuthoringDocument document,
        BuffAuthoringValidationReport report)
    {
        if (document?.Buffs == null || report == null)
            return;

        BuffLocalizationTextCatalog catalog = ReadBuffLocalizationTextCatalog(rootPath);
        if (!catalog.HasLocalizationText)
            return;

        for (int i = 0; i < document.Buffs.Count; i++)
        {
            BuffDefinitionConfig buff = document.Buffs[i];
            string nameKey = buff?.Presentation?.NameKey;
            if (string.IsNullOrWhiteSpace(nameKey))
                continue;

            nameKey = nameKey.Trim();
            if (catalog.ContainsKey(nameKey))
                continue;

            AddBuffWarning(
                report,
                "BUFF_PRESENTATION_NAME_KEY_UNDECLARED",
                "buffs[" + i.ToString(CultureInfo.InvariantCulture) + "].presentation.nameKey",
                "Buff presentation nameKey is not declared in available Localization TSV files: " + nameKey + ".");
        }
    }

    private static void AddBuffWarning(
        BuffAuthoringValidationReport report,
        string code,
        string path,
        string message)
    {
        if (report.Diagnostics.Exists(diagnostic =>
                string.Equals(diagnostic.Code, code, StringComparison.Ordinal)
                && string.Equals(diagnostic.Path, path, StringComparison.Ordinal)))
        {
            return;
        }

        report.Diagnostics.Add(new BuffAuthoringDiagnostic
        {
            Severity = "Warning",
            Code = code,
            Path = path,
            Message = message
        });
    }

    private static string ResolveLocaleLabel(BuffLocalizationTextCatalog catalog, string localeId)
    {
        if (catalog.TryGetText("locale." + localeId + ".label", localeId, out string label))
            return label;
        if (string.Equals(localeId, "zh-CN", StringComparison.Ordinal))
            return "简体中文";
        if (string.Equals(localeId, "en-US", StringComparison.Ordinal))
            return "English";
        return localeId;
    }

    private static BuffLocalizationTextCatalog ReadBuffLocalizationTextCatalog(string rootPath)
    {
        var catalog = new BuffLocalizationTextCatalog();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            catalog.EnsureDefaultLocales();
            return catalog;
        }

        string localizationRoot = Path.Combine(
            rootPath,
            "Assets",
            "StreamingAssets",
            "MxFramework",
            "Localization");
        if (!Directory.Exists(localizationRoot))
        {
            catalog.EnsureDefaultLocales();
            return catalog;
        }

        foreach (string path in Directory.GetFiles(localizationRoot, "*.tsv").OrderBy(item => item, StringComparer.Ordinal))
            catalog.LoadTsv(File.ReadAllText(path));
        catalog.EnsureDefaultLocales();
        return catalog;
    }

    internal sealed class BuffAuthoringPackageListItem
    {
        public string Relative { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool CanWrite { get; set; }
    }

    internal sealed class BuffAuthoringOptionsResponse
    {
        public int DurationFrameRate { get; set; }
        public int InstantFrameRateBase { get; set; }
        public int InstantFrames { get; set; }
        public BuffAuthoringLocaleOption[] Locales { get; set; } = Array.Empty<BuffAuthoringLocaleOption>();
        public BuffAuthoringStringOption[] DurationKinds { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringStringOption[] StackingModes { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringStringOption[] DescriptionModes { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringStringOption[] BuffTypes { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringAttributeOption[] Attributes { get; set; } = Array.Empty<BuffAuthoringAttributeOption>();
        public BuffAuthoringStringOption[] StackPolicies { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringStringOption[] Polarities { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringStringOption[] Relations { get; set; } = Array.Empty<BuffAuthoringStringOption>();
        public BuffAuthoringLayerMaskOption[] AuraLayerMasks { get; set; } = Array.Empty<BuffAuthoringLayerMaskOption>();
        public BuffAuthoringFieldOption[] Fields { get; set; } = Array.Empty<BuffAuthoringFieldOption>();
    }

    internal sealed class BuffAuthoringLocaleOption
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringAttributeOption
    {
        public int AttributeId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringStringOption
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringLayerMaskOption
    {
        public string Id { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringFieldOption
    {
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringPackageState
    {
        public bool Success { get; set; }
        public string Package { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public bool CanWrite { get; set; }
        public BuffAuthoringDocument Document { get; set; } = new BuffAuthoringDocument();
        public string SavedDocumentHash { get; set; } = string.Empty;
        public string DraftRevision { get; set; } = string.Empty;
        public BuffAuthoringValidationReport ValidationReport { get; set; } = new BuffAuthoringValidationReport();
        public BuffAuthoringDiffReport AuthoringDiff { get; set; } = new BuffAuthoringDiffReport();
        public bool HasSourceChanges { get; set; }
        public BuffAuthoringImportWrite[] ImportPlan { get; set; } = Array.Empty<BuffAuthoringImportWrite>();
    }

    internal sealed class BuffAuthoringPackageRequest
    {
        public string Package { get; set; } = string.Empty;
        public BuffAuthoringDocument Document { get; set; }
    }

    internal sealed class BuffAuthoringImportRequest
    {
        public string Package { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringDescriptionPreviewRequest
    {
        public string Package { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
        public BuffAuthoringDocument Document { get; set; }
    }

    internal sealed class BuffAuthoringDescriptionPreviewResponse
    {
        public bool Success { get; set; }
        public string Locale { get; set; } = string.Empty;
        public BuffAuthoringLocaleOption[] Locales { get; set; } = Array.Empty<BuffAuthoringLocaleOption>();
        public BuffAuthoringDescriptionPreviewItem[] Buffs { get; set; } = Array.Empty<BuffAuthoringDescriptionPreviewItem>();
    }

    internal sealed class BuffAuthoringDescriptionPreviewItem
    {
        public int BuffId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringSaveResponse
    {
        public bool Success { get; set; }
        public bool Saved { get; set; }
        public string Package { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public BuffAuthoringDocument Document { get; set; } = new BuffAuthoringDocument();
        public string SavedDocumentHash { get; set; } = string.Empty;
        public string DraftRevision { get; set; } = string.Empty;
        public BuffAuthoringValidationReport ValidationReport { get; set; } = new BuffAuthoringValidationReport();
        public BuffAuthoringDiffReport AuthoringDiff { get; set; } = new BuffAuthoringDiffReport();
        public bool HasSourceChanges { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringValidationResponse
    {
        public bool Success { get; set; }
        public BuffAuthoringDocument Document { get; set; } = new BuffAuthoringDocument();
        public string SavedDocumentHash { get; set; } = string.Empty;
        public string DraftRevision { get; set; } = string.Empty;
        public BuffAuthoringValidationReport ValidationReport { get; set; } = new BuffAuthoringValidationReport();
        public BuffAuthoringDiffReport AuthoringDiff { get; set; } = new BuffAuthoringDiffReport();
        public bool HasSourceChanges { get; set; }
    }

    internal sealed class BuffAuthoringCompileResponse
    {
        public bool Success { get; set; }
        public BuffAuthoringCompileResult CompileResult { get; set; } = new BuffAuthoringCompileResult();
        public BuffAuthoringImportWrite[] ImportPlan { get; set; } = Array.Empty<BuffAuthoringImportWrite>();
        public BuffAuthoringDiffReport GeneratedDiff { get; set; } = new BuffAuthoringDiffReport();
    }

    internal sealed class BuffAuthoringImportResponse
    {
        public bool Success { get; set; }
        public bool Imported { get; set; }
        public bool ValidationBlocked { get; set; }
        public string Package { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public BuffAuthoringValidationReport ValidationReport { get; set; } = new BuffAuthoringValidationReport();
        public BuffAuthoringImportWrite[] Writes { get; set; } = Array.Empty<BuffAuthoringImportWrite>();
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class BuffAuthoringImportWrite
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    private sealed class BuffLocalizationTextCatalog
    {
        private readonly Dictionary<string, Dictionary<string, string>> _texts =
            new(StringComparer.Ordinal);
        private readonly List<string> _localeIds = new();

        public string[] LocaleIds => _localeIds.ToArray();
        public bool HasLocalizationText => _texts.Count > 0;

        public void EnsureDefaultLocales()
        {
            RegisterLocale("zh-CN");
            RegisterLocale("en-US");
        }

        public void LoadTsv(string text)
        {
            string[] lines = SplitLines(text ?? string.Empty);
            if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
                return;

            string[] header = lines[0].Split('\t');
            if (header.Length < 2 || !string.Equals(header[0].Trim(), "key", StringComparison.Ordinal))
                return;

            var locales = new List<string>();
            for (int column = 1; column < header.Length; column++)
            {
                string localeId = header[column].Trim();
                if (string.IsNullOrWhiteSpace(localeId))
                    continue;
                locales.Add(localeId);
                RegisterLocale(localeId);
            }

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                    continue;

                string[] columns = lines[lineIndex].Split('\t');
                if (columns.Length == 0 || string.IsNullOrWhiteSpace(columns[0]))
                    continue;

                string key = columns[0].Trim();
                for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
                {
                    int column = localeIndex + 1;
                    if (column >= columns.Length)
                        continue;
                    string value = columns[column];
                    if (string.IsNullOrEmpty(value))
                        continue;
                    RegisterText(key, locales[localeIndex], value);
                }
            }
        }

        public bool TryGetText(string key, string localeId, out string text)
        {
            text = string.Empty;
            return !string.IsNullOrWhiteSpace(key)
                && !string.IsNullOrWhiteSpace(localeId)
                && _texts.TryGetValue(key, out Dictionary<string, string> table)
                && table.TryGetValue(localeId, out text)
                && !string.IsNullOrEmpty(text);
        }

        public bool ContainsKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _texts.ContainsKey(key.Trim());
        }

        private void RegisterText(string key, string localeId, string value)
        {
            if (!_texts.TryGetValue(key, out Dictionary<string, string> table))
            {
                table = new Dictionary<string, string>(StringComparer.Ordinal);
                _texts.Add(key, table);
            }

            table[localeId] = value ?? string.Empty;
        }

        private void RegisterLocale(string localeId)
        {
            if (!string.IsNullOrWhiteSpace(localeId) && !_localeIds.Contains(localeId))
                _localeIds.Add(localeId);
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }
    }
}
