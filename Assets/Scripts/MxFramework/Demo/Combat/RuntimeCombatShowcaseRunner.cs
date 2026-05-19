using System;
using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Diagnostics;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Diagnostics;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo
{
    public enum RuntimeCombatQueryShapeMode
    {
        Capsule = 0,
        Ray = 1,
        Sphere = 2,
        Aabb = 3,
        Sector = 4,
    }

    public readonly struct RuntimeCombatShowcaseAuthoringConfig
    {
        public RuntimeCombatShowcaseAuthoringConfig(
            string sourceSummary,
            int actionId,
            int traceId,
            Transform playerMarker,
            Transform enemyMarker,
            string validationSummary,
            string markerSummary)
        {
            SourceSummary = string.IsNullOrEmpty(sourceSummary) ? "Authoring Preview: 未连接" : sourceSummary;
            ActionId = actionId;
            TraceId = traceId;
            PlayerMarker = playerMarker;
            EnemyMarker = enemyMarker;
            ValidationSummary = string.IsNullOrEmpty(validationSummary) ? "validation: 未执行" : validationSummary;
            MarkerSummary = markerSummary ?? string.Empty;
        }

        public string SourceSummary { get; }
        public int ActionId { get; }
        public int TraceId { get; }
        public Transform PlayerMarker { get; }
        public Transform EnemyMarker { get; }
        public string ValidationSummary { get; }
        public string MarkerSummary { get; }
        public bool HasAuthoringPreview => !string.IsNullOrEmpty(SourceSummary)
            && !string.Equals(SourceSummary, "Authoring Preview: 未连接", System.StringComparison.Ordinal);
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Runtime Combat Showcase Runner")]
    public sealed class RuntimeCombatShowcaseRunner : MonoBehaviour
    {
        private const int PlayerEntityId = 1;
        private const int EnemyEntityId = 2;
        private const int DefaultActionId = 400001;
        private const int ActionInstanceId = 1;
        private const int DefaultTraceId = 7;
        private const int HurtboxLayer = 1;
        private const int MotionObstacleLayer = 2;
        private const int CommandStep = 1;
        private const int CommandTrace = 2;
        private const int CommandResolve = 3;
        private const int CommandMove = 4;
        private const int CommandProbe = 5;
        private const int CommandInteractiveAttack = 6;
        private const int CommandMotion = 7;
        private const int CommandCharacterControl = 8;
        private const int CommandPressureBreak = 9;
        private const int CommandRuntimeAiPlanner = 10;

        private const int MaxLog = 40;

        private static readonly CombatBodyId PlayerBodyId = new CombatBodyId(PlayerEntityId);
        private static readonly CombatBodyId EnemyBodyId = new CombatBodyId(EnemyEntityId);
        private static readonly CombatColliderId HurtboxColliderId = new CombatColliderId(1);
        private static readonly CombatPhysicsLayerMask HurtboxLayerMask = CombatPhysicsLayerMask.FromLayer(HurtboxLayer);

        [SerializeField] private int _playerHp = 1000;
        [SerializeField] private int _playerAttack = 120;
        [SerializeField] private int _playerDefense = 20;
        [SerializeField] private int _enemyHp = 600;
        [SerializeField] private int _enemyDefense = 10;
        [SerializeField] private bool _syncPhysicsFromSceneMarkers = true;
        [SerializeField] private Transform _playerMarker;
        [SerializeField] private Transform _enemyMarker;
        [SerializeField] private Vector3 _hurtboxHalfExtents = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Transform _traceDebugLine;

        private readonly CombatFrameClock _clock = new CombatFrameClock();
        private readonly CombatPhysicsWorld _physicsWorld = new CombatPhysicsWorld();
        private readonly CombatReplayRecorder _replayRecorder = new CombatReplayRecorder();
        private readonly CombatDebugSnapshotBuilder _snapshotBuilder = new CombatDebugSnapshotBuilder();
        private readonly HitResolveSystem _hitResolveSystem = new HitResolveSystem();
        private readonly List<string> _eventLog = new List<string>();
        private readonly List<CombatCapsuleQuery> _queries = new List<CombatCapsuleQuery>();
        private readonly List<CombatQueryHeader> _queryHeaders = new List<CombatQueryHeader>();
        private readonly List<CombatQueryResult> _physicsHits = new List<CombatQueryResult>();
        private readonly List<HitResolveResult> _hitResults = new List<HitResolveResult>();
        private readonly HashSet<WeaponHitOnceKey> _consumedHitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly List<LineRenderer> _traceLinePool = new List<LineRenderer>();
        private readonly List<Transform> _traceEndpointPool = new List<Transform>();

        private int _playerCurrentHp;
        private int _enemyCurrentHp;
        private CombatDebugSnapshot _lastSnapshot;
        private FixVector3 _playerPhysicsPosition;
        private FixVector3 _enemyPhysicsPosition;
        private string _physicsBindingSummary;
        private CombatEntityId _selectedEntityId = new CombatEntityId(PlayerEntityId);
        private string _interactionSummary;
        private Renderer _traceDebugRenderer;
        private int _actionId = DefaultActionId;
        private int _traceId = DefaultTraceId;
        private RuntimeCombatShowcaseAuthoringConfig _authoringConfig;
        private RuntimeCombatQueryShapeMode _queryShapeMode = RuntimeCombatQueryShapeMode.Capsule;
        private CombatPhysicsQueryDebugReport _lastQueryDebugReport;
        private int _score;
        private int _round = 1;
        private int _streak;
        private Transform _combatVisualRoot;
        private Transform _hitDebugMarker;
        private Renderer _hitDebugRenderer;
        private LineRenderer _hitRingRenderer;
        private LineRenderer _missCrossRendererA;
        private LineRenderer _missCrossRendererB;
        private TextMesh _resultLabel;
        private Material _traceMissMaterial;
        private Material _traceHitMaterial;
        private Material _hitMarkerMaterial;
        private Material _missMarkerMaterial;
        private Collider _traceDebugCollider;
        private float _resultShownAt;
        private bool _lastResultWasHit;
        private RuntimeCombatShowcaseMotionAdapter _motionAdapter;
        private RuntimeCombatCharacterControlSlice _characterControlSlice;
        private Func<RuntimeFrame, CharacterFacingBasis> _characterControlFacingBasisProvider;

        public bool IsInitialized { get; private set; }
        public CombatFrame CurrentFrame => _clock.CurrentFrame;
        public int PlayerHp => _playerCurrentHp;
        public int PlayerMaxHp => _playerHp;
        public int PlayerAttack => _playerAttack;
        public int PlayerDefense => _playerDefense;
        public int EnemyHp => _enemyCurrentHp;
        public int EnemyMaxHp => _enemyHp;
        public int EnemyDefense => _enemyDefense;
        public IReadOnlyList<string> EventLog => _eventLog;
        public CombatDebugSnapshot LastSnapshot => _lastSnapshot;
        public int QueryCount => _queryHeaders.Count;
        public int HitCount => _hitResults.Count;
        public int Score => _score;
        public int Round => _round;
        public int Streak => _streak;
        public RuntimeCombatQueryShapeMode QueryShapeMode => _queryShapeMode;
        public string QueryShapeName => _queryShapeMode.ToString();
        public string PhysicsBindingSummary => _physicsBindingSummary;
        public CombatEntityId SelectedEntityId => _selectedEntityId;
        public string SelectedEntityName => _selectedEntityId.Value == EnemyEntityId ? "Enemy" : "Player";
        public string InteractionSummary => _interactionSummary;
        public string PhysicsPlaygroundSummary => $"Round {_round} | Score {_score} | Streak {_streak} | Shape {QueryShapeName} | {MotionBrief}";
        public string LastQueryDebugSummary => BuildLastQueryDebugSummary();
        public string MotionSummary => BuildMotionSummary();
        public string MotionCollisionSummary => _motionAdapter != null ? _motionAdapter.BuildCollisionSummary() : "Motion collision: waiting";
        public string CharacterControlSummary => _characterControlSlice != null ? _characterControlSlice.BuildSummary() : "CharacterControl: waiting";
        public string CharacterControlAnimationSummary => _characterControlSlice != null ? _characterControlSlice.BuildAnimationSummary() : "animation: waiting";
        public string CharacterControlDebugReport => _characterControlSlice != null ? _characterControlSlice.BuildDebugReport() : "CharacterControl: waiting";
        public FrameworkDebugSnapshot CharacterControlDebugSnapshot => EnsureCharacterControlSlice().CreateDebugSnapshot();
        public int CharacterControlGameplayCommandCount => _characterControlSlice != null ? _characterControlSlice.TotalGameplayCommandsDrained : 0;
        public string MotionBrief => _motionAdapter != null
            ? $"Motion grounded={_motionAdapter.State.Grounded} flags={_motionAdapter.State.CollisionFlags}"
            : "Motion waiting";
        public Transform PlayerMarker => _playerMarker;
        public Transform EnemyMarker => _enemyMarker;

        public void SetCharacterControlFacingBasisProvider(Func<RuntimeFrame, CharacterFacingBasis> facingBasisProvider)
        {
            _characterControlFacingBasisProvider = facingBasisProvider;
            if (_characterControlSlice != null)
                _characterControlSlice.SetFacingBasisProvider(facingBasisProvider);
        }
        public RuntimeCombatShowcaseAuthoringConfig AuthoringPreviewConfig => _authoringConfig;
        public string AuthoringPreviewSummary => _authoringConfig.HasAuthoringPreview
            ? $"{_authoringConfig.SourceSummary} | ActionId={_actionId} TraceId={_traceId} | {_authoringConfig.ValidationSummary}"
            : "Authoring Preview: 未连接";

        private void Awake()
        {
            if (GetComponent<RuntimeCombatShowcaseInputController>() == null)
                gameObject.AddComponent<RuntimeCombatShowcaseInputController>();

            ResetShowcase();
        }

        private void LateUpdate()
        {
            AnimateCombatVisuals();
        }

        private void OnDestroy()
        {
            DisposeCharacterControlSlice();
        }

        public void StepFrame()
        {
            CombatFrame frame = _clock.Step();
            _replayRecorder.Record(new CombatReplayInput(frame, new CombatEntityId(PlayerEntityId), CommandStep, frame.Value));
            LogEvent($"Frame advanced: {frame.Value}");
            UpdateSnapshot();
        }

        public void GenerateWeaponTrace()
        {
            CombatFrame frame = EnsureNonZeroFrame();
            _replayRecorder.Record(new CombatReplayInput(frame, new CombatEntityId(PlayerEntityId), CommandTrace, frame.Value));
            _queries.Clear();
            _queryHeaders.Clear();
            RebuildPhysicsWorld(logBinding: false);

            WeaponTraceFrame trace = CreateTraceFrame(frame);
            CombatCapsuleQuery blade = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                trace,
                new CombatEntityId(PlayerEntityId),
                _actionId,
                queryId: frame.Value * 10,
                sourceOrder: 0);
            _queries.Add(blade);
            WeaponTraceQueryBuilder.BuildTipSweepCapsules(
                trace,
                new CombatEntityId(PlayerEntityId),
                _actionId,
                queryIdStart: frame.Value * 10 + 1,
                Fix64.FromInt(2),
                maxSubsteps: 4,
                _queries);

            _physicsHits.Clear();
            for (int i = 0; i < _queries.Count; i++)
            {
                CombatPhysicsQuery query = CombatPhysicsQuery.From(_queries[i]);
                ExecutePhysicsQuery(query, _physicsHits);
            }

            ShowTraceVisual(_queries.Count > 0 ? _queries[0] : default, _physicsHits.Count > 0);
            ShowHitMarker(_physicsHits.Count > 0 ? GetHitPresentationPoint(_physicsHits[0]) : trace.TipNow, _physicsHits.Count > 0);
            LogEvent($"WeaponTrace generated: frame={frame.Value} traceQueries={_queries.Count} physicsHits={_physicsHits.Count} | {LastQueryDebugSummary}");
            UpdateSnapshot();
        }

        public void ResolveHit()
        {
            CombatFrame frame = EnsureNonZeroFrame();
            if (_queries.Count == 0)
                GenerateWeaponTrace();

            _replayRecorder.Record(new CombatReplayInput(frame, new CombatEntityId(PlayerEntityId), CommandResolve, frame.Value));
            _hitResults.Clear();

            if (_physicsHits.Count == 0)
            {
                ShowMissVisual();
                LogEvent($"HitResolve: Miss enemyHp={_enemyCurrentHp}/{_enemyHp}");
                UpdateSnapshot();
                return;
            }

            CombatQueryResult physicsHit = _physicsHits[0];
            int damage = Mathf.Max(1, _playerAttack - _enemyDefense);
            HitTargetStateFlags targetState = _enemyCurrentHp > 0 ? HitTargetStateFlags.Alive : HitTargetStateFlags.None;
            var candidate = new HitCandidate(
                new CombatEntityId(PlayerEntityId),
                physicsHit.TargetEntityId,
                _actionId,
                ActionInstanceId,
                physicsHit.Query.TraceId,
                frame,
                physicsHit,
                damage,
                staggerFrames: 8,
                knockback: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                targetState);

            _hitResolveSystem.Resolve(new[] { candidate }, _consumedHitOnceKeys, _hitResults);
            HitResolveResult result = _hitResults[0];
            if (result.IsAcceptedDamage)
                _enemyCurrentHp = Mathf.Max(0, _enemyCurrentHp - result.Damage);

            ShowHitMarker(GetHitPresentationPoint(physicsHit), result.IsAcceptedDamage);
            LogEvent($"HitResolve: {result.Kind} target={result.TargetId.Value} damage={result.Damage} enemyHp={_enemyCurrentHp}/{_enemyHp}");
            UpdateSnapshot();
        }

        public void VerifyReplayHash()
        {
            CombatHash inputHash = _replayRecorder.ComputeInputHash();
            CombatHash snapshotHash = _lastSnapshot != null ? _lastSnapshot.FrameHash : CombatHash.Empty;
            LogEvent($"Replay hash stable: input={inputHash} frame={snapshotHash}");
            UpdateSnapshot();
        }

        public void LogSnapshotSummary()
        {
            UpdateSnapshot();
            LogEvent(_lastSnapshot != null ? _lastSnapshot.Summary : "Snapshot not ready");
        }

        public void ResetShowcase()
        {
            _clock.Reset();
            _replayRecorder.Clear();
            _snapshotBuilder.Clear();
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();
            _consumedHitOnceKeys.Clear();
            _eventLog.Clear();
            _playerCurrentHp = _playerHp;
            _enemyCurrentHp = _enemyHp;
            _score = 0;
            _round = 1;
            _streak = 0;
            _lastQueryDebugReport = null;
            _selectedEntityId = new CombatEntityId(PlayerEntityId);
            ResolveSceneMarkers();
            EnsureMotionAdapter();
            ResetCharacterControlSlice();
            _motionAdapter.Reset(GetPhysicsPosition(
                _playerMarker,
                new FixVector3(Fix64.Zero, _motionAdapter.Config.CharacterHalfExtents.Y, Fix64.Zero)));
            ApplyMotionToPlayerMarker();
            SnapEnemyMarkerToMotionPlane();
            RebuildPhysicsWorld(logBinding: false);
            _motionAdapter.EnsureObstacleVisuals(transform);
            HideCombatVisuals();
            _interactionSummary = $"Selected: {SelectedEntityName}";
            IsInitialized = true;
            LogEvent("=== Combat Showcase Reset ===");
            if (_authoringConfig.HasAuthoringPreview)
            {
                LogEvent(_authoringConfig.SourceSummary);
                LogEvent($"ActionId={_actionId} TraceId={_traceId}");
                LogEvent(_authoringConfig.ValidationSummary);
                if (!string.IsNullOrEmpty(_authoringConfig.MarkerSummary))
                    LogEvent(_authoringConfig.MarkerSummary);
            }

            LogEvent(_physicsBindingSummary);
            LogEvent("Playground: WASD/Arrows move through CharacterControl, Space jump, J attack, T Runtime AI Planner, P probe, Q shape, R reset.");
            LogEvent(MotionSummary);
            LogEvent(CharacterControlSummary);
            UpdateSnapshot();
        }

        public void ApplyAuthoringPreviewConfig(RuntimeCombatShowcaseAuthoringConfig config)
        {
            _authoringConfig = config;
            if (config.ActionId > 0)
                _actionId = config.ActionId;
            if (config.TraceId > 0)
                _traceId = config.TraceId;

            if (config.PlayerMarker != null)
                _playerMarker = config.PlayerMarker;
            if (config.EnemyMarker != null)
                _enemyMarker = config.EnemyMarker;

            ResetShowcase();
        }

        public bool SelectMarker(Transform marker)
        {
            ResolveSceneMarkers();
            if (marker == null)
            {
                return false;
            }

            if (_playerMarker != null && marker == _playerMarker)
            {
                SelectEntity(new CombatEntityId(PlayerEntityId));
                return true;
            }

            if (_enemyMarker != null && marker == _enemyMarker)
            {
                SelectEntity(new CombatEntityId(EnemyEntityId));
                return true;
            }

            return false;
        }

        public void SelectEntity(CombatEntityId entityId)
        {
            if (entityId.Value != PlayerEntityId && entityId.Value != EnemyEntityId)
            {
                return;
            }

            _selectedEntityId = entityId;
            _interactionSummary = $"Selected: {SelectedEntityName}";
            LogEvent(_interactionSummary);
            UpdateSnapshot();
        }

        public void MoveSelectedTo(Vector3 worldPosition)
        {
            Transform marker = GetSelectedMarker();
            if (marker == null)
            {
                LogEvent("Move ignored: selected marker is missing.");
                return;
            }

            worldPosition.y = marker.position.y;
            CombatFrame frame = _clock.Step();
            _replayRecorder.Record(new CombatReplayInput(frame, _selectedEntityId, CommandMove, frame.Value));
            if (_selectedEntityId.Value == PlayerEntityId)
            {
                EnsureMotionAdapter();
                _motionAdapter.Warp(frame, ToFixVector3(worldPosition));
                ApplyMotionToPlayerMarker();
            }
            else
            {
                marker.position = worldPosition;
                SnapEnemyMarkerToMotionPlane();
            }

            RebuildPhysicsWorld(logBinding: false);
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();
            _lastQueryDebugReport = null;
            _interactionSummary = $"Move {SelectedEntityName} -> {FormatPosition(marker.position)}";
            LogEvent(_interactionSummary);
            UpdateSnapshot();
        }

        public void StepPlayerMotion(Vector3 moveDirection, bool jumpPressed)
        {
            EnsureMotionAdapter();
            CombatFrame frame = _clock.Step();
            int encodedInput = EncodeMotionInput(moveDirection, jumpPressed);
            _replayRecorder.Record(new CombatReplayInput(frame, new CombatEntityId(PlayerEntityId), CommandMotion, encodedInput));
            RebuildPhysicsWorld(logBinding: false);

            CombatMotionInput input = new CombatMotionInput(
                ToMotionDirection(moveDirection),
                jumpPressed,
                Fix64.One);
            CombatMotionStepResult result = _motionAdapter.Step(frame, _physicsWorld, PlayerBodyId, input);
            _playerPhysicsPosition = result.State.Position;
            ApplyMotionToPlayerMarker();
            if (_physicsWorld.TryGetBody(PlayerBodyId, out CombatPhysicsBody playerBody))
                _playerPhysicsPosition = playerBody.Position;
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();
            _lastQueryDebugReport = null;
            _interactionSummary = $"Motion Player -> pos={FormatPosition(_playerMarker != null ? _playerMarker.position : ToVector3(result.State.Position))} grounded={result.State.Grounded} flags={result.CollisionFlags}";

            if (jumpPressed || result.CollisionFlags != CombatMotionCollisionFlags.None || frame.Value % 15 == 0)
                LogEvent(_interactionSummary);

            UpdateSnapshot();
        }

        public bool StepCharacterControlFromInput(IInputProvider inputProvider)
        {
            RuntimeCombatCharacterControlSlice slice = EnsureCharacterControlSlice();
            CombatFrame frame = _clock.Step();
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame.Value);
            if (!slice.TryReadLocalCommand(inputProvider, runtimeFrame, out CharacterCommand command))
            {
                slice.TickRuntime(runtimeFrame);
                UpdateSnapshot();
                return false;
            }

            ApplyCharacterControlCommand(frame, command, CommandCharacterControl);
            return true;
        }

        public bool RunRuntimeAiPlannerCommand()
        {
            RuntimeCombatCharacterControlSlice slice = EnsureCharacterControlSlice();
            CombatFrame frame = _clock.Step();
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame.Value);
            if (!slice.TryReadRuntimeAiPlannerCommand(runtimeFrame, out CharacterCommand command))
            {
                slice.TickRuntime(runtimeFrame);
                _interactionSummary = "Runtime AI Planner command suppressed.";
                LogEvent(_interactionSummary);
                UpdateSnapshot();
                return false;
            }

            ApplyCharacterControlCommand(frame, command, CommandRuntimeAiPlanner);
            return true;
        }

        public CharacterPressureReactionResult TriggerCharacterPressureBreak()
        {
            RuntimeCombatCharacterControlSlice slice = EnsureCharacterControlSlice();
            CombatFrame frame = _clock.Step();
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame.Value);
            _replayRecorder.Record(new CombatReplayInput(frame, new CombatEntityId(PlayerEntityId), CommandPressureBreak, frame.Value));
            CharacterPressureReactionResult result = slice.ApplyPostureBreak(runtimeFrame, "combat-showcase:manual-pressure-break");
            slice.TickRuntime(runtimeFrame);
            _interactionSummary = $"CharacterControl PressureBreak: kind={result.Kind} started={result.ReactionStarted} active={CharacterControlSummary}";
            LogEvent(_interactionSummary);
            UpdateSnapshot();
            return result;
        }

        private void ApplyCharacterControlCommand(
            CombatFrame frame,
            CharacterCommand command,
            int replayCommandId)
        {
            RuntimeCombatCharacterControlSlice slice = EnsureCharacterControlSlice();
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame.Value);
            EnsureMotionAdapter();
            _replayRecorder.Record(new CombatReplayInput(
                frame,
                new CombatEntityId(PlayerEntityId),
                replayCommandId,
                EncodeCharacterControlInput(command)));
            RebuildPhysicsWorld(logBinding: false);
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();
            _lastQueryDebugReport = null;

            slice.RecordCommand(command);
            CharacterActionResult actionResult = default;
            bool hasActionResult = false;
            if (command.HasActionRequest)
            {
                actionResult = slice.SubmitAction(command.ActionRequest);
                hasActionResult = true;
            }

            CharacterMotionResult motionResult = _motionAdapter.StepCharacter(
                frame,
                slice.StateMachine,
                command,
                _physicsWorld);
            slice.RecordMotion(motionResult);
            _playerPhysicsPosition = motionResult.Position;
            ApplyMotionToPlayerMarker();
            if (_physicsWorld.TryGetBody(PlayerBodyId, out CombatPhysicsBody playerBody))
                _playerPhysicsPosition = playerBody.Position;

            bool resolvedCombatAction = hasActionResult
                && actionResult.Success
                && command.ActionRequest.HasCombatAction;
            if (resolvedCombatAction)
            {
                _selectedEntityId = new CombatEntityId(PlayerEntityId);
                ResolveInteractiveAttack(frame, recordReplay: false, sourceLabel: "CharacterControl Attack");
            }
            else
            {
                _interactionSummary = $"CharacterControl {command.TraceId}: pos={FormatPosition(_playerMarker != null ? _playerMarker.position : ToVector3(motionResult.Position))} grounded={motionResult.Grounded} state={slice.StateMachine.CurrentState}";
                if (hasActionResult)
                    _interactionSummary += $" action={command.ActionRequest.Kind} success={actionResult.Success}";
                LogEvent(_interactionSummary);
            }

            slice.TickRuntime(runtimeFrame);
            if (command.JumpPressed
                || command.HasActionRequest
                || motionResult.CollisionFlags != CombatMotionCollisionFlags.None
                || frame.Value % 15 == 0)
            {
                LogEvent(CharacterControlSummary);
            }

            UpdateSnapshot();
        }

        public void ProbeFromSelected()
        {
            CombatFrame frame = _clock.Step();
            _replayRecorder.Record(new CombatReplayInput(frame, _selectedEntityId, CommandProbe, frame.Value));
            RebuildPhysicsWorld(logBinding: false);
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();

            CombatPhysicsQuery query = BuildSelectedTargetQuery(frame, CommandProbe, Fix64.FromRatio(35, 100));
            ExecutePhysicsQuery(query, _physicsHits);

            string target = GetTargetEntityName();
            ShowPhysicsQueryVisual(query, _physicsHits.Count > 0);
            ShowHitMarker(_physicsHits.Count > 0 ? GetHitPresentationPoint(_physicsHits[0]) : GetQueryEndPoint(query), _physicsHits.Count > 0);
            _interactionSummary = _physicsHits.Count > 0
                ? $"Probe {SelectedEntityName}->{target}: hit distance={_physicsHits[0].Distance} | {LastQueryDebugSummary}"
                : $"Probe {SelectedEntityName}->{target}: miss | {LastQueryDebugSummary}";
            LogEvent(_interactionSummary);
            UpdateSnapshot();
        }

        public void AttackFromSelected()
        {
            CombatFrame frame = _clock.Step();
            ResolveInteractiveAttack(frame, recordReplay: true, sourceLabel: "Attack");
        }

        private void ResolveInteractiveAttack(CombatFrame frame, bool recordReplay, string sourceLabel)
        {
            if (recordReplay)
                _replayRecorder.Record(new CombatReplayInput(frame, _selectedEntityId, CommandInteractiveAttack, frame.Value));

            RebuildPhysicsWorld(logBinding: false);
            _queries.Clear();
            _queryHeaders.Clear();
            _physicsHits.Clear();
            _hitResults.Clear();

            CombatPhysicsQuery query = BuildSelectedTargetQuery(frame, CommandInteractiveAttack, Fix64.Half);
            ExecutePhysicsQuery(query, _physicsHits);

            if (_physicsHits.Count == 0)
            {
                _streak = 0;
                AddScore(-10);
                ShowPhysicsQueryVisual(query, false);
                ShowHitMarker(GetQueryEndPoint(query), false);
                _interactionSummary = $"{sourceLabel} {SelectedEntityName}->{GetTargetEntityName()}: miss | {LastQueryDebugSummary}";
                LogEvent(_interactionSummary);
                UpdateSnapshot();
                return;
            }

            CombatQueryResult physicsHit = _physicsHits[0];
            int attack = _selectedEntityId.Value == PlayerEntityId ? _playerAttack : Mathf.Max(1, _playerAttack - 20);
            int defense = _selectedEntityId.Value == PlayerEntityId ? _enemyDefense : _playerDefense;
            int damage = Mathf.Max(1, attack - defense);
            HitTargetStateFlags targetState = IsTargetAlive() ? HitTargetStateFlags.Alive : HitTargetStateFlags.None;
            var candidate = new HitCandidate(
                _selectedEntityId,
                physicsHit.TargetEntityId,
                _actionId,
                ActionInstanceId + frame.Value,
                physicsHit.Query.TraceId,
                frame,
                physicsHit,
                damage,
                staggerFrames: 8,
                knockback: BuildDirectionToTarget(),
                targetState);

            _hitResolveSystem.Resolve(new[] { candidate }, _consumedHitOnceKeys, _hitResults);
            HitResolveResult result = _hitResults[0];
            if (result.IsAcceptedDamage)
            {
                ApplyDamageToTarget(result.Damage);
                if (_selectedEntityId.Value == EnemyEntityId)
                {
                    EnsureCharacterControlSlice().ApplyPostureBreak(new RuntimeFrame(frame.Value), "combat-showcase:enemy-hit");
                }

                _streak++;
                AddScore(100 + _streak * 25);
            }
            else
            {
                _streak = 0;
            }

            ShowPhysicsQueryVisual(query, result.IsAcceptedDamage);
            ShowHitMarker(GetHitPresentationPoint(physicsHit), result.IsAcceptedDamage);
            _interactionSummary = $"{sourceLabel} {SelectedEntityName}->{GetTargetEntityName()}: {result.Kind} damage={result.Damage} | {LastQueryDebugSummary}";
            LogEvent(_interactionSummary);
            TryAdvanceRoundAfterKill();
            UpdateSnapshot();
        }

        public void CycleQueryShape()
        {
            int next = ((int)_queryShapeMode + 1) % 5;
            _queryShapeMode = (RuntimeCombatQueryShapeMode)next;
            _interactionSummary = $"Shape mode -> {QueryShapeName}";
            LogEvent(_interactionSummary);
            UpdateSnapshot();
        }

        private CombatFrame EnsureNonZeroFrame()
        {
            if (_clock.CurrentFrame.Value == 0)
                return _clock.Step();

            return _clock.CurrentFrame;
        }

        private WeaponTraceFrame CreateTraceFrame(CombatFrame frame)
        {
            FixVector3 direction = _enemyPhysicsPosition - _playerPhysicsPosition;
            if (!direction.TryNormalize(out direction))
                direction = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

            FixVector3 previousRootOffset = Scale(direction, Fix64.FromRatio(25, 100));
            FixVector3 previousTipOffset = Scale(direction, Fix64.FromRatio(11, 10));
            FixVector3 currentRootOffset = Scale(direction, Fix64.FromRatio(55, 100));
            FixVector3 currentTipOffset = Scale(direction, Fix64.FromRatio(26, 10));
            return new WeaponTraceFrame(
                _traceId,
                _playerPhysicsPosition + previousRootOffset,
                _playerPhysicsPosition + previousTipOffset,
                _playerPhysicsPosition + currentRootOffset,
                _playerPhysicsPosition + currentTipOffset,
                Fix64.Half,
                HurtboxLayerMask);
        }

        private CombatCapsuleQuery BuildSelectedTargetCapsule(CombatFrame frame, int commandId, Fix64 radius)
        {
            FixVector3 source = GetSelectedPhysicsPosition();
            FixVector3 target = GetTargetPhysicsPosition();
            return new CombatCapsuleQuery(
                new CombatQueryHeader(
                    queryId: frame.Value * 100 + commandId,
                    CombatQueryKind.Capsule,
                    _selectedEntityId,
                    traceId: _traceId + commandId,
                    actionId: _actionId,
                    sourceOrder: 0,
                    HurtboxLayerMask),
                source,
                target,
                radius);
        }

        private CombatPhysicsQuery BuildSelectedTargetQuery(CombatFrame frame, int commandId, Fix64 radius)
        {
            FixVector3 source = GetSelectedPhysicsPosition();
            FixVector3 target = GetTargetPhysicsPosition();
            FixVector3 delta = target - source;
            Fix64 distance = delta.LengthSquared().IsZero ? Fix64.One : delta.LengthSquared().Sqrt();
            FixVector3 direction = delta.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            CombatQueryHeader header = new CombatQueryHeader(
                queryId: frame.Value * 100 + commandId,
                ToQueryKind(_queryShapeMode),
                _selectedEntityId,
                traceId: _traceId + commandId,
                actionId: _actionId,
                sourceOrder: (int)_queryShapeMode,
                HurtboxLayerMask);

            switch (_queryShapeMode)
            {
                case RuntimeCombatQueryShapeMode.Ray:
                    return CombatPhysicsQuery.From(new CombatRayQuery(header, source, direction, distance + Fix64.One));
                case RuntimeCombatQueryShapeMode.Sphere:
                    return CombatPhysicsQuery.From(new CombatSphereQuery(header, source + Scale(direction, Fix64.FromRatio(3, 2)), Fix64.FromRatio(13, 10)));
                case RuntimeCombatQueryShapeMode.Aabb:
                    FixVector3 min = Min(source, target) - new FixVector3(radius, radius, radius);
                    FixVector3 max = Max(source, target) + new FixVector3(radius, radius, radius);
                    return CombatPhysicsQuery.From(new CombatAabbQuery(header, min, max));
                case RuntimeCombatQueryShapeMode.Sector:
                    return CombatPhysicsQuery.From(new CombatSectorQuery(header, source, direction, distance + Fix64.One, Fix64.FromRatio(45, 100)));
                case RuntimeCombatQueryShapeMode.Capsule:
                default:
                    return CombatPhysicsQuery.From(new CombatCapsuleQuery(header, source, target, radius));
            }
        }

        private static CombatQueryKind ToQueryKind(RuntimeCombatQueryShapeMode mode)
        {
            switch (mode)
            {
                case RuntimeCombatQueryShapeMode.Ray:
                    return CombatQueryKind.Ray;
                case RuntimeCombatQueryShapeMode.Sphere:
                    return CombatQueryKind.Sphere;
                case RuntimeCombatQueryShapeMode.Aabb:
                    return CombatQueryKind.Aabb;
                case RuntimeCombatQueryShapeMode.Sector:
                    return CombatQueryKind.Sector;
                case RuntimeCombatQueryShapeMode.Capsule:
                default:
                    return CombatQueryKind.Capsule;
            }
        }

        private void ExecutePhysicsQuery(CombatPhysicsQuery query, List<CombatQueryResult> hits)
        {
            _queryHeaders.Add(query.Header);
            _physicsWorld.Query(query, hits);
            _lastQueryDebugReport = _physicsWorld.ExplainQuery(query);
        }

        private void RebuildPhysicsWorld(bool logBinding)
        {
            ResolveSceneMarkers();
            EnsureMotionAdapter();
            _playerPhysicsPosition = _motionAdapter.IsInitialized
                ? _motionAdapter.State.Position
                : GetPhysicsPosition(_playerMarker, FixVector3.Zero);
            _enemyPhysicsPosition = GetPhysicsPosition(
                _enemyMarker,
                new FixVector3(Fix64.FromInt(3), _motionAdapter.Config.CharacterHalfExtents.Y, Fix64.Zero));

            FixVector3 halfExtents = ToFixVector3(_hurtboxHalfExtents);
            _physicsWorld.Clear();
            _physicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(PlayerEntityId),
                PlayerBodyId,
                _playerPhysicsPosition));
            _physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                PlayerBodyId,
                HurtboxColliderId,
                HurtboxLayer,
                -halfExtents,
                halfExtents));

            _physicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(EnemyEntityId),
                EnemyBodyId,
                _enemyPhysicsPosition));
            _physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                EnemyBodyId,
                HurtboxColliderId,
                HurtboxLayer,
                -halfExtents,
                halfExtents));
            _motionAdapter.RegisterStaticObstacles(_physicsWorld);

            _physicsBindingSummary = _syncPhysicsFromSceneMarkers
                ? $"Scene markers -> CombatPhysicsWorld: P{_playerPhysicsPosition} E{_enemyPhysicsPosition} obstacles=ground/wall/ceiling"
                : $"Fixed debug positions -> CombatPhysicsWorld: P{_playerPhysicsPosition} E{_enemyPhysicsPosition} obstacles=ground/wall/ceiling";

            if (logBinding)
                LogEvent(_physicsBindingSummary);
        }

        private void ResolveSceneMarkers()
        {
            if (!_syncPhysicsFromSceneMarkers)
                return;

            if (_playerMarker == null)
            {
                GameObject player = GameObject.Find("Combat_Player_Marker");
                if (player != null)
                    _playerMarker = player.transform;
            }

            if (_enemyMarker == null)
            {
                GameObject enemy = GameObject.Find("Combat_Enemy_Marker");
                if (enemy != null)
                    _enemyMarker = enemy.transform;
            }

            if (_traceDebugLine == null)
            {
                GameObject trace = GameObject.Find("Weapon_Trace_Debug_Line");
                if (trace != null)
                    _traceDebugLine = trace.transform;
            }

            if (_traceDebugLine != null && _traceDebugCollider == null)
            {
                _traceDebugCollider = _traceDebugLine.GetComponent<Collider>();
                if (_traceDebugCollider != null)
                    _traceDebugCollider.enabled = false;
            }
        }

        private FixVector3 GetPhysicsPosition(Transform marker, FixVector3 fallback)
        {
            if (!_syncPhysicsFromSceneMarkers || marker == null)
                return fallback;

            return ToFixVector3(marker.position);
        }

        private void EnsureMotionAdapter()
        {
            if (_motionAdapter == null)
                _motionAdapter = new RuntimeCombatShowcaseMotionAdapter(MotionObstacleLayer);
        }

        private RuntimeCombatCharacterControlSlice EnsureCharacterControlSlice()
        {
            if (_characterControlSlice == null)
                _characterControlSlice = new RuntimeCombatCharacterControlSlice(_actionId, _characterControlFacingBasisProvider);

            return _characterControlSlice;
        }

        private void ResetCharacterControlSlice()
        {
            DisposeCharacterControlSlice();
            EnsureCharacterControlSlice();
        }

        private void DisposeCharacterControlSlice()
        {
            if (_characterControlSlice == null)
                return;

            _characterControlSlice.Dispose();
            _characterControlSlice = null;
        }

        private void ApplyMotionToPlayerMarker()
        {
            if (_playerMarker == null || _motionAdapter == null)
                return;

            _playerMarker.position = ToVector3(_motionAdapter.State.Position);
        }

        private void SnapEnemyMarkerToMotionPlane()
        {
            if (_enemyMarker == null || _motionAdapter == null)
                return;

            Vector3 position = _enemyMarker.position;
            float minY = ToFloat(_motionAdapter.Config.CharacterHalfExtents.Y);
            if (position.y < minY)
                _enemyMarker.position = new Vector3(position.x, minY, position.z);
        }

        private string BuildMotionSummary()
        {
            if (_motionAdapter == null)
                return "Motion: waiting";

            FixVector3 bodyPosition = _motionAdapter.State.Position;
            if (_physicsWorld.TryGetBody(PlayerBodyId, out CombatPhysicsBody body))
                bodyPosition = body.Position;

            return _motionAdapter.BuildSummary(_physicsWorld.Revision, bodyPosition);
        }

        private static FixVector3 ToMotionDirection(Vector3 direction)
        {
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude > 1f)
                horizontal.Normalize();

            return new FixVector3(ToFix64(horizontal.x), Fix64.Zero, ToFix64(horizontal.z));
        }

        private static int EncodeMotionInput(Vector3 moveDirection, bool jumpPressed)
        {
            int x = moveDirection.x > 0.01f ? 1 : moveDirection.x < -0.01f ? -1 : 0;
            int z = moveDirection.z > 0.01f ? 1 : moveDirection.z < -0.01f ? -1 : 0;
            return (jumpPressed ? 100 : 0) + (x + 1) * 10 + (z + 1);
        }

        private static int EncodeCharacterControlInput(CharacterCommand command)
        {
            unchecked
            {
                int hash = 17;
                hash = AddHash(hash, command.SourceId);
                hash = AddHash(hash, command.Entity.GetHashCode());
                hash = AddHash(hash, command.MoveDirection.X);
                hash = AddHash(hash, command.MoveDirection.Z);
                hash = AddHash(hash, command.FacingBasis.Right.X);
                hash = AddHash(hash, command.FacingBasis.Right.Z);
                hash = AddHash(hash, command.FacingBasis.Forward.X);
                hash = AddHash(hash, command.FacingBasis.Forward.Z);
                hash = AddHash(hash, command.FacingBasis.Facing.X);
                hash = AddHash(hash, command.FacingBasis.Facing.Z);
                hash = AddHash(hash, command.JumpPressed ? 1 : 0);
                hash = AddHash(hash, command.SprintHeld ? 1 : 0);
                hash = AddHash(hash, (int)command.ActionButtons);
                hash = AddHash(hash, command.MoveSpeedScale);

                CharacterActionRequest request = command.ActionRequest;
                hash = AddHash(hash, request.SourceId);
                hash = AddHash(hash, (int)request.Kind);
                hash = AddHash(hash, request.CombatActionId);
                hash = AddHash(hash, request.GameplayAbilityId);
                hash = AddHash(hash, request.TargetGameplayEntityId.Index);
                hash = AddHash(hash, request.TargetGameplayEntityId.Generation);
                hash = AddHash(hash, request.ForceStart ? 1 : 0);
                hash = AddHash(hash, request.QueueIfBusy ? 1 : 0);
                return hash;
            }
        }

        private static int AddHash(int hash, int value)
        {
            unchecked
            {
                return (hash * 397) ^ value;
            }
        }

        private static int AddHash(int hash, Fix64 value)
        {
            unchecked
            {
                long raw = value.RawValue;
                return AddHash(hash, (int)(raw ^ (raw >> 32)));
            }
        }

        private static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(
                ToFix64(value.x),
                ToFix64(value.y),
                ToFix64(value.z));
        }

        private static FixVector3 Scale(FixVector3 value, Fix64 scalar)
        {
            return new FixVector3(value.X * scalar, value.Y * scalar, value.Z * scalar);
        }

        private static FixVector3 Min(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Min(left.X, right.X),
                Fix64.Min(left.Y, right.Y),
                Fix64.Min(left.Z, right.Z));
        }

        private static FixVector3 Max(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                Fix64.Max(left.X, right.X),
                Fix64.Max(left.Y, right.Y),
                Fix64.Max(left.Z, right.Z));
        }

        private void HideCombatVisuals()
        {
            ResolveSceneMarkers();
            EnsureVisualMaterials();
            if (_traceDebugLine != null)
                _traceDebugLine.gameObject.SetActive(false);

            for (int i = 0; i < _traceLinePool.Count; i++)
                _traceLinePool[i].gameObject.SetActive(false);

            for (int i = 0; i < _traceEndpointPool.Count; i++)
                _traceEndpointPool[i].gameObject.SetActive(false);

            if (_hitDebugMarker != null)
                _hitDebugMarker.gameObject.SetActive(false);
        }

        private void ShowTraceVisual(CombatCapsuleQuery query, bool hit)
        {
            ResolveSceneMarkers();
            EnsureVisualMaterials();
            HideLegacyTraceDebugLine();

            if (_queries.Count > 0)
            {
                ShowTraceVisuals(_queries, hit);
                return;
            }

            ShowTraceSegment(query, 0, hit);
            HideUnusedTraceSegments(1);
        }

        private void ShowTraceVisuals(IReadOnlyList<CombatCapsuleQuery> queries, bool hit)
        {
            for (int i = 0; i < queries.Count; i++)
                ShowTraceSegment(queries[i], i, hit);

            HideUnusedTraceSegments(queries.Count);
        }

        private void ShowPhysicsQueryVisual(CombatPhysicsQuery query, bool hit)
        {
            ResolveSceneMarkers();
            EnsureVisualMaterials();
            HideLegacyTraceDebugLine();
            _queries.Clear();
            CombatCapsuleQuery visual = BuildVisualCapsule(query);
            ShowTraceSegment(visual, 0, hit);
            HideUnusedTraceSegments(1);
        }

        private CombatCapsuleQuery BuildVisualCapsule(CombatPhysicsQuery query)
        {
            CombatPhysicsShape shape = query.Shape;
            CombatQueryHeader visualHeader = ToVisualCapsuleHeader(query.Header);
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    FixVector3 direction = shape.Direction.TryNormalize(out FixVector3 rayDirection)
                        ? rayDirection
                        : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
                    return new CombatCapsuleQuery(visualHeader, shape.Origin, shape.Origin + Scale(direction, shape.Length), Fix64.FromRatio(8, 100));
                case CombatPhysicsShapeKind.Sphere:
                    return new CombatCapsuleQuery(visualHeader, shape.Center - new FixVector3(shape.Radius, Fix64.Zero, Fix64.Zero), shape.Center + new FixVector3(shape.Radius, Fix64.Zero, Fix64.Zero), shape.Radius);
                case CombatPhysicsShapeKind.Aabb:
                    return new CombatCapsuleQuery(visualHeader, shape.Min, shape.Max, Fix64.FromRatio(8, 100));
                case CombatPhysicsShapeKind.Sector:
                    FixVector3 sectorDirection = shape.Direction.TryNormalize(out FixVector3 normalized)
                        ? normalized
                        : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
                    return new CombatCapsuleQuery(visualHeader, shape.Origin, shape.Origin + Scale(sectorDirection, shape.Radius), Fix64.FromRatio(18, 100));
                case CombatPhysicsShapeKind.Capsule:
                default:
                    return query.ToCapsuleQuery();
            }
        }

        private static CombatQueryHeader ToVisualCapsuleHeader(CombatQueryHeader source)
        {
            return new CombatQueryHeader(
                source.QueryId,
                CombatQueryKind.Capsule,
                source.SourceEntityId,
                source.TraceId,
                source.ActionId,
                source.SourceOrder,
                source.LayerMask);
        }

        private void ShowTraceSegment(CombatCapsuleQuery query, int index, bool hit)
        {
            Vector3 pointA = ToVector3(query.PointA);
            Vector3 pointB = ToVector3(query.PointB);
            Vector3 delta = pointB - pointA;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            Vector3 direction = delta.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude <= 0.0001f)
                side = Vector3.right;

            side.Normalize();
            float visualRadius = Mathf.Clamp(ToFloat(query.Radius), 0.12f, 0.42f);
            Material material = hit ? _traceHitMaterial : _traceMissMaterial;

            LineRenderer center = GetTraceLine(index * 3);
            ConfigureTraceLine(center, material, hit ? 0.045f : 0.035f, pointA, pointB);

            LineRenderer sideA = GetTraceLine(index * 3 + 1);
            ConfigureTraceLine(sideA, material, 0.02f, pointA + side * visualRadius, pointB + side * visualRadius);

            LineRenderer sideB = GetTraceLine(index * 3 + 2);
            ConfigureTraceLine(sideB, material, 0.02f, pointA - side * visualRadius, pointB - side * visualRadius);

            Transform start = GetTraceEndpoint(index * 2);
            Transform end = GetTraceEndpoint(index * 2 + 1);
            ConfigureEndpoint(start, pointA, material, hit ? 0.13f : 0.105f);
            ConfigureEndpoint(end, pointB, material, hit ? 0.13f : 0.105f);
        }

        private void ShowMissVisual()
        {
            if (_queries.Count > 0)
            {
                CombatCapsuleQuery query = _queries[0];
                ShowTraceVisual(query, false);
                ShowHitMarker(query.PointB, false);
            }
        }

        private FixVector3 GetHitPresentationPoint(CombatQueryResult hit)
        {
            FixVector3 targetCenter = hit.TargetEntityId.Value == EnemyEntityId
                ? _enemyPhysicsPosition
                : _playerPhysicsPosition;
            Vector3 center = ToVector3(targetCenter);
            Vector3 contact = ToVector3(hit.Point);
            Vector3 direction = contact - center;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = GetOpposingPhysicsPosition(hit.TargetEntityId) - center;

            Vector3 surface = ProjectToHurtboxSurface(center, direction, _hurtboxHalfExtents);
            return ToFixVector3(surface);
        }

        private FixVector3 GetQueryEndPoint(CombatPhysicsQuery query)
        {
            CombatPhysicsShape shape = query.Shape;
            switch (shape.Kind)
            {
                case CombatPhysicsShapeKind.Ray:
                    FixVector3 direction = shape.Direction.TryNormalize(out FixVector3 rayDirection)
                        ? rayDirection
                        : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
                    return shape.Origin + Scale(direction, shape.Length);
                case CombatPhysicsShapeKind.Sphere:
                    return shape.Center;
                case CombatPhysicsShapeKind.Aabb:
                    return (shape.Min + shape.Max) / Fix64.FromInt(2);
                case CombatPhysicsShapeKind.Sector:
                    FixVector3 sectorDirection = shape.Direction.TryNormalize(out FixVector3 normalized)
                        ? normalized
                        : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
                    return shape.Origin + Scale(sectorDirection, shape.Radius);
                case CombatPhysicsShapeKind.Capsule:
                default:
                    return shape.PointB;
            }
        }

        private static Vector3 ProjectToHurtboxSurface(Vector3 center, Vector3 direction, Vector3 halfExtents)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return center;

            float scale = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > 0.0001f)
                scale = Mathf.Min(scale, Mathf.Abs(halfExtents.x / direction.x));
            if (Mathf.Abs(direction.y) > 0.0001f)
                scale = Mathf.Min(scale, Mathf.Abs(halfExtents.y / direction.y));
            if (Mathf.Abs(direction.z) > 0.0001f)
                scale = Mathf.Min(scale, Mathf.Abs(halfExtents.z / direction.z));

            if (float.IsInfinity(scale))
                return center;

            return center + direction * scale;
        }

        private Vector3 GetOpposingPhysicsPosition(CombatEntityId targetEntityId)
        {
            return targetEntityId.Value == EnemyEntityId
                ? ToVector3(_playerPhysicsPosition)
                : ToVector3(_enemyPhysicsPosition);
        }

        private void ShowHitMarker(FixVector3 position, bool hit)
        {
            EnsureVisualMaterials();
            EnsureVisualRoot();
            if (_hitDebugMarker == null)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Combat_Hit_Result_Marker";
                marker.transform.SetParent(_combatVisualRoot, worldPositionStays: false);
                DisableAndDestroyCollider(marker);
                _hitDebugMarker = marker.transform;
                _hitDebugRenderer = marker.GetComponent<Renderer>();

                _hitRingRenderer = CreateLineRenderer("Combat_Hit_Ring", loop: true);
                _hitRingRenderer.transform.SetParent(_hitDebugMarker, worldPositionStays: false);

                _missCrossRendererA = CreateLineRenderer("Combat_Miss_Cross_A", loop: false);
                _missCrossRendererA.transform.SetParent(_hitDebugMarker, worldPositionStays: false);

                _missCrossRendererB = CreateLineRenderer("Combat_Miss_Cross_B", loop: false);
                _missCrossRendererB.transform.SetParent(_hitDebugMarker, worldPositionStays: false);

                GameObject label = new GameObject("Combat_Result_Label");
                label.transform.SetParent(_hitDebugMarker, worldPositionStays: false);
                _resultLabel = label.AddComponent<TextMesh>();
                _resultLabel.anchor = TextAnchor.MiddleCenter;
                _resultLabel.alignment = TextAlignment.Center;
                _resultLabel.fontSize = 48;
                _resultLabel.characterSize = 0.028f;
            }

            _hitDebugMarker.gameObject.SetActive(true);
            _hitDebugMarker.position = ToVector3(position);
            _hitDebugMarker.localScale = hit ? Vector3.one * 0.32f : Vector3.one * 0.24f;
            if (_hitDebugRenderer != null)
            {
                _hitDebugRenderer.enabled = hit;
                _hitDebugRenderer.sharedMaterial = hit ? _hitMarkerMaterial : _missMarkerMaterial;
            }

            ConfigureResultRing(hit);
            ConfigureMissCross(hit);
            ConfigureResultLabel(hit);
            _resultShownAt = Time.time;
            _lastResultWasHit = hit;
        }

        private void HideLegacyTraceDebugLine()
        {
            if (_traceDebugLine == null)
                return;

            _traceDebugLine.gameObject.SetActive(false);
            _traceDebugRenderer = _traceDebugRenderer ?? _traceDebugLine.GetComponent<Renderer>();
            if (_traceDebugRenderer != null)
                _traceDebugRenderer.enabled = false;

            _traceDebugCollider = _traceDebugCollider ?? _traceDebugLine.GetComponent<Collider>();
            if (_traceDebugCollider != null)
                _traceDebugCollider.enabled = false;
        }

        private void EnsureVisualRoot()
        {
            if (_combatVisualRoot != null)
                return;

            GameObject root = new GameObject("Combat_Showcase_Visuals");
            root.transform.SetParent(transform, worldPositionStays: false);
            _combatVisualRoot = root.transform;
        }

        private LineRenderer GetTraceLine(int index)
        {
            EnsureVisualRoot();
            while (_traceLinePool.Count <= index)
            {
                LineRenderer line = CreateLineRenderer($"Combat_Query_Line_{_traceLinePool.Count:00}", loop: false);
                line.transform.SetParent(_combatVisualRoot, worldPositionStays: false);
                _traceLinePool.Add(line);
            }

            return _traceLinePool[index];
        }

        private Transform GetTraceEndpoint(int index)
        {
            EnsureVisualRoot();
            while (_traceEndpointPool.Count <= index)
            {
                GameObject endpoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                endpoint.name = $"Combat_Query_Endpoint_{_traceEndpointPool.Count:00}";
                endpoint.transform.SetParent(_combatVisualRoot, worldPositionStays: false);
                DisableAndDestroyCollider(endpoint);
                _traceEndpointPool.Add(endpoint.transform);
            }

            return _traceEndpointPool[index];
        }

        private static void DisableAndDestroyCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider == null)
                return;

            collider.enabled = false;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(collider);
            else
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private LineRenderer CreateLineRenderer(string name, bool loop)
        {
            GameObject lineObject = new GameObject(name);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void ConfigureTraceLine(LineRenderer line, Material material, float width, Vector3 pointA, Vector3 pointB)
        {
            line.gameObject.SetActive(true);
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            line.SetPosition(0, pointA);
            line.SetPosition(1, pointB);
        }

        private static void ConfigureEndpoint(Transform endpoint, Vector3 position, Material material, float size)
        {
            endpoint.gameObject.SetActive(true);
            endpoint.position = position;
            endpoint.localScale = Vector3.one * size;
            Renderer renderer = endpoint.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private void HideUnusedTraceSegments(int visibleQueryCount)
        {
            int visibleLineCount = visibleQueryCount * 3;
            for (int i = visibleLineCount; i < _traceLinePool.Count; i++)
                _traceLinePool[i].gameObject.SetActive(false);

            int visibleEndpointCount = visibleQueryCount * 2;
            for (int i = visibleEndpointCount; i < _traceEndpointPool.Count; i++)
                _traceEndpointPool[i].gameObject.SetActive(false);
        }

        private void ConfigureResultRing(bool hit)
        {
            if (_hitRingRenderer == null)
                return;

            _hitRingRenderer.gameObject.SetActive(hit);
            if (!hit)
                return;

            _hitRingRenderer.sharedMaterial = _hitMarkerMaterial;
            _hitRingRenderer.startWidth = 0.035f;
            _hitRingRenderer.endWidth = 0.035f;
            SetCircle(_hitRingRenderer, Vector3.zero, 1.15f, 40);
        }

        private void ConfigureMissCross(bool hit)
        {
            bool showMiss = !hit;
            if (_missCrossRendererA != null)
            {
                _missCrossRendererA.gameObject.SetActive(showMiss);
                if (showMiss)
                    ConfigureLocalLine(_missCrossRendererA, _missMarkerMaterial, 0.04f, new Vector3(-0.8f, 0f, -0.8f), new Vector3(0.8f, 0f, 0.8f));
            }

            if (_missCrossRendererB != null)
            {
                _missCrossRendererB.gameObject.SetActive(showMiss);
                if (showMiss)
                    ConfigureLocalLine(_missCrossRendererB, _missMarkerMaterial, 0.04f, new Vector3(-0.8f, 0f, 0.8f), new Vector3(0.8f, 0f, -0.8f));
            }
        }

        private void ConfigureResultLabel(bool hit)
        {
            if (_resultLabel == null)
                return;

            _resultLabel.text = hit ? "HIT" : "MISS";
            _resultLabel.color = hit ? new Color(1f, 0.22f, 0.16f) : new Color(0.65f, 0.75f, 0.85f);
            _resultLabel.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            _resultLabel.transform.localRotation = Quaternion.identity;
        }

        private static void ConfigureLocalLine(LineRenderer line, Material material, float width, Vector3 pointA, Vector3 pointB)
        {
            line.sharedMaterial = material;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.SetPosition(0, pointA);
            line.SetPosition(1, pointB);
        }

        private static void SetCircle(LineRenderer line, Vector3 center, float radius, int segments)
        {
            line.useWorldSpace = false;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void AnimateCombatVisuals()
        {
            if (_hitDebugMarker == null || !_hitDebugMarker.gameObject.activeSelf)
                return;

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (_resultLabel != null && camera != null)
                _resultLabel.transform.rotation = Quaternion.LookRotation(_resultLabel.transform.position - camera.transform.position, Vector3.up);

            float age = Mathf.Max(0f, Time.time - _resultShownAt);
            float pulse = Mathf.Clamp01(1f - age * 1.6f);
            if (_hitRingRenderer != null && _hitRingRenderer.gameObject.activeSelf)
            {
                float radius = Mathf.Lerp(1.15f, 1.65f, 1f - pulse);
                SetCircle(_hitRingRenderer, Vector3.zero, radius, 40);
            }

            if (_hitDebugRenderer != null && _lastResultWasHit)
                _hitDebugMarker.localScale = Vector3.one * Mathf.Lerp(0.32f, 0.46f, pulse);
        }

        private void EnsureVisualMaterials()
        {
            if (_traceMissMaterial == null)
                _traceMissMaterial = CreateRuntimeMaterial("CombatTraceMiss", new Color(1f, 0.72f, 0.18f));

            if (_traceHitMaterial == null)
                _traceHitMaterial = CreateRuntimeMaterial("CombatTraceHit", new Color(0.36f, 0.94f, 0.58f));

            if (_hitMarkerMaterial == null)
                _hitMarkerMaterial = CreateRuntimeMaterial("CombatHitMarker", new Color(1f, 0.24f, 0.18f));

            if (_missMarkerMaterial == null)
                _missMarkerMaterial = CreateRuntimeMaterial("CombatMissMarker", new Color(0.55f, 0.65f, 0.75f));
        }

        private static Material CreateRuntimeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }

        private Transform GetSelectedMarker()
        {
            ResolveSceneMarkers();
            return _selectedEntityId.Value == EnemyEntityId ? _enemyMarker : _playerMarker;
        }

        private FixVector3 GetSelectedPhysicsPosition()
        {
            return _selectedEntityId.Value == EnemyEntityId ? _enemyPhysicsPosition : _playerPhysicsPosition;
        }

        private FixVector3 GetTargetPhysicsPosition()
        {
            return _selectedEntityId.Value == EnemyEntityId ? _playerPhysicsPosition : _enemyPhysicsPosition;
        }

        private string GetTargetEntityName()
        {
            return _selectedEntityId.Value == EnemyEntityId ? "Player" : "Enemy";
        }

        private bool IsTargetAlive()
        {
            return _selectedEntityId.Value == EnemyEntityId ? _playerCurrentHp > 0 : _enemyCurrentHp > 0;
        }

        private void ApplyDamageToTarget(int damage)
        {
            if (_selectedEntityId.Value == EnemyEntityId)
                _playerCurrentHp = Mathf.Max(0, _playerCurrentHp - damage);
            else
                _enemyCurrentHp = Mathf.Max(0, _enemyCurrentHp - damage);
        }

        private void AddScore(int delta)
        {
            _score = Mathf.Max(0, _score + delta);
        }

        private void TryAdvanceRoundAfterKill()
        {
            if (_selectedEntityId.Value != PlayerEntityId || _enemyCurrentHp > 0)
                return;

            AddScore(500);
            _round++;
            _streak = 0;
            _enemyCurrentHp = _enemyHp;
            if (_enemyMarker != null)
            {
                float lane = (_round % 2 == 0) ? 1.2f : -1.2f;
                float distance = 2.2f + Mathf.Min(3, _round) * 0.45f;
                _enemyMarker.position = new Vector3(distance, _enemyMarker.position.y, lane);
            }

            RebuildPhysicsWorld(logBinding: false);
            LogEvent($"Round {_round} started. Enemy respawned; score={_score}.");
        }

        private string BuildLastQueryDebugSummary()
        {
            if (_lastQueryDebugReport == null)
                return "Query: waiting";

            CombatPhysicsQueryDebugReport report = _lastQueryDebugReport;
            return $"{report.ShapeKind} raw={report.BroadphaseRawCandidateCount} dedup={report.BroadphaseCandidateCount} post={report.PostFilterCandidateCount} hit={report.HitCount}";
        }

        private FixVector3 BuildDirectionToTarget()
        {
            FixVector3 delta = GetTargetPhysicsPosition() - GetSelectedPhysicsPosition();
            return delta.TryNormalize(out FixVector3 direction) ? direction : FixVector3.Zero;
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"({position.x:0.00},{position.y:0.00},{position.z:0.00})";
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRatio(Mathf.RoundToInt(value * 1000f), 1000);
        }

        private void UpdateSnapshot()
        {
            _snapshotBuilder.Clear();
            for (int i = 0; i < _replayRecorder.Inputs.Count; i++)
                _snapshotBuilder.AddInput(_replayRecorder.Inputs[i]);

            for (int i = 0; i < _queryHeaders.Count; i++)
                _snapshotBuilder.AddQuery(new CombatQueryTrace(_clock.CurrentFrame, _queryHeaders[i]));

            for (int i = 0; i < _hitResults.Count; i++)
                _snapshotBuilder.AddHit(new CombatHitExplain(_hitResults[i], _hitResults[i].Kind.ToString()));

            _lastSnapshot = _snapshotBuilder.Build(_clock.CurrentFrame);
        }

        private void LogEvent(string message)
        {
            _eventLog.Add(message);
            if (_eventLog.Count > MaxLog)
                _eventLog.RemoveAt(0);
        }
    }
}
