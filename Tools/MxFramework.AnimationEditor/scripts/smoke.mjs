import fs from "node:fs";
import path from "node:path";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
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
assert(app.includes("/api/authoring/resources/pick") && app.includes("/api/authoring/resources/resolve-selection"), "AnimationEditor should use shared resource picker APIs");
assert(app.includes("Animation.SourceClip"), "AnimationEditor should use Animation.SourceClip ResourceFieldSpec");
assert(app.includes("sourceSelection") && app.includes("sourceSubClipId") && app.includes("sourceClipName"), "AnimationEditor should persist clip source mapping fields");
assert(app.includes("rootMotionPolicy") && app.includes("RootMotionPolicy"), "AnimationEditor should expose root motion policy editing");
assert(app.includes("blend1D") && app.includes("blend2D") && app.includes("Blend Editor"), "AnimationEditor should expose visual blend editing");
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
assert(app.includes("Tools/MxFramework.ResourceLibrary/web/") && app.includes("Tools/MxFramework.CharacterStudio/web/"), "AnimationEditor should link Resource Manager and CharacterStudio");
assert(!app.includes("React") && !app.includes("createRoot") && !app.includes("vite"), "AnimationEditor should remain a vanilla web workstation");
assert(styles.includes(".workspace") && styles.includes(".resource-picker-overlay"), "AnimationEditor should style workspace and resource picker overlay");
assert(styles.includes(".blend-track") && styles.includes(".blend-plane") && styles.includes(".blend-diagnostics"), "AnimationEditor should style visual blend editors and diagnostics");
assert(styles.includes(".timeline-scrubber") && styles.includes(".timeline-domain-row") && styles.includes(".timeline-event-row"), "AnimationEditor should style timeline scrubber and event list");
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
