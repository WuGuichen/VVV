import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import test from "node:test";
import { fileURLToPath } from "node:url";

// Execute the shipped diagnostic functions without loading the browser or preview.
// An optional source path allows the same fixtures to check an exported commit.
const sourcePath = process.argv[2] || fileURLToPath(new URL("../web/app.js", import.meta.url));
const source = fs.readFileSync(sourcePath, "utf8");
const names = [
  "getLocalClipIds", "getTimelineDurationSeconds", "getTimelineDiagnostics",
  "normalizeEventKind", "formatNumber", "renderTimelineDiagnostics", "escapeHtml"
];
const snippets = names.map(name => {
  const start = source.lastIndexOf(`\nfunction ${name}(`);
  assert.ok(start >= 0, `Missing shipped function: ${name}`);
  const next = source.indexOf("\nfunction ", start + 1);
  return source.slice(start, next < 0 ? source.length : next);
});
const context = vm.createContext({});
vm.runInContext(snippets.join("\n"), context, { filename: sourcePath });

const group = {
  groupId: "g",
  clips: [
    { clipId: "short", metadata: { durationSeconds: "1" } },
    { clipId: "long", metadata: { durationSeconds: "10" } },
    { clipId: "legacy" }
  ]
};
const secondsRange = "ANIM_TIMELINE_EVENT_SECONDS_RANGE";
const cases = [
  { name: "long parent / short event rejects 5 seconds", parent: "long", clip: "short", time: 5, duration: 1 },
  { name: "short parent / long event accepts 5 seconds", parent: "short", clip: "long", time: 5 },
  { name: "same Clip still rejects an overrun", parent: "short", clip: "short", time: 5, duration: 1 },
  { name: "same Clip still accepts a valid time", parent: "long", clip: "long", time: 5 },
  { name: "empty event Clip inherits the parent", parent: "short", clip: "", time: 5, duration: 1 },
  { name: "omitted event Clip inherits the parent", parent: "long", time: 5 },
  { name: "exact duration is inclusive", parent: "short", clip: "short", time: 1 },
  { name: "negative seconds are rejected", parent: "long", clip: "long", time: -0.1, duration: 10 },
  { name: "zero seconds are accepted", parent: "short", clip: "short", time: 0 },
  { name: "override applies to inherited Clip", parent: "short", clip: "", time: 5, override: "8" },
  { name: "override applies to explicit default Clip", parent: "short", clip: "short", time: 9, override: "8", duration: 8 },
  { name: "override duration boundary is inclusive", parent: "short", clip: "short", time: 8, override: "8" },
  { name: "long override cannot hide a short event overrun", parent: "long", clip: "short", time: 5, override: "20", duration: 1 },
  { name: "short override cannot reject a valid long event", parent: "short", clip: "long", time: 5, override: "0.25" },
  { name: "short override does not shorten another short Clip", parent: "long", clip: "short", time: 0.5, override: "0.25" },
  { name: "another Clip duration boundary stays inclusive", parent: "long", clip: "short", time: 1, override: "20" },
  { name: "legacy Clip without duration retains one-second fallback", parent: "long", clip: "legacy", time: 2, duration: 1 },
  { name: "legacy default Clip retains one-second fallback", parent: "legacy", clip: "", time: 1 },
  { name: "empty override falls back to default Clip metadata", parent: "short", clip: "", time: 2, override: "", duration: 1 },
  { name: "numeric zero override retains legacy fallback", parent: "short", clip: "", time: 0.5, override: 0 },
  { name: "string zero override retains legacy zero duration", parent: "short", clip: "", time: 0.5, override: "0", duration: 0 }
];

function event(clipId, time, timeDomain = "Seconds", eventId = "e") {
  return { eventId, clipId, timeDomain, time, eventKind: "Custom", payloadJson: "{}" };
}

function inspect(timeline) {
  const before = JSON.stringify({ group, timeline });
  const diagnostics = JSON.parse(JSON.stringify(context.getTimelineDiagnostics(group, timeline)));
  assert.equal(JSON.stringify({ group, timeline }), before, "Diagnostics must not mutate authoring data");
  return diagnostics;
}

for (const fixture of cases) {
  test(fixture.name, () => {
    const diagnostics = inspect({
      timelineId: "t", clipId: fixture.parent, timeDomain: "Seconds",
      metadata: { durationSeconds: fixture.override }, events: [event(fixture.clip, fixture.time)]
    });
    if (fixture.duration === undefined) {
      assert.deepEqual(diagnostics, []);
      assert.match(context.renderTimelineDiagnostics(diagnostics), /当前事件列表可用于后续编译/);
    } else {
      assert.deepEqual(diagnostics, [{
        tone: "error", code: secondsRange,
        message: `Event 1 seconds 超出 clip duration ${fixture.duration}。`
      }]);
      const html = context.renderTimelineDiagnostics(diagnostics);
      assert.match(html, /class="error"/);
      assert.doesNotMatch(html, /当前事件列表可用于后续编译/);
    }
  });
}

test("mixed events resolve their own durations within one Timeline", () => {
  const diagnostics = inspect({
    timelineId: "t", clipId: "long", metadata: { durationSeconds: "20" },
    events: [event("short", 5, "Seconds", "short-event"), event("long", 5, "Seconds", "long-event"), event("", 21, "Seconds", "inherited-event")]
  });
  assert.deepEqual(diagnostics, [
    { tone: "error", code: secondsRange, message: "Event 1 seconds 超出 clip duration 1。" },
    { tone: "error", code: secondsRange, message: "Event 3 seconds 超出 clip duration 20。" }
  ]);
});

test("event domain still inherits the Timeline Seconds domain", () => {
  const diagnostics = inspect({ timelineId: "t", clipId: "long", timeDomain: "Seconds", events: [event("short", 5, "")] });
  assert.deepEqual(diagnostics.map(item => item.code), [secondsRange]);
});

test("missing event Clip retains its reference diagnostic", () => {
  const diagnostics = inspect({ timelineId: "t", clipId: "long", events: [event("missing", 0.5)] });
  assert.deepEqual(diagnostics.map(item => item.code), ["ANIM_TIMELINE_EVENT_CLIP_MISSING"]);
});

test("valid explicit event Clip is independent of a missing parent Clip", () => {
  const diagnostics = inspect({ timelineId: "t", clipId: "missing", events: [event("long", 5)] });
  assert.deepEqual(diagnostics.map(item => item.code), ["ANIM_TIMELINE_CLIP_MISSING"]);
});

test("Normalized and frame domains retain their existing checks", () => {
  const diagnostics = inspect({
    timelineId: "t", clipId: "long", metadata: { durationSeconds: "0.25" },
    events: [
      event("short", 0.5, "Normalized", "normalized-ok"),
      event("long", 1.1, "Normalized", "normalized-bad"),
      event("short", 500, "PresentationFrame", "presentation-ok"),
      event("long", -1, "CombatFrame", "combat-bad")
    ]
  });
  assert.deepEqual(diagnostics.map(item => item.code), ["ANIM_TIMELINE_EVENT_NORMALIZED_RANGE", "ANIM_TIMELINE_EVENT_FRAME_NEGATIVE"]);
});
