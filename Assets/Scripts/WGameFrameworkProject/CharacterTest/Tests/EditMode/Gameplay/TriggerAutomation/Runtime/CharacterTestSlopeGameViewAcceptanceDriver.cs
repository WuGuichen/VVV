using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Runtime;
using MxFramework.Tests.Game.CharacterTest;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal enum CharacterTestSlopeGameViewRunStatus
    {
        None = 0,
        Running = 1,
        Passed = 2,
        Failed = 3,
        Canceled = 4
    }

    [Serializable]
    internal sealed class CharacterTestSlopeGameViewAuthorityEvidence
    {
        public string segment = string.Empty;
        public string phase = string.Empty;
        public long tick;
        public int x_mm;
        public int y_mm;
        public int z_mm;
        public bool grounded;
        public int collision_flags;
        public string collision_flags_text = string.Empty;
        public string surface_stable_id = string.Empty;
        public int body_id;
        public int collider_id;
        public string shape = string.Empty;
        public long normal_x_raw;
        public long normal_y_raw;
        public long normal_z_raw;
        public long ground_min_normal_y_raw;
        public long ground_snap_raw;
        public string classification = string.Empty;
        public string material = string.Empty;
        public long requested_x_raw;
        public long requested_y_raw;
        public long requested_z_raw;
        public long applied_x_raw;
        public long applied_y_raw;
        public long applied_z_raw;
        public long applied_dot_normal_raw2;
        public long applied_dot_lower_bound_raw2;
        public long resolved_x_raw;
        public long resolved_y_raw;
        public long resolved_z_raw;
        public int control_intent_source_id;
        public int active_action_id;
        public bool formal_view_present;
        public int view_x_mm;
        public int view_y_mm;
        public int view_z_mm;
        public int view_yaw_millidegrees;
        public int view_position_error_mm;
        public int view_yaw_error_millidegrees;
        public bool slope_top_footprint_overlap;
        public long slope_top_separation_raw;
    }

    [Serializable]
    internal sealed class CharacterTestSlopeGameViewSegmentEvidence
    {
        public string segment_id = string.Empty;
        public string display_name = string.Empty;
        public bool passed;
        public string message = string.Empty;
        public long start_tick;
        public long end_tick;
        public string screenshot_point = string.Empty;
        public string command_evidence = string.Empty;
        public List<CharacterTestSlopeGameViewAuthorityEvidence> samples =
            new List<CharacterTestSlopeGameViewAuthorityEvidence>();
    }

    [Serializable]
    internal sealed class CharacterTestSlopeGameViewReport
    {
        public string schema = "mx.character-test.slope-game-view-acceptance.v1";
        public string scene_path = CharacterTestSlopePlayableFixture.ScenePath;
        public ulong level_hash = CharacterTestSlopePlayableFixture.ExpectedLevelHash;
        public string driver_boundary =
            "Formal active CharacterTestScene with exactly one live LocalPlayer and production view. "
            + "Test-only direct authority setup establishes each independently positioned segment/subphase; "
            + "each 15/30 up-stop-down sequence remains continuous after its initial setup. "
            + "Inside a phase, only FakeInputProvider, a formal CastAbilitySlot command, or a typed motion request, "
            + "the production command path, "
            + "formal fixed ticks, and production presentation/camera bridges advance state.";
        public string[] labels =
        {
            "Static OBB slopes · slope limit 45°",
            "15° / 30° walkable",
            "50° blocked"
        };
        public CharacterTestSlopeGameViewRunStatus status;
        public string message = string.Empty;
        public string started_utc = string.Empty;
        public string completed_utc = string.Empty;
        public List<CharacterTestSlopeGameViewSegmentEvidence> segments =
            new List<CharacterTestSlopeGameViewSegmentEvidence>();
    }

    /// <summary>
    /// Test-only Game View acceptance driver. It runs only in an Editor test
    /// assembly and never becomes part of the formal CharacterTest scene.
    /// </summary>
    /// <remarks>
    /// The formal scene supplies the serialized level, live LocalPlayer,
    /// visible OBB meshes, production view/camera bridges and framebuffer.
    /// Test code only positions the live entity at each segment start and
    /// supplies deterministic input through FakeInputProvider, the formal
    /// CastAbilitySlot command, or a typed motion request.
    /// </remarks>
    public sealed class CharacterTestSlopeGameViewAcceptanceDriver : IDisposable
    {
        internal const string ReportAssetRelativePath =
            "Temp/MxFramework/TriggerTests/mx.trigger.test.character-test.slope-game-view/latest.json";

        private const int StopTicks = 60;
        private const int StopSettleTicks = 30;
        private const int LongitudinalPlayableTicks = 60;
        private const int TraversePlayableTicks = 24;
        private const int DashPlayableTicks = 30;
        private const int ImpulsePlayableTicks = 30;
        private const int ScreenshotCameraSettleTicks = 60;
        private const long LiveEdgeStartLongitudinalRaw = -2000000L;
        private const short LiveEdgeInputRaw = -200;
        private const float SegmentTimeoutSeconds = 20f;
        private const float RunTimeoutSeconds = 900f;
        private const int EvidenceStride = 20;
        private static readonly RuntimeTraceIdentity DashCommandTraceId = new RuntimeTraceIdentity(980001);

        private static readonly string[] SegmentIds =
        {
            "15-up-stop-down",
            "30-up-stop-down",
            "15-30-traverse",
            "50-blocked",
            "30-jump-land",
            "30-real-edge",
            "dash327-30-50",
            "impulse-30-50"
        };

        private static readonly string[] SegmentNames =
        {
            "15° 上行 / 停止 / 下行",
            "30° 上行 / 停止 / 下行",
            "15° / 30° 横向穿越",
            "50° 持续输入阻挡",
            "30° 跳跃并落回坡面",
            "30° 真实边缘离地",
            "Dash 327 · 30° / 50°",
            "External Impulse · 30° / 50°"
        };

        private GameManager _manager;
        private GameSlice _slice;
        private CharacterTestCharacterEntity _player;
        private CharacterTestCharacterPhysicsRuntime _physics;
        private readonly Dictionary<int, SlopeSpec> _slopes = new Dictionary<int, SlopeSpec>();
        private FakeInputProvider _inputProvider;
        private IInputProvider _previousInputProvider;
        private bool _previousPaused;
        private CharacterTestSlopeGameViewReport _report;
        private CharacterTestSlopeGameViewSegmentEvidence _segment;
        private CharacterTestSlopeAuthorityRow _latestRow;
        private UIDocument _uiDocument;
        private GameObject _overlayObject;
        private PanelSettings _panelSettings;
        private Label _statusLabel;
        private Label _authorityLabel;
        private int _segmentIndex = -1;
        private int _phase;
        private int _phaseTick;
        private int _phaseStartX;
        private int _phaseStartY;
        private int _phaseStartZ;
        private int _maximumY;
        private int _minimumY;
        private int _contactCount;
        private int _groundedCount;
        private int _stopStableX;
        private int _stopStableZ;
        private bool _stopStableReferenceCaptured;
        private bool _sawAirborne;
        private bool _sawLanding;
        private bool _sawSteep;
        private bool _sawRequested;
        private bool _sawAppliedOrResolved;
        private bool _sawAction327;
        private bool _sawEdgeBeyondGuard;
        private bool _sawAirborneBeyondGuard;
        private Vector3 _latestViewPosition;
        private Quaternion _latestViewRotation = Quaternion.identity;
        private int _latestViewPositionErrorMillimeters;
        private int _latestViewYawErrorMilliDegrees;
        private bool _latestSlopeTopFootprintOverlap;
        private long _latestSlopeTopSeparationRaw;
        private bool _screenshotReady;
        private bool _reportWritten;
        private bool _cleanupComplete;
        private bool _liveStateCaptured;
        private float _segmentDeadline;
        private float _runDeadline;
        private string _phaseName = string.Empty;
        private string _reportPath = string.Empty;
        private string _initializationStage = "not-started";

        internal static CharacterTestSlopeGameViewAcceptanceDriver Active { get; private set; }

        internal CharacterTestSlopeGameViewRunStatus Status =>
            _report?.status ?? CharacterTestSlopeGameViewRunStatus.None;
        internal string Message => _report?.message ?? string.Empty;
        internal string ReportPath => _reportPath;
        internal bool ScreenshotReady => _screenshotReady;
        internal string CurrentSegmentId => _segment?.segment_id ?? string.Empty;
        internal string CurrentPhase => _phaseName;
        internal bool ReportWritten => _reportWritten;
        internal IReadOnlyList<CharacterTestSlopeGameViewSegmentEvidence> Segments =>
            _report != null
                ? (IReadOnlyList<CharacterTestSlopeGameViewSegmentEvidence>)_report.segments
                : Array.Empty<CharacterTestSlopeGameViewSegmentEvidence>();

        internal static CharacterTestSlopeGameViewAcceptanceDriver Begin(
            GameManager manager,
            PanelSettings panelSettings)
        {
            if (Active != null)
                throw new InvalidOperationException("A slope Game View acceptance run is already active.");
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            if (panelSettings == null)
                throw new ArgumentNullException(nameof(panelSettings));
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid()
                || !string.Equals(
                    activeScene.path,
                    CharacterTestSlopePlayableFixture.ScenePath,
                    StringComparison.Ordinal)
                || manager.gameObject.scene.handle != activeScene.handle)
            {
                throw new InvalidOperationException(
                    "The slope driver requires the GameManager owned by the active formal CharacterTestScene.");
            }
            CharacterTestSlopePlayableFixture.AssertFormalManagerRuntime(manager);
            var driver = new CharacterTestSlopeGameViewAcceptanceDriver();
            try
            {
                driver.Initialize(manager, panelSettings);
                Active = driver;
                return driver;
            }
            catch (Exception exception)
            {
                string stage = driver != null
                    ? driver._initializationStage
                    : "add-component";
                driver?.CleanupInitializationFailure();
                throw new InvalidOperationException(
                    "Slope driver initialization failed at stage '"
                    + stage + "': " + exception.Message,
                    exception);
            }
        }

        internal object BuildToolEvidence()
        {
            return new
            {
                status = Status.ToString(),
                message = Message,
                scene_path = CharacterTestSlopePlayableFixture.ScenePath,
                level_hash = CharacterTestSlopePlayableFixture.ExpectedLevelHash,
                segment_id = CurrentSegmentId,
                phase = CurrentPhase,
                screenshot_ready = ScreenshotReady,
                report_written = ReportWritten,
                report_path = ReportPath,
                driver_boundary = _report?.driver_boundary ?? string.Empty,
                latest_authority = _segment?.samples.Count > 0
                    ? _segment.samples[_segment.samples.Count - 1]
                    : null,
                segments = _report?.segments.ToArray()
                    ?? Array.Empty<CharacterTestSlopeGameViewSegmentEvidence>()
            };
        }

        internal void Cancel(string reason)
        {
            if (!IsTerminal(Status))
                Finish(CharacterTestSlopeGameViewRunStatus.Canceled,
                    string.IsNullOrWhiteSpace(reason) ? "Canceled by the caller." : reason);
        }

        internal bool AcknowledgeScreenshot(out string message)
        {
            if (Status != CharacterTestSlopeGameViewRunStatus.Running || !_screenshotReady)
            {
                message = "No running slope segment is waiting for screenshot acknowledgement.";
                return false;
            }
            _screenshotReady = false;
            message = "Screenshot acknowledged for " + CurrentSegmentId + ".";
            BeginNextSegment();
            return true;
        }

        private void Initialize(GameManager manager, PanelSettings panelSettings)
        {
            _initializationStage = "report";
            _reportPath = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                ReportAssetRelativePath));
            _report = new CharacterTestSlopeGameViewReport
            {
                status = CharacterTestSlopeGameViewRunStatus.Running,
                message = "Initializing formal-scene Game View acceptance.",
                started_utc = DateTime.UtcNow.ToString("O")
            };
            _initializationStage = "runtime-context";
            _manager = manager;
            CharacterTestRuntimeContext context = manager.RuntimeContext
                ?? throw new InvalidOperationException("Formal GameManager runtime context is unavailable.");
            _slice = context.Slice
                ?? throw new InvalidOperationException("Formal GameManager live Slice is unavailable.");
            CharacterTestCharacterRuntimeModule characterRuntime = _slice.CharacterRuntime
                ?? throw new InvalidOperationException("Formal Slice character runtime is unavailable.");
            CharacterTestCharacterUnityViewBridge viewBridge = characterRuntime.ViewBridge
                ?? throw new InvalidOperationException("Formal production character view bridge is unavailable.");
            if (UnityEngine.Camera.main == null)
                throw new InvalidOperationException("Formal CharacterTestScene has no active MainCamera.");
            _physics = characterRuntime.PhysicsRuntime
                ?? throw new InvalidOperationException("Formal live Slice has no Character physics runtime.");
            _initializationStage = "formal-slope-specs";
            AddSlopeSpec(CharacterTestSlopePlayableFixture.Slope15BodyId);
            AddSlopeSpec(CharacterTestSlopePlayableFixture.Slope30BodyId);
            AddSlopeSpec(CharacterTestSlopePlayableFixture.Slope50BodyId);

            _initializationStage = "input-provider";
            _previousInputProvider = context.InputProvider;
            _previousPaused = context.Paused;
            _liveStateCaptured = true;
            _inputProvider = new FakeInputProvider();
            _inputProvider.SetContext(InputContext.Gameplay);
            _slice.SetLocalInputProvider(_inputProvider);
            _initializationStage = "main-menu";
            CharacterTestMainMenuCoordinator mainMenu = context.MainMenuCoordinator
                ?? throw new InvalidOperationException("Formal main-menu coordinator is unavailable.");
            mainMenu.SetPaused(true);
            CharacterTestMainMenuCommandResult start =
                mainMenu.StartOrContinueFromMainMenu();
            if (!start.Success)
                throw new InvalidOperationException(start.Message);
            _initializationStage = "ui-toolkit-overlay";
            CreateValidationOverlay(panelSettings);
            _initializationStage = "ready";
            _runDeadline = Time.realtimeSinceStartup + RunTimeoutSeconds;
            _report.message =
                "Bootstrapping the formal queued LocalPlayer through the first production fixed tick.";
            UpdateOverlay();
        }

        internal void TickDriver()
        {
            if (_report == null || Status != CharacterTestSlopeGameViewRunStatus.Running)
                return;
            try
            {
                if (_screenshotReady)
                    return;
                if (Time.realtimeSinceStartup > _runDeadline)
                    throw new TimeoutException("The complete slope Game View run exceeded its timeout.");
                if (_player == null)
                {
                    BootstrapFormalLocalPlayer();
                    UpdateOverlay();
                    return;
                }
                if (Time.realtimeSinceStartup > _segmentDeadline)
                    throw new TimeoutException("Segment timed out: " + CurrentSegmentId + " / " + _phaseName);

                TickCurrentSegment();
                UpdateOverlay();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void BootstrapFormalLocalPlayer()
        {
            EnsureFormalRuntimeIdentity();
            _inputProvider.SetSnapshot(InputSnapshot.Empty);
            long beforeFrame = _slice.CurrentFrame.Value;
            if (!_manager.StepSimulationOnceForRuntimeAiPlannerDev())
                throw new InvalidOperationException("Formal GameManager rejected the bootstrap fixed tick.");
            long afterFrame = _slice.CurrentFrame.Value;
            if (afterFrame != beforeFrame + 1L)
            {
                throw new InvalidOperationException(
                    "Formal bootstrap must advance exactly one runtime frame; before="
                    + beforeFrame + ", after=" + afterFrame + ".");
            }
            EnsureFormalRuntimeIdentity();

            CharacterTestCharacterEntity candidate = FindUniqueLocalPlayer(
                _slice,
                out int localPlayerCount);
            if (localPlayerCount > 1)
            {
                throw new InvalidOperationException(
                    "Formal live Slice contains multiple LocalPlayers after bootstrap: "
                    + localPlayerCount + ".");
            }
            if (candidate == null
                || !_slice.CharacterRuntime.ViewBridge.HasView(candidate.Id))
            {
                _report.message = "Waiting for the queued formal LocalPlayer and production view; count="
                    + localPlayerCount + ".";
                return;
            }

            _player = candidate;
            _report.message = "Formal LocalPlayer and production view are ready.";
            BeginNextSegment();
        }

        private void BeginNextSegment()
        {
            _segmentIndex++;
            if (_segmentIndex >= SegmentIds.Length)
            {
                Finish(CharacterTestSlopeGameViewRunStatus.Passed,
                    "All eight formal-scene Game View slope segments passed typed authority checks.");
                return;
            }

            _phase = 0;
            ResetPhaseCounters();
            _segmentDeadline = Time.realtimeSinceStartup + SegmentTimeoutSeconds;
            _segment = new CharacterTestSlopeGameViewSegmentEvidence
            {
                segment_id = SegmentIds[_segmentIndex],
                display_name = SegmentNames[_segmentIndex],
                start_tick = _slice.CurrentFrame.Value,
                screenshot_point = "segment-complete:" + SegmentIds[_segmentIndex]
            };
            _report.segments.Add(_segment);
            _report.message = "Running " + _segment.display_name;
            BeginPhaseForCurrentSegment();
            UpdateOverlay();
        }

        private void BeginPhaseForCurrentSegment()
        {
            ResetPhaseCounters();
            switch (_segmentIndex)
            {
                case 0:
                    BeginLongitudinalPhase(CharacterTestSlopePlayableFixture.Slope15BodyId);
                    break;
                case 1:
                    BeginLongitudinalPhase(CharacterTestSlopePlayableFixture.Slope30BodyId);
                    break;
                case 2:
                    BeginTraversePhase(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope15BodyId
                        : CharacterTestSlopePlayableFixture.Slope30BodyId);
                    break;
                case 3:
                    _phaseName = "50.blocked";
                    PlaceOnSlope(
                        CharacterTestSlopePlayableFixture.Slope50BodyId,
                        CharacterTestSlopePlayableFixture.SteepStartLongitudinalRaw,
                        0,
                        false,
                        CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
                    CapturePhaseStart();
                    break;
                case 4:
                    _phaseName = "30.jump-land";
                    PlaceAndSettle(CharacterTestSlopePlayableFixture.Slope30BodyId, 0, 0);
                    CapturePhaseStart();
                    break;
                case 5:
                    _phaseName = "30.edge";
                    PlaceAndSettle(
                        CharacterTestSlopePlayableFixture.Slope30BodyId,
                        LiveEdgeStartLongitudinalRaw,
                        0);
                    CapturePhaseStart();
                    break;
                case 6:
                    BeginDashPhase(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope30BodyId
                        : CharacterTestSlopePlayableFixture.Slope50BodyId);
                    break;
                case 7:
                    BeginImpulsePhase(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope30BodyId
                        : CharacterTestSlopePlayableFixture.Slope50BodyId);
                    break;
                default:
                    throw new InvalidOperationException("Unknown slope acceptance segment.");
            }
        }

        private void BeginLongitudinalPhase(int bodyId)
        {
            if (_phase == 0)
            {
                _phaseName = BodyAngle(bodyId) + ".up";
                PlaceAndSettle(bodyId, CharacterTestSlopePlayableFixture.UphillStartLongitudinalRaw, 0);
            }
            else if (_phase == 1)
            {
                _phaseName = BodyAngle(bodyId) + ".stop";
            }
            else
            {
                _phaseName = BodyAngle(bodyId) + ".down";
            }
            CapturePhaseStart();
        }

        private void BeginTraversePhase(int bodyId)
        {
            _phaseName = BodyAngle(bodyId) + ".traverse";
            PlaceAndSettle(bodyId, 0, CharacterTestSlopePlayableFixture.TraverseStartLateralRaw);
            CapturePhaseStart();
        }

        private void BeginDashPhase(int bodyId)
        {
            bool walkable = bodyId == CharacterTestSlopePlayableFixture.Slope30BodyId;
            _phaseName = BodyAngle(bodyId) + ".dash327";
            PlaceOnSlope(
                bodyId,
                CharacterTestSlopePlayableFixture.DashStartLongitudinalRaw,
                0,
                walkable,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            if (walkable)
                SettleWalkable();
            if (!_slice.AbilityHotbar.AssignSlot(
                    CharacterTestSlopePlayableFixture.DashStrikeSlot,
                    CharacterTestSlopePlayableFixture.DashStrikeAbilityId,
                    out _))
                throw new InvalidOperationException("Dash 327 hotbar slot assignment was rejected.");
            CapturePhaseStart();
        }

        private void BeginImpulsePhase(int bodyId)
        {
            bool walkable = bodyId == CharacterTestSlopePlayableFixture.Slope30BodyId;
            _phaseName = BodyAngle(bodyId) + ".impulse";
            PlaceOnSlope(
                bodyId,
                CharacterTestSlopePlayableFixture.ImpulseStartLongitudinalRaw,
                0,
                walkable,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            if (walkable)
                SettleWalkable();
            _player.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                CharacterTestSlopePlayableFixture.ImpulseXMillimetersPerSecond,
                0,
                CharacterTestSlopePlayableFixture.ImpulseZMillimetersPerSecond,
                CharacterMovementMode.Run,
                new CharacterActionId(990327),
                980002,
                CharacterTestCharacterMotionRequestSpace.World));
            CapturePhaseStart();
        }

        private void TickCurrentSegment()
        {
            switch (_segmentIndex)
            {
                case 0:
                    TickLongitudinal(CharacterTestSlopePlayableFixture.Slope15BodyId);
                    break;
                case 1:
                    TickLongitudinal(CharacterTestSlopePlayableFixture.Slope30BodyId);
                    break;
                case 2:
                    TickTraverse(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope15BodyId
                        : CharacterTestSlopePlayableFixture.Slope30BodyId);
                    break;
                case 3:
                    TickSteepBlocked();
                    break;
                case 4:
                    TickJumpLand();
                    break;
                case 5:
                    TickEdge();
                    break;
                case 6:
                    TickDash(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope30BodyId
                        : CharacterTestSlopePlayableFixture.Slope50BodyId);
                    break;
                case 7:
                    TickImpulse(_phase == 0
                        ? CharacterTestSlopePlayableFixture.Slope30BodyId
                        : CharacterTestSlopePlayableFixture.Slope50BodyId);
                    break;
            }
        }

        private void TickLongitudinal(int bodyId)
        {
            int limit;
            short input;
            if (_phase == 0)
            {
                limit = LongitudinalPlayableTicks;
                input = CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw;
            }
            else if (_phase == 1)
            {
                limit = StopTicks;
                input = 0;
            }
            else
            {
                limit = LongitudinalPlayableTicks;
                input = (short)-CharacterTestSlopePlayableFixture.WalkableLongitudinalInputRaw;
            }
            Observe(TickLive(
                input,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput), bodyId);
            if (_phase == 1)
            {
                if (_phaseTick == StopSettleTicks)
                {
                    _stopStableX = _latestRow.XMillimeters;
                    _stopStableZ = _latestRow.ZMillimeters;
                    _stopStableReferenceCaptured = true;
                }
                else if (_phaseTick > StopSettleTicks)
                {
                    if (_latestRow.XMillimeters != _stopStableX
                        || _latestRow.ZMillimeters != _stopStableZ
                        || !_latestRow.Grounded)
                    {
                        throw new InvalidOperationException(
                            _phaseName + " did not remain position-stable and grounded after braking.");
                    }
                }
            }
            if (++_phaseTick < limit)
                return;

            if (_phase == 0 && _maximumY - _phaseStartY <= 200)
                throw new InvalidOperationException(_phaseName + " did not ascend more than 200 mm.");
            if (_phase == 1 && (_groundedCount != limit
                || !_stopStableReferenceCaptured
                || _latestRow.XMillimeters != _stopStableX
                || _latestRow.ZMillimeters != _stopStableZ))
            {
                throw new InvalidOperationException(_phaseName + " did not remain grounded and position-stable.");
            }
            if (_phase == 2 && _minimumY - _phaseStartY >= -200)
                throw new InvalidOperationException(_phaseName + " did not descend more than 200 mm.");
            if (_contactCount == 0)
                throw new InvalidOperationException(_phaseName + " never produced typed slope contact.");

            if (_phase < 2)
            {
                _phase++;
                BeginPhaseForCurrentSegment();
            }
            else
            {
                CompleteSegment("Walkable up/stop/down authority checks passed.");
            }
        }

        private void TickTraverse(int bodyId)
        {
            Observe(TickLive(
                0,
                CharacterTestSlopePlayableFixture.WalkableTraverseInputRaw,
                CharacterTestRuntimeCommandSources.PlayerInput), bodyId);
            if (++_phaseTick < TraversePlayableTicks)
                return;
            if (_contactCount == 0 || _groundedCount == 0 ||
                Math.Abs(_latestRow.ZMillimeters - _phaseStartZ) <= 200)
            {
                throw new InvalidOperationException(_phaseName + " did not retain contact and traverse visibly.");
            }
            if (_phase == 0)
            {
                _phase = 1;
                BeginPhaseForCurrentSegment();
            }
            else
            {
                CompleteSegment("15° and 30° lateral traverses passed.");
            }
        }

        private void TickSteepBlocked()
        {
            Observe(TickLive(
                CharacterTestSlopePlayableFixture.SteepUphillInputRaw,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput),
                CharacterTestSlopePlayableFixture.Slope50BodyId);
            if (++_phaseTick < CharacterTestSlopePlayableFixture.SteepTicks)
                return;
            if (!_sawSteep || _contactCount == 0 ||
                _maximumY - _phaseStartY > CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters)
            {
                throw new InvalidOperationException("50° input did not remain steep and blocked.");
            }
            PrepareSteepBlockedScreenshotRow();
            CompleteSegment(
                "50° remained blocked for 120 ticks; representative screenshot row revalidated OBB/Steep/non-entering.");
        }

        private void TickJumpLand()
        {
            int tick = _phaseTick;
            Observe(TickLive(
                0,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput,
                jumpPressed: tick == 0,
                jumpHeld: tick < CharacterTestSlopePlayableFixture.JumpHeldTicks,
                jumpReleased: tick == CharacterTestSlopePlayableFixture.JumpHeldTicks),
                CharacterTestSlopePlayableFixture.Slope30BodyId);
            _sawAirborne |= !_latestRow.Grounded;
            _sawLanding |= _sawAirborne && _latestRow.Grounded &&
                _latestRow.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope30BodyId);
            _phaseTick++;
            if (_sawLanding)
            {
                CompleteSegment("30° jump left and landed back on typed slope authority.");
                return;
            }
            if (_phaseTick >= CharacterTestSlopePlayableFixture.JumpLandMaxTicks)
                throw new InvalidOperationException("30° jump did not land before timeout.");
        }

        private void TickEdge()
        {
            Observe(TickLive(
                LiveEdgeInputRaw,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput),
                CharacterTestSlopePlayableFixture.Slope30BodyId);
            int gap = ComputeLongitudinalEdgeGapMillimeters(
                CharacterTestSlopePlayableFixture.Slope30BodyId,
                _latestRow);
            if (gap > CharacterTestSlopePlayableFixture.EdgeGuardMillimeters)
            {
                _sawEdgeBeyondGuard = true;
                _sawAirborneBeyondGuard |= !_latestRow.Grounded;
                bool slopeSnap = _latestRow.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope30BodyId)
                    && (_latestRow.Diagnostic.CollisionFlags & CombatMotionCollisionFlags.GroundSnap) != 0;
                if (slopeSnap)
                    throw new InvalidOperationException("Slope snap remained after the true edge guard was exceeded.");
            }
            _phaseTick++;
            if (_sawEdgeBeyondGuard && _sawAirborneBeyondGuard)
            {
                CompleteSegment("30° true edge exceeded 0.10 m and became airborne.");
                return;
            }
            if (_phaseTick < CharacterTestSlopePlayableFixture.EdgeTicks)
                return;
            throw new InvalidOperationException("The true 30° edge did not produce an airborne authority phase.");
        }

        private void TickDash(int bodyId)
        {
            if (_phaseTick == 0)
            {
                RuntimeCommandValidationResult enqueue = _slice.EnqueueCastAbilitySlot(
                    _player.Id.Value,
                    CharacterTestSlopePlayableFixture.DashStrikeSlot,
                    CharacterTestSlopePlayableFixture.DashStrikeAbilityId,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    DashCommandTraceId);
                if (!enqueue.Success)
                {
                    throw new InvalidOperationException(
                        "Dash 327 formal CastAbilitySlot command was rejected: "
                        + enqueue.Error.Message);
                }

                _segment.command_evidence +=
                    BodyAngle(bodyId)
                    + "°: accepted formal PlayerInput CastAbilitySlot(slot=9, ability=910023); ";
            }

            Observe(TickLive(
                0,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput), bodyId);
            bool firstAction327 = !_sawAction327
                && _latestRow.ActiveActionId == CharacterTestSlopePlayableFixture.DashStrikeActionId;
            bool firstRequestedRootMotion = !_sawRequested
                && _latestRow.HasDiagnostic
                && _latestRow.Diagnostic.RequestedDelta.X.RawValue != 0;
            _sawAction327 |= firstAction327;
            _sawRequested |= firstRequestedRootMotion;
            if ((firstAction327 || firstRequestedRootMotion) && _phaseTick != 0)
                AddEvidence(_latestRow);
            if (++_phaseTick < DashPlayableTicks)
                return;
            ValidateTypedSourceResult(bodyId, "Dash 327", requireAction327: true);
            if (_phase == 0)
            {
                _phase = 1;
                BeginPhaseForCurrentSegment();
            }
            else
            {
                CompleteSegment("Dash 327 climbed 30° and remained blocked on 50°.");
            }
        }

        private void TickImpulse(int bodyId)
        {
            Observe(TickLive(
                0,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput), bodyId);
            if (_latestRow.HasDiagnostic
                && _latestRow.Diagnostic.RequestedDelta.X.RawValue != 0
                && _latestRow.Diagnostic.RequestedDelta.Z.RawValue != 0)
            {
                _sawRequested = true;
                _sawAppliedOrResolved |=
                    _latestRow.Diagnostic.AppliedDelta.X.RawValue != 0
                    || _latestRow.Diagnostic.AppliedDelta.Z.RawValue != 0
                    || _latestRow.Diagnostic.ResolvedVelocity.X.RawValue != 0
                    || _latestRow.Diagnostic.ResolvedVelocity.Z.RawValue != 0;
            }
            if (++_phaseTick < ImpulsePlayableTicks)
                return;
            ValidateTypedSourceResult(bodyId, "Impulse", requireAction327: false);
            if (_phase == 0)
            {
                _phase = 1;
                BeginPhaseForCurrentSegment();
            }
            else
            {
                CompleteSegment("External Impulse climbed 30° and remained blocked on 50°.");
            }
        }

        private void ValidateTypedSourceResult(int bodyId, string source, bool requireAction327)
        {
            if (!_sawRequested || _contactCount == 0)
                throw new InvalidOperationException(source + " did not expose requested delta and slope contact.");
            if (source == "Impulse" && !_sawAppliedOrResolved)
                throw new InvalidOperationException(
                    "Impulse request did not retain any allowed applied or resolved horizontal motion.");
            if (requireAction327 && !_sawAction327)
                throw new InvalidOperationException("Dash 327 never became the active typed action.");
            if (bodyId == CharacterTestSlopePlayableFixture.Slope30BodyId)
            {
                if (_maximumY - _phaseStartY <= (source == "Impulse" ? 10 : 200))
                    throw new InvalidOperationException(source + " did not ascend the 30° slope.");
            }
            else if (!_sawSteep ||
                _maximumY - _phaseStartY > CharacterTestSlopePlayableFixture.SteepMaximumRiseMillimeters)
            {
                throw new InvalidOperationException(source + " did not remain blocked by the 50° slope.");
            }
        }

        private void Observe(CharacterTestSlopeAuthorityRow row, int expectedBodyId)
        {
            _latestRow = row;
            _maximumY = Math.Max(_maximumY, row.YMillimeters);
            _minimumY = Math.Min(_minimumY, row.YMillimeters);
            _groundedCount += row.Grounded ? 1 : 0;
            if (row.IsSlopeContact(expectedBodyId))
                _contactCount++;
            if (row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId) &&
                row.Diagnostic.SurfaceClassification == CharacterTestMotionSurfaceClassification.Steep)
            {
                _sawSteep = true;
            }
            if (expectedBodyId == CharacterTestSlopePlayableFixture.Slope50BodyId)
            {
                _latestSlopeTopSeparationRaw = ComputeSlopeTopSeparationRaw(
                    CharacterTestSlopePlayableFixture.Slope50BodyId,
                    row,
                    out _latestSlopeTopFootprintOverlap);
                if (_latestSlopeTopFootprintOverlap
                    && _latestSlopeTopSeparationRaw < -_physics.SkinWidth.RawValue)
                {
                    throw new InvalidOperationException(
                        _phaseName + " penetrated beyond the formal 50-degree OBB skin. separationRaw="
                        + _latestSlopeTopSeparationRaw + ".");
                }
                if (row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId))
                {
                    if (!TryGetAppliedDotSkinEvidence(
                            row,
                            out long appliedDotNormalRaw2,
                            out long appliedDotLowerBoundRaw2)
                        || appliedDotNormalRaw2 < appliedDotLowerBoundRaw2)
                    {
                        throw new InvalidOperationException(
                            _phaseName
                            + " applied motion exceeded the formal 50-degree OBB skin. dotRaw2="
                            + appliedDotNormalRaw2
                            + ", allowedMinimumRaw2=" + appliedDotLowerBoundRaw2
                            + ", skinRaw=" + _physics.SkinWidth.RawValue
                            + ", separationRaw=" + _latestSlopeTopSeparationRaw + ".");
                    }
                }
            }
            else
            {
                _latestSlopeTopFootprintOverlap = false;
                _latestSlopeTopSeparationRaw = 0L;
            }
            if ((row.CollisionFlags & CombatMotionCollisionFlags.IterationLimit) != 0 ||
                row.HasDiagnostic &&
                (row.Diagnostic.CollisionFlags & CombatMotionCollisionFlags.IterationLimit) != 0)
            {
                throw new InvalidOperationException(_phaseName + " produced IterationLimit.");
            }
            if (row.ControlIntentSourceId != CharacterTestRuntimeCommandSources.PlayerInput)
                throw new InvalidOperationException(_phaseName + " did not use the live PlayerInput command source.");
            if (_phaseTick == 0 || _phaseTick % EvidenceStride == 0)
                AddEvidence(row);
        }

        private void AddEvidence(CharacterTestSlopeAuthorityRow row)
        {
            TryGetAppliedDotSkinEvidence(
                row,
                out long appliedDotNormalRaw2,
                out long appliedDotLowerBoundRaw2);
            var evidence = new CharacterTestSlopeGameViewAuthorityEvidence
            {
                segment = CurrentSegmentId,
                phase = _phaseName,
                tick = row.FrameValue,
                x_mm = row.XMillimeters,
                y_mm = row.YMillimeters,
                z_mm = row.ZMillimeters,
                grounded = row.Grounded,
                collision_flags = (int)row.CollisionFlags,
                collision_flags_text = row.CollisionFlags.ToString(),
                surface_stable_id = SurfaceStableId(row),
                body_id = row.HasDiagnostic ? row.Diagnostic.TargetBodyId.Value : 0,
                collider_id = row.HasDiagnostic ? row.Diagnostic.TargetColliderId.Value : 0,
                shape = row.HasDiagnostic ? row.Diagnostic.TargetShapeKind.ToString() : string.Empty,
                normal_x_raw = row.HasDiagnostic ? row.Diagnostic.Normal.X.RawValue : 0,
                normal_y_raw = row.HasDiagnostic ? row.Diagnostic.Normal.Y.RawValue : 0,
                normal_z_raw = row.HasDiagnostic ? row.Diagnostic.Normal.Z.RawValue : 0,
                ground_min_normal_y_raw = row.HasDiagnostic ? row.Diagnostic.GroundMinNormalY.RawValue : 0,
                ground_snap_raw = row.HasDiagnostic ? row.Diagnostic.GroundSnapAppliedDistance.RawValue : 0,
                classification = row.HasDiagnostic ? row.Diagnostic.SurfaceClassification.ToString() : string.Empty,
                material = row.HasDiagnostic ? row.Diagnostic.SurfaceMaterial.ToString() : row.SurfaceMaterial.ToString(),
                requested_x_raw = row.HasDiagnostic ? row.Diagnostic.RequestedDelta.X.RawValue : 0,
                requested_y_raw = row.HasDiagnostic ? row.Diagnostic.RequestedDelta.Y.RawValue : 0,
                requested_z_raw = row.HasDiagnostic ? row.Diagnostic.RequestedDelta.Z.RawValue : 0,
                applied_x_raw = row.HasDiagnostic ? row.Diagnostic.AppliedDelta.X.RawValue : 0,
                applied_y_raw = row.HasDiagnostic ? row.Diagnostic.AppliedDelta.Y.RawValue : 0,
                applied_z_raw = row.HasDiagnostic ? row.Diagnostic.AppliedDelta.Z.RawValue : 0,
                applied_dot_normal_raw2 = appliedDotNormalRaw2,
                applied_dot_lower_bound_raw2 = appliedDotLowerBoundRaw2,
                resolved_x_raw = row.HasDiagnostic ? row.Diagnostic.ResolvedVelocity.X.RawValue : 0,
                resolved_y_raw = row.HasDiagnostic ? row.Diagnostic.ResolvedVelocity.Y.RawValue : 0,
                resolved_z_raw = row.HasDiagnostic ? row.Diagnostic.ResolvedVelocity.Z.RawValue : 0,
                control_intent_source_id = row.ControlIntentSourceId,
                active_action_id = row.ActiveActionId,
                formal_view_present = true,
                view_x_mm = Mathf.RoundToInt(_latestViewPosition.x * 1000f),
                view_y_mm = Mathf.RoundToInt(_latestViewPosition.y * 1000f),
                view_z_mm = Mathf.RoundToInt(_latestViewPosition.z * 1000f),
                view_yaw_millidegrees = Mathf.RoundToInt(
                    _latestViewRotation.eulerAngles.y * 1000f),
                view_position_error_mm = _latestViewPositionErrorMillimeters,
                view_yaw_error_millidegrees = _latestViewYawErrorMilliDegrees,
                slope_top_footprint_overlap = _latestSlopeTopFootprintOverlap,
                slope_top_separation_raw = _latestSlopeTopSeparationRaw
            };
            _segment.samples.Add(evidence);
        }

        private bool TryGetAppliedDotSkinEvidence(
            CharacterTestSlopeAuthorityRow row,
            out long appliedDotNormalRaw2,
            out long appliedDotLowerBoundRaw2)
        {
            appliedDotNormalRaw2 = 0L;
            appliedDotLowerBoundRaw2 = 0L;
            if (!row.HasDiagnostic
                || !CombatKinematicMotor.TryRawDotNumerator(
                    row.Diagnostic.AppliedDelta,
                    row.Diagnostic.Normal,
                    out appliedDotNormalRaw2))
            {
                return false;
            }

            long normalLengthRaw = row.Diagnostic.Normal.LengthSquared().Sqrt().RawValue;
            if (normalLengthRaw <= 0L)
                return false;
            try
            {
                appliedDotLowerBoundRaw2 = checked(
                    -_physics.SkinWidth.RawValue * normalLengthRaw);
            }
            catch (OverflowException)
            {
                return false;
            }
            return true;
        }

        private void CompleteSegment(string message)
        {
            AddEvidence(_latestRow);
            _segment.passed = true;
            _segment.message = message;
            _segment.end_tick = _latestRow.FrameValue;
            _report.message = "Screenshot ready: " + _segment.display_name + " · " + message;
            _screenshotReady = true;
            SettleProductionCameraForScreenshot();
            UpdateOverlay();
        }

        private void PrepareSteepBlockedScreenshotRow()
        {
            _phaseName = "50.blocked.screenshot";
            PlaceOnSlope(
                CharacterTestSlopePlayableFixture.Slope50BodyId,
                CharacterTestSlopePlayableFixture.SteepStartLongitudinalRaw,
                0,
                false,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            CharacterTestSlopeAuthorityRow row = TickLive(
                CharacterTestSlopePlayableFixture.SteepUphillInputRaw,
                0,
                CharacterTestRuntimeCommandSources.PlayerInput);
            Observe(row, CharacterTestSlopePlayableFixture.Slope50BodyId);
            if (!row.IsSlopeContact(CharacterTestSlopePlayableFixture.Slope50BodyId)
                || row.Diagnostic.SurfaceClassification
                    != CharacterTestMotionSurfaceClassification.Steep)
            {
                throw new InvalidOperationException(
                    "The representative 50-degree screenshot row did not retain typed OBB/Steep contact.");
            }
        }

        private void SettleProductionCameraForScreenshot()
        {
            CharacterTestThirdPersonCameraRigBridge bridge =
                _manager?.RuntimeContext?.ThirdPersonCameraBridge;
            if (bridge == null || _slice == null)
                return;
            for (int i = 0; i < ScreenshotCameraSettleTicks; i++)
                bridge.TickPresentation(1f / 60f, _slice.CurrentFrame);
        }

        private void Finish(CharacterTestSlopeGameViewRunStatus status, string message)
        {
            if (_report == null)
                return;
            _report.status = status;
            _report.message = message ?? string.Empty;
            _report.completed_utc = DateTime.UtcNow.ToString("O");
            _screenshotReady = false;
            _reportWritten = TryWriteReport(out string reportError);
            if (!_reportWritten && status == CharacterTestSlopeGameViewRunStatus.Passed)
            {
                _report.status = CharacterTestSlopeGameViewRunStatus.Failed;
                _report.message = "Slope behavior passed, but the required evidence report could not be written: "
                    + reportError;
            }
            UpdateOverlay();
        }

        private void Fail(Exception exception)
        {
            if (_segment != null && !_segment.passed)
            {
                _segment.message = exception.GetType().Name + ": " + exception.Message;
                _segment.end_tick = _slice?.CurrentFrame.Value ?? 0;
            }
            Debug.LogException(exception);
            Finish(CharacterTestSlopeGameViewRunStatus.Failed,
                "Slope Game View acceptance failed: " + exception.Message);
        }

        private bool TryWriteReport(out string error)
        {
            error = string.Empty;
            try
            {
                string directory = Path.GetDirectoryName(_reportPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(
                    _reportPath,
                    JsonConvert.SerializeObject(_report, Formatting.Indented));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                Debug.LogError("Could not write slope Game View report: " + exception);
                return false;
            }
        }

        private void CreateValidationOverlay(PanelSettings panelSettings)
        {
            GameObject overlay = new GameObject("[TEST] Slope Acceptance UI Toolkit Overlay");
            _overlayObject = overlay;
            overlay.SetActive(false);
            _panelSettings = panelSettings;
            _uiDocument = overlay.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            _uiDocument.sortingOrder = 10000;
            overlay.SetActive(true);

            VisualElement root = _uiDocument.rootVisualElement;
            if (root == null)
                throw new InvalidOperationException("Slope acceptance UI Toolkit root was not created.");
            root.style.position = Position.Absolute;
            root.style.left = 18;
            root.style.top = 18;
            root.style.width = 760;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 0.9f);
            root.style.borderTopLeftRadius = 6;
            root.style.borderTopRightRadius = 6;
            root.style.borderBottomLeftRadius = 6;
            root.style.borderBottomRightRadius = 6;

            AddOverlayLabel(root, "Static OBB slopes · slope limit 45°", 22, Color.white, true);
            AddOverlayLabel(root, "15° / 30° walkable", 18, new Color(0.35f, 1f, 0.55f), true);
            AddOverlayLabel(root, "50° blocked", 18, new Color(1f, 0.55f, 0.35f), true);
            AddOverlayLabel(root,
                "TEST-ONLY SETUP positions independent segment/subphase starts; 15°/30° up→stop→down stays continuous · phase motion uses formal live LocalPlayer, commands, fixed ticks and production view · no Unity Collider authority",
                13,
                new Color(1f, 0.85f, 0.25f),
                false);
            _statusLabel = AddOverlayLabel(root, string.Empty, 16, Color.white, true);
            _authorityLabel = AddOverlayLabel(root, string.Empty, 13, new Color(0.75f, 0.9f, 1f), false);
        }

        private static Label AddOverlayLabel(
            VisualElement root,
            string text,
            int fontSize,
            Color color,
            bool bold)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 3;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            root.Add(label);
            return label;
        }

        private void UpdateOverlay()
        {
            if (_statusLabel == null || _authorityLabel == null)
                return;
            _statusLabel.text = Status + " · " + CurrentSegmentId + " · " + _phaseName
                + (_screenshotReady ? " · SCREENSHOT READY" : string.Empty)
                + "\n" + Message;
            if (_segment == null || _segment.samples.Count == 0)
            {
                _authorityLabel.text = "Waiting for typed authority row…";
                return;
            }
            CharacterTestSlopeGameViewAuthorityEvidence row =
                _segment.samples[_segment.samples.Count - 1];
            _authorityLabel.text =
                "tick=" + row.tick
                + "  pos(mm)=" + row.x_mm + "," + row.y_mm + "," + row.z_mm
                + "  grounded=" + row.grounded
                + "  flags=" + row.collision_flags_text
                + "\nsurface=" + row.surface_stable_id
                + "  body.collider=" + row.body_id + "." + row.collider_id
                + "  shape=" + row.shape
                + "  class=" + row.classification
                + "  material=" + row.material
                + "\nnormalRaw=" + row.normal_x_raw + "," + row.normal_y_raw + "," + row.normal_z_raw
                + "  thresholdRaw=" + row.ground_min_normal_y_raw
                + "  snapRaw=" + row.ground_snap_raw
                + "\nrequestedRaw=" + row.requested_x_raw + "," + row.requested_y_raw + "," + row.requested_z_raw
                + "  appliedRaw=" + row.applied_x_raw + "," + row.applied_y_raw + "," + row.applied_z_raw
                + "  resolvedRaw=" + row.resolved_x_raw + "," + row.resolved_y_raw + "," + row.resolved_z_raw
                + "  source=" + row.control_intent_source_id
                + "  action=" + row.active_action_id
                + "\nappliedDotRaw2=" + row.applied_dot_normal_raw2
                + "  allowedMinRaw2=" + row.applied_dot_lower_bound_raw2
                + "\nformalView(mm)=" + row.view_x_mm + "," + row.view_y_mm + "," + row.view_z_mm
                + "  viewError(mm/yawMilli)=" + row.view_position_error_mm
                + "/" + row.view_yaw_error_millidegrees
                + "  slopeTopFootprint=" + row.slope_top_footprint_overlap
                + "  slopeTopSeparationRaw=" + row.slope_top_separation_raw;
        }

        private void CapturePhaseStart()
        {
            _phaseStartX = _player.Transform.XMillimeters;
            _phaseStartY = _player.Transform.YMillimeters;
            _phaseStartZ = _player.Transform.ZMillimeters;
            _maximumY = _phaseStartY;
            _minimumY = _phaseStartY;
        }

        private void PlaceAndSettle(int bodyId, long longitudinalRaw, long lateralRaw)
        {
            PlaceOnSlope(
                bodyId,
                longitudinalRaw,
                lateralRaw,
                true,
                CharacterTestSlopePlayableFixture.UphillYawMilliDegrees);
            SettleWalkable();
        }

        private void PlaceOnSlope(
            int bodyId,
            long longitudinalRaw,
            long lateralRaw,
            bool grounded,
            int yawMilliDegrees)
        {
            SlopeSpec spec = GetSlope(bodyId);
            FixVector3 halfExtents = _physics.GetCharacterHalfExtents(_player);
            Fix64 radius = Fix64.Min(halfExtents.X.Abs(), halfExtents.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(halfExtents.Y.Abs() - radius, Fix64.Zero);
            Fix64 support = radius + segmentHalf * spec.Collider.AxisY.Y.Abs();
            FixVector3 topPoint = spec.WorldCenter
                + spec.Collider.AxisY * spec.Collider.HalfExtents.Y
                + spec.Collider.AxisZ * Fix64.FromRaw(longitudinalRaw)
                + spec.Collider.AxisX * Fix64.FromRaw(lateralRaw);
            FixVector3 characterCenter = topPoint + spec.Collider.AxisY * support;
            Fix64 feetY = characterCenter.Y - _physics.GetCharacterCenterOffsetY(_player);

            _player.ClearMotionRequests();
            _player.SetInput(CharacterTestCharacterInputComponent.None);
            _player.SetAction(CharacterTestCharacterActionComponent.None);
            _player.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.X),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(feetY),
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(characterCenter.Z),
                yawMilliDegrees));
            _player.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: grounded,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: grounded ? CharacterMovementMode.Idle : CharacterMovementMode.Airborne,
                physicsEnabled: true,
                collisionFlags: grounded
                    ? CombatMotionCollisionFlags.Grounded
                    : CombatMotionCollisionFlags.None,
                surfaceMaterial: grounded
                    ? CombatSurfaceMaterial.Rough
                    : CombatSurfaceMaterial.Default);
        }

        private void SettleWalkable()
        {
            for (int i = 0; i < 4; i++)
                TickLive(0, 0, CharacterTestRuntimeCommandSources.PlayerInput);
            if (!_player.IsGrounded)
                throw new InvalidOperationException("Formal live LocalPlayer did not settle on a walkable slope.");
        }

        private CharacterTestSlopeAuthorityRow TickLive(
            short worldMoveXRaw,
            short worldMoveZRaw,
            int sourceId,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool jumpReleased = false,
            int skillSlotPressedMask = 0)
        {
            if (sourceId != CharacterTestRuntimeCommandSources.PlayerInput)
                throw new InvalidOperationException("T7 live player driver only accepts PlayerInput source.");
            EnsureFormalRuntimeIdentity();
            Vector2 move = ResolveInputForWorldMove(worldMoveXRaw, worldMoveZRaw);
            _inputProvider.SetSnapshot(new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false,
                skillSlotPressedMask: skillSlotPressedMask));
            RuntimeFrame frame = _slice.CurrentFrame;
            if (!_manager.StepSimulationOnceForRuntimeAiPlannerDev())
                throw new InvalidOperationException("Formal GameManager rejected a fixed simulation/presentation step.");
            if (_slice.CurrentFrame.Value != frame.Value + 1L)
            {
                throw new InvalidOperationException(
                    "Each T7 driver step must advance exactly one runtime frame; before="
                    + frame.Value + ", after=" + _slice.CurrentFrame.Value + ".");
            }
            EnsureFormalRuntimeIdentity();
            bool hasDiagnostic = _slice.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                _player.Id.Value,
                out CharacterTestCharacterMotionDiagnostic diagnostic);
            var row = new CharacterTestSlopeAuthorityRow(frame, _player, hasDiagnostic, diagnostic);
            CharacterTestCharacterEntity uniquePlayer = FindUniqueLocalPlayer(
                _slice,
                out int localPlayerCount);
            if (localPlayerCount != 1 || !ReferenceEquals(uniquePlayer, _player))
            {
                throw new InvalidOperationException(
                    "Formal live LocalPlayer identity changed during acceptance; count="
                    + localPlayerCount + ".");
            }
            if (!_slice.CharacterRuntime.ViewBridge.TryGetViewPose(
                    _player.Id,
                    out _latestViewPosition,
                    out _latestViewRotation))
            {
                throw new InvalidOperationException(
                    "Formal production character view disappeared during " + _phaseName + ".");
            }

            int viewXMillimeters = Mathf.RoundToInt(_latestViewPosition.x * 1000f);
            int viewYMillimeters = Mathf.RoundToInt(_latestViewPosition.y * 1000f);
            int viewZMillimeters = Mathf.RoundToInt(_latestViewPosition.z * 1000f);
            _latestViewPositionErrorMillimeters = Math.Max(
                Math.Abs(viewXMillimeters - row.XMillimeters),
                Math.Max(
                    Math.Abs(viewYMillimeters - row.YMillimeters),
                    Math.Abs(viewZMillimeters - row.ZMillimeters)));
            _latestViewYawErrorMilliDegrees = Mathf.RoundToInt(
                Mathf.Abs(Mathf.DeltaAngle(
                    row.YawMilliDegrees / 1000f,
                    _latestViewRotation.eulerAngles.y)) * 1000f);
            if (_latestViewPositionErrorMillimeters > 2
                || _latestViewYawErrorMilliDegrees > 5)
            {
                throw new InvalidOperationException(
                    "Formal production view diverged from authority in " + _phaseName
                    + ": positionErrorMm=" + _latestViewPositionErrorMillimeters
                    + ", yawErrorMilliDegrees=" + _latestViewYawErrorMilliDegrees + ".");
            }
            return row;
        }

        private void EnsureFormalRuntimeIdentity()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid()
                || !string.Equals(
                    activeScene.path,
                    CharacterTestSlopePlayableFixture.ScenePath,
                    StringComparison.Ordinal)
                || _manager == null
                || _manager.gameObject.scene.handle != activeScene.handle
                || !ReferenceEquals(_manager.RuntimeContext?.Slice, _slice))
            {
                throw new InvalidOperationException(
                    "The active formal CharacterTestScene/GameManager/Slice identity changed during T7.");
            }
        }

        private Vector2 ResolveInputForWorldMove(short worldXRaw, short worldZRaw)
        {
            Vector2 desired = new Vector2(worldXRaw / 1000f, worldZRaw / 1000f);
            Vector2 basisX = _slice.CharacterRuntime.ResolveControlWorldMove(_player, Vector2.right);
            Vector2 basisY = _slice.CharacterRuntime.ResolveControlWorldMove(_player, Vector2.up);
            float determinant = basisX.x * basisY.y - basisY.x * basisX.y;
            if (Mathf.Abs(determinant) < 0.0001f)
                throw new InvalidOperationException("Formal player movement basis is singular.");
            return Vector2.ClampMagnitude(new Vector2(
                (desired.x * basisY.y - basisY.x * desired.y) / determinant,
                (basisX.x * desired.y - desired.x * basisX.y) / determinant), 1f);
        }

        private int ComputeHorizontalEdgeGapMillimeters(int bodyId, CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(bodyId);
            FixVector3 point = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                Fix64.Zero,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            FixVector3 center = new FixVector3(slope.WorldCenter.X, Fix64.Zero, slope.WorldCenter.Z);
            FixVector3 axis = new FixVector3(slope.Collider.AxisX.X, Fix64.Zero, slope.Collider.AxisX.Z);
            Fix64 lateral = (point - center).Dot(axis).Abs();
            FixVector3 characterHalf = _physics.GetCharacterHalfExtents(_player);
            Fix64 radius = Fix64.Min(characterHalf.X.Abs(), characterHalf.Z.Abs());
            Fix64 gap = lateral - slope.Collider.HalfExtents.X - radius;
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                gap > Fix64.Zero ? gap : Fix64.Zero);
        }

        private int ComputeLongitudinalEdgeGapMillimeters(
            int bodyId,
            CharacterTestSlopeAuthorityRow row)
        {
            SlopeSpec slope = GetSlope(bodyId);
            FixVector3 center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.YMillimeters)
                    + _physics.GetCharacterCenterOffsetY(_player),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            FixVector3 characterHalf = _physics.GetCharacterHalfExtents(_player);
            Fix64 radius = Fix64.Min(characterHalf.X.Abs(), characterHalf.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(characterHalf.Y.Abs() - radius, Fix64.Zero);
            Fix64 support = radius + segmentHalf * slope.Collider.AxisZ.Y.Abs();
            Fix64 longitudinal = (center - slope.WorldCenter).Dot(slope.Collider.AxisZ).Abs();
            Fix64 gap = longitudinal - slope.Collider.HalfExtents.Z - support;
            return CharacterTestDeterministicUnits.Fix64MetersToMillimeters(
                gap > Fix64.Zero ? gap : Fix64.Zero);
        }

        private long ComputeSlopeTopSeparationRaw(
            int bodyId,
            CharacterTestSlopeAuthorityRow row,
            out bool footprintOverlap)
        {
            SlopeSpec slope = GetSlope(bodyId);
            FixVector3 center = new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.YMillimeters)
                    + _physics.GetCharacterCenterOffsetY(_player),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(row.ZMillimeters));
            FixVector3 halfExtents = _physics.GetCharacterHalfExtents(_player);
            Fix64 radius = Fix64.Min(halfExtents.X.Abs(), halfExtents.Z.Abs());
            Fix64 segmentHalf = Fix64.Max(halfExtents.Y.Abs() - radius, Fix64.Zero);
            Fix64 capsuleSupport = radius
                + segmentHalf * slope.Collider.AxisY.Y.Abs();
            FixVector3 local = center - slope.WorldCenter;
            Fix64 supportX = radius
                + segmentHalf * slope.Collider.AxisX.Y.Abs();
            Fix64 supportZ = radius
                + segmentHalf * slope.Collider.AxisZ.Y.Abs();
            footprintOverlap = local.Dot(slope.Collider.AxisX).Abs()
                    <= slope.Collider.HalfExtents.X + supportX + _physics.SkinWidth
                && local.Dot(slope.Collider.AxisZ).Abs()
                    <= slope.Collider.HalfExtents.Z + supportZ + _physics.SkinWidth;
            Fix64 separation = local.Dot(slope.Collider.AxisY)
                - slope.Collider.HalfExtents.Y
                - capsuleSupport;
            return separation.RawValue;
        }

        private void AddSlopeSpec(int bodyIdValue)
        {
            var bodyId = new CombatBodyId(bodyIdValue);
            if (!_physics.World.TryGetBody(bodyId, out CombatPhysicsBody body) ||
                !_physics.World.TryGetObbCollider(
                    bodyId,
                    new CombatColliderId(CharacterTestSlopePlayableFixture.SlopeColliderId),
                    out CombatPhysicsObbCollider collider))
            {
                throw new InvalidOperationException("Formal live physics is missing slope OBB " + bodyIdValue + ".1.");
            }
            _slopes.Add(bodyIdValue, new SlopeSpec(body, collider));
        }

        private SlopeSpec GetSlope(int bodyId)
        {
            if (!_slopes.TryGetValue(bodyId, out SlopeSpec slope))
                throw new InvalidOperationException("Slope geometry was not registered: " + bodyId + ".");
            return slope;
        }

        private static CharacterTestCharacterEntity FindUniqueLocalPlayer(
            GameSlice slice,
            out int count)
        {
            count = 0;
            CharacterTestCharacterEntity result = null;
            for (int i = 0; i < slice.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = slice.CharacterEntities[i];
                if (candidate.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                {
                    count++;
                    result = candidate;
                }
            }
            return count == 1 ? result : null;
        }

        private readonly struct SlopeSpec
        {
            internal SlopeSpec(CombatPhysicsBody body, CombatPhysicsObbCollider collider)
            {
                Collider = collider;
                WorldCenter = collider.GetWorldCenter(body.Position);
            }

            internal CombatPhysicsObbCollider Collider { get; }
            internal FixVector3 WorldCenter { get; }
        }

        private void ResetPhaseCounters()
        {
            _phaseTick = 0;
            _contactCount = 0;
            _groundedCount = 0;
            _stopStableX = 0;
            _stopStableZ = 0;
            _stopStableReferenceCaptured = false;
            _sawAirborne = false;
            _sawLanding = false;
            _sawSteep = false;
            _sawRequested = false;
            _sawAppliedOrResolved = false;
            _sawAction327 = false;
            _sawEdgeBeyondGuard = false;
            _sawAirborneBeyondGuard = false;
        }

        private static string SurfaceStableId(CharacterTestSlopeAuthorityRow row)
        {
            if (!row.HasDiagnostic || !row.Diagnostic.HasCollision)
                return string.Empty;
            switch (row.Diagnostic.TargetBodyId.Value)
            {
                case CharacterTestSlopePlayableFixture.Slope15BodyId:
                    return "character-test.slope.15deg";
                case CharacterTestSlopePlayableFixture.Slope30BodyId:
                    return "character-test.slope.30deg";
                case CharacterTestSlopePlayableFixture.Slope50BodyId:
                    return "character-test.slope.50deg";
                default:
                    return "body." + row.Diagnostic.TargetBodyId.Value;
            }
        }

        private static string BodyAngle(int bodyId)
        {
            return bodyId == CharacterTestSlopePlayableFixture.Slope15BodyId ? "15"
                : bodyId == CharacterTestSlopePlayableFixture.Slope30BodyId ? "30"
                : "50";
        }

        private static bool IsTerminal(CharacterTestSlopeGameViewRunStatus status)
        {
            return status == CharacterTestSlopeGameViewRunStatus.Passed
                || status == CharacterTestSlopeGameViewRunStatus.Failed
                || status == CharacterTestSlopeGameViewRunStatus.Canceled;
        }

        internal void CleanupAfterTerminal()
        {
            RestoreLiveState();
        }

        private void RestoreLiveState()
        {
            if (_cleanupComplete)
                return;

            _cleanupComplete = true;
            if (!_liveStateCaptured)
                return;
            if (_inputProvider != null)
                _inputProvider.SetSnapshot(InputSnapshot.Empty);
            if (_slice != null)
                _slice.SetLocalInputProvider(_previousInputProvider);
            if (_manager?.RuntimeContext?.MainMenuCoordinator != null)
                _manager.RuntimeContext.MainMenuCoordinator.SetPaused(_previousPaused);
        }

        private void CleanupInitializationFailure()
        {
            RestoreLiveState();
            DestroyOverlay();
        }

        private void DestroyOverlay()
        {
            if (_overlayObject == null)
                return;
            UnityEngine.Object.Destroy(_overlayObject);
            _overlayObject = null;
            _uiDocument = null;
        }

        public void Dispose()
        {
            if (_report != null && !IsTerminal(Status))
                Finish(CharacterTestSlopeGameViewRunStatus.Canceled, "Driver was disposed before completion.");
            RestoreLiveState();
            DestroyOverlay();
            if (ReferenceEquals(Active, this))
                Active = null;
        }
    }
}
