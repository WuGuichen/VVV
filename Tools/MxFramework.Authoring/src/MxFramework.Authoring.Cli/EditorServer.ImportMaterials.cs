using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string ImportMaterialsDocumentRelativePath = "config/import_materials.json";
    private const string ImportMaterialsStateRelativePath = "config/import_materials_state.json";
    private const string CharacterSourceManifestFileName = "character_sources.json";
    private const string CharacterSourceRootRelativePrefix = "ArtSource/Characters";
    private const string CharacterSourceFileKind = "file";
    private const string CharacterSourceResourceRefKind = "resourceRef";
    private const string CharacterSourcePackageScope = "package";
    private const string CharacterSourcePublicScope = "public";
    private const string CharacterSourcePublicRootRelative = "ArtSource/Common";
    private const long FbxTextureDiagnosticScanMaxBytes = 64L * 1024L * 1024L;
    private static readonly Regex FbxTexturePathRegex = new Regex(
        @"(?:[A-Za-z]:)?[A-Za-z0-9_ .\-\\/]+?\.(?:png|jpg|jpeg|tga|bmp|tif|tiff|psd|exr)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool TryHandleCharacterImportMaterialsApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/character/import-materials" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterImportMaterials(rootPath, characterPackage, defaultPackage, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/character/import-materials/source-candidates" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string sourceRoot = context.Request.QueryString["sourceRoot"] ?? string.Empty;
            string query = context.Request.QueryString["query"] ?? string.Empty;
            WriteJson(
                context.Response,
                ReadCharacterImportMaterialSourceCandidates(rootPath, characterPackage, defaultPackage, sourceRoot, query, jsonOptions),
                jsonOptions);
            return true;
        }

        if (path == "/api/character/import-materials/source-image" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string sourcePath = context.Request.QueryString["sourcePath"] ?? string.Empty;
            string sourceScope = context.Request.QueryString["sourceScope"] ?? CharacterSourcePackageScope;
            ServeCharacterImportMaterialSourceImage(context.Response, rootPath, characterPackage, defaultPackage, sourcePath, sourceScope, jsonOptions);
            return true;
        }

        if (path == "/api/character/import-materials/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterImportMaterialsSaveRequest request = string.IsNullOrWhiteSpace(body)
                ? new CharacterImportMaterialsSaveRequest()
                : (JsonSerializer.Deserialize<CharacterImportMaterialsSaveRequest>(body, jsonOptions) ?? new CharacterImportMaterialsSaveRequest());
            string characterPackage = string.IsNullOrWhiteSpace(request.package)
                ? ResolveCharacterPackageRelative(context, rootPath, defaultPackage)
                : request.package;
            try
            {
                WriteJson(context.Response, SaveCharacterImportMaterials(rootPath, characterPackage, defaultPackage, request, jsonOptions), jsonOptions);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, new { success = false, error = "IMPORT_MATERIALS_INVALID_REQUEST", message = ex.Message }, jsonOptions, 400);
            }
            return true;
        }

        if (path == "/api/character/import-materials/sync" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterImportMaterialsSyncRequest request = string.IsNullOrWhiteSpace(body)
                ? new CharacterImportMaterialsSyncRequest()
                : (JsonSerializer.Deserialize<CharacterImportMaterialsSyncRequest>(body, jsonOptions) ?? new CharacterImportMaterialsSyncRequest());
            string characterPackage = string.IsNullOrWhiteSpace(request.package)
                ? ResolveCharacterPackageRelative(context, rootPath, defaultPackage)
                : request.package;
            try
            {
                CharacterImportMaterialsSyncResponse response = SyncCharacterImportMaterials(rootPath, characterPackage, defaultPackage, request, jsonOptions);
                WriteJson(context.Response, response, jsonOptions, response.success ? 200 : 409);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, new { success = false, error = "IMPORT_MATERIALS_INVALID_REQUEST", message = ex.Message }, jsonOptions, 400);
            }
            return true;
        }

        return false;
    }

    private static void ServeCharacterImportMaterialSourceImage(
        HttpListenerResponse response,
        string rootPath,
        string packageRelative,
        string defaultPackage,
        string sourcePath,
        string sourceScope,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            WriteJson(response, new { success = false, error = "IMPORT_MATERIALS_SOURCE_PATH_REQUIRED", message = "sourcePath is required." }, jsonOptions, 400);
            return;
        }

        if (!IsImportMaterialBrowserImagePath(sourcePath))
        {
            WriteJson(response, new { success = false, error = "IMPORT_MATERIALS_SOURCE_IMAGE_UNSUPPORTED", message = "Only browser-displayable image sources can be previewed." }, jsonOptions, 415);
            return;
        }

        string resolvedPackage = string.IsNullOrWhiteSpace(packageRelative) ? defaultPackage : packageRelative;
        string packagePath = ResolveSafePath(rootPath, resolvedPackage);
        CharacterResourcePackage package = File.Exists(Path.Combine(packagePath, "manifest.json"))
            ? CharacterPackageCommands.ReadPackage(packagePath, jsonOptions)
            : null;
        CharacterImportMaterialsLoad load = ReadCharacterImportMaterialsDocument(rootPath, resolvedPackage, package, jsonOptions);
        var item = new CharacterImportMaterialItem
        {
            SourcePath = NormalizeImportMaterialRelativePath(sourcePath),
            Source = new CharacterSourceReference
            {
                Kind = CharacterSourceFileKind,
                Scope = string.IsNullOrWhiteSpace(sourceScope) ? CharacterSourcePackageScope : sourceScope.Trim(),
                Path = NormalizeImportMaterialRelativePath(sourcePath)
            }
        };
        ImportMaterialResolvedSource source = ResolveImportMaterialSource(rootPath, packagePath, load, load.Document?.SourceRoot ?? string.Empty, item);
        if (!source.safe)
        {
            WriteJson(response, new { success = false, error = "IMPORT_MATERIALS_SOURCE_IMAGE_FORBIDDEN", message = source.message }, jsonOptions, 403);
            return;
        }

        if (!source.exists || string.IsNullOrWhiteSpace(source.fullPath))
        {
            WriteJson(response, new { success = false, error = "IMPORT_MATERIALS_SOURCE_IMAGE_NOT_FOUND", message = "Source image was not found." }, jsonOptions, 404);
            return;
        }

        if (!IsImportMaterialBrowserImagePath(source.fullPath))
        {
            WriteJson(response, new { success = false, error = "IMPORT_MATERIALS_SOURCE_IMAGE_UNSUPPORTED", message = "Only browser-displayable image sources can be previewed." }, jsonOptions, 415);
            return;
        }

        byte[] bytes = File.ReadAllBytes(source.fullPath);
        response.StatusCode = 200;
        response.ContentType = GetContentType(source.fullPath);
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.Headers["Cache-Control"] = "no-store";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    internal static CharacterImportMaterialsState ReadCharacterImportMaterials(
        string rootPath,
        string packageRelative,
        string defaultPackage,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackage = string.IsNullOrWhiteSpace(packageRelative) ? defaultPackage : packageRelative;
        string packagePath = ResolveSafePath(rootPath, resolvedPackage);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterImportMaterialsLoad load = ReadCharacterImportMaterialsDocument(rootPath, resolvedPackage, package, jsonOptions);
        CharacterImportMaterialsDocument document = load.Document;
        NormalizeCharacterImportMaterialsDocument(document);

        var state = new CharacterImportMaterialsState
        {
            success = true,
            packageRelative = resolvedPackage,
            packageId = package.Manifest?.PackageId ?? string.Empty,
            documentRelative = load.DocumentRelative,
            sourceManifestRelative = load.SourceManifestRelative,
            sourceManifestExists = load.SourceManifestExists,
            stateRelative = load.StateRelative,
            legacyDocumentRelative = load.LegacyDocumentRelative,
            legacyDocumentExists = load.LegacyDocumentExists,
            documentKind = load.DocumentKind,
            document = document,
            templates = BuildImportMaterialTemplates()
        };

        string unityCatalogPath = GetCharacterUnityCatalogPath(rootPath, package.Manifest?.PackageId ?? string.Empty);
        using JsonDocument unityCatalog = File.Exists(unityCatalogPath) ? JsonDocument.Parse(File.ReadAllText(unityCatalogPath)) : null;
        for (int i = 0; i < document.Items.Count; i++)
        {
            state.items.Add(BuildImportMaterialItemStatus(rootPath, resolvedPackage, packagePath, package, load, document, document.Items[i], i, unityCatalog));
        }

        state.integrityReceipt = BuildImportMaterialsIntegrityReceipt(
            rootPath,
            resolvedPackage,
            package,
            load.SourceManifestRelative,
            document,
            jsonOptions);
        MergeImportMaterialsDiagnostics(
            state.integrityReceipt,
            load.PersistedIntegrityReceipt?.Diagnostics?.Where(diagnostic =>
                diagnostic != null
                && string.Equals(diagnostic.Severity, "Info", StringComparison.OrdinalIgnoreCase)));
        state.summary = BuildImportMaterialsSummary(state.items, state.integrityReceipt);
        return state;
    }

    internal static CharacterImportMaterialSourceCandidatesState ReadCharacterImportMaterialSourceCandidates(
        string rootPath,
        string packageRelative,
        string defaultPackage,
        string sourceRoot,
        string query,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackage = string.IsNullOrWhiteSpace(packageRelative) ? defaultPackage : packageRelative;
        string packagePath = ResolveSafePath(rootPath, resolvedPackage);
        CharacterResourcePackage package = File.Exists(Path.Combine(packagePath, "manifest.json"))
            ? CharacterPackageCommands.ReadPackage(packagePath, jsonOptions)
            : null;
        string defaultSourceRoot = GetCharacterSourceRootRelative(package, resolvedPackage);
        sourceRoot = NormalizeImportMaterialRelativePath(sourceRoot);
        if (string.IsNullOrWhiteSpace(sourceRoot))
            sourceRoot = defaultSourceRoot;
        query = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        var response = new CharacterImportMaterialSourceCandidatesState
        {
            success = true,
            packageRelative = resolvedPackage,
            sourceRoot = sourceRoot,
            query = query
        };
        response.roots.AddRange(BuildImportMaterialSourceRootSuggestions(rootPath, resolvedPackage, packagePath, sourceRoot, defaultSourceRoot));

        if (IsUnsafeImportMaterialRelativePath(sourceRoot, allowEmpty: true))
        {
            response.success = false;
            response.diagnostics.Add(new CharacterImportMaterialSourceCandidateDiagnostic
            {
                code = "IMPORT_MATERIALS_UNSAFE_SOURCE_ROOT",
                message = "sourceRoot must be a project/package relative path."
            });
            return response;
        }

        var scanRoots = BuildImportMaterialSourceScanRoots(rootPath, resolvedPackage, packagePath, sourceRoot, defaultSourceRoot);
        if (scanRoots.Count == 0)
        {
            response.diagnostics.Add(new CharacterImportMaterialSourceCandidateDiagnostic
            {
                code = "IMPORT_MATERIALS_SOURCE_ROOT_MISSING",
                message = "No existing project/package source root was found."
            });
            return response;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ImportMaterialSourceScanRoot scanRoot in scanRoots)
        {
            if (!Directory.Exists(scanRoot.fullPath))
            {
                response.diagnostics.Add(new CharacterImportMaterialSourceCandidateDiagnostic
                {
                    code = "IMPORT_MATERIALS_SOURCE_ROOT_MISSING",
                    path = scanRoot.displayRoot,
                    message = "Source root does not exist."
                });
                continue;
            }

            foreach (string file in EnumerateImportMaterialCandidateFiles(scanRoot.fullPath))
            {
                if (response.items.Count >= CharacterImportMaterialSourceCandidateMaxCount)
                {
                    response.truncated = true;
                    return response;
                }

                string fullPath = Path.GetFullPath(file);
                if (!seen.Add(fullPath))
                    continue;
                string extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!IsImportMaterialCandidateExtension(extension))
                    continue;

                CharacterImportMaterialSourceCandidate candidate = BuildImportMaterialSourceCandidate(rootPath, scanRoot, fullPath);
                if (!MatchesImportMaterialSourceCandidateQuery(candidate, query))
                    continue;
                response.items.Add(candidate);
            }
        }

        response.items.Sort((left, right) =>
        {
            int rank = GetImportMaterialSourceScopeRank(left.sourceScope).CompareTo(GetImportMaterialSourceScopeRank(right.sourceScope));
            return rank != 0 ? rank : string.Compare(left.displayPath, right.displayPath, StringComparison.OrdinalIgnoreCase);
        });
        return response;
    }

    internal static CharacterImportMaterialsState SaveCharacterImportMaterials(
        string rootPath,
        string packageRelative,
        string defaultPackage,
        CharacterImportMaterialsSaveRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackage = string.IsNullOrWhiteSpace(packageRelative) ? defaultPackage : packageRelative;
        string packagePath = ResolveSafePath(rootPath, resolvedPackage);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterImportMaterialsDocument document = request.document;
        if (document == null)
        {
            document = new CharacterImportMaterialsDocument
            {
                SourceRoot = request.sourceRoot ?? string.Empty,
                Items = request.items ?? new List<CharacterImportMaterialItem>()
            };
        }

        NormalizeCharacterImportMaterialsDocument(document);
        ValidateCharacterImportMaterialsDocument(document);
        WriteCharacterImportMaterialsDocument(rootPath, resolvedPackage, package, document, jsonOptions);
        return ReadCharacterImportMaterials(rootPath, resolvedPackage, defaultPackage, jsonOptions);
    }

    internal static CharacterImportMaterialsSyncResponse SyncCharacterImportMaterials(
        string rootPath,
        string packageRelative,
        string defaultPackage,
        CharacterImportMaterialsSyncRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackage = string.IsNullOrWhiteSpace(packageRelative) ? defaultPackage : packageRelative;
        CharacterImportMaterialsState before = ReadCharacterImportMaterials(rootPath, resolvedPackage, defaultPackage, jsonOptions);
        CharacterImportMaterialsDocument document = before.document ?? new CharacterImportMaterialsDocument();
        NormalizeCharacterImportMaterialsDocument(document);

        var response = new CharacterImportMaterialsSyncResponse
        {
            success = true,
            packageRelative = resolvedPackage,
            dryRun = request.dryRun
        };

        var selectedIds = new HashSet<string>(request.itemIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        bool explicitSelection = selectedIds.Count > 0;
        bool changedOnly = request.allChanged || !explicitSelection;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        for (int i = 0; i < before.items.Count; i++)
        {
            CharacterImportMaterialItemStatus status = before.items[i];
            CharacterImportMaterialItem item = document.Items.Count > i ? document.Items[i] : status.item;
            if (!ShouldSyncImportMaterialItem(status, explicitSelection, selectedIds, changedOnly, request.force))
                continue;

            CharacterImportMaterialsSyncOperation operation = SyncCharacterImportMaterialItem(
                rootPath,
                resolvedPackage,
                item,
                status,
                now,
                request,
                jsonOptions);
            response.operations.Add(operation);
            if (!operation.success && !operation.skipped)
                response.success = false;
        }

        var reconciliationDiagnostics = new List<CharacterImportMaterialsIntegrityDiagnostic>();
        if (!request.dryRun)
        {
            try
            {
                reconciliationDiagnostics.AddRange(ReconcileCharacterImportMaterialGraph(
                    rootPath,
                    resolvedPackage,
                    document,
                    jsonOptions));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException)
            {
                reconciliationDiagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_GRAPH_RECONCILE_FAILED",
                    "Error",
                    "Raw-material files were copied, but the model-material-texture graph could not be reconciled. " + ex.Message,
                    "Inspect the material descriptors and run source synchronization again."));
            }

            // Persist updated per-file hashes before rebuilding status; otherwise the same
            // sync response would still classify freshly copied sources as NotImported.
            WriteCharacterImportMaterialsStateDocument(
                rootPath,
                resolvedPackage,
                before.sourceManifestRelative,
                document,
                before.integrityReceipt,
                jsonOptions);
        }

        response.state = ReadCharacterImportMaterials(rootPath, resolvedPackage, defaultPackage, jsonOptions);
        MergeImportMaterialsDiagnostics(response.state.integrityReceipt, reconciliationDiagnostics);
        response.diagnostics.AddRange(response.state.integrityReceipt?.Diagnostics ?? new List<CharacterImportMaterialsIntegrityDiagnostic>());
        if (response.state.integrityReceipt?.BlockingIssueCount > 0)
            response.success = false;
        if (!request.dryRun)
            WriteCharacterImportMaterialsStateDocument(
                rootPath,
                resolvedPackage,
                before.sourceManifestRelative,
                document,
                response.state.integrityReceipt,
                jsonOptions);
        return response;
    }

    private static CharacterImportMaterialsSyncOperation SyncCharacterImportMaterialItem(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialItem item,
        CharacterImportMaterialItemStatus status,
        string importedUtc,
        CharacterImportMaterialsSyncRequest request,
        JsonSerializerOptions jsonOptions)
    {
        var operation = new CharacterImportMaterialsSyncOperation
        {
            itemId = status.id,
            label = item.Label ?? string.Empty,
            sourcePath = status.resolvedSourcePath,
            statusBefore = status.status
        };

        if (IsResourceRefImportMaterialItem(item))
        {
            ImportMaterialPreset referencePreset = ResolveImportMaterialPreset(item);
            if (request.dryRun)
            {
                operation.success = true;
                operation.skipped = true;
                operation.resourceKey = ResolveImportMaterialSelectionKey(item) ?? item.ResourceKey ?? string.Empty;
                operation.statusAfter = status.status;
                operation.message = "Dry run: local resource alias would be updated from the public resource reference.";
                return operation;
            }

            CharacterPackageResourceEntry alias = UpsertResourceRefAlias(rootPath, packageRelative, item, referencePreset, importedUtc, jsonOptions);
            item.ResourceKey = alias.ResourceKey ?? string.Empty;
            item.LastImportedHash = CharacterPackageHashUtility.NormalizeSha256(item.Source?.Selection?.ExpectedHash);
            item.LastImportedUtc = importedUtc;
            PersistImportMaterialMetadata(rootPath, packageRelative, item, alias, jsonOptions);
            BindImportedMaterialResource(rootPath, packageRelative, item, alias, referencePreset, jsonOptions);

            operation.success = true;
            operation.skipped = false;
            operation.resourceKey = alias.ResourceKey ?? string.Empty;
            operation.statusAfter = "Imported";
            operation.message = "Updated local resource alias to reference the selected public resource.";
            return operation;
        }

        if (!status.sourceSafe)
            return FailImportMaterialOperation(operation, true, "IMPORT_MATERIALS_UNSAFE_SOURCE_PATH", status.message);
        if (string.IsNullOrWhiteSpace(status.sourceFullPath) || !File.Exists(status.sourceFullPath))
            return FailImportMaterialOperation(operation, true, "IMPORT_MATERIALS_SOURCE_MISSING", status.message);
        if (!request.force && status.status != "NotImported" && !status.sourceDirty)
            return FailImportMaterialOperation(operation, true, "IMPORT_MATERIALS_SOURCE_UNCHANGED", "Source hash is unchanged; import skipped.");
        if (request.dryRun)
        {
            operation.success = true;
            operation.skipped = true;
            operation.message = "Dry run: source would be imported.";
            return operation;
        }

        ImportMaterialPreset preset = ResolveImportMaterialPreset(item);
        string targetResourceKey = ResolveImportMaterialTargetResourceKey(rootPath, packageRelative, item, preset, jsonOptions);
        string sourceFileName = Path.GetFileName(status.sourceFullPath);
        if (string.Equals(preset.bindTarget, "faceDetail", StringComparison.OrdinalIgnoreCase))
        {
            const string Prefix = "char.";
            const string Suffix = ".texture.face_detail";
            if (targetResourceKey.StartsWith(Prefix, StringComparison.Ordinal)
                && targetResourceKey.EndsWith(Suffix, StringComparison.Ordinal)
                && targetResourceKey.Length > Prefix.Length + Suffix.Length)
            {
                string packageId = targetResourceKey.Substring(
                    Prefix.Length,
                    targetResourceKey.Length - Prefix.Length - Suffix.Length);
                sourceFileName = packageId + "_face_detail_rgba.png";
            }
        }
        string bytesBase64 = Convert.ToBase64String(File.ReadAllBytes(status.sourceFullPath));
        CharacterPackageResourceEntry imported;
        if (string.Equals(preset.kind, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
        {
            imported = ImportCharacterModel(rootPath, packageRelative, new CharacterStudioModelImportRequest
            {
                fileName = sourceFileName,
                role = preset.role,
                resourceKey = targetResourceKey,
                targetRelativePath = BuildImportMaterialModelTargetRelativePath(sourceFileName),
                bytesBase64 = bytesBase64
            }, jsonOptions);
        }
        else
        {
            imported = ImportGenericCharacterResource(rootPath, packageRelative, new ResourceLibraryWriteRequest
            {
                fileName = sourceFileName,
                resourceKey = targetResourceKey,
                kind = preset.kind,
                usage = preset.usage,
                localId = preset.localId,
                bytesBase64 = bytesBase64,
                tags = new List<string> { "import-materials" }
            }, existingEntry: null, preserveExistingPath: false, jsonOptions);
        }

        item.ResourceKey = imported.ResourceKey ?? string.Empty;
        item.LastImportedHash = status.sourceHash;
        item.LastImportedUtc = importedUtc;
        PersistImportMaterialMetadata(rootPath, packageRelative, item, imported, jsonOptions);
        BindImportedMaterialResource(rootPath, packageRelative, item, imported, preset, jsonOptions);

        operation.success = true;
        operation.skipped = false;
        operation.resourceKey = imported.ResourceKey ?? string.Empty;
        operation.statusAfter = "Imported";
        operation.message = "Imported source file.";
        return operation;
    }

    private static CharacterPackageResourceEntry UpsertResourceRefAlias(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialItem item,
        ImportMaterialPreset preset,
        string importedUtc,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.ResourceCatalog ??= new CharacterPackageResourceCatalog();
        package.ResourceCatalog.Entries ??= new List<CharacterPackageResourceEntry>();
        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string typeId = NormalizeImportMaterialResourceTypeId(FirstNonEmpty(item.Target?.ResourceKind, preset.kind));
        string usage = FirstNonEmpty(item.Target?.ResourceUsage, preset.usage, GetDefaultResourceUsage(typeId));
        string localId = BuildImportMaterialAliasLocalId(item, preset, typeId);
        string resourceLocalId = BuildImportMaterialAliasResourceLocalId(item, preset);
        string resourceKey = FirstNonEmpty(
            item.Target?.ResourceKey,
            item.ResourceKey,
            AuthoringResourceNaming.BuildCharacterResourceKey(packageId, typeId, resourceLocalId));
        CharacterPackageResourceEntry entry = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, resourceKey);
        if (entry == null)
        {
            entry = new CharacterPackageResourceEntry();
            package.ResourceCatalog.Entries.Add(entry);
        }

        AuthoringResourceSelectionRef selection = CloneAuthoringResourceSelection(item.Source?.Selection);
        entry.ResourceKey = resourceKey;
        entry.LocalId = localId;
        entry.StableId = string.IsNullOrWhiteSpace(entry.StableId)
            ? AuthoringResourceNaming.BuildCharacterPackageFallbackStableId(packageId, localId)
            : entry.StableId;
        entry.TypeId = typeId;
        entry.Variant = string.IsNullOrWhiteSpace(entry.Variant) ? "default" : entry.Variant;
        entry.Usage = usage;
        entry.SourceFormat = CharacterSourceResourceRefKind;
        entry.PackageId = packageId;
        entry.RelativePath = string.Empty;
        entry.Hash = CharacterPackageHashUtility.NormalizeSha256(selection.ExpectedHash);
        entry.ResourceSelection = selection;
        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.ProviderId = "assetBundle";
        entry.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.ProjectResourceCatalogOnly;
        entry.ImportHints.Metadata ??= new Dictionary<string, string>();
        entry.ImportHints.Metadata["resourceSelectionAlias"] = "true";
        entry.ImportHints.Metadata["runtimeBindingKind"] = AuthoringResourceBindingKind.ResourceManagerAsset.ToString();
        entry.ImportHints.Metadata["sourceProviderId"] = selection.SourceProviderId ?? string.Empty;
        entry.ImportHints.Metadata["runtimeResourceKey"] = selection.RuntimeResourceKey ?? string.Empty;
        entry.ImportHints.Metadata["providerResourceKey"] = selection.ProviderResourceKey ?? string.Empty;
        entry.ImportHints.Metadata["unityAssetPath"] = selection.UnityAssetPath ?? string.Empty;
        entry.ImportHints.Metadata["unityGuid"] = selection.UnityGuid ?? string.Empty;
        entry.ImportHints.Metadata["usage"] = usage;
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.ContentHash = CharacterPackageHashUtility.NormalizeSha256(selection.ExpectedHash);
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog);
        entry.Tags ??= new List<string>();
        AddUniqueString(entry.Tags, "public-resource");
        AddUniqueString(entry.Tags, "resource-ref");
        entry.Provenance ??= new CharacterPackageResourceProvenance();
        entry.Provenance.SourceTool = "CharacterStudio";
        entry.Provenance.SourceFile = "resourceRef:" + ResolveImportMaterialSelectionKey(item);
        entry.Provenance.ModifiedBy = "CharacterStudio";
        entry.Provenance.ModifiedUtc = importedUtc;

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return entry;
    }

    private static string NormalizeImportMaterialResourceTypeId(string typeId)
    {
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Model;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Texture;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Material;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Animation;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Audio;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Vfx;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Preview;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceTypeIds.Geometry;
        return CharacterPackageResourceTypeIds.Config;
    }

    private static string BuildImportMaterialAliasLocalId(CharacterImportMaterialItem item, ImportMaterialPreset preset, string typeId)
    {
        if (string.Equals(preset.bindTarget, "body", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.BindTarget, "body", StringComparison.OrdinalIgnoreCase))
            return "model.body";
        string segment = BuildImportMaterialAliasResourceLocalId(item, preset);
        string typeSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(typeId);
        return string.IsNullOrWhiteSpace(segment) ? typeSegment : typeSegment + "." + segment;
    }

    private static string BuildImportMaterialAliasResourceLocalId(CharacterImportMaterialItem item, ImportMaterialPreset preset)
    {
        if (string.Equals(preset.bindTarget, "body", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.BindTarget, "body", StringComparison.OrdinalIgnoreCase))
            return "body";
        string seed = FirstNonEmpty(item.Target?.Slot, item.Slot, item.Id, item.Label, ResolveImportMaterialSelectionKey(item), "resource");
        return CharacterPackageResourceKeyGenerator.NormalizeSegment(seed);
    }

    private static AuthoringResourceSelectionRef CloneAuthoringResourceSelection(AuthoringResourceSelectionRef selection)
    {
        if (selection == null)
            return new AuthoringResourceSelectionRef();
        return new AuthoringResourceSelectionRef
        {
            ResourceStableId = selection.ResourceStableId ?? string.Empty,
            SourceProviderId = selection.SourceProviderId ?? string.Empty,
            BindingKind = selection.BindingKind,
            ExpectedKind = selection.ExpectedKind ?? string.Empty,
            ExpectedUsage = selection.ExpectedUsage ?? string.Empty,
            ExpectedHash = selection.ExpectedHash ?? string.Empty,
            UnityGuid = selection.UnityGuid ?? string.Empty,
            UnityAssetPath = selection.UnityAssetPath ?? string.Empty,
            RuntimeResourceKey = selection.RuntimeResourceKey ?? string.Empty,
            ProviderResourceKey = selection.ProviderResourceKey ?? string.Empty,
            PackageResourceKey = selection.PackageResourceKey ?? string.Empty,
            AudioCueId = selection.AudioCueId ?? string.Empty,
            AudioEventDefinitionId = selection.AudioEventDefinitionId ?? string.Empty
        };
    }

    private static void AddUniqueString(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
            return;
        if (!values.Contains(value, StringComparer.Ordinal))
            values.Add(value);
    }

    private static string BuildImportMaterialModelTargetRelativePath(string sourceFileName)
    {
        string extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(sourceFileName)).Replace('.', '_');
        return ("resources/models/" + fileStem + extension).Replace('\\', '/');
    }

    private static string ResolveImportMaterialTargetResourceKey(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialItem item,
        ImportMaterialPreset preset,
        JsonSerializerOptions jsonOptions)
    {
        if (!string.IsNullOrWhiteSpace(item.ResourceKey))
            return item.ResourceKey;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        CharacterPackageResourceEntry entry = null;

        if (string.Equals(preset.kind, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(preset.bindTarget, "body", StringComparison.OrdinalIgnoreCase))
            {
                entry = catalog.Entries.FirstOrDefault(candidate =>
                    candidate != null &&
                    (string.Equals(candidate.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidate.LocalId, "model.body", StringComparison.OrdinalIgnoreCase)));
            }
            else if (string.Equals(preset.bindTarget, "mainHand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(preset.bindTarget, "offHand", StringComparison.OrdinalIgnoreCase))
            {
                SingleWeaponConfiguration definition = package.GlobalWeaponLibrary?.SingleWeaponConfigurations?.FirstOrDefault(candidate =>
                    candidate != null && candidate.AllowedSlots != null && candidate.AllowedSlots.Contains(preset.bindTarget, StringComparer.OrdinalIgnoreCase));
                string weaponResourceKey = GetWeaponDefinitionReferenceKey(definition);
                if (!string.IsNullOrWhiteSpace(weaponResourceKey))
                    entry = CharacterPackageResourcePipeline.FindByKey(catalog, weaponResourceKey);
            }
        }
        else if (string.Equals(preset.kind, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.Slot))
        {
            string expectedLocalId = string.IsNullOrWhiteSpace(preset.localId)
                ? "anim." + CharacterPackageResourceKeyGenerator.NormalizeSegment(item.Slot)
                : preset.localId;
            entry = catalog.Entries.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.TypeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(candidate.LocalId, expectedLocalId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.ResourceKey, "char." + CharacterPackageResourceKeyGenerator.NormalizeSegment(package.Manifest?.PackageId ?? string.Empty) + "." + expectedLocalId, StringComparison.OrdinalIgnoreCase)));
        }

        return entry?.ResourceKey ?? string.Empty;
    }

    private static void BindImportedMaterialResource(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialItem item,
        CharacterPackageResourceEntry entry,
        ImportMaterialPreset preset,
        JsonSerializerOptions jsonOptions)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
            return;

        bool shouldBindAnimation = string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.Slot);
        bool shouldEnsureApplicationResource = shouldBindAnimation
            || string.Equals(preset.bindTarget, "body", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preset.bindTarget, "mainHand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preset.bindTarget, "offHand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(preset.bindTarget, "faceDetail", StringComparison.OrdinalIgnoreCase);
        if (!shouldBindAnimation && !shouldEnsureApplicationResource)
            return;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.ApplicationConfig ??= new CharacterApplicationAuthoringSummary();
        EnsureApplicationResourceKey(package.ApplicationConfig, entry.ResourceKey);

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
    }

    private static AuthoringResourceSelectionRef CreatePackageResourceSelection(CharacterPackageResourceEntry entry)
    {
        return new AuthoringResourceSelectionRef
        {
            ResourceStableId = entry.StableId ?? string.Empty,
            SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
            BindingKind = AuthoringResourceBindingKind.PackageResource,
            ExpectedKind = entry.TypeId ?? string.Empty,
            ExpectedUsage = entry.Usage ?? string.Empty,
            ExpectedHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
            ProviderResourceKey = entry.ResourceKey ?? string.Empty,
            PackageResourceKey = entry.ResourceKey ?? string.Empty
        };
    }

    private static void EnsureApplicationResourceKey(CharacterApplicationAuthoringSummary application, string resourceKey)
    {
        if (application == null || string.IsNullOrWhiteSpace(resourceKey))
            return;
        application.ResourceKeys ??= new List<string>();
        if (!application.ResourceKeys.Contains(resourceKey, StringComparer.Ordinal))
            application.ResourceKeys.Add(resourceKey);
    }

    private static CharacterImportMaterialsSyncOperation FailImportMaterialOperation(
        CharacterImportMaterialsSyncOperation operation,
        bool skipped,
        string code,
        string message)
    {
        operation.success = skipped;
        operation.skipped = skipped;
        operation.code = code;
        operation.message = string.IsNullOrWhiteSpace(message) ? code : message;
        operation.statusAfter = operation.statusBefore;
        return operation;
    }

    private static bool ShouldSyncImportMaterialItem(
        CharacterImportMaterialItemStatus status,
        bool explicitSelection,
        HashSet<string> selectedIds,
        bool changedOnly,
        bool force)
    {
        if (status == null)
            return false;
        if (explicitSelection && !selectedIds.Contains(status.id))
            return false;
        if (force || explicitSelection && !changedOnly)
            return true;
        return status.status == "NotImported" || status.sourceDirty;
    }

    private static CharacterImportMaterialItemStatus BuildImportMaterialItemStatus(
        string rootPath,
        string packageRelative,
        string packagePath,
        CharacterResourcePackage package,
        CharacterImportMaterialsLoad load,
        CharacterImportMaterialsDocument document,
        CharacterImportMaterialItem item,
        int index,
        JsonDocument unityCatalog)
    {
        ImportMaterialResolvedSource source = ResolveImportMaterialSource(rootPath, packagePath, load, document.SourceRoot, item);
        CharacterPackageResourceEntry entry = FindImportMaterialResourceEntry(package.ResourceCatalog, item, source);
        string sourceHash = source.exists ? CharacterPackageHashUtility.ComputeFileSha256(source.fullPath) : string.Empty;
        string lastHash = CharacterPackageHashUtility.NormalizeSha256(item.LastImportedHash);
        bool referenceOnly = IsResourceRefImportMaterialItem(item);
        bool imported = referenceOnly
            ? HasImportMaterialResourceSelection(item) || entry != null || !string.IsNullOrWhiteSpace(item.ResourceKey)
            : entry != null && !string.IsNullOrWhiteSpace(lastHash);
        bool dirty = source.exists && imported && !string.Equals(sourceHash, lastHash, StringComparison.OrdinalIgnoreCase);
        ImportMaterialUnityStatus unity = BuildImportMaterialUnityStatus(unityCatalog, entry);
        CharacterImportMaterialFbxTextureDiagnostic fbxTextureDiagnostic = BuildFbxTextureDiagnostic(source);
        string status;
        string message;
        if (!source.safe)
        {
            status = "Blocked";
            message = source.message;
        }
        else if (!source.exists)
        {
            status = item.Required ? "Missing" : "OptionalMissing";
            message = "Source file was not found.";
        }
        else if (!imported)
        {
            status = "NotImported";
            message = referenceOnly ? "Resource reference has not been resolved." : "Source file has not been imported from this checklist.";
        }
        else if (!referenceOnly && dirty)
        {
            status = "Changed";
            message = "Source file hash differs from last imported hash.";
        }
        else if (entry != null && !unity.synced)
        {
            status = "UnityPending";
            message = "Package resource exists, but Unity resource catalog has not caught up.";
        }
        else
        {
            status = "Clean";
            message = "Source file and package resource are in sync.";
        }

        return new CharacterImportMaterialItemStatus
        {
            index = index,
            id = item.Id ?? string.Empty,
            label = item.Label ?? string.Empty,
            item = item,
            sourceSafe = source.safe,
            sourceExists = source.exists,
            sourceHash = sourceHash,
            sourceDirty = dirty || status == "NotImported",
            resolvedSourcePath = source.displayPath,
            sourceFullPath = source.fullPath,
            sourceReferenceKind = source.referenceKind,
            sourceScope = source.scope,
            resourceKey = entry?.ResourceKey ?? ResolveImportMaterialSelectionKey(item) ?? item.ResourceKey ?? string.Empty,
            resourceExists = entry != null,
            resourceRelativePath = entry?.RelativePath ?? string.Empty,
            unitySynced = unity.synced,
            unityImportStatus = unity.importStatus,
            unityAssetPath = unity.assetPath,
            fbxTextureDiagnostic = fbxTextureDiagnostic,
            status = status,
            message = message
        };
    }

    private static CharacterImportMaterialFbxTextureDiagnostic BuildFbxTextureDiagnostic(ImportMaterialResolvedSource source)
    {
        if (source == null
            || !source.exists
            || string.IsNullOrWhiteSpace(source.fullPath)
            || !string.Equals(Path.GetExtension(source.fullPath), ".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(source.fullPath);
            if (!file.Exists)
                return null;
            if (file.Length > FbxTextureDiagnosticScanMaxBytes)
            {
                return new CharacterImportMaterialFbxTextureDiagnostic
                {
                    status = "unknown",
                    message = "FBX is too large for quick texture-source inspection."
                };
            }

            string text = Encoding.UTF8.GetString(File.ReadAllBytes(source.fullPath));
            bool hasEmbeddedContent = ContainsOrdinalIgnoreCase(text, "Content");
            bool hasTextureObject = ContainsOrdinalIgnoreCase(text, "Texture::")
                || ContainsOrdinalIgnoreCase(text, "Video::")
                || ContainsOrdinalIgnoreCase(text, "RelativeFilename")
                || ContainsOrdinalIgnoreCase(text, "FileName");
            List<string> externalRefs = ExtractFbxTextureReferences(text);
            if (hasEmbeddedContent && hasTextureObject)
            {
                return new CharacterImportMaterialFbxTextureDiagnostic
                {
                    status = "embeddedTextureDetected",
                    embeddedTextureDetected = true,
                    externalTexturePaths = externalRefs,
                    message = "Embedded FBX texture/media data was detected."
                };
            }

            if (externalRefs.Count > 0)
            {
                return new CharacterImportMaterialFbxTextureDiagnostic
                {
                    status = "externalTextureRefs",
                    externalTextureRefs = true,
                    externalTexturePaths = externalRefs,
                    message = "The FBX references external texture file paths."
                };
            }

            return new CharacterImportMaterialFbxTextureDiagnostic
            {
                status = "noTextureEvidence",
                message = "No embedded texture or external texture reference was detected in the FBX."
            };
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DecoderFallbackException)
        {
            return new CharacterImportMaterialFbxTextureDiagnostic
            {
                status = "unknown",
                message = "FBX texture-source inspection failed: " + ex.Message
            };
        }
    }

    private static List<string> ExtractFbxTextureReferences(string text)
    {
        var refs = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return refs;
        foreach (Match match in FbxTexturePathRegex.Matches(text))
        {
            string value = NormalizeImportMaterialRelativePath(match.Value.Trim(' ', '\t', '\r', '\n', '"', '\''));
            if (string.IsNullOrWhiteSpace(value) || refs.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                continue;
            refs.Add(value);
            if (refs.Count >= 16)
                break;
        }

        return refs;
    }

    private static bool ContainsOrdinalIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text)
            && !string.IsNullOrEmpty(value)
            && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static CharacterPackageResourceEntry FindImportMaterialResourceEntry(
        CharacterPackageResourceCatalog catalog,
        CharacterImportMaterialItem item,
        ImportMaterialResolvedSource source)
    {
        if (catalog == null || catalog.Entries == null || item == null)
            return null;
        if (!string.IsNullOrWhiteSpace(item.ResourceKey))
            return CharacterPackageResourcePipeline.FindByKey(catalog, item.ResourceKey);
        if (IsResourceRefImportMaterialItem(item))
        {
            string selectionKey = ResolveImportMaterialSelectionKey(item);
            if (!string.IsNullOrWhiteSpace(selectionKey))
            {
                CharacterPackageResourceEntry selected = CharacterPackageResourcePipeline.FindByKey(catalog, selectionKey);
                if (selected != null)
                    return selected;
            }
        }

        string sourceFileName = Path.GetFileName(source.fullPath ?? item.SourcePath ?? string.Empty);
        return catalog.Entries.FirstOrDefault(entry =>
            entry != null
            && entry.Provenance != null
            && !string.IsNullOrWhiteSpace(entry.Provenance.SourceFile)
            && string.Equals(Path.GetFileName(entry.Provenance.SourceFile), sourceFileName, StringComparison.OrdinalIgnoreCase));
    }

    private static ImportMaterialUnityStatus BuildImportMaterialUnityStatus(JsonDocument unityCatalog, CharacterPackageResourceEntry entry)
    {
        var status = new ImportMaterialUnityStatus();
        if (unityCatalog == null || entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
            return status;
        if (!unityCatalog.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
            return status;

        foreach (JsonElement element in entries.EnumerateArray())
        {
            string packageKey = FirstNonEmpty(
                GetJsonString(element, "packageResourceKey"),
                GetJsonString(element, "id"),
                GetJsonString(GetJsonObject(element, "providerData"), "packageResourceKey"));
            string stableId = FirstNonEmpty(
                GetJsonString(element, "stableId"),
                GetJsonString(GetJsonObject(element, "providerData"), "stableId"));
            if (!string.Equals(packageKey, entry.ResourceKey, StringComparison.Ordinal) &&
                !string.Equals(stableId, entry.StableId, StringComparison.Ordinal))
                continue;

            status.synced = true;
            status.importStatus = FirstNonEmpty(
                GetJsonString(element, "importStatus"),
                GetJsonString(GetJsonObject(element, "providerData"), "importStatus"));
            status.assetPath = FirstNonEmpty(
                GetJsonString(element, "unityAssetPath"),
                GetJsonString(GetJsonObject(element, "providerData"), "unityAssetPath"),
                GetJsonString(element, "address"));
            return status;
        }

        return status;
    }

    private static List<CharacterImportMaterialsIntegrityDiagnostic> ReconcileCharacterImportMaterialGraph(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialsDocument document,
        JsonSerializerOptions jsonOptions)
    {
        var diagnostics = new List<CharacterImportMaterialsIntegrityDiagnostic>();
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.ResourceCatalog ??= new CharacterPackageResourceCatalog();
        package.ResourceCatalog.Entries ??= new List<CharacterPackageResourceEntry>();
        CharacterPackageResourceEntry bodyModel = ResolveImportMaterialsBodyModel(package, document);
        List<ResolvedImportTexture> textures = ResolveImportMaterialTextures(package, document);
        if (bodyModel == null || textures.Count == 0)
            return diagnostics;

        var materialEntries = new List<CharacterPackageResourceEntry>();
        foreach (IGrouping<string, ResolvedImportTexture> group in textures
                     .Where(item => !string.IsNullOrWhiteSpace(item.Slot) && !string.IsNullOrWhiteSpace(item.Semantic))
                     .GroupBy(item => item.Slot, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            ResolvedImportTexture color = group.FirstOrDefault(item => item.Semantic == CharacterTextureSemanticIds.Color);
            if (color?.Entry == null)
                continue;

            string declaredSurfaceMode = color.Item?.Target?.MaterialSurfaceMode ?? string.Empty;
            string surfaceMode = CharacterMaterialSurfaceModeIds.Normalize(declaredSurfaceMode);
            if (!string.IsNullOrWhiteSpace(declaredSurfaceMode) && string.IsNullOrWhiteSpace(surfaceMode))
            {
                diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_SURFACE_MODE_UNSUPPORTED",
                    "Error",
                    "Material slot '" + group.Key + "' declares unsupported surface mode '" + declaredSurfaceMode + "'.",
                    "Use Opaque or AlphaClip on the slot's Color source target.",
                    itemId: color.Item?.Id,
                    resourceKey: color.Entry.ResourceKey));
                continue;
            }
            if (string.IsNullOrWhiteSpace(surfaceMode))
                surfaceMode = CharacterMaterialSurfaceModeIds.Opaque;
            float alphaCutoff = color.Item?.Target?.AlphaCutoff ?? 0.5f;
            if (string.Equals(surfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                && (float.IsNaN(alphaCutoff) || float.IsInfinity(alphaCutoff) || alphaCutoff < 0f || alphaCutoff > 1f))
            {
                diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_ALPHA_CUTOFF_INVALID",
                    "Error",
                    "Material slot '" + group.Key + "' declares AlphaClip with cutoff " + alphaCutoff.ToString(CultureInfo.InvariantCulture) + ".",
                    "Set alphaCutoff to a value from 0 through 1.",
                    itemId: color.Item?.Id,
                    resourceKey: color.Entry.ResourceKey));
                continue;
            }

            string slot = CharacterPackageResourceKeyGenerator.NormalizeSegment(group.Key);
            string sourceMaterialName = BuildImportMaterialSourceName(slot);
            CharacterPackageResourceEntry material = FindImportMaterialDescriptorEntry(package.ResourceCatalog, slot, sourceMaterialName);
            bool createdEntry = material == null;
            if (createdEntry)
            {
                material = new CharacterPackageResourceEntry();
                package.ResourceCatalog.Entries.Add(material);
            }

            string descriptorRelativePath = !string.IsNullOrWhiteSpace(material.RelativePath)
                ? material.RelativePath.Replace('\\', '/')
                : "resources/materials/" + sourceMaterialName + "/" + sourceMaterialName + ".material.json";
            if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(descriptorRelativePath)
                || !descriptorRelativePath.EndsWith(".material.json", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_DESCRIPTOR_PATH_INVALID",
                    "Error",
                    "Material slot '" + group.Key + "' has an invalid descriptor path: " + descriptorRelativePath + ".",
                    "Choose a package-relative resources/materials/*.material.json path.",
                    resourceKey: material.ResourceKey,
                    path: descriptorRelativePath));
                continue;
            }

            string descriptorPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, descriptorRelativePath);
            bool descriptorExisted = File.Exists(descriptorPath);
            bool managedDescriptor = material.ImportHints?.Metadata != null
                && material.ImportHints.Metadata.TryGetValue("rawMaterialDescriptorOwner", out string owner)
                && string.Equals(owner, "CharacterStudio.ImportMaterials", StringComparison.Ordinal);
            bool wroteManagedDescriptor = !descriptorExisted || managedDescriptor;
            if (wroteManagedDescriptor)
            {
                WriteGeneratedImportMaterialDescriptor(
                    descriptorPath,
                    sourceMaterialName,
                    group.ToList(),
                    surfaceMode,
                    alphaCutoff,
                    jsonOptions);
                diagnostics.Add(ImportMaterialsDiagnostic(
                    File.Exists(descriptorPath) && managedDescriptor ? "MATERIAL_DESCRIPTOR_UPDATED" : "MATERIAL_DESCRIPTOR_GENERATED",
                    "Info",
                    "Generated package material descriptor for source material '" + sourceMaterialName + "'.",
                    string.Empty,
                    resourceKey: material.ResourceKey,
                    path: descriptorRelativePath));
            }

            if (!File.Exists(descriptorPath))
                continue;

            string packageId = package.Manifest?.PackageId ?? string.Empty;
            string normalizedPackageId = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
            material.ResourceKey = string.IsNullOrWhiteSpace(material.ResourceKey)
                ? "char." + normalizedPackageId + ".material." + slot
                : material.ResourceKey;
            material.LocalId = string.IsNullOrWhiteSpace(material.LocalId) ? "material." + slot : material.LocalId;
            material.StableId = string.IsNullOrWhiteSpace(material.StableId)
                ? "charpkg." + normalizedPackageId + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(material.LocalId)
                : material.StableId;
            material.TypeId = CharacterPackageResourceTypeIds.Material;
            material.Variant = string.IsNullOrWhiteSpace(material.Variant) ? "default" : material.Variant;
            material.Usage = CharacterPackageResourceUsageIds.Material;
            material.SourceFormat = CharacterPackageResourceFormatIds.MaterialJson;
            material.PackageId = packageId;
            material.RelativePath = descriptorRelativePath;
            material.ImportHints ??= new CharacterPackageImportHint();
            material.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
            material.ImportHints.TargetRelativePath = descriptorRelativePath;
            material.ImportHints.ProviderId = string.IsNullOrWhiteSpace(material.ImportHints.ProviderId) ? "unityAsset" : material.ImportHints.ProviderId;
            material.ImportHints.Metadata ??= new Dictionary<string, string>();
            if (wroteManagedDescriptor)
                material.ImportHints.Metadata["rawMaterialDescriptorOwner"] = "CharacterStudio.ImportMaterials";
            material.ImportHints.Metadata["sourceMaterialName"] = sourceMaterialName;
            material.Provenance ??= new CharacterPackageResourceProvenance();
            if (string.IsNullOrWhiteSpace(material.Provenance.SourceTool))
                material.Provenance.SourceTool = "CharacterStudio.ImportMaterials";
            if (string.IsNullOrWhiteSpace(material.Provenance.SourceFile))
                material.Provenance.SourceFile = sourceMaterialName + ".mat";

            MaterialDescriptorSnapshot descriptor = ReadImportMaterialDescriptor(descriptorPath, descriptorRelativePath);
            ReplaceResourceDependencies(material, "usesTexture", descriptor.TextureResourceKeys);
            material.Hash = CharacterPackageHashUtility.ComputeFileSha256(descriptorPath);
            material.Hashes ??= new CharacterPackageResourceHashes();
            material.Hashes.Algorithm = "sha256";
            material.Hashes.ContentHash = material.Hash;
            material.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(material);
            materialEntries.Add(material);
        }

        if (materialEntries.Count == 0)
            return diagnostics;

        ReplaceResourceDependencies(bodyModel, "usesMaterial", materialEntries.Select(entry => entry.ResourceKey));
        bodyModel.ImportHints ??= new CharacterPackageImportHint();
        bodyModel.ImportHints.MaterialPolicy = "generateFromPackageMaterialDescriptors";
        bodyModel.ImportHints.Metadata ??= new Dictionary<string, string>();
        bodyModel.ImportHints.Metadata["rendererMaterialBindingsVersion"] = "2";
        bodyModel.ImportHints.Metadata["rendererMaterialBindingsJson"] = JsonSerializer.Serialize(new
        {
            bindingMode = "sourceMaterialName",
            bindings = materialEntries.Select(entry => new
            {
                rendererNodePath = string.Empty,
                materialSlotIndex = -1,
                materialResourceKey = entry.ResourceKey,
                sourceMaterialName = FirstNonEmpty(
                    entry.ImportHints?.Metadata != null && entry.ImportHints.Metadata.TryGetValue("sourceMaterialName", out string name) ? name : string.Empty,
                    Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(entry.RelativePath)))
            }).ToArray()
        }, jsonOptions);
        bodyModel.ImportHints.Metadata["sourceRendererMaterialBindingCount"] = materialEntries.Count.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i < materialEntries.Count; i++)
        {
            CharacterPackageResourceEntry material = materialEntries[i];
            material.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(material, package.ResourceCatalog);
        }
        bodyModel.Hashes ??= new CharacterPackageResourceHashes();
        bodyModel.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(bodyModel);
        bodyModel.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(bodyModel, package.ResourceCatalog);
        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);

        diagnostics.Add(ImportMaterialsDiagnostic(
            "DEPENDENCY_GRAPH_RECONCILED",
            "Info",
            "Reconciled " + materialEntries.Count.ToString(CultureInfo.InvariantCulture)
                + " model-to-material bindings and their descriptor texture dependencies.",
            string.Empty,
            resourceKey: bodyModel.ResourceKey));
        diagnostics.Add(ImportMaterialsDiagnostic(
            "SOURCE_MATERIAL_BINDINGS_RECONCILED",
            "Info",
            "Declared source-material-name bindings for: " + string.Join(", ", materialEntries.Select(entry =>
                entry.ImportHints.Metadata.TryGetValue("sourceMaterialName", out string name) ? name : entry.ResourceKey)) + ".",
            string.Empty,
            resourceKey: bodyModel.ResourceKey));
        return diagnostics;
    }

    private static CharacterImportMaterialsIntegrityReceipt BuildImportMaterialsIntegrityReceipt(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        string sourceManifestRelative,
        CharacterImportMaterialsDocument document,
        JsonSerializerOptions jsonOptions)
    {
        var receipt = new CharacterImportMaterialsIntegrityReceipt
        {
            PackageId = package?.Manifest?.PackageId ?? string.Empty,
            SourceManifestRelative = NormalizeImportMaterialRelativePath(sourceManifestRelative),
            EvaluatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            SourceItemCount = document?.Items?.Count ?? 0
        };
        if (package?.ResourceCatalog == null || document?.Items == null)
            return receipt;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterPackageResourceEntry bodyModel = ResolveImportMaterialsBodyModel(package, document);
        List<ResolvedImportTexture> textures = ResolveImportMaterialTextures(package, document);
        receipt.TextureCount = textures.Count;
        if (bodyModel == null && document.Items.Any(IsImportMaterialsBodyItem))
        {
            receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                "BODY_MODEL_RESOURCE_MISSING",
                "Error",
                "The body source is configured, but no package characterModel resource exists.",
                "Synchronize the body source before importing Unity."));
        }

        if (textures.Count == 0)
        {
            FinalizeImportMaterialsIntegrityReceipt(receipt);
            return receipt;
        }

        for (int i = 0; i < textures.Count; i++)
        {
            ResolvedImportTexture texture = textures[i];
            if (texture.Entry == null)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "TEXTURE_RESOURCE_MISSING",
                    texture.Item?.Required == false ? "Warning" : "Error",
                    "Tracked texture source has no package texture resource: " + (texture.ResourceKey ?? texture.Item?.Id ?? "unknown") + ".",
                    "Synchronize this source item again.",
                    itemId: texture.Item?.Id,
                    resourceKey: texture.ResourceKey));
            }
            if (string.IsNullOrWhiteSpace(texture.Semantic))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "TEXTURE_SEMANTIC_UNRESOLVED",
                    texture.Item?.Required == false ? "Warning" : "Error",
                    "Texture semantic is not Color, Normal, or Mask for source item '" + (texture.Item?.Id ?? "unknown") + "'.",
                    "Set target.textureSemantic in character_sources.json.",
                    itemId: texture.Item?.Id,
                    resourceKey: texture.ResourceKey));
            }
            if (string.IsNullOrWhiteSpace(texture.Slot))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "TEXTURE_MATERIAL_SLOT_UNRESOLVED",
                    texture.Item?.Required == false ? "Warning" : "Error",
                    "Texture source item '" + (texture.Item?.Id ?? "unknown") + "' is not assigned to a material slot.",
                    "Set target.slot, for example body or eye.",
                    itemId: texture.Item?.Id,
                    resourceKey: texture.ResourceKey));
            }
        }

        List<IGrouping<string, ResolvedImportTexture>> groups = textures
            .Where(item => !string.IsNullOrWhiteSpace(item.Slot))
            .GroupBy(item => item.Slot, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        receipt.ExpectedMaterialCount = groups.Count;
        Dictionary<string, RendererMaterialBindingSnapshot> rendererBindings = ReadImportMaterialRendererBindings(bodyModel, jsonOptions);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            IGrouping<string, ResolvedImportTexture> group = groups[groupIndex];
            string slot = CharacterPackageResourceKeyGenerator.NormalizeSegment(group.Key);
            string sourceMaterialName = BuildImportMaterialSourceName(slot);
            var bindingReceipt = new CharacterImportMaterialBindingReceipt
            {
                Slot = group.Key,
                SourceMaterialName = sourceMaterialName
            };
            receipt.Materials.Add(bindingReceipt);

            foreach (IGrouping<string, ResolvedImportTexture> semanticGroup in group
                         .Where(item => !string.IsNullOrWhiteSpace(item.Semantic))
                         .GroupBy(item => item.Semantic, StringComparer.Ordinal))
            {
                if (semanticGroup.Count() > 1)
                {
                    receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                        "TEXTURE_SEMANTIC_AMBIGUOUS",
                        "Error",
                        "Material slot '" + group.Key + "' has multiple " + semanticGroup.Key + " textures.",
                        "Keep one texture per semantic and material slot."));
                }
            }

            ResolvedImportTexture color = group.FirstOrDefault(item => item.Semantic == CharacterTextureSemanticIds.Color);
            if (color?.Entry == null)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_BASE_COLOR_MISSING",
                    "Error",
                    "Material slot '" + group.Key + "' does not resolve a Color texture.",
                    "Add or classify the slot's BaseMap/Albedo texture as Color."));
            }

            CharacterPackageResourceEntry material = FindImportMaterialDescriptorEntry(package.ResourceCatalog, slot, sourceMaterialName);
            if (material == null)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_DESCRIPTOR_MISSING",
                    "Error",
                    "Material slot '" + group.Key + "' has textures but no package material descriptor.",
                    "Synchronize raw materials to generate the descriptor."));
                continue;
            }

            bindingReceipt.MaterialResourceKey = material.ResourceKey ?? string.Empty;
            bindingReceipt.DescriptorRelativePath = material.RelativePath ?? string.Empty;
            if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(material.RelativePath)
                || !material.RelativePath.EndsWith(".material.json", StringComparison.OrdinalIgnoreCase))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_DESCRIPTOR_INVALID",
                    "Error",
                    "Material descriptor path is invalid for slot '" + group.Key + "': " + material.RelativePath + ".",
                    "Use a safe package-relative *.material.json path.",
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
                continue;
            }
            string descriptorPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, material.RelativePath);
            if (!File.Exists(descriptorPath))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_DESCRIPTOR_MISSING",
                    "Error",
                    "Material descriptor file is missing for slot '" + group.Key + "': " + material.RelativePath + ".",
                    "Synchronize raw materials to regenerate a tool-owned descriptor, or restore the authored descriptor.",
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
                continue;
            }
            MaterialDescriptorSnapshot descriptor;
            try
            {
                descriptor = ReadImportMaterialDescriptor(descriptorPath, material.RelativePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_DESCRIPTOR_INVALID",
                    "Error",
                    "Material descriptor could not be read: " + ex.Message,
                    "Repair or regenerate the material descriptor.",
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
                continue;
            }

            bindingReceipt.SourceMaterialName = FirstNonEmpty(descriptor.SourceMaterialName, sourceMaterialName);
            bindingReceipt.SurfaceMode = descriptor.SurfaceMode;
            bindingReceipt.AlphaCutoff = descriptor.AlphaCutoff;
            string requestedSurfaceModeValue = color?.Item?.Target?.MaterialSurfaceMode ?? string.Empty;
            string requestedSurfaceMode = CharacterMaterialSurfaceModeIds.Normalize(requestedSurfaceModeValue);
            if (!string.IsNullOrWhiteSpace(requestedSurfaceModeValue) && string.IsNullOrWhiteSpace(requestedSurfaceMode))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_SURFACE_MODE_UNSUPPORTED",
                    "Error",
                    "Material slot '" + group.Key + "' requests unsupported surface mode '" + requestedSurfaceModeValue + "'.",
                    "Use Opaque or AlphaClip on the slot's Color source target.",
                    itemId: color?.Item?.Id,
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
            }
            if (string.IsNullOrWhiteSpace(requestedSurfaceMode))
                requestedSurfaceMode = CharacterMaterialSurfaceModeIds.Opaque;
            float requestedAlphaCutoff = color?.Item?.Target?.AlphaCutoff ?? 0.5f;
            if (string.Equals(requestedSurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                && (float.IsNaN(requestedAlphaCutoff)
                    || float.IsInfinity(requestedAlphaCutoff)
                    || requestedAlphaCutoff < 0f
                    || requestedAlphaCutoff > 1f))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_ALPHA_CUTOFF_INVALID",
                    "Error",
                    "Material slot '" + group.Key + "' requests AlphaClip with cutoff "
                        + requestedAlphaCutoff.ToString(CultureInfo.InvariantCulture) + ".",
                    "Set alphaCutoff to a value from 0 through 1.",
                    itemId: color?.Item?.Id,
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
            }
            if (!descriptor.SurfaceModeSupported)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_SURFACE_MODE_UNSUPPORTED",
                    "Error",
                    "Material descriptor declares unsupported surface mode '" + descriptor.SurfaceMode + "'.",
                    "Use Opaque or AlphaClip.",
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
            }
            else if (string.Equals(descriptor.SurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                     && (float.IsNaN(descriptor.AlphaCutoff)
                         || float.IsInfinity(descriptor.AlphaCutoff)
                         || descriptor.AlphaCutoff < 0f
                         || descriptor.AlphaCutoff > 1f))
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_ALPHA_CUTOFF_INVALID",
                    "Error",
                    "Material descriptor AlphaClip cutoff must be from 0 through 1; observed "
                        + descriptor.AlphaCutoff.ToString(CultureInfo.InvariantCulture) + ".",
                    "Correct alphaCutoff in the package material descriptor.",
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
            }
            else
            {
                if (!string.Equals(requestedSurfaceMode, descriptor.SurfaceMode, StringComparison.Ordinal)
                    || (string.Equals(requestedSurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                        && Math.Abs(requestedAlphaCutoff - descriptor.AlphaCutoff) > 0.0001f))
                {
                    receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                        "MATERIAL_SURFACE_MODE_MISMATCH",
                        "Error",
                        "Material slot '" + group.Key + "' requests " + requestedSurfaceMode
                            + (string.Equals(requestedSurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                                ? " cutoff " + requestedAlphaCutoff.ToString(CultureInfo.InvariantCulture)
                                : string.Empty)
                            + ", but its descriptor resolves to " + descriptor.SurfaceMode
                            + (string.Equals(descriptor.SurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                                ? " cutoff " + descriptor.AlphaCutoff.ToString(CultureInfo.InvariantCulture)
                                : string.Empty) + ".",
                        "Synchronize a tool-owned descriptor or update the authored descriptor to match the source manifest.",
                        itemId: color?.Item?.Id,
                        resourceKey: material.ResourceKey,
                        path: material.RelativePath));
                }
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "MATERIAL_SURFACE_MODE_RESOLVED",
                    "Info",
                    "Material '" + bindingReceipt.SourceMaterialName + "' resolves to " + descriptor.SurfaceMode
                        + (string.Equals(descriptor.SurfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                            ? " with alpha cutoff " + descriptor.AlphaCutoff.ToString(CultureInfo.InvariantCulture)
                            : string.Empty) + ".",
                    string.Empty,
                    resourceKey: material.ResourceKey,
                    path: material.RelativePath));
            }
            bindingReceipt.DeclaredTextureCount = descriptor.TextureResourceKeys.Count;
            for (int textureIndex = 0; textureIndex < descriptor.Textures.Count; textureIndex++)
            {
                MaterialDescriptorTextureSnapshot descriptorTexture = descriptor.Textures[textureIndex];
                if (CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, descriptorTexture.ResourceKey) != null)
                    bindingReceipt.ResolvedTextureCount++;
                else
                    receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                        "MATERIAL_TEXTURE_RESOURCE_MISSING",
                        "Error",
                        "Material descriptor references an unknown texture resource: " + descriptorTexture.ResourceKey + ".",
                        "Import the texture or correct the descriptor resourceKey.",
                        resourceKey: material.ResourceKey,
                        path: material.RelativePath));
            }

            bindingReceipt.ColorTextureResourceKey = descriptor.Textures.FirstOrDefault(item => item.Semantic == CharacterTextureSemanticIds.Color)?.ResourceKey ?? string.Empty;
            bindingReceipt.NormalTextureResourceKey = descriptor.Textures.FirstOrDefault(item => item.Semantic == CharacterTextureSemanticIds.Normal)?.ResourceKey ?? string.Empty;
            bindingReceipt.MaskTextureResourceKey = descriptor.Textures.FirstOrDefault(item => item.Semantic == CharacterTextureSemanticIds.Mask)?.ResourceKey ?? string.Empty;
            foreach (ResolvedImportTexture texture in group.Where(item => item.Entry != null && !string.IsNullOrWhiteSpace(item.Semantic)))
            {
                bool descriptorBound = descriptor.Textures.Any(item =>
                    item.Semantic == texture.Semantic
                    && string.Equals(item.ResourceKey, texture.Entry.ResourceKey, StringComparison.Ordinal));
                if (!descriptorBound)
                {
                    receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                        "TEXTURE_SEMANTIC_UNBOUND",
                        "Error",
                        "Texture " + texture.Entry.ResourceKey + " is classified as " + texture.Semantic
                            + " for material slot '" + group.Key + "' but is not bound by its descriptor.",
                        "Regenerate or edit the package material descriptor.",
                        itemId: texture.Item?.Id,
                        resourceKey: texture.Entry.ResourceKey,
                        path: material.RelativePath));
                }
            }

            for (int dependencyIndex = 0; dependencyIndex < descriptor.TextureResourceKeys.Count; dependencyIndex++)
            {
                string textureKey = descriptor.TextureResourceKeys[dependencyIndex];
                if (!HasResourceDependency(material, textureKey, "usesTexture"))
                {
                    receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                        "DEPENDENCY_GRAPH_INCOMPLETE",
                        "Error",
                        "Material " + material.ResourceKey + " does not declare its texture dependency " + textureKey + ".",
                        "Synchronize raw materials to rebuild the package-local dependency graph.",
                        resourceKey: material.ResourceKey,
                        path: material.RelativePath));
                }
            }

            bindingReceipt.ModelDependencyDeclared = bodyModel != null && HasResourceDependency(bodyModel, material.ResourceKey, "usesMaterial");
            if (!bindingReceipt.ModelDependencyDeclared)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "DEPENDENCY_GRAPH_INCOMPLETE",
                    "Error",
                    "Body model does not declare material dependency " + material.ResourceKey + ".",
                    "Synchronize raw materials to rebuild model-to-material dependencies.",
                    resourceKey: bodyModel?.ResourceKey));
            }

            bindingReceipt.SourceMaterialBindingDeclared = rendererBindings.TryGetValue(material.ResourceKey ?? string.Empty, out RendererMaterialBindingSnapshot rendererBinding)
                && string.Equals(rendererBinding.SourceMaterialName, bindingReceipt.SourceMaterialName, StringComparison.OrdinalIgnoreCase);
            if (!bindingReceipt.SourceMaterialBindingDeclared)
            {
                receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                    "RENDERER_BINDING_INCOMPLETE",
                    "Error",
                    "Body model has no source-material binding for '" + bindingReceipt.SourceMaterialName + "' -> " + material.ResourceKey + ".",
                    "Synchronize raw materials to generate source-material-name bindings.",
                    resourceKey: bodyModel?.ResourceKey));
            }
            else
            {
                receipt.RendererBindingCount++;
            }

            if (!receipt.Diagnostics.Any(issue =>
                    issue.Severity == "Error"
                    && (string.Equals(issue.ResourceKey, material.ResourceKey, StringComparison.Ordinal)
                        || string.Equals(issue.Path, material.RelativePath, StringComparison.Ordinal))))
                receipt.BoundMaterialCount++;
        }

        if (receipt.ExpectedMaterialCount > 0)
        {
            receipt.Diagnostics.Add(ImportMaterialsDiagnostic(
                "UNITY_RENDERER_SLOT_VERIFICATION_DEFERRED",
                "Info",
                "Raw-material import verified source material names. Exact Renderer node/slot coverage is verified later by the Unity rig-aware import receipt.",
                "Run Unity Import and require every model material slot to be assigned."));
        }

        FinalizeImportMaterialsIntegrityReceipt(receipt);
        return receipt;
    }

    private static void FinalizeImportMaterialsIntegrityReceipt(CharacterImportMaterialsIntegrityReceipt receipt)
    {
        if (receipt == null)
            return;
        receipt.BlockingIssueCount = receipt.Diagnostics.Count(issue => string.Equals(issue?.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        receipt.WarningCount = receipt.Diagnostics.Count(issue => string.Equals(issue?.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        receipt.Status = receipt.BlockingIssueCount > 0
            ? "Blocked"
            : receipt.ExpectedMaterialCount == 0 && receipt.TextureCount == 0
                ? "NotApplicable"
                : receipt.WarningCount > 0 ? "ReadyWithWarnings" : "Ready";
    }

    private static void MergeImportMaterialsDiagnostics(
        CharacterImportMaterialsIntegrityReceipt receipt,
        IEnumerable<CharacterImportMaterialsIntegrityDiagnostic> diagnostics)
    {
        if (receipt == null || diagnostics == null)
            return;
        foreach (CharacterImportMaterialsIntegrityDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic == null)
                continue;
            bool duplicate = receipt.Diagnostics.Any(existing =>
                existing != null
                && string.Equals(existing.Code, diagnostic.Code, StringComparison.Ordinal)
                && string.Equals(existing.ResourceKey, diagnostic.ResourceKey, StringComparison.Ordinal)
                && string.Equals(existing.Path, diagnostic.Path, StringComparison.Ordinal)
                && string.Equals(existing.Message, diagnostic.Message, StringComparison.Ordinal));
            if (!duplicate)
                receipt.Diagnostics.Add(diagnostic);
        }
        FinalizeImportMaterialsIntegrityReceipt(receipt);
    }

    private static CharacterImportMaterialsIntegrityDiagnostic ImportMaterialsDiagnostic(
        string code,
        string severity,
        string message,
        string suggestedAction,
        string itemId = "",
        string resourceKey = "",
        string path = "")
    {
        return new CharacterImportMaterialsIntegrityDiagnostic
        {
            Code = code ?? string.Empty,
            Severity = severity ?? "Info",
            ItemId = itemId ?? string.Empty,
            ResourceKey = resourceKey ?? string.Empty,
            Path = path ?? string.Empty,
            Message = message ?? string.Empty,
            SuggestedAction = suggestedAction ?? string.Empty
        };
    }

    private static CharacterPackageResourceEntry ResolveImportMaterialsBodyModel(
        CharacterResourcePackage package,
        CharacterImportMaterialsDocument document)
    {
        CharacterImportMaterialItem bodyItem = document?.Items?.FirstOrDefault(IsImportMaterialsBodyItem);
        string resourceKey = FirstNonEmpty(bodyItem?.ResourceKey, bodyItem?.Target?.ResourceKey);
        CharacterPackageResourceEntry body = CharacterPackageResourcePipeline.FindByKey(package?.ResourceCatalog, resourceKey);
        if (body != null)
            return body;
        return package?.ResourceCatalog?.Entries?.FirstOrDefault(entry =>
            entry != null
            && string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsImportMaterialsBodyItem(CharacterImportMaterialItem item)
    {
        return item != null && (string.Equals(item.PresetId, "characterModel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(FirstNonEmpty(item.Target?.BindTarget, item.BindTarget), "body", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ResolvedImportTexture> ResolveImportMaterialTextures(
        CharacterResourcePackage package,
        CharacterImportMaterialsDocument document)
    {
        var result = new List<ResolvedImportTexture>();
        if (document?.Items == null)
            return result;
        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i];
            if (string.Equals(FirstNonEmpty(item?.Target?.BindTarget, item?.BindTarget), "faceDetail", StringComparison.OrdinalIgnoreCase))
                continue;
            string resourceKey = FirstNonEmpty(item?.ResourceKey, item?.Target?.ResourceKey, ResolveImportMaterialSelectionKey(item));
            CharacterPackageResourceEntry entry = CharacterPackageResourcePipeline.FindByKey(package?.ResourceCatalog, resourceKey);
            bool textureItem = string.Equals(item?.PresetId, "texture", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item?.Target?.ResourceKind, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry?.TypeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase);
            if (!textureItem)
                continue;

            string identity = string.Join(" ", new[]
            {
                item?.Id,
                item?.Label,
                item?.SourcePath,
                item?.Source?.Path,
                resourceKey,
                entry?.RelativePath
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            string semantic = CharacterTextureSemanticIds.Normalize(FirstNonEmpty(
                item?.Target?.TextureSemantic,
                item?.TextureSemantic,
                entry?.ImportHints?.Metadata != null && entry.ImportHints.Metadata.TryGetValue("textureSemantic", out string metadataSemantic) ? metadataSemantic : string.Empty));
            if (string.IsNullOrWhiteSpace(semantic))
                semantic = InferImportMaterialTextureSemantic(identity);
            string slot = FirstNonEmpty(item?.Target?.Slot, item?.Slot);
            if (string.IsNullOrWhiteSpace(slot))
                slot = InferImportMaterialTextureSlot(identity);
            result.Add(new ResolvedImportTexture
            {
                Item = item,
                Entry = entry,
                ResourceKey = resourceKey,
                Slot = string.IsNullOrWhiteSpace(slot) ? string.Empty : CharacterPackageResourceKeyGenerator.NormalizeSegment(slot),
                Semantic = semantic
            });
        }
        return result;
    }

    private static string InferImportMaterialTextureSemantic(string identity)
    {
        if (Regex.IsMatch(identity ?? string.Empty, @"(?:^|[._\- /])(?:normal|norm|bump)(?:$|[._\- /])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Normal;
        if (Regex.IsMatch(identity ?? string.Empty, @"(?:^|[._\- /])(?:mask|maskmap|orm|metallic|roughness)(?:$|[._\- /])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Mask;
        if (Regex.IsMatch(identity ?? string.Empty, @"(?:^|[._\- /])(?:albedo|base.?map|base.?color|diffuse|color)(?:$|[._\- /])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Color;
        return string.Empty;
    }

    private static string InferImportMaterialTextureSlot(string identity)
    {
        if (Regex.IsMatch(identity ?? string.Empty, @"(?:^|[._\- /])eye(?:$|[._\- /])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return "eye";
        if (Regex.IsMatch(identity ?? string.Empty, @"(?:^|[._\- /])body(?:$|[._\- /])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return "body";
        return string.Empty;
    }

    private static CharacterPackageResourceEntry FindImportMaterialDescriptorEntry(
        CharacterPackageResourceCatalog catalog,
        string slot,
        string sourceMaterialName)
    {
        if (catalog?.Entries == null)
            return null;
        string normalizedSlot = CharacterPackageResourceKeyGenerator.NormalizeSegment(slot);
        return catalog.Entries.FirstOrDefault(entry =>
            entry != null
            && string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId?.Replace("material.", string.Empty) ?? string.Empty), normalizedSlot, StringComparison.Ordinal)
                || string.Equals(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(entry.RelativePath ?? string.Empty)), sourceMaterialName, StringComparison.OrdinalIgnoreCase)
                || (entry.ImportHints?.Metadata != null
                    && entry.ImportHints.Metadata.TryGetValue("sourceMaterialName", out string configuredName)
                    && string.Equals(configuredName, sourceMaterialName, StringComparison.OrdinalIgnoreCase))));
    }

    private static string BuildImportMaterialSourceName(string slot)
    {
        string normalized = CharacterPackageResourceKeyGenerator.NormalizeSegment(slot).Replace('-', '_').Replace('.', '_');
        if (string.IsNullOrWhiteSpace(normalized))
            return "Material";
        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
    }

    private static void WriteGeneratedImportMaterialDescriptor(
        string descriptorPath,
        string sourceMaterialName,
        List<ResolvedImportTexture> textures,
        string surfaceMode,
        float alphaCutoff,
        JsonSerializerOptions jsonOptions)
    {
        var descriptor = new GeneratedImportMaterialDescriptor
        {
            SourceAssetPath = sourceMaterialName + ".mat",
            SurfaceMode = surfaceMode,
            AlphaCutoff = alphaCutoff,
            RenderQueue = string.Equals(surfaceMode, CharacterMaterialSurfaceModeIds.AlphaClip, StringComparison.Ordinal)
                ? 2450
                : 2000
        };
        AddGeneratedImportMaterialTexture(descriptor, textures, CharacterTextureSemanticIds.Color, "_BaseMap");
        AddGeneratedImportMaterialTexture(descriptor, textures, CharacterTextureSemanticIds.Normal, "_WgNormalTex");
        AddGeneratedImportMaterialTexture(descriptor, textures, CharacterTextureSemanticIds.Mask, "_WgMaskTex");
        WriteJsonFileAtomic(descriptorPath, descriptor, jsonOptions);
    }

    private static void AddGeneratedImportMaterialTexture(
        GeneratedImportMaterialDescriptor descriptor,
        List<ResolvedImportTexture> textures,
        string semantic,
        string property)
    {
        ResolvedImportTexture texture = textures.FirstOrDefault(item => item.Semantic == semantic && item.Entry != null);
        if (texture == null)
            return;
        descriptor.Textures.Add(new GeneratedImportMaterialTexture
        {
            Property = property,
            ResourceKey = texture.Entry.ResourceKey
        });
    }

    private static MaterialDescriptorSnapshot ReadImportMaterialDescriptor(string descriptorPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(descriptorPath) || !File.Exists(descriptorPath))
            throw new IOException("Descriptor file is missing: " + relativePath + ".");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(descriptorPath));
        JsonElement root = document.RootElement;
        var snapshot = new MaterialDescriptorSnapshot
        {
            SourceMaterialName = Path.GetFileNameWithoutExtension(FirstNonEmpty(
                GetJsonString(root, "sourceAssetPath"),
                Path.GetFileNameWithoutExtension(relativePath)))
        };
        string declaredSurfaceMode = GetJsonString(root, "surfaceMode");
        string normalizedSurfaceMode = CharacterMaterialSurfaceModeIds.Normalize(declaredSurfaceMode);
        snapshot.SurfaceModeSupported = string.IsNullOrWhiteSpace(declaredSurfaceMode)
            || !string.IsNullOrWhiteSpace(normalizedSurfaceMode);
        snapshot.SurfaceMode = string.IsNullOrWhiteSpace(declaredSurfaceMode)
            ? CharacterMaterialSurfaceModeIds.Opaque
            : !string.IsNullOrWhiteSpace(normalizedSurfaceMode)
                ? normalizedSurfaceMode
                : declaredSurfaceMode.Trim();
        snapshot.AlphaCutoff = ReadImportMaterialDescriptorFloat(root, "alphaCutoff", 0.5f);
        if (root.TryGetProperty("textures", out JsonElement textures) && textures.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement texture in textures.EnumerateArray())
            {
                string resourceKey = GetJsonString(texture, "resourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey))
                    continue;
                string property = GetJsonString(texture, "property");
                string semantic = GetImportMaterialDescriptorTextureSemantic(property);
                snapshot.Textures.Add(new MaterialDescriptorTextureSnapshot
                {
                    Property = property,
                    ResourceKey = resourceKey,
                    Semantic = semantic
                });
                if (!snapshot.TextureResourceKeys.Contains(resourceKey, StringComparer.Ordinal))
                    snapshot.TextureResourceKeys.Add(resourceKey);
            }
        }
        return snapshot;
    }

    private static float ReadImportMaterialDescriptorFloat(JsonElement root, string propertyName, float fallback)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Number)
            return fallback;
        return value.TryGetSingle(out float result) ? result : fallback;
    }

    private static string GetImportMaterialDescriptorTextureSemantic(string property)
    {
        if (Regex.IsMatch(property ?? string.Empty, @"^(?:_MainTex|_BaseMap|_BaseColorMap)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Color;
        if (Regex.IsMatch(property ?? string.Empty, @"^(?:_WgNormalTex|_NormalMap|_BumpMap)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Normal;
        if (Regex.IsMatch(property ?? string.Empty, @"^(?:_WgMaskTex|_MaskMap|_MetallicGlossMap)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return CharacterTextureSemanticIds.Mask;
        return string.Empty;
    }

    private static void ReplaceResourceDependencies(
        CharacterPackageResourceEntry entry,
        string relation,
        IEnumerable<string> resourceKeys)
    {
        entry.Dependencies ??= new List<CharacterPackageResourceDependency>();
        entry.Dependencies.RemoveAll(dependency => dependency != null && string.Equals(dependency.Relation, relation, StringComparison.OrdinalIgnoreCase));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string resourceKey in resourceKeys ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(resourceKey) || !seen.Add(resourceKey))
                continue;
            entry.Dependencies.Add(new CharacterPackageResourceDependency
            {
                ResourceKey = resourceKey,
                Required = true,
                Relation = relation,
                AffectsDependencyHash = true
            });
        }
    }

    private static bool HasResourceDependency(CharacterPackageResourceEntry entry, string resourceKey, string relation)
    {
        return entry?.Dependencies != null && entry.Dependencies.Any(dependency =>
            dependency != null
            && string.Equals(dependency.ResourceKey, resourceKey, StringComparison.Ordinal)
            && string.Equals(dependency.Relation, relation, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, RendererMaterialBindingSnapshot> ReadImportMaterialRendererBindings(
        CharacterPackageResourceEntry bodyModel,
        JsonSerializerOptions jsonOptions)
    {
        var result = new Dictionary<string, RendererMaterialBindingSnapshot>(StringComparer.Ordinal);
        if (bodyModel?.ImportHints?.Metadata == null
            || !bodyModel.ImportHints.Metadata.TryGetValue("rendererMaterialBindingsJson", out string json)
            || string.IsNullOrWhiteSpace(json))
            return result;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("bindings", out JsonElement bindings) || bindings.ValueKind != JsonValueKind.Array)
                return result;
            foreach (JsonElement binding in bindings.EnumerateArray())
            {
                string materialResourceKey = GetJsonString(binding, "materialResourceKey");
                if (string.IsNullOrWhiteSpace(materialResourceKey))
                    continue;
                result[materialResourceKey] = new RendererMaterialBindingSnapshot
                {
                    MaterialResourceKey = materialResourceKey,
                    SourceMaterialName = GetJsonString(binding, "sourceMaterialName")
                };
            }
        }
        catch (JsonException)
        {
            return new Dictionary<string, RendererMaterialBindingSnapshot>(StringComparer.Ordinal);
        }
        return result;
    }

    private static CharacterImportMaterialsSummary BuildImportMaterialsSummary(
        List<CharacterImportMaterialItemStatus> items,
        CharacterImportMaterialsIntegrityReceipt integrityReceipt)
    {
        var summary = new CharacterImportMaterialsSummary();
        if (items == null)
            return summary;

        summary.total = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            CharacterImportMaterialItemStatus item = items[i];
            if (item == null)
                continue;
            if (item.status == "Clean")
                summary.clean++;
            else if (item.status == "Changed")
                summary.changed++;
            else if (item.status == "Missing" || item.status == "OptionalMissing")
                summary.missing++;
            else if (item.status == "NotImported")
                summary.notImported++;
            else if (item.status == "UnityPending")
                summary.unityPending++;
            else
                summary.blocked++;
        }

        summary.dirty = summary.changed + summary.notImported;
        summary.integrityStatus = integrityReceipt?.Status ?? "NotApplicable";
        summary.integrityBlocking = integrityReceipt?.BlockingIssueCount ?? 0;
        summary.integrityWarnings = integrityReceipt?.WarningCount ?? 0;
        summary.blocked += summary.integrityBlocking;
        return summary;
    }

    private static CharacterImportMaterialsLoad ReadCharacterImportMaterialsDocument(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions)
    {
        string sourceManifestRelative = GetCharacterSourceManifestRelative(package, packageRelative);
        string sourceManifestPath = ResolveSafePath(rootPath, sourceManifestRelative);
        string stateRelative = JoinImportMaterialPath(packageRelative, ImportMaterialsStateRelativePath);
        string legacyRelative = JoinImportMaterialPath(packageRelative, ImportMaterialsDocumentRelativePath);
        string statePath = ResolveSafePath(rootPath, stateRelative);
        string legacyPath = ResolveSafePath(rootPath, legacyRelative);
        string sourceRootRelative = GetCharacterSourceRootRelative(package, packageRelative);
        bool sourceManifestExists = File.Exists(sourceManifestPath);
        bool legacyDocumentExists = File.Exists(legacyPath);
        var load = new CharacterImportMaterialsLoad
        {
            SourceManifestRelative = sourceManifestRelative,
            SourceManifestExists = sourceManifestExists,
            SourceManifestDirectoryRelative = NormalizeImportMaterialRelativePath(Path.GetDirectoryName(sourceManifestRelative)?.Replace('\\', '/') ?? sourceRootRelative),
            SourceManifestDirectoryPath = Path.GetDirectoryName(sourceManifestPath) ?? rootPath,
            StateRelative = stateRelative,
            LegacyDocumentRelative = legacyRelative,
            LegacyDocumentExists = legacyDocumentExists,
            DocumentRelative = sourceManifestRelative,
            DocumentKind = "sourceManifest"
        };

        CharacterImportMaterialsDocument document;
        if (sourceManifestExists)
        {
            CharacterSourceManifestDocument sourceManifest = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(File.ReadAllText(sourceManifestPath), jsonOptions)
                ?? new CharacterSourceManifestDocument();
            document = ConvertSourceManifestToImportMaterials(sourceManifest, package, packageRelative, load.SourceManifestDirectoryRelative);
            load.PersistedIntegrityReceipt = MergeImportMaterialsState(rootPath, statePath, legacyPath, document, jsonOptions)?.IntegrityReceipt;
        }
        else if (legacyDocumentExists)
        {
            document = JsonSerializer.Deserialize<CharacterImportMaterialsDocument>(File.ReadAllText(legacyPath), jsonOptions)
                ?? new CharacterImportMaterialsDocument();
            load.PersistedIntegrityReceipt = MergeImportMaterialsState(rootPath, statePath, legacyPath, document, jsonOptions)?.IntegrityReceipt;
            load.DocumentRelative = legacyRelative;
            load.DocumentKind = "legacyPackageDocument";
        }
        else
        {
            document = new CharacterImportMaterialsDocument
            {
                SourceRoot = sourceRootRelative
            };
        }

        NormalizeCharacterImportMaterialsDocument(document);
        load.Document = document;
        return load;
    }

    private static void WriteCharacterImportMaterialsDocument(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        CharacterImportMaterialsDocument document,
        JsonSerializerOptions jsonOptions)
    {
        string sourceManifestRelative = GetCharacterSourceManifestRelative(package, packageRelative);
        string sourceManifestPath = ResolveSafePath(rootPath, sourceManifestRelative);
        string sourceManifestDirectoryRelative = NormalizeImportMaterialRelativePath(Path.GetDirectoryName(sourceManifestRelative)?.Replace('\\', '/') ?? GetCharacterSourceRootRelative(package, packageRelative));
        CharacterSourceManifestDocument manifest = ConvertImportMaterialsToSourceManifest(document, package, sourceManifestDirectoryRelative);
        WriteJsonFileAtomic(sourceManifestPath, manifest, jsonOptions);
    }

    private static void WriteCharacterImportMaterialsStateDocument(
        string rootPath,
        string packageRelative,
        string sourceManifestRelative,
        CharacterImportMaterialsDocument document,
        CharacterImportMaterialsIntegrityReceipt integrityReceipt,
        JsonSerializerOptions jsonOptions)
    {
        string statePath = ResolveSafePath(rootPath, JoinImportMaterialPath(packageRelative, ImportMaterialsStateRelativePath));
        var state = new CharacterImportMaterialsStateDocument
        {
            SourceManifestRelative = NormalizeImportMaterialRelativePath(sourceManifestRelative),
            IntegrityReceipt = integrityReceipt ?? new CharacterImportMaterialsIntegrityReceipt()
        };
        if (document?.Items != null)
        {
            for (int i = 0; i < document.Items.Count; i++)
            {
                CharacterImportMaterialItem item = document.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    continue;
                state.Items.Add(new CharacterImportMaterialItemState
                {
                    Id = item.Id,
                    ResourceKey = item.ResourceKey ?? string.Empty,
                    LastImportedHash = CharacterPackageHashUtility.NormalizeSha256(item.LastImportedHash),
                    LastImportedUtc = item.LastImportedUtc ?? string.Empty
                });
            }
        }

        WriteJsonFileAtomic(statePath, state, jsonOptions);
    }

    private static CharacterImportMaterialsDocument ConvertSourceManifestToImportMaterials(
        CharacterSourceManifestDocument manifest,
        CharacterResourcePackage package,
        string packageRelative,
        string sourceManifestDirectoryRelative)
    {
        manifest ??= new CharacterSourceManifestDocument();
        string manifestSourceRoot = NormalizeManifestSourceRoot(manifest.SourceRoot);
        var document = new CharacterImportMaterialsDocument
        {
            SchemaVersion = string.IsNullOrWhiteSpace(manifest.SchemaVersion) ? "1.0" : manifest.SchemaVersion,
            SourceRoot = JoinImportMaterialPath(sourceManifestDirectoryRelative, manifestSourceRoot)
        };
        if (string.IsNullOrWhiteSpace(document.SourceRoot))
            document.SourceRoot = GetCharacterSourceRootRelative(package, packageRelative);

        manifest.Items ??= new List<CharacterSourceManifestItem>();
        for (int i = 0; i < manifest.Items.Count; i++)
        {
            CharacterSourceManifestItem sourceItem = manifest.Items[i] ?? new CharacterSourceManifestItem();
            CharacterSourceReference source = NormalizeCharacterSourceReference(sourceItem.Source);
            CharacterSourceTarget target = NormalizeCharacterSourceTarget(sourceItem.Target);
            string sourcePath = string.Equals(source.Kind, CharacterSourceFileKind, StringComparison.OrdinalIgnoreCase)
                ? source.Path
                : string.Empty;
            document.Items.Add(new CharacterImportMaterialItem
            {
                Id = sourceItem.Id ?? string.Empty,
                Label = sourceItem.Label ?? string.Empty,
                SourcePath = sourcePath,
                Source = source,
                Target = target,
                PresetId = sourceItem.PresetId ?? string.Empty,
                Slot = FirstNonEmpty(sourceItem.Slot, target.Slot),
                ResourceKey = target.ResourceKey ?? string.Empty,
                BindTarget = FirstNonEmpty(sourceItem.BindTarget, target.BindTarget),
                TextureSemantic = target.TextureSemantic ?? string.Empty,
                Required = sourceItem.Required
            });
        }

        return document;
    }

    private static CharacterSourceManifestDocument ConvertImportMaterialsToSourceManifest(
        CharacterImportMaterialsDocument document,
        CharacterResourcePackage package,
        string sourceManifestDirectoryRelative)
    {
        document ??= new CharacterImportMaterialsDocument();
        var manifest = new CharacterSourceManifestDocument
        {
            SchemaVersion = string.IsNullOrWhiteSpace(document.SchemaVersion) ? "1.0" : document.SchemaVersion,
            CharacterId = package?.Manifest?.PackageId ?? string.Empty,
            PackageId = package?.Manifest?.PackageId ?? string.Empty,
            SourceRoot = "."
        };
        document.Items ??= new List<CharacterImportMaterialItem>();
        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i] ?? new CharacterImportMaterialItem();
            CharacterSourceReference source = NormalizeCharacterSourceReference(item.Source);
            if (!string.Equals(source.Kind, CharacterSourceResourceRefKind, StringComparison.OrdinalIgnoreCase))
                source = BuildFileSourceReferenceForManifest(item, document.SourceRoot, sourceManifestDirectoryRelative);

            string targetBind = FirstNonEmpty(item.Target?.BindTarget, item.BindTarget);
            string targetSlot = FirstNonEmpty(item.Target?.Slot, item.Slot);
            manifest.Items.Add(new CharacterSourceManifestItem
            {
                Id = item.Id ?? string.Empty,
                Label = item.Label ?? string.Empty,
                PresetId = item.PresetId ?? string.Empty,
                Slot = targetSlot,
                BindTarget = targetBind,
                Required = item.Required,
                Source = source,
                Target = new CharacterSourceTarget
                {
                    ResourceKey = FirstNonEmpty(item.Target?.ResourceKey, item.ResourceKey),
                    ResourceKind = item.Target?.ResourceKind ?? string.Empty,
                    ResourceUsage = item.Target?.ResourceUsage ?? string.Empty,
                    BindTarget = targetBind,
                    Slot = targetSlot,
                    TextureSemantic = CharacterTextureSemanticIds.Normalize(FirstNonEmpty(
                        item.Target?.TextureSemantic,
                        item.TextureSemantic)),
                    MaterialSurfaceMode = item.Target?.MaterialSurfaceMode ?? CharacterMaterialSurfaceModeIds.Unspecified,
                    AlphaCutoff = item.Target?.AlphaCutoff ?? 0.5f
                }
            });
        }

        return manifest;
    }

    private static CharacterSourceReference BuildFileSourceReferenceForManifest(
        CharacterImportMaterialItem item,
        string documentSourceRoot,
        string sourceManifestDirectoryRelative)
    {
        string path = NormalizeImportMaterialRelativePath(FirstNonEmpty(item.Source?.Path, item.SourcePath));
        string scope = FirstNonEmpty(item.Source?.Scope, CharacterSourcePackageScope);
        if (IsPublicImportMaterialSourcePath(path))
        {
            return new CharacterSourceReference
            {
                Kind = CharacterSourceFileKind,
                Scope = CharacterSourcePublicScope,
                Path = path
            };
        }

        string sourceRoot = NormalizeImportMaterialRelativePath(documentSourceRoot);
        string manifestRoot = NormalizeImportMaterialRelativePath(sourceManifestDirectoryRelative);
        if (!string.IsNullOrWhiteSpace(sourceRoot) && path.StartsWith(sourceRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(sourceRoot.TrimEnd('/').Length + 1);
        else if (!string.IsNullOrWhiteSpace(manifestRoot) && path.StartsWith(manifestRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(manifestRoot.TrimEnd('/').Length + 1);

        if (!string.Equals(scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase))
            scope = CharacterSourcePackageScope;
        return new CharacterSourceReference
        {
            Kind = CharacterSourceFileKind,
            Scope = scope,
            Path = path
        };
    }

    private static CharacterImportMaterialsStateDocument MergeImportMaterialsState(
        string rootPath,
        string statePath,
        string legacyPath,
        CharacterImportMaterialsDocument document,
        JsonSerializerOptions jsonOptions)
    {
        CharacterImportMaterialsStateDocument state = File.Exists(statePath)
            ? JsonSerializer.Deserialize<CharacterImportMaterialsStateDocument>(File.ReadAllText(statePath), jsonOptions)
            : null;
        if (state == null && File.Exists(legacyPath))
        {
            CharacterImportMaterialsDocument legacy = JsonSerializer.Deserialize<CharacterImportMaterialsDocument>(File.ReadAllText(legacyPath), jsonOptions);
            state = CreateImportMaterialsStateFromLegacy(legacy);
        }

        if (state?.Items == null || document?.Items == null)
            return state;

        var byId = new Dictionary<string, CharacterImportMaterialItemState>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < state.Items.Count; i++)
        {
            CharacterImportMaterialItemState itemState = state.Items[i];
            if (itemState != null && !string.IsNullOrWhiteSpace(itemState.Id))
                byId[itemState.Id] = itemState;
        }

        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || !byId.TryGetValue(item.Id, out CharacterImportMaterialItemState itemState))
                continue;
            if (string.IsNullOrWhiteSpace(item.ResourceKey))
                item.ResourceKey = itemState.ResourceKey ?? string.Empty;
            item.LastImportedHash = CharacterPackageHashUtility.NormalizeSha256(itemState.LastImportedHash);
            item.LastImportedUtc = itemState.LastImportedUtc ?? string.Empty;
        }

        return state;
    }

    private static CharacterImportMaterialsStateDocument CreateImportMaterialsStateFromLegacy(CharacterImportMaterialsDocument legacy)
    {
        var state = new CharacterImportMaterialsStateDocument();
        if (legacy?.Items == null)
            return state;
        for (int i = 0; i < legacy.Items.Count; i++)
        {
            CharacterImportMaterialItem item = legacy.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;
            state.Items.Add(new CharacterImportMaterialItemState
            {
                Id = item.Id,
                ResourceKey = item.ResourceKey ?? string.Empty,
                LastImportedHash = CharacterPackageHashUtility.NormalizeSha256(item.LastImportedHash),
                LastImportedUtc = item.LastImportedUtc ?? string.Empty
            });
        }

        return state;
    }

    private static void NormalizeCharacterImportMaterialsDocument(CharacterImportMaterialsDocument document)
    {
        if (document == null)
            return;
        if (string.IsNullOrWhiteSpace(document.SchemaVersion))
            document.SchemaVersion = "1.0";
        document.SourceRoot = NormalizeImportMaterialRelativePath(document.SourceRoot);
        document.Items ??= new List<CharacterImportMaterialItem>();

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i] ?? new CharacterImportMaterialItem();
            document.Items[i] = item;
            item.Source = NormalizeCharacterSourceReference(item.Source);
            item.Target = NormalizeCharacterSourceTarget(item.Target);
            if (string.IsNullOrWhiteSpace(item.SourcePath)
                && string.Equals(item.Source.Kind, CharacterSourceFileKind, StringComparison.OrdinalIgnoreCase))
                item.SourcePath = item.Source.Path;
            if (string.IsNullOrWhiteSpace(item.ResourceKey))
                item.ResourceKey = item.Target.ResourceKey;
            if (string.IsNullOrWhiteSpace(item.BindTarget))
                item.BindTarget = item.Target.BindTarget;
            if (string.IsNullOrWhiteSpace(item.Slot))
                item.Slot = item.Target.Slot;
            if (string.IsNullOrWhiteSpace(item.TextureSemantic))
                item.TextureSemantic = item.Target.TextureSemantic;
            item.SourcePath = NormalizeImportMaterialRelativePath(item.SourcePath);
            item.PresetId = NormalizeImportMaterialPresetId(item.PresetId, item.SourcePath, item.BindTarget);
            item.BindTarget = string.IsNullOrWhiteSpace(item.BindTarget) ? InferBindTargetFromPreset(item.PresetId) : item.BindTarget.Trim();
            item.Slot = string.IsNullOrWhiteSpace(item.Slot) ? string.Empty : item.Slot.Trim();
            item.ResourceKey = string.IsNullOrWhiteSpace(item.ResourceKey) ? string.Empty : item.ResourceKey.Trim();
            string normalizedSemantic = CharacterTextureSemanticIds.Normalize(item.TextureSemantic);
            item.TextureSemantic = !string.IsNullOrWhiteSpace(normalizedSemantic)
                ? normalizedSemantic
                : (item.TextureSemantic ?? string.Empty).Trim();
            item.Target.TextureSemantic = item.TextureSemantic;
            item.LastImportedHash = CharacterPackageHashUtility.NormalizeSha256(item.LastImportedHash);
            item.LastImportedUtc = string.IsNullOrWhiteSpace(item.LastImportedUtc) ? string.Empty : item.LastImportedUtc.Trim();
            if (string.IsNullOrWhiteSpace(item.Label))
                item.Label = BuildImportMaterialDefaultLabel(item);
            string id = string.IsNullOrWhiteSpace(item.Id) ? BuildImportMaterialItemId(item, i) : CharacterPackageResourceKeyGenerator.NormalizeSegment(item.Id);
            string baseId = id;
            int suffix = 2;
            while (!ids.Add(id))
                id = baseId + "-" + suffix++.ToString(CultureInfo.InvariantCulture);
            item.Id = id;
        }
    }

    private static void ValidateCharacterImportMaterialsDocument(CharacterImportMaterialsDocument document)
    {
        if (document == null)
            throw new ArgumentException("document is required.");
        if (IsUnsafeImportMaterialRelativePath(document.SourceRoot, allowEmpty: true))
            throw new ArgumentException("sourceRoot must be a project/package relative path.");
        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i];
            if (item == null)
                continue;
            bool resourceRef = IsResourceRefImportMaterialItem(item);
            if (!resourceRef && string.IsNullOrWhiteSpace(item.SourcePath))
                throw new ArgumentException("items[" + i.ToString(CultureInfo.InvariantCulture) + "].sourcePath is required.");
            if (!resourceRef && IsUnsafeImportMaterialRelativePath(item.SourcePath, allowEmpty: false))
                throw new ArgumentException("items[" + i.ToString(CultureInfo.InvariantCulture) + "].sourcePath must be a project/package relative path.");
            if (!resourceRef && string.Equals(item.Source?.Scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase) && !IsPublicImportMaterialSourcePath(item.SourcePath))
                throw new ArgumentException("items[" + i.ToString(CultureInfo.InvariantCulture) + "].source.path uses public scope but is not under " + CharacterSourcePublicRootRelative + ".");
            if (resourceRef && !HasImportMaterialResourceSelection(item))
                throw new ArgumentException("items[" + i.ToString(CultureInfo.InvariantCulture) + "].source.selection is required for resourceRef items.");
            string declaredTextureSemantic = FirstNonEmpty(item.Target?.TextureSemantic, item.TextureSemantic);
            if (!string.IsNullOrWhiteSpace(declaredTextureSemantic)
                && string.IsNullOrWhiteSpace(CharacterTextureSemanticIds.Normalize(declaredTextureSemantic)))
                throw new ArgumentException("items[" + i.ToString(CultureInfo.InvariantCulture) + "].target.textureSemantic must be Color, Normal, or Mask.");
        }
    }

    private static CharacterSourceReference NormalizeCharacterSourceReference(CharacterSourceReference source)
    {
        if (source == null)
            source = new CharacterSourceReference();
        source.Kind = string.IsNullOrWhiteSpace(source.Kind) ? CharacterSourceFileKind : source.Kind.Trim();
        source.Scope = string.IsNullOrWhiteSpace(source.Scope) ? CharacterSourcePackageScope : source.Scope.Trim();
        source.Path = NormalizeImportMaterialRelativePath(source.Path);
        return source;
    }

    private static CharacterSourceTarget NormalizeCharacterSourceTarget(CharacterSourceTarget target)
    {
        if (target == null)
            target = new CharacterSourceTarget();
        target.ResourceKey = string.IsNullOrWhiteSpace(target.ResourceKey) ? string.Empty : target.ResourceKey.Trim();
        target.ResourceKind = string.IsNullOrWhiteSpace(target.ResourceKind) ? string.Empty : target.ResourceKind.Trim();
        target.ResourceUsage = string.IsNullOrWhiteSpace(target.ResourceUsage) ? string.Empty : target.ResourceUsage.Trim();
        target.BindTarget = string.IsNullOrWhiteSpace(target.BindTarget) ? string.Empty : target.BindTarget.Trim();
        target.Slot = string.IsNullOrWhiteSpace(target.Slot) ? string.Empty : target.Slot.Trim();
        string normalizedSemantic = CharacterTextureSemanticIds.Normalize(target.TextureSemantic);
        target.TextureSemantic = !string.IsNullOrWhiteSpace(normalizedSemantic)
            ? normalizedSemantic
            : (target.TextureSemantic ?? string.Empty).Trim();
        string normalizedSurfaceMode = CharacterMaterialSurfaceModeIds.Normalize(target.MaterialSurfaceMode);
        target.MaterialSurfaceMode = !string.IsNullOrWhiteSpace(normalizedSurfaceMode)
            ? normalizedSurfaceMode
            : (target.MaterialSurfaceMode ?? string.Empty).Trim();
        return target;
    }

    private static void PersistImportMaterialMetadata(
        string rootPath,
        string packageRelative,
        CharacterImportMaterialItem item,
        CharacterPackageResourceEntry imported,
        JsonSerializerOptions jsonOptions)
    {
        if (imported == null || string.IsNullOrWhiteSpace(imported.ResourceKey))
            return;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = CharacterPackageResourcePipeline.FindByKey(
            package.ResourceCatalog,
            imported.ResourceKey);
        if (entry == null)
            return;

        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.Metadata ??= new Dictionary<string, string>();
        string semantic = CharacterTextureSemanticIds.Normalize(FirstNonEmpty(
            item?.Target?.TextureSemantic,
            item?.TextureSemantic));
        if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(semantic))
            entry.ImportHints.Metadata["textureSemantic"] = semantic;
        else
            entry.ImportHints.Metadata.Remove("textureSemantic");
        string bindTarget = FirstNonEmpty(item?.Target?.BindTarget, item?.BindTarget);
        if (!string.IsNullOrWhiteSpace(bindTarget))
            entry.ImportHints.Metadata["bindTarget"] = bindTarget;
        else
            entry.ImportHints.Metadata.Remove("bindTarget");
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
    }

    private static string NormalizeManifestSourceRoot(string sourceRoot)
    {
        sourceRoot = NormalizeImportMaterialRelativePath(sourceRoot);
        return sourceRoot == "." ? string.Empty : sourceRoot;
    }

    private static bool IsResourceRefImportMaterialItem(CharacterImportMaterialItem item)
    {
        return string.Equals(item?.Source?.Kind, CharacterSourceResourceRefKind, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasImportMaterialResourceSelection(CharacterImportMaterialItem item)
    {
        if (item == null)
            return false;
        if (!string.IsNullOrWhiteSpace(item.ResourceKey))
            return true;
        AuthoringResourceSelectionRef selection = item.Source?.Selection;
        if (selection == null)
            return false;
        return !string.IsNullOrWhiteSpace(selection.ProviderResourceKey)
            || !string.IsNullOrWhiteSpace(selection.PackageResourceKey)
            || !string.IsNullOrWhiteSpace(selection.RuntimeResourceKey)
            || !string.IsNullOrWhiteSpace(selection.UnityGuid)
            || !string.IsNullOrWhiteSpace(selection.UnityAssetPath);
    }

    private static string ResolveImportMaterialSelectionKey(CharacterImportMaterialItem item)
    {
        AuthoringResourceSelectionRef selection = item?.Source?.Selection;
        if (selection == null)
            return string.Empty;
        return FirstNonEmpty(
            selection.PackageResourceKey,
            selection.RuntimeResourceKey,
            selection.ProviderResourceKey,
            selection.UnityGuid,
            selection.UnityAssetPath);
    }

    private static bool IsPublicImportMaterialSourcePath(string path)
    {
        path = NormalizeImportMaterialRelativePath(path);
        string publicRoot = CharacterSourcePublicRootRelative.TrimEnd('/');
        return string.Equals(path, publicRoot, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(publicRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImportMaterialBrowserImagePath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => true,
            ".jpg" => true,
            ".jpeg" => true,
            ".webp" => true,
            ".gif" => true,
            ".bmp" => true,
            ".svg" => true,
            _ => false
        };
    }

    private static ImportMaterialResolvedSource ResolveImportMaterialSource(
        string rootPath,
        string packagePath,
        CharacterImportMaterialsLoad load,
        string sourceRoot,
        CharacterImportMaterialItem item)
    {
        string sourcePath = item?.SourcePath ?? string.Empty;
        if (IsResourceRefImportMaterialItem(item))
        {
            return new ImportMaterialResolvedSource
            {
                safe = HasImportMaterialResourceSelection(item),
                exists = HasImportMaterialResourceSelection(item),
                displayPath = "resourceRef:" + ResolveImportMaterialSelectionKey(item),
                referenceKind = CharacterSourceResourceRefKind,
                scope = string.Empty,
                message = HasImportMaterialResourceSelection(item) ? string.Empty : "resourceRef selection is required."
            };
        }

        CharacterSourceReference source = NormalizeCharacterSourceReference(item?.Source);
        if (source != null
            && string.Equals(source.Kind, CharacterSourceFileKind, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(source.Path))
        {
            string scopedSourcePath = FirstNonEmpty(source.Path, sourcePath);
            if (string.IsNullOrWhiteSpace(scopedSourcePath))
                return new ImportMaterialResolvedSource { safe = false, message = "sourcePath is required." };
            if (IsUnsafeImportMaterialRelativePath(scopedSourcePath, allowEmpty: false))
                return new ImportMaterialResolvedSource { safe = false, displayPath = scopedSourcePath, message = "Only package/public relative source paths are supported." };

            string scope = string.IsNullOrWhiteSpace(source.Scope) ? CharacterSourcePackageScope : source.Scope.Trim();
            if (string.Equals(scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsPublicImportMaterialSourcePath(scopedSourcePath))
                    return new ImportMaterialResolvedSource { safe = false, displayPath = scopedSourcePath, message = "Public source files must live under " + CharacterSourcePublicRootRelative + "." };
                string publicCandidate = Path.GetFullPath(Path.Combine(rootPath, scopedSourcePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathAllowed(rootPath, publicCandidate))
                    return new ImportMaterialResolvedSource { safe = false, displayPath = scopedSourcePath, message = "Resolved public source path escapes the project root." };
                return new ImportMaterialResolvedSource
                {
                    safe = true,
                    exists = File.Exists(publicCandidate),
                    fullPath = publicCandidate,
                    displayPath = ToAuthoringRelativePath(rootPath, publicCandidate),
                    referenceKind = CharacterSourceFileKind,
                    scope = CharacterSourcePublicScope,
                    message = File.Exists(publicCandidate) ? string.Empty : "Source file was not found."
                };
            }

            string sourceRootPath = load?.SourceManifestDirectoryPath ?? packagePath;
            string scopedSelected = Path.GetFullPath(Path.Combine(sourceRootPath, scopedSourcePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathAtOrUnderRoot(sourceRootPath, scopedSelected))
                return new ImportMaterialResolvedSource { safe = false, displayPath = scopedSourcePath, message = "Resolved package source path escapes the character source root." };
            return new ImportMaterialResolvedSource
            {
                safe = true,
                exists = File.Exists(scopedSelected),
                fullPath = scopedSelected,
                displayPath = ToAuthoringRelativePath(rootPath, scopedSelected),
                referenceKind = CharacterSourceFileKind,
                scope = CharacterSourcePackageScope,
                message = File.Exists(scopedSelected) ? string.Empty : "Source file was not found."
            };
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
            return new ImportMaterialResolvedSource { safe = false, message = "sourcePath is required." };
        if (IsUnsafeImportMaterialRelativePath(sourceRoot, allowEmpty: true) || IsUnsafeImportMaterialRelativePath(sourcePath, allowEmpty: false))
            return new ImportMaterialResolvedSource { safe = false, displayPath = sourcePath ?? string.Empty, message = "Only project/package relative source paths are supported." };

        string combined = JoinImportMaterialPath(sourceRoot, sourcePath);
        string projectCandidate = Path.GetFullPath(Path.Combine(rootPath, combined.Replace('/', Path.DirectorySeparatorChar)));
        string packageCandidate = Path.GetFullPath(Path.Combine(packagePath, combined.Replace('/', Path.DirectorySeparatorChar)));
        string selected = File.Exists(projectCandidate) ? projectCandidate : (File.Exists(packageCandidate) ? packageCandidate : projectCandidate);
        bool allowed = IsPathAllowed(rootPath, selected) || IsPathAtOrUnderRoot(packagePath, selected);
        if (!allowed)
            return new ImportMaterialResolvedSource { safe = false, displayPath = combined, message = "Resolved source path escapes the project/package root." };

        return new ImportMaterialResolvedSource
        {
            safe = true,
            exists = File.Exists(selected),
            fullPath = selected,
            displayPath = ToAuthoringRelativePath(rootPath, selected),
            referenceKind = CharacterSourceFileKind,
            scope = CharacterSourcePackageScope,
            message = File.Exists(selected) ? string.Empty : "Source file was not found."
        };
    }

    private static ImportMaterialPreset ResolveImportMaterialPreset(CharacterImportMaterialItem item)
    {
        string preset = NormalizeImportMaterialPresetId(item.PresetId, item.SourcePath, item.BindTarget);
        string bindTarget = string.IsNullOrWhiteSpace(item.BindTarget) ? InferBindTargetFromPreset(preset) : item.BindTarget.Trim();
        string slot = item.Slot ?? string.Empty;
        switch (preset)
        {
            case "characterModel":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.CharacterModel, "body", "body", string.Empty);
            case "animationClipGroup":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Animation, CharacterPackageResourceUsageIds.AnimationClipGroup, string.Empty, bindTarget, string.IsNullOrWhiteSpace(slot) ? string.Empty : "anim." + CharacterPackageResourceKeyGenerator.NormalizeSegment(slot));
            case "texture":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Texture, CharacterPackageResourceUsageIds.Texture, string.Empty, bindTarget, string.Empty);
            case "faceDetailTexture":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Texture, CharacterPackageResourceUsageIds.Texture, string.Empty, "faceDetail", "texture.face_detail");
            case "material":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Material, CharacterPackageResourceUsageIds.Material, string.Empty, bindTarget, string.Empty);
            case "audioCue":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Audio, CharacterPackageResourceUsageIds.AudioCue, string.Empty, bindTarget, string.Empty);
            case "vfxCue":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Vfx, CharacterPackageResourceUsageIds.VfxCue, string.Empty, bindTarget, string.Empty);
            case "config":
                return new ImportMaterialPreset(CharacterPackageResourceTypeIds.Config, CharacterPackageResourceUsageIds.CharacterConfig, string.Empty, bindTarget, string.Empty);
            default:
                string extension = Path.GetExtension(item.SourcePath ?? string.Empty);
                string kind = NormalizeResourceTypeId(string.Empty, extension);
                return new ImportMaterialPreset(kind, GetDefaultResourceUsage(kind), bindTarget, bindTarget, string.Empty);
        }
    }

    private static string NormalizeImportMaterialPresetId(string presetId, string sourcePath, string bindTarget)
    {
        if (!string.IsNullOrWhiteSpace(presetId))
            return presetId.Trim();
        if (string.Equals(bindTarget, "faceDetail", StringComparison.OrdinalIgnoreCase))
            return "faceDetailTexture";
        if (string.Equals(bindTarget, "body", StringComparison.OrdinalIgnoreCase))
            return "characterModel";
        string extension = Path.GetExtension(sourcePath ?? string.Empty).ToLowerInvariant();
        if (extension == ".glb" || extension == ".gltf" || extension == ".fbx")
            return "modelPreview";
        if (extension == ".anim")
            return "animationClipGroup";
        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga")
            return "texture";
        return "config";
    }

    private static string InferBindTargetFromPreset(string presetId)
    {
        if (string.Equals(presetId, "characterModel", StringComparison.OrdinalIgnoreCase))
            return "body";
        if (string.Equals(presetId, "faceDetailTexture", StringComparison.OrdinalIgnoreCase))
            return "faceDetail";
        return string.Empty;
    }

    private static List<string> BuildImportMaterialSourceRootSuggestions(
        string rootPath,
        string packageRelative,
        string packagePath,
        string sourceRoot,
        string defaultSourceRoot)
    {
        var roots = new List<string>();
        AddImportMaterialRootSuggestion(roots, sourceRoot);
        AddImportMaterialRootSuggestion(roots, defaultSourceRoot);
        if (Directory.Exists(Path.Combine(rootPath, CharacterSourcePublicRootRelative.Replace('/', Path.DirectorySeparatorChar))))
            AddImportMaterialRootSuggestion(roots, CharacterSourcePublicRootRelative);
        string packageResources = JoinImportMaterialPath(packageRelative, "resources");
        if (Directory.Exists(Path.Combine(packagePath, "resources")))
            AddImportMaterialRootSuggestion(roots, packageResources);
        return roots;
    }

    private static void AddImportMaterialRootSuggestion(List<string> roots, string value)
    {
        value = NormalizeImportMaterialRelativePath(value);
        if (!roots.Contains(value, StringComparer.OrdinalIgnoreCase))
            roots.Add(value);
    }

    private static List<ImportMaterialSourceScanRoot> BuildImportMaterialSourceScanRoots(
        string rootPath,
        string packageRelative,
        string packagePath,
        string sourceRoot,
        string defaultSourceRoot)
    {
        var roots = new List<ImportMaterialSourceScanRoot>();
        if (!string.IsNullOrWhiteSpace(sourceRoot))
        {
            string scope = IsPublicImportMaterialSourcePath(sourceRoot) ? CharacterSourcePublicScope : CharacterSourcePackageScope;
            string candidateSourceRoot = string.Equals(scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase) ? string.Empty : sourceRoot;
            AddImportMaterialSourceScanRoot(roots, rootPath, sourceRoot, candidateSourceRoot, sourceRoot, scope);
            if (!string.Equals(scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase))
                AddImportMaterialSourceScanRootFullPath(roots, rootPath, Path.Combine(packagePath, sourceRoot.Replace('/', Path.DirectorySeparatorChar)), sourceRoot, JoinImportMaterialPath(packageRelative, sourceRoot), CharacterSourcePackageScope);
            return roots;
        }

        AddImportMaterialSourceScanRoot(roots, rootPath, defaultSourceRoot, defaultSourceRoot, defaultSourceRoot, CharacterSourcePackageScope);
        AddImportMaterialSourceScanRoot(roots, rootPath, CharacterSourcePublicRootRelative, string.Empty, CharacterSourcePublicRootRelative, CharacterSourcePublicScope);
        AddImportMaterialSourceScanRootFullPath(roots, rootPath, Path.Combine(packagePath, "resources"), string.Empty, JoinImportMaterialPath(packageRelative, "resources"), CharacterSourcePackageScope);
        return roots;
    }

    private static void AddImportMaterialSourceScanRoot(
        List<ImportMaterialSourceScanRoot> roots,
        string rootPath,
        string relativePath,
        string sourceRoot,
        string displayRoot,
        string sourceScope)
    {
        if (IsUnsafeImportMaterialRelativePath(relativePath, allowEmpty: true))
            return;
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathAllowed(rootPath, fullPath))
            return;
        if (roots.Exists(root => string.Equals(root.fullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            return;
        if (Directory.Exists(fullPath))
        {
            roots.Add(new ImportMaterialSourceScanRoot
            {
                fullPath = fullPath,
                sourceRoot = NormalizeImportMaterialRelativePath(sourceRoot),
                displayRoot = NormalizeImportMaterialRelativePath(displayRoot),
                sourceScope = sourceScope
            });
        }
    }

    private static void AddImportMaterialSourceScanRootFullPath(
        List<ImportMaterialSourceScanRoot> roots,
        string rootPath,
        string fullPath,
        string sourceRoot,
        string displayRoot,
        string sourceScope)
    {
        fullPath = Path.GetFullPath(fullPath);
        if (!IsPathAllowed(rootPath, fullPath))
            return;
        if (roots.Exists(root => string.Equals(root.fullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            return;
        if (Directory.Exists(fullPath))
        {
            roots.Add(new ImportMaterialSourceScanRoot
            {
                fullPath = fullPath,
                sourceRoot = NormalizeImportMaterialRelativePath(sourceRoot),
                displayRoot = NormalizeImportMaterialRelativePath(displayRoot),
                sourceScope = sourceScope
            });
        }
    }

    private static IEnumerable<string> EnumerateImportMaterialCandidateFiles(string rootPath)
    {
        var pending = new Queue<string>();
        pending.Enqueue(rootPath);
        int scanned = 0;
        while (pending.Count > 0 && scanned < CharacterImportMaterialSourceCandidateScanLimit)
        {
            string current = pending.Dequeue();
            string[] files;
            try
            {
                files = Directory.GetFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            for (int i = 0; i < files.Length && scanned < CharacterImportMaterialSourceCandidateScanLimit; i++)
            {
                scanned++;
                yield return files[i];
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (IsIgnoredImportMaterialSourceDirectory(name))
                    continue;
                pending.Enqueue(directories[i]);
            }
        }
    }

    private static bool IsIgnoredImportMaterialSourceDirectory(string name)
    {
        return string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Library", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Temp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "MxFrameworkGenerated", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImportMaterialCandidateExtension(string extension)
    {
        switch (extension)
        {
            case ".fbx":
            case ".glb":
            case ".gltf":
            case ".blend":
            case ".anim":
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".psd":
            case ".mat":
            case ".material":
            case ".json":
            case ".wav":
            case ".ogg":
            case ".prefab":
            case ".vfx":
            case ".vfxgraph":
                return true;
            default:
                return false;
        }
    }

    private static CharacterImportMaterialSourceCandidate BuildImportMaterialSourceCandidate(
        string rootPath,
        ImportMaterialSourceScanRoot scanRoot,
        string fullPath)
    {
        string sourcePath = Path.GetRelativePath(scanRoot.fullPath, fullPath).Replace('\\', '/');
        string displayPath = string.IsNullOrWhiteSpace(scanRoot.displayRoot)
            ? sourcePath
            : JoinImportMaterialPath(scanRoot.displayRoot, sourcePath);
        string sourcePathForDocument = string.IsNullOrWhiteSpace(scanRoot.sourceRoot)
            ? displayPath
            : sourcePath;
        string fileName = Path.GetFileName(fullPath);
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        string slotKey = InferImportMaterialCandidateSlotKey(sourcePath, extension);
        string presetId = InferImportMaterialCandidatePresetId(sourcePath, extension, slotKey);
        string bindTarget = slotKey == "body" || slotKey == "mainHand" || slotKey == "offHand" ? slotKey : InferBindTargetFromPreset(presetId);
        string kind = GetImportMaterialCandidateKind(extension, presetId);
        string reason = BuildImportMaterialCandidateReason(slotKey, presetId, extension);
        bool selectable = IsImportMaterialCandidateDirectlySyncable(kind, extension, presetId);
        var candidate = new CharacterImportMaterialSourceCandidate
        {
            id = CharacterPackageResourceKeyGenerator.NormalizeSegment(displayPath),
            sourcePath = sourcePathForDocument,
            displayPath = displayPath,
            fileName = fileName,
            extension = extension.TrimStart('.'),
            sourceKind = kind,
            sourceScope = scanRoot.sourceScope,
            presetId = presetId,
            slotKey = slotKey,
            bindTarget = bindTarget,
            exists = File.Exists(fullPath),
            selectable = selectable,
            reason = reason
        };
        if (!selectable)
        {
            candidate.diagnostics.Add(new CharacterImportMaterialSourceCandidateDiagnostic
            {
                code = "IMPORT_MATERIALS_SOURCE_FORMAT_NEEDS_EXPORT",
                path = displayPath,
                message = "This source is visible for tracking, but v1 sync expects an import-ready package format."
            });
        }

        return candidate;
    }

    private static int GetImportMaterialSourceScopeRank(string sourceScope)
    {
        if (string.Equals(sourceScope, CharacterSourcePackageScope, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (string.Equals(sourceScope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static bool MatchesImportMaterialSourceCandidateQuery(CharacterImportMaterialSourceCandidate candidate, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        return ContainsIgnoreCase(candidate.displayPath, query)
            || ContainsIgnoreCase(candidate.sourcePath, query)
            || ContainsIgnoreCase(candidate.fileName, query)
            || ContainsIgnoreCase(candidate.presetId, query)
            || ContainsIgnoreCase(candidate.slotKey, query);
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string InferImportMaterialCandidateSlotKey(string path, string extension)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        bool modelSource = extension == ".fbx" || extension == ".glb" || extension == ".gltf" || extension == ".blend";
        bool textureSource = extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga" || extension == ".psd";
        if (textureSource && ContainsAnyImportMaterialToken(normalized, "face_detail", "facedetail", "face-detail"))
            return "faceDetail";
        if (modelSource && ContainsAnyImportMaterialToken(normalized, "body", "model", "character", "avatar", "mesh"))
            return "body";
        return string.Empty;
    }

    private static bool ContainsAnyImportMaterialToken(string value, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.Contains(tokens[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string InferImportMaterialCandidatePresetId(string path, string extension, string slotKey)
    {
        if (slotKey == "body")
            return "characterModel";
        if (slotKey == "faceDetail")
            return "faceDetailTexture";
        if (slotKey == "locomotion")
            return "animationClipGroup";
        if (extension == ".fbx" || extension == ".glb" || extension == ".gltf" || extension == ".blend")
            return "modelPreview";
        if (extension == ".anim")
            return "animationClipGroup";
        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga" || extension == ".psd")
            return "texture";
        if (extension == ".mat" || extension == ".material")
            return "material";
        if (extension == ".wav" || extension == ".ogg")
            return "audioCue";
        if (extension == ".prefab" || extension == ".vfx" || extension == ".vfxgraph")
            return "vfxCue";
        return "config";
    }

    private static string GetImportMaterialCandidateKind(string extension, string presetId)
    {
        if (presetId == "animationClipGroup")
            return CharacterPackageResourceTypeIds.Animation;
        if (presetId == "texture")
            return CharacterPackageResourceTypeIds.Texture;
        if (presetId == "material")
            return CharacterPackageResourceTypeIds.Material;
        if (presetId == "audioCue")
            return CharacterPackageResourceTypeIds.Audio;
        if (presetId == "vfxCue")
            return CharacterPackageResourceTypeIds.Vfx;
        if (presetId == "config")
            return CharacterPackageResourceTypeIds.Config;
        if (extension == ".fbx" || extension == ".glb" || extension == ".gltf" || extension == ".blend")
            return CharacterPackageResourceTypeIds.Model;
        return CharacterPackageResourceTypeIds.Config;
    }

    private static bool IsImportMaterialCandidateDirectlySyncable(string kind, string extension, string presetId)
    {
        if (extension == ".blend" || extension == ".psd" || extension == ".mat" || extension == ".material" || extension == ".prefab" || extension == ".vfx" || extension == ".vfxgraph")
            return false;
        if (string.Equals(kind, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return extension == ".fbx" || extension == ".glb" || extension == ".gltf";
        if (string.Equals(kind, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return extension == ".anim" || extension == ".glb" || extension == ".gltf" || extension == ".json";
        if (string.Equals(kind, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga";
        if (string.Equals(kind, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return extension == ".wav" || extension == ".ogg";
        return extension == ".json";
    }

    private static string BuildImportMaterialCandidateReason(string slotKey, string presetId, string extension)
    {
        if (!string.IsNullOrWhiteSpace(slotKey))
            return "Matched " + slotKey + " slot by file name/path and " + extension + " extension.";
        return "Matched " + presetId + " preset by " + extension + " extension.";
    }

    private static string BuildImportMaterialDefaultLabel(CharacterImportMaterialItem item)
    {
        string sourceName = Path.GetFileNameWithoutExtension(item.SourcePath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(sourceName))
            return sourceName;
        if (!string.IsNullOrWhiteSpace(item.BindTarget))
            return item.BindTarget;
        return "material";
    }

    private static string BuildImportMaterialItemId(CharacterImportMaterialItem item, int index)
    {
        string seed = FirstNonEmpty(item.Label, item.BindTarget, item.Slot, Path.GetFileNameWithoutExtension(item.SourcePath), "item-" + index.ToString(CultureInfo.InvariantCulture));
        return CharacterPackageResourceKeyGenerator.NormalizeSegment(seed);
    }

    private static bool IsUnsafeImportMaterialRelativePath(string path, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(path))
            return !allowEmpty;
        string normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathFullyQualified(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
            return true;
        string[] parts = normalized.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "..")
                return true;
        }
        return false;
    }

    private static string NormalizeImportMaterialRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string JoinImportMaterialPath(string first, string second)
    {
        first = NormalizeImportMaterialRelativePath(first);
        second = NormalizeImportMaterialRelativePath(second);
        if (string.IsNullOrWhiteSpace(first))
            return second;
        if (string.IsNullOrWhiteSpace(second))
            return first;
        return first.TrimEnd('/') + "/" + second.TrimStart('/');
    }

    private static string GetCharacterSourceManifestRelative(CharacterResourcePackage package, string packageRelative)
    {
        return JoinImportMaterialPath(GetCharacterSourceRootRelative(package, packageRelative), CharacterSourceManifestFileName);
    }

    private static string GetCharacterSourceRootRelative(CharacterResourcePackage package, string packageRelative)
    {
        string packageId = package?.Manifest?.PackageId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            string normalizedPackage = NormalizeImportMaterialRelativePath(packageRelative);
            packageId = Path.GetFileName(normalizedPackage.TrimEnd('/'));
            if (packageId.StartsWith("character-", StringComparison.OrdinalIgnoreCase))
                packageId = packageId.Substring("character-".Length);
        }

        packageId = NormalizeCharacterPackageId(packageId);
        return JoinImportMaterialPath(CharacterSourceRootRelativePrefix, packageId);
    }

    private static string GetCharacterUnityCatalogPath(string rootPath, string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return string.Empty;
        return Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId, "config", "unity_resource_catalog.json");
    }

    private static JsonElement GetJsonObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.Object)
            return value;
        return default;
    }

    private static List<CharacterImportMaterialsTemplate> BuildImportMaterialTemplates()
    {
        return new List<CharacterImportMaterialsTemplate>
        {
            new CharacterImportMaterialsTemplate
            {
                id = "humanoid-basic",
                label = "Basic Humanoid",
                description = "Body model source. Locomotion is configured as a BlendTree in AnimationEditor.",
                items = new List<CharacterImportMaterialItem>
                {
                    new CharacterImportMaterialItem { Id = "body-model", Label = "Body model", SourcePath = "body.fbx", PresetId = "characterModel", BindTarget = "body", Required = true },
                    new CharacterImportMaterialItem { Id = "face-detail", Label = "Character FaceDetail RGBA", SourcePath = "FaceDetail/face_detail_rgba.png", PresetId = "faceDetailTexture", BindTarget = "faceDetail", Required = false }
                }
            },
            new CharacterImportMaterialsTemplate
            {
                id = "custom-empty",
                label = "Custom",
                description = "Start with an empty checklist.",
                items = new List<CharacterImportMaterialItem>()
            }
        };
    }

    internal sealed class CharacterImportMaterialsSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public string sourceRoot { get; set; } = string.Empty;
        public CharacterImportMaterialsDocument document { get; set; }
        public List<CharacterImportMaterialItem> items { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialsSyncRequest
    {
        public string package { get; set; } = string.Empty;
        public bool allChanged { get; set; }
        public bool force { get; set; }
        public bool dryRun { get; set; }
        public List<string> itemIds { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialsState
    {
        public bool success { get; set; }
        public string packageRelative { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string documentRelative { get; set; } = string.Empty;
        public string sourceManifestRelative { get; set; } = string.Empty;
        public bool sourceManifestExists { get; set; }
        public string stateRelative { get; set; } = string.Empty;
        public string legacyDocumentRelative { get; set; } = string.Empty;
        public bool legacyDocumentExists { get; set; }
        public string documentKind { get; set; } = string.Empty;
        public CharacterImportMaterialsDocument document { get; set; } = new CharacterImportMaterialsDocument();
        public CharacterImportMaterialsSummary summary { get; set; } = new CharacterImportMaterialsSummary();
        public CharacterImportMaterialsIntegrityReceipt integrityReceipt { get; set; } = new CharacterImportMaterialsIntegrityReceipt();
        public List<CharacterImportMaterialItemStatus> items { get; set; } = new();
        public List<CharacterImportMaterialsTemplate> templates { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialSourceCandidatesState
    {
        public bool success { get; set; }
        public string packageRelative { get; set; } = string.Empty;
        public string sourceRoot { get; set; } = string.Empty;
        public string query { get; set; } = string.Empty;
        public bool truncated { get; set; }
        public List<string> roots { get; set; } = new();
        public List<CharacterImportMaterialSourceCandidate> items { get; set; } = new();
        public List<CharacterImportMaterialSourceCandidateDiagnostic> diagnostics { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialSourceCandidate
    {
        public string id { get; set; } = string.Empty;
        public string sourcePath { get; set; } = string.Empty;
        public string displayPath { get; set; } = string.Empty;
        public string fileName { get; set; } = string.Empty;
        public string extension { get; set; } = string.Empty;
        public string sourceKind { get; set; } = string.Empty;
        public string sourceScope { get; set; } = string.Empty;
        public string presetId { get; set; } = string.Empty;
        public string slotKey { get; set; } = string.Empty;
        public string bindTarget { get; set; } = string.Empty;
        public bool exists { get; set; }
        public bool selectable { get; set; }
        public string reason { get; set; } = string.Empty;
        public List<CharacterImportMaterialSourceCandidateDiagnostic> diagnostics { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialSourceCandidateDiagnostic
    {
        public string code { get; set; } = string.Empty;
        public string path { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CharacterImportMaterialsSummary
    {
        public int total { get; set; }
        public int clean { get; set; }
        public int changed { get; set; }
        public int notImported { get; set; }
        public int missing { get; set; }
        public int unityPending { get; set; }
        public int blocked { get; set; }
        public int dirty { get; set; }
        public string integrityStatus { get; set; } = "NotApplicable";
        public int integrityBlocking { get; set; }
        public int integrityWarnings { get; set; }
    }

    internal sealed class CharacterImportMaterialsTemplate
    {
        public string id { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public List<CharacterImportMaterialItem> items { get; set; } = new();
    }

    internal sealed class CharacterImportMaterialItemStatus
    {
        public int index { get; set; }
        public string id { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public CharacterImportMaterialItem item { get; set; } = new CharacterImportMaterialItem();
        public bool sourceSafe { get; set; }
        public bool sourceExists { get; set; }
        public string sourceHash { get; set; } = string.Empty;
        public bool sourceDirty { get; set; }
        public string sourceReferenceKind { get; set; } = string.Empty;
        public string sourceScope { get; set; } = string.Empty;
        public string resolvedSourcePath { get; set; } = string.Empty;
        public string sourceFullPath { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public bool resourceExists { get; set; }
        public string resourceRelativePath { get; set; } = string.Empty;
        public bool unitySynced { get; set; }
        public string unityImportStatus { get; set; } = string.Empty;
        public string unityAssetPath { get; set; } = string.Empty;
        public CharacterImportMaterialFbxTextureDiagnostic fbxTextureDiagnostic { get; set; }
        public string status { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CharacterImportMaterialFbxTextureDiagnostic
    {
        public string status { get; set; } = string.Empty;
        public bool embeddedTextureDetected { get; set; }
        public bool externalTextureRefs { get; set; }
        public List<string> externalTexturePaths { get; set; } = new();
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CharacterImportMaterialsSyncResponse
    {
        public bool success { get; set; }
        public bool dryRun { get; set; }
        public string packageRelative { get; set; } = string.Empty;
        public List<CharacterImportMaterialsSyncOperation> operations { get; set; } = new();
        public List<CharacterImportMaterialsIntegrityDiagnostic> diagnostics { get; set; } = new();
        public CharacterImportMaterialsState state { get; set; } = new CharacterImportMaterialsState();
    }

    internal sealed class CharacterImportMaterialsSyncOperation
    {
        public bool success { get; set; }
        public bool skipped { get; set; }
        public string itemId { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public string sourcePath { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public string statusBefore { get; set; } = string.Empty;
        public string statusAfter { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    private sealed class ImportMaterialSourceScanRoot
    {
        public string fullPath { get; set; } = string.Empty;
        public string sourceRoot { get; set; } = string.Empty;
        public string displayRoot { get; set; } = string.Empty;
        public string sourceScope { get; set; } = string.Empty;
    }

    private sealed class ImportMaterialResolvedSource
    {
        public bool safe { get; set; }
        public bool exists { get; set; }
        public string fullPath { get; set; } = string.Empty;
        public string displayPath { get; set; } = string.Empty;
        public string referenceKind { get; set; } = string.Empty;
        public string scope { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    private sealed class CharacterImportMaterialsLoad
    {
        public CharacterImportMaterialsDocument Document { get; set; } = new CharacterImportMaterialsDocument();
        public string DocumentRelative { get; set; } = string.Empty;
        public string SourceManifestRelative { get; set; } = string.Empty;
        public bool SourceManifestExists { get; set; }
        public string SourceManifestDirectoryRelative { get; set; } = string.Empty;
        public string SourceManifestDirectoryPath { get; set; } = string.Empty;
        public string StateRelative { get; set; } = string.Empty;
        public string LegacyDocumentRelative { get; set; } = string.Empty;
        public bool LegacyDocumentExists { get; set; }
        public string DocumentKind { get; set; } = string.Empty;
        public CharacterImportMaterialsIntegrityReceipt PersistedIntegrityReceipt { get; set; }
    }

    private sealed class ImportMaterialUnityStatus
    {
        public bool synced { get; set; }
        public string importStatus { get; set; } = string.Empty;
        public string assetPath { get; set; } = string.Empty;
    }

    private sealed class ResolvedImportTexture
    {
        public CharacterImportMaterialItem Item { get; set; }
        public CharacterPackageResourceEntry Entry { get; set; }
        public string ResourceKey { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string Semantic { get; set; } = string.Empty;
    }

    private sealed class MaterialDescriptorSnapshot
    {
        public string SourceMaterialName { get; set; } = string.Empty;
        public string SurfaceMode { get; set; } = CharacterMaterialSurfaceModeIds.Opaque;
        public float AlphaCutoff { get; set; } = 0.5f;
        public bool SurfaceModeSupported { get; set; } = true;
        public List<string> TextureResourceKeys { get; set; } = new();
        public List<MaterialDescriptorTextureSnapshot> Textures { get; set; } = new();
    }

    private sealed class MaterialDescriptorTextureSnapshot
    {
        public string Property { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string Semantic { get; set; } = string.Empty;
    }

    private sealed class RendererMaterialBindingSnapshot
    {
        public string MaterialResourceKey { get; set; } = string.Empty;
        public string SourceMaterialName { get; set; } = string.Empty;
    }

    private sealed class GeneratedImportMaterialDescriptor
    {
        public string SchemaVersion { get; set; } = "raw.unity.material.v1";
        public string SourceAssetPath { get; set; } = string.Empty;
        public string ShaderName { get; set; } = "WGame/CharacterTraditionalLit";
        public string RuntimeMaterialStatus { get; set; } = "RequiresWGameFrameworkMaterialGeneration";
        public string SurfaceMode { get; set; } = CharacterMaterialSurfaceModeIds.Opaque;
        public float AlphaCutoff { get; set; } = 0.5f;
        public int RenderQueue { get; set; } = 2000;
        public bool EnableInstancing { get; set; }
        public bool DoubleSidedGi { get; set; }
        public List<GeneratedImportMaterialTexture> Textures { get; set; } = new();
        public List<GeneratedImportMaterialColor> Colors { get; set; } = new()
        {
            new GeneratedImportMaterialColor()
        };
        public List<string> UnresolvedSourceGuids { get; set; } = new();
    }

    private sealed class GeneratedImportMaterialTexture
    {
        public string Property { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public GeneratedImportMaterialVector2 Scale { get; set; } = new() { X = 1, Y = 1 };
        public GeneratedImportMaterialVector2 Offset { get; set; } = new();
    }

    private sealed class GeneratedImportMaterialVector2
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    private sealed class GeneratedImportMaterialColor
    {
        public string Name { get; set; } = "_Color";
        public GeneratedImportMaterialColorValue Value { get; set; } = new();
    }

    private sealed class GeneratedImportMaterialColorValue
    {
        public float R { get; set; } = 1;
        public float G { get; set; } = 1;
        public float B { get; set; } = 1;
        public float A { get; set; } = 1;
    }

    private readonly struct ImportMaterialPreset
    {
        public ImportMaterialPreset(string kind, string usage, string role, string bindTarget, string localId)
        {
            this.kind = kind ?? string.Empty;
            this.usage = usage ?? string.Empty;
            this.role = role ?? string.Empty;
            this.bindTarget = bindTarget ?? string.Empty;
            this.localId = localId ?? string.Empty;
        }

        public readonly string kind;
        public readonly string usage;
        public readonly string role;
        public readonly string bindTarget;
        public readonly string localId;
    }

    private const int CharacterImportMaterialSourceCandidateMaxCount = 500;
    private const int CharacterImportMaterialSourceCandidateScanLimit = 5000;
}
