using System;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Repository validation HUD. Reads authority state and submits versioned input intents.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(111)]
    [AddComponentMenu("MxFramework/Game/Character Test/Tactical Card HUD")]
    internal sealed partial class TacticalCardHud : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        private PanelSettings _ownedPanel;
        private ThemeStyleSheet _ownedTheme;
        private CharacterTestFairyGuiTacticalBattleView _fairyBattleView;
        private bool UsesFairyBattlePresentation => _fairyBattleView != null && !_fairyBattleView.IsDisposed && _preparationState == TacticalPreparationState.Battle;
        private TacticalStageFlowCoordinator _coordinator;
        private TacticalCommandPump _commands;
        private CharacterTestStoryFlowBridge _storyBridge;
        private IMxCursorController _cursorController;
        private IDisposable _cursorScope;
        private ulong _nextSequence = 1;
        private ulong _revision, _turn;
        private int _current = -1, _reserved = -1, _spendable = -1;
        private TacticalStagePhase _phase;
        private TacticalStageHealth _health;
        private TacticalStageRejection _fault;
        private TacticalStageRejection _rejection;
        private CharacterTestBattleOutcome _outcome;
        private string _executionIssue;
        private bool _paused, _dirty = true;
        private VisualElement _root, _outcomeBanner, _faultBanner, _commandFeedback;
        private Label _turnLabel, _phaseLabel, _currentLabel, _reservedLabel, _spendableLabel;
        private Label _clockLabel, _outcomeLabel, _faultLabel, _commandFeedbackLabel;
        private Button _confirm, _cancel, _restart, _recover, _faultRestart;
        private Button _openSelectBtn, _changeHeroFromOutcome;
        private TacticalCardInstance[] _slots = new TacticalCardInstance[3];
        private readonly Button[] _buttons = new Button[3];
        private readonly Label[] _names = new Label[3], _costs = new Label[3], _kinds = new Label[3];
        private readonly Label[] _descriptions = new Label[3], _states = new Label[3];
        // Finite ledger range; formatting happens once, outside refresh and command hot paths.
        private static readonly string[] Numbers = BuildNumbers();

        private TacticalPreparationState _preparationState = TacticalPreparationState.Battle;
        private GameManager _preparationManager;
        private bool _inventoryOpen;
        private bool _automaticBattlePresentation;

        internal void SetAutomaticBattlePresentation(bool active)
        {
            _automaticBattlePresentation = active;
            _dirty = true;
            _root?.EnableInClassList("tactical-hidden", active || _inventoryOpen);
        }

        internal void SetPreparationState(TacticalPreparationState state, string issue)
        {
            _preparationState = state; _dirty = true;
            if (_root != null) RefreshIfChanged();
        }
        public int HandCount => _coordinator?.Deck.Hand.Count ?? 0;
        internal TacticalMotionReadout MotionReadout => TacticalMotionReadout.FromLedger(
            _coordinator?.Ledger, _coordinator?.InitialMomentum ?? 0);
        public int CurrentMomentum => MotionReadout.Current;
        public int ReservedMomentum => MotionReadout.Reserved;
        public int SpendableMomentum => MotionReadout.Spendable;
        public TacticalStagePhase Phase => _coordinator?.Phase ?? TacticalStagePhase.Booting;
        public ulong TurnIndex => _coordinator?.TurnIndex ?? 0;
        public TacticalStageHealth Health => _coordinator?.Health ?? TacticalStageHealth.Running;
        public bool IsBound => _coordinator != null;
        public bool IsWorldPaused => _coordinator != null && _coordinator.Time.IsWorldPaused;
        internal float WorldRateFraction => _coordinator?.Time.WorldRateFraction ?? 1f;
        internal float WorldTransitionProgress => _coordinator?.Time.TransitionProgress ?? 1f;
        internal bool IsWorldAccelerating => _coordinator?.Time.IsAccelerating == true;
        internal bool IsWorldDecelerating => _coordinator?.Time.IsDecelerating == true;
        public bool HasOutcome => Phase == TacticalStagePhase.StageCompleted &&
            _storyBridge != null && _storyBridge.LastBattleOutcome != CharacterTestBattleOutcome.None;
        public bool IsVictory => HasOutcome && _storyBridge.LastBattleOutcome == CharacterTestBattleOutcome.Victory;
        public string OutcomeMessage => !HasOutcome ? string.Empty : IsVictory ? "战斗胜利！" : "战斗失败！";
        public string FaultMessage { get; private set; } = string.Empty;
        public bool CanConfirmExecution => CanPlan && _coordinator.Deck.PendingAction != null
            && _coordinator.Deck.IsDefinitionAvailable(_coordinator.Deck.PendingAction.Definition)
            && (!NeedsPoint || ((_pointPickStatus == TacticalInputStatus.Valid
                || (_pointPickStatus == TacticalInputStatus.MissingInput && _coordinator.Deck.PendingExecutionInput.Kind == TacticalExecutionInputKind.WorldPoint))
                && _coordinator.Driver.ValidateConfirmationInput(_coordinator.Deck.PendingAction) == TacticalInputStatus.Valid));
        public bool CanCancelPending => CanPlan && _coordinator.Deck.PendingAction != null;
        public bool CanRequestRecovery => _coordinator?.CanRequestRecovery ?? false;
        public bool CanRequestRestart => IsBound && Health != TacticalStageHealth.AwaitingStoryTransfer;
        public TacticalStageRejection LastSubmission { get; private set; }
        private string _commandFeedbackMessage = string.Empty;
        public string CommandFeedbackMessage
        {
            get => !string.IsNullOrEmpty(_pointFeedback) ? _pointFeedback : _commandFeedbackMessage;
            private set => _commandFeedbackMessage = value ?? string.Empty;
        }
        private bool CanPlan => _preparationState == TacticalPreparationState.Battle && IsBound && Health == TacticalStageHealth.Running && Phase == TacticalStagePhase.Planning;

        /// <remarks>LowFreqAlloc for view construction only. Rebinding never resets a live source sequence.</remarks>
        public void Bind(TacticalStageFlowCoordinator coordinator, TacticalCommandPump commands,
            CharacterTestStoryFlowBridge storyBridge = null, IMxCursorController cursorController = null, IInputProvider inputProvider = null)
        {
            if ((coordinator == null) != (commands == null) || (commands != null && !commands.Uses(coordinator)))
                throw new ArgumentException("HUD requires a matching stage and command pump.");
            if (!ReferenceEquals(_coordinator, coordinator))
            { ResetDirectPlay(); Array.Clear(_slots, 0, _slots.Length); _behindPreviewCard = null; _cancelledPreviewCard = null; HidePointVisuals(); }
            _coordinator = coordinator;
            _commands = commands;
            _pointInputProvider = inputProvider;
            _storyBridge = storyBridge;
            if (commands != null && commands.GetLastAcceptedSequence(0) >= _nextSequence)
                _nextSequence = commands.GetLastAcceptedSequence(0) == ulong.MaxValue ? 0 : commands.GetLastAcceptedSequence(0) + 1;
            SetCursorController(cursorController);
            _dirty = true;
            if (isActiveAndEnabled) { EnsureView(); RefreshIfChanged(); }
        }

        private void SetCursorController(IMxCursorController cursorController)
        {
            if (ReferenceEquals(_cursorController, cursorController))
            {
                if (_cursorScope == null && _cursorController != null && isActiveAndEnabled)
                    _cursorScope = _cursorController.Push(MxCursorRequest.Released("CharacterTest.TacticalCard.Hud"));
                EnsureCursorVisible();
                return;
            }

            _cursorScope?.Dispose();
            _cursorScope = null;
            _cursorController = cursorController;
            if (isActiveAndEnabled && _cursorController != null)
                _cursorScope = _cursorController.Push(MxCursorRequest.Released("CharacterTest.TacticalCard.Hud"));
            EnsureCursorVisible();
        }

        private void EnsureCursorController()
        {
            if (_cursorController == null)
            {
                var gm = GetComponent<GameManager>();
                if (gm != null && gm.RuntimeContext != null)
                    _cursorController = gm.RuntimeContext.CursorController;
            }

            if (_cursorScope == null && _cursorController != null)
                _cursorScope = _cursorController.Push(MxCursorRequest.Released("CharacterTest.TacticalCard.Hud"));

            EnsureCursorVisible();
        }

        private static void EnsureCursorVisible()
        {
            if (UnityEngine.Cursor.lockState != CursorLockMode.None)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else if (!UnityEngine.Cursor.visible)
            {
                UnityEngine.Cursor.visible = true;
            }
        }

        public TacticalCardInstance GetHandSlot(int slotIndex)
        {
            RefreshIfChanged();
            return slotIndex >= 0 && slotIndex < _slots.Length ? _slots[slotIndex] : null;
        }

        internal bool CanSelectHandSlot(int slotIndex)
        {
            var card = GetHandSlot(slotIndex);
            return CanPlan && card != null && card.Zone == CardZone.Hand && _coordinator.EvaluateHandCard(card).IsLegal;
        }

        // Read-only feedback from the same authority used by the click gate.
        // The hand view samples this during Sync; no UI or wall clock advances cooldowns.
        internal string GetHandSlotUseNotice(int slotIndex)
        {
            var card = GetHandSlot(slotIndex);
            if (DirectPlayActive && !DirectPlayNeedsPoint) return "正在提交出牌，请稍候。";
            if (card == null) return "空手牌槽。";
            if (card.Zone == CardZone.PendingAction) return "请选择有效落点；右键或 Esc 取消。";
            if (card.Zone == CardZone.Resolving) return "正在执行，结束后才能再次行动。";
            if (!CanPlan) return "当前不是自由期，暂时不能行动。";
            var evaluation = _coordinator.EvaluateHandCard(card);
            switch (evaluation.Failure)
            {
                case CardPlayFailure.None: return HandCardNeedsWorldPoint(slotIndex)
                    ? "拖至有效落点后松开使用。" : "向上拖出手牌后松开使用。";
                case CardPlayFailure.CardOnCooldown: return "此卡正在冷却。";
                case CardPlayFailure.BoundActionOnCooldown:
                    return evaluation.SimulationTickRate > 0
                        ? $"装配动作冷却中，剩余 {(double)evaluation.RemainingCooldownFrames / evaluation.SimulationTickRate:0.0} 秒。"
                        : $"装配动作冷却中，剩余 {evaluation.RemainingCooldownFrames} 帧。";
                case CardPlayFailure.BoundActionUnavailable: return "装配动作、消耗或冷却条件不可用，请检查动作配置与角色状态。";
                case CardPlayFailure.WeaponTypeUnclassified: return "动作的武器分类尚未核定。";
                case CardPlayFailure.WeaponTypeMismatch: return "当前武器不满足动作要求。";
                case CardPlayFailure.CharacterUnavailable: return "角色当前不能开始行动。";
                case CardPlayFailure.InsufficientMomentum: return "可用动势不足。";
                case CardPlayFailure.EmptyActionSlot:
                case CardPlayFailure.MissingActionOrSlotBinding: return "此卡缺少有效的装配动作，请先完成动作装配。";
                case CardPlayFailure.ActionAlreadyPending: return "正在处理一次出牌，请完成选点或取消。";
                case CardPlayFailure.CardNotInHand: return "此卡当前不在手牌中。";
                case CardPlayFailure.WrongPhase: return "当前不是自由期，暂时不能行动。";
                default: return "当前不能使用，请先完成或取消待执行动作。";
            }
        }

        public bool TrySelectHandSlot(int slotIndex)
        {
            var card = GetHandSlot(slotIndex);
            if (!CanPlan || card == null || card.Zone != CardZone.Hand) return false;
            return Submit(card.Definition.Kind == TacticalCardKind.Action || InteractionPreparationPayload.IsPreparation(card.Definition)
                ? TacticalStageCommandKind.SelectAction : TacticalStageCommandKind.PlayUtility, card);
        }

        internal TacticalCardDisplayData GetCardDisplayData(int slotIndex) =>
            TacticalCardDisplayData.FromDefinition(GetHandSlot(slotIndex)?.Definition);

        internal MxFramework.UI.IMxUiTextProvider BattleTextProvider =>
            _preparationManager?.RuntimeContext?.LocalizationRuntime?.TextProvider;

        // Cold presentation path. Use the deployment's frozen per-instance bindings, never the editable loadout.
        internal CharacterTestCardUiPreviewData GetBattleCardPresentationData(int slotIndex)
        {
            var definition = GetHandSlot(slotIndex)?.Definition;
            var context = _preparationManager?.RuntimeContext;
            double frameSeconds = context?.SessionBuilder?.ResolveSimulationDeltaTime()
                ?? 1d / Math.Max(1, context?.Profile?.TargetFrameRate ?? CharacterTestDeterministicUnits.DefaultTickRate);
            return CharacterTestTacticalCardPresentationProjection.FromDeployment(definition,
                _cardTrialPresentation ? _cardTrialDeployment?.PlayerParticipant : GetComponent<TacticalCardSliceCoordinator>()?.BattleDeployment?.PlayerParticipant,
                _cardTrialPresentation ? _cardTrialCatalog : CharacterTacticalServices.Player?.Catalog, context?.Slice?.CharacterRuntime?.ActionRuntime,
                BattleTextProvider, frameSeconds);
        }

        public bool TryPlayUtility(int slotIndex)
        {
            var card = GetHandSlot(slotIndex);
            return CanPlan && card != null && card.Zone == CardZone.Hand && card.Definition.Kind == TacticalCardKind.Utility &&
                Submit(TacticalStageCommandKind.PlayUtility, card);
        }

        public bool TryConfirmExecution() => CanConfirmExecution && Submit(TacticalStageCommandKind.ConfirmExecution, _coordinator.Deck.PendingAction);
        public bool TryCancelPending()
        {
            if (!CanCancelPending || !Submit(TacticalStageCommandKind.CancelPending, _coordinator.Deck.PendingAction)) return false;
            if (DirectPlayActive) { _directCancelRequested = true; _directCancelSubmitted = true; }
            _cancelledPreviewCard = _coordinator.Deck.PendingAction;
            _behindPreviewCard = null; HidePointVisuals(); SetPointFeedback(string.Empty);
            return true;
        }
        public bool TryRequestRestart()
        {
            var flow = GetComponent<TacticalCardSliceCoordinator>();
            return flow != null ? flow.ReturnToPreparation("已返回整备，请再次确认出战。")
                : CanRequestRestart && Submit(TacticalStageCommandKind.RequestRestart, null);
        }
        public bool TryRequestRecovery() => CanRequestRecovery && Submit(TacticalStageCommandKind.RequestRecovery, null);

        private bool Submit(TacticalStageCommandKind kind, TacticalCardInstance card)
        {
            if (_nextSequence == 0) { LastSubmission = TacticalStageRejection.InvalidSequence; return false; }
            var command = new TacticalStageCommand(_coordinator.StageRunId, TurnIndex, card?.InstanceId ?? 0,
                card?.Version ?? 0, _coordinator.SnapshotRevision, kind);
            LastSubmission = _commands.TrySubmit(in command, source: 0, sequence: _nextSequence++);
            _dirty = true;
            return LastSubmission == TacticalStageRejection.None;
        }

        private async void OnEnable()
        {
            _preparationManager = GetComponent<GameManager>();
            _cardTrialPresentation = _preparationManager?.RuntimeContext?.RuntimeAiPlannerDevSceneController?.CardTrialOpen == true;
            if (!_cardTrialPresentation && _slots.Length != 3) _slots = new TacticalCardInstance[3];
            EnsureCursorController();
            EnsureView();
            _dirty = true;
            RefreshIfChanged();
            try
            {
                if (_preparationManager == null || _cardTrialPresentation) return;
                await CharacterTacticalServices.OpenAsync();
                if (this != null && isActiveAndEnabled) _dirty = true;
            }
            catch (Exception error) { FaultMessage = error.Message; _dirty = true; }
        }

        private long _inventoryRevision = -1;
        private void Update()
        {
            if (_cardTrialPresentation)
            {
                EnsureCursorVisible(); UpdatePointSelection(); RefreshIfChanged(); return;
            }
            long revision = CharacterTacticalServices.Player?.Revision ?? 0;
            if (revision != _inventoryRevision)
            {
                _inventoryRevision = revision; _dirty = true;
            }
            EnsureCursorVisible();
            _fairyBattleView?.ApplyBattleProjection();
            TickDirectPlay();
            UpdatePointSelection();
            RefreshIfChanged();
            bool inventory = _preparationManager?.RuntimeContext?.UiRuntime?.FairyGuiTacticalDeckPanelHost?.IsOpen == true;
            _inventoryOpen = inventory;
            if (_fairyBattleView == null || _fairyBattleView.IsDisposed) _fairyBattleView = CharacterTestFairyGuiTacticalBattleView.TryCreate(this);
            _fairyBattleView?.Sync(_preparationState == TacticalPreparationState.Battle && !inventory && !_automaticBattlePresentation);
            _root?.EnableInClassList("tactical-hidden", inventory || _automaticBattlePresentation || UsesFairyBattlePresentation);
        }

        private void LateUpdate()
        {
            if (_cardTrialPresentation) return;
            if(UsesFairyBattlePresentation){_fairyBattleView.ApplyBattleProjection();_fairyBattleView.RefreshBattleOverlayAfterCamera();}
        }

        private void OnDisable()
        {
            ResetDirectPlay();
            HidePointVisuals();
            _behindPreviewCard = null;
            _cursorScope?.Dispose();
            _cursorScope = null;
            if (_root != null) _root.AddToClassList("tactical-hidden");
            _fairyBattleView?.Sync(false);
        }

        private void OnDestroy()
        {
            DisposePointVisuals();
            _cursorScope?.Dispose();
            _cursorScope = null;
            _fairyBattleView?.Dispose();
            _fairyBattleView = null;
            _root?.RemoveFromHierarchy();
            if (_ownedPanel != null)
            {
                if (_document != null && _document.panelSettings == _ownedPanel) _document.panelSettings = null;
                Destroy(_ownedPanel);
            }
            if (_ownedTheme != null) Destroy(_ownedTheme);
        }

        /// <summary>NoAlloc for unchanged state and warmed, bounded state transitions. No simulation or receipt drain.</summary>
        public void RefreshIfChanged()
        {
            ulong revision = _coordinator?.SnapshotRevision ?? 0;
            var outcome = _storyBridge?.LastBattleOutcome ?? CharacterTestBattleOutcome.None;
            var fault = _coordinator?.Fault ?? TacticalStageRejection.None;
            var rejection = LastSubmission != TacticalStageRejection.None
                ? LastSubmission : _coordinator?.LastRejection ?? TacticalStageRejection.None;
            string issue = _coordinator?.ExecutionIssue ?? string.Empty;
            if (!_dirty && revision == _revision && _phase == Phase && _health == Health && _turn == TurnIndex &&
                _current == CurrentMomentum && _reserved == ReservedMomentum && _spendable == SpendableMomentum &&
                _paused == IsWorldPaused && _outcome == outcome && _fault == fault && _rejection == rejection && _executionIssue == issue) return;
            bool turnChanged = _dirty || _turn != TurnIndex;
            bool faultChanged = _dirty || _fault != fault || _health != Health || _executionIssue != issue;
            bool rejectionChanged = _dirty || _rejection != rejection || _executionIssue != issue;
            _revision = revision; _phase = Phase; _health = Health; _turn = TurnIndex;
            _current = CurrentMomentum; _reserved = ReservedMomentum; _spendable = SpendableMomentum;
            _paused = IsWorldPaused; _outcome = outcome; _fault = fault; _rejection = rejection; _executionIssue = issue; _dirty = false;
            SynchronizeSlots();
            if (faultChanged)
                FaultMessage = Health == TacticalStageHealth.SuspendedByFault
                    ? "战斗已保护暂停：" + fault + (string.IsNullOrEmpty(issue) ? string.Empty : "\n" + issue)
                        + (CanRequestRecovery ? "\n可尝试继续等待收势。" : "\n本次执行不能继续，请返回整备后重新出战。") : string.Empty;
            if (rejectionChanged) CommandFeedbackMessage = DescribeCommandFeedback(rejection, issue);
            if (_cardTrialPresentation) return;
            if (_root == null) return;
            if (_automaticBattlePresentation || UsesFairyBattlePresentation)
            {
                _root.AddToClassList("tactical-hidden");
                return;
            }
            _root.RemoveFromClassList("tactical-hidden");
            bool battle = _preparationState == TacticalPreparationState.Battle;
            _root.Q<VisualElement>(className: "tactical-hand")?.EnableInClassList("tactical-hidden", !battle);
            _root.Q<VisualElement>(className: "tactical-controls")?.EnableInClassList("tactical-hidden", !battle);
            _root.Q<VisualElement>(className: "tactical-header")?.EnableInClassList("tactical-hidden", !battle);
            if (turnChanged) _turnLabel.text = _turn < (ulong)Numbers.Length ? Numbers[(int)_turn] : _turn.ToString(CultureInfo.InvariantCulture);
            _phaseLabel.text = PhaseText(Phase);
            _currentLabel.text = Number(_current); _reservedLabel.text = Number(_reserved); _spendableLabel.text = Number(_spendable);
            _clockLabel.text = !IsBound ? "等待战斗连接" : _paused ? "世界暂停" : "世界运行中";
            for (int i = 0; i < 3; i++) RefreshSlot(i);
            _confirm.SetEnabled(CanConfirmExecution); _cancel.SetEnabled(CanCancelPending);
            _commandFeedback?.EnableInClassList("tactical-hidden", string.IsNullOrEmpty(CommandFeedbackMessage));
            if (_commandFeedbackLabel != null) _commandFeedbackLabel.text = CommandFeedbackMessage;
            _outcomeBanner.EnableInClassList("tactical-hidden", !HasOutcome);
            _outcomeLabel.text = OutcomeMessage;
            _restart.SetEnabled(CanRequestRestart);
            _faultBanner.EnableInClassList("tactical-hidden", Health != TacticalStageHealth.SuspendedByFault);
            _faultLabel.text = FaultMessage;
            _recover.SetEnabled(CanRequestRecovery); _faultRestart.SetEnabled(CanRequestRestart);
        }

        private void SynchronizeSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && (!IsBound || (_slots[i].Zone != CardZone.Hand &&
                    !ReferenceEquals(_slots[i], _coordinator.Deck.PendingAction) && !ReferenceEquals(_slots[i], _coordinator.Deck.ResolvingAction)))) _slots[i] = null;
            if (!IsBound) return;
            for (int i = 0; i < _coordinator.Deck.Hand.Count; i++) PlaceCard(_coordinator.Deck.Hand[i]);
            PlaceCard(_coordinator.Deck.PendingAction); PlaceCard(_coordinator.Deck.ResolvingAction);
        }

        private void PlaceCard(TacticalCardInstance card)
        {
            if (card == null) return;
            for (int i = 0; i < _slots.Length; i++) if (ReferenceEquals(_slots[i], card)) return;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] == null) { _slots[i] = card; return; }
        }

        private void RefreshSlot(int i)
        {
            var card = _slots[i];
            var display = TacticalCardDisplayData.FromDefinition(card?.Definition);
            _names[i].text = display.IsAvailable ? display.Name : "空手牌槽";
            _costs[i].text = display.IsAvailable ? Number(display.Cost) : "—";
            _kinds[i].text = !display.IsAvailable ? string.Empty : display.Kind == TacticalCardKind.Action ? "Action" : "Utility";
            _descriptions[i].text = display.Description;
            bool available = CanPlan && card != null && _coordinator.EvaluateHandCard(card).IsLegal;
            _states[i].text = card == null ? "禁用不可用" : card.Zone == CardZone.PendingAction ? "已选待命 Pending" :
                card.Zone == CardZone.Resolving ? "正在推演 Resolving" : available ? "手牌中" : "禁用不可用";
            _buttons[i].SetEnabled(available);
            _buttons[i].EnableInClassList("tactical-selected", card != null && card.Zone == CardZone.PendingAction);
            _buttons[i].EnableInClassList("tactical-resolving", card != null && card.Zone == CardZone.Resolving);
        }

        private void EnsureView()
        {
            if (_cardTrialPresentation) return;
            if (_document == null) _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (_document.panelSettings == null)
            {
                if (_panelSettings == null)
                {
                    _ownedPanel = ScriptableObject.CreateInstance<PanelSettings>();
                    // The tree supplies its own complete stylesheet; an owned theme also makes standalone panels valid.
                    _ownedTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                    _ownedPanel.themeStyleSheet = _ownedTheme;
                    _ownedPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    _ownedPanel.referenceResolution = new Vector2Int(1280, 720);
                    _panelSettings = _ownedPanel;
                }
                _document.panelSettings = _panelSettings;
            }
            if (_root != null && _root.parent == _document.rootVisualElement) return;
            var tree = UnityEngine.Resources.Load<VisualTreeAsset>("TacticalCard/TacticalCardHud");
            if (tree == null) throw new InvalidOperationException("TacticalCard HUD UXML is missing.");
            _root = tree.Instantiate();
            _root.AddToClassList("tactical-document");
            _root.pickingMode = PickingMode.Ignore;
            _document.rootVisualElement.pickingMode = PickingMode.Ignore;
            _document.rootVisualElement.Add(_root);
            _turnLabel = _root.Q<Label>("turnValue"); _phaseLabel = _root.Q<Label>("phaseValue");
            _currentLabel = _root.Q<Label>("currentValue"); _reservedLabel = _root.Q<Label>("reservedValue");
            _spendableLabel = _root.Q<Label>("spendableValue"); _clockLabel = _root.Q<Label>("clockValue");
            for (int i = 0; i < 3; i++)
            {
                _buttons[i] = _root.Q<Button>("hand" + i);
                _names[i] = _buttons[i].Q<Label>("cardName"); _costs[i] = _buttons[i].Q<Label>("cardCost");
                _kinds[i] = _buttons[i].Q<Label>("cardKind"); _descriptions[i] = _buttons[i].Q<Label>("cardDescription");
                _states[i] = _buttons[i].Q<Label>("cardState");
            }
            _buttons[0].clicked += SelectZero; _buttons[1].clicked += SelectOne; _buttons[2].clicked += SelectTwo;
            _confirm = _root.Q<Button>("confirmExecution"); _confirm.clicked += Confirm;
            _cancel = _root.Q<Button>("cancelPending"); _cancel.clicked += Cancel;
            _commandFeedback = _root.Q("commandFeedback");
            _commandFeedbackLabel = _root.Q<Label>("commandFeedbackMessage");
            _outcomeBanner = _root.Q("outcomeBanner"); _outcomeLabel = _root.Q<Label>("outcomeMessage");
            _restart = _root.Q<Button>("restartBattle"); _restart.clicked += Restart;
            _changeHeroFromOutcome = _root.Q<Button>("changeHeroFromOutcome");
            if (_changeHeroFromOutcome != null) _changeHeroFromOutcome.clicked += () => { EnsureCursorVisible(); TryRequestRestart(); };
            _openSelectBtn = _root.Q<Button>("openSelectBtn");
            if (_openSelectBtn != null) _openSelectBtn.clicked += () => { EnsureCursorVisible(); GetComponent<TacticalCardSliceCoordinator>()?.ReturnToPreparation(); };
            _faultBanner = _root.Q("faultBanner"); _faultLabel = _root.Q<Label>("faultMessage");
            _recover = _root.Q<Button>("recoverBattle"); _recover.clicked += Recover;
            _faultRestart = _root.Q<Button>("resetBattle"); _faultRestart.clicked += Restart;
        }

        private void SelectZero() { EnsureCursorVisible(); if(UsesFairyBattlePresentation)TryDirectPlayHandSlot(0);else TrySelectHandSlot(0); }
        private void SelectOne() { EnsureCursorVisible(); if(UsesFairyBattlePresentation)TryDirectPlayHandSlot(1);else TrySelectHandSlot(1); }
        private void SelectTwo() { EnsureCursorVisible(); if(UsesFairyBattlePresentation)TryDirectPlayHandSlot(2);else TrySelectHandSlot(2); }
        private void Confirm() { EnsureCursorVisible(); TryConfirmExecution(); }
        private void Cancel() { if(BattleCancelInputBlocked)return; EnsureCursorVisible(); if(DirectPlayActive)CancelDirectPlay();else TryCancelPending(); }
        private void Restart() { EnsureCursorVisible(); TryRequestRestart(); }
        private void Recover() { EnsureCursorVisible(); TryRequestRecovery(); }
        private static string DescribeCommandFeedback(TacticalStageRejection rejection, string issue)
        {
            if (rejection == TacticalStageRejection.None) return string.Empty;
            if (rejection == TacticalStageRejection.ExecutionRejected && !string.IsNullOrEmpty(issue))
            {
                if (issue.StartsWith("execution:Point:", StringComparison.Ordinal)
                    || issue.StartsWith("execution-input:", StringComparison.Ordinal))
                    return "前往地点未完成：" + issue;
                if (issue.IndexOf("ActionSummary:", StringComparison.Ordinal) >= 0)
                    return (issue.IndexOf("auto-repair", StringComparison.Ordinal) >= 0
                        ? "出牌未执行：动作测量摘要不可用，自动重建失败。\n"
                        : "出牌未执行：动作测量摘要不可用。\n") + issue;
                return "出牌未执行：" + issue;
            }
            return "操作未执行：" + rejection;
        }
        private static string Number(int value) => value >= 0 && value < Numbers.Length ? Numbers[value] : value.ToString(CultureInfo.InvariantCulture);
        private static string[] BuildNumbers()
        {
            var values = new string[1025];
            for (int i = 0; i < values.Length; i++) values[i] = i.ToString(CultureInfo.InvariantCulture);
            return values;
        }
        private static string PhaseText(TacticalStagePhase phase)
        {
            switch (phase)
            {
                case TacticalStagePhase.AwaitingIngressSettle: return "AwaitingIngressSettle · 入场平息";
                case TacticalStagePhase.Planning: return "Planning · 战术准备";
                case TacticalStagePhase.Executing: return "Executing · 推演中";
                case TacticalStagePhase.TurnSettling: return "TurnSettling · 收势平息";
                case TacticalStagePhase.StageCompleted: return "StageCompleted · 战斗结束";
                default: return "Booting · 正在加载";
            }
        }
    }
}
