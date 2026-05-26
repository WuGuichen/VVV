import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(fileURLToPath(new URL("../../../", import.meta.url)));
const editorRoot = path.join(repoRoot, "Tools/MxFramework.AnimationEditor");
const hubRoot = path.join(repoRoot, "Tools/MxFramework.EditorHub");
const characterStudioRoot = path.join(repoRoot, "Tools/MxFramework.CharacterStudio");

const required = [
  "README.md",
  "start-animation-editor.sh",
  "start-animation-editor.bat",
  "start-animation-editor.command",
  "scripts/smoke.mjs",
  "web/index.html",
  "web/app.js",
  "web/styles.css"
];

for (const relative of required) {
  assert(fs.existsSync(path.join(editorRoot, relative)), `missing AnimationEditor ${relative}`);
}

const index = fs.readFileSync(path.join(editorRoot, "web/index.html"), "utf8");
const app = fs.readFileSync(path.join(editorRoot, "web/app.js"), "utf8");
const styles = fs.readFileSync(path.join(editorRoot, "web/styles.css"), "utf8");
const launcher = fs.readFileSync(path.join(editorRoot, "start-animation-editor.sh"), "utf8");
const hubApp = fs.readFileSync(path.join(hubRoot, "web/app.js"), "utf8");
const hubSmoke = fs.readFileSync(path.join(hubRoot, "scripts/smoke.mjs"), "utf8");
const characterApp = fs.readFileSync(path.join(characterStudioRoot, "web/app.js"), "utf8");
const characterSmoke = fs.readFileSync(path.join(characterStudioRoot, "scripts/smoke.mjs"), "utf8");

assert(index.includes("MxFramework 动画编辑器"), "AnimationEditor page should be Chinese-first");
assert(index.includes("animationTree") && index.includes("inspectorPanel") && index.includes("diagnosticsPanel"), "AnimationEditor should expose tree, inspector, and diagnostics panels");
assert(app.includes("/api/authoring/animation/packages"), "AnimationEditor should list animation packages through Authoring API");
assert(app.includes("/api/authoring/animation/load"), "AnimationEditor should load animation package through Authoring API");
assert(app.includes("/api/authoring/animation/save"), "AnimationEditor should save animation package through Authoring API");
assert(app.includes("/api/authoring/animation/validate"), "AnimationEditor should validate animation package through Authoring API");
assert(app.includes("/api/authoring/animation/preview"), "AnimationEditor preview should call compiler-backed preview endpoint");
assert(app.includes("previewAnimation") && app.includes("package: state.packageRelative") && app.includes("animation: state.animation"), "AnimationEditor preview should post { package, animation } to the compiler-backed endpoint");
assert(app.includes("/api/authoring/resources/pick") && app.includes("/api/authoring/resources/resolve-selection"), "AnimationEditor should use shared resource picker APIs");
assert(app.includes("Animation.SourceClip"), "AnimationEditor should use Animation.SourceClip ResourceFieldSpec");
assert(app.includes("sourceSelection") && app.includes("sourceSubClipId") && app.includes("sourceClipName"), "AnimationEditor should persist clip source mapping fields");
assert(index.includes("addClipFromResourceButton") && app.includes("addClipFromResource") && app.includes("newSourceClip"), "AnimationEditor should expose Add Clip from Resource and create clip mappings from picker selections");
assert(app.includes("Runtime Ready Animation Clips") && app.includes("Unity Animation Clips") && app.includes("Unity Model Sub-Clips") && app.includes("Preview-only / Incomplete Sources"), "Animation.SourceClip picker should group runtime-ready, Unity clips, model sub-clips, and incomplete sources");
assert(app.includes("deriveSourceClipDefaults") && app.includes("applySourceClipSelection") && app.includes("sourceClipNameDerived"), "Animation.SourceClip picker should derive clipId, displayName, sourceSubClipId, and sourceClipName instead of requiring manual sourceClipName input");
assert(app.includes("EditorOnly") && app.includes("PreviewOnly") && app.includes("RuntimeReady 优先推荐"), "Animation.SourceClip picker should explain RuntimeReady, EditorOnly, and PreviewOnly states");
assert(app.includes("rootMotionPolicy") && app.includes("RootMotionPolicy"), "AnimationEditor should expose root motion policy editing");
assert(app.includes("runtimeResourceKey") && app.includes("generatedArtifactSelections") && app.includes("metadataText"), "AnimationEditor should edit clip runtime key, generated artifact selections, and metadata");
assert(app.includes("Profiles / Slots") && app.includes("profileId") && app.includes("defaultSetId") && app.includes("defaultGroupId") && app.includes("defaultBlendId") && app.includes("preloadPolicy"), "AnimationEditor should edit profiles and profile slots");
assert(app.includes("Set Runtime Structure") && app.includes("data-add-layer") && app.includes("layerId") && app.includes("avatarMaskSelection"), "AnimationEditor should edit set layers and avatar masks");
assert(app.includes("Action Bindings") && app.includes("data-add-action-binding") && app.includes("actionId") && app.includes("timelineId"), "AnimationEditor should edit action bindings");
assert(app.includes("Compatibility") && app.includes("compatibilityProfileSelection") && app.includes("requiredBoneIdsText") && app.includes("requiredSocketIdsText"), "AnimationEditor should edit compatibility expectations");
assert(app.includes("Warmup") && app.includes("includeDefaultClip") && app.includes("includeFallbackClip") && app.includes("includeActionBindings") && app.includes("includeBlendPoints") && app.includes("requiredClipIds") && app.includes("requiredBlendIds"), "AnimationEditor should edit warmup policy and required clip/blend ids");
assert(app.includes("avatarMaskSelections") && app.includes("vfxSelections") && app.includes("audioCueSelections") && app.includes("additionalResourceSelections"), "AnimationEditor should edit warmup resource selection lists");
assert(app.includes("ANIM_REF_BINDING_CLIP_MISSING") && app.includes("ANIM_REF_SLOT_BLEND_MISSING") && app.includes("ANIM_REF_WARMUP_CLIP_MISSING"), "AnimationEditor should diagnose cross-reference gaps after edits");
assert(app.includes("/api/authoring/animation/compile") && index.includes("compileButton"), "AnimationEditor should expose compile preflight from the UI");
assert(app.includes("blend1D") && app.includes("blend2D") && app.includes("Blend Editor"), "AnimationEditor should expose visual blend editing");
assert(app.includes("WORKSPACE_MODES") && app.includes("资源映射") && app.includes("运行时高级") && app.includes("renderActiveWorkspaceMode"), "AnimationEditor should organize authoring by workflow tabs instead of showing every DTO section at once");
assert(app.includes("1D line") && app.includes("2D plane"), "AnimationEditor should provide 1D line and 2D plane blend views");
assert(app.includes("ANIM_BLEND_POINT_CLIP_MISSING") && app.includes("ANIM_BLEND_POINT_DUPLICATE"), "AnimationEditor should diagnose missing local clip references and duplicate blend coordinates");
assert(app.includes("data-point-field") && app.includes("data-blend-field"), "AnimationEditor should edit blend fields and blend points without raw JSON");
assert(app.includes("Timeline Events") && app.includes("data-add-timeline") && app.includes("data-add-timeline-event"), "AnimationEditor should expose timeline event editing controls");
assert(app.includes("timelineId") && app.includes("Animation.EventVfx") && app.includes("Animation.EventAudioCue"), "AnimationEditor should edit timelines and expose event resource picker hooks");
assert(app.includes("Footstep") && app.includes("TraceOn") && app.includes("TraceOff") && app.includes("HitMarker") && app.includes("Vfx") && app.includes("AudioCue") && app.includes("CameraCue") && app.includes("Custom"), "AnimationEditor should offer required timeline event kinds");
assert(app.includes("Seconds") && app.includes("Normalized") && app.includes("PresentationFrame") && app.includes("CombatFrame"), "AnimationEditor should expose timeline time domains");
assert(app.includes("ANIM_TIMELINE_EVENT_NORMALIZED_RANGE") && app.includes("ANIM_TIMELINE_EVENT_FRAME_NEGATIVE") && app.includes("ANIM_TIMELINE_CLIP_MISSING"), "AnimationEditor should diagnose timeline range and local clip issues");
assert(app.includes("copyTimelineContext") && app.includes("Timeline Event JSON"), "AnimationEditor should copy timeline event JSON context");
assert(app.includes("audioCueId") && !app.includes("Event AudioClip"), "Timeline AudioCue picker should not be treated as an AudioClip selector");
assert(app.includes("Preview / Bake / Compatibility") && app.includes("PREVIEW_TARGET_OPTIONS"), "AnimationEditor should expose preview, bake, and compatibility workflow");
assert(app.includes("runtime authority") && app.includes("Unity scene/prefab"), "Preview workflow should state it is not runtime authority and does not write Unity scene/prefab");
assert(index.includes("previewButton") && app.includes("compilerPreviewPanel") && app.includes("compilerPreviewViewport") && app.includes("previewPlaybackToggle") && app.includes("previewClipList") && app.includes("previewResourceStatus"), "AnimationEditor should expose compiler-backed 3D preview panel controls and DOM hooks");
assert(app.includes("previewResources") && app.includes("animationClips") && app.includes("compileResult") && app.includes("animationResourcePlan"), "AnimationEditor should render compiler preview resource and compile output state");
assert(app.includes("unityPreviewReport") && app.includes("canPreviewInUnity") && app.includes("canPreviewInWeb"), "AnimationEditor should consume Unity preview report capability flags from the preview API");
assert(app.includes("Unity Preview") && app.includes("Web Preview Artifact") && app.includes("Unavailable") && app.includes("getUnityPreviewAuthoritySummary"), "AnimationEditor should clearly label Unity-authoritative, web artifact, and unavailable preview states");
assert(app.includes("renderUnityPreviewAuthorityPanel") && app.includes("ANIM_UNITY_PREVIEW_REPORT_CLIP_MISSING") && app.includes("suggestedFix"), "AnimationEditor should render key Unity preview diagnostics for the selected clip");
assert(app.includes("buildPreviewRequestBody") && app.includes("shouldSendEditorAnimationForPreview") && app.includes("ANIM_PREVIEW_CLIPS_EMPTY"), "AnimationEditor preview should avoid posting an unloaded empty draft and should diagnose empty clip reports distinctly");
assert(app.includes("ANIM_UNITY_PREVIEW_REPORT_ENDPOINT_MISSING") && app.includes("Authoring server 没有返回 unityPreviewReport") && app.includes("服务需重启"), "AnimationEditor should clearly diagnose stale Authoring server preview endpoints");
assert(app.includes("renderSourceSelectionBlock") && app.includes("高级绑定信息") && app.includes("read-only-binding"), "AnimationEditor clip inspector should protect derived source binding fields behind read-only UI");
assert(app.includes("handleClipIdRename") && app.includes("renameClipReferences") && app.includes("Clip ID 已从"), "AnimationEditor should safely propagate local Clip ID renames to references");
assert(index.includes("type=\"importmap\"") && index.includes("/Tools/MxFramework.CharacterStudio/node_modules/three") && app.includes("GLTFLoader") && app.includes("OrbitControls"), "AnimationEditor 3D preview should use CharacterStudio local Three.js runtime when available");
assert(app.includes("AnimationMixer") && app.includes("animationGltf.animations") && app.includes("mixer.setTime") && !app.includes("model.rotation.y ="), "AnimationEditor 3D preview should play real GLTF animation clips instead of fake model rotation");
assert(app.includes("getPreviewDisplayResource") && app.includes("getPreviewAnimationResource") && app.includes("previewModelResource"), "AnimationEditor 3D preview should separate the default preview model from the animation source resource");
assert(app.includes("getPreviewClipIdForSelection") && app.includes("syncPreviewClipToCurrentSelection"), "AnimationEditor 3D preview should follow the currently selected authoring clip");
assert(app.includes("PREVIEW_RETARGET_NAME_MAP") && app.includes("createPreviewPlayableClip") && app.includes("sanitizePreviewNodeName"), "AnimationEditor 3D preview should retarget sample animation tracks onto the preview skeleton model");
assert(app.includes("previewTimelineScrubber") && app.includes("previewSpeedInput") && app.includes("previewLoopToggle") && app.includes("handlePreviewPlaybackInput"), "AnimationEditor 3D preview should expose scrub, speed, and loop controls");
assert(app.includes("ANIM_PREVIEW_GLTF_CLIPS_EMPTY") && app.includes("ANIM_PREVIEW_CLIP_MATCH_MISSING") && app.includes("ANIM_PREVIEW_CLIP_FALLBACK_USED"), "AnimationEditor 3D preview should diagnose GLTF clip availability and match fallback");
assert(app.includes("selectGltfAnimationClip") && app.includes("sourceSubClipId") && app.includes("sourceClipName") && app.includes("normalizeAnimationClipName"), "AnimationEditor 3D preview should match compiled clips to GLTF animation names");
assert(app.includes("getBakeArtifactSummary") && app.includes("generatedArtifactSelections") && app.includes("ANIM_BAKE_ARTIFACT_STALE"), "AnimationEditor should summarize bake artifacts and stale hashes");
assert(app.includes("getCompatibilityReport") && app.includes("ANIM_COMPAT_ROOT_MOTION_POLICY_MISMATCH") && app.includes("ANIM_COMPAT_SKELETON_PROFILE_MISSING"), "AnimationEditor should diagnose compatibility and root motion policy issues");
assert(app.includes("Tools/MxFramework.ResourceLibrary/web/") && app.includes("Tools/MxFramework.CharacterStudio/web/"), "AnimationEditor should link Resource Manager and CharacterStudio");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "AnimationEditor should remain a vanilla web workstation");
assert(!index.includes("react") && !index.includes("vite") && !styles.includes("@vite"), "AnimationEditor should not migrate to a frontend framework or build tool");
assert(styles.includes(".workspace") && styles.includes(".resource-picker-overlay"), "AnimationEditor should style workspace and resource picker overlay");
assert(styles.includes(".picker-group") && styles.includes(".picker-summary") && styles.includes(".source-clip-derived") && styles.includes(".subclip-options"), "AnimationEditor should style grouped source picker rows and read-only derived source clip metadata");
assert(styles.includes(".workspace-mode-tabs") && styles.includes(".workflow-summary-grid"), "AnimationEditor should style workflow tabs and mapping summary cards");
assert(styles.includes(".blend-track") && styles.includes(".blend-plane") && styles.includes(".blend-diagnostics"), "AnimationEditor should style visual blend editors and diagnostics");
assert(styles.includes(".timeline-scrubber") && styles.includes(".timeline-domain-row") && styles.includes(".timeline-event-row"), "AnimationEditor should style timeline scrubber and event list");
assert(styles.includes(".preview-bake-compatibility") && styles.includes(".artifact-table") && styles.includes(".workflow-diagnostics"), "AnimationEditor should style preview, bake, and compatibility workflow");
assert(styles.includes(".compiler-preview-panel") && styles.includes(".compiler-preview-viewport") && styles.includes(".preview-resource-status") && styles.includes(".preview-clip-list"), "AnimationEditor should style compiler-backed 3D preview panel");
assert(styles.includes(".preview-player-controls") && styles.includes(".preview-gltf-status") && styles.includes(".preview-gltf-names"), "AnimationEditor should style 3D playback controls and GLTF match status");
assert(styles.includes(".unity-preview-authority") && styles.includes(".preview-capability-flag") && styles.includes(".unity-preview-diagnostics"), "AnimationEditor should style Unity preview authority, capability flags, and diagnostics");
assert(styles.includes(".clip-source-card") && styles.includes(".clip-advanced-binding") && styles.includes(".field-hint"), "AnimationEditor should style guarded clip binding and helper hints");
assert(styles.includes(".structure-editor") && styles.includes(".runtime-table") && styles.includes(".selection-list-editor"), "AnimationEditor should style full DTO structure editors");
assert(launcher.includes("dotnet --list-sdks") && launcher.includes("is_animation_editor_server_ready"), "AnimationEditor launcher should check .NET and server readiness");
assert(hubApp.includes("Tools/MxFramework.AnimationEditor/web/"), "EditorHub should link Animation Editor");
assert(hubSmoke.includes("MxFramework.AnimationEditor"), "EditorHub smoke should check Animation Editor");
assert(characterApp.includes("Tools/MxFramework.AnimationEditor/web/") && characterApp.includes("打开动画编辑器"), "CharacterStudio should link Animation Editor");
assert(characterSmoke.includes("Tools/MxFramework.AnimationEditor/web/"), "CharacterStudio smoke should check Animation Editor link");

console.log("AnimationEditor smoke ok");

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
