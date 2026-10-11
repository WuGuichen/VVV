using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Opt-in transient acceptance over the production HUD, command pump, original owners, adapter and real character world.</summary>
    public static partial class CharacterTestSelectedPointValidationFixture
    {
        private static TacticalStageFlowCoordinator _formalStage;
        private static TacticalCommandPump _formalPump;
        private static CharacterTestTacticalExecutionAdapter _formalAdapter;
        private static TacticalCardHud _formalHud;
        private static UnityTacticalTimeAdapter _formalTime;
        private static TacticalActionCardDefinition _formalCard;
        private static FakeInputProvider _formalInput;
        private static long _formalStartedTick;
        private static int _formalIndex;
        private static InputService _formalNativeInput;
        private static bool _manualActive;
        private static double _manualNextTick;
        private static string _formalOwnedPackage;
        private static TacticalWorldPoint _formalSelectedPoint;

        [UnityEditor.MenuItem("MxFramework/Character Test/Selected Point/Start Temporary Manual Validation")]
        public static void BeginManual()
        {
            try
            {
                RequireSafeContext(); BeginFormal(); FormalNativeInput(); FormalFairyView();
                _manualActive = true; _manualNextTick = UnityEditor.EditorApplication.timeSinceStartup;
                UnityEditor.EditorApplication.update += ManualUpdate;
                UnityEditor.EditorApplication.playModeStateChanged += ManualPlayStateChanged;
                UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            }
            catch (Exception error) { EndManual(); Debug.LogException(error); }
        }

        [UnityEditor.MenuItem("MxFramework/Character Test/Selected Point/Stop Temporary Manual Validation")]
        public static void EndManual() { CleanupFormal(); Cleanup(); }

        private static void ManualPlayStateChanged(UnityEditor.PlayModeStateChange state)
        { if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) EndManual(); }

        private static void ManualUpdate()
        {
            if (!_manualActive || !Application.isPlaying || UnityEditor.EditorApplication.isPaused) return;
            try
            {
                RequireSafeContext();
                FormalDrain();
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (_formalStage.Time.IsWorldPaused) { _manualNextTick = now; return; }
                for (int n = 0; n < 4 && now >= _manualNextTick && !_formalStage.Time.IsWorldPaused; n++)
                { StepFormal(); _manualNextTick += 1d / 60d; }
                if (now - _manualNextTick > 0.1d) _manualNextTick = now;
            }
            catch (Exception error) { EndManual(); Debug.LogException(error); }
        }

        public static string FormalFairyView()
        {
            RequireSafeContext();
            if (FairyGUI.StageCamera.main != null && FairyGUI.StageCamera.main.transform.root != _root.transform)
                throw new InvalidOperationException("Temporary fixture cannot take over an existing FairyGUI camera.");
            const string name = "MxCharacterTestTacticalDeckUi";
            var package = FairyGUI.UIPackage.GetByName(name);
            if (package == null)
            {
                package = FairyGUI.UIPackage.AddPackage("Assets/Bundles/FGUI/" + name + "/" + name,
                    (string asset, string extension, Type type, out FairyGUI.DestroyMethod destroy) =>
                    { destroy = FairyGUI.DestroyMethod.None; return UnityEditor.AssetDatabase.LoadAssetAtPath(asset + extension, type); });
                _formalOwnedPackage = package.id;
            }
            var root = package.CreateObject("TacticalBattleHud").asCom;
            FairyGUI.GRoot.inst.AddChild(root);
            FairyGUI.StageCamera.CheckMainCamera();
            var baseCamera = _root.GetComponentInChildren<Camera>();
            var overlay = FairyGUI.StageCamera.main;
            overlay.gameObject.hideFlags = HideFlags.DontSave;
            overlay.transform.SetParent(_root.transform, true);
            if (baseCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                baseCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (overlay.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                overlay.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Keep the optional camera assembly out of this Editor assembly's namespace surface.
            var binder = Type.GetType("MxFramework.Camera.URP.MxCameraUrpOverlayStackBinder, MxFramework.Camera.URP");
            var stack = binder?.GetMethod("Bind").Invoke(null, new object[] { baseCamera, overlay, true, true });
            if (stack == null || !(bool)stack.GetType().GetProperty("Success").GetValue(stack))
                throw new InvalidOperationException("Temporary FairyGUI camera stack could not bind.");
            var view = new CharacterTestFairyGuiTacticalBattleView(_formalHud, root);
            typeof(TacticalCardHud).GetField("_fairyBattleView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_formalHud, view);
            view.Sync(true); _formalHud.RefreshIfChanged();
            Check("formal.published-fairy-battle-view-bound", root.GetChild("hand0").visible && root.GetChild("confirm") != null, root.name);
            return FormalReport();
        }

        public static string FormalNativeInput()
        {
            RequireSafeContext();
            if (_formalNativeInput != null) UnityEngine.Object.DestroyImmediate(_formalNativeInput.gameObject);
            var go = new GameObject("SelectedPointFormalInput") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform); go.SetActive(false);
            _formalNativeInput = go.AddComponent<InputService>();
            var serialized = new UnityEditor.SerializedObject(_formalNativeInput);
            serialized.FindProperty("_inputActions").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions");
            serialized.FindProperty("_bindingOverridesKey").stringValue = "mxframework.task05.transient.validation";
            serialized.ApplyModifiedPropertiesWithoutUndo(); go.SetActive(true); _formalNativeInput.SetContext(InputContext.UI);
            _formalHud.Bind(_formalStage, _formalPump, inputProvider: _formalNativeInput);
            var point = _root.GetComponentInChildren<Camera>().WorldToScreenPoint(new Vector3(0, 0, 2.5f));
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { point.x, point.y, width = Screen.width, height = Screen.height,
                mouse = Mouse.current == null ? -1 : Mouse.current.deviceId, actions = _formalNativeInput.Actions != null });
        }

        public static string FormalDeviceClick()
        {
            RequireSafeContext();
            if (!Application.isFocused) throw new InvalidOperationException("Game View must own input focus for a native device event.");
            var camera = _root.GetComponentInChildren<Camera>();
            var screen = camera.WorldToScreenPoint(new Vector3(0, 0, 2.5f));
            var mouse = Mouse.current;
            if (mouse == null) throw new InvalidOperationException("Native mouse unavailable.");
            var previous = new MouseState { position = mouse.position.ReadValue(), delta = mouse.delta.ReadValue(), scroll = mouse.scroll.ReadValue() };
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(screen.x, screen.y) }.WithButton(MouseButton.Left));
                InputSystem.Update();
                typeof(InputService).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_formalNativeInput, null);
                var picked = _formalNativeInput.Snapshot.Point;
                Check("formal.native-input-service-receives-device-click", _formalNativeInput.Snapshot.ClickPressed, new { picked.x, picked.y });
                PollFormalHud(); FormalDrain();
                Check("formal.device-click-real-provider-ray-selects", _formalStage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.WorldPoint
                    && _formalHud.CanConfirmExecution, FormalState());
            }
            finally { InputSystem.QueueStateEvent(mouse, previous); InputSystem.Update(); }
            return FormalReport();
        }

        public static string FormalReadNativeClick()
        {
            RequireSafeContext();
            FormalDrain();
            Check("formal.os-click-selected-real-input-provider-point", _formalStage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.WorldPoint
                && _formalHud.CanConfirmExecution, FormalState());
            return FormalReport();
        }

        public static string BeginFormal(int maximumFrames = 180) => BeginFormalCore(maximumFrames, false, "");

        // All balancing numbers below are transient acceptance values, never written to project catalogs.
        public static string BeginPublishedFormal(int maximumFrames = 180, string catalogMutation = "") =>
            BeginFormalCore(maximumFrames, true, catalogMutation);

        private static string BeginFormalCore(int maximumFrames, bool published, string catalogMutation)
        {
            RequireSafeContext();
            CleanupFormal(); Build(); _formalStartedTick = -1; _formalSelectedPoint = default; _formalIndex++;
            _preview.Hide();
            _formalTime = new UnityTacticalTimeAdapter(); _formalTime.SetSimulationPaused(false);
            var deck = new TacticalDeckManager(); var ledger = new MomentumLedger();
            var cooldown = new TacticalCooldownTracker();
            deck.BindCooldownTracker(cooldown, _probe.Source.Identity.CharacterId, () => _frame);
            _formalCard = TacticalActionCardDefinition.CreatePointMove("fixture.formal.point", "前往选定点 · 验收", "本次地点输入", 10,
                new TacticalPointMoveRules(5000, 200, maximumFrames, 1000, true, TacticalPointMoveStartPolicy.OwnedMoveAccepted), 2, CardDestination.Discard);
            TacticalDeployment deployment = null;
            Func<CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog, CharacterTestRuntimeAiPlannerProjectCatalogState> loader = null;
            if (published)
            {
                var contract = new TacticalPointMoveAuthoring { RangeMillimeters = 5000, ArrivalMillimeters = 200,
                    MaximumFrames = maximumFrames, SpeedMilli = 1000, RequireReachable = true };
                var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("point-transient", "bc_qkjc3ka7za", "Point transient", "Tactical", null);
                draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreatePointMove("Point transient", contract));
                draft.Cost = 10; draft.CooldownSeconds = 2;
                var inputs = CharacterTestTacticalCardInputs.Unbound(_probe.Source.Identity.CharacterId);
                if (!CharacterTestTacticalAuthoringCompiler.TryCompile(draft.CloneTacticalSource(), inputs, out var compiled, out var diagnostics))
                    throw new InvalidOperationException(diagnostics[0].Message);
                var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled);
                var logic = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen), null);
                var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("Point transient", "fixture.icon", 10,
                    "", "", "", cooldownSeconds: 2);
                int version = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(draft.PublishedStableId, 1,
                    version, version, metadata, CharacterTestAuthoredContentIds.D417_Id3);
                var publish = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft, request, null, logic);
                if (!publish.Success) throw new InvalidOperationException("Transient point publishing rejected.");
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, _probe.Runtime.ActionRuntime.Registry);
                var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("point-transient", 1, new[] { publish.Card }, references);
                if (!catalog.Success) throw new InvalidOperationException("Transient catalog validation rejected.");
                string json = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog.Catalog);
                var roundTrip = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(json, references);
                if (!roundTrip.Success) throw new InvalidOperationException("Transient catalog round trip rejected.");
                var projection = new CharacterTacticalContentCatalog(); projection.SetPublishedCards(roundTrip.Catalog.Cards);
                var projected = projection.Cards.Single();
                var reference = new CharacterTacticalCardReference { CardId = projected.CardId, Revision = projected.Revision,
                    ContentHash = projected.ContentHash, InstanceId = 41 };
                _formalCard = TacticalDeployment.CreateCard(reference, projected);
                var bindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>> {
                    { 41, new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()) } };
                var participant = new TacticalDeploymentParticipant("point-transient", _probe.Source.Identity.CharacterId, "fixture", "fixture", 1,
                    true, Vector3.zero, 0, new TacticalCardDefinition[] { _formalCard }, bindings);
                deployment = (TacticalDeployment)Activator.CreateInstance(typeof(TacticalDeployment), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { new[] { participant }, 50 }, CultureInfo.InvariantCulture);
                var main = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
                if (!main.Success) throw new InvalidOperationException(main.Diagnostic);
                if (catalogMutation.Length > 0)
                {
                    if (catalogMutation != "id" && catalogMutation != "revision" && catalogMutation != "hash")
                        throw new ArgumentException("Unknown transient catalog mutation.");
                    var original = publish.Card;
                    var changedMetadata = original.Metadata.Clone();
                    if (catalogMutation == "hash") changedMetadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("changed transient", "fixture.icon", 10,
                        original.Metadata.ShortDescription, original.Metadata.UseSummary, original.Metadata.RiskSummary,
                        original.Metadata.Tags, original.Metadata.RequiredCapabilities, original.Metadata.Responsibilities, 2);
                    string changedId = catalogMutation == "id" ? "transient.missing" : original.StableCardId;
                    int changedRevision = catalogMutation == "revision" ? 2 : original.Revision;
                    long changedHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                        changedId, changedRevision, version, version, changedMetadata, original.SemanticHash);
                    var changedCard = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(changedId, changedRevision, version, version,
                        changedMetadata, null, original.SemanticHash, changedHash, tacticalSource: original.TacticalSource, numericId: original.NumericId);
                    var changedCatalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("point-transient", 1,
                        new[] { changedCard }, references);
                    if (!changedCatalog.Success) throw new InvalidOperationException("Changed transient catalog is invalid.");
                    json = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(changedCatalog.Catalog);
                }
                loader = refs => {
                    var loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(json, refs);
                    return new CharacterTestRuntimeAiPlannerProjectCatalogState(loaded.Catalog, main.GroupCatalog, "transient-memory", "read-only-main",
                        "", "", loaded.Success ? "" : "Transient authentication failed.");
                };
                Check("published.author-freeze-json-catalog-deployment", _formalCard.PointMove != null && _formalCard.HasPublishedBehavior
                    && _formalCard.PublishedContentHash == publish.Card.ContentHash.ToString(CultureInfo.InvariantCulture)
                    && _formalCard.BaseMomentumCost == 10 && _formalCard.CooldownSeconds == 2,
                    new { _formalCard.PublishedCardId, _formalCard.PublishedRevision, _formalCard.PublishedContentHash, catalogMutation });
            }
            var time = new TacticalTimeCoordinator(_formalTime, new SettleDetector(new SettleDetectorConfig(3)));
            var binding = new CharacterTestTacticalCardBinding(new FormalInputs());
            var session = new CharacterTestTacticalCardSession(_probe.Runtime, _probe.Source.Id.Value, binding, _commands.Enqueue, _navigation);
            _formalAdapter = new CharacterTestTacticalExecutionAdapter(_probe.Runtime, _commands, session, null, _probe.Source,
                _formalTime, cooldownTracker: cooldown, deployment: deployment, publishedCatalogLoader: loader);
            var driver = new TacticalSingleCardDriver(_formalAdapter, time, deck, ledger);
            _formalStage = new TacticalStageFlowCoordinator((ulong)_formalIndex, time, deck, ledger, driver,
                new[] { _formalCard }, 42, id => id == _probe.Source.GameplayEntityId || id == _probe.Target.GameplayEntityId,
                new TacticalStoryStepBinding(1, 1, 1));
            _formalPump = new TacticalCommandPump(_formalStage);
            _formalAdapter.ConfigurePointInput(_navigation, card => new TacticalExecutionInputBinding(_formalStage.StageRunId,
                _formalStage.TurnIndex, card.InstanceId, card.Version, _probe.Source.GameplayEntityId));
            _formalAdapter.SetContext(null, new RuntimeFrame(_frame));
            _formalStage.OnStoryStepStarted(new TacticalStoryStepToken(_formalStage.StageRunId, 1, 1, 1, 1));
            _formalStage.OnSpawnBound(_formalStage.StageRunId, 0, _probe.Source.GameplayEntityId, _frame);
            _formalStage.OnSpawnBound(_formalStage.StageRunId, 1, _probe.Target.GameplayEntityId, _frame);
            for (int n = 1; n <= 3; n++) { var sample = FormalStable(_frame + n); _formalStage.OnCompletedSimulationTick(_frame + n, sample, sample); }
            _frame += 3;
            _formalInput = new FakeInputProvider();
            _formalHud = _root.AddComponent<TacticalCardHud>(); _formalHud.Bind(_formalStage, _formalPump, inputProvider: _formalInput);
            Check("formal.ingress.original-planning-pause", _formalStage.Phase == TacticalStagePhase.Planning && time.IsWorldPaused, FormalState());
            return FormalReport();
        }

        public static string FormalVerifyPublishedRejection(string expected)
        {
            RequireSafeContext();
            var pending = _formalStage.Deck.PendingAction;
            bool accepted = _formalAdapter.TryPrepare(pending, _formalStage.Deck.PendingExecutionInput, out string issue);
            Check("published.exact-consumer-rejects-" + expected, !accepted && issue == "execution:" + expected
                && _formalStage.Ledger.Current == 50 && _formalStage.Driver.ExecutionId == 0
                && _probe.Source.Input.BehaviorControl.OwnerToken == 0, new { accepted, issue });
            FormalCancel();
            return FormalReport();
        }

        // Read-only authentication against the original project catalog, including unchanged zero-cooldown hashes.
        public static string VerifyNativePublishedCompatibility()
        {
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!state.Success) throw new InvalidOperationException(state.Diagnostic);
            var cards = state.CardCatalog.Cards.Where(card => card.TacticalSource != null).Select(card => new {
                id = card.StableCardId, revision = card.Revision, semanticHash = card.SemanticHash, contentHash = card.ContentHash,
                pass = card.SemanticHash == card.TacticalSource.SemanticHash
                    && card.ContentHash == CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(card.StableCardId,
                        card.Revision, card.MinimumRuntimeContractVersion, card.MaximumRuntimeContractVersion, card.Metadata, card.TacticalSource.SemanticHash)
                    && card.Metadata.CooldownSeconds == 0
            }).ToArray();
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { pass = cards.Length > 0 && cards.All(card => card.pass), cards }, Newtonsoft.Json.Formatting.Indented);
        }

        private static SettleSample FormalStable(long frame) => new SettleSample(frame, false, true, true, false, false, false,
            true, true, true, false, 0, 0, noActionStarted: true);

        public static string FormalSelect()
        {
            RequireSafeContext();
            bool accepted = _formalHud.TrySelectHandSlot(0); FormalDrain(); PollFormalHud();
            Check("formal.hud.select-reserves-once", accepted && _formalStage.Ledger.Current == 50 && _formalStage.Ledger.Reserved == 10,
                FormalState());
            Check("formal.hud.missing-point-cannot-confirm", !_formalHud.CanConfirmExecution, FormalState());
            return FormalReport();
        }

        public static string FormalPointer(bool valid) => FormalPointerAt(0, valid ? 2500 : 6000, valid);
        public static string FormalPointerAt(int xMillimeters, int zMillimeters) => FormalPointerAt(xMillimeters, zMillimeters, true);
        private static string FormalPointerAt(int xMillimeters, int zMillimeters, bool valid)
        {
            RequireSafeContext();
            var camera = _root.GetComponentInChildren<Camera>();
            var world = new Vector3(xMillimeters / 1000f, 0, zMillimeters / 1000f);
            var screen = camera.WorldToScreenPoint(world);
            _formalInput.SetSnapshot(new InputSnapshot(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(screen.x, screen.y),
                Vector2.zero, 0, false, false, false, false, false, false, false, false, false, false, false, false, false, clickPressed: true));
            PollFormalHud(); _formalInput.SetSnapshot(InputSnapshot.Empty); FormalDrain(); PollFormalHud();
            bool selected = _formalStage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.WorldPoint;
            Check(valid ? "formal.pointer.snapshot-ray-selects-ground" : "formal.pointer.out-of-range-rejects", valid
                ? selected && _formalHud.CanConfirmExecution && Math.Abs(_formalStage.Deck.PendingExecutionInput.Point.Z - zMillimeters) <= 10
                    && Math.Abs(_formalStage.Deck.PendingExecutionInput.Point.X - xMillimeters) <= 10
                : !_formalHud.CanConfirmExecution && !selected, FormalState());
            return FormalReport();
        }

        public static string FormalCancel()
        {
            RequireSafeContext();
            _formalInput.SetSnapshot(new InputSnapshot(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                0, false, false, false, false, false, false, false, false, false, false, true, false, false));
            PollFormalHud(); _formalInput.SetSnapshot(InputSnapshot.Empty); FormalDrain(); PollFormalHud();
            Check("formal.pointer.cancel-releases-reservation", _formalStage.Deck.PendingAction == null && _formalStage.Ledger.Current == 50
                && _formalStage.Ledger.Reserved == 0 && _formalStage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.None, FormalState());
            return FormalReport();
        }

        public static string FormalConfirm()
        {
            RequireSafeContext();
            bool accepted = _formalHud.TryConfirmExecution(); FormalDrain();
            Check("formal.hud.confirm-keeps-original-reservation", accepted && _formalStage.Driver.ExecutionId != 0
                && _formalStage.Ledger.Current == 50 && _formalStage.Ledger.Reserved == 10 && !_formalStage.Deck.IsCardOnCooldown(_formalStage.Deck.ResolvingAction, _frame), FormalState());
            Check("formal.hud.repeat-confirm-cannot-submit", !_formalHud.TryConfirmExecution(), FormalState());
            return FormalReport();
        }

        private static void FormalDrain()
        {
            if (_formalStage.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.WorldPoint)
                _formalSelectedPoint = _formalStage.Deck.PendingExecutionInput.Point;
            _formalAdapter.SetContext(null, new RuntimeFrame(_frame)); _formalPump.Drain();
            while (_formalPump.TryReadReceipt(out var receipt))
                if (receipt.Rejection != TacticalStageRejection.None) throw new InvalidOperationException(receipt.Rejection.ToString());
            _formalHud.RefreshIfChanged();
        }
        private static void PollFormalHud() => typeof(TacticalCardHud).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_formalHud, null);

        private static void StepFormal(bool replaceOwner = false)
        {
            _frame++; _formalAdapter.SetContext(null, new RuntimeFrame(_frame)); _formalAdapter.Tick();
            if (replaceOwner) _commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(_frame),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, _probe.Source.Id.Value, Vector2.right, 1,
                new CharacterTestBehaviorControlInput(2, 1000, 1000, ownerToken: 222)));
            var tick = new RuntimeTickContext(_frame, 1d / 60d, _frame / 60d, RuntimeTickStage.Simulation);
            _commands.Tick(tick); _probe.Runtime.Tick(tick);
            _formalAdapter.SetPhysicsSample(_frame, null, default, hasPendingImpulse: _probe.Source.PendingMotionRequestCount != 0,
                hasStableSupport: _probe.Source.IsGrounded);
            var observation = _formalAdapter.Observe(_formalStage.Driver.ExecutionId);
            if (observation.ExecutionStarted && _formalStartedTick < 0) _formalStartedTick = observation.ExecutionStartedTick;
            var sample = _formalAdapter.BuildSettleSample(_frame, _probe.Source);
            var opponent = FormalStable(_frame); _formalStage.OnCompletedSimulationTick(_frame, sample, opponent);
            _actorView.transform.position = new Vector3(Origin.X / 1000f, Origin.Y / 1000f + 0.9f, Origin.Z / 1000f);
            PollFormalHud();
        }

        public static string FormalAdvance(int count)
        {
            RequireSafeContext();
            for (int n = 0; n < count && !_formalStage.Time.IsWorldPaused; n++) StepFormal();
            return FormalReport();
        }

        public static string FormalCheckStarted()
        {
            RequireSafeContext();
            StepFormal();
            Check("formal.first-consumer-acceptance-pays-once", _formalStartedTick >= 0 && _formalStage.Ledger.Current == 40
                && _formalStage.Ledger.Reserved == 0, FormalState());
            Check("formal.start-original-cooldown-no-physical-action", _formalStage.Deck.CooldownTracker.IsOnCooldown(_probe.Source.Identity.CharacterId,
                _formalCard, _frame) && !_formalAdapter.Observe(_formalStage.Driver.ExecutionId).ActionStarted, FormalState());
            return FormalReport();
        }

        public static string FormalFinish()
        {
            RequireSafeContext();
            FormalAdvance(240);
            Check("formal.arrival-settles-and-releases-owner", _formalStage.Phase == TacticalStagePhase.Planning && Released
                && _formalStage.Ledger.Current == 40
                && Math.Abs(Origin.X - _formalSelectedPoint.X) <= 200 && Math.Abs(Origin.Z - _formalSelectedPoint.Z) <= 200, FormalState());
            Check("formal.cooldown-bound-to-first-accepted-frame", _formalStartedTick >= 0
                && _formalStage.Deck.CooldownTracker.IsOnCooldown(_probe.Source.Identity.CharacterId, _formalCard, _formalStartedTick + 119)
                && !_formalStage.Deck.CooldownTracker.IsOnCooldown(_probe.Source.Identity.CharacterId, _formalCard, _formalStartedTick + 120), FormalState());
            return FormalReport();
        }

        public static string FormalFailBeforeStart()
        {
            RequireSafeContext();
            _probe.Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, -4000, 0));
            FormalAdvance(30);
            Check("formal.delayed-range-failure-releases-original-reservation", _formalStartedTick < 0 && _formalStage.Ledger.Current == 50
                && _formalStage.Ledger.Reserved == 0 && Released && !_formalStage.Deck.CooldownTracker.IsOnCooldown(
                    _probe.Source.Identity.CharacterId, _formalCard, _frame), FormalState());
            return FormalReport();
        }

        public static string FormalInterrupt()
        {
            RequireSafeContext();
            _formalAdapter.ReleasePointExecution(); FormalAdvance(30);
            Check("formal.post-start-interruption-keeps-payment-and-cooldown", _formalStartedTick >= 0 && _formalStage.Ledger.Current == 40
                && _formalStage.Ledger.Reserved == 0 && Released && _formalStage.Deck.CooldownTracker.IsOnCooldown(
                    _probe.Source.Identity.CharacterId, _formalCard, _frame), FormalState());
            return FormalReport();
        }

        public static string FormalTimeout()
        {
            RequireSafeContext();
            FormalAdvance(60);
            Check("formal.timeout-keeps-started-payment-and-releases", _formalStartedTick >= 0 && _formalStage.Ledger.Current == 40
                && _formalStage.Ledger.Reserved == 0 && Released && _formalStage.Driver.LastIssue.Contains("TimedOut"), FormalState());
            return FormalReport();
        }

        public static string FormalBlockedAfterStart()
        {
            RequireSafeContext();
            AddWall(902001, new Vector3(0, 1.5f, 2.5f), new Vector3(1.5f, 3, 1.5f));
            FormalAdvance(240);
            Check("formal.dynamic-obstruction-keeps-started-payment-and-releases", _formalStartedTick >= 0 && _formalStage.Ledger.Current == 40
                && _formalStage.Ledger.Reserved == 0 && Released && !string.IsNullOrEmpty(_formalStage.Driver.LastIssue), FormalState());
            return FormalReport();
        }

        public static string FormalSameFrameReplacement()
        {
            RequireSafeContext();
            StepFormal(true);
            Check("formal.same-frame-new-owner-cannot-erase-acceptance", _formalStartedTick >= 0 && _formalStage.Ledger.Current == 40
                && _probe.Source.Input.BehaviorControl.OwnerToken == 222 && _probe.Source.Input.HasActiveMove, FormalState());
            _formalAdapter.ReleasePointExecution(); StepFormal();
            Check("formal.release-preserves-new-owner", _probe.Source.Input.BehaviorControl.OwnerToken == 222
                && _probe.Source.Input.HasActiveMove && _formalStage.Ledger.Current == 40, FormalState());
            return FormalReport();
        }

        private static object FormalState() => new { frame = _frame, phase = _formalStage.Phase.ToString(),
            health = _formalStage.Health.ToString(), current = _formalStage.Ledger.Current, reserved = _formalStage.Ledger.Reserved,
            execution = _formalStage.Driver.ExecutionId, started = _formalStartedTick, point = _formalStage.Deck.PendingExecutionInput.Point,
            origin = Origin, issue = _formalStage.Driver.LastIssue, feedback = _formalHud == null ? "" : _formalHud.CommandFeedbackMessage,
            pendingCommands = _commands.PendingCommandCount, control = _probe.Source.Input.BehaviorControl.OwnerToken };
        public static string FormalReport() => Newtonsoft.Json.JsonConvert.SerializeObject(new { state = FormalState(), results = Results,
            scope = "original HUD/owners with real isolated runtime/physics; snapshot, queued-device and OS-injected click checks are separately named; not a human hardware-click receipt" }, Newtonsoft.Json.Formatting.Indented);

        public static string CleanupFormal()
        {
            _manualActive = false;
            UnityEditor.EditorApplication.update -= ManualUpdate;
            UnityEditor.EditorApplication.playModeStateChanged -= ManualPlayStateChanged;
            if (_formalHud != null) UnityEngine.Object.DestroyImmediate(_formalHud);
            if (_formalOwnedPackage != null) FairyGUI.UIPackage.RemovePackage(_formalOwnedPackage);
            _formalOwnedPackage = null;
            _formalAdapter?.DisposePointInput(); _formalStage?.ReleaseControl(); _formalTime?.Dispose();
            _formalHud = null; _formalAdapter = null; _formalStage = null; _formalTime = null; _formalPump = null; _formalInput = null;
            _formalNativeInput = null;
            return "formal fixture released";
        }
        private sealed class FormalInputs : ICharacterTestTacticalCardInputSource
        {
            public bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue)
            { inputs = CharacterTestTacticalCardInputs.Unbound(_probe.Source.Identity.CharacterId); issue = ""; return true; }
            public bool TryPrepare(int actionId, out CharacterTestTacticalCardInputs inputs, out string issue) => TryPrepare(out inputs, out issue);
        }
    }
}
