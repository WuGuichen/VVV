import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const repoRoot = path.resolve(fileURLToPath(new URL("../../../", import.meta.url)));
const toolRoot = path.join(repoRoot, "Tools/MxFramework.ResourceLibrary");
const resourceLibraryScripts = [
  "web/resource-library-core.js",
  "web/resource-library-audio.js",
  "web/resource-library-audio-workspace.js",
  "web/resource-library-audio-inspector.js",
  "web/resource-library-audio-preview.js",
  "web/resource-library-audio-diagnostics.js",
  "web/resource-library-browse.js",
  "web/resource-library-profile.js",
  "web/resource-library-references.js",
  "web/resource-library-graph.js",
  "web/resource-library-build-debug.js"
];
const required = [
  "web/index.html",
  "web/app.js",
  "web/story-source-status.js",
  "web/art-source-status.js",
  ...resourceLibraryScripts,
  "web/styles.css",
  "scripts/smoke.mjs"
];

for (const relative of required) {
  assert(fs.existsSync(path.join(toolRoot, relative)), `missing ${relative}`);
}

const index = fs.readFileSync(path.join(toolRoot, "web/index.html"), "utf8");
const app = resourceLibraryScripts
  .map(relative => fs.readFileSync(path.join(toolRoot, relative), "utf8"))
  .join("\n\n");
const appShim = fs.readFileSync(path.join(toolRoot, "web/app.js"), "utf8");
const storySourceStatus = fs.readFileSync(path.join(toolRoot, "web/story-source-status.js"), "utf8");
const artSourceStatus = fs.readFileSync(path.join(toolRoot, "web/art-source-status.js"), "utf8");
const styles = fs.readFileSync(path.join(toolRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(toolRoot, "start-resource-library.sh"), "utf8");
const windowsLauncher = fs.readFileSync(path.join(toolRoot, "start-resource-library.bat"), "utf8");
const readme = fs.readFileSync(path.join(toolRoot, "README.md"), "utf8");

runNodeSyntaxCheck(path.join(toolRoot, "web/app.js"));
for (const relative of resourceLibraryScripts) {
  runNodeSyntaxCheck(path.join(toolRoot, relative));
}
runNodeSyntaxCheck(path.join(toolRoot, "web/story-source-status.js"));
runNodeSyntaxCheck(path.join(toolRoot, "web/art-source-status.js"));

assert(index.includes("MxFramework 资源管理器"), "index should expose Chinese title");
assert(appShim.includes("resource-library-*.js"), "legacy app.js should only describe the split implementation");
assertScriptOrder(index, [
  "./story-source-status.js",
  "./art-source-status.js",
  "./resource-library-core.js",
  "./resource-library-audio.js",
  "./resource-library-audio-workspace.js",
  "./resource-library-audio-inspector.js",
  "./resource-library-audio-preview.js",
  "./resource-library-audio-diagnostics.js",
  "./resource-library-browse.js",
  "./resource-library-profile.js",
  "./resource-library-references.js",
  "./resource-library-graph.js",
  "./resource-library-build-debug.js"
], "index should load StorySource, ArtSource, then Resource Library split scripts in dependency order");
assert(index.includes("workspaceStory") && index.includes("storyWorkspace") && index.includes("data-workspace=\"story\"") && index.includes("故事设定"), "index should expose the StorySource workspace");
assert(index.includes("workspaceArt") && index.includes("artWorkspace") && index.includes("data-workspace=\"art\"") && index.includes("美术状态"), "index should expose the Art status workspace");
assert(index.includes("workspaceAudio") && index.includes("audioWorkspace") && index.includes("data-workspace=\"audio\"") && index.includes("音频语义审查"), "index should expose the Audio semantic review workspace");
assert(index.includes("resourceList") && index.includes("resourceTree") && index.includes("treeGroupModeSelect"), "index should render resource tree and flat list");
assert(index.includes("inspectorContent"), "index should render inspector anchors");
assert(app.includes("buildPathTree") && app.includes("buildTaxonomyTree") && app.includes("getTreeScopedItems"), "app should build hierarchical resource trees");
assert(index.includes("data-tab=\"overview\"") && index.includes("总览") && index.includes("data-tab=\"unity\"") && index.includes("Unity") && index.includes("data-tab=\"runtime\"") && index.includes("运行时") && index.includes("data-tab=\"references\"") && index.includes("引用") && index.includes("data-tab=\"diagnostics\"") && index.includes("诊断"), "index should expose localized inspector tabs");
assert(index.includes("resourceImportFileInput") && index.includes("resourceImportFolderInput") && index.includes("resourceReplaceFileInput"), "write actions should provide hidden file inputs");
assert(index.includes("importPresetSelect") && index.includes("导入类型"), "write actions should expose typed import presets");
assert(index.includes("importResourceButton") && index.includes("importFolderButton") && index.includes("reimportResourceButton") && index.includes("replaceSourceButton"), "write actions should expose import/folder/reimport/replace buttons");
assert(index.includes("workspaceProfile") && index.includes("buildProfileContent") && index.includes("saveBuildProfileButton"), "profile workspace should expose build profile editor and save action");
assert(index.includes("data-workspace=\"story\"") && index.includes("data-workspace=\"art\"") && index.includes("data-workspace=\"browse\"") && index.includes("data-workspace=\"audio\"") && index.includes("data-workspace=\"profile\"") && index.includes("data-workspace=\"references\"") && index.includes("data-workspace=\"graph\"") && index.includes("data-workspace=\"build\"") && index.includes("data-workspace=\"debug\""), "index should expose primary workspaces including Story, Art, Audio, Refs, and Graph");
assert(index.indexOf("data-workspace=\"story\"") < index.indexOf("data-workspace=\"art\"") && index.indexOf("data-workspace=\"art\"") < index.indexOf("data-workspace=\"browse\"") && index.indexOf("data-workspace=\"browse\"") < index.indexOf("data-workspace=\"audio\"") && index.indexOf("data-workspace=\"audio\"") < index.indexOf("data-workspace=\"profile\""), "Story and Art workspaces should appear before Browse, Audio, and Profile");
assert(index.indexOf("data-workspace=\"profile\"") < index.indexOf("data-workspace=\"references\"") && index.indexOf("data-workspace=\"references\"") < index.indexOf("data-workspace=\"graph\"") && index.indexOf("data-workspace=\"graph\"") < index.indexOf("data-workspace=\"build\""), "Graph workspace should sit between Refs and Build");
assert(index.includes("workspaceReferences") && index.includes("referencesTargetList") && index.includes("referencesContent") && index.includes("referencesAdviceContent"), "Refs workspace should expose filters, reference table, and bundle advice panels");
assert(index.includes("referencesReviewBar") && index.includes("referencesScopeSearch") && index.includes("referencesScopeHint") && index.includes("referencesScopeExplanation"), "Refs workspace should expose review summary, scope search, scope explanation, and scope hint");
assert(index.includes("workspaceGraph") && index.includes("graphCanvas") && index.includes("graphInspectorContent") && index.includes("copyGraphSummaryButton"), "Graph workspace should expose SVG canvas, inspector, and copy summary action");
assert(index.includes("data-graph-scope=\"current\"") && index.includes("data-graph-scope=\"checked\"") && index.includes("data-graph-scope=\"issues\"") && index.includes("data-graph-scope=\"bundle\"") && index.includes("data-graph-scope=\"all\""), "Graph workspace should expose controlled graph scopes");
assert(app.includes('data-action="add-profile"') && app.includes('data-action="open-profile"'), "browse/profile contextual actions should use data-action hooks");
assert(app.includes("bundle-profile-layout") && app.includes("renderBundleMembers") && app.includes("renderBundleSettings"), "profile workspace should expose bundle-first editor layout");
assert(app.includes("bundle-rule-meta") && app.includes("bundle-plan-resources"), "bundle UI should expose build target/compression and expanded plan resources");
assert(!app.includes('data-bundle-action="create-bundle"') && app.includes("assignCheckedToBundle") && app.includes("getBuildProfileBundleRules"), "app should keep Bundle policy read-only while assigning manual resources to predefined rules");
assert(app.includes('data-action="batch-assign-bundle"') && app.includes('data-action="batch-clear-bundle"'), "batch actions should support assigning and removing bundle membership");
assert(!index.includes("bottom-panel-container") && !index.includes("action-bar"), "persistent bottom action bar should be removed");
assert(index.includes("profileMembershipFilter") && index.includes("runtimeReadyFilter"), "profile workflow should expose membership and runtime-ready filters");
assert(index.includes("selectVisibleButton") && index.includes("clearCheckedButton") && index.includes("checkedSummary"), "resource browser should expose explicit multi-select controls");
assert(app.includes("batch-add-profile") && app.includes("batch-remove-profile"), "batch profile actions should appear via contextual batch bars");
assert(index.includes("rawJsonSection") && index.includes("原始 JSON"), "debug workspace should expose collapsed raw JSON section");
assert(index.includes("openUnityWorkbenchButton") && index.includes("buildChecklist") && index.includes("复制构建总览"), "build workspace should expose unity guidance, local chain checklist, and copyable build overview");
assert(index.includes("build-primary-panel") && index.includes("resourcePlanDetails") && index.indexOf("buildArtifactsPanel") < index.indexOf("resourcePlanPanel"), "build workspace should prioritize build output and keep Resource Plan as expandable context");
assert(!index.includes('id="workflowAssistant"'), "top-level workflow assistant should be removed");
assert(index.includes("browseWorkflowAssistant") && index.includes("profileWorkflowAssistant") && index.includes("buildWorkflowAssistant"), "browse/profile/build workspaces should expose contextual workflow assistants");
assert(index.includes("data-tab=\"build\""), "inspector should expose Build tab");
assert(!index.includes("deleteResourceButton") && !index.includes("editTagsButton"), "delete/tag actions should stay hidden until APIs are ready");
assert(index.includes("复制详情 JSON") && index.includes("复制诊断 JSON"), "copy JSON actions should be visible");

assert(app.includes("/api/character/packages"), "app should call character packages API");
assert(app.includes("/api/authoring/resources?package="), "app should call authoring resource list API");
assert(app.includes("/api/authoring/resources/resource-plan?package="), "app should call authoring resource plan API");
assert(app.includes("/api/authoring/resources/inspect?package="), "app should call authoring inspect API defensively");
assert(app.includes("/api/authoring/resources/stage-import"), "app should call external import staging API before folder promotion");
assert(app.includes("/api/authoring/resources/import") && app.includes("/api/authoring/resources/reimport") && app.includes("/api/authoring/resources/replace-source"), "app should call authoring resource write API gates");
assert(app.includes("/api/authoring/common-resources") && app.includes("/api/authoring/common-resources/sync") && app.includes("/api/authoring/common-resources/save"), "Resource Manager should own public reusable resource sync and import-hint save APIs");
assert(app.includes("characterStudioReturn") && app.includes("returnField") && app.includes("mx-resource-manager-selection") && app.includes("window.opener.postMessage"), "Resource Manager should support delegated return binding back to CharacterStudio");
assert(app.includes("/api/authoring/resources/resource-mapping?buildTarget=") && app.includes("/api/authoring/resources/resource-mapping/plan?buildTarget=") && app.includes("/api/authoring/resources/global-build-status?package="), "app should call production resource mapping, shared plan, and build status APIs");
assert(app.includes("renderResourceMappingStatus") && app.includes("生产资源映射") && app.includes("Resource Manager 与 Unity Builder 共享同一个编译计划"), "build workspace should expose the active production resource mapping state");
assert(app.includes("saveBuildProfileDraft") && app.includes("findBuildProfileEntryForItem") && app.includes("overrideIntent") && app.includes("isManualProfileEntry"), "app should edit only manual profile membership and v2 override intent");
assert(app.includes("activeWorkspace") && app.includes("setWorkspace") && app.includes('setWorkspace("profile")'), "app should navigate across browse/profile/build/debug workspaces");
assert(app.includes('const WORKSPACES = ["story", "art", "browse", "audio", "profile", "references", "graph", "build", "debug"]'), "app workspace order should include Story and Art before Browse, Audio before Profile, and graph between references and build");
assert(app.includes('updateStoryPacket: "/api/authoring/resources/story-packets/update"') && app.includes('approveStoryPacket: "/api/authoring/resources/story-packets/approve"') && app.includes('updateStoryCanon: "/api/authoring/resources/story-canon/update"') && app.includes('updateStoryOutline: "/api/authoring/resources/story-outlines/update"') && app.includes('approveStoryOutline: "/api/authoring/resources/story-outlines/approve-for-packet"') && app.includes("renderStoryWorkspace") && app.includes("handleStoryWorkspaceEvent") && app.includes("saveStoryPacketDraft") && app.includes("approveStoryPacket") && app.includes("saveStoryCanonDraft") && app.includes("saveStoryOutlineDraft"), "app should route controlled StorySource canon, outline, packet saves and approvals through Authoring API");
assert(app.includes("artFilters") && app.includes("artImagePreview") && app.includes("renderArtWorkspace") && app.includes("handleArtWorkspaceEvent") && app.includes("openStaticArtSourceUrl") && app.includes("openArtImagePreview") && app.includes('state.filters.providerId = "artSource"'), "app should only register Art workspace state, rendering, static-url opening, preview lightbox state, and event forwarding");
assert(app.includes("getDeliveryEntryState") && app.includes("将构建") && app.includes("需要 Bundle"), "app should expose user-facing delivery outcome labels for profile entries");
assert(app.includes("inferProfileValidationField") && app.includes("field-highlight"), "app should highlight profile fields on save validation failure");
assert(app.includes('data-status-action="') && app.includes("openDiagnosticsView") && app.includes("renderDiagnosticsSummary"), "diagnostic count should be actionable and expose a global diagnostic summary");
assert(app.includes("deliveryMode") && app.includes('entry.bundleRule = ""'), "non-internal delivery modes should clear bundle rule in draft edits");
assert(app.includes("需要 Bundle") && app.includes("Bundle 缺失"), "profile delivery state should surface missing bundle assignments");
assert(app.includes("checkedResourceKeys: new Set()") && app.includes("selectVisibleResources") && app.includes("toggleCheckedResource"), "app should keep explicit checked resources separate from inspected resource selection");
assert(app.includes("addCheckedToBuildProfile") && app.includes("removeCheckedFromBuildProfile"), "app should support batch add/remove of checked resources to the profile draft");
assert(app.includes("applyBuildProfileBatchFields") && app.includes("data-profile-batch-enabled") && app.includes("data-profile-batch-field"), "app should support explicit opt-in batch field edits for checked draft profile entries");
assert(app.includes('renderBuildProfileBatchField("overrideIntent", "同键覆盖意图", "select", ["forbid", "allow"])'), "batch editing should use the v2 override intent contract");
assert(!app.includes("bundleOverrideMode") && !app.includes("DEFAULT_BUILD_TARGET"), "Resource Manager should not retain v1 override aliases or a guessed build target");
assert(app.includes("splitCsv(rawValue)") && app.includes("Array.isArray(value) && value.length === 0"), "batch labels and preload groups should replace arrays through splitCsv and ignore empty parsed inputs");
assert(app.includes("profileMembership") && app.includes("runtimeReady") && app.includes("isRuntimeReadyCandidate"), "app should filter by Build Profile membership and runtime-ready candidates");
assert(app.includes("notInProfile") && app.includes("saved") && app.includes("draftOnly") && app.includes("removedInDraft") && app.includes("modifiedInDraft"), "app should expose draft-vs-saved Build Profile state labels");
assert(app.includes("Web UI 只读构建状态，不构建 AssetBundle，也不写 StreamingAssets"), "app should describe Web build status as preview/read-only only");
assert(app.includes("renderWorkflowAssistant") && app.includes("getBrowseGuideState") && app.includes("getProfileGuideState") && app.includes("getBuildGuideState"), "app should expose workspace-specific beginner workflow assistants");
assert(app.includes("data-guide-action") && app.includes("handleGuideAction") && app.includes("选择资源") && app.includes("Offline 验证"), "workflow assistant stages should be clickable guide actions");
assert(app.includes("getReferenceRowsForItem") && app.includes("getCoReferencedResourceRows") && app.includes("getBundleRecommendationRows") && app.includes("applyBundleRecommendationDraft"), "app should compute Refs rows, co-references, bundle advice, and one-click draft actions");
assert(app.includes("getReferenceReviewModel") && app.includes("getReferenceIssueBuckets") && app.includes("getVisibleReferenceRecommendations") && app.includes("applyVisibleReferenceRecommendations") && app.includes("undoLastReferenceRecommendationDraft"), "app should expose Refs review model, issue buckets, visible apply, and undo helpers");
assert(app.includes("getReferenceEducationState") && app.includes("renderReferencesIntro") && app.includes("renderReferencesScopeExplanation") && app.includes("renderReferenceStatusMeaning") && app.includes("renderRecommendationSafetyText") && app.includes("formatReferenceUserLabel"), "Refs workspace should expose beginner education helpers");
assert(app.includes("本页能回答什么") && app.includes("谁在引用当前资源") && app.includes("同组资源 Bundle 是否合理") && app.includes("哪些建议可以安全写入草稿"), "Refs workspace should explain what the page answers");
assert(app.includes("隐藏说明") && app.includes("显示说明") && app.includes("REFERENCES_EDUCATION_STORAGE_KEY"), "Refs education should be toggleable through a local UI preference");
assert(app.includes("当前检查资源") && app.includes("同一配置还会加载") && app.includes("工程详情") && app.includes("为什么要关注"), "Refs source cards should use user-facing labels and fold technical fields");
assert(app.includes("写入推荐 Bundle 草稿") && app.includes("应用本组草稿建议") && app.includes("安全性：只会把这条建议涉及的资源写入前端 Profile 草稿"), "Refs recommendations should explain draft-only safety and use explicit draft action labels");
assert(app.includes("resourcesPayload, \"referenceGraph\"") && app.includes('pick(getCurrentDetail(item), "references")') && app.includes("findDraftBuildProfileEntryForItem"), "Refs workspace should reuse existing reference graph, inspect references, and profile draft data");
assert(app.includes('data-action="open-references-current"') && app.includes('data-action="open-references-issues"') && app.includes('data-recommendation-action="apply"'), "Refs entrypoints and recommendation actions should be clickable");
assert(app.includes("getReferenceGraphModel") && app.includes("getReferenceGraphNodes") && app.includes("getReferenceGraphEdges") && app.includes("getReferenceGraphLayout") && app.includes("renderReferenceGraphWorkspace") && app.includes("renderReferenceGraphSvg") && app.includes("renderReferenceGraphInspector"), "Graph workspace should expose graph model, layout, SVG, and inspector helpers");
assert(app.includes("resourcesPayload, \"referenceGraph\"") && app.includes("normalizeReferenceRow(edge") && app.includes("findDraftBuildProfileEntryForItem") && app.includes("getBundlePlan()"), "Graph workspace should reuse existing reference graph, profile draft, and bundle plan data");
assert(app.includes("graphScope") && app.includes("graphSearch") && app.includes("graphSelectedNodeId") && app.includes("graphShowOptional") && app.includes("graphColorMode"), "Graph workspace should keep local UI state for scope, search, selection, optional edges, and color mode");
assert(app.includes("reference-graph-svg") && app.includes("data-graph-node-id") && app.includes("data-graph-edge-id") && app.includes("copyReferenceGraphSummary"), "Graph workspace should render clickable SVG nodes/edges and copy a graph summary");
assert(app.includes("Graph 是只读可视图") && app.includes("去 Refs 查看建议") && !app.includes("handleReferenceGraphActionClick(event) {\n  apply"), "Graph actions should stay read-only and route fixes back to Refs");
assert(app.includes('el.browseContextBody.addEventListener("click", handleContextActionClick)'), "Browse detail actions should be wired to the shared action handler");
assert(app.includes("Profile 草稿") && app.includes("markBuildProfileDirty") && !app.includes("applyBundleRecommendationDraft(recommendationId) {\n  saveBuildProfileDraft"), "Refs recommendation actions should only edit the unsaved profile draft");
assert(app.includes("renderBrowsePrimaryActions") && app.includes("browse-more-actions") && app.includes("筛选推荐候选"), "browse workspace should expose one primary next action and fold secondary actions");
assert(app.includes("profile-save-callout") && app.includes("profile-batch-advanced"), "profile workspace should highlight unsaved drafts and fold advanced batch fields");
assert(app.includes("renderBuildReadinessPanel") && app.includes("getAuthorBuildReadiness") && app.includes("needsUnityBuild"), "build workspace should expose author-facing readiness and next action");
assert(app.includes("build-checklist-inline") && app.includes("el.buildChecklist.innerHTML = \"\""), "build checklist should render inside the single build scroll flow");
assert(app.includes("renderBuildStatusBundleReport") && app.includes("renderBuildArtifactReport") && app.includes("formatBytes"), "build workspace should expose bundle and artifact status reports");
assert(app.includes("groupBuildDiagnosticsForAuthors") && app.includes("external 配置引用（不进入本地 AssetBundle，已折叠）"), "build diagnostics should group expected external warnings for authors");
assert(app.includes("renderBundlePlanExternalSummary") && app.includes("External configuration references") && app.includes("不进入本地 AssetBundle catalog"), "bundle plan should expose external configuration-only resources");
assert(app.includes("countBuildProfileDeliveryModes") && app.includes("external config"), "profile workspace should summarize external config entries");
assert(!app.includes("/api/character/resources/import") && !app.includes("/api/character/resources/reimport") && !app.includes("/api/character/resources/replace-source"), "new Resource Manager writes should not use character-prefixed write APIs");
assert(!app.includes("/api/authoring/resources/build-assetbundle") && !app.includes("/api/authoring/resources/write-streaming-assets"), "web UI should not add backend build or StreamingAssets write routes");
assert(app.includes("resourceId") && app.includes("sourceProviderId") && app.includes("providerBindings"), "app should understand authoring resource identity and provider bindings");
assert(app.includes("providerFilter") && app.includes("getProviders") && app.includes("renderProviderList"), "app should expose provider filter and provider status");
assert(app.includes("storySource: 870") && app.includes("artSource: 860") && app.includes("characterAction: 790"), "app provider priority should include StorySource, ArtSource, and CharacterAction config without treating them as runtime-loadable");
assert(app.includes("fmodEventPath") && app.includes("audioCueId") && app.includes("audioEventDefinitionId"), "app should preserve FMOD audio provider metadata");
assert(app.includes("renderAudioWorkspace") && app.includes("handleAudioWorkspaceEvent") && app.includes("AudioCueOnly") && app.includes("FMOD 来源已过期") && app.includes("FMOD 来源不可用"), "Audio workspace should render read-only FMOD provider, stale, unavailable, and AudioCueOnly states");
assert(app.includes("open-browse-audio") && app.includes("open-references-current") && app.includes("copyAudioSummary") && app.includes("buildAudioSummaryJson"), "Audio workspace should expose Browse, Refs, Build, and copy-summary actions");
assert(app.includes("getAudioPrimaryFocus") && app.includes("focus-packaging-gaps") && app.includes("notAudioPlan") && app.includes("getRecommendedAudioSelection"), "Audio workspace should expose task-first focus actions and a recommended selection");
assert(app.includes("playSelectedAudioPreview") && app.includes("getAudioPreviewInfo") && app.includes("data-audio-action=\"play-preview\"") && app.includes("Web 试听播放的是可定位的源音频文件"), "Audio workspace should expose source-file preview playback without claiming FMOD runtime playback");
assert(app.includes("handleAudioKeyboardShortcut") && app.includes('event.code !== "Space"') && app.includes("isAudioShortcutTextInput"), "Audio workspace should support Space playback without hijacking form controls");
assert(app.includes("/api/authoring/resources/audio-semantics") && app.includes("loadAudioSemantics"), "Audio workspace should retain the compatibility semantic read endpoint");
assert(app.includes("renderAudioSemanticEditor") && app.includes("语义清单 · 只读") && app.includes("替换契约 · 只读") && app.includes("auditionStatus") && app.includes("replacementContract"), "Audio workspace should expose semantic, audition, and replacement status as a read-only overview");
assert(app.includes('data-audio-action="open-audio-workbench"') && app.includes("MxFramework.AudioWorkbench") && app.includes('url.searchParams.set("cue", row.audioCueId)'), "Audio workspace should deep-link the selected audioCueId to Audio Workbench");
assert(app.includes("getAudioPackageState") && app.includes("referenced-missing-plan") && app.includes("not-targeted") && app.includes("isAudioActionableDiagnostic") && app.includes("summarizeAudioDiagnosticHealth"), "Audio health should separate package scope and actionable diagnostics from workflow information");
assert(app.includes("AUDIO_INSPECTOR_TABS") && app.includes("data-audio-inspector-tab") && app.includes("renderAudioReplacementEditor") && app.includes("renderAudioTechnicalPanel"), "Audio workspace should split inspector content into overview, semantic, replacement, and technical tabs");
assert(app.includes("audioSemanticApiState") && app.includes("语义 API 不可用") && app.includes("Authoring server 版本过旧或语义 API 不可用"), "Audio workspace should distinguish unavailable semantic API from missing semantic entries");
assert(app.includes("dedupeAudioRows") && app.includes("renderAudioSourceSampleGroup") && app.includes("源文件样本"), "Audio workspace should deduplicate rows and keep source audio samples separate from FMOD bank events");
assert(app.includes("audioCueDetailExpanded") && app.includes("data-audio-toggle=\"cue-detail\"") && app.includes("Cue 进包明细"), "Audio workspace should keep AudioCue manifest detail behind an explicit foldout");
assert(app.includes("audio-code-line") && app.includes("data-audio-copy-text"), "Audio workspace should render long audio identifiers as copyable single-line code fields");
assert(!app.includes("renderAudioEventHeaderRow") && app.includes("audio-event-meta") && app.includes("当前包未使用") && app.includes("待人工试听"), "Audio workspace should render cue-first chip rows with package and semantic states");
assert(app.includes("sourcePreviewPath") && app.includes("audioSemanticPurpose") && app.includes("buildAudioSemanticManifestWithDraft") && app.includes("getAudioSemanticManifestPath"), "Audio workspace should preserve semantic metadata in rows and JSON flows");
assert(app.includes("targetResourceId") && app.includes("targetProviderResourceKey") && app.includes("targetRuntimeResourceKey"), "app should match cross-consumer reference graph targets");
assert(app.includes("postJson") && app.includes("readFileAsBase64"), "app should post write requests with uploaded file bytes");
assert(app.includes("IMPORT_PRESETS") && app.includes("animationClipGroup") && app.includes("audioCue"), "app should define typed animation and audio import presets");
assert(app.includes('id: "animationClipGroup"') && app.includes('extensions: ["anim", "glb", "gltf", "json"]'), "animation import preset should accept Unity .anim, GLB/GLTF, and JSON animation sources without treating FBX as animation");
assert(app.includes("importResourceFolder") && app.includes("webkitRelativePath"), "app should support folder import with stable local ids");
assert(app.includes("isIgnoredImportFile") && app.includes(".meta") && app.includes("忽略元数据"), "folder import should ignore editor metadata files separately from skipped resources");
assert(app.includes("isImportableStagedItem") && app.includes("AUTH_RES_IMPORT_IGNORED_FILE") && app.includes("externalImportStaging"), "folder import should use staging diagnostics and provider items");
assert(app.includes("isStagedItemSupportedByPreset") && app.includes("buildImportRequestFromStagedItem(file, staged, preset)") && app.includes("跳过非匹配"), "folder import should filter and promote staged files by the selected import preset");
assert(app.includes("inspectCache.clear") && app.includes("loadPackageData"), "write responses should refresh resource data");
assert(app.includes('updateArtTask: "/api/authoring/resources/art-tasks/update"') && app.includes('createArtTask: "/api/authoring/resources/art-tasks/create"') && app.includes("saveArtTaskDraft") && app.includes("saveArtCreateDraft") && app.includes("buildTaskUpdateRequest") && app.includes("buildCreateRequest"), "app should route controlled ArtSource task saves and creates through the Authoring API");
assert(app.includes("selectArtWorkstreamTask") && app.includes("data-art-workstream-key") && app.includes("artGraphZoom") && app.includes("handleArtGraphPointerDown") && app.includes("handleArtGraphPointerMove") && app.includes("state.artUiPanels[panelKey] = toggledPanel.open") && !app.includes("data-art-filter-field][data-art-filter-value"), "app should select ArtSource workstreams through the graph, support graph panning, and keep native collapsible panels synchronized without using workstream cards as filters");
assert(app.includes("buildFallbackInspect") && app.includes("inspect endpoint 不可用"), "app should include inspect fallback behavior");
assert(app.includes("onlyRuntimeLoadable") && app.includes("onlyDiagnostics"), "app should include client-side filters");
assert(app.includes("navigator.clipboard?.writeText") && app.includes("copyTextWithHiddenTextarea"), "app should copy JSON through clipboard with a local fallback");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "app should remain vanilla DOM/fetch JavaScript");

assert(artSourceStatus.includes("MxResourceArtSourceStatus") && artSourceStatus.includes("createModel") && artSourceStatus.includes("render"), "ArtSource status module should expose a small rendering interface");
assert(storySourceStatus.includes("MxResourceStorySourceStatus") && storySourceStatus.includes("createModel") && storySourceStatus.includes("render"), "StorySource status module should expose a small rendering interface");
assert(storySourceStatus.includes("故事设定编辑器") && storySourceStatus.includes("世界观记忆") && storySourceStatus.includes("故事大纲") && storySourceStatus.includes("输入包派生") && storySourceStatus.includes("批准给美术") && storySourceStatus.includes("下一步"), "StorySource module should expose localized canon, outline, packet editing, approval, and workflow copy");
assert(storySourceStatus.includes("storySourcePacketSnapshotJson") && storySourceStatus.includes("storySourceCanonSnapshotJson") && storySourceStatus.includes("storySourceOutlineSnapshotJson") && storySourceStatus.includes("storySourcePacketsHash") && storySourceStatus.includes("buildUpdateRequest") && storySourceStatus.includes("buildApproveRequest") && storySourceStatus.includes("buildCanonUpdateRequest") && storySourceStatus.includes("buildOutlineUpdateRequest") && storySourceStatus.includes("buildOutlineApproveRequest"), "StorySource module should consume provider metadata and build controlled canon/outline/packet save/approve requests");
assert(app.includes("storySelectedKeys") && storySourceStatus.includes("selectedCanon") && storySourceStatus.includes("selectedOutline") && storySourceStatus.includes("selectedPacket") && storySourceStatus.includes("data-story-packet-id"), "StorySource UI should keep independent canon, outline, and packet selections and bind packet actions to their own ids");
assert(storySourceStatus.includes("applyLinkHighlights") && storySourceStatus.includes("story-field-group") && storySourceStatus.includes("story-token-list") && storySourceStatus.includes("story-diagnostic-panel") && storySourceStatus.includes("renderBeatRows"), "StorySource module should render editor-style linkage, field groups, token lists, diagnostics, and beat row controls");
assert(app.includes("updateStoryOutlineBeatDraft") && app.includes("addStoryOutlineBeatDraft") && app.includes("removeStoryOutlineBeatDraft") && storySourceStatus.includes("updateOutlineDraftBeatField") && storySourceStatus.includes("addOutlineDraftBeat") && storySourceStatus.includes("removeOutlineDraftBeat"), "StorySource outline beat rows should edit the same draft model through app event forwarding");
assert(storySourceStatus.includes("PACKET_EDIT_STATUSES") && storySourceStatus.includes("OUTLINE_EDIT_STAGES") && storySourceStatus.includes("optionsWithCurrent"), "StorySource UI should keep approved packet/outline states out of normal save dropdowns");
assert(!storySourceStatus.includes("fetch(") && !storySourceStatus.includes("postJson") && !storySourceStatus.includes("/api/authoring/resources/art-tasks") && !storySourceStatus.includes("/api/authoring/resources/import") && !storySourceStatus.includes("/api/authoring/resources/global-build-profile"), "StorySource module should not call Art, Unity import, or profile APIs");
assert(artSourceStatus.includes("ArtSource 多进程美术工作台") && artSourceStatus.includes("流程依赖图") && artSourceStatus.includes("本条下一动作") && !artSourceStatus.includes("流程状态总览") && !artSourceStatus.includes("流程导航"), "ArtSource module should use the dependency graph as the only flow navigation surface");
assert(artSourceStatus.includes("ART_SOURCE_TASKS_MISSING") && artSourceStatus.includes("ART_SOURCE_TASKS_JSON_INVALID") && artSourceStatus.includes("ART_SOURCE_TASK_WORKSTREAM_MISSING") && artSourceStatus.includes("ART_SOURCE_TASK_ASSET_MISSING") && artSourceStatus.includes("ART_SOURCE_PREVIEW_MISSING"), "ArtSource module should surface task and preview diagnostics");
assert(artSourceStatus.includes("buildDependencyGraph") && artSourceStatus.includes("流程依赖图") && artSourceStatus.includes("外部输入门禁") && artSourceStatus.includes("等待外部输入") && artSourceStatus.includes("按住图中空白处拖动可移动视图") && artSourceStatus.includes("art-graph-node") && artSourceStatus.includes("blockedBy") && artSourceStatus.includes("normalizeGraphZoom") && artSourceStatus.includes('data-art-panel="graph"') && artSourceStatus.includes("data-art-graph-canvas") && artSourceStatus.includes('data-art-action="art-graph-zoom-reset"'), "ArtSource module should derive and render a collapsible, zoomable, pannable task dependency graph from blockedBy and distinguish external blockers");
assert(artSourceStatus.includes("art-contact-sheet") && artSourceStatus.includes("art-linked-assets") && artSourceStatus.includes("data-art-workstream-key") && !artSourceStatus.includes("art-workstream-board") && !artSourceStatus.includes('data-art-filter-field="workstream"'), "ArtSource module should render clickable dependency graph nodes, contact sheets, and linked assets without a duplicate flow list");
assert(artSourceStatus.includes("art-image-lightbox") && artSourceStatus.includes("data-art-preview-url") && artSourceStatus.includes("点击放大查看") && artSourceStatus.includes("图像放大预览"), "ArtSource module should render a read-only image preview lightbox for real preview images");
assert(artSourceStatus.includes("任务写入门禁") && artSourceStatus.includes("创建美术进程 / 任务") && artSourceStatus.includes("新建进程") && artSourceStatus.includes("新建任务") && artSourceStatus.includes("任务推进") && artSourceStatus.includes("保存任务") && artSourceStatus.includes("解除阻塞") && artSourceStatus.includes("送审"), "ArtSource workspace should expose controlled task progression and creation controls");
assert(artSourceStatus.includes("编辑阻塞与下一动作") && artSourceStatus.includes("任务详情、引用与诊断") && artSourceStatus.includes('data-art-panel="taskAssets"'), "ArtSource task editor should keep noisy fields in collapsible panels");
assert(!artSourceStatus.includes("fetch(") && !artSourceStatus.includes("postJson") && !artSourceStatus.includes("/api/authoring/resources/import") && !artSourceStatus.includes("/api/authoring/resources/reimport") && !artSourceStatus.includes("/api/authoring/resources/replace-source") && !artSourceStatus.includes("/api/authoring/resources/global-build-profile"), "ArtSource module should not call write or profile APIs");

assert(styles.includes(".resource-browser") && styles.includes(".inspector-tabs") && styles.includes(".workspace-nav") && styles.includes(".import-preset-label") && styles.includes(".profile-workspace-panel"), "styles should cover browser, workspace nav, import preset, and profile workspace UI");
assert(styles.includes("#workspaceAudio.active") && styles.includes(".audio-workspace") && styles.includes(".audio-main-grid") && styles.includes(".audio-bank-list") && styles.includes(".audio-cue-list") && styles.includes(".audio-impact-panel"), "styles should cover Audio workspace, bank/event list, cue list, and impact panel");
assert(styles.includes(".audio-focus-card") && styles.includes(".audio-health-grid") && styles.includes(".audio-health-card") && styles.includes("min-height: 50px"), "styles should cover compact Audio task focus and health status strip");
assert(styles.includes("minmax(420px, 0.9fr) minmax(520px, 1.1fr)") && styles.includes("@media (max-width: 1040px)") && styles.includes(".audio-inspector-tabs"), "styles should keep the Cue-first Audio layout readable and switch to one column below the desktop threshold");
assert(styles.includes(".audio-code-line") && styles.includes("white-space: nowrap") && styles.includes("overflow-x: auto"), "styles should keep long inspector identifiers on a copyable scrolling line");
assert(styles.includes(".audio-impact-panel") && styles.includes("position: sticky") && styles.includes(".audio-filter-primary") && styles.includes(".audio-advanced-filters"), "styles should keep the Cue inspector visible and secondary filters compact");
assert(styles.includes(".audio-cue-foldout") && styles.includes(".audio-source-samples") && styles.includes(".audio-event-meta"), "styles should cover Audio cue foldout, source sample group, and chip-based event rows");
assert(styles.includes(".audio-preview-panel") && styles.includes(".audio-preview-actions"), "styles should cover Audio source preview controls");
assert(styles.includes(".audio-semantic-editor") && styles.includes(".audio-replacement-panel") && styles.includes(".audio-validation-result") && styles.includes(".audio-save-state"), "styles should cover Audio semantic editing, replacement diagnostics, and save states");
assert(styles.includes(".story-workspace") && styles.includes(".story-workbench") && styles.includes(".story-workbench-v3") && styles.includes(".story-column") && styles.includes(".story-row") && styles.includes(".story-field-group") && styles.includes(".story-token-list") && styles.includes(".story-beat-row") && styles.includes(".story-diagnostic-panel"), "styles should cover StorySource canon, outline, packet lists, field groups, tokens, beat rows, and diagnostics");
assert(styles.includes(".art-workspace") && styles.includes(".art-preview-stage") && styles.includes(".art-action-panel") && styles.includes(".art-task-editor") && styles.includes(".art-create-panel") && styles.includes(".art-create-grid") && styles.includes(".art-segmented") && styles.includes(".art-asset-picker"), "styles should cover ArtSource workbench, create panel, contact sheet, action panel, and task editor");
assert(styles.includes(".art-collapse-panel") && styles.includes(".art-editor-compact-row") && styles.includes(".art-dependency-graph") && styles.includes(".art-dependency-summary") && styles.includes(".art-dependency-body") && styles.includes(".art-graph-node") && styles.includes(".art-graph-toolbar") && styles.includes(".art-graph-scale") && styles.includes(".art-graph-canvas.dragging") && styles.includes(".art-graph-lines > path") && styles.includes(".art-image-lightbox") && styles.includes(".art-preview-image-button") && !styles.includes(".art-workstream-group"), "styles should cover quiet collapsible ArtSource task editing, image preview lightbox, and the collapsible zoomable pannable dependency graph without duplicate flow navigation styles");
assert(styles.includes(".bundle-rule-meta") && styles.includes(".bundle-plan-resources"), "styles should cover bundle-first metadata and plan resource lists");
assert(styles.includes(".external-plan-panel"), "styles should cover external configuration-only plan summary");
assert(styles.includes(".context-actions") && styles.includes(".batch-bar") && styles.includes(".build-checklist"), "styles should cover contextual actions and build checklist");
assert(styles.includes(".build-readiness-panel") && styles.includes(".bundle-status-row") && styles.includes(".artifact-status-card"), "styles should cover build readiness, bundle status, and artifact status");
assert(styles.includes(".workflow-assistant") && styles.includes(".context-workflow") && styles.includes(".workflow-step") && styles.includes(".workflow-next"), "styles should cover contextual beginner workflow assistants");
assert(styles.includes(".references-review-bar") && styles.includes(".references-filter-panel") && styles.includes(".references-target-row") && styles.includes(".reference-group-summary") && styles.includes(".reference-tech-details") && styles.includes(".recommendation-row"), "styles should cover Refs review bar, filters, source cards, technical details, and recommendations");
assert(styles.includes(".refs-intro-grid") && styles.includes(".references-scope-explanation") && styles.includes(".reference-status-meaning") && styles.includes(".recommendation-explain-grid") && styles.includes(".recommendation-safety") && styles.includes(".references-ok-callout"), "styles should cover Refs beginner explanations, status meaning, safety text, and clear states");
assert(styles.includes("#workspaceGraph.active") && styles.includes(".graph-control-panel") && styles.includes(".graph-main-panel") && styles.includes(".graph-inspector-panel") && styles.includes(".reference-graph-svg") && styles.includes(".graph-node") && styles.includes(".graph-edge"), "styles should cover Graph layout, SVG nodes, and edges");
assert(styles.includes(".browse-primary-action-row") && styles.includes(".profile-save-callout") && styles.includes(".profile-batch-advanced"), "styles should cover simplified browse/profile beginner affordances");
assert(styles.includes("#workspaceBuild.active") && styles.includes("overflow-y: auto") && styles.includes(".resource-plan-details"), "styles should keep Build workspace readable with one scroll flow and collapsed plan context");
assert(styles.includes(".status-chip.clickable") && styles.includes(".diagnostics-summary"), "styles should cover clickable diagnostics and diagnostics summary");
assert(!styles.includes(".action-bar") && !styles.includes(".bottom-panel-container"), "styles should not retain persistent bottom action bar layout");
assert(styles.includes(".selection-toolbar") && styles.includes(".resource-check") && styles.includes(".profile-state-strip"), "styles should cover multi-select and profile state feedback");
assert(styles.includes(".profile-batch-form") && styles.includes(".profile-batch-toggle") && styles.includes(".profile-batch-apply"), "styles should cover build profile batch field editing controls");
assert(styles.includes("@media"), "styles should include responsive rules");
assert(launcher.includes("HEALTH_INSPECT_URL") && launcher.includes("HEALTH_AUDIO_SEMANTICS_URL") && launcher.includes("is_resource_library_server_ready"), "launcher should require inspect and audio semantic API readiness");
assert(windowsLauncher.includes("HEALTH_INSPECT_URL") && windowsLauncher.includes("HEALTH_AUDIO_SEMANTICS_URL"), "Windows launcher should require inspect and audio semantic API readiness");
assert(readme.includes("服务未就绪") && readme.includes("inspect API") && readme.includes("audio-semantics") && readme.includes("语义 API 不可用"), "README should explain service-not-ready, audio semantic API compatibility, and old-server troubleshooting");
assert(readme.includes("Audio 工作区") && readme.includes("Cue-first 的只读总览") && readme.includes("MxFramework.AudioWorkbench") && readme.includes("不再提供第二套保存入口"), "README should document the read-only Audio overview and Audio Workbench authoring boundary");
assert(readme.includes("选择可见") && readme.includes("notInProfile") && readme.includes("removedInDraft") && readme.includes("Bundle Plan 是预览面"), "README should document multi-select, draft state labels, and preview-only Bundle Plan behavior");
assert(!readme.includes("YooAsset 是默认路线") && !readme.includes("Addressables"), "README should not claim a new default resource backend");

runBuildProfileBatchBehaviorSmoke();
runReferencesRecommendationSmoke();
await runManualProducerSnapshotSmoke();

console.log("ResourceLibrary smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}

function assertScriptOrder(source, scripts, message) {
  let previousIndex = -1;
  for (const script of scripts) {
    const index = source.indexOf(script);
    assert(index >= 0, `${message}: missing ${script}`);
    assert(index > previousIndex, `${message}: ${script} is out of order`);
    previousIndex = index;
  }
}

function runNodeSyntaxCheck(filePath) {
  const result = spawnSync(process.execPath, ["--check", filePath], { encoding: "utf8" });
  if (result.status !== 0) {
    const output = [result.stdout, result.stderr].filter(Boolean).join("\n").trim();
    assert(false, `JavaScript syntax check failed for ${path.relative(toolRoot, filePath)}:\n${output}`);
  }
}

function runBuildProfileBatchBehaviorSmoke() {
  const runtime = buildAppBatchRuntime();
  const entries = [
    {
      resourceKey: "pkg:Texture:hero:",
      producerReceipt: { producerId: "resource-manager.manual" },
      deliveryMode: "internal",
      overrideIntent: "forbid",
      bundleGroupHint: "characters",
      bundleRule: "character",
      labels: ["old.label"],
      preloadGroups: ["old.preload"]
    },
    {
      resourceKey: "pkg:Audio:theme:",
      producerReceipt: { producerId: "audio.authoring" },
      deliveryMode: "internal",
      overrideIntent: "forbid",
      bundleGroupHint: "music",
      bundleRule: "audio",
      labels: ["audio.old"],
      preloadGroups: ["music.old"]
    }
  ];

  runtime.reset(entries, [
    { key: "deliveryMode", value: "external", enabled: true },
    { key: "overrideIntent", value: "allow", enabled: true },
    { key: "bundleGroupHint", value: "   ", enabled: true },
    { key: "bundleRule", value: "should-not-apply", enabled: true, disabled: true },
    { key: "labels", value: "runtime, featured,  ", enabled: true },
    { key: "preloadGroups", value: "warmup, hero", enabled: true }
  ]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.lastActionMessage.includes("已应用 1") && runtime.state.lastActionMessage.includes("跳过 2"), "batch edits should apply only to manual entries and skip domain-owned or missing entries");
  assert(runtime.state.buildProfileDirty === true, "real batch edits should mark the profile dirty");
  assert(entries[0].deliveryMode === "external" && entries[1].deliveryMode === "internal", "deliveryMode should update manual entries without mutating domain-owned entries");
  assert(entries[0].overrideIntent === "allow" && entries[1].overrideIntent === "forbid", "v2 override intent should preserve producer ownership");
  assert(entries[0].bundleGroupHint === "characters", "blank bundleGroupHint should not overwrite existing values");
  assert(entries[0].bundleRule === "" && entries[1].bundleRule === "audio", "external delivery should clear only the manual entry bundle assignment");
  assert(arrayEquals(entries[0].labels, ["runtime", "featured"]) && arrayEquals(entries[0].preloadGroups, ["warmup", "hero"]), "labels and preloadGroups should replace whole arrays from CSV");
  assert(arrayEquals(entries[1].labels, ["audio.old"]) && arrayEquals(entries[1].preloadGroups, ["music.old"]), "domain producer fields should remain unchanged");

  runtime.reset(entries, [{ key: "overrideIntent", value: "forbid", enabled: true }], ["hero"]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.buildProfileDirty === true && entries[0].overrideIntent === "forbid", "allowed v2 override intent should explicitly replace the manual value");

  runtime.reset(entries, [
    { key: "deliveryMode", value: "external", enabled: true },
    { key: "overrideIntent", value: "forbid", enabled: true },
    { key: "labels", value: "runtime, featured", enabled: true },
    { key: "preloadGroups", value: "warmup, hero", enabled: true }
  ], ["hero"]);
  runtime.applyBuildProfileBatchFields();

  assert(runtime.state.lastActionMessage.includes("已应用 1") && runtime.state.buildProfileDirty === false, "no-op batches should apply to draft entries without marking them dirty");

  runtime.reset(entries, []);
  runtime.applyBuildProfileBatchFields();
  assert(runtime.state.lastActionMessage.includes("没有启用可应用"), "disabled or blank-only batches should not overwrite and should report no applicable fields");
}

function runReferencesRecommendationSmoke() {
  const runtime = buildReferencesRuntime();
  const recommendations = runtime.getBundleRecommendationRows(runtime.targets);
  runtime.sandbox.lastRecommendations = recommendations;
  const buckets = runtime.getReferenceIssueBuckets(recommendations);

  assert(buckets.blocking.some(row => row.type === "missingProfile"), "Refs recommendations should flag required runtime resources missing Profile");
  assert(buckets.blocking.some(row => row.type === "unassignedBundle"), "Refs recommendations should flag internal required resources missing Bundle");
  assert(buckets.suggested.some(row => row.type === "splitBundle"), "Refs recommendations should flag required co-references split across bundles");
  assert(buckets.accepted.length > 0, "Refs recommendations should keep acceptable optional or valid Bundle references visible");
  assert(buckets.info.some(row => row.type === "unmapped"), "Refs recommendations should report unmapped reference targets as information");

  const visible = runtime.getVisibleReferenceRecommendations(recommendations);
  assert(visible.length >= 3, "Refs visible apply should include auto-applicable blocking/suggested recommendations");

  runtime.applyVisibleReferenceRecommendations();
  const entriesAfterApply = runtime.sandbox.profile.entries;
  assert(runtime.sandbox.state.buildProfileDirty === true, "Refs visible apply should mark Profile draft dirty");
  assert(entriesAfterApply.find(entry => entry.resourceKey === "anim")?.bundleRule === "bundleA", "Refs visible apply should add missing Profile entries to the recommended bundle");
  assert(entriesAfterApply.find(entry => entry.resourceKey === "shield")?.bundleRule === "bundleA", "Refs visible apply should fix missing Bundle assignment");
  assert(entriesAfterApply.find(entry => entry.resourceKey === "sword")?.bundleRule === "bundleA", "Refs visible apply should merge split required co-references");
  assert(runtime.sandbox.state.lastActionMessage.includes("Profile 草稿未保存"), "Refs visible apply should report unsaved Profile draft state");

  runtime.undoLastReferenceRecommendationDraft();
  const entriesAfterUndo = runtime.sandbox.profile.entries;
  assert(entriesAfterUndo.find(entry => entry.resourceKey === "anim") === undefined, "Refs undo should remove entries created by the last apply");
  assert(entriesAfterUndo.find(entry => entry.resourceKey === "shield")?.bundleRule === "", "Refs undo should restore previous missing Bundle assignment");
  assert(entriesAfterUndo.find(entry => entry.resourceKey === "sword")?.bundleRule === "bundleB", "Refs undo should restore previous split Bundle assignment");
}

async function runManualProducerSnapshotSmoke() {
  const snippets = [
    "normalizeProfileDeliveryMode",
    "buildManualProducerSnapshot",
    "normalizeManualResourceKey",
    "cloneManualProviderData",
    "sha256Text"
  ].map(name => {
    const source = extractFunctionBlock(name);
    return name === "buildManualProducerSnapshot" || name === "sha256Text" ? `async ${source}` : source;
  }).join("\n\n");
  const buildSnapshot = Function("helpers", `
    const asArray = helpers.asArray;
    const pick = helpers.pick;
    const stringValue = helpers.stringValue;
    ${snippets}
    return buildManualProducerSnapshot;
  `)({
    asArray: value => Array.isArray(value) ? value : [],
    pick(source, ...keys) {
      if (!source) return undefined;
      for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(source, key)) return source[key];
      }
      return undefined;
    },
    stringValue: value => value === null || value === undefined ? "" : String(value)
  });

  const snapshot = await buildSnapshot({ entries: [
    {
      authoringRecordId: "manual.editor-tool",
      deliveryIntent: "editorOnly",
      sourceLocator: {
        kind: "UnityAsset",
        unityGuid: "0123456789abcdef0123456789abcdef",
        cachedPath: "Assets/Editor/Tool.asset",
        domainSourceId: "manual.tool",
        providerData: { customExtension: "keep-locator", providerId: "retired-authority" }
      },
      providerData: { customTopLevel: "keep-entry", runtimeResourceKey: "retired-alias" },
      producerReceipt: { producerId: "resource-manager.manual" }
    },
    {
      authoringRecordId: "domain.owned",
      deliveryIntent: "internal",
      producerReceipt: { producerId: "character.package" }
    },
    {
      authoringRecordId: "manual.excluded",
      deliveryIntent: "excluded",
      sourceLocator: { kind: "UnityAsset", cachedPath: "Assets/Excluded.asset" },
      producerReceipt: { producerId: "resource-manager.manual" }
    },
    {
      authoringRecordId: "manual.bundle-move",
      resourceKey: { id: "bundle.move", typeId: "Object", packageId: "pkg", variant: "" },
      deliveryIntent: "internal",
      deliveryMode: "internal",
      overrideIntent: "forbid",
      bundleIntent: { ruleId: "old.bundle" },
      bundleRule: "new.bundle",
      sourceLocator: { kind: "unityAsset", unityGuid: "fedcba9876543210fedcba9876543210", cachedPath: "Assets/Moved.asset" },
      producerReceipt: { producerId: "resource-manager.manual" }
    }
  ] });

  assert(snapshot.entries.length === 3, "manual snapshot should omit domain-owned entries instead of rewriting them");
  const editorOnly = snapshot.entries.find(entry => entry.authoringRecordId === "manual.editor-tool");
  const excluded = snapshot.entries.find(entry => entry.authoringRecordId === "manual.excluded");
  const bundleMove = snapshot.entries.find(entry => entry.authoringRecordId === "manual.bundle-move");
  assert(editorOnly?.deliveryIntent === "editorOnly", "editor-only delivery intent should retain the canonical v2 enum spelling");
  assert(editorOnly?.sourceLocator?.cachedPath === "Assets/Editor/Tool.asset", "editor-only manual entries should preserve their source locator");
  assert(editorOnly?.sourceLocator?.providerData?.customExtension === "keep-locator" && !editorOnly.sourceLocator.providerData.providerId, "manual locator extensions should survive while retired authority aliases are stripped");
  assert(editorOnly?.providerData?.customTopLevel === "keep-entry" && !editorOnly.providerData.runtimeResourceKey, "manual entry provider extensions should survive without retired resource-key aliases");
  assert(excluded?.sourceLocator === null, "excluded manual entries should intentionally omit source locators");
  assert(bundleMove?.bundleIntent?.ruleId === "new.bundle", "manual save should persist the edited Bundle view instead of the stale loaded bundleIntent");
  assert(snapshot.entries.every(entry => /^sha256:[0-9a-f]{64}$/.test(entry.producerReceipt?.sourceRecordFingerprint || "")), "manual producer receipts should carry deterministic SHA-256 fingerprints");

  let missingMountRejected = false;
  try {
    await buildSnapshot({ entries: [{
      authoringRecordId: "manual.external",
      resourceKey: { id: "external", typeId: "Object", packageId: "pkg", variant: "" },
      deliveryIntent: "external",
      producerReceipt: { producerId: "resource-manager.manual" }
    }] });
  } catch (error) {
    missingMountRejected = String(error?.message || error).includes("explicit CatalogMount");
  }
  assert(missingMountRejected, "external manual entries should fail closed without an explicit CatalogMount");

  const external = await buildSnapshot({ entries: [{
    authoringRecordId: "manual.external",
    resourceKey: { id: "external", typeId: "Object", packageId: "pkg", variant: "" },
    deliveryIntent: "internal",
    deliveryMode: "external",
    overrideIntent: "forbid",
    bundleIntent: { ruleId: "stale.internal.bundle" },
    bundleRule: "",
    externalCatalogBinding: { catalogMountId: "external.mount" },
    source: { externalSourcePath: "External/OwnedByCatalog.asset" },
    producerReceipt: { producerId: "resource-manager.manual" }
  }] });
  assert(external.entries[0].deliveryIntent === "external" && external.entries[0].bundleIntent === null, "manual save should persist the edited delivery view instead of stale loaded intents");
  assert(external.entries[0].sourceLocator === null, "external manual entries should not invent a Unity source locator when the source Catalog owns the concrete route");
}

function buildReferencesRuntime() {
  const snippets = [
    "isManualProfileEntry",
    "getReferenceRowsForItem",
    "getCoReferencedResourceRows",
    "getReferenceRowIdentity",
    "getBundleRecommendationRows",
    "buildBundleRecommendation",
    "buildAcceptedRecommendation",
    "buildInfoRecommendation",
    "buildUnmappedReferenceRecommendation",
    "addRecommendation",
    "sortBundleRecommendations",
    "getBundleAssignmentState",
    "getRecommendedBundleId",
    "getReferenceIssueBuckets",
    "getVisibleReferenceRecommendations",
    "applyVisibleReferenceRecommendations",
    "applyReferenceRecommendationsDraft",
    "undoLastReferenceRecommendationDraft",
    "buildReferencePatchItemSnapshot",
    "getReferencePatchItemKey",
    "normalizeReferenceRow",
    "groupReferenceRows",
    "getAllReferenceEdges",
    "findItemForReferenceTarget",
    "uniqueItems"
  ].map(extractFunctionBlock).join("\n\n");
  const createRuntime = Function("sandbox", `
    const MANUAL_RESOURCE_MAPPING_PRODUCER = "resource-manager.manual";
    const state = sandbox.state;
    const pick = sandbox.pick;
    const asArray = sandbox.asArray;
    const stringValue = sandbox.stringValue;
    const stableJson = sandbox.stableJson;
    const compareText = sandbox.compareText;
    const structuredCloneCompat = sandbox.structuredCloneCompat;
    const uniqueRows = sandbox.uniqueRows;
    const uniqueStrings = sandbox.uniqueStrings;
    const getBrowserItems = sandbox.getBrowserItems;
    const getCurrentDetail = sandbox.getCurrentDetail;
    const getReferencesForItem = sandbox.getReferencesForItem;
    const matchesItem = sandbox.matchesItem;
    const findDraftBuildProfileEntryForItem = sandbox.findDraftBuildProfileEntryForItem;
    const getBuildProfileBundleRules = sandbox.getBuildProfileBundleRules;
    const findBundleRuleById = sandbox.findBundleRuleById;
    const canAssignItemToInternalBundle = sandbox.canAssignItemToInternalBundle;
    const isRuntimeReadyCandidate = sandbox.isRuntimeReadyCandidate;
    const formatResourceDisplayName = sandbox.formatResourceDisplayName;
    const buildDraftBuildProfileEntryForItem = sandbox.buildDraftBuildProfileEntryForItem;
    const ensureBuildProfileDraft = sandbox.ensureBuildProfileDraft;
    const markBuildProfileDirty = sandbox.markBuildProfileDirty;
    const render = sandbox.render;
    const renderReferencesWorkspace = sandbox.renderReferencesWorkspace;
    const profileEntryMatchesItem = sandbox.profileEntryMatchesItem;
    const normalizeBundleRuleId = sandbox.normalizeBundleRuleId;
    const getReferenceReviewModel = sandbox.getReferenceReviewModel;
    ${snippets}
    return {
      getBundleRecommendationRows,
      getReferenceIssueBuckets,
      getVisibleReferenceRecommendations,
      applyVisibleReferenceRecommendations,
      undoLastReferenceRecommendationDraft
    };
  `);

  const items = [
    { key: "body", resourceKey: "body", displayName: "Body", runtimeReady: true, assignable: true },
    { key: "sword", resourceKey: "sword", displayName: "Sword", runtimeReady: true, assignable: true },
    { key: "shield", resourceKey: "shield", displayName: "Shield", runtimeReady: true, assignable: true },
    { key: "anim", resourceKey: "anim", displayName: "Anim", runtimeReady: true, assignable: true },
    { key: "optional", resourceKey: "optional", displayName: "Optional", runtimeReady: true, assignable: true }
  ];
  const edges = [
    refEdge("body", true, "SpawnCritical"),
    refEdge("sword", true, "SpawnCritical"),
    refEdge("shield", true, "SpawnCritical"),
    refEdge("anim", true, "AnimationWarmup"),
    refEdge("optional", false, "UiDeferred"),
    { ...refEdge("ghost", true, "SpawnCritical"), targetStableId: "ghostStable", targetLibraryItemStableId: "ghostStable" }
  ];
  const profile = {
    entries: [
      { resourceKey: "body", deliveryMode: "internal", overrideIntent: "forbid", bundleRule: "bundleA" },
      { resourceKey: "sword", deliveryMode: "internal", overrideIntent: "forbid", bundleRule: "bundleB" },
      { resourceKey: "shield", deliveryMode: "internal", overrideIntent: "forbid", bundleRule: "" },
      { resourceKey: "optional", deliveryMode: "external", overrideIntent: "forbid", bundleRule: "" }
    ],
    bundleRules: [{ id: "bundleA" }, { id: "bundleB" }]
  };
  const state = {
    resourcesPayload: { referenceGraph: { edges } },
    selectedBundleRuleId: "bundleA",
    buildProfileDirty: false,
    lastActionMessage: "",
    referencesLastPatch: null
  };
  const sandbox = {
    state,
    items,
    targets: items.filter(item => item.key !== "sword"),
    profile,
    lastRecommendations: [],
    pick(source, ...keys) {
      if (!source) return undefined;
      for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(source, key)) return source[key];
      }
      return undefined;
    },
    asArray(value) {
      return Array.isArray(value) ? value : [];
    },
    stringValue(value) {
      return value === null || value === undefined ? "" : String(value);
    },
    stableJson(value) {
      return JSON.stringify(value, Object.keys(value || {}).sort());
    },
    compareText(left, right) {
      return String(left || "").localeCompare(String(right || ""));
    },
    structuredCloneCompat(value) {
      return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
    },
    uniqueRows(rows) {
      const seen = new Set();
      return rows.filter(row => {
        const key = JSON.stringify(row);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      });
    },
    uniqueStrings(values) {
      return Array.from(new Set((Array.isArray(values) ? values : []).map(String).filter(Boolean)));
    },
    getBrowserItems() {
      return items;
    },
    getCurrentDetail() {
      return {};
    },
    getReferencesForItem(item) {
      return edges.filter(edge => edge.targetResourceKey === item.resourceKey);
    },
    matchesItem(item, stableId, resourceKey, resourceId) {
      return Boolean(item && (item.resourceKey === resourceKey || item.key === resourceKey || item.stableId === stableId || item.resourceId === resourceId));
    },
    findDraftBuildProfileEntryForItem(item) {
      return profile.entries.find(entry => sandbox.profileEntryMatchesItem(entry, item)) || null;
    },
    getBuildProfileBundleRules() {
      return profile.bundleRules;
    },
    findBundleRuleById(id) {
      return profile.bundleRules.find(rule => rule.id === id) || null;
    },
    canAssignItemToInternalBundle(item) {
      return item?.assignable !== false;
    },
    isRuntimeReadyCandidate(item) {
      return item?.runtimeReady !== false;
    },
    formatResourceDisplayName(item) {
      return item?.displayName || item?.resourceKey || item?.key || "";
    },
    buildDraftBuildProfileEntryForItem(item) {
      return { resourceKey: item.resourceKey, deliveryMode: "internal", overrideIntent: "forbid", bundleRule: "" };
    },
    ensureBuildProfileDraft() {
      return profile;
    },
    markBuildProfileDirty() {
      state.buildProfileDirty = true;
    },
    render() {},
    renderReferencesWorkspace() {},
    profileEntryMatchesItem(entry, item) {
      return entry?.resourceKey === item?.resourceKey;
    },
    normalizeBundleRuleId(value) {
      return String(value || "").toLowerCase();
    },
    getReferenceReviewModel() {
      return { recommendations: sandbox.lastRecommendations };
    }
  };
  const runtime = createRuntime(sandbox);
  return { ...runtime, sandbox, targets: sandbox.targets };
}

function refEdge(resourceKey, required, preloadPolicy) {
  return {
    sourceConsumerKind: "character",
    sourceConfigKind: "character",
    sourceStableId: "char.demo",
    sourceField: `resourceKeys/${resourceKey}`,
    targetResourceId: `resource:${resourceKey}`,
    targetStableId: resourceKey,
    targetLibraryItemStableId: resourceKey,
    targetProviderResourceKey: resourceKey,
    targetResourceKey: resourceKey,
    bindingKind: "PackageResource",
    isRequiredAtRuntime: required,
    preloadPolicy
  };
}

function buildAppBatchRuntime() {
  const snippets = [
    "isManualProfileEntry",
    "applyBuildProfileBatchFields",
    "readBuildProfileBatchFields",
    "getProfileBatchRoot",
    "buildProfileBatchValuesEqual",
    "cloneBuildProfileBatchValue",
    "splitCsv",
    "cssEscapeCompat"
  ].map(extractFunctionBlock).join("\n\n");
  const createRuntime = Function("sandbox", `
    const MANUAL_RESOURCE_MAPPING_PRODUCER = "resource-manager.manual";
    const state = sandbox.state;
    const el = sandbox.el;
    const window = {};
    const pick = sandbox.pick;
    const stringValue = sandbox.stringValue;
    const getCheckedItems = sandbox.getCheckedItems;
    const findDraftBuildProfileEntryForItem = sandbox.findDraftBuildProfileEntryForItem;
    const markBuildProfileDirty = sandbox.markBuildProfileDirty;
    const render = sandbox.render;
    ${snippets}
    return { applyBuildProfileBatchFields };
  `);

  const state = { buildProfileDirty: false, lastActionMessage: "" };
  const checkedCatalog = {
    hero: { resourceKey: "pkg:Texture:hero:" },
    theme: { resourceKey: "pkg:Audio:theme:" },
    missing: { resourceKey: "pkg:Prefab:missing:" }
  };
  const sandbox = {
    state,
    el: { buildProfileContent: createBatchForm([]), profileBatchBar: createBatchForm([]) },
    checkedItems: [checkedCatalog.hero, checkedCatalog.theme, checkedCatalog.missing],
    entries: [],
    pick(source, ...keys) {
      if (!source) return undefined;
      for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(source, key)) return source[key];
      }
      return undefined;
    },
    stringValue(value) {
      return value === null || value === undefined ? "" : String(value);
    },
    getCheckedItems() {
      return sandbox.checkedItems;
    },
    findDraftBuildProfileEntryForItem(item) {
      return sandbox.entries.find(entry => entry.resourceKey === item.resourceKey) || null;
    },
    markBuildProfileDirty() {
      state.buildProfileDirty = true;
    },
    render() {}
  };
  const runtime = createRuntime(sandbox);
  return {
    state,
    applyBuildProfileBatchFields: runtime.applyBuildProfileBatchFields,
    reset(entries, fields, checkedKeys = ["hero", "theme", "missing"]) {
      state.buildProfileDirty = false;
      state.lastActionMessage = "";
      sandbox.entries = entries;
      sandbox.checkedItems = checkedKeys.map(key => checkedCatalog[key]);
      const form = createBatchForm(fields);
      sandbox.el.buildProfileContent = form;
      sandbox.el.profileBatchBar = form;
      sandbox.el.profileBatchBar.classList = { contains: () => false };
    }
  };
}

function createBatchForm(fields) {
  const controls = fields.map(field => ({
    dataset: { profileBatchField: field.key },
    value: field.value,
    disabled: Boolean(field.disabled)
  }));
  const toggles = new Map(fields.map(field => [field.key, { checked: Boolean(field.enabled) }]));
  return {
    querySelectorAll(selector) {
      return selector === "[data-profile-batch-field]" ? controls : [];
    },
    querySelector(selector) {
      const fieldMatch = selector.match(/data-profile-batch-enabled="([^"]+)"/);
      if (fieldMatch) return toggles.get(fieldMatch[1]) || null;
      return null;
    }
  };
}

function extractFunctionBlock(name) {
  const signature = `function ${name}`;
  const start = app.indexOf(signature);
  assert(start >= 0, `missing app function ${name}`);
  const bodyStart = app.indexOf("{", start);
  assert(bodyStart >= 0, `missing app function body ${name}`);
  let depth = 0;
  for (let index = bodyStart; index < app.length; index++) {
    const char = app[index];
    if (char === "{") depth++;
    if (char === "}") {
      depth--;
      if (depth === 0) return app.slice(start, index + 1);
    }
  }
  assert(false, `unterminated app function ${name}`);
}

function arrayEquals(left, right) {
  return Array.isArray(left)
    && Array.isArray(right)
    && left.length === right.length
    && left.every((value, index) => value === right[index]);
}
