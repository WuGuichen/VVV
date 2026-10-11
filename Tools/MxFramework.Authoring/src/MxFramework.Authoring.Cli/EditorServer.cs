using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string DefaultPackageRelativePath = "Authoring/CharacterPackages/character-000001";
    private const string DefaultCharacterPackageRelativePath = "Authoring/CharacterPackages/character-000001";
    private const string AnimationAuthoringDocumentRelativePath = "config/animation_authoring.json";
    private const string SamplesRelativePath = "Tools/MxFramework.Authoring/samples";
    private const string ProjectCharacterPackagesRelativePath = "Authoring/CharacterPackages";
    private static string s_toolsRootPath = string.Empty;
    private static string s_defaultPackage = string.Empty;
    private static int s_port;
    private static int s_restartScheduled;
    private static readonly TimeSpan AuthoringResourceCacheDuration = TimeSpan.FromSeconds(2);
    private static readonly object AuthoringResourceCacheGate = new object();
    private static readonly Dictionary<string, AuthoringResourceCacheEntry> AuthoringResourceCache = new Dictionary<string, AuthoringResourceCacheEntry>(StringComparer.Ordinal);
    private static readonly object CompactJsonOptionsGate = new object();
    private static readonly string[] CharacterPackageRevisionRelativeFiles =
    {
        "manifest.json",
        "resource_catalog.json",
        "config/character_application.json",
        CharacterBaseAttributesAuthoring.RelativePath,
        MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath,
        "config/character_actions.json",
        "config/character_palette_binding.json",
        "config/character_palette_usage.json",
        "config/character_baked_surface_binding.json",
        "geometry/body_geometry.json",
        "geometry/body_parts.json",
        "geometry/body_colliders.json",
        "geometry/body_presence.json",
        "geometry/damage_zones.json",
        "geometry/engagement_anchors.json",
        "geometry/sockets.json",
        "geometry/weapon_slots.json",
        "geometry/body_part_contact_points.json",
        "validation/last_report.json"
    };
    private static JsonSerializerOptions s_compactJsonOptionsSource;
    private static JsonSerializerOptions s_compactJsonOptions;

    public static int Serve(string root, int port, JsonSerializerOptions jsonOptions, string defaultPackageRelativePath = DefaultPackageRelativePath)
    {
        return Serve(root, root, port, jsonOptions, defaultPackageRelativePath);
    }

    public static int Serve(string toolsRoot, string projectRoot, int port, JsonSerializerOptions jsonOptions, string defaultPackageRelativePath = DefaultPackageRelativePath)
    {
        string toolsRootPath = NormalizeDirectoryRoot(toolsRoot);
        string projectRootPath = NormalizeDirectoryRoot(projectRoot);
        string defaultPackage = string.IsNullOrEmpty(defaultPackageRelativePath) ? DefaultPackageRelativePath : defaultPackageRelativePath;
        s_toolsRootPath = toolsRootPath;
        s_defaultPackage = defaultPackage;
        s_port = port;
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Console.WriteLine("MxFramework Authoring Tools");
        Console.WriteLine($"Tools root: {toolsRootPath}");
        Console.WriteLine($"Project root: {projectRootPath}");
        Console.WriteLine($"Hub URL: http://127.0.0.1:{port}/Tools/MxFramework.EditorHub/web/");
        Console.WriteLine($"CharacterStudio URL: http://127.0.0.1:{port}/Tools/MxFramework.CharacterStudio/web/");
        Console.WriteLine($"AnimationEditor URL: http://127.0.0.1:{port}/Tools/MxFramework.AnimationEditor/web/");
        Console.WriteLine($"Default package: {defaultPackage}");

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            try
            {
                Handle(context, projectRootPath, defaultPackage, jsonOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("request error: " + ex);
                try
                {
                    WriteJson(context.Response, new { error = ex.Message }, jsonOptions, 500);
                }
                catch
                {
                }
            }
        }
    }

    private static void Handle(HttpListenerContext context, string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        if (path == "/")
        {
            context.Response.Redirect("/Tools/MxFramework.EditorHub/web/");
            context.Response.Close();
            return;
        }

        if (TryHandleCharacterBodyPresenceApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;
        if (TryHandleCharacterBodyPartDamageApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;
        if (TryHandleSurfacePaletteApi(context, rootPath, jsonOptions, path))
            return;

        string packageRelative = ResolvePackageRelative(context, rootPath, defaultPackage);

        if (path == "/api/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListPackages(rootPath), jsonOptions);
            return;
        }

        if (path == "/api/editor/restart" && context.Request.HttpMethod == "POST")
        {
            ScheduleEditorRestart(rootPath, port: s_port, defaultPackage);
            WriteJson(context.Response, new
            {
                restarting = true,
                port = s_port,
                package = defaultPackage
            }, jsonOptions, 202);
            return;
        }

        if (TryHandleActionDamageAnalysisApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleCharacterActionLibraryApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;
        if (TryHandleTacticalCardQueryApi(context, rootPath, jsonOptions, path))
            return;
        if (TryHandleTacticalContentApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleCombatDesignApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleCharacterActionOpenApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleCharacterActionReactionProfileApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleCharacterUnityImportEditorApi(context, rootPath, jsonOptions, path))
            return;
        if (TryHandleCharacterClothMagicaApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;

        if (TryHandleBuffAuthoringApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleFairyGuiArtSlicesApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleUiComposerApi(context, rootPath, jsonOptions, path))
            return;

        if (TryHandleAudioWorkbenchApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;

        if (path == "/api/character/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListCharacterPackages(rootPath, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListAnimationPackages(rootPath, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/load" && context.Request.HttpMethod == "GET")
        {
            string animationPackage = context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, ReadAnimationPackageState(rootPath, animationPackage, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/save" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, SaveAnimationPackage(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/validate" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            AnimationAuthoringPackage package = request.animation;
            if (package == null)
            {
                string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                    ? request.package
                    : context.Request.QueryString["package"] ?? string.Empty;
                package = ReadAnimationPackage(rootPath, ResolveAnimationDocumentPath(rootPath, animationPackage, defaultPackage, jsonOptions), jsonOptions);
            }
            WriteJson(context.Response, ValidateAnimationPackage(package), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/compile" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, CompileAnimationPackage(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/preview" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            AnimationAuthoringSaveRequest request = context.Request.HttpMethod == "POST"
                ? ReadAnimationAuthoringSaveRequest(context, jsonOptions)
                : new AnimationAuthoringSaveRequest();
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, ReadAnimationPreview(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/state" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/state" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadGlobalWeaponLibraryState(rootPath, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/references" && context.Request.HttpMethod == "GET")
        {
            string configurationType = context.Request.QueryString["configurationType"] ?? string.Empty;
            string configurationId = context.Request.QueryString["configurationId"] ?? string.Empty;
            List<GlobalWeaponLibraryReference> references = FindGlobalConfigurationReferences(
                rootPath,
                configurationType,
                configurationId,
                jsonOptions);
            WriteJson(context.Response, new
            {
                configurationType,
                configurationId,
                referenceCount = references.Count,
                references
            }, jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/reference-index" && context.Request.HttpMethod == "GET")
        {
            string configurationType = context.Request.QueryString["configurationType"] ?? string.Empty;
            WriteJson(
                context.Response,
                BuildGlobalConfigurationReferenceIndex(rootPath, configurationType, jsonOptions),
                jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/configurations" && context.Request.HttpMethod == "GET")
        {
            string configurationType = context.Request.QueryString["configurationType"] ?? string.Empty;
            WriteJson(context.Response,
                ReadGlobalConfigurations(rootPath, configurationType, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/configuration/validate" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            GlobalConfigurationMutationRequest request =
                JsonSerializer.Deserialize<GlobalConfigurationMutationRequest>(body, jsonOptions) ??
                new GlobalConfigurationMutationRequest();
            WriteGlobalConfigurationMutationResponse(context.Response,
                () => ValidateGlobalConfiguration(rootPath, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/configuration/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            GlobalConfigurationMutationRequest request =
                JsonSerializer.Deserialize<GlobalConfigurationMutationRequest>(body, jsonOptions) ??
                new GlobalConfigurationMutationRequest();
            WriteGlobalConfigurationMutationResponse(context.Response,
                () => SaveGlobalConfiguration(rootPath, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/configuration/delete" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            GlobalConfigurationDeleteRequest request =
                JsonSerializer.Deserialize<GlobalConfigurationDeleteRequest>(body, jsonOptions) ??
                new GlobalConfigurationDeleteRequest();
            WriteGlobalConfigurationMutationResponse(context.Response,
                () => DeleteGlobalConfiguration(rootPath, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/weapon-library/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            GlobalWeaponLibrarySaveRequest request =
                JsonSerializer.Deserialize<GlobalWeaponLibrarySaveRequest>(body, jsonOptions) ??
                new GlobalWeaponLibrarySaveRequest();
            try
            {
                WriteJson(context.Response, SaveGlobalWeaponLibrary(rootPath, request, jsonOptions), jsonOptions);
            }
            catch (GlobalWeaponLibraryWriteConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = GlobalWeaponLibraryWriteConflictException.ConflictCode,
                    expectedRevision = ex.ExpectedRevision,
                    currentRevision = ex.CurrentRevision,
                    message = ex.Message
                }, jsonOptions, 409);
            }
            catch (GlobalWeaponLibraryInUseException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = CharacterAuthoringValidationCodes.GlobalWeaponConfigurationInUse,
                    configurationType = ex.ConfigurationType,
                    configurationId = ex.ConfigurationId,
                    references = ex.References,
                    message = ex.Message
                }, jsonOptions, 409);
            }
            return;
        }

        if (path == "/api/character/readiness" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterReadiness(rootPath, characterPackage, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/ability-chain" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterAbilityChain(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/weapon-configurations" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterWeaponConfigurations(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (TryHandleCharacterImportMaterialsApi(context, rootPath, defaultPackage, jsonOptions, path))
            return;

        if (TryHandleCommonResourcesApi(context, rootPath, jsonOptions, path))
            return;

        if (path == "/api/character/resources" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterResources(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/resources/inspect" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string id = context.Request.QueryString["id"] ?? string.Empty;
            object result = ReadCharacterResourceInspect(rootPath, characterPackage, id, jsonOptions);
            if (result == null)
            {
                WriteJson(context.Response, new
                {
                    error = "RESOURCE_LIBRARY_ITEM_NOT_FOUND",
                    message = "Resource library item was not found.",
                    id
                }, jsonOptions, 404);
                return;
            }

            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/audio-semantics" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAudioSemanticManifestState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/audio-semantics/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            AudioSemanticManifestSaveRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new AudioSemanticManifestSaveRequest()
                    : (JsonSerializer.Deserialize<AudioSemanticManifestSaveRequest>(body, jsonOptions) ?? new AudioSemanticManifestSaveRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing,
                    message = "音频语义保存 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            AudioSemanticManifestSaveResponse result = SaveAudioSemanticManifest(rootPath, characterPackage, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/audio-semantics/validate-replacement" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            AudioSemanticReplacementApiRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new AudioSemanticReplacementApiRequest()
                    : (JsonSerializer.Deserialize<AudioSemanticReplacementApiRequest>(body, jsonOptions) ?? new AudioSemanticReplacementApiRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    accepted = false,
                    diagnostics = new[]
                    {
                        new AuthoringResourceDiagnostic
                        {
                            Severity = CharacterAuthoringValidationSeverity.Error,
                            Code = AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                            Message = "音频替换验证 JSON 无效：" + ex.Message,
                            SourceConfigKind = "audioSemanticManifest"
                        }
                    }
                }, jsonOptions, 400);
                return;
            }

            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            WriteJson(context.Response, ValidateAudioSemanticReplacement(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/resource-mapping/plan" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string buildTarget = context.Request.QueryString["buildTarget"] ?? string.Empty;
            try { buildTarget = ResourceMappingBuildTargetResolver.Resolve(rootPath, buildTarget); }
            catch (InvalidOperationException ex)
            {
                WriteJson(context.Response, new { success = false, error = "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED", message = ex.Message }, jsonOptions, 409);
                return;
            }
            WriteJson(context.Response, ReadResourceMappingPlan(rootPath, buildTarget), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/global-build-status" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string buildTarget = context.Request.QueryString["buildTarget"] ?? string.Empty;
            WriteJson(context.Response, ReadGlobalResourceBuildStatus(rootPath, characterPackage, jsonOptions, buildTarget), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/resource-mapping" && context.Request.HttpMethod == "GET")
        {
            string buildTarget = context.Request.QueryString["buildTarget"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buildTarget) || string.Equals(buildTarget, "ActiveBuildTarget", StringComparison.Ordinal))
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    error = "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED",
                    message = "resource-mapping requires a concrete buildTarget from Unity build status."
                }, jsonOptions, 400);
                return;
            }
            WriteJson(context.Response, ReadResourceMappingState(rootPath, buildTarget), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/resource-mapping/manual/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            ResourceMappingManualSnapshotSaveRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new ResourceMappingManualSnapshotSaveRequest()
                    : (JsonSerializer.Deserialize<ResourceMappingManualSnapshotSaveRequest>(body, jsonOptions) ?? new ResourceMappingManualSnapshotSaveRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new { success = false, error = "RESOURCE_MAPPING_MANUAL_INVALID_JSON", message = ex.Message }, jsonOptions, 400);
                return;
            }
            object result = SaveResourceMappingManualSnapshot(rootPath, request.expectedRevision, request.snapshot, request.buildTarget, jsonOptions);
            JsonElement resultJson = JsonSerializer.SerializeToElement(result, jsonOptions);
            bool success = resultJson.TryGetProperty("success", out JsonElement successElement) && successElement.GetBoolean();
            WriteJson(context.Response, result, jsonOptions, success ? 200 : 409);
            return;
        }

        if (path == "/api/authoring/resources/art-tasks/update" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            ArtSourceTaskUpdateRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new ArtSourceTaskUpdateRequest()
                    : (JsonSerializer.Deserialize<ArtSourceTaskUpdateRequest>(body, jsonOptions) ?? new ArtSourceTaskUpdateRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.ArtSourceTaskUpdateInvalid,
                    message = "美术任务更新 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            ArtSourceTaskUpdateResponse result = SaveArtSourceTaskUpdate(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/art-tasks/create" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            ArtSourceTaskCreateRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new ArtSourceTaskCreateRequest()
                    : (JsonSerializer.Deserialize<ArtSourceTaskCreateRequest>(body, jsonOptions) ?? new ArtSourceTaskCreateRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.ArtSourceTaskCreateInvalid,
                    message = "美术任务创建 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            ArtSourceTaskCreateResponse result = SaveArtSourceTaskCreate(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/story-packets/update" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            StorySourcePacketUpdateRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new StorySourcePacketUpdateRequest()
                    : (JsonSerializer.Deserialize<StorySourcePacketUpdateRequest>(body, jsonOptions) ?? new StorySourcePacketUpdateRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid,
                    message = "故事输入包更新 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            StorySourcePacketResponse result = SaveStorySourcePacketUpdate(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/story-packets/approve" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            StorySourcePacketApproveRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new StorySourcePacketApproveRequest()
                    : (JsonSerializer.Deserialize<StorySourcePacketApproveRequest>(body, jsonOptions) ?? new StorySourcePacketApproveRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid,
                    message = "故事输入包审批 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            StorySourcePacketResponse result = SaveStorySourcePacketApprove(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/story-canon/update" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            StorySourceCanonUpdateRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new StorySourceCanonUpdateRequest()
                    : (JsonSerializer.Deserialize<StorySourceCanonUpdateRequest>(body, jsonOptions) ?? new StorySourceCanonUpdateRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid,
                    message = "世界观记忆更新 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            StorySourcePacketResponse result = SaveStorySourceCanonUpdate(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/story-outlines/update" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            StorySourceOutlineUpdateRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new StorySourceOutlineUpdateRequest()
                    : (JsonSerializer.Deserialize<StorySourceOutlineUpdateRequest>(body, jsonOptions) ?? new StorySourceOutlineUpdateRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid,
                    message = "故事大纲更新 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            StorySourcePacketResponse result = SaveStorySourceOutlineUpdate(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/story-outlines/approve-for-packet" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            StorySourceOutlineApproveRequest request;
            try
            {
                request = string.IsNullOrWhiteSpace(body)
                    ? new StorySourceOutlineApproveRequest()
                    : (JsonSerializer.Deserialize<StorySourceOutlineApproveRequest>(body, jsonOptions) ?? new StorySourceOutlineApproveRequest());
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid,
                    message = "故事大纲批准 JSON 无效：" + ex.Message
                }, jsonOptions, 400);
                return;
            }

            StorySourcePacketResponse result = SaveStorySourceOutlineApproveForPacket(rootPath, request, jsonOptions);
            WriteJson(context.Response, result, jsonOptions, result.httpStatus);
            return;
        }

        if (path == "/api/authoring/resources/inspect" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string id = context.Request.QueryString["id"] ?? string.Empty;
            object result = ReadAuthoringResourceInspect(rootPath, characterPackage, id, jsonOptions);
            if (result == null)
            {
                WriteJson(context.Response, new
                {
                    error = "AUTH_RES_ITEM_MISSING",
                    message = "Authoring resource item was not found.",
                    id
                }, jsonOptions, 404);
                return;
            }

            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/references" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions).ReferenceGraph, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/providers" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions).Providers, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/stage-import" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ExternalImportStageRequest request = ReadExternalImportStageRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            WriteJson(context.Response, StageExternalImport(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            return;
        }

        if ((path == "/api/authoring/resources/pick" || path == "/api/authoring/resources/resolve-selection") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            AuthoringResourceSelectionRequest request = JsonSerializer.Deserialize<AuthoringResourceSelectionRequest>(body, jsonOptions) ?? new AuthoringResourceSelectionRequest();
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            AuthoringResourceCollection resources = ReadAuthoringResources(rootPath, characterPackage, jsonOptions);
            var service = new AuthoringResourceSelectionService();
            object result = path == "/api/authoring/resources/pick"
                ? service.Query(resources, request.fieldSpec, request.context)
                : service.Resolve(resources, request.fieldSpec, request.context, request.selection);
            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if ((path == "/api/authoring/resources/import" || path == "/api/character/resources/import") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ImportCharacterResource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/reimport" || path == "/api/character/resources/reimport") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ReimportCharacterResource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/replace-source" || path == "/api/character/resources/replace-source") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ReplaceCharacterResourceSource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/resource-plan" || path == "/api/character/resource-plan") && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            bool checkHashes = string.Equals(context.Request.QueryString["checkHashes"], "true", StringComparison.OrdinalIgnoreCase);
            WriteJson(context.Response, ReadCharacterResourcePlan(rootPath, characterPackage, checkFiles: true, checkHashes: checkHashes, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/id-catalog" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadCharacterIdCatalogState(rootPath, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/reallocate-id" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterIdReallocateRequest request = JsonSerializer.Deserialize<CharacterIdReallocateRequest>(body, jsonOptions) ?? new CharacterIdReallocateRequest();
            try
            {
                WriteJson(context.Response, ReallocateDraftCharacterId(rootPath, request, jsonOptions), jsonOptions);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                WriteJson(context.Response, new { success = false, code = "CHARACTER_ID_REALLOCATION_BLOCKED", message = ex.Message }, jsonOptions, 409);
            }
            return;
        }

        if (path == "/api/character/authoring-sources" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterAuthoringSourceState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/authoring-sources/apply" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterAuthoringSourceApplyRequest request = JsonSerializer.Deserialize<CharacterAuthoringSourceApplyRequest>(body, jsonOptions) ?? new CharacterAuthoringSourceApplyRequest();
            try
            {
                WriteJson(context.Response, ApplyCharacterAuthoringSource(rootPath, request, jsonOptions), jsonOptions);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, new { success = false, code = "CHAR_AUTHORING_SOURCE_INVALID_REQUEST", message = ex.Message }, jsonOptions, 400);
            }
            return;
        }

        if (path == "/api/character/create" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioCreateRequest request = JsonSerializer.Deserialize<CharacterStudioCreateRequest>(body, jsonOptions) ?? new CharacterStudioCreateRequest();
            try
            {
                object created = CreateCharacterPackage(rootPath, request, jsonOptions);
                WriteJson(context.Response, created, jsonOptions, 201);
            }
            catch (CharacterPackageCreateConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = ex.Code,
                    message = ex.Message,
                    conflictPackage = ex.ConflictPackageRelative
                }, jsonOptions, 409);
            }
            catch (CharacterIdCatalogConflictException ex)
            {
                WriteJson(context.Response, new { success = false, code = ex.Code, message = ex.Message, actualRevision = ex.ActualRevision }, jsonOptions, 409);
            }
            return;
        }

        if (path == "/api/character/duplicate" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioDuplicateRequest request = JsonSerializer.Deserialize<CharacterStudioDuplicateRequest>(body, jsonOptions) ?? new CharacterStudioDuplicateRequest();
            try
            {
                object duplicated = DuplicateCharacterPackage(rootPath, request, jsonOptions);
                WriteJson(context.Response, duplicated, jsonOptions, 201);
            }
            catch (CharacterPackageCreateConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = ex.Code,
                    message = ex.Message,
                    conflictPackage = ex.ConflictPackageRelative
                }, jsonOptions, 409);
            }
            catch (CharacterIdCatalogConflictException ex)
            {
                WriteJson(context.Response, new { success = false, code = ex.Code, message = ex.Message, actualRevision = ex.ActualRevision }, jsonOptions, 409);
            }
            return;
        }

        if ((path == "/api/character/delete-plan" || path == "/api/character/delete") && context.Request.HttpMethod == "POST")
        {
            HandleCharacterDeletion(context, rootPath, path == "/api/character/delete", jsonOptions);
            return;
        }

        if (path == "/api/character/archive" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioArchiveRequest request = JsonSerializer.Deserialize<CharacterStudioArchiveRequest>(body, jsonOptions) ?? new CharacterStudioArchiveRequest();
            try
            {
                WriteJson(context.Response, ArchiveCharacterPackage(rootPath, request, jsonOptions), jsonOptions);
            }
            catch (CharacterPackageManagementConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = ex.Code,
                    message = ex.Message,
                    conflictPackage = ex.ConflictPackageRelative
                }, jsonOptions, 409);
            }
            return;
        }

        if (path == "/api/character/save" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioSaveRequest request = JsonSerializer.Deserialize<CharacterStudioSaveRequest>(body, jsonOptions) ?? new CharacterStudioSaveRequest();
            if (request.package == null)
                throw new ArgumentException("package is required.");
            try
            {
                SaveCharacterStudioPackage(
                    rootPath,
                    characterPackage,
                    request.package,
                    jsonOptions,
                    request.expectedRevision,
                    request.force);
                WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            }
            catch (CharacterPackageWriteConflictException ex)
            {
                WriteCharacterPackageWriteConflict(context.Response, ex, jsonOptions);
            }
            catch (GlobalWeaponLibraryWriteConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = GlobalWeaponLibraryWriteConflictException.ConflictCode,
                    expectedRevision = ex.ExpectedRevision,
                    currentRevision = ex.CurrentRevision,
                    message = ex.Message
                }, jsonOptions, 409);
            }
            catch (CharacterPackageValidationException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = CharacterPackageValidationException.ErrorCode,
                    message = ex.Message,
                    validation = ex.Validation
                }, jsonOptions, 422);
            }
            return;
        }

        if (path == "/api/character/humanoid-hurtboxes" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            try
            {
                var request = JsonSerializer.Deserialize<CharacterHumanoidHurtboxRequest>(body, jsonOptions);
                // Read-only draft generation; persistence remains the revision-gated character save route.
                CharacterHumanoidHurtboxResult result = CharacterHumanoidHurtboxTemplate.Generate(request);
                WriteJson(context.Response, result, jsonOptions, result.Success ? 200 : 422);
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new { success = false, issues = new[] { "无效的模板请求：" + ex.Message } }, jsonOptions, 400);
            }
            return;
        }

        if (path == "/api/character/hurtbox-fit-review" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioHurtboxFitReviewRequest request = JsonSerializer.Deserialize<CharacterStudioHurtboxFitReviewRequest>(body, jsonOptions)
                ?? new CharacterStudioHurtboxFitReviewRequest();
            try
            {
                string packagePath = ResolveSafePath(rootPath, characterPackage);
                string cachePath = Path.Combine(packagePath, "package_cache");
                string colliderPath = Path.Combine(packagePath, "geometry", "body_colliders.json");
                if (!File.Exists(colliderPath)) throw new InvalidOperationException("Authoritative body_colliders.json is missing.");
                string currentColliderHash = ComputeHurtboxColliderFileHash(colliderPath);
                if (!string.IsNullOrWhiteSpace(request.colliderHash)
                    && !string.Equals(request.colliderHash, currentColliderHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Hurtbox confirmation does not match the current saved body_colliders.json. Reload the saved character before confirming.");
                if (!request.acceptedAsAuthored)
                    throw new InvalidOperationException("Hurtbox import readiness requires explicit author confirmation.");
                request.colliderHash = currentColliderHash;
                request.animation = "manual-author-confirmation";
                request.measurementStatus = "NotRequired";
                Directory.CreateDirectory(cachePath);
                string reviewPath = Path.Combine(cachePath, "hurtbox_fit_review.json");
                File.WriteAllText(reviewPath, JsonSerializer.Serialize(request, jsonOptions) + Environment.NewLine);
                WriteJson(context.Response, new
                {
                    success = true,
                    passed = true,
                    confirmationMode = "ManualAuthoring",
                    review = request
                }, jsonOptions);
            }
            catch (Exception ex)
            {
                WriteJson(context.Response, new { success = false, message = ex.Message }, jsonOptions, 422);
            }
            return;
        }

        if (path == "/api/character/migration" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterMigrationPlan(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/migration" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioMigrationRequest request = JsonSerializer.Deserialize<CharacterStudioMigrationRequest>(body, jsonOptions)
                ?? new CharacterStudioMigrationRequest();
            try
            {
                WriteJson(
                    context.Response,
                    MigrateCharacterPackage(
                        rootPath,
                        characterPackage,
                        request.expectedRevision,
                        request.expectedWeaponLibraryRevision,
                        request.force,
                        jsonOptions),
                    jsonOptions);
            }
            catch (CharacterPackageWriteConflictException ex)
            {
                WriteCharacterPackageWriteConflict(context.Response, ex, jsonOptions);
            }
            catch (GlobalWeaponLibraryWriteConflictException ex)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    code = GlobalWeaponLibraryWriteConflictException.ConflictCode,
                    expectedRevision = ex.ExpectedRevision,
                    currentRevision = ex.CurrentRevision,
                    message = ex.Message
                }, jsonOptions, 409);
            }
            return;
        }

        if (path == "/api/character/import-model" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioModelImportRequest request = JsonSerializer.Deserialize<CharacterStudioModelImportRequest>(body, jsonOptions) ?? new CharacterStudioModelImportRequest();
            ImportCharacterModel(rootPath, characterPackage, request, jsonOptions);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/compile" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            bool checkHashes = string.Equals(context.Request.QueryString["checkHashes"], "true", StringComparison.OrdinalIgnoreCase);
            WriteJson(context.Response, CompileCharacterPackage(rootPath, characterPackage, checkFiles: true, checkHashes: checkHashes, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/import-unity" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioImportRequest request = string.IsNullOrWhiteSpace(body)
                ? new CharacterStudioImportRequest()
                : (JsonSerializer.Deserialize<CharacterStudioImportRequest>(body, jsonOptions) ?? new CharacterStudioImportRequest());
            string characterPackage = string.IsNullOrWhiteSpace(request.package)
                ? ResolveCharacterPackageRelative(context, rootPath, defaultPackage)
                : request.package;
            ResolveSafePath(rootPath, characterPackage);
            WriteJson(context.Response, RunCharacterImport(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/mod/diagnose" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            ModDiagnoseRequest request = ReadDiagnoseRequest(context, rootPath);
            if (request.containers.Count == 0)
                request.containers.Add("Assets/StreamingAssets/MxFramework/Demo");
            ModDiagnosticSnapshotDto snapshot = ModDiagnosticService.BuildSnapshot(
                request.containers.ToArray(),
                request.loadout,
                request.includeAbsolutePaths);
            WriteJson(context.Response, snapshot, jsonOptions);
            return;
        }

        ServeStatic(
            context.Response,
            IsProjectStaticPreviewPath(path) ? rootPath : string.IsNullOrWhiteSpace(s_toolsRootPath) ? rootPath : s_toolsRootPath,
            path);
    }

    private static string ResolvePackageRelative(HttpListenerContext context, string rootPath, string defaultPackage)
    {
        string requested = context.Request.QueryString["package"];
        if (string.IsNullOrEmpty(requested))
            return defaultPackage;
        ResolveSafePath(rootPath, requested);
        return requested;
    }

    private static string ResolveCharacterPackageRelative(HttpListenerContext context, string rootPath, string defaultPackage)
    {
        string requested = context.Request.QueryString["package"];
        if (string.IsNullOrEmpty(requested))
            return IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath;
        ResolveSafePath(rootPath, requested);
        return requested;
    }

    private static string ResolveSafePath(string rootPath, string relative)
    {
        relative ??= string.Empty;
        string normalizedProjectRoot = NormalizeDirectoryRoot(rootPath);
        string normalizedToolsRoot = NormalizeDirectoryRoot(string.IsNullOrWhiteSpace(s_toolsRootPath) ? normalizedProjectRoot : s_toolsRootPath);

        if (Path.IsPathFullyQualified(relative))
        {
            string fullPath = Path.GetFullPath(relative);
            if (!IsPathAllowed(normalizedProjectRoot, fullPath))
                throw new ArgumentException("path escapes root: " + relative);
            return fullPath;
        }

        bool preferToolsRoot = IsToolsRelativePath(relative);
        string projectCandidate = Path.GetFullPath(Path.Combine(normalizedProjectRoot, relative));
        string toolsCandidate = Path.GetFullPath(Path.Combine(normalizedToolsRoot, relative));

        if (preferToolsRoot && IsPathAtOrUnderRoot(normalizedToolsRoot, toolsCandidate))
            return toolsCandidate;

        if (IsPathAtOrUnderRoot(normalizedProjectRoot, projectCandidate)
            && (File.Exists(projectCandidate) || Directory.Exists(projectCandidate) || !preferToolsRoot))
            return projectCandidate;

        if (IsPathAtOrUnderRoot(normalizedToolsRoot, toolsCandidate)
            && (File.Exists(toolsCandidate) || Directory.Exists(toolsCandidate) || preferToolsRoot))
            return toolsCandidate;

        if (IsPathAtOrUnderRoot(normalizedProjectRoot, projectCandidate))
            return projectCandidate;
        if (IsPathAtOrUnderRoot(normalizedToolsRoot, toolsCandidate))
            return toolsCandidate;

        throw new ArgumentException("path escapes root: " + relative);
    }

    private static bool IsToolsRelativePath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return false;
        string normalized = relative.Replace('\\', '/').TrimStart('/');
        return normalized.Equals("Tools", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectStaticPreviewPath(string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
            return false;

        string normalized = requestPath.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("ArtSource/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(
                "Assets/Art/WGame/Textures/Character/WGameColorPaletteAtlas.png",
                StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("FGUIProject/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryRoot(string root)
    {
        string normalized = Path.GetFullPath(string.IsNullOrWhiteSpace(root) ? Directory.GetCurrentDirectory() : root);
        string pathRoot = Path.GetPathRoot(normalized) ?? string.Empty;
        if (!string.Equals(normalized, pathRoot, StringComparison.OrdinalIgnoreCase))
            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized;
    }

    private static bool IsPathAllowed(string projectRootPath, string fullPath)
    {
        return IsPathAtOrUnderRoot(projectRootPath, fullPath)
            || (!string.IsNullOrWhiteSpace(s_toolsRootPath) && IsPathAtOrUnderRoot(s_toolsRootPath, fullPath));
    }

    private static bool IsPathAtOrUnderRoot(string rootPath, string fullPath)
    {
        string normalizedRoot = NormalizeDirectoryRoot(rootPath);
        string normalizedPath = Path.GetFullPath(fullPath);
        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;
        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAuthoringRelativePath(string rootPath, string path)
    {
        string fullPath = Path.GetFullPath(path);
        string projectRoot = NormalizeDirectoryRoot(rootPath);
        if (IsPathAtOrUnderRoot(projectRoot, fullPath))
            return Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');

        string toolsRoot = NormalizeDirectoryRoot(string.IsNullOrWhiteSpace(s_toolsRootPath) ? projectRoot : s_toolsRootPath);
        if (IsPathAtOrUnderRoot(toolsRoot, fullPath))
            return Path.GetRelativePath(toolsRoot, fullPath).Replace('\\', '/');

        return fullPath.Replace('\\', '/');
    }

    private static ModDiagnoseRequest ReadDiagnoseRequest(HttpListenerContext context, string rootPath)
    {
        var request = new ModDiagnoseRequest();

        string[] queryContainers = context.Request.QueryString.GetValues("container") ?? Array.Empty<string>();
        foreach (string value in queryContainers)
        {
            if (!string.IsNullOrWhiteSpace(value))
                request.containers.Add(value.Trim());
        }
        string containersCsv = context.Request.QueryString["containers"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(containersCsv))
        {
            foreach (string part in containersCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    request.containers.Add(trimmed);
            }
        }

        string queryLoadout = context.Request.QueryString["loadout"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(queryLoadout))
            request.loadout = queryLoadout.Trim();

        string includeAbsolute = context.Request.QueryString["includeAbsolutePaths"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(includeAbsolute) && bool.TryParse(includeAbsolute, out bool parsedInclude))
            request.includeAbsolutePaths = parsedInclude;

        if (context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            if (!string.IsNullOrWhiteSpace(body))
            {
                ModDiagnoseRequest post = JsonSerializer.Deserialize<ModDiagnoseRequest>(body) ?? new ModDiagnoseRequest();
                if (post.containers != null && post.containers.Count > 0)
                    request.containers = post.containers;
                if (!string.IsNullOrWhiteSpace(post.loadout))
                    request.loadout = post.loadout;
                request.includeAbsolutePaths = post.includeAbsolutePaths;
            }
        }

        var normalizedContainers = new List<string>();
        foreach (string container in request.containers)
        {
            if (string.IsNullOrWhiteSpace(container))
                continue;
            normalizedContainers.Add(NormalizePathInput(rootPath, container.Trim()));
        }
        request.containers = normalizedContainers;
        request.loadout = string.IsNullOrWhiteSpace(request.loadout) ? string.Empty : NormalizePathInput(rootPath, request.loadout.Trim());

        return request;
    }

    private static string NormalizePathInput(string rootPath, string path)
    {
        string fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(rootPath, path));
        if (!IsPathAllowed(rootPath, fullPath))
            throw new ArgumentException("path escapes root: " + path);
        return fullPath;
    }

    private static List<object> ListPackages(string rootPath)
    {
        var result = new List<object>();
        string samplesDir = ResolveSafePath(rootPath, SamplesRelativePath);
        if (!Directory.Exists(samplesDir)) return result;
        foreach (string dir in Directory.GetDirectories(samplesDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            string modJson = Path.Combine(dir, "mod.json");
            if (!File.Exists(modJson)) continue;
            try
            {
                ModPackageManifest m = JsonSerializer.Deserialize<ModPackageManifest>(File.ReadAllText(modJson), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }) ?? new ModPackageManifest();
                string relative = ToAuthoringRelativePath(rootPath, dir);
                result.Add(new { relative, packageId = m.PackageId, kind = m.Kind.ToString() });
            }
            catch
            {
            }
        }
        return result;
    }

    private static IEnumerable<string> EnumerateCharacterPackageDirectories(string rootPath)
    {
        string[] packageRootRelatives = IsExternalProjectRoot(rootPath)
            ? new[] { ProjectCharacterPackagesRelativePath }
            : new[] { ProjectCharacterPackagesRelativePath, SamplesRelativePath };

        foreach (string packageRootRelative in packageRootRelatives)
        {
            string packageRoot;
            try
            {
                packageRoot = ResolveSafePath(rootPath, packageRootRelative);
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(packageRoot))
                continue;

            foreach (string dir in Directory.GetDirectories(packageRoot).OrderBy(p => p, StringComparer.Ordinal))
                yield return dir;
        }
    }

    private static string GetWritableCharacterPackageRootRelative(string rootPath)
    {
        return ProjectCharacterPackagesRelativePath;
    }

    private static bool IsExternalProjectRoot(string rootPath)
    {
        string projectRoot = NormalizeDirectoryRoot(rootPath);
        string toolsRoot = NormalizeDirectoryRoot(string.IsNullOrWhiteSpace(s_toolsRootPath) ? projectRoot : s_toolsRootPath);
        return !string.Equals(projectRoot, toolsRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static List<object> ListCharacterPackages(string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        var result = new List<object>();
        AddCharacterPackageListItem(result, rootPath, defaultPackage, jsonOptions);

        foreach (string dir in EnumerateCharacterPackageDirectories(rootPath))
        {
            string relative = ToAuthoringRelativePath(rootPath, dir);
            AddCharacterPackageListItem(result, rootPath, relative, jsonOptions);
        }

        return result;
    }

    private static void AddCharacterPackageListItem(List<object> result, string rootPath, string relative, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return;
        string packagePath;
        try
        {
            packagePath = ResolveSafePath(rootPath, relative);
        }
        catch
        {
            return;
        }

        string manifestPath = Path.Combine(packagePath, "manifest.json");
        string resourceCatalogPath = Path.Combine(packagePath, "resource_catalog.json");
        if (!File.Exists(manifestPath) || !File.Exists(resourceCatalogPath))
            return;

        try
        {
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
            if (manifest.Kind != CharacterResourcePackageKind.Character)
                return;

            string normalized = ToAuthoringRelativePath(rootPath, packagePath);
            if (result.Any(item => string.Equals(GetAnonymousProperty(item, "relative"), normalized, StringComparison.Ordinal)))
                return;
            result.Add(new
            {
                relative = normalized,
                packageId = manifest.PackageId,
                displayName = manifest.DisplayName,
                stableId = manifest.StableId,
                version = manifest.Version,
                kind = manifest.Kind,
                isTemplate = IsProtectedCharacterTemplateRelative(normalized),
                canArchive = !IsProtectedCharacterTemplateRelative(normalized)
            });
        }
        catch
        {
        }
    }

    private static bool IsCharacterPackage(string rootPath, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return false;
        try
        {
            string packagePath = ResolveSafePath(rootPath, relative);
            return File.Exists(Path.Combine(packagePath, "manifest.json")) && File.Exists(Path.Combine(packagePath, "resource_catalog.json"));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsActiveCharacterPackageRelative(string relative)
    {
        string normalized = NormalizePackageRelativePath(relative);
        return normalized.StartsWith(SamplesRelativePath + "/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(ProjectCharacterPackagesRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtectedCharacterTemplateRelative(string relative)
    {
        string normalized = NormalizePackageRelativePath(relative);
        return string.Equals(normalized, ResolveCharacterTemplateRelative("humanoid"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ResolveCharacterTemplateRelative("creature"), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCharacterPackageArchiveRootRelative(string rootPath)
    {
        return IsExternalProjectRoot(rootPath)
            ? "Authoring/Archive/CharacterPackages"
            : "Tools/MxFramework.Authoring/archive/character-packages";
    }

    private static object ReadCharacterState(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = packagePath,
            ValidateResourceFiles = true,
            ValidateResourceHashes = false
        });

        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", package.Manifest.PackageId);
        string importReportPath = Path.Combine(generatedRoot, "package_cache", "import_report.json");
        string unityResourceCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string paletteBindingPath = Path.Combine(packagePath, "config", "character_palette_binding.json");
        string paletteUsagePath = Path.Combine(packagePath, "config", "character_palette_usage.json");

        return new
        {
            packageRelative,
            canWrite = true,
            revision = ComputeCharacterPackageRevision(packagePath),
            weaponLibraryRevision = ComputeGlobalWeaponLibraryRevision(rootPath),
            package,
            paletteBinding = File.Exists(paletteBindingPath) ? ReadOptionalJson(rootPath, paletteBindingPath) : null,
            bakedSurfaceBinding = CharacterBakedSurfaceAuthority.Exists(packagePath) ? ReadOptionalJson(rootPath, Path.Combine(packagePath, CharacterBakedSurfaceAuthority.RelativePath)) : null,
            bakedSurfaceIssues = CharacterBakedSurfaceAuthority.Exists(packagePath) ? CharacterBakedSurfaceAuthority.Validate(packagePath, package.Manifest.PackageId) : null,
            paletteUsage = File.Exists(paletteUsagePath) ? ReadOptionalJson(rootPath, paletteUsagePath) : null,
            validation,
            authoringSources = IsNumericCharacterId(package?.Manifest?.PackageId)
                ? ReadCharacterAuthoringSourceState(rootPath, packageRelative, jsonOptions)
                : null,
            clothMagica = ReadCharacterClothMagicaState(rootPath, packageRelative, package.Manifest.PackageId),
            importReport = File.Exists(importReportPath) ? ReadOptionalJson(rootPath, importReportPath) : null,
            unityResourceCatalogPath = ToProjectRelativePath(rootPath, unityResourceCatalogPath),
            unityResourceCatalog = File.Exists(unityResourceCatalogPath) ? ReadOptionalJson(rootPath, unityResourceCatalogPath) : null
        };
    }

    internal static object CreateCharacterPackage(string rootPath, CharacterStudioCreateRequest request, JsonSerializerOptions jsonOptions)
    {
        if (request == null)
            throw new ArgumentException("create request is required.");

        string templateRelative = ResolveCharacterTemplateRelative(request.template);
        CharacterIdCatalogLease idLease = null;
        string packageId = NormalizeCharacterPackageId(request.packageId);
        if (string.IsNullOrWhiteSpace(packageId))
        {
            idLease = AllocateCharacterId(rootPath, request.displayName, request.idCatalogRevision, jsonOptions);
            packageId = idLease.CharacterId;
        }
        return CreateCharacterPackageFromSource(
            rootPath,
            templateRelative,
            packageId,
            request.displayName,
            string.IsNullOrWhiteSpace(request.directoryName) ? "character-" + packageId : request.directoryName,
            jsonOptions,
            idLease,
            recordDerivedFrom: false);
    }

    internal static object DuplicateCharacterPackage(string rootPath, CharacterStudioDuplicateRequest request, JsonSerializerOptions jsonOptions)
    {
        if (request == null)
            throw new ArgumentException("duplicate request is required.");
        string sourcePackageRelative = NormalizePackageRelativePath(request.sourcePackage);
        if (!IsActiveCharacterPackageRelative(sourcePackageRelative) || !IsCharacterPackage(rootPath, sourcePackageRelative))
            throw new ArgumentException("sourcePackage must reference an active character package.");

        CharacterIdCatalogLease idLease = null;
        string packageId = NormalizeCharacterPackageId(request.packageId);
        if (string.IsNullOrWhiteSpace(packageId))
        {
            idLease = AllocateCharacterId(rootPath, request.displayName, request.idCatalogRevision, jsonOptions);
            packageId = idLease.CharacterId;
        }
        return CreateCharacterPackageFromSource(
            rootPath,
            sourcePackageRelative,
            packageId,
            request.displayName,
            string.IsNullOrWhiteSpace(request.directoryName) ? "character-" + packageId : request.directoryName,
            jsonOptions,
            idLease,
            recordDerivedFrom: true);
    }

    private static object CreateCharacterPackageFromSource(
        string rootPath,
        string sourcePackageRelative,
        string requestedPackageId,
        string requestedDisplayName,
        string requestedDirectoryName,
        JsonSerializerOptions jsonOptions,
        CharacterIdCatalogLease idLease = null,
        bool recordDerivedFrom = false)
    {
        string packageId = NormalizeCharacterPackageId(requestedPackageId);
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("packageId is required.");

        string displayName = string.IsNullOrWhiteSpace(requestedDisplayName)
            ? BuildDefaultCharacterDisplayName(packageId)
            : requestedDisplayName.Trim();
        string directoryName = NormalizeCharacterPackageDirectoryName(requestedDirectoryName, packageId);
        string packageRelative = GetWritableCharacterPackageRootRelative(rootPath) + "/" + directoryName;
        string sourcePackagePath = ResolveSafePath(rootPath, sourcePackageRelative);
        string packagePath = ResolveSafePath(rootPath, packageRelative);

        if (!Directory.Exists(sourcePackagePath))
            throw new DirectoryNotFoundException("Source character package directory was not found: " + sourcePackageRelative);
        string conflictPackageRelative = FindCharacterPackageByPackageId(rootPath, packageId, jsonOptions);
        if (!string.IsNullOrWhiteSpace(conflictPackageRelative))
        {
            throw new CharacterPackageCreateConflictException(
                "CHARACTER_PACKAGE_ID_CONFLICT",
                "Character packageId already exists: " + packageId,
                conflictPackageRelative);
        }
        if (Directory.Exists(packagePath))
        {
            throw new CharacterPackageCreateConflictException(
                "CHARACTER_PACKAGE_DIRECTORY_CONFLICT",
                "Character package directory already exists: " + packageRelative,
                packageRelative);
        }

        string sourcePackageId = GetTemplatePackageId(sourcePackagePath, jsonOptions);
        string sourceArtRelative = JoinImportMaterialPath(CharacterSourceRootRelativePrefix, NormalizeCharacterPackageId(sourcePackageId));
        string sourceRelative = JoinImportMaterialPath(CharacterSourceRootRelativePrefix, packageId);
        string sourceArtPath = ResolveSafePath(rootPath, sourceArtRelative);
        string sourcePath = ResolveSafePath(rootPath, sourceRelative);
        if (Directory.Exists(sourcePath))
        {
            throw new CharacterPackageCreateConflictException(
                "CHARACTER_SOURCE_ROOT_CONFLICT",
                "Character ArtSource directory already exists: " + sourceRelative,
                string.Empty);
        }

        CharacterResourcePackage sourceCharacterPackage =
            CharacterPackageCommands.ReadPackage(sourcePackagePath, jsonOptions);
        int[] sourceCharacterActionIds = sourceCharacterPackage.CharacterActionLibrary.Actions
            .Select(action => ReadInt32(action, "actionId"))
            .ToArray();
        string characterActionLibraryId = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
        CharacterActionIdCatalogLease characterActionRangeLease = ReserveCharacterActionIdRange(
            rootPath,
            characterActionLibraryId,
            sourceCharacterActionIds,
            packageRelative + "/config/character_actions.json");
        try
        {
            CopyDirectoryRecursive(sourcePackagePath, packagePath);
            if (Directory.Exists(sourceArtPath))
                CopyDirectoryRecursive(sourceArtPath, sourcePath);
            else
                Directory.CreateDirectory(sourcePath);

            string sourceDisplayName = GetTemplateDisplayName(sourcePackagePath, jsonOptions);
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
            CharacterResourcePackageMigrationPlan createMigration = CharacterResourcePackageMigrator.CreatePlan(package);
            if (createMigration.RequiresMigration)
                CharacterResourcePackageMigrator.Apply(package);
            RewriteCharacterTemplatePackage(package, packageId, displayName, jsonOptions);
            RemapClonedCharacterActionIds(
                package,
                characterActionRangeLease.ReassignedActionIds,
                jsonOptions);
            SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);

            string sourceManifestRelative = GetCharacterSourceManifestRelative(package, packageRelative);
            RewriteCharacterTemplateSourceManifest(
                rootPath,
                sourceManifestRelative,
                sourcePackageId,
                packageId,
                jsonOptions);
            ResetCharacterTemplateImportState(
                rootPath,
                packageRelative,
                sourceManifestRelative,
                jsonOptions);

            string animationDocumentPath = Path.Combine(packagePath, AnimationAuthoringDocumentRelativePath);
            if (File.Exists(animationDocumentPath))
            {
                AnimationAuthoringPackage animation = ReadAnimationPackage(rootPath, animationDocumentPath, jsonOptions);
                animation = RewriteAnimationTemplatePackage(animation, oldPackageId: sourcePackageId, oldDisplayName: sourceDisplayName, newPackageId: packageId, newDisplayName: displayName, package, jsonOptions);
                WriteJsonFileAtomic(animationDocumentPath, animation, jsonOptions);
            }

            string audioCueManifestPath = Path.Combine(packagePath, "config", "audio_cue_manifest.json");
            if (File.Exists(audioCueManifestPath))
            {
                AudioPackageCueSubsetDocument audioCueManifest =
                    ReadOptionalJsonFile<AudioPackageCueSubsetDocument>(rootPath, audioCueManifestPath, jsonOptions)
                    ?? new AudioPackageCueSubsetDocument();
                audioCueManifest.PackageId = packageId;
                WriteJsonFileAtomic(audioCueManifestPath, audioCueManifest, jsonOptions);
            }

            string importMaterialsDocumentPath = Path.Combine(packagePath, ImportMaterialsDocumentRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(importMaterialsDocumentPath))
            {
                CharacterImportMaterialsDocument importMaterials = ReadOptionalJsonFile<CharacterImportMaterialsDocument>(rootPath, importMaterialsDocumentPath, jsonOptions) ?? new CharacterImportMaterialsDocument();
                RewriteCharacterTemplateImportMaterials(importMaterials, sourcePackageId, packageId);
                WriteJsonFileAtomic(importMaterialsDocumentPath, importMaterials, jsonOptions);
            }

            EnsureNumericCharacterAuthoringScaffold(
                rootPath,
                sourcePath,
                packageId,
                displayName,
                recordDerivedFrom && !string.IsNullOrWhiteSpace(sourcePackageId) ? sourcePackageId : string.Empty,
                jsonOptions);
            idLease?.Commit(packageRelative, sourceRelative);
        }
        catch
        {
            try
            {
                Directory.Delete(packagePath, recursive: true);
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(sourcePath))
                    Directory.Delete(sourcePath, recursive: true);
            }
            catch
            {
            }

            try
            {
                characterActionRangeLease.Rollback();
            }
            catch
            {
            }

            throw;
        }

        return new
        {
            allocatedCharacterId = packageId,
            packageRelative,
            artSourceRelative = sourceRelative,
            sourceManifestRelative = JoinImportMaterialPath(sourceRelative, CharacterSourceManifestFileName),
            state = ReadCharacterState(rootPath, packageRelative, jsonOptions)
        };
    }

    internal static object ArchiveCharacterPackage(string rootPath, CharacterStudioArchiveRequest request, JsonSerializerOptions jsonOptions)
    {
        if (request == null)
            throw new ArgumentException("archive request is required.");
        string packageRelative = NormalizePackageRelativePath(request.package);
        if (!IsActiveCharacterPackageRelative(packageRelative) || !IsCharacterPackage(rootPath, packageRelative))
            throw new ArgumentException("package must reference an active character package.");
        if (IsProtectedCharacterTemplateRelative(packageRelative))
        {
            throw new CharacterPackageManagementConflictException(
                "CHARACTER_PACKAGE_PROTECTED",
                "Built-in character templates cannot be archived.",
                packageRelative);
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = NormalizeCharacterPackageId(package?.Manifest?.PackageId);
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Character package manifest is missing packageId.");
        if (!string.Equals(NormalizeCharacterPackageId(request.confirmPackageId), packageId, StringComparison.Ordinal))
            throw new ArgumentException("confirmPackageId must match the active character packageId.");

        string archiveStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string packageArchiveRootRelative = GetCharacterPackageArchiveRootRelative(rootPath);
        string sourceRelative = JoinImportMaterialPath(CharacterSourceRootRelativePrefix, packageId);
        string sourceArchiveRootRelative = "ArtSource/Archive/Characters";
        bool numericCharacter = IsNumericCharacterId(packageId);
        string packageArchiveRelative = JoinImportMaterialPath(
            packageArchiveRootRelative,
            numericCharacter ? "character-" + packageId : Path.GetFileName(packagePath) + "--" + archiveStamp);
        string sourceArchiveRelative = JoinImportMaterialPath(
            sourceArchiveRootRelative,
            numericCharacter ? packageId : packageId + "--" + archiveStamp);
        string sourcePath = ResolveSafePath(rootPath, sourceRelative);
        string packageArchivePath = ResolveSafePath(rootPath, packageArchiveRelative);
        string sourceArchivePath = ResolveSafePath(rootPath, sourceArchiveRelative);

        Directory.CreateDirectory(Path.GetDirectoryName(packageArchivePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceArchivePath)!);
        bool packageMoved = false;
        bool sourceMoved = false;
        try
        {
            Directory.Move(packagePath, packageArchivePath);
            packageMoved = true;
            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, sourceArchivePath);
                sourceMoved = true;
            }
            if (numericCharacter)
                RetireCharacterId(rootPath, packageId, jsonOptions);
        }
        catch
        {
            if (sourceMoved && Directory.Exists(sourceArchivePath) && !Directory.Exists(sourcePath))
                Directory.Move(sourceArchivePath, sourcePath);
            if (packageMoved && Directory.Exists(packageArchivePath) && !Directory.Exists(packagePath))
                Directory.Move(packageArchivePath, packagePath);
            throw;
        }

        InvalidateAuthoringResourceCache(rootPath, packageRelative);
        return new
        {
            success = true,
            packageId,
            packageArchiveRelative,
            sourceArchiveRelative = sourceMoved ? sourceArchiveRelative : string.Empty,
            generatedOutputsRetained = new[]
            {
                "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId,
                "Assets/MxFrameworkGenerated/CharacterActions/Profiles/" + packageId,
                "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/" + packageId
            }
        };
    }

    private static string FindCharacterPackageByPackageId(
        string rootPath,
        string packageId,
        JsonSerializerOptions jsonOptions)
    {
        string normalizedPackageId = NormalizeCharacterPackageId(packageId);
        if (string.IsNullOrWhiteSpace(normalizedPackageId))
            return string.Empty;

        string[] packageRoots = { SamplesRelativePath, ProjectCharacterPackagesRelativePath };
        for (int rootIndex = 0; rootIndex < packageRoots.Length; rootIndex++)
        {
            string packageRoot;
            try
            {
                packageRoot = ResolveSafePath(rootPath, packageRoots[rootIndex]);
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(packageRoot))
                continue;

            foreach (string directory in Directory.GetDirectories(packageRoot).OrderBy(static path => path, StringComparer.Ordinal))
            {
                string manifestPath = Path.Combine(directory, "manifest.json");
                if (!File.Exists(manifestPath))
                    continue;

                try
                {
                    CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions);
                    if (manifest?.Kind != CharacterResourcePackageKind.Character)
                        continue;
                    if (string.Equals(NormalizeCharacterPackageId(manifest.PackageId), normalizedPackageId, StringComparison.Ordinal))
                        return ToAuthoringRelativePath(rootPath, directory);
                }
                catch
                {
                }
            }
        }

        return string.Empty;
    }

    private static void RewriteCharacterTemplateSourceManifest(
        string rootPath,
        string sourceManifestRelative,
        string oldPackageId,
        string newPackageId,
        JsonSerializerOptions jsonOptions)
    {
        string sourceManifestPath = ResolveSafePath(rootPath, sourceManifestRelative);
        CharacterSourceManifestDocument manifest = File.Exists(sourceManifestPath)
            ? JsonSerializer.Deserialize<CharacterSourceManifestDocument>(File.ReadAllText(sourceManifestPath), jsonOptions)
                ?? new CharacterSourceManifestDocument()
            : new CharacterSourceManifestDocument();

        manifest.CharacterId = newPackageId;
        manifest.PackageId = newPackageId;
        manifest.SourceRoot = ".";
        manifest.Items ??= new List<CharacterSourceManifestItem>();

        string oldResourcePrefix = "char." + oldPackageId + ".";
        string newResourcePrefix = "char." + newPackageId + ".";
        string oldStablePrefix = "charpkg." + oldPackageId + ".";
        string newStablePrefix = "charpkg." + newPackageId + ".";
        for (int i = 0; i < manifest.Items.Count; i++)
        {
            CharacterSourceManifestItem item = manifest.Items[i];
            if (item == null)
                continue;

            item.Target ??= new CharacterSourceTarget();
            item.Target.ResourceKey = ReplaceCharacterPackagePrefix(item.Target.ResourceKey, oldResourcePrefix, newResourcePrefix);
            item.Source ??= new CharacterSourceReference();
            if (item.Source.Selection != null)
            {
                item.Source.Selection.PackageResourceKey = ReplaceCharacterPackagePrefix(
                    item.Source.Selection.PackageResourceKey,
                    oldResourcePrefix,
                    newResourcePrefix);
                item.Source.Selection.ResourceStableId = ReplaceCharacterPackagePrefix(
                    item.Source.Selection.ResourceStableId,
                    oldStablePrefix,
                    newStablePrefix);
            }
        }

        WriteJsonFileAtomic(sourceManifestPath, manifest, jsonOptions);
    }

    private static void ResetCharacterTemplateImportState(
        string rootPath,
        string packageRelative,
        string sourceManifestRelative,
        JsonSerializerOptions jsonOptions)
    {
        string sourceManifestPath = ResolveSafePath(rootPath, sourceManifestRelative);
        CharacterSourceManifestDocument manifest = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(File.ReadAllText(sourceManifestPath), jsonOptions)
            ?? new CharacterSourceManifestDocument();
        var state = new CharacterImportMaterialsStateDocument
        {
            SourceManifestRelative = sourceManifestRelative
        };

        manifest.Items ??= new List<CharacterSourceManifestItem>();
        for (int i = 0; i < manifest.Items.Count; i++)
        {
            CharacterSourceManifestItem item = manifest.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;
            state.Items.Add(new CharacterImportMaterialItemState
            {
                Id = item.Id,
                ResourceKey = item.Target?.ResourceKey ?? string.Empty
            });
        }

        string statePath = ResolveSafePath(rootPath, JoinImportMaterialPath(packageRelative, ImportMaterialsStateRelativePath));
        WriteJsonFileAtomic(statePath, state, jsonOptions);
    }

    private static string ReplaceCharacterPackagePrefix(string value, string oldPrefix, string newPrefix)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.IsNullOrWhiteSpace(oldPrefix)
            || !value.StartsWith(oldPrefix, StringComparison.Ordinal))
            return value ?? string.Empty;
        return newPrefix + value.Substring(oldPrefix.Length);
    }

    internal static void SaveCharacterPackage(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions,
        string expectedRevision = "",
        bool force = false)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packageRelative);

        EnsureCharacterPackageRevision(packagePath, expectedRevision, force);
        package.GlobalWeaponLibrary = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        if (package.BaseAttributes != null && !package.Manifest.ConfigFiles.Contains(CharacterBaseAttributesAuthoring.RelativePath))
            package.Manifest.ConfigFiles.Add(CharacterBaseAttributesAuthoring.RelativePath);
        if (package.TacticalDefaults != null)
        {
            if (!package.TacticalDefaults.TryValidate(out string tacticalIssue))
                throw new InvalidOperationException(tacticalIssue);
            if (!package.Manifest.ConfigFiles.Contains(MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath))
                package.Manifest.ConfigFiles.Add(MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath);
        }

        CharacterResourcePackageMigrationPlan migrationPlan =
            CharacterResourcePackageMigrator.CreatePlan(package);
        if (!migrationPlan.CanMigrate)
        {
            throw new ArgumentException(
                "Character package uses an unsupported schema: "
                + string.Join("; ", migrationPlan.BlockingReasons));
        }

        RefreshCharacterResourceHashes(packagePath, package);

        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = packagePath,
            ValidateResourceFiles = true,
            ValidateResourceHashes = false
        });
        if (HasNonDraftExportBlocker(validation))
            throw new CharacterPackageValidationException(validation);

        WriteJsonFileAtomic(Path.Combine(packagePath, "manifest.json"), package.Manifest, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "resource_catalog.json"), package.ResourceCatalog, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "config", "character_application.json"), package.ApplicationConfig, jsonOptions);
        if (package.BaseAttributes != null)
            WriteJsonFileAtomic(Path.Combine(packagePath, CharacterBaseAttributesAuthoring.RelativePath), package.BaseAttributes, jsonOptions);
        if (package.TacticalDefaults != null)
            WriteJsonFileAtomic(Path.Combine(packagePath, MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath), package.TacticalDefaults, jsonOptions);
        WriteJsonFileAtomic(
            Path.Combine(packagePath, "config", "character_actions.json"),
            package.CharacterActionLibrary ?? new CharacterActionSourceLibrary(),
            jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_geometry.json"), package.Geometry.BodyProfile, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_parts.json"), new CharacterBodyPartsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            bodyParts = package.Geometry.BodyParts
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_colliders.json"), new CharacterBodyCollidersFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            colliders = package.Geometry.Colliders
        }, jsonOptions);
        if (package.Geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current
            && package.Geometry.DamageZones != null)
        {
            WriteJsonFileAtomic(
                Path.Combine(packagePath, "geometry", "damage_zones.json"),
                CreateCanonicalDamageZonesDocument(package.Geometry.DamageZones),
                jsonOptions);
        }
        if (package.Geometry.EngagementAnchorsDocumentState
                == CharacterEngagementAnchorsDocumentState.Current
            && package.Geometry.EngagementAnchors != null)
        {
            WriteJsonFileAtomic(
                Path.Combine(packagePath, "geometry", "engagement_anchors.json"),
                CreateCanonicalEngagementAnchorsDocument(package.Geometry.EngagementAnchors),
                jsonOptions);
        }
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "sockets.json"), new CharacterSocketsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            sockets = package.Geometry.Sockets
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "weapon_slots.json"), new CharacterWeaponSlotsFile
        {
            schemaVersion = package.Geometry.WeaponSlotsSchemaVersion,
            weaponSlots = package.Geometry.WeaponSlots
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_part_contact_points.json"), new CharacterBodyPartContactPointsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            contactPoints = package.Geometry.BodyPartContactPoints
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "validation", "last_report.json"), validation, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath, packageRelative);
    }

    private static CharacterDamageZonesDocument CreateCanonicalDamageZonesDocument(
        CharacterDamageZonesDocument source)
    {
        var result = new CharacterDamageZonesDocument
        {
            Format = source?.Format ?? CharacterDamageZonesDocument.CurrentFormat,
            SchemaVersion = source?.SchemaVersion ?? CharacterDamageZonesDocument.CurrentSchemaVersion
        };
        var zones = new List<CharacterDamageZoneAuthoring>(
            source?.Zones ?? new List<CharacterDamageZoneAuthoring>());
        zones.Sort((left, right) => string.CompareOrdinal(left?.ZoneId, right?.ZoneId));
        for (int i = 0; i < zones.Count; i++)
        {
            CharacterDamageZoneAuthoring sourceZone = zones[i];
            if (sourceZone == null)
                continue;
            var members = new List<string>(sourceZone.HitZoneIds ?? new List<string>());
            members.Sort(StringComparer.Ordinal);
            result.Zones.Add(new CharacterDamageZoneAuthoring
            {
                ZoneId = sourceZone.ZoneId,
                DisplayName = string.IsNullOrWhiteSpace(sourceZone.DisplayName)
                    ? sourceZone.ZoneId
                    : sourceZone.DisplayName,
                HitZoneIds = members,
                Hittable = sourceZone.Hittable,
                Aimable = sourceZone.Aimable,
                Approachable = sourceZone.Approachable,
                AuthoredPriority = sourceZone.AuthoredPriority
            });
        }
        return result;
    }

    private static CharacterEngagementAnchorsDocument CreateCanonicalEngagementAnchorsDocument(
        CharacterEngagementAnchorsDocument source)
    {
        var result = new CharacterEngagementAnchorsDocument
        {
            Format = source?.Format ?? CharacterEngagementAnchorsDocument.CurrentFormat,
            SchemaVersion = source?.SchemaVersion
                ?? CharacterEngagementAnchorsDocument.CurrentSchemaVersion,
            Revision = source?.Revision ?? 1
        };
        var anchors = new List<CharacterEngagementAnchorAuthoring>(
            source?.Anchors ?? new List<CharacterEngagementAnchorAuthoring>());
        anchors.Sort((left, right) => string.CompareOrdinal(left?.AnchorId, right?.AnchorId));
        for (int i = 0; i < anchors.Count; i++)
        {
            CharacterEngagementAnchorAuthoring anchor = anchors[i];
            if (anchor == null)
                continue;
            CharacterAuthoringIntVector3 position =
                anchor.LocalPositionMillimeters ?? new CharacterAuthoringIntVector3();
            result.Anchors.Add(new CharacterEngagementAnchorAuthoring
            {
                AnchorId = anchor.AnchorId,
                ZoneId = anchor.ZoneId,
                ReferenceFrame = anchor.ReferenceFrame,
                LocalPositionMillimeters = new CharacterAuthoringIntVector3(
                    position.X,
                    position.Y,
                    position.Z),
                YawMilliDegrees = anchor.YawMilliDegrees,
                Capacity = anchor.Capacity,
                AuthoredPriority = anchor.AuthoredPriority,
                Enabled = anchor.Enabled,
                PhaseId = anchor.PhaseId ?? string.Empty
            });
        }
        return result;
    }

    internal static void SaveCharacterStudioPackage(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions,
        string expectedRevision = "",
        bool force = false)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage persisted = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.CharacterActionLibrary = persisted.CharacterActionLibrary;
        package.CharacterActionLibraryPath = persisted.CharacterActionLibraryPath;
        package.CommonCharacterActionLibrary = persisted.CommonCharacterActionLibrary;
        package.CommonCharacterActionLibraryPath = persisted.CommonCharacterActionLibraryPath;
        PreservePersistedBodyPresence(persisted, package);
        PreservePersistedBodyPartDamage(persisted, package);

        SaveCharacterPackage(
            rootPath,
            packageRelative,
            package,
            jsonOptions,
            expectedRevision,
            force);
    }

    internal static object ReadCharacterMigrationPlan(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.GlobalWeaponLibrary = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        return new
        {
            packageRelative,
            revision = ComputeCharacterPackageRevision(packagePath),
            weaponLibraryRevision = ComputeGlobalWeaponLibraryRevision(rootPath),
            plan = CharacterResourcePackageMigrator.CreatePlan(package)
        };
    }

    internal static object MigrateCharacterPackage(
        string rootPath,
        string packageRelative,
        string expectedRevision,
        bool force,
        JsonSerializerOptions jsonOptions)
    {
        return MigrateCharacterPackage(
            rootPath,
            packageRelative,
            expectedRevision,
            ComputeGlobalWeaponLibraryRevision(rootPath),
            force,
            jsonOptions);
    }

    internal static object MigrateCharacterPackage(
        string rootPath,
        string packageRelative,
        string expectedRevision,
        string expectedWeaponLibraryRevision,
        bool force,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        package.GlobalWeaponLibrary = ReadGlobalWeaponLibraryAtRoot(rootPath, jsonOptions);
        CharacterResourcePackageMigrationPlan plan = CharacterResourcePackageMigrator.CreatePlan(package);
        if (!plan.CanMigrate)
            throw new ArgumentException("Character package migration is blocked: " + string.Join("; ", plan.BlockingReasons));

        if (plan.RequiresMigration)
        {
            EnsureCharacterPackageRevision(packagePath, expectedRevision, force);
            EnsureGlobalWeaponLibraryRevision(rootPath, expectedWeaponLibraryRevision, force);
            string globalPath = GetGlobalWeaponLibraryPath(rootPath);
            bool globalExisted = File.Exists(globalPath);
            string globalBackup = globalExisted ? File.ReadAllText(globalPath) : string.Empty;
            CharacterResourcePackageMigrator.Apply(package);
            string backupRoot = CreateCharacterPackageMigrationBackup(packagePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(globalPath) ?? rootPath);
                WriteJsonFileAtomic(globalPath, package.GlobalWeaponLibrary, jsonOptions);
                SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions, expectedRevision, force);
            }
            catch (CharacterPackageWriteConflictException)
            {
                RestoreGlobalWeaponLibraryMigrationBackup(globalPath, globalExisted, globalBackup);
                throw;
            }
            catch
            {
                RestoreCharacterPackageMigrationBackup(packagePath, backupRoot);
                RestoreGlobalWeaponLibraryMigrationBackup(globalPath, globalExisted, globalBackup);
                throw;
            }
            finally
            {
                if (Directory.Exists(backupRoot))
                    Directory.Delete(backupRoot, recursive: true);
            }
        }

        return new
        {
            success = true,
            migrated = plan.RequiresMigration,
            appliedSteps = plan.Steps,
            state = ReadCharacterState(rootPath, packageRelative, jsonOptions)
        };
    }

    private static void EnsureGlobalWeaponLibraryRevision(
        string rootPath,
        string expectedRevision,
        bool force)
    {
        if (force || string.IsNullOrWhiteSpace(expectedRevision))
            return;
        string currentRevision = ComputeGlobalWeaponLibraryRevision(rootPath);
        if (!string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
            throw new GlobalWeaponLibraryWriteConflictException(
                expectedRevision,
                currentRevision,
                "The global weapon library changed on disk after the migration plan was loaded.");
    }

    private static void RestoreGlobalWeaponLibraryMigrationBackup(
        string path,
        bool existed,
        string content)
    {
        if (existed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, content ?? string.Empty);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    internal static string ComputeCharacterPackageRevision(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
            return string.Empty;

        var builder = new StringBuilder(CharacterPackageRevisionRelativeFiles.Length * 96);
        for (int i = 0; i < CharacterPackageRevisionRelativeFiles.Length; i++)
        {
            string relative = CharacterPackageRevisionRelativeFiles[i];
            string path = Path.Combine(packagePath, relative.Replace('/', Path.DirectorySeparatorChar));
            builder.Append(relative)
                .Append('\n')
                .Append(File.Exists(path)
                    ? CharacterPackageHashUtility.ComputeFileSha256(path)
                    : "missing")
                .Append('\n');
        }

        return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
    }

    private static string CreateCharacterPackageMigrationBackup(string packagePath)
    {
        string backupRoot = Path.Combine(
            Path.GetTempPath(),
            "mx-character-package-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupRoot);
        for (int i = 0; i < CharacterPackageRevisionRelativeFiles.Length; i++)
        {
            string relative = CharacterPackageRevisionRelativeFiles[i];
            string source = Path.Combine(packagePath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
                continue;
            string target = Path.Combine(backupRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? backupRoot);
            File.Copy(source, target, overwrite: true);
        }
        return backupRoot;
    }

    private static void RestoreCharacterPackageMigrationBackup(string packagePath, string backupRoot)
    {
        for (int i = 0; i < CharacterPackageRevisionRelativeFiles.Length; i++)
        {
            string relative = CharacterPackageRevisionRelativeFiles[i];
            string source = Path.Combine(backupRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            string target = Path.Combine(packagePath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? packagePath);
                File.Copy(source, target, overwrite: true);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
    }

    private static void EnsureCharacterPackageRevision(
        string packagePath,
        string expectedRevision,
        bool force)
    {
        if (force || string.IsNullOrWhiteSpace(expectedRevision))
            return;

        string currentRevision = ComputeCharacterPackageRevision(packagePath);
        if (!string.Equals(expectedRevision, currentRevision, StringComparison.Ordinal))
        {
            throw new CharacterPackageWriteConflictException(
                expectedRevision,
                currentRevision,
                "Character package changed on disk after it was loaded. Reload it or explicitly force the save.");
        }
    }

    private static void WriteCharacterPackageWriteConflict(
        HttpListenerResponse response,
        CharacterPackageWriteConflictException exception,
        JsonSerializerOptions jsonOptions)
    {
        WriteJson(response, new
        {
            success = false,
            code = CharacterPackageWriteConflictException.ConflictCode,
            message = exception.Message,
            expectedRevision = exception.ExpectedRevision,
            currentRevision = exception.CurrentRevision,
            resolutions = new[] { "reload", "force" }
        }, jsonOptions, 409);
    }

    private static void RewriteCharacterTemplatePackage(
        CharacterResourcePackage package,
        string packageId,
        string displayName,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null)
            throw new ArgumentException("character package template is required.");

        package.Manifest ??= new CharacterPackageManifest();
        package.ResourceCatalog ??= new CharacterPackageResourceCatalog();
        package.Geometry ??= new CharacterAuthoringGeometry();
        package.ApplicationConfig ??= new CharacterApplicationAuthoringSummary();

        string oldPackageId = package.Manifest.PackageId ?? string.Empty;
        string oldDisplayName = package.Manifest.DisplayName ?? string.Empty;
        string publicDefaultCombinedWeaponConfigurationId =
            package.ApplicationConfig.DefaultCombinedWeaponConfigurationId ?? string.Empty;
        (string CombinedWeaponConfigurationId,
            string AnimationConfigurationId,
            string AbilityConfigurationId,
            string ReactionConfigurationId)[] publicCombinedWeaponReferences =
            (package.ApplicationConfig.CombinedWeaponConfigurationBindings
                ?? new List<CharacterCombinedWeaponConfigurationBinding>())
            .Select(static binding =>
            {
                CharacterCombinedWeaponConfigurationOverrides overrides = binding?.Overrides;
                return (
                    binding?.CombinedWeaponConfigurationId ?? string.Empty,
                    overrides?.AnimationConfigurationId ?? string.Empty,
                    overrides?.AbilityConfigurationId ?? string.Empty,
                    overrides?.ReactionConfigurationId ?? string.Empty);
            })
            .ToArray();

        var resourceKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var stableIdMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
        {
            if (entry == null)
                continue;

            string oldResourceKey = entry.ResourceKey ?? string.Empty;
            string oldStableId = entry.StableId ?? string.Empty;
            string newResourceKey = BuildCharacterResourceKey(oldResourceKey, oldPackageId, packageId, entry.TypeId, entry.LocalId, entry.Variant);
            string newStableId = BuildCharacterResourceStableId(packageId, entry.LocalId);
            if (!string.IsNullOrWhiteSpace(oldResourceKey))
                resourceKeyMap[oldResourceKey] = newResourceKey;
            if (!string.IsNullOrWhiteSpace(oldStableId))
                stableIdMap[oldStableId] = newStableId;

            entry.PackageId = packageId;
            entry.ResourceKey = newResourceKey;
            entry.StableId = newStableId;
        }

        foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
        {
            if (entry == null)
                continue;

            for (int dependencyIndex = 0; dependencyIndex < entry.Dependencies.Count; dependencyIndex++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[dependencyIndex];
                if (dependency == null)
                    continue;
                dependency.ResourceKey = ReplaceMappedValue(dependency.ResourceKey, resourceKeyMap);
            }

            entry.ImportHints ??= new CharacterPackageImportHint();
            for (int labelIndex = 0; labelIndex < entry.ImportHints.Labels.Count; labelIndex++)
            {
                string label = entry.ImportHints.Labels[labelIndex] ?? string.Empty;
                if (string.Equals(label, "package." + oldPackageId, StringComparison.Ordinal))
                    entry.ImportHints.Labels[labelIndex] = "package." + packageId;
            }

            entry.Preview ??= new CharacterPackagePreviewMetadata();
            entry.Preview.ThumbnailResourceKey = ReplaceMappedValue(entry.Preview.ThumbnailResourceKey, resourceKeyMap);
            entry.Preview.PreviewMeshResourceKey = ReplaceMappedValue(entry.Preview.PreviewMeshResourceKey, resourceKeyMap);
            entry.Preview.PlaceholderResourceKey = ReplaceMappedValue(entry.Preview.PlaceholderResourceKey, resourceKeyMap);

            entry.ResourceSelection = RewriteSelection(entry.ResourceSelection, resourceKeyMap, stableIdMap);
        }

        package.Manifest.PackageId = packageId;
        package.Manifest.StableId = "charpkg." + packageId;
        package.Manifest.DisplayName = displayName;
        package.Manifest.Kind = CharacterResourcePackageKind.Character;
        package.Manifest.ConfigFiles ??= new List<string>();
        if (!package.Manifest.ConfigFiles.Contains("config/character_actions.json", StringComparer.Ordinal))
            package.Manifest.ConfigFiles.Add("config/character_actions.json");

        var replacementMap = new Dictionary<string, string>(StringComparer.Ordinal);
        AddReplacement(replacementMap, oldPackageId, packageId);
        AddReplacement(replacementMap, oldDisplayName, displayName);
        AddReplacement(replacementMap, "charpkg." + oldPackageId, "charpkg." + packageId);
        AddReplacement(replacementMap, "char." + oldPackageId, "char." + packageId);
        AddReplacement(replacementMap, "body." + oldPackageId, "body." + packageId);
        AddReplacement(replacementMap, "attr." + oldPackageId, "attr." + packageId);
        AddReplacement(replacementMap, "equip_loadout." + oldPackageId, "equip_loadout." + packageId);
        AddReplacement(replacementMap, "geometry." + oldPackageId, "geometry." + packageId);
        AddReplacement(replacementMap, "animation." + oldPackageId, "animation." + packageId);
        AddReplacement(replacementMap, "skeleton." + oldPackageId, "skeleton." + packageId);
        AddReplacement(replacementMap, "avatar." + oldPackageId, "avatar." + packageId);

        foreach (KeyValuePair<string, string> pair in resourceKeyMap)
            AddReplacement(replacementMap, pair.Key, pair.Value);
        foreach (KeyValuePair<string, string> pair in stableIdMap)
            AddReplacement(replacementMap, pair.Key, pair.Value);

        package.ApplicationConfig = RewriteJsonObject(package.ApplicationConfig, jsonOptions, replacementMap);
        package.ApplicationConfig.DefaultCombinedWeaponConfigurationId =
            publicDefaultCombinedWeaponConfigurationId;
        for (int bindingIndex = 0;
            bindingIndex < package.ApplicationConfig.CombinedWeaponConfigurationBindings.Count
                && bindingIndex < publicCombinedWeaponReferences.Length;
            bindingIndex++)
        {
            CharacterCombinedWeaponConfigurationBinding binding =
                package.ApplicationConfig.CombinedWeaponConfigurationBindings[bindingIndex];
            if (binding == null)
                continue;
            binding.CombinedWeaponConfigurationId =
                publicCombinedWeaponReferences[bindingIndex].CombinedWeaponConfigurationId;
            binding.Overrides ??= new CharacterCombinedWeaponConfigurationOverrides();
            binding.Overrides.AnimationConfigurationId =
                publicCombinedWeaponReferences[bindingIndex].AnimationConfigurationId;
            binding.Overrides.AbilityConfigurationId =
                publicCombinedWeaponReferences[bindingIndex].AbilityConfigurationId;
            binding.Overrides.ReactionConfigurationId =
                publicCombinedWeaponReferences[bindingIndex].ReactionConfigurationId;
        }
        package.CharacterActionLibrary = RewriteJsonObject(
            package.CharacterActionLibrary ?? new CharacterActionSourceLibrary(),
            jsonOptions,
            replacementMap);
        package.Geometry = RewriteJsonObject(package.Geometry, jsonOptions, replacementMap);
        package.ApplicationConfig.ResourceKeys = package.ResourceCatalog.Entries
            .Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
            .Select(static entry => entry.ResourceKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (WeaponAttachmentProfile attachment in package.Geometry.WeaponAttachments)
        {
            if (attachment == null)
                continue;
            attachment.PreviewResourceKey = ReplaceMappedValue(attachment.PreviewResourceKey, resourceKeyMap);
            attachment.PreviewResourceSelection = RewriteSelection(attachment.PreviewResourceSelection, resourceKeyMap, stableIdMap);
        }
    }

    private static void RemapClonedCharacterActionIds(
        CharacterResourcePackage package,
        IReadOnlyDictionary<int, int> actionIdMap,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null || actionIdMap == null || actionIdMap.Count == 0)
            return;

        package.ApplicationConfig = RemapCharacterActionIds(
            package.ApplicationConfig,
            actionIdMap,
            jsonOptions);
        package.CharacterActionLibrary = RemapCharacterActionIds(
            package.CharacterActionLibrary,
            actionIdMap,
            jsonOptions);
    }

    private static T RemapCharacterActionIds<T>(
        T source,
        IReadOnlyDictionary<int, int> actionIdMap,
        JsonSerializerOptions jsonOptions)
    {
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(source, jsonOptions))
            ?? throw new InvalidDataException("CharacterAction clone payload is empty.");
        RemapCharacterActionIds(node, actionIdMap, propertyName: string.Empty);
        return JsonSerializer.Deserialize<T>(node.ToJsonString(), jsonOptions)
            ?? throw new InvalidDataException("CharacterAction clone payload could not be restored.");
    }

    private static void RemapCharacterActionIds(
        JsonNode node,
        IReadOnlyDictionary<int, int> actionIdMap,
        string propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach ((string name, JsonNode child) in obj.ToArray())
            {
                if (IsCharacterActionIdProperty(name)
                    && child is JsonValue value
                    && value.TryGetValue(out int oldActionId)
                    && actionIdMap.TryGetValue(oldActionId, out int newActionId))
                {
                    obj[name] = newActionId;
                    continue;
                }
                RemapCharacterActionIds(child, actionIdMap, name);
            }
            return;
        }

        if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                JsonNode child = array[i];
                if (string.Equals(propertyName, "actionIds", StringComparison.Ordinal)
                    && child is JsonValue value
                    && value.TryGetValue(out int oldActionId)
                    && actionIdMap.TryGetValue(oldActionId, out int newActionId))
                {
                    array[i] = newActionId;
                    continue;
                }
                RemapCharacterActionIds(child, actionIdMap, propertyName);
            }
        }
    }

    private static bool IsCharacterActionIdProperty(string propertyName)
    {
        return string.Equals(propertyName, "actionId", StringComparison.Ordinal)
            || string.Equals(propertyName, "defaultActionId", StringComparison.Ordinal)
            || string.Equals(propertyName, "lightLandingActionId", StringComparison.Ordinal)
            || string.Equals(propertyName, "heavyLandingActionId", StringComparison.Ordinal)
            || string.Equals(propertyName, "targetActionId", StringComparison.Ordinal)
            || string.Equals(propertyName, "requestedActionId", StringComparison.Ordinal);
    }

    private static AnimationAuthoringPackage RewriteAnimationTemplatePackage(
        AnimationAuthoringPackage animation,
        string oldPackageId,
        string oldDisplayName,
        string newPackageId,
        string newDisplayName,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions)
    {
        if (animation == null)
            return new AnimationAuthoringPackage();

        var replacementMap = new Dictionary<string, string>(StringComparer.Ordinal);
        AddReplacement(replacementMap, oldPackageId, newPackageId);
        AddReplacement(replacementMap, oldDisplayName, newDisplayName);
        AddReplacement(replacementMap, "charpkg." + oldPackageId, "charpkg." + newPackageId);
        AddReplacement(replacementMap, "animation." + oldPackageId, "animation." + newPackageId);
        AddReplacement(replacementMap, "skeleton." + oldPackageId, "skeleton." + newPackageId);
        AddReplacement(replacementMap, "avatar." + oldPackageId, "avatar." + newPackageId);

        if (package?.ResourceCatalog?.Entries != null)
        {
            foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
            {
                if (entry == null)
                    continue;
                AddReplacement(replacementMap, "charpkg." + oldPackageId + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId), entry.StableId);
                AddReplacement(replacementMap, "char." + oldPackageId, "char." + newPackageId);
            }
        }

        animation = RewriteJsonObject(animation, jsonOptions, replacementMap);
        animation.PackageId = "animation." + newPackageId;
        animation.StableId = "charpkg." + newPackageId + ".animation";
        animation.DisplayName = newDisplayName + " Animation";
        if (!string.IsNullOrWhiteSpace(animation.SkeletonProfileId))
            animation.SkeletonProfileId = animation.SkeletonProfileId.Replace(oldPackageId, newPackageId, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(animation.AvatarProfileId))
            animation.AvatarProfileId = animation.AvatarProfileId.Replace(oldPackageId, newPackageId, StringComparison.Ordinal);
        return animation;
    }

    private static void RewriteCharacterTemplateImportMaterials(
        CharacterImportMaterialsDocument document,
        string oldPackageId,
        string newPackageId)
    {
        if (document?.Items == null || string.IsNullOrWhiteSpace(oldPackageId) || string.IsNullOrWhiteSpace(newPackageId))
            return;

        string oldResourcePrefix = "char." + oldPackageId;
        string newResourcePrefix = "char." + newPackageId;
        for (int i = 0; i < document.Items.Count; i++)
        {
            CharacterImportMaterialItem item = document.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.ResourceKey))
                continue;
            item.ResourceKey = item.ResourceKey.Replace(oldResourcePrefix, newResourcePrefix, StringComparison.Ordinal);
        }
    }

    private static T RewriteJsonObject<T>(T value, JsonSerializerOptions jsonOptions, IReadOnlyDictionary<string, string> replacements)
    {
        if (value == null || replacements == null || replacements.Count == 0)
            return value;

        string json = JsonSerializer.Serialize(value, jsonOptions);
        foreach (KeyValuePair<string, string> pair in replacements.OrderByDescending(static item => item.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.Equals(pair.Key, pair.Value, StringComparison.Ordinal))
                continue;
            json = json.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? value;
    }

    private static AuthoringResourceSelectionRef RewriteSelection(
        AuthoringResourceSelectionRef selection,
        IReadOnlyDictionary<string, string> resourceKeyMap,
        IReadOnlyDictionary<string, string> stableIdMap)
    {
        selection ??= new AuthoringResourceSelectionRef();
        selection.PackageResourceKey = ReplaceMappedValue(selection.PackageResourceKey, resourceKeyMap);
        selection.ResourceStableId = ReplaceMappedValue(selection.ResourceStableId, stableIdMap);
        return selection;
    }

    private static string ReplaceMappedValue(string value, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrWhiteSpace(value) || map == null)
            return value ?? string.Empty;
        return map.TryGetValue(value, out string replacement) ? replacement : value;
    }

    private static void AddReplacement(IDictionary<string, string> replacements, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.Equals(from, to, StringComparison.Ordinal))
            return;
        replacements[from] = to ?? string.Empty;
    }

    private static string BuildCharacterResourceStableId(string packageId, string localId)
    {
        return "charpkg." + packageId + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(localId);
    }

    private static string BuildCharacterResourceKey(string oldResourceKey, string oldPackageId, string newPackageId, string typeId, string localId, string variant)
    {
        string oldPrefix = "char." + oldPackageId + ".";
        if (!string.IsNullOrWhiteSpace(oldPackageId)
            && !string.IsNullOrWhiteSpace(oldResourceKey)
            && oldResourceKey.StartsWith(oldPrefix, StringComparison.Ordinal))
        {
            return "char." + newPackageId + "." + oldResourceKey.Substring(oldPrefix.Length);
        }

        return CharacterPackageResourceKeyGenerator.Generate(newPackageId, typeId, localId, variant);
    }

    private static string NormalizeCharacterPackageId(string packageId)
    {
        string normalized = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId).Replace('.', '_');
        return string.Equals(normalized, "unknown", StringComparison.Ordinal) ? string.Empty : normalized;
    }

    private static string NormalizeCharacterPackageDirectoryName(string directoryName, string packageId)
    {
        string slug = CharacterPackageResourceKeyGenerator.NormalizeSegment(directoryName);
        if (string.IsNullOrWhiteSpace(slug) || string.Equals(slug, "unknown", StringComparison.Ordinal))
            slug = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId).Replace('.', '-').Replace('_', '-');
        if (!slug.StartsWith("character-", StringComparison.Ordinal))
            slug = "character-" + slug;
        return slug;
    }

    private static string BuildDefaultCharacterDisplayName(string packageId)
    {
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        string[] parts = packageId.Split(new[] { '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "New Character";
        return string.Join(" ", parts.Select(part => textInfo.ToTitleCase(part)));
    }

    private static string ResolveCharacterTemplateRelative(string template)
    {
        if (string.Equals(template, "creature", StringComparison.OrdinalIgnoreCase))
            return "Tools/MxFramework.Authoring/samples/character-slime";
        return "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
    }

    private static void CopyDirectoryRecursive(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (string directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, file);
            string targetFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: false);
        }
    }

    private static string GetTemplatePackageId(string templatePath, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = Path.Combine(templatePath, "manifest.json");
        if (!File.Exists(manifestPath))
            return string.Empty;
        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
        return manifest.PackageId ?? string.Empty;
    }

    private static string GetTemplateDisplayName(string templatePath, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = Path.Combine(templatePath, "manifest.json");
        if (!File.Exists(manifestPath))
            return string.Empty;
        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
        return manifest.DisplayName ?? string.Empty;
    }

    private static void RefreshCharacterResourceHashes(string packagePath, CharacterResourcePackage package)
    {
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            entry.Hashes ??= new CharacterPackageResourceHashes();
            entry.Hashes.Algorithm = "sha256";

            string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, entry.RelativePath);
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                entry.Hash = CharacterPackageHashUtility.ComputeFileSha256(fullPath);
                entry.Hashes.ContentHash = entry.Hash;
            }

            if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            {
                entry.ImportHints ??= new CharacterPackageImportHint();
                entry.ImportHints.ModelWrapperPose ??= new CharacterAuthoringLocalPose();
            }

            entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            entry.Hashes ??= new CharacterPackageResourceHashes();
            entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog);
        }
    }

    private static CharacterPackageResourceEntry ImportCharacterModel(
        string rootPath,
        string packageRelative,
        CharacterStudioModelImportRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(request.fileName))
            throw new ArgumentException("fileName is required.");
        if (string.IsNullOrWhiteSpace(request.bytesBase64))
            throw new ArgumentException("bytesBase64 is required.");

        string sourceExtension = Path.GetExtension(request.fileName).ToLowerInvariant();
        if (sourceExtension != ".glb" && sourceExtension != ".gltf" && sourceExtension != ".fbx")
            throw new ArgumentException("Only .glb, .gltf, and .fbx model files are supported.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.bytesBase64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("bytesBase64 is not valid base64.", ex);
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packageRelative);

        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName)).Replace('.', '_');
        string targetExtension = sourceExtension;
        byte[] packageModelBytes = bytes;
        bool convertedFromFbx = false;

        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string contentHash = CharacterPackageHashUtility.ComputeSha256(packageModelBytes);
        string defaultRelativePath = ("resources/models/" + fileStem + targetExtension).Replace('\\', '/');
        CharacterPackageResourceEntry entry = ResolveModelImportEntry(package, request.role, fileStem, contentHash, defaultRelativePath, request.resourceKey);
        string existingExtension = Path.GetExtension(entry.RelativePath ?? string.Empty);
        string relativePath = !string.IsNullOrWhiteSpace(request.targetRelativePath)
            ? ResolveModelImportTargetRelativePath(request.targetRelativePath)
            : (!string.IsNullOrWhiteSpace(request.resourceKey)
            && !string.IsNullOrWhiteSpace(entry.RelativePath)
            && string.Equals(existingExtension, targetExtension, StringComparison.OrdinalIgnoreCase)
            ? entry.RelativePath
            : defaultRelativePath);
        string outputPath = Path.Combine(packagePath, relativePath);
        bool shouldWriteFile = !File.Exists(outputPath)
            || !string.Equals(CharacterPackageHashUtility.ComputeFileSha256(outputPath), contentHash, StringComparison.OrdinalIgnoreCase);
        if (shouldWriteFile)
            WriteBytesAtomic(outputPath, packageModelBytes);

        ApplyModelImportEntry(package, entry, request, relativePath, contentHash, targetExtension, convertedFromFbx);
        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return entry;
    }

    private static string ResolveModelImportTargetRelativePath(string targetRelativePath)
    {
        if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(targetRelativePath))
            throw new ArgumentException("targetRelativePath must stay inside the character package.");
        return targetRelativePath.Replace('\\', '/').TrimStart('/');
    }

    private static byte[] ConvertFbxToGlb(string rootPath, string fileStem, string originalFileName, byte[] bytes)
    {
        string converterPath = ResolveFbx2GltfPath(rootPath);
        string conversionRoot = Path.Combine(rootPath, "Temp", "MxFrameworkAuthoring", "CharacterStudio", "fbx-imports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(conversionRoot);

        string sourceName = string.IsNullOrWhiteSpace(fileStem) ? "model" : fileStem;
        string sourcePath = Path.Combine(conversionRoot, sourceName + ".fbx");
        string outputBasePath = Path.Combine(conversionRoot, sourceName);
        string outputPath = outputBasePath + ".glb";
        File.WriteAllBytes(sourcePath, bytes);

        var startInfo = new ProcessStartInfo(converterPath)
        {
            WorkingDirectory = conversionRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputBasePath);
        startInfo.ArgumentList.Add("--binary");

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to launch FBX2glTF converter.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { process.WaitForExit(); } catch { }
            throw new InvalidOperationException("FBX to GLB conversion timed out: " + originalFileName);
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            string detail = string.Join("\n", new[] { stdout, stderr }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(detail))
                detail = "FBX2glTF did not produce a GLB output.";
            throw new InvalidOperationException("FBX to GLB conversion failed for " + originalFileName + ": " + detail);
        }

        return File.ReadAllBytes(outputPath);
    }

    private static string ResolveFbx2GltfPath(string rootPath)
    {
        string overridePath = Environment.GetEnvironmentVariable("MXFRAMEWORK_FBX2GLTF") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string fullOverridePath = Path.GetFullPath(overridePath);
            if (File.Exists(fullOverridePath))
                return fullOverridePath;
            throw new FileNotFoundException("MXFRAMEWORK_FBX2GLTF points to a missing converter executable.", fullOverridePath);
        }

        string platformDirectory;
        string executableName = "FBX2glTF";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platformDirectory = "Darwin";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platformDirectory = "Windows_NT";
            executableName = "FBX2glTF.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            platformDirectory = "Linux";
        else
            throw new PlatformNotSupportedException("FBX conversion is only wired for macOS, Linux, and Windows.");

        string packageConverterPath = Path.Combine(rootPath, "Tools", "MxFramework.CharacterStudio", "node_modules", "fbx2gltf", "bin", platformDirectory, executableName);
        if (File.Exists(packageConverterPath))
            return packageConverterPath;

        throw new FileNotFoundException("FBX conversion requires FBX2glTF. Run `npm --prefix Tools/MxFramework.CharacterStudio install`, or set MXFRAMEWORK_FBX2GLTF to the converter executable.", packageConverterPath);
    }

    private static CharacterPackageResourceEntry ResolveModelImportEntry(
        CharacterResourcePackage package,
        string role,
        string fileStem,
        string contentHash,
        string relativePath,
        string requestedResourceKey)
    {
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;
        string normalizedRole = string.IsNullOrWhiteSpace(role) ? "preview" : role.Trim();

        CharacterPackageResourceEntry entry = null;
        if (!string.IsNullOrWhiteSpace(requestedResourceKey))
            entry = CharacterPackageResourcePipeline.FindByKey(catalog, requestedResourceKey);

        if (normalizedRole.Equals("body", StringComparison.OrdinalIgnoreCase))
        {
            entry ??= catalog.Entries.FirstOrDefault(item =>
                item != null && (string.Equals(item.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocalId, "model.body", StringComparison.OrdinalIgnoreCase)));
        }
        else if (normalizedRole.Equals("mainHand", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("offHand", StringComparison.OrdinalIgnoreCase))
        {
            SingleWeaponConfiguration definition = package.GlobalWeaponLibrary?.SingleWeaponConfigurations?.FirstOrDefault(item =>
                item != null && item.AllowedSlots != null && item.AllowedSlots.Contains(normalizedRole, StringComparer.OrdinalIgnoreCase));
            string weaponKey = GetWeaponDefinitionReferenceKey(definition);
            if (!string.IsNullOrWhiteSpace(weaponKey))
                entry ??= CharacterPackageResourcePipeline.FindByKey(catalog, weaponKey);
        }

        entry ??= FindModelResourceByContentHash(catalog, contentHash);
        entry ??= catalog.Entries.FirstOrDefault(item =>
            item != null
            && string.Equals(item.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizePackageRelativePath(item.RelativePath), NormalizePackageRelativePath(relativePath), StringComparison.Ordinal));

        if (entry == null)
        {
            entry = new CharacterPackageResourceEntry();
            catalog.Entries.Add(entry);
        }

        return entry;
    }

    private static void ApplyModelImportEntry(
        CharacterResourcePackage package,
        CharacterPackageResourceEntry entry,
        CharacterStudioModelImportRequest request,
        string relativePath,
        string contentHash,
        string extension,
        bool convertedFromFbx)
    {
        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string normalizedRole = string.IsNullOrWhiteSpace(request.role) ? "preview" : request.role.Trim();
        bool isBody = normalizedRole.Equals("body", StringComparison.OrdinalIgnoreCase);
        bool isWeapon = normalizedRole.Equals("mainHand", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("offHand", StringComparison.OrdinalIgnoreCase);
        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName));
        string localId = isBody ? "model.body" : (isWeapon ? ("weapon." + normalizedRole + ".model") : ("model." + fileStem));

        if (string.IsNullOrWhiteSpace(entry.ResourceKey))
            entry.ResourceKey = ResolveRequestedOrGeneratedResourceKey(
                request.resourceKey,
                packageId,
                CharacterPackageResourceTypeIds.Model,
                isBody ? "body" : localId);
        if (string.IsNullOrWhiteSpace(entry.LocalId))
            entry.LocalId = localId;
        if (string.IsNullOrWhiteSpace(entry.StableId))
            entry.StableId = "charpkg." + CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId) + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId);

        entry.TypeId = CharacterPackageResourceTypeIds.Model;
        entry.Variant = string.IsNullOrWhiteSpace(entry.Variant) ? "default" : entry.Variant;
        entry.Usage = isBody
            ? CharacterPackageResourceUsageIds.CharacterModel
            : (isWeapon ? CharacterPackageResourceUsageIds.WeaponModel : CharacterPackageResourceUsageIds.PreviewMesh);
        entry.SourceFormat = extension.TrimStart('.');
        entry.PackageId = packageId;
        entry.RelativePath = relativePath;
        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        entry.ImportHints.TargetRelativePath = relativePath;
        entry.ImportHints.Scale = entry.ImportHints.Scale <= 0 ? 1f : entry.ImportHints.Scale;
        entry.ImportHints.ModelWrapperPose ??= new CharacterAuthoringLocalPose();
        entry.ImportHints.ProviderId = string.IsNullOrWhiteSpace(entry.ImportHints.ProviderId) ? "unityAsset" : entry.ImportHints.ProviderId;
        entry.ImportHints.MaterialPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.MaterialPolicy) ? "importEmbeddedOrPackageMaterials" : entry.ImportHints.MaterialPolicy;
        entry.ImportHints.CollisionPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.CollisionPolicy) ? "authoringGeometryOnly" : entry.ImportHints.CollisionPolicy;
        entry.ImportHints.PhysicsDataPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.PhysicsDataPolicy) ? "separateGeometryBinding" : entry.ImportHints.PhysicsDataPolicy;
        entry.ImportHints.UpAxis = string.IsNullOrWhiteSpace(entry.ImportHints.UpAxis) ? "Y+" : entry.ImportHints.UpAxis;
        entry.ImportHints.ForwardAxis = string.IsNullOrWhiteSpace(entry.ImportHints.ForwardAxis) ? "Z+" : entry.ImportHints.ForwardAxis;
        entry.Tags ??= new List<string>();
        AddTag(entry.Tags, "characterstudio-import");
        AddTag(entry.Tags, normalizedRole);
        if (convertedFromFbx)
            AddTag(entry.Tags, "converted-from-fbx");
        else
            entry.Tags.RemoveAll(tag => string.Equals(tag, "converted-from-fbx", StringComparison.OrdinalIgnoreCase));
        if (isBody)
            entry.Tags.RemoveAll(tag => string.Equals(tag, "weapon", StringComparison.OrdinalIgnoreCase));

        entry.Hash = contentHash;
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog);

        entry.Provenance ??= new CharacterPackageResourceProvenance();
        entry.Provenance.SourceTool = convertedFromFbx ? "CharacterStudio/FBX2glTF" : "CharacterStudio";
        entry.Provenance.SourceFile = request.fileName;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(entry.Provenance.CreatedUtc))
            entry.Provenance.CreatedUtc = now;
        entry.Provenance.ModifiedUtc = now;
        entry.ImportHints.Metadata ??= new Dictionary<string, string>();
        entry.ImportHints.Metadata["placeholderResource"] = "false";
        if (entry.Preview != null)
            entry.Preview.IsPlaceholder = false;

        // Schema 3.0 deliberately does not turn character-local model imports into weapon rules.
        // Single-weapon resource selection is owned by WeaponSource/global_weapon_library.json.
    }

    private static void TouchResourceProvenance(CharacterPackageResourceEntry entry, string sourceTool, string sourceFile)
    {
        if (entry == null)
            return;

        entry.Provenance ??= new CharacterPackageResourceProvenance();
        if (!string.IsNullOrWhiteSpace(sourceTool))
            entry.Provenance.SourceTool = sourceTool;
        if (!string.IsNullOrWhiteSpace(sourceFile))
            entry.Provenance.SourceFile = sourceFile;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(entry.Provenance.CreatedUtc))
            entry.Provenance.CreatedUtc = now;
        entry.Provenance.ModifiedUtc = now;
    }

    private static CharacterPackageResourceEntry FindModelResourceByContentHash(CharacterPackageResourceCatalog catalog, string contentHash)
    {
        string normalizedHash = CharacterPackageHashUtility.NormalizeSha256(contentHash);
        if (catalog == null || string.IsNullOrWhiteSpace(normalizedHash))
            return null;

        return catalog.Entries.FirstOrDefault(item =>
            item != null
            && string.Equals(item.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(CharacterPackageHashUtility.NormalizeSha256(CharacterPackageResourcePipeline.GetDeclaredContentHash(item)), normalizedHash, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePackageRelativePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimStart('/');
    }

    private static void AddTag(List<string> tags, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            tags.Add(tag);
    }

    private static object RunCharacterImport(
        string rootPath,
        string packageRelative,
        CharacterStudioImportRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string reportOut = Path.Combine(rootPath, "Temp", "MxFrameworkAuthoring", "CharacterStudio", "import-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(reportOut);

        ProcessStartInfo startInfo = BuildAuthoringCliProcessStartInfo(rootPath);
        startInfo.ArgumentList.Add("character");
        startInfo.ArgumentList.Add("import-unity");
        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add("--project-root");
        startInfo.ArgumentList.Add(rootPath);
        startInfo.ArgumentList.Add("--unity-root");
        startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(request.unityRoot) ? "Assets/MxFrameworkGenerated/CharacterPackages" : request.unityRoot);
        startInfo.ArgumentList.Add("--report-out");
        startInfo.ArgumentList.Add(reportOut);
        startInfo.ArgumentList.Add("--check-files");
        if (!string.IsNullOrWhiteSpace(request.buildTarget))
        {
            startInfo.ArgumentList.Add("--build-target");
            startInfo.ArgumentList.Add(request.buildTarget);
        }
        if (request.checkHashes)
            startInfo.ArgumentList.Add("--check-hashes");
        if (request.dryRun)
            startInfo.ArgumentList.Add("--dry-run");

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to launch character import command.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { process.WaitForExit(); } catch { }
            string timedOutStdout = stdoutTask.GetAwaiter().GetResult();
            string timedOutStderr = stderrTask.GetAwaiter().GetResult();
            return new { success = false, timedOut = true, exitCode = -1, stdout = timedOutStdout, stderr = timedOutStderr, reportOut };
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        string reportPath = Path.Combine(reportOut, "import_report.json");
        CharacterUnityImportEditorRequestResponse editorRebuild = null;
        if (process.ExitCode == 0 && !request.dryRun)
        {
            if (!File.Exists(reportPath))
                throw new InvalidOperationException("Character Unity import completed without an import report.");
            CharacterUnityImportReport importReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(
                File.ReadAllText(reportPath),
                jsonOptions) ?? throw new InvalidOperationException("Character Unity import report was empty.");
            editorRebuild = WriteCharacterUnityImportEditorRequest(
                rootPath,
                packageRelative,
                string.IsNullOrWhiteSpace(request.unityRoot) ? "Assets/MxFrameworkGenerated/CharacterPackages" : request.unityRoot,
                importReport,
                jsonOptions);
        }

        return new
        {
            success = process.ExitCode == 0,
            timedOut = false,
            exitCode = process.ExitCode,
            stdout,
            stderr,
            reportOut = ToProjectRelativePath(rootPath, reportOut),
            report = File.Exists(reportPath) ? ReadOptionalJson(rootPath, reportPath) : null,
            editorRebuild
        };
    }

    private static ProcessStartInfo BuildAuthoringCliProcessStartInfo(string rootPath)
    {
        string processPath = Environment.ProcessPath ?? string.Empty;
        if (IsRunnableAuthoringProcess(processPath))
            return CreateAuthoringCliProcessStartInfo(processPath, rootPath);

        string assemblyPath = typeof(EditorServer).Assembly.Location;
        if (File.Exists(assemblyPath))
        {
            if (string.Equals(Path.GetExtension(assemblyPath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                var dotnetStartInfo = CreateAuthoringCliProcessStartInfo("dotnet", rootPath);
                dotnetStartInfo.ArgumentList.Add(assemblyPath);
                return dotnetStartInfo;
            }

            return CreateAuthoringCliProcessStartInfo(assemblyPath, rootPath);
        }

        string cliProject = Path.Combine(rootPath, "Tools", "MxFramework.Authoring", "src", "MxFramework.Authoring.Cli", "MxFramework.Authoring.Cli.csproj");
        var startInfo = CreateAuthoringCliProcessStartInfo("dotnet", rootPath);
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cliProject);
        startInfo.ArgumentList.Add("--");
        return startInfo;
    }

    private static ProcessStartInfo CreateAuthoringCliProcessStartInfo(string fileName, string rootPath)
    {
        return new ProcessStartInfo(fileName)
        {
            WorkingDirectory = rootPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    private static bool IsRunnableAuthoringProcess(string processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            return false;

        string fileName = Path.GetFileNameWithoutExtension(processPath);
        if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(fileName, "authoring", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteJsonFileAtomic<T>(string path, T value, JsonSerializerOptions jsonOptions)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine);
        ReplaceFileAtomic(temp, path);
    }

    private static void WriteBytesAtomic(string path, byte[] bytes)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        ReplaceFileAtomic(temp, path);
    }

    private static void ReplaceFileAtomic(string temp, string path)
    {
        if (!File.Exists(path))
        {
            File.Move(temp, path);
            return;
        }

        try
        {
            File.Replace(temp, path, null);
        }
        catch (IOException ex) when ((ex.HResult & 0xFFFF) == 1175)
        {
            // Windows ERROR_UNABLE_TO_REMOVE_REPLACED can occur when a file watcher briefly
            // prevents metadata-preserving replacement. The temp file is already complete;
            // overwrite only for this exact condition and keep every other I/O failure visible.
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);
        }
    }

    private static bool HasValidationGate(CharacterAuthoringValidationReport report, CharacterAuthoringValidationGate gate)
    {
        if (report == null || report.Issues == null)
            return false;
        for (int i = 0; i < report.Issues.Count; i++)
        {
            if (report.Issues[i] != null && report.Issues[i].Gate == gate)
                return true;
        }

        return false;
    }

    private static bool HasNonDraftExportBlocker(CharacterAuthoringValidationReport report)
    {
        if (report?.Issues == null)
            return false;
        for (int i = 0; i < report.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = report.Issues[i];
            if (issue == null || issue.Gate != CharacterAuthoringValidationGate.ExportBlocked)
                continue;
            if (!IsCharacterPackageDraftIssue(issue.Code))
                return true;
        }
        return false;
    }

    internal static bool IsCharacterPackageDraftIssue(string code)
    {
        return code == CharacterAuthoringValidationCodes.WeaponConfigurationUpgradeRequired ||
               code == CharacterAuthoringValidationCodes.MissingWeaponConfiguration ||
               code == CharacterAuthoringValidationCodes.MissingDefaultWeaponConfiguration ||
               code == CharacterAuthoringValidationCodes.DuplicateWeaponDefinitionId ||
               code == CharacterAuthoringValidationCodes.DuplicateWeaponConfigurationId ||
               code == CharacterAuthoringValidationCodes.InvalidWeaponConfigurationSlot ||
               code == CharacterAuthoringValidationCodes.MissingWeaponDefinitionReference ||
               code == CharacterAuthoringValidationCodes.MissingWeaponAttachmentReference ||
               code == CharacterAuthoringValidationCodes.DuplicateWeaponAttachmentId ||
               code == CharacterAuthoringValidationCodes.WeaponSlotNotAllowed ||
               code == CharacterAuthoringValidationCodes.TwoHandWeaponSlotConflict ||
               code == CharacterAuthoringValidationCodes.MissingAnimationProfileReference ||
               code == CharacterAuthoringValidationCodes.MissingActionSetReference ||
               code == CharacterAuthoringValidationCodes.MissingAbilityLoadoutReference ||
               code == CharacterAuthoringValidationCodes.GlobalWeaponLibraryMissing ||
               code == CharacterAuthoringValidationCodes.GlobalWeaponLibrarySchemaUnsupported ||
               code == CharacterAuthoringValidationCodes.SingleWeaponConfigurationIdDuplicate ||
               code == CharacterAuthoringValidationCodes.SingleWeaponConfigurationNotFound ||
               code == CharacterAuthoringValidationCodes.UnarmedConfigurationInvalid ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationIdDuplicate ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationDefaultInvalid ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponSlotMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponSlotDuplicate ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponAttachmentRequired ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponAttachmentForbidden ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponAttachmentMismatch ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponAnimationProfileMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponActionSetMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponAbilityLoadoutMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate ||
               code == CharacterAuthoringValidationCodes.CombinedWeaponActionIdShared ||
               code == CharacterAuthoringValidationCodes.WeaponSchemaMigrationAbilityRebuildRequired;
    }

    private static JsonElement? ReadOptionalJson(string rootPath, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!IsPathAllowed(rootPath, fullPath) || !File.Exists(fullPath))
            return null;
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        return doc.RootElement.Clone();
    }

    private static T ReadOptionalJsonFile<T>(string rootPath, string path, JsonSerializerOptions jsonOptions)
        where T : class
    {
        string fullPath = Path.GetFullPath(path);
        if (!IsPathAllowed(rootPath, fullPath) || !File.Exists(fullPath))
            return null;

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), jsonOptions);
    }

    private static string ToProjectRelativePath(string rootPath, string path)
    {
        string fullRoot = NormalizeDirectoryRoot(rootPath);
        string fullPath = Path.GetFullPath(path);
        if (!IsPathAtOrUnderRoot(fullRoot, fullPath))
            return fullPath.Replace('\\', '/');
        return Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/');
    }

    private static void ServeStatic(HttpListenerResponse response, string rootPath, string requestPath)
    {
        string relative = Uri.UnescapeDataString(requestPath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, relative));
        if (!IsPathAtOrUnderRoot(rootPath, fullPath))
        {
            WriteText(response, "Forbidden", "text/plain", 403);
            return;
        }

        if (Directory.Exists(fullPath))
            fullPath = Path.Combine(fullPath, "index.html");

        if (!File.Exists(fullPath))
        {
            WriteText(response, "Not Found", "text/plain", 404);
            return;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        response.StatusCode = 200;
        response.ContentType = GetContentType(fullPath);
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static void WriteJson<T>(HttpListenerResponse response, T value, JsonSerializerOptions jsonOptions, int statusCode = 200)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, GetCompactJsonOptions(jsonOptions));
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static JsonSerializerOptions GetCompactJsonOptions(JsonSerializerOptions jsonOptions)
    {
        if (jsonOptions == null || !jsonOptions.WriteIndented)
            return jsonOptions;

        lock (CompactJsonOptionsGate)
        {
            if (!ReferenceEquals(s_compactJsonOptionsSource, jsonOptions))
            {
                s_compactJsonOptionsSource = jsonOptions;
                s_compactJsonOptions = new JsonSerializerOptions(jsonOptions)
                {
                    WriteIndented = false
                };
            }

            return s_compactJsonOptions;
        }
    }

    private static void ScheduleEditorRestart(string rootPath, int port, string defaultPackage)
    {
        if (Interlocked.Exchange(ref s_restartScheduled, 1) != 0)
            return;

        ProcessStartInfo restart = BuildEditorRestartProcessStartInfo(string.IsNullOrWhiteSpace(s_toolsRootPath) ? rootPath : s_toolsRootPath, rootPath, port, defaultPackage);
        Process.Start(restart);

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            Environment.Exit(0);
        });
    }

    private static ProcessStartInfo BuildEditorRestartProcessStartInfo(string toolsRootPath, string projectRootPath, int port, string defaultPackage)
    {
        string restartScriptPath = Path.Combine(toolsRootPath, "Tools", "MxFramework.EditorHub", "start-editor-hub.bat");
        if (File.Exists(restartScriptPath))
        {
            string command = "set MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER=0"
                + " && set \"MXFRAMEWORK_EDITOR_HUB_PROJECT_ROOT=" + projectRootPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
                + " && ping 127.0.0.1 -n 3 >nul"
                + " && start \"\" \"" + restartScriptPath + "\" " + port.ToString(CultureInfo.InvariantCulture) + " \"" + defaultPackage.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        }

        string processPath = Environment.ProcessPath ?? string.Empty;
        string entryAssemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        string executablePath = !string.IsNullOrWhiteSpace(processPath) ? processPath : entryAssemblyPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("Unable to determine authoring executable path for restart.");

        string fallbackCommand = "ping 127.0.0.1 -n 3 >nul && start \"\" \"" + executablePath + "\" editor serve --tools-root \"" + toolsRootPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" --project-root \"" + projectRootPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" --port " + port.ToString(CultureInfo.InvariantCulture) + " --package \"" + defaultPackage.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + fallbackCommand,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    private static void WriteText(HttpListenerResponse response, string text, string contentType, int statusCode)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".md" => "text/markdown; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml; charset=utf-8",
            ".otf" => "font/otf",
            _ => "application/octet-stream"
        };
    }

    private sealed class AuthoringResourceCacheEntry
    {
        public AuthoringResourceCacheEntry(AuthoringResourceCollection collection, DateTime cachedAtUtc)
        {
            Collection = collection;
            CachedAtUtc = cachedAtUtc;
        }

        public AuthoringResourceCollection Collection { get; }

        public DateTime CachedAtUtc { get; }
    }

    private sealed class CharacterStudioSaveRequest
    {
        public CharacterResourcePackage package { get; set; }
        public string expectedRevision { get; set; } = string.Empty;
        public bool force { get; set; }
    }

    private sealed class CharacterStudioHurtboxFitReviewRequest
    {
        public string colliderHash { get; set; } = string.Empty;
        public string animation { get; set; } = string.Empty;
        public double falseNegativeRate { get; set; }
        public double falsePositiveRate { get; set; }
        public double firstHitDepthP95Meters { get; set; }
        public int bodySampleCount { get; set; }
        public int hurtboxSampleCount { get; set; }
        public bool acceptedAsAuthored { get; set; }
        public string measurementStatus { get; set; } = "NotRequired";
    }

    private static string ComputeHurtboxColliderFileHash(string path)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class CharacterStudioMigrationRequest
    {
        public string expectedRevision { get; set; } = string.Empty;
        public string expectedWeaponLibraryRevision { get; set; } = string.Empty;
        public bool force { get; set; }
    }

    internal sealed class CharacterStudioCreateRequest
    {
        public string packageId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public string directoryName { get; set; } = string.Empty;
        public string template { get; set; } = "humanoid";
        public int idCatalogRevision { get; set; }
    }

    internal sealed class CharacterStudioDuplicateRequest
    {
        public string sourcePackage { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public string directoryName { get; set; } = string.Empty;
        public int idCatalogRevision { get; set; }
    }

    internal sealed class CharacterStudioArchiveRequest
    {
        public string package { get; set; } = string.Empty;
        public string confirmPackageId { get; set; } = string.Empty;
    }

    internal sealed class CharacterPackageCreateConflictException : InvalidOperationException
    {
        public CharacterPackageCreateConflictException(string code, string message, string conflictPackageRelative)
            : base(message)
        {
            Code = code ?? string.Empty;
            ConflictPackageRelative = conflictPackageRelative ?? string.Empty;
        }

        public string Code { get; }
        public string ConflictPackageRelative { get; }
    }

    internal sealed class CharacterPackageWriteConflictException : InvalidOperationException
    {
        public const string ConflictCode = "CHARACTER_PACKAGE_WRITE_CONFLICT";

        public CharacterPackageWriteConflictException(
            string expectedRevision,
            string currentRevision,
            string message)
            : base(message)
        {
            ExpectedRevision = expectedRevision ?? string.Empty;
            CurrentRevision = currentRevision ?? string.Empty;
        }

        public string ExpectedRevision { get; }
        public string CurrentRevision { get; }
    }

    internal sealed class CharacterPackageValidationException : InvalidOperationException
    {
        public const string ErrorCode = "CHARACTER_PACKAGE_VALIDATION_BLOCKED";

        public CharacterPackageValidationException(CharacterAuthoringValidationReport validation)
            : base(BuildMessage(validation))
        {
            Validation = validation ?? new CharacterAuthoringValidationReport();
        }

        public CharacterAuthoringValidationReport Validation { get; }

        private static string BuildMessage(CharacterAuthoringValidationReport validation)
        {
            CharacterAuthoringValidationIssue[] blockers = validation?.Issues?
                .Where(issue =>
                    issue != null && issue.Gate == CharacterAuthoringValidationGate.ExportBlocked)
                .ToArray()
                ?? Array.Empty<CharacterAuthoringValidationIssue>();
            int blockerCount = blockers.Length;
            string blockerSummary = string.Join(
                "; ",
                blockers.Take(5).Select(issue =>
                    issue.Code
                    + (string.IsNullOrWhiteSpace(issue.Field) ? string.Empty : "@" + issue.Field)));
            return blockerCount > 0
                ? $"Package 草稿未保存：仍有 {blockerCount} 个必须先修复的结构错误。"
                    + (string.IsNullOrWhiteSpace(blockerSummary)
                        ? ""
                        : " " + blockerSummary)
                    + " 请在“诊断”中查看并定位。"
                : "Package 草稿未保存：仍有必须先修复的结构错误。请在“诊断”中查看并定位。";
        }
    }

    internal sealed class CharacterPackageManagementConflictException : InvalidOperationException
    {
        public CharacterPackageManagementConflictException(string code, string message, string conflictPackageRelative)
            : base(message)
        {
            Code = code ?? string.Empty;
            ConflictPackageRelative = conflictPackageRelative ?? string.Empty;
        }

        public string Code { get; }
        public string ConflictPackageRelative { get; }
    }

    private sealed class CharacterStudioImportRequest
    {
        public string package { get; set; } = string.Empty;
        public string unityRoot { get; set; } = string.Empty;
        public string buildTarget { get; set; } = string.Empty;
        public bool checkHashes { get; set; }
        public bool dryRun { get; set; }
    }

    private sealed class CharacterStudioModelImportRequest
    {
        public string fileName { get; set; } = string.Empty;
        public string role { get; set; } = "body";
        public string resourceKey { get; set; } = string.Empty;
        public string targetRelativePath { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
    }

    private sealed class CharacterBodyPartsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartAuthoring> bodyParts { get; set; } = new();
    }

    private sealed class CharacterBodyCollidersFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterBodyColliderProfile> colliders { get; set; } = new();
    }

    private sealed class CharacterSocketsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterSocketProfile> sockets { get; set; } = new();
    }

    private sealed class CharacterWeaponSlotsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterWeaponSlotProfile> weaponSlots { get; set; } = new();
    }

    private sealed class CharacterTracesFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<WeaponTraceProfile> traces { get; set; } = new();
    }

    private sealed class CharacterBodyPartContactPointsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartContactPointProfile> contactPoints { get; set; } = new();
    }

    private sealed class ModDiagnoseRequest
    {
        public List<string> containers { get; set; } = new();
        public string loadout { get; set; } = string.Empty;
        public bool includeAbsolutePaths { get; set; }
    }

}
