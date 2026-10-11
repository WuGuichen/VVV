using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using System.IO;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Input;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Opt-in Player acceptance through the existing input and action command chain.</summary>
    internal sealed class CharacterTestPlayerGameplayProbe : IDisposable
    {
        internal const string TraceFileName = "CharacterTestPlayerGameplayProbe.csv";
        private readonly CharacterTestRuntimeContext _context;
        private readonly FakeInputProvider _input = new FakeInputProvider();
        private readonly StringBuilder _trace = new StringBuilder("frame,action,grounded,rootY,hipsY,feetY\n");
        private CharacterTestCharacterEntity _player;
        private CharacterTestPlayerVfx455Probe _vfx455;
        private long _startFrame;
        private long _lastFrame = -1;
        private int _waitingUpdates;
        private bool _bound;
        private bool _baseline;
        private bool _attackSent;
        private bool _jumpSent;
        private bool _jumpReleased;
        private float _startX, _startY, _startZ, _baseHips;
        private float _minimumHips = float.MaxValue;
        private float _maximumDistance, _maximumJumpHeight;
        private bool _actionsReady, _moved, _attack, _attackAnimation, _jump, _jumpAnimation, _airborne, _landed;

        internal CharacterTestPlayerGameplayProbe(CharacterTestRuntimeContext context) { _context = context; }
        internal bool Completed { get; private set; }
        internal bool Passed { get; private set; }
        internal string Issue { get; private set; }

        internal void BeginFrame()
        {
            if (Completed) return;
            if (_player == null)
            {
                _player = _context.Slice.Services.Commands.FindLocalPlayerEntity();
                if (_player == null)
                {
                    if (++_waitingUpdates > 600) Finish(false, "No local player appeared for gameplay acceptance.");
                    return;
                }
                var runtime = _context.Slice.Services.ActionRuntime;
                if (runtime == null || !runtime.IsReady
                    || runtime.ConfigSourceKind != CharacterTestCharacterActionConfigSourceKind.ExternalJson)
                {
                    Finish(false, "Formal Character Action configuration is not ready in Player.");
                    return;
                }
                foreach (var entity in _context.Slice.Services.CharacterRuntime.Entities)
                    if (!runtime.TryGetEntityActionSet(entity.Id, out _))
                    {
                        Finish(false, "Missing Player ActionSet: " + entity.Identity.CharacterStableId
                            + " / " + entity.Equipment.EquipmentStateStableId);
                        return;
                    }
                _actionsReady = true;
                _startFrame = _context.Slice.CurrentFrame.Value;
                _input.SetContext(InputContext.Gameplay);
                _context.Slice.Services.SetLocalInputProvider(_input);
                _bound = true;
                if (Environment.GetEnvironmentVariable("MX_CHARACTERTEST_VFX455_ACCEPTANCE") == "1")
                    _vfx455 = new CharacterTestPlayerVfx455Probe(_context, _player);
            }
            long elapsed = _context.Slice.CurrentFrame.Value - _startFrame;
            _vfx455?.BeginFrame(elapsed);
            Vector2 move = elapsed >= 45 && elapsed < 70 ? Vector2.up
                : elapsed >= 110 && elapsed < 135 ? Vector2.down : Vector2.zero;
            bool jumpHeld = elapsed >= 320 && elapsed < 335;
            _input.SetSnapshot(new InputSnapshot(move, Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero, 0f, false, jumpHeld, false, false, false, false, false,
                false, false, false, false, false, false));
            if (elapsed >= 180 && !_attackSent)
            {
                _input.Enqueue(new InputCommand(_context.Slice.CurrentFrame.Value, 0, InputIntent.AttackPrimary));
                _attackSent = true;
            }
            if (elapsed >= 320 && !_jumpSent)
            {
                _input.Enqueue(new InputCommand(_context.Slice.CurrentFrame.Value, 0, InputIntent.Jump));
                _jumpSent = true;
            }
            if (elapsed >= 335 && !_jumpReleased)
            {
                _input.Enqueue(new InputCommand(_context.Slice.CurrentFrame.Value, 0, InputIntent.Jump,
                    InputCommandPhase.Released));
                _jumpReleased = true;
            }
        }

        /// <remarks>AllocByDesign: opt-in acceptance sampling occurs before the measured performance interval.</remarks>
        internal void EndFrame()
        {
            if (Completed || _player == null) return;
            long frame = _context.Slice.CurrentFrame.Value;
            if (frame == _lastFrame) return;
            _lastFrame = frame;
            long elapsed = frame - _startFrame;
            _vfx455?.EndFrame(elapsed);
            var bridge = _context.Slice.Services.CharacterRuntime.ViewBridge;
            bool hasPose = bridge.TryGetHumanoidPresentationHeights(_player.Id,
                out float rootY, out float hipsY, out float feetY);
            if (elapsed >= 30 && !_baseline)
            {
                if (!hasPose || !_player.IsGrounded)
                {
                    if (elapsed >= 120) Finish(false, "Cannot establish a grounded humanoid Player pose baseline.");
                    return;
                }
                _startX = _player.Transform.X; _startY = _player.Transform.Y; _startZ = _player.Transform.Z;
                _baseHips = hipsY - rootY;
                _baseline = true;
            }
            if (elapsed >= 45 && elapsed < 180 && _baseline)
            {
                float dx = _player.Transform.X - _startX, dz = _player.Transform.Z - _startZ;
                _maximumDistance = Mathf.Max(_maximumDistance, Mathf.Sqrt(dx * dx + dz * dz));
                _moved |= _maximumDistance > 0.2f;
                if (hasPose) _minimumHips = Mathf.Min(_minimumHips, hipsY - rootY);
            }
            var runtime = _context.Slice.Services.ActionRuntime;
            if (runtime.Registry.TryGetAction(_player.Action.ActiveActionId, out var action))
            {
                if (elapsed >= 180 && elapsed < 320 && action.Category == CharacterActionCategory.BasicAttack)
                {
                    _attack = true;
                    var diagnostics = bridge.CreateAnimationDiagnostics();
                    foreach (var entry in diagnostics.Entries)
                        if (entry.EntityId == _player.Id.Value && MatchesAuthoredClip(action, entry)) _attackAnimation = true;
                }
                if (elapsed >= 320 && action.Category == CharacterActionCategory.Jump)
                {
                    _jump = true;
                    foreach (var entry in bridge.CreateAnimationDiagnostics().Entries)
                        if (entry.EntityId == _player.Id.Value && MatchesAuthoredClip(action, entry)) _jumpAnimation = true;
                }
            }
            if (elapsed >= 320 && _baseline)
            {
                _maximumJumpHeight = Mathf.Max(_maximumJumpHeight, _player.Transform.Y - _startY);
                if (!_player.IsGrounded && _maximumJumpHeight > 0.15f) _airborne = true;
                if (_airborne && _player.IsGrounded) _landed = true;
            }
            _trace.Append(frame).Append(',').Append(_player.Action.ActiveActionId).Append(',')
                .Append(_player.IsGrounded).Append(',').Append(F(rootY)).Append(',')
                .Append(F(hipsY)).Append(',').AppendLine(F(feetY));
            if (elapsed < 540) return;
            bool poseStable = _baseline && _baseHips > 0f && _minimumHips >= _baseHips * 0.7f;
            bool passed = _actionsReady && _moved && _attack && _attackAnimation && _jump && _jumpAnimation && _airborne && _landed && poseStable;
            if (passed && _vfx455 != null)
            {
                if (!_vfx455.Completed && elapsed < 780) return;
                Finish(_vfx455.Passed, _vfx455.Passed ? null : "VFX455 probe failed: " + _vfx455.Issue);
                return;
            }
            Finish(passed, passed ? null : "Gameplay probe failed: move=" + _moved + " attack=" + _attack
                + " attackAnimation=" + _attackAnimation + " jump=" + _jump + " jumpAnimation=" + _jumpAnimation + " airborne=" + _airborne
                + " landed=" + _landed + " stableGroundedPose=" + poseStable
                + " actionReject=" + _player.Action.LastRejection);
        }

        internal void WriteTrace(string reportDirectory)
        {
            File.WriteAllText(Path.Combine(reportDirectory, TraceFileName), _trace.ToString());
            _vfx455?.WriteTrace(reportDirectory);
        }

        internal void AppendTo(StringBuilder report)
        {
            report.Append("gameplayProbe.status: ").AppendLine(Completed ? Passed ? "Passed" : "Failed" : "Incomplete");
            report.Append("gameplayProbe.actionsReady: ").AppendLine(B(_actionsReady));
            report.Append("gameplayProbe.moved: ").AppendLine(B(_moved));
            report.Append("gameplayProbe.attack: ").AppendLine(B(_attack));
            report.Append("gameplayProbe.attackAnimation: ").AppendLine(B(_attackAnimation));
            report.Append("gameplayProbe.jump: ").AppendLine(B(_jump));
            report.Append("gameplayProbe.jumpAnimation: ").AppendLine(B(_jumpAnimation));
            report.Append("gameplayProbe.airborne: ").AppendLine(B(_airborne));
            report.Append("gameplayProbe.landed: ").AppendLine(B(_landed));
            report.Append("gameplayProbe.moveDistanceMeters: ").AppendLine(F(_maximumDistance));
            report.Append("gameplayProbe.jumpHeightMeters: ").AppendLine(F(_maximumJumpHeight));
            report.Append("gameplayProbe.baselineHipsAboveRoot: ").AppendLine(F(_baseHips));
            report.Append("gameplayProbe.minimumHipsAboveRoot: ").AppendLine(F(_minimumHips));
            _vfx455?.AppendTo(report);
            if (!string.IsNullOrEmpty(Issue)) report.Append("gameplayProbe.issue: ").AppendLine(Issue);
        }

        private void Finish(bool passed, string issue)
        {
            Passed = passed; Issue = issue; Completed = true; Dispose();
        }

        private static bool MatchesAuthoredClip(CharacterActionConfig action, CharacterTestRuntimeCharacterAnimationDiagnostics entry)
        {
            if (!entry.LastActionResultSuccess) return false;
            foreach (var evt in action.AnimationTrack.Events)
                if (!string.IsNullOrEmpty(evt.AnimationClipResourceKey)
                    && entry.LastActionKey == evt.EffectiveAnimationKey
                    && entry.LastActionClipKey == evt.AnimationClipResourceKey) return true;
            return false;
        }

        public void Dispose()
        {
            _vfx455?.Dispose();
            if (!_bound) return;
            _context.Slice?.Services.SetLocalInputProvider(_context.InputProvider);
            _bound = false;
        }

        private static string F(float value) => value.ToString("F5", CultureInfo.InvariantCulture);
        private static string B(bool value) => value ? "true" : "false";
    }

    /// <summary>Explicit opt-in Player evidence for action 455; observes production authority and pooled views.</summary>
    /// <remarks>AllocByDesign: diagnostic snapshots/reflection/CSV only before the measured performance interval.</remarks>
    internal sealed class CharacterTestPlayerVfx455Probe : IVfxRuntimeEventSink, IDisposable
    {
        private const int ActionId = 455, EventId = 391;
        private readonly CharacterTestRuntimeContext _context;
        private CharacterTestCharacterEntity _player;
        private bool _spawnRequested, _cleanupRequested;
        private int _previousMaximumEntityId;
        private readonly List<VfxInstanceSnapshot> _instances = new List<VfxInstanceSnapshot>(16);
        private readonly HashSet<long> _seen = new HashSet<long>();
        private readonly HashSet<GameObject> _available = new HashSet<GameObject>();
        private readonly StringBuilder _trace = new StringBuilder("frame,play,action,localFrame,authorityInstance,vfxInstance,viewId,viewActive,slices,vertices,pooled\n");
        private readonly FieldInfo _activeField = typeof(UnityVfxPresenter).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly FieldInfo _poolField = typeof(UnityVfxPresenter).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
        private long _authorityId, _vfxId;
        private int _play, _normalVertices, _repeatVertices;
        private string _normalViewId, _repeatViewId;
        private GameObject _view;
        private UnityBladeTrailBackend _backend;
        private bool _normalReturned, _repeatReturned, _normalActionFinished, _interruptAccepted, _interruptPending, _repeatStarted, _repeatFromPool;
        private long _normalExitFrame, _normalReturnFrame, _interruptFrame, _repeatExitFrame, _repeatReturnFrame;
        private string _shader, _material, _prefabKey;
        private bool _normalStopObserved, _repeatStopObserved, _forcedExitRequest;
        private int _normalStopLocalFrame, _interruptLocalFrame;
        private long _normalAuthorityId, _repeatAuthorityId, _normalVfxId, _repeatVfxId;
        private readonly VfxRuntimeEventFanout _fanout;
        private VfxStopMode _interruptionStopMode;
        private readonly FieldInfo _sourceField = typeof(UnityBladeTrailBackend).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly FieldInfo _clockField = typeof(UnityBladeTrailBackend).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly FieldInfo _leaseField = typeof(UnityBladeTrailBackend).GetField("_leased", BindingFlags.Instance | BindingFlags.NonPublic);
        private bool _activeFreezePassed, _tailFreezePassed, _freezeTail, _normalTailObserved, _bindingReleased;
        private int _freezeUpdatesLeft, _freezeChecks, _freezeLocalFrame, _freezeSlices, _followSamples;
        private long _freezeFrame;
        private double _freezeClock;
        private float _previousTimeScale, _maximumAnchorError, _tipTravel;
        private Vector3[] _freezeVertices;
        private Color32[] _freezeColors;
        private Vector3 _lastFollowTip;
        private string _actualCharacterStableId = "unavailable";


        internal CharacterTestPlayerVfx455Probe(CharacterTestRuntimeContext context, CharacterTestCharacterEntity player)
        {
            _context = context; _player = player;
            _fanout = context.Slice.Services.Modules.VfxEventFanout;
            if (_fanout == null) throw new InvalidOperationException("VFX event fanout unavailable.");
            _fanout.AddSink(this);
        }
        internal bool Completed { get; private set; }
        internal bool Passed => Completed && string.IsNullOrEmpty(Issue);
        internal string Issue { get; private set; } = "Lifecycle evidence incomplete.";

        internal void BeginFrame(long elapsed)
        {
            if (Completed) return;
            try
            {
                var runtime = _context.Slice.Services.ActionRuntime;
                if (_freezeUpdatesLeft > 0)
                {
                    var mesh = _backend.GetComponent<MeshFilter>().sharedMesh;
                    var vertices = mesh.vertices;
                    var colors = mesh.colors32;
                    if (Time.timeScale != 0f || _context.Slice.CurrentFrame.Value != _freezeFrame
                        || _player.Action.LocalFrame != _freezeLocalFrame || _backend.SliceCount != _freezeSlices
                        || (double)_clockField.GetValue(_backend) != _freezeClock
                        || vertices.Length != _freezeVertices.Length || colors.Length != _freezeColors.Length)
                    { Fail("Paused455 authority/trail clock or geometry changed."); return; }
                    for (int i = 0; i < vertices.Length; i++)
                        if ((_backend.transform.TransformPoint(vertices[i]) - _freezeVertices[i]).sqrMagnitude > .000001f
                            || !colors[i].Equals(_freezeColors[i]))
                        { Fail("Paused455 world mesh or fade changed."); return; }
                    _freezeChecks++;
                    if (_freezeUpdatesLeft == 1)
                    {
                        if (_freezeTail) _tailFreezePassed = true; else _activeFreezePassed = true;
                        RestoreTimeScale();
                    }
                    else _freezeUpdatesLeft--;
                    return;
                }

                if (_play == 0 && elapsed >= 540)
                {
                    var services = _context.Slice.Services;
                    if (!_spawnRequested)
                    {
                        foreach (var entity in services.CharacterRuntime.Entities)
                            _previousMaximumEntityId = Math.Max(_previousMaximumEntityId, entity.Id.Value);
                        _spawnRequested = true;
                        _player = null;
                        if (!services.Modules.CharacterSpawnFlow.RequestSpawn(new CharacterTestCharacterSpawnSpec(
                            CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, 1, 8f, 1f, 0f,
                            CharacterTestCharacterControlOwner.None)))
                        { Fail("Formal default-character spawn rejected."); return; }
                        _player = null;
                    }
                    foreach (var entity in services.CharacterRuntime.Entities)
                        if (entity.Id.Value > _previousMaximumEntityId && entity.Identity.CharacterStableId == "char.000001")
                            _player = entity;
                    if (_player == null || !_player.IsGrounded
                        || !services.CharacterRuntime.ViewBridge.TryGetBladeTrailView(_player.Id.Value, out _)) return;
                    if (!runtime.TryStartActionSlot(_player.Id, (int)CharacterCancelInputMask.AttackPrimary,
                        new CharacterActionId(ActionId), _context.Slice.CurrentFrame))
                    { Fail("Normal action 455 was rejected: " + _player.Action.LastRejection); return; }
                    _actualCharacterStableId = _player.Identity.CharacterStableId;
                    _play = 1;
                    _authorityId = runtime.GetActionInstanceForObservation(_player.Id).InstanceId;
                    _normalAuthorityId = _authorityId;
                }
                if (_interruptPending && !_interruptAccepted)
                {
                    var authority = runtime.GetActionInstanceForObservation(_player.Id);
                    if (authority == null || authority.InstanceId != _authorityId || !authority.IsRunning
                        || _player.Action.ActiveActionId.Value != ActionId || _player.Action.LocalFrame >= 38)
                    { Fail("Interruption missed the active 455 blade-trail window."); return; }
                    _interruptLocalFrame = _player.Action.LocalFrame;
                    _interruptFrame = _context.Slice.CurrentFrame.Value;
                    _interruptAccepted = runtime.TerminateForLifecycle(_player.Id, _context.Slice.CurrentFrame,
                        CharacterActionLifecycleReason.ConfigurationReload, default);
                    _interruptPending = false;
                    if (!_interruptAccepted) { Fail("Gateway rejected ConfigurationReload termination."); return; }
                    foreach (var request in _player.VfxRequests)
                        if (request.Metadata.InstanceId == _authorityId && request.Metadata.StableEventId == EventId
                            && request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit
                            && request.Metadata.LifecycleExitReason == CharacterActionTrackExitReason.ConfigurationReload)
                        {
                            _forcedExitRequest = true;
                            _interruptionStopMode = request.VfxConfig.StopPolicy == CharacterActionVfxStopPolicy.Immediate ? VfxStopMode.Immediate
                                : request.VfxConfig.StopPolicy == CharacterActionVfxStopPolicy.Dissipate ? VfxStopMode.Dissipate : VfxStopMode.Complete;
                        }
                    if (!_forcedExitRequest) { Fail("Gateway did not project the matching ConfigurationReload VFX exit request."); return; }
                }
                if (elapsed >= 560 && _normalReturned && _normalActionFinished && !_repeatStarted)
                {
                    CapturePooledViews();
                    _repeatStarted = true; _play = 2; _seen.Clear(); _view = null; _backend = null; _vfxId = 0;
                    if (!runtime.TryStartActionSlot(_player.Id, (int)CharacterCancelInputMask.AttackPrimary,
                        new CharacterActionId(ActionId), _context.Slice.CurrentFrame))
                    { Fail("Repeat action 455 was rejected: " + _player.Action.LastRejection); return; }
                    _authorityId = runtime.GetActionInstanceForObservation(_player.Id).InstanceId;
                    _repeatAuthorityId = _authorityId;
                }
            }
            catch (Exception ex) { Fail(ex.GetType().Name + ": " + ex.Message); }
        }

        internal void EndFrame(long elapsed)
        {
            if (Completed) return;
            try
            {
                var services = _context.Slice.Services;
                if (_play == 0)
                { if (elapsed >= 775) Fail("Formal default-character spawn/view readiness timed out."); return; }
                if (_play == 1 && elapsed >= 280 && _player.Action.ActiveActionId.Value != ActionId) _normalActionFinished = true;
                _instances.Clear(); services.VfxRuntime.CopyActiveInstances(_instances);
                bool active = false;
                foreach (var instance in _instances)
                {
                    if (instance.SourceDomain != VfxSourceDomains.ActionPresentation || instance.SourceId != _player.Id.Value
                        || instance.SourceFrame != _authorityId || instance.TriggerId != EventId) continue;
                    active = true; _seen.Add(instance.InstanceId.Value); _vfxId = instance.InstanceId.Value;
                    _prefabKey = instance.PrefabResource.ToString();
                    if (_play == 1) _normalVfxId = _vfxId; else _repeatVfxId = _vfxId;
                    if (_seen.Count != 1) { Fail("Duplicate VFX instances for a single 455 authority/event identity."); return; }
                    if (_view == null)
                    {
                        if (_activeField == null) throw new InvalidOperationException("Presenter active metadata unavailable.");
                        var dictionary = (IDictionary)_activeField.GetValue(services.VfxPresenter);
                        object presentation = dictionary[_vfxId];
                        if (presentation == null) throw new InvalidOperationException("Runtime VFX has no presenter view.");
                        _view = (GameObject)presentation.GetType().GetProperty("View").GetValue(presentation);
                        _backend = _view.GetComponentInChildren<UnityBladeTrailBackend>(true);
                        if (_backend == null) { Fail("455 view has no UnityBladeTrailBackend."); return; }
                        var material = _backend.GetComponent<MeshRenderer>().sharedMaterial;
                        if (material == null || material.shader == null || !material.shader.isSupported)
                        { Fail("Blade trail material/shader is unavailable in Player."); return; }
                        _shader = material.shader.name; _material = material.name;
                        if (_play == 1) _normalViewId = _view.GetEntityId().ToString();
                        else { _repeatViewId = _view.GetEntityId().ToString(); _repeatFromPool = _available.Contains(_view); }
                    }
                }
                int vertices = _backend == null || _backend.GetComponent<MeshFilter>().sharedMesh == null
                    ? 0 : _backend.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                if (_play == 1) _normalVertices = Math.Max(_normalVertices, vertices);
                else _repeatVertices = Math.Max(_repeatVertices, vertices);
                if (_play == 1 && active && vertices >= 4 && _player.Action.LocalFrame >= 20)
                {
                    var source = (IUnityBladeTrailPoseSource)_sourceField.GetValue(_backend);
                    if (source == null || !source.TryGetPresentedPose(out UnityBladeTrailPose pose))
                    { Fail("455 has no authenticated equipped-weapon pose source."); return; }
                    var meshVertices = _backend.GetComponent<MeshFilter>().sharedMesh.vertices;
                    Vector3 root = _backend.transform.TransformPoint(meshVertices[meshVertices.Length - 2]);
                    Vector3 tip = _backend.transform.TransformPoint(meshVertices[meshVertices.Length - 1]);
                    float error = Mathf.Max(Vector3.Distance(root, pose.Root), Vector3.Distance(tip, pose.Tip));
                    _maximumAnchorError = Mathf.Max(_maximumAnchorError, error);
                    if (error > .02f) { Fail("455 mesh does not follow the actual blade locators."); return; }
                    if (_followSamples > 0) _tipTravel += Vector3.Distance(tip, _lastFollowTip);
                    _lastFollowTip = tip; _followSamples++;
                    if (!_activeFreezePassed && _freezeUpdatesLeft == 0 && _player.Action.LocalFrame >= 25
                        && _player.Action.LocalFrame < 35) BeginFreeze(false);
                }
                long frame = _context.Slice.CurrentFrame.Value;
                bool pooled = _view != null && IsInPool(_view);
                if (!active && _vfxId != 0)
                {
                    if (_play == 1 && _normalExitFrame == 0) _normalExitFrame = frame;
                    if (_play == 2 && _repeatExitFrame == 0) _repeatExitFrame = frame;
                    if (_play == 1 && vertices > 0 && _view.activeSelf && _normalStopObserved)
                    {
                        _normalTailObserved = true;
                        if (!_tailFreezePassed && _freezeUpdatesLeft == 0) BeginFreeze(true);
                    }
                    bool bindingReleased = _sourceField.GetValue(_backend) == null && !(bool)_leaseField.GetValue(_backend);
                    bool returned = pooled && !_view.activeSelf && _backend.IsPresentationComplete && _backend.SliceCount == 0
                        && vertices == 0 && bindingReleased;
                    if (returned) _bindingReleased = true;
                    if (_play == 1 && returned && !_normalReturned) { _normalReturned = true; _normalReturnFrame = frame; }
                    if (_play == 2 && returned && !_repeatReturned) { _repeatReturned = true; _repeatReturnFrame = frame; }
                }
                if (_play == 2 && active && vertices > 0 && _player.Action.ActiveActionId.Value == ActionId
                    && _player.Action.LocalFrame >= 24 && _player.Action.LocalFrame < 38 && !_interruptAccepted) _interruptPending = true;
                _trace.Append(frame).Append(',').Append(_play).Append(',').Append(_player.Action.ActiveActionId.Value).Append(',')
                    .Append(_player.Action.LocalFrame).Append(',').Append(_authorityId).Append(',').Append(_vfxId).Append(',')
                    .Append(_view == null ? string.Empty : _view.GetEntityId().ToString()).Append(',').Append(_view != null && _view.activeSelf).Append(',')
                    .Append(_backend == null ? 0 : _backend.SliceCount).Append(',').Append(vertices).Append(',').AppendLine(pooled.ToString());
                if (_normalReturned && _normalActionFinished && _normalVertices > 0 && _repeatReturned && _repeatVertices > 0
                    && _repeatFromPool && _interruptAccepted && _forcedExitRequest && _normalStopObserved && _repeatStopObserved
                    && _repeatExitFrame >= _interruptFrame && _normalTailObserved && _activeFreezePassed
                    && _tailFreezePassed && _followSamples >= 3 && _tipTravel > .01f && _bindingReleased)
                { Issue = null; Completed = true; CleanupCharacter(); }
                else if (elapsed >= 775) Fail("normalReturn=" + _normalReturned + " normalActionFinished=" + _normalActionFinished
                    + " normalVertices=" + _normalVertices + " repeatReturn=" + _repeatReturned + " repeatVertices=" + _repeatVertices
                    + " repeatFromPool=" + _repeatFromPool + " interruptAccepted=" + _interruptAccepted
                    + " normalStopObserved=" + _normalStopObserved + " repeatStopObserved=" + _repeatStopObserved);
            }
            catch (Exception ex) { Fail(ex.GetType().Name + ": " + ex.Message); }
        }

        public void OnVfxRuntimeEvents(RuntimeFrame frame, IReadOnlyList<VfxRuntimeEvent> events)
        {
            foreach (var evt in events)
            {
                if (evt.InstanceId.Value != _vfxId || _vfxId == 0) continue;
                if (_play == 1 && (evt.Type == VfxRuntimeEventType.Stopped || evt.Type == VfxRuntimeEventType.Expired))
                {
                    _normalStopLocalFrame = _player.Action.LocalFrame;
                    _normalStopObserved = _player.Action.ActiveActionId.Value == ActionId
                        && _normalStopLocalFrame >= 38 && _normalStopLocalFrame <= 45
                        && evt.StopMode == VfxStopMode.Dissipate;
                }
                if (_play == 2 && evt.Type == VfxRuntimeEventType.Stopped && evt.StopMode == _interruptionStopMode
                    && evt.Reason == "character_action.lifecycle_exit" && _interruptAccepted) _repeatStopObserved = true;
            }
        }
        public void Dispose() { RestoreTimeScale(); _fanout.RemoveSink(this); CleanupCharacter(); }
        private void BeginFreeze(bool tail)
        {
            var mesh = _backend.GetComponent<MeshFilter>().sharedMesh;
            _freezeVertices = mesh.vertices;
            for (int i = 0; i < _freezeVertices.Length; i++)
                _freezeVertices[i] = _backend.transform.TransformPoint(_freezeVertices[i]);
            _freezeColors = mesh.colors32;
            _freezeClock = (double)_clockField.GetValue(_backend);
            _freezeFrame = _context.Slice.CurrentFrame.Value; _freezeLocalFrame = _player.Action.LocalFrame;
            _freezeSlices = _backend.SliceCount; _freezeTail = tail; _freezeUpdatesLeft = 12;
            _previousTimeScale = Time.timeScale; Time.timeScale = 0f;
        }
        private void RestoreTimeScale()
        {
            if (_freezeUpdatesLeft > 0) { Time.timeScale = _previousTimeScale; _freezeUpdatesLeft = 0; }
        }
        private void CleanupCharacter()
        {
            if (!_spawnRequested || _player == null || _cleanupRequested) return;
            _cleanupRequested = true;
            _context.Slice.Services.Commands.EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.DestroyCharacter(
                _context.Slice.CurrentFrame, CharacterTestRuntimeCommandSources.System, _player.Id.Value));
        }

        private bool IsInPool(GameObject view)
        {
            if (_poolField == null) throw new InvalidOperationException("Presenter pool metadata unavailable.");
            foreach (DictionaryEntry entry in (IDictionary)_poolField.GetValue(_context.Slice.Services.VfxPresenter))
                foreach (object item in (IEnumerable)entry.Value)
                    if ((GameObject)item.GetType().GetProperty("View").GetValue(item) == view) return true;
            return false;
        }
        private void CapturePooledViews()
        {
            if (_poolField == null) throw new InvalidOperationException("Presenter pool metadata unavailable.");
            foreach (DictionaryEntry entry in (IDictionary)_poolField.GetValue(_context.Slice.Services.VfxPresenter))
                foreach (object item in (IEnumerable)entry.Value)
                    _available.Add((GameObject)item.GetType().GetProperty("View").GetValue(item));
        }
        private void Fail(string issue) { RestoreTimeScale(); Issue = issue; Completed = true; CleanupCharacter(); }
        internal void WriteTrace(string directory) => File.WriteAllText(Path.Combine(directory, "CharacterTestPlayerVfx455Probe.csv"), _trace.ToString());
        internal void AppendTo(StringBuilder report)
        {
            report.Append("vfx455.characterStableId: ").AppendLine(_actualCharacterStableId);
            report.Append("vfx455.spawnRoute: ").AppendLine("production CharacterSpawnFlow and ResourceManager; authored default loadout");
            report.Append("vfx455.followSamples: ").AppendLine(_followSamples.ToString());
            report.Append("vfx455.maximumBladeAnchorErrorMeters: ").AppendLine(_maximumAnchorError.ToString("F6", CultureInfo.InvariantCulture));
            report.Append("vfx455.bladeTipTravelMeters: ").AppendLine(_tipTravel.ToString("F6", CultureInfo.InvariantCulture));
            report.Append("vfx455.activeTimeScaleZeroFreezePassed: ").AppendLine(_activeFreezePassed.ToString());
            report.Append("vfx455.retiringTimeScaleZeroFreezePassed: ").AppendLine(_tailFreezePassed.ToString());
            report.Append("vfx455.pausedUpdateChecks: ").AppendLine(_freezeChecks.ToString());
            report.Append("vfx455.normalTailObservedBeforePool: ").AppendLine(_normalTailObserved.ToString());
            report.Append("vfx455.poseBindingAndBudgetLeaseReleased: ").AppendLine(_bindingReleased.ToString());
            report.Append("vfx455.status: ").AppendLine(Passed ? "Passed" : "Failed");
            report.Append("vfx455.normalReturnedToPool: ").AppendLine(_normalReturned.ToString());
            report.Append("vfx455.normalActionFinished: ").AppendLine(_normalActionFinished.ToString());
            report.Append("vfx455.normalMaximumVertices: ").AppendLine(_normalVertices.ToString());
            report.Append("vfx455.normalViewId: ").AppendLine(_normalViewId);
            report.Append("vfx455.normalAuthorityExitFrame: ").AppendLine(_normalExitFrame.ToString());
            report.Append("vfx455.normalPoolReturnFrame: ").AppendLine(_normalReturnFrame.ToString());
            report.Append("vfx455.repeatMaximumVertices: ").AppendLine(_repeatVertices.ToString());
            report.Append("vfx455.repeatViewId: ").AppendLine(_repeatViewId);
            report.Append("vfx455.repeatUsedPooledView: ").AppendLine(_repeatFromPool.ToString());
            report.Append("vfx455.interruptionReason: ").AppendLine("ConfigurationReload via production Gateway");
            report.Append("vfx455.interruptionAccepted: ").AppendLine(_interruptAccepted.ToString());
            report.Append("vfx455.interruptionFrame: ").AppendLine(_interruptFrame.ToString());
            report.Append("vfx455.repeatAuthorityExitFrame: ").AppendLine(_repeatExitFrame.ToString());
            report.Append("vfx455.repeatPoolReturnFrame: ").AppendLine(_repeatReturnFrame.ToString());
            report.Append("vfx455.repeatReturnedToPool: ").AppendLine(_repeatReturned.ToString());
            report.Append("vfx455.normalAuthorityInstance: ").AppendLine(_normalAuthorityId.ToString());
            report.Append("vfx455.repeatAuthorityInstance: ").AppendLine(_repeatAuthorityId.ToString());
            report.Append("vfx455.normalVfxInstance: ").AppendLine(_normalVfxId.ToString());
            report.Append("vfx455.repeatVfxInstance: ").AppendLine(_repeatVfxId.ToString());
            report.Append("vfx455.normalStopObservedAtAuthoredEnd: ").AppendLine(_normalStopObserved.ToString());
            report.Append("vfx455.normalStopActionLocalFrame: ").AppendLine(_normalStopLocalFrame.ToString());
            report.Append("vfx455.interruptionActionLocalFrame: ").AppendLine(_interruptLocalFrame.ToString());
            report.Append("vfx455.matchingForcedExitRequest: ").AppendLine(_forcedExitRequest.ToString());
            report.Append("vfx455.interruptionStopMode: ").AppendLine(_interruptionStopMode.ToString());
            report.Append("vfx455.runtimeInterruptionStopObserved: ").AppendLine(_repeatStopObserved.ToString());
            report.Append("vfx455.prefabResourceKey: ").AppendLine(_prefabKey);
            report.Append("vfx455.material: ").AppendLine(_material);
            report.Append("vfx455.shader: ").AppendLine(_shader);
            report.Append("vfx455.issue: ").AppendLine(Issue);
        }
    }

}
