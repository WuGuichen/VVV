using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Editor.ProjectileAuthoring
{
    public enum ProjectilePreviewScenario { Miss, DirectHit, Blocked, AreaHit, Expire, Pierce, LateAttach }
    public enum ProjectilePreviewMarkerKind { Spawn, Flight, Direct, Blocked, Area, Expire, Rebuild, Exit, EffectTimeline, Diagnostic }
    public enum ProjectilePreviewPresentationPhase { Build, Rebuild, OneShot, Exit }

    public readonly struct ProjectilePreviewPresentationIdentity : IEquatable<ProjectilePreviewPresentationIdentity>
    {
        public ProjectilePreviewPresentationIdentity(int frame, int projectileId, int slotKind, int sequence)
        { Frame = frame; ProjectileId = projectileId; SlotKind = slotKind; Sequence = sequence; }
        public int Frame { get; }
        public int ProjectileId { get; }
        public int SlotKind { get; }
        public int Sequence { get; }
        public bool Equals(ProjectilePreviewPresentationIdentity other) => Frame == other.Frame && ProjectileId == other.ProjectileId && SlotKind == other.SlotKind && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is ProjectilePreviewPresentationIdentity other && Equals(other);
        public override int GetHashCode() { unchecked { int h = Frame; h = h * 397 ^ ProjectileId; h = h * 397 ^ SlotKind; return h * 397 ^ Sequence; } }
    }

    public readonly struct ProjectilePreviewPresentationTrace
    {
        internal ProjectilePreviewPresentationTrace(ProjectilePreviewPresentationIdentity identity, ProjectilePreviewPresentationPhase phase,
            string resourceKey, int vfxDefinitionId, int lifetimeFrames, FixVector3 position, FixVector3 forward, string exitMode, string displayText)
        { Identity = identity; Phase = phase; ResourceKey = resourceKey ?? string.Empty; VfxDefinitionId = vfxDefinitionId; LifetimeFrames = lifetimeFrames; Position = position; Forward = forward; ExitMode = exitMode ?? string.Empty; DisplayText = displayText ?? string.Empty; }
        public ProjectilePreviewPresentationIdentity Identity { get; }
        public ProjectilePreviewPresentationPhase Phase { get; }
        public string ResourceKey { get; }
        public int VfxDefinitionId { get; }
        public int LifetimeFrames { get; }
        public FixVector3 Position { get; }
        public FixVector3 Forward { get; }
        public string ExitMode { get; }
        public string DisplayText { get; }
    }

    public readonly struct ProjectilePreviewMarkerIdentity : IEquatable<ProjectilePreviewMarkerIdentity>
    {
        public ProjectilePreviewMarkerIdentity(int frame, int projectileId, ProjectilePreviewMarkerKind kind, int sequence, int slotKind)
        { Frame = frame; ProjectileId = projectileId; Kind = kind; Sequence = sequence; SlotKind = slotKind; }
        public int Frame { get; }
        public int ProjectileId { get; }
        public ProjectilePreviewMarkerKind Kind { get; }
        public int Sequence { get; }
        public int SlotKind { get; }
        public bool Equals(ProjectilePreviewMarkerIdentity other) => Frame == other.Frame && ProjectileId == other.ProjectileId && Kind == other.Kind && Sequence == other.Sequence && SlotKind == other.SlotKind;
        public override bool Equals(object obj) => obj is ProjectilePreviewMarkerIdentity other && Equals(other);
        public override int GetHashCode() { unchecked { int h = Frame; h = h * 397 ^ ProjectileId; h = h * 397 ^ (int)Kind; h = h * 397 ^ Sequence; return h * 397 ^ SlotKind; } }
    }

    public readonly struct ProjectilePreviewMarker
    {
        public ProjectilePreviewMarker(ProjectilePreviewMarkerIdentity identity, FixVector3 position, FixVector3 normal, string effectTimelineId, string diagnosticCode = "")
        { Identity = identity; Position = position; Normal = normal; EffectTimelineId = effectTimelineId ?? string.Empty; DiagnosticCode = diagnosticCode ?? string.Empty; }
        public ProjectilePreviewMarkerIdentity Identity { get; }
        public FixVector3 Position { get; }
        public FixVector3 Normal { get; }
        public string EffectTimelineId { get; }
        public string DiagnosticCode { get; }
    }

    public readonly struct ProjectilePreviewFrame
    {
        public ProjectilePreviewFrame(int frame, bool active, bool consumed, FixVector3 position, FixVector3 forward, int age, int acceptedImpactCount,
            int spawnCount, int directCount, int blockedCount, int areaCount, int expireCount, bool persistentBuild, bool persistentRebuild, bool persistentExit)
        { Frame = frame; Active = active; Consumed = consumed; Position = position; Forward = forward; AgeFrames = age; AcceptedImpactCount = acceptedImpactCount; SpawnCount = spawnCount; DirectCount = directCount; BlockedCount = blockedCount; AreaCount = areaCount; ExpireCount = expireCount; PersistentBuild = persistentBuild; PersistentRebuild = persistentRebuild; PersistentExit = persistentExit; }
        public int Frame { get; }
        public bool Active { get; }
        public bool Consumed { get; }
        public FixVector3 Position { get; }
        public FixVector3 Forward { get; }
        public int AgeFrames { get; }
        public int AcceptedImpactCount { get; }
        public int SpawnCount { get; }
        public int DirectCount { get; }
        public int BlockedCount { get; }
        public int AreaCount { get; }
        public int ExpireCount { get; }
        public bool PersistentBuild { get; }
        public bool PersistentRebuild { get; }
        public bool PersistentExit { get; }
    }

    /// <summary>Immutable low-frequency compile snapshot for one projectile preview.</summary>
    public sealed class ProjectilePreviewCompileInput
    {
        public ProjectilePreviewCompileInput(ProjectileDefinitionListItem definition, ProjectileRequestVariantListItem request, string draftCompileHash,
            ProjectilePreviewScenario scenario, int totalFrames, FixVector3 start, FixVector3 target, string sourcePath, string objectPath,
            int scenarioTargetCount = 3, ProjectilePreviewSpawnFixture? spawnFixture = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Request = request;
            DraftCompileHash = draftCompileHash ?? string.Empty;
            Scenario = scenario;
            TotalFrames = Math.Max(1, totalFrames);
            Start = start;
            Target = target;
            SourcePath = sourcePath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            ScenarioTargetCount = Math.Max(0, scenarioTargetCount);
            SpawnFixture = spawnFixture ?? new ProjectilePreviewSpawnFixture(
                start,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                false,
                start,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                true,
                target);
        }
        public ProjectileDefinitionListItem Definition { get; }
        public ProjectileRequestVariantListItem Request { get; }
        public string DraftCompileHash { get; }
        public ProjectilePreviewScenario Scenario { get; }
        public int TotalFrames { get; }
        public FixVector3 Start { get; }
        public FixVector3 Target { get; }
        public string SourcePath { get; }
        public string ObjectPath { get; }
        public int ScenarioTargetCount { get; }
        public ProjectilePreviewSpawnFixture SpawnFixture { get; }
    }

    /// <summary>Forward-simulated frame cache produced only by the formal CombatProjectileRuntime.</summary>
    /// <remarks>Recompile is LowFreqAlloc. Frame lookup and marker lookup are NoAlloc after initialization.</remarks>
    public sealed class ProjectilePreviewSandbox : IDisposable
    {
        private readonly List<CombatProjectileActiveView> _active = new List<CombatProjectileActiveView>(2);
        private ProjectilePreviewFrame[] _frames = Array.Empty<ProjectilePreviewFrame>();
        private ProjectilePreviewMarker[] _markers = Array.Empty<ProjectilePreviewMarker>();
        private ProjectilePreviewPresentationTrace[] _presentationTraces = Array.Empty<ProjectilePreviewPresentationTrace>();
        private CombatProjectileRuntimeState[] _runtimeStates = Array.Empty<CombatProjectileRuntimeState>();
        private CombatProjectileActiveView[][] _activeByFrame = Array.Empty<CombatProjectileActiveView[]>();
        private string[][] _displayTexts = Array.Empty<string[]>();
        private ProjectilePreviewCompileInput _compileInput;
        private CombatProjectileDefinition _runtimeDefinition;
        private RuntimeFixture _replay;
        private readonly CombatProjectileFrameEventBuffer _replayEvents = new CombatProjectileFrameEventBuffer(2, 2, 16, 2, 2, 4);
        private readonly List<CombatProjectileActiveView> _replayActive = new List<CombatProjectileActiveView>(2);
        private int _replayFrame = -1;
        private int _markerCount;
        private int _presentationTraceCount;
        private bool _disposed;

        public int FrameCount => _frames.Length;
        public int MarkerCount => _markerCount;
        public int PresentationTraceCount => _presentationTraceCount;
        public string DraftCompileHash { get; private set; } = string.Empty;
        public string DiagnosticCode { get; private set; } = string.Empty;
        public ProjectilePreviewSpawnResolution SpawnResolution { get; private set; }

        public bool Recompile(ProjectilePreviewCompileInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProjectilePreviewSandbox));
            DiagnosticCode = string.Empty;
            if (input?.Definition?.detail?.motion == null || input.Definition.detail.collision == null || input.Definition.detail.impact == null)
            { DiagnosticCode = "PROJAUTH021"; return false; }
            if (!ScenarioSupported(input.Definition.detail, input.Scenario))
            { DiagnosticCode = "PROJAUTH026"; return false; }
            try
            {
                CombatProjectileDefinition definition = CreateRuntimeDefinition(input.Definition.detail, input.Scenario);
                CombatProjectileSpawnProfile profile = ProjectilePreviewSpawnResolver.CreateProfile(input.Definition, input.Request, out bool syntheticProfile);
                ProjectilePreviewSpawnResolution spawn = ProjectilePreviewSpawnResolver.Resolve(profile, definition, input.SpawnFixture, syntheticProfile);
                SpawnResolution = spawn;
                if (!spawn.Success)
                {
                    DiagnosticCode = string.IsNullOrEmpty(spawn.DiagnosticCode) ? "PROJAUTH021" : spawn.DiagnosticCode;
                    return false;
                }
                RuntimeFixture fixture = CreateRuntimeFixture(input, definition, spawn, enqueueSpawn: true);
                CombatProjectileRuntime runtime = fixture.Runtime;
                var events = new CombatProjectileFrameEventBuffer(2, 2, 16, 2, 2, 4);
                _frames = new ProjectilePreviewFrame[input.TotalFrames + 1];
                _markers = new ProjectilePreviewMarker[Math.Max(16, (input.TotalFrames + 1) * 8)];
                _runtimeStates = new CombatProjectileRuntimeState[input.TotalFrames + 1];
                _activeByFrame = new CombatProjectileActiveView[input.TotalFrames + 1][];
                _markerCount = 0;
                DraftCompileHash = input.DraftCompileHash;

                for (int frame = 0; frame <= input.TotalFrames; frame++)
                {
                    events.Clear();
                    runtime.Tick(new CombatFrame(frame), events);
                    if (input.Scenario == ProjectilePreviewScenario.LateAttach && frame == 2)
                    {
                        CombatProjectileRuntimeState state = runtime.CreateStateSnapshot();
                        RuntimeFixture attached = CreateRuntimeFixture(input, definition, spawn, enqueueSpawn: false);
                        if (!attached.Runtime.TryRestoreStateSnapshot(state, out _)) { DiagnosticCode = "PROJAUTH021"; return false; }
                        runtime = attached.Runtime;
                    }
                    _active.Clear();
                    runtime.CopyActiveViewsTo(_active);
                    _runtimeStates[frame] = runtime.CreateStateSnapshot();
                    if (input.Scenario == ProjectilePreviewScenario.LateAttach && frame < 2)
                        _activeByFrame[frame] = Array.Empty<CombatProjectileActiveView>();
                    else
                        _activeByFrame[frame] = _active.Count == 0 ? Array.Empty<CombatProjectileActiveView>() : _active.ToArray();
                    RecordFrame(frame, input.Scenario, events);
                }
                _compileInput = input;
                _runtimeDefinition = definition;
                BuildDisplayTexts();
                ResetReplayRuntime();
                return true;
            }
            catch
            {
                DiagnosticCode = "PROJAUTH021";
                _frames = Array.Empty<ProjectilePreviewFrame>();
                _markers = Array.Empty<ProjectilePreviewMarker>();
                _markerCount = 0;
                return false;
            }
        }

        public ref readonly ProjectilePreviewFrame GetFrame(int frame)
        {
            if (_frames.Length == 0) throw new InvalidOperationException("Preview cache is empty.");
            int clamped = Math.Max(0, Math.Min(frame, _frames.Length - 1));
            return ref _frames[clamped];
        }

        public ref readonly ProjectilePreviewMarker GetMarker(int index)
        {
            if ((uint)index >= (uint)_markerCount) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _markers[index];
        }

        public ref readonly ProjectilePreviewPresentationTrace GetPresentationTrace(int index)
        {
            if ((uint)index >= (uint)_presentationTraceCount) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _presentationTraces[index];
        }

        /// <summary>Compiles the independent seven-slot presentation trace from runtime lifecycle markers and the selected binding.</summary>
        /// <remarks>LowFreqAlloc. Timeline playback only reads the resulting immutable trace.</remarks>
        internal void BuildPresentationTrace(ProjectilePresentationBindingListItem binding)
        {
            _presentationTraceCount = 0;
            _presentationTraces = binding?.detail?.slots == null || _frames.Length == 0
                ? Array.Empty<ProjectilePreviewPresentationTrace>()
                : new ProjectilePreviewPresentationTrace[Math.Max(8, _markerCount * 3)];
            if (_presentationTraces.Length == 0)
                return;

            for (int index = 0; index < _markerCount; index++)
            {
                ProjectilePreviewMarker marker = _markers[index];
                switch (marker.Identity.Kind)
                {
                    case ProjectilePreviewMarkerKind.Spawn:
                        AddPresentationTrace(marker, binding.detail.slots.spawn, CombatProjectileUnityPresentationSlotKind.Spawn, ProjectilePreviewPresentationPhase.OneShot, string.Empty);
                        AddPresentationTrace(marker, binding.detail.slots.flightBody, CombatProjectileUnityPresentationSlotKind.FlightBody, ProjectilePreviewPresentationPhase.Build, string.Empty);
                        AddPresentationTrace(marker, binding.detail.slots.trail, CombatProjectileUnityPresentationSlotKind.Trail, ProjectilePreviewPresentationPhase.Build, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Direct:
                        AddPresentationTrace(marker, binding.detail.slots.directImpact, CombatProjectileUnityPresentationSlotKind.DirectImpact, ProjectilePreviewPresentationPhase.OneShot, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Area:
                        AddPresentationTrace(marker, binding.detail.slots.areaImpact, CombatProjectileUnityPresentationSlotKind.AreaImpact, ProjectilePreviewPresentationPhase.OneShot, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Blocked:
                        AddPresentationTrace(marker, binding.detail.slots.blockedImpact, CombatProjectileUnityPresentationSlotKind.BlockedImpact, ProjectilePreviewPresentationPhase.OneShot, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Expire:
                        AddPresentationTrace(marker, binding.detail.slots.expire, CombatProjectileUnityPresentationSlotKind.Expire, ProjectilePreviewPresentationPhase.OneShot, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Rebuild:
                        AddPresentationTrace(marker, binding.detail.slots.flightBody, CombatProjectileUnityPresentationSlotKind.FlightBody, ProjectilePreviewPresentationPhase.Rebuild, string.Empty);
                        AddPresentationTrace(marker, binding.detail.slots.trail, CombatProjectileUnityPresentationSlotKind.Trail, ProjectilePreviewPresentationPhase.Rebuild, string.Empty);
                        break;
                    case ProjectilePreviewMarkerKind.Exit:
                        string exitMode = ResolveExitMode(binding.detail.exitPolicy, marker.Identity);
                        AddPresentationTrace(marker, binding.detail.slots.flightBody, CombatProjectileUnityPresentationSlotKind.FlightBody, ProjectilePreviewPresentationPhase.Exit, exitMode);
                        AddPresentationTrace(marker, binding.detail.slots.trail, CombatProjectileUnityPresentationSlotKind.Trail, ProjectilePreviewPresentationPhase.Exit, exitMode);
                        break;
                }
            }
        }

        /// <summary>Returns a compile-time cached status string for the requested frame and supported playback speed.</summary>
        /// <remarks>NoAlloc after Recompile. Unsupported speed values use the cached 1x text.</remarks>
        public string GetDisplayText(int frame, float playbackSpeed)
        {
            if (_displayTexts.Length == 0)
                return "无预览";
            int speedIndex = playbackSpeed <= .5f ? 0 : playbackSpeed >= 4f ? 3 : playbackSpeed >= 2f ? 2 : 1;
            int clamped = Math.Max(0, Math.Min(frame, _displayTexts[speedIndex].Length - 1));
            return _displayTexts[speedIndex][clamped];
        }

        internal void ClearPreview()
        {
            _active.Clear();
            _replayActive.Clear();
            _replay = null;
            _compileInput = null;
            _runtimeDefinition = null;
            _frames = Array.Empty<ProjectilePreviewFrame>();
            _markers = Array.Empty<ProjectilePreviewMarker>();
            _presentationTraces = Array.Empty<ProjectilePreviewPresentationTrace>();
            _runtimeStates = Array.Empty<CombatProjectileRuntimeState>();
            _activeByFrame = Array.Empty<CombatProjectileActiveView[]>();
            _displayTexts = Array.Empty<string[]>();
            _markerCount = 0;
            _presentationTraceCount = 0;
            DraftCompileHash = string.Empty;
            DiagnosticCode = string.Empty;
            SpawnResolution = default;
        }

        internal void RecordDiagnosticMarker(int frame, FixVector3 position, string code)
        {
            if (_markerCount >= _markers.Length || string.IsNullOrWhiteSpace(code)) return;
            int clamped = Math.Max(0, Math.Min(frame, Math.Max(0, _frames.Length - 1)));
            _markers[_markerCount++] = new ProjectilePreviewMarker(
                new ProjectilePreviewMarkerIdentity(clamped, 0, ProjectilePreviewMarkerKind.Diagnostic, _markerCount, -1),
                position, FixVector3.Zero, string.Empty, code);
        }

        public bool VerifyFrame(int frame, in ProjectilePreviewFrame golden)
        {
            ref readonly ProjectilePreviewFrame cached = ref GetFrame(frame);
            bool match = cached.Position.Equals(golden.Position) && cached.Forward.Equals(golden.Forward) && cached.Active == golden.Active
                && cached.SpawnCount == golden.SpawnCount && cached.DirectCount == golden.DirectCount && cached.AreaCount == golden.AreaCount && cached.ExpireCount == golden.ExpireCount;
            DiagnosticCode = match ? string.Empty : "PROJAUTH023";
            return match;
        }

        /// <summary>Routes one displayed frame through the formal runtime and M5 presentation bridge.</summary>
        /// <remarks>NoAlloc after replay buffers and downstream presentation pools are warmed.</remarks>
        internal void RoutePresentationFrame(
            int frame,
            bool allowForwardOneShots,
            CombatProjectileVfxBridge bridge,
            Action<RuntimeFrame> afterRoute)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProjectilePreviewSandbox));
            if (bridge == null || _compileInput == null || _replay == null || _frames.Length == 0)
                return;

            int clamped = Math.Max(0, Math.Min(frame, _frames.Length - 1));
            bool sequential = allowForwardOneShots && clamped == _replayFrame + 1;
            if (!sequential)
            {
                if (!_replay.Runtime.TryRestoreStateSnapshot(_runtimeStates[clamped], out _))
                    throw new InvalidOperationException("PROJAUTH023: cached projectile runtime state could not be restored.");
                var seekFrame = new RuntimeFrame(clamped);
                bridge.Reset(seekFrame);
                _replayEvents.Clear();
                bridge.ProcessFrame(seekFrame, _replayEvents, _activeByFrame[clamped]);
                _replayFrame = clamped;
                return;
            }

            _replayEvents.Clear();
            _replay.Runtime.Tick(new CombatFrame(clamped), _replayEvents);
            CopyReplayActive(clamped);
            var runtimeFrame = new RuntimeFrame(clamped);
            if (_compileInput.Scenario == ProjectilePreviewScenario.LateAttach)
            {
                if (clamped < 2)
                {
                    _replayEvents.Clear();
                    _replayActive.Clear();
                }
                else if (clamped == 2)
                {
                    bridge.Reset(runtimeFrame);
                    _replayEvents.Clear();
                }
            }
            bridge.ProcessFrame(runtimeFrame, _replayEvents, _replayActive);
            afterRoute?.Invoke(runtimeFrame);
            _replayFrame = clamped;
        }

        internal void ResetPresentationReplay(CombatProjectileVfxBridge bridge)
        {
            if (_compileInput == null) return;
            ResetReplayRuntime();
            bridge?.Reset(RuntimeFrame.Zero);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearPreview();
        }

        private void ResetReplayRuntime()
        {
            if (_replay == null)
                _replay = CreateRuntimeFixture(_compileInput, _runtimeDefinition, SpawnResolution, enqueueSpawn: true);
            else if (!_replay.TryReset())
                throw new InvalidOperationException("PROJAUTH023: preview replay runtime could not restore its initial state.");
            _replayFrame = -1;
            _replayEvents.Clear();
            _replayActive.Clear();
        }

        private void CopyReplayActive(int frame)
        {
            _replayActive.Clear();
            if (_compileInput.Scenario == ProjectilePreviewScenario.LateAttach && frame < 2)
                return;
            _replay.Runtime.CopyActiveViewsTo(_replayActive);
        }

        private void BuildDisplayTexts()
        {
            float[] speeds = { .5f, 1f, 2f, 4f };
            _displayTexts = new string[speeds.Length][];
            for (int speedIndex = 0; speedIndex < speeds.Length; speedIndex++)
            {
                string[] values = new string[_frames.Length];
                for (int frameIndex = 0; frameIndex < values.Length; frameIndex++)
                {
                    ProjectilePreviewFrame frame = _frames[frameIndex];
                    string state = frame.Active ? "飞行" : frame.PersistentExit ? "退出" : "空闲";
                    values[frameIndex] = frameIndex + "/" + (_frames.Length - 1) + "  " + speeds[speedIndex]
                        + "x  " + state + "  标记：" + _markerCount;
                }
                _displayTexts[speedIndex] = values;
            }
        }

        private void RecordFrame(int frame, ProjectilePreviewScenario scenario, CombatProjectileFrameEventBuffer events)
        {
            bool lateHidden = scenario == ProjectilePreviewScenario.LateAttach && frame < 2;
            bool active = !lateHidden && _active.Count > 0;
            FixVector3 position = FixVector3.Zero;
            FixVector3 forward = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            int age = 0, accepted = 0, projectileId = 1;
            if (_active.Count > 0)
            {
                CombatProjectileActiveView view = _active[0];
                position = view.Position; forward = view.Direction; age = view.AgeFrames; accepted = view.AcceptedImpactCount; projectileId = view.ProjectileId;
            }
            else if (events.Expirations.Count > 0) { var value = events.Expirations[0].Projectile; position = value.Position; forward = value.Direction; age = value.AgeFrames; accepted = value.AcceptedImpactCount; projectileId = value.ProjectileId; }
            else if (events.Impacts.Count > 0) { var value = events.Impacts[0].Projectile; position = value.Position; forward = value.Direction; age = value.AgeFrames; accepted = value.AcceptedImpactCount; projectileId = value.ProjectileId; }

            int direct = 0, blocked = 0;
            for (int i = 0; i < events.Impacts.Count; i++)
            {
                CombatProjectileImpactEvent impact = events.Impacts[i];
                if (impact.Context.IsAreaPulse) continue;
                if (impact.ResolveResult.Kind == HitResolveKind.Blocked) blocked++; else direct++;
                AddMarker(frame, projectileId, impact.ResolveResult.Kind == HitResolveKind.Blocked ? ProjectilePreviewMarkerKind.Blocked : ProjectilePreviewMarkerKind.Direct, i, impact.PhysicsHit.Point, impact.PhysicsHit.Normal, ResolveEffectTimelineLabel(impact.EffectTimelineId));
                AddEffectTimelineMarker(frame, projectileId, i, impact.PhysicsHit.Point, impact.EffectTimelineId);
            }
            for (int i = 0; i < events.AreaPulseTriggers.Count; i++)
            {
                AddMarker(frame, projectileId, ProjectilePreviewMarkerKind.Area, i, events.AreaPulseTriggers[i].Center, FixVector3.Zero, ResolveEffectTimelineLabel(events.AreaPulseTriggers[i].EffectTimelineId));
                AddEffectTimelineMarker(frame, projectileId, i, events.AreaPulseTriggers[i].Center, events.AreaPulseTriggers[i].EffectTimelineId);
            }
            for (int i = 0; i < events.Expirations.Count; i++)
            {
                AddMarker(frame, projectileId, ProjectilePreviewMarkerKind.Expire, i, events.Expirations[i].Projectile.Position, FixVector3.Zero, ResolveEffectTimelineLabel(events.Expirations[i].EffectTimelineId));
                AddEffectTimelineMarker(frame, projectileId, i, events.Expirations[i].Projectile.Position, events.Expirations[i].EffectTimelineId);
            }
            if (!lateHidden) for (int i = 0; i < events.Spawns.Count; i++) AddMarker(frame, events.Spawns[i].Projectile.ProjectileId, ProjectilePreviewMarkerKind.Spawn, i, events.Spawns[i].Projectile.Position, events.Spawns[i].Projectile.Direction, string.Empty);
            bool rebuild = scenario == ProjectilePreviewScenario.LateAttach && frame == 2 && active;
            if (rebuild) AddMarker(frame, projectileId, ProjectilePreviewMarkerKind.Rebuild, 0, position, forward, string.Empty);
            bool exit = (events.Expirations.Count > 0 || events.Impacts.Count > 0) && !active;
            if (exit) AddMarker(frame, projectileId, ProjectilePreviewMarkerKind.Exit, 0, position, forward, string.Empty);
            _frames[frame] = new ProjectilePreviewFrame(frame, active, exit, position, forward, age, accepted,
                lateHidden ? 0 : events.Spawns.Count, direct, blocked, events.AreaPulseTriggers.Count, events.Expirations.Count,
                !lateHidden && events.Spawns.Count > 0, rebuild, exit);
        }

        private void AddEffectTimelineMarker(int frame, int projectileId, int sequence, FixVector3 position, int effectTimelineId)
        {
            if (effectTimelineId > 0)
                AddMarker(frame, projectileId, ProjectilePreviewMarkerKind.EffectTimeline, sequence, position, FixVector3.Zero, ResolveEffectTimelineLabel(effectTimelineId));
        }

        private string ResolveEffectTimelineLabel(int effectTimelineId)
        {
            string label = _compileInput?.Definition?.detail?.impact?.effectTimelineId;
            if (!string.IsNullOrWhiteSpace(label)) return label;
            return effectTimelineId > 0 ? effectTimelineId.ToString() : string.Empty;
        }

        private void AddMarker(int frame, int projectileId, ProjectilePreviewMarkerKind kind, int sequence, FixVector3 position, FixVector3 normal, string effectTimelineId)
        {
            if (_markerCount >= _markers.Length) return;
            _markers[_markerCount++] = new ProjectilePreviewMarker(new ProjectilePreviewMarkerIdentity(frame, projectileId, kind, sequence, MarkerSlot(kind)), position, normal, effectTimelineId);
        }

        private void AddPresentationTrace(ProjectilePreviewMarker marker, ProjectilePresentationSlotView slot,
            CombatProjectileUnityPresentationSlotKind slotKind, ProjectilePreviewPresentationPhase phase, string exitMode)
        {
            if (slot == null || _presentationTraceCount >= _presentationTraces.Length)
                return;
            if (!ProjectilePreviewPresentationSession.TryCreateSlot(slot, slotKind, out CombatProjectileUnityPresentationSlot formalSlot))
                return;
            ProjectilePreviewFrame runtimeFrame = _frames[Math.Max(0, Math.Min(marker.Identity.Frame, _frames.Length - 1))];
            CombatProjectileUnityPresentationPose pose = CombatProjectileUnityPresentationPoseResolver.Resolve(
                formalSlot.Orientation,
                formalSlot.LocalOffset,
                marker.Position,
                runtimeFrame.Forward,
                marker.Normal);
            string resource = formalSlot.Resource.ToString();
            var identity = new ProjectilePreviewPresentationIdentity(marker.Identity.Frame, marker.Identity.ProjectileId, (int)slotKind, marker.Identity.Sequence);
            string text = PresentationSlotLabel(slotKind) + " " + PresentationPhaseLabel(phase) + " @" + marker.Identity.Frame
                + "  VFX：" + formalSlot.VfxDefinitionId + "  生命周期：" + formalSlot.LifetimeFramesOverride
                + (string.IsNullOrEmpty(exitMode) ? string.Empty : "  退出：" + exitMode);
            _presentationTraces[_presentationTraceCount++] = new ProjectilePreviewPresentationTrace(identity, phase, resource,
                formalSlot.VfxDefinitionId, formalSlot.LifetimeFramesOverride, pose.Position, pose.Forward, exitMode, text);
        }

        private static string PresentationSlotLabel(CombatProjectileUnityPresentationSlotKind kind)
        {
            switch (kind)
            {
                case CombatProjectileUnityPresentationSlotKind.Spawn: return "出生";
                case CombatProjectileUnityPresentationSlotKind.FlightBody: return "飞行主体";
                case CombatProjectileUnityPresentationSlotKind.Trail: return "拖尾";
                case CombatProjectileUnityPresentationSlotKind.DirectImpact: return "直接命中";
                case CombatProjectileUnityPresentationSlotKind.AreaImpact: return "范围命中";
                case CombatProjectileUnityPresentationSlotKind.BlockedImpact: return "格挡命中";
                case CombatProjectileUnityPresentationSlotKind.Expire: return "过期";
                default: return kind.ToString();
            }
        }

        private static string PresentationPhaseLabel(ProjectilePreviewPresentationPhase phase)
        {
            switch (phase)
            {
                case ProjectilePreviewPresentationPhase.Build: return "创建";
                case ProjectilePreviewPresentationPhase.Rebuild: return "重建";
                case ProjectilePreviewPresentationPhase.OneShot: return "单次";
                case ProjectilePreviewPresentationPhase.Exit: return "退出";
                default: return phase.ToString();
            }
        }

        private string ResolveExitMode(ProjectilePresentationExitPolicyView exit, ProjectilePreviewMarkerIdentity exitIdentity)
        {
            CombatProjectileUnityPresentationExitPolicy formal = ProjectilePreviewPresentationSession.CreateExitPolicy(exit);
            bool blocked = false;
            bool accepted = false;
            for (int index = 0; index < _markerCount; index++)
            {
                ProjectilePreviewMarker marker = _markers[index];
                if (marker.Identity.Frame != exitIdentity.Frame || marker.Identity.ProjectileId != exitIdentity.ProjectileId)
                    continue;
                if (marker.Identity.Kind == ProjectilePreviewMarkerKind.Expire) return formal.Expiration.ToString();
                if (marker.Identity.Kind == ProjectilePreviewMarkerKind.Blocked) blocked = true;
                else if (marker.Identity.Kind == ProjectilePreviewMarkerKind.Direct || marker.Identity.Kind == ProjectilePreviewMarkerKind.Area) accepted = true;
            }
            if (blocked) return formal.BlockedImpact.ToString();
            if (accepted) return formal.AcceptedImpact.ToString();
            return formal.ExternalRemoval.ToString();
        }

        private static int MarkerSlot(ProjectilePreviewMarkerKind kind)
        {
            switch (kind)
            {
                case ProjectilePreviewMarkerKind.Spawn: return (int)CombatProjectileUnityPresentationSlotKind.Spawn;
                case ProjectilePreviewMarkerKind.Direct: return (int)CombatProjectileUnityPresentationSlotKind.DirectImpact;
                case ProjectilePreviewMarkerKind.Area: return (int)CombatProjectileUnityPresentationSlotKind.AreaImpact;
                case ProjectilePreviewMarkerKind.Blocked: return (int)CombatProjectileUnityPresentationSlotKind.BlockedImpact;
                case ProjectilePreviewMarkerKind.Expire: return (int)CombatProjectileUnityPresentationSlotKind.Expire;
                default: return (int)CombatProjectileUnityPresentationSlotKind.FlightBody;
            }
        }

        internal static CombatProjectileDefinition CreateRuntimeDefinition(ProjectileDefinitionDetailView dto, ProjectilePreviewScenario scenario)
        {
            if (dto == null || dto.motion == null || dto.collision == null || dto.impact == null)
                throw new ArgumentException("Projectile preview definition is incomplete.", nameof(dto));
            ProjectileMotionView m = dto.motion;
            CombatProjectileMotionKind motionKind = ParseDefined<CombatProjectileMotionKind>(m.kind);
            CombatProjectileSpeedProfileKind speedProfile = ParseDefined<CombatProjectileSpeedProfileKind>(m.speedProfile?.kind);
            var motion = new CombatProjectileMotionConfig(motionKind, F(m.speedPerFrame), m.lifetimeFrames, F(m.arcHeight), speedProfile,
                F(m.lateralAmplitude), m.lateralCycles, V(m.bezierControlOffsetA), V(m.bezierControlOffsetB), F(m.homing?.turnRatePerFrame ?? 0f),
                F(m.gravityPerFrameSquared), F(m.maxLaunchAngleTangent));
            ProjectileCollisionView c = dto.collision;
            var collision = new CombatProjectileCollisionConfig(F(c.radius), new CombatPhysicsLayerMask(c.layerMask),
                c.stopOnFirstAcceptedImpact, c.stopOnBlockedImpact, c.maxAcceptedImpacts,
                ParseDefined<CombatProjectileHitSameTargetPolicy>(c.hitSameTargetPolicy));
            ProjectileImpactView i = dto.impact;
            CombatProjectileImpactPolicyKind impactKind = ParseDefined<CombatProjectileImpactPolicyKind>(i.policyKind);
            CombatHitEventConfig hit = CreateHitEvent(i.hitEvent);
            CombatHitEventConfig areaHit = CreateHitEvent(i.areaHitEvent);
            var impact = new CombatProjectileImpactConfig(0, 0, impactKind,
                F(i.areaRadius), new CombatPhysicsLayerMask(i.areaLayerMask), i.triggerOnExpire, hit, areaHit, 0, i.presentationProfileId, i.stableEventId, i.effectTimelineId);
            return new CombatProjectileDefinition(dto.definitionId, dto.stableId, motion, collision, impact, dto.physicalEventsEnabled);
        }

        private static bool ScenarioSupported(ProjectileDefinitionDetailView definition, ProjectilePreviewScenario scenario)
        {
            if (scenario == ProjectilePreviewScenario.Pierce)
                return !definition.collision.stopOnFirstAcceptedImpact && definition.collision.maxAcceptedImpacts >= 2;
            if (scenario == ProjectilePreviewScenario.AreaHit)
                return string.Equals(definition.impact.policyKind, "AreaPulseOnImpact", StringComparison.Ordinal);
            if (scenario == ProjectilePreviewScenario.Blocked)
                return definition.impact.hitEvent != null
                    && string.Equals(definition.impact.hitEvent.environmentHitPolicy, "BlockedPresentation", StringComparison.Ordinal);
            return true;
        }

        private static CombatHitEventConfig CreateHitEvent(CombatHitEventView view)
        {
            if (view == null || view.maxAcceptedHits <= 0)
                throw new ArgumentException("Projectile hit-event projection is incomplete.", nameof(view));
            CombatHitTargetTypes targets = CombatHitTargetTypes.None;
            string[] values = view.targetTypes ?? Array.Empty<string>();
            for (int index = 0; index < values.Length; index++)
                targets |= ParseDefined<CombatHitTargetTypes>(values[index]);
            CombatEnvironmentHitPolicy environment = ParseDefined<CombatEnvironmentHitPolicy>(view.environmentHitPolicy);
            CombatHitSameTargetPolicy sameTarget = ParseDefined<CombatHitSameTargetPolicy>(view.hitSameTargetPolicy);
            CombatHitDamageView damageView = view.damage;
            var damage = damageView == null ? default : new CombatHitDamageConfig(
                0,
                damageView.baseDamage,
                damageView.impactPower,
                Array.Empty<int>(),
                0,
                damageView.launchPower, damageView.damageTypeId, damageView.analysisCategory, damageView.tags);
            CombatHitBuffView[] buffViews = view.onHitBuffs ?? Array.Empty<CombatHitBuffView>();
            var buffs = new CombatHitBuffApplicationConfig[buffViews.Length];
            for (int index = 0; index < buffViews.Length; index++)
                buffs[index] = new CombatHitBuffApplicationConfig(buffViews[index].buffId);
            return new CombatHitEventConfig(targets, environment, 0, view.maxAcceptedHits, sameTarget, damage, buffs, view.stableEventId);
        }

        private static T ParseDefined<T>(string value) where T : struct
        {
            if (!Enum.TryParse(value, false, out T parsed) || !Enum.IsDefined(typeof(T), parsed))
                throw new ArgumentException("Unsupported projectile preview enum value: " + (value ?? "<null>"));
            return parsed;
        }

        private static void ConfigureWorld(CombatPhysicsWorld world, CombatProjectileDefinition definition, ProjectilePreviewCompileInput input, in ProjectilePreviewSpawnResolution spawn)
        {
            ProjectilePreviewScenario scenario = input.Scenario;
            if (scenario == ProjectilePreviewScenario.Miss || scenario == ProjectilePreviewScenario.Expire || scenario == ProjectilePreviewScenario.LateAttach) return;
            Fix64 x = spawn.Origin.X + definition.Motion.SpeedPerFrame * Fix64.FromInt(2);
            int collisionLayer = FirstLayer(definition.Collision.LayerMask.Value);
            if (scenario == ProjectilePreviewScenario.Pierce)
            {
                RegisterTarget(world, 2, x - F(.15f), collisionLayer);
                RegisterTarget(world, 3, x, collisionLayer);
                return;
            }
            RegisterTarget(world, scenario == ProjectilePreviewScenario.Blocked ? 90 : 2, x, collisionLayer);
            if (scenario == ProjectilePreviewScenario.AreaHit)
            {
                int areaLayer = FirstLayer(definition.Impact.AreaLayerMask.Value);
                for (int index = 0; index < input.ScenarioTargetCount; index++)
                    RegisterTarget(world, 10 + index, x, areaLayer, F((index - input.ScenarioTargetCount / 2f) * .35f));
            }
        }

        private static RuntimeFixture CreateRuntimeFixture(
            ProjectilePreviewCompileInput input,
            CombatProjectileDefinition definition,
            in ProjectilePreviewSpawnResolution spawn,
            bool enqueueSpawn)
        {
            var registry = new CombatProjectileDefinitionRegistry(new[] { definition });
            var spawns = new CombatProjectileSpawnRequestBuffer();
            var world = new CombatPhysicsWorld();
            var resolver = new PreviewImpactResolver(input.Scenario == ProjectilePreviewScenario.Blocked ? new CombatEntityId(90) : default);
            var tracking = new PreviewTrackingTargetProvider(spawn.TargetPoint);
            ConfigureWorld(world, definition, input, spawn);
            var runtime = new CombatProjectileRuntime(registry, spawns, world, resolver, trackingTargetProvider: tracking);
            CombatProjectileSpawnRequest spawnRequest = default;
            if (enqueueSpawn)
            {
                spawnRequest = new CombatProjectileSpawnRequest(new CombatFrame(0), definition.DefinitionId,
                    new CombatEntityId(1), spawn.Origin, spawn.Direction, true, spawn.TargetPoint, 1, 1, 1, 0,
                    effectiveLifetimeFrames: spawn.EffectiveLifetimeFrames,
                    hasTrackingTarget: spawn.HasTrackingTarget,
                    trackingTargetEntityId: spawn.TrackingTargetEntityId);
                spawns.Enqueue(spawnRequest);
            }
            return new RuntimeFixture(runtime, spawns, spawnRequest, enqueueSpawn);
        }

        private static int FirstLayer(int mask)
        {
            for (int layer = 0; layer < 32; layer++) if ((mask & (1 << layer)) != 0) return layer;
            return 0;
        }

        private static void RegisterTarget(CombatPhysicsWorld world, int entity, Fix64 x, int layer, Fix64 z = default)
        {
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), new CombatBodyId(entity), new FixVector3(x, Fix64.Zero, z)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(new CombatBodyId(entity), new CombatColliderId(1), layer,
                new FixVector3(F(-.1f), F(-.1f), F(-.1f)), new FixVector3(F(.1f), F(.1f), F(.1f))));
        }

        private static Fix64 F(float value) => Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        private static FixVector3 V(ProjectileVector3View value) => value == null ? FixVector3.Zero : new FixVector3(F(value.x), F(value.y), F(value.z));

        private sealed class PreviewTrackingTargetProvider : ICombatProjectileTrackingTargetProvider
        {
            private readonly FixVector3 _target;
            public PreviewTrackingTargetProvider(FixVector3 target) { _target = target; }
            public bool TryResolveTrackingTarget(CombatProjectileTrackingTargetQuery query, out FixVector3 targetPoint)
            { targetPoint = _target + new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromRatio(query.Frame.Value, 10)); return true; }
        }

        private sealed class PreviewImpactResolver : ICombatProjectileImpactResolver
        {
            private const string PreviewAreaEventId = "preview.area";
            private const string PreviewDirectEventId = "preview.direct";

            private readonly CombatEntityId _blocked;
            public PreviewImpactResolver(CombatEntityId blocked) { _blocked = blocked; }
            public bool TryResolveImpact(CombatProjectileSnapshot projectile, CombatQueryResult hit, CombatFrame frame, CombatProjectileImpactContext context, out CombatProjectileImpactPayload payload)
            {
                HitTargetStateFlags state = !_blocked.IsNone && hit.TargetEntityId.Equals(_blocked) ? HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking : HitTargetStateFlags.Alive;
                payload = new CombatProjectileImpactPayload(1, 0, FixVector3.Zero, state, stableEventId: context.IsAreaPulse ? PreviewAreaEventId : PreviewDirectEventId);
                return true;
            }
        }

        private sealed class RuntimeFixture
        {
            private readonly CombatProjectileSpawnRequestBuffer _spawns;
            private readonly CombatProjectileSpawnRequest _spawnRequest;
            private readonly bool _hasSpawn;
            private readonly CombatProjectileRuntimeState _initialState;
            internal RuntimeFixture(CombatProjectileRuntime runtime, CombatProjectileSpawnRequestBuffer spawns,
                CombatProjectileSpawnRequest spawnRequest, bool hasSpawn)
            {
                Runtime = runtime;
                _spawns = spawns;
                _spawnRequest = spawnRequest;
                _hasSpawn = hasSpawn;
                _initialState = runtime.CreateStateSnapshot();
            }
            internal CombatProjectileRuntime Runtime { get; }
            internal bool TryReset()
            {
                if (!Runtime.TryRestoreStateSnapshot(_initialState, out _))
                    return false;
                _spawns.Clear();
                if (_hasSpawn)
                    _spawns.Enqueue(_spawnRequest);
                return true;
            }
        }
    }
}
