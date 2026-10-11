using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Damage;
using MxFramework.Input;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest composition shell for the shared, player-facing Guidance presenter.
    /// </summary>
    /// <remarks>AllocByDesign during scene composition. This is intentionally not a MonoBehaviour.</remarks>
    public sealed class CharacterTestFairyGuiGuidanceHost : IDisposable
    {
        private static readonly MxUiGuidanceScopeId DevelopmentPreviewCardScope =
            new MxUiGuidanceScopeId("character_test.guidance.development_preview.card");
        private static readonly MxUiGuidanceScopeId DevelopmentPreviewSuccessScope =
            new MxUiGuidanceScopeId("character_test.guidance.development_preview.success");
        private static readonly MxUiGuidanceScopeId DevelopmentPreviewWarningScope =
            new MxUiGuidanceScopeId("character_test.guidance.development_preview.warning");

        private CharacterTestGuidanceSequenceCoordinator _sequenceCoordinator;
        private CharacterTestGuidanceSnapshotMapper _snapshotMapper;
        private CharacterTestGuidancePlan _guidancePlan;
        private GameSlice _slice;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private InputBindingDisplayService _bindingDisplay;
        private IMxUiTextProvider _textProvider;
        private MxFairyGuiGuidancePresenter _presenter;
        private long _bindingRevision;
        private long _textRevision;
        private string _lastStatusMessage = string.Empty;
        private bool _configured;

        public bool IsConfigured => _configured;
        public bool IsReady => _presenter?.IsReady == true;
        public bool HasVisibleContent =>
            _presenter != null && (_presenter.HasCard || _presenter.NoticeCount > 0);
        public string LastStatusMessage => _lastStatusMessage;
        public IMxUiGuidanceSink Sink => _presenter;
        public MxFairyGuiGuidancePresenter Presenter => _presenter;
        internal CharacterTestGuidanceSelectionSnapshot SelectionSnapshot =>
            _sequenceCoordinator != null
                ? _sequenceCoordinator.CreateSelectionSnapshot()
                : default;

        /// <summary>
        /// Legacy cache-less configuration entry. It intentionally fails closed because Player
        /// Guidance must use the scene-owned compiled Authoring cache.
        /// </summary>
        public void Configure(
            GameSlice slice,
            MxFairyGuiRuntimeShell runtimeShell,
            IRuntimeLogger logger = null,
            IMxUiTextProvider textProvider = null,
            InputBindingDisplayService bindingDisplay = null)
        {
            Release();
            _logger = logger ?? NullRuntimeLogger.Instance;
            Warn(
                CharacterTestGuidanceRuntimeDiagnosticCode.CacheMissing
                + ": Legacy Guidance configuration has no scene cache and was disabled.");
        }

        internal void ConfigureFromSceneCache(
            CharacterTestGuidanceSceneCache sceneCache,
            GameSlice slice,
            MxFairyGuiRuntimeShell runtimeShell,
            IRuntimeLogger logger = null,
            IMxUiTextProvider textProvider = null,
            InputBindingDisplayService bindingDisplay = null)
        {
            Release();
            _logger = logger ?? NullRuntimeLogger.Instance;
            CharacterTestGuidanceRuntimeDiagnostic diagnostic =
                CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.CacheMissing,
                    "Player Guidance scene cache is missing.");
            if (sceneCache == null
                || !sceneCache.TryCompilePlan(
                    out CharacterTestGuidancePlan plan,
                    out diagnostic))
            {
                string message = string.IsNullOrWhiteSpace(diagnostic.Message)
                    ? "Player Guidance disabled because the scene cache is missing."
                    : diagnostic.Message;
                message = diagnostic.Code + ": " + message;
                if (diagnostic.IsError)
                {
                    Warn(message);
                }
                else
                {
                    _lastStatusMessage = message;
                    _logger.Info("CharacterTest.FairyGUI.Guidance", message);
                }
                return;
            }

            ConfigureWithPlan(plan, slice, runtimeShell, logger, textProvider, bindingDisplay);
        }

        private void ConfigureWithPlan(
            CharacterTestGuidancePlan plan,
            GameSlice slice,
            MxFairyGuiRuntimeShell runtimeShell,
            IRuntimeLogger logger,
            IMxUiTextProvider textProvider,
            InputBindingDisplayService bindingDisplay)
        {
            Release();
            _logger = logger ?? NullRuntimeLogger.Instance;
            if (plan == null || !plan.Enabled || plan.StepCount == 0)
            {
                Warn("Player Guidance disabled because no compiled guidance plan is available.");
                return;
            }

            _guidancePlan = plan;
            _sequenceCoordinator = new CharacterTestGuidanceSequenceCoordinator(plan);
            _snapshotMapper = new CharacterTestGuidanceSnapshotMapper(plan);
            _slice = slice;
            _bindingDisplay = bindingDisplay;
            _bindingRevision = bindingDisplay?.Revision ?? 0L;
            if (_slice?.Resources?.ResourceManager == null)
            {
                Warn("Player Guidance disabled because resource runtime is not available.");
                return;
            }
            if (runtimeShell == null)
            {
                Warn("Player Guidance disabled because the shared FairyGUI runtime shell is not available.");
                return;
            }

            IMxUiTextProvider safeTextProvider = new CharacterTestGuidanceTextProvider(
                textProvider ?? MxUiNullTextProvider.Instance);
            _textProvider = safeTextProvider;
            _textRevision = safeTextProvider.Revision;
            MxFairyGuiRuntimeCatalog catalog =
                MxFairyGuiGuidancePresenter.CreateCatalog(safeTextProvider);
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics =
                catalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                LogFirstDiagnostic(diagnostics);
                return;
            }

            MxFairyGuiNavigator navigator = runtimeShell.Register(catalog);
            _presenter = new MxFairyGuiGuidancePresenter(
                navigator,
                safeTextProvider,
                MxFairyGuiGuidanceLayoutPreset.TopCornersOccupied);
            if (!_presenter.IsReady)
            {
                Warn("Player Guidance views failed to open: " + _presenter.LastDiagnosticMessage);
                _presenter.Dispose();
                _presenter = null;
                return;
            }

            SetLocalizedPrompts();
            CharacterTestGuidanceObservationContext context = CaptureContext();
            _sequenceCoordinator.Prime(in context);
            CharacterTestGuidanceObservation observation = context.Observation;
            MxUiGuidanceCardSnapshot initialCard = _snapshotMapper.CreateCurrentCard(
                _sequenceCoordinator,
                in observation);
            _presenter.PresentCard(in initialCard);
            _configured = true;
            _lastStatusMessage = string.Empty;
        }

        /// <summary>Projects newly confirmed domain facts into the current Guidance step.</summary>
        /// <remarks>LowFreqAlloc only when a new confirmed result changes UI text.</remarks>
        public void RefreshFromRuntime()
        {
            if (!_configured || _presenter == null || _slice == null)
                return;
            RefreshLocalizedPromptsIfChanged();
            CharacterTestGuidanceObservationContext context = CaptureContext();
            CharacterTestGuidanceObservation observation = context.Observation;
            if (!_sequenceCoordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome outcome)
                || !_snapshotMapper.TryMapOutcome(
                    _sequenceCoordinator,
                    in observation,
                    in outcome,
                    out MxUiGuidanceCardSnapshot card,
                    out MxUiGuidanceNotice notice))
            {
                RestoreGuidanceCardIfMissing();
                return;
            }

            _presenter.ClearScope(CharacterTestGuidanceSnapshotMapper.ScopeId);
            _presenter.PresentCard(in card);
            _presenter.TryPushNotice(in notice);
        }

        /// <summary>Selects an authored step by stable ID without changing gameplay authority.</summary>
        /// <remarks>LowFreqAlloc: rebuilds the selected guidance card and observer.</remarks>
        internal bool SelectTest(string stableId)
        {
            if (!_configured || _presenter == null || _slice == null)
                return false;
            CharacterTestGuidanceObservationContext context = CaptureContext();
            if (!_sequenceCoordinator.TrySelectStableId(stableId, in context))
                return false;
            CharacterTestGuidanceObservation observation = context.Observation;
            PresentCurrentCard(in observation);
            return true;
        }

        /// <summary>Selects the previous authored guidance step when one exists.</summary>
        internal bool SelectPreviousTest()
        {
            if (!_configured || _presenter == null || _slice == null)
                return false;
            CharacterTestGuidanceObservationContext context = CaptureContext();
            if (!_sequenceCoordinator.TrySelectPrevious(in context))
                return false;
            CharacterTestGuidanceObservation observation = context.Observation;
            PresentCurrentCard(in observation);
            return true;
        }

        /// <summary>Selects the next authored guidance step when one exists.</summary>
        internal bool SelectNextTest()
        {
            if (!_configured || _presenter == null || _slice == null)
                return false;
            CharacterTestGuidanceObservationContext context = CaptureContext();
            if (!_sequenceCoordinator.TrySelectNext(in context))
                return false;
            CharacterTestGuidanceObservation observation = context.Observation;
            PresentCurrentCard(in observation);
            return true;
        }

        /// <summary>Advances notice timing and authored auto-advance timing.</summary>
        /// <remarks>NoAlloc after configuration.</remarks>
        public void TickPresentation(float deltaTime)
        {
            RefreshLocalizedPromptsIfChanged();
            _presenter?.Tick(deltaTime);
            if (_presenter == null || _slice == null || _sequenceCoordinator == null)
                return;

            CharacterTestGuidanceObservationContext context = CaptureContext();
            if (_sequenceCoordinator.TryAdvance(deltaTime, in context))
            {
                CharacterTestGuidanceObservation observation = context.Observation;
                PresentCurrentCard(in observation);
            }
        }

        /// <summary>
        /// Shows explicitly labelled preview content for visual development and PlayMode verification.
        /// </summary>
        /// <remarks>LowFreqAlloc. Does not claim that any validation or observer has completed.</remarks>
        public bool ShowDevelopmentPreview()
        {
            if (!_configured || _presenter == null)
                return false;

            MxUiGuidanceCardSnapshot card = CreateDevelopmentPreviewCard();
            _presenter.PresentCard(in card);
            MxUiGuidanceNotice success = new MxUiGuidanceNotice(
                DevelopmentPreviewSuccessScope,
                new MxUiGuidanceMessageId("preview.success"),
                MxUiGuidanceTone.Success,
                MxUiGuidanceGlyph.Success,
                Text(
                    "ui.character_test.guidance.preview.success",
                    "效果预览：Typed 读数已采集"),
                8f);
            MxUiGuidanceNotice warning = new MxUiGuidanceNotice(
                DevelopmentPreviewWarningScope,
                new MxUiGuidanceMessageId("preview.warning"),
                MxUiGuidanceTone.Warning,
                MxUiGuidanceGlyph.Warning,
                Text(
                    "ui.character_test.guidance.preview.warning",
                    "效果预览：尚未进入观察区"),
                8f);
            bool successShown = _presenter.TryPushNotice(in success);
            bool warningShown = _presenter.TryPushNotice(in warning);
            return successShown && warningShown;
        }

        /// <summary>Clears the currently known CharacterTest guidance scopes.</summary>
        public void ClearGameplayPresentation()
        {
            if (_presenter == null)
                return;

            _presenter.ClearScope(CharacterTestGuidanceSnapshotMapper.ScopeId);
            _presenter.ClearScope(DevelopmentPreviewCardScope);
            _presenter.ClearScope(DevelopmentPreviewSuccessScope);
            _presenter.ClearScope(DevelopmentPreviewWarningScope);
        }

        public void DetachSlice()
        {
            _slice = null;
            _sequenceCoordinator?.Reset();
            _snapshotMapper?.Reset();
            ClearGameplayPresentation();
        }

        public void RebindSlice(GameSlice slice)
        {
            _slice = slice;
            _sequenceCoordinator?.Reset();
            _snapshotMapper?.Reset();
            ClearGameplayPresentation();
        }

        public void Release()
        {
            _presenter?.Dispose();
            _presenter = null;
            _slice = null;
            _bindingDisplay = null;
            _textProvider = null;
            _bindingRevision = 0L;
            _textRevision = 0L;
            _sequenceCoordinator?.Reset();
            _sequenceCoordinator = null;
            _snapshotMapper?.Reset();
            _snapshotMapper = null;
            _guidancePlan = null;
            _configured = false;
            _lastStatusMessage = string.Empty;
        }

        public void Dispose()
        {
            Release();
        }

        private void RefreshLocalizedPromptsIfChanged()
        {
            bool bindingChanged = _bindingDisplay != null
                && _bindingDisplay.Revision != _bindingRevision;
            bool textChanged = _textProvider != null
                && _textProvider.Revision != _textRevision;
            if (!bindingChanged && !textChanged)
                return;

            _bindingRevision = _bindingDisplay?.Revision ?? 0L;
            _textRevision = _textProvider?.Revision ?? 0L;
            SetLocalizedPrompts();
            if (_presenter?.HasCard == true
                && _snapshotMapper != null
                && _sequenceCoordinator != null)
            {
                CharacterTestGuidanceObservation observation = CaptureObservation();
                MxUiGuidanceCardSnapshot card = _snapshotMapper.CreateCurrentCard(
                    _sequenceCoordinator,
                    in observation);
                _presenter.PresentCard(in card);
            }
        }

        private void RestoreGuidanceCardIfMissing()
        {
            if (_presenter.HasCard)
                return;

            CharacterTestGuidanceObservation observation = CaptureObservation();
            MxUiGuidanceCardSnapshot card = _snapshotMapper.CreateCurrentCard(
                _sequenceCoordinator,
                in observation);
            _presenter.PresentCard(in card);
        }

        private void PresentCurrentCard(in CharacterTestGuidanceObservation observation)
        {
            MxUiGuidanceCardSnapshot card = _snapshotMapper.CreateCurrentCard(
                _sequenceCoordinator,
                in observation);
            _presenter.ClearScope(CharacterTestGuidanceSnapshotMapper.ScopeId);
            _presenter.PresentCard(in card);
        }

        private void SetLocalizedPrompts()
        {
            int count = _guidancePlan?.StepCount ?? 0;
            var titles = new MxUiLocalizedTextRequest[count];
            var prompts = new MxUiLocalizedTextRequest[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestGuidanceStepDefinition step = _guidancePlan.Steps[i];
                titles[i] = CreateTitlePrompt(_textProvider, step, i, count);
                prompts[i] = CreateActionPrompt(_textProvider, _bindingDisplay, step);
            }
            _snapshotMapper.SetStepTitlePrompts(titles);
            _snapshotMapper.SetStepActionPrompts(prompts);
        }

        private static MxUiLocalizedTextRequest CreateTitlePrompt(
            IMxUiTextProvider textProvider,
            CharacterTestGuidanceStepDefinition step,
            int stepIndex,
            int stepCount)
        {
            string semanticTitle = Resolve(
                textProvider,
                Text(step.Content.Title.Key, step.Content.Title.Fallback));
            string format = Resolve(
                textProvider,
                Text(
                    "ui.character_test.guidance.sequence_title.format",
                    "测试 {0}/{1}：{2}"));
            string fallback = Format(
                format,
                (stepIndex + 1).ToString(CultureInfo.InvariantCulture),
                stepCount.ToString(CultureInfo.InvariantCulture),
                semanticTitle);
            return Text(step.Content.Title.Key + ".resolved", fallback);
        }

        private static MxUiLocalizedTextRequest CreateActionPrompt(
            IMxUiTextProvider textProvider,
            InputBindingDisplayService bindingDisplay,
            CharacterTestGuidanceStepDefinition step)
        {
            string instruction = Resolve(
                textProvider,
                Text(
                    step.Content.ActionInstruction.Key,
                    step.Content.ActionInstruction.Fallback));
            string bindingLabel = string.Empty;
            bool hasBinding = bindingDisplay != null
                && bindingDisplay.TryGetDisplayString(step.InputActionId, 0, out bindingLabel)
                && !string.IsNullOrWhiteSpace(bindingLabel);
            string previousLabel = ResolveBindingLabel(
                bindingDisplay,
                "Gameplay/" + InputActionNames.GuidancePrevious,
                "[");
            string nextLabel = ResolveBindingLabel(
                bindingDisplay,
                "Gameplay/" + InputActionNames.GuidanceNext,
                "]");
            string format = Resolve(
                textProvider,
                hasBinding
                    ? Text(
                        "ui.character_test.guidance.action_prompt.format",
                        "按 {0}：{1}；{2} 上一项，{3} 下一项")
                    : Text(
                        "ui.character_test.guidance.action_prompt.no_binding.format",
                        "{0}；{1} 上一项，{2} 下一项"));
            string fallback = hasBinding
                ? Format(format, bindingLabel, instruction, previousLabel, nextLabel)
                : Format(format, instruction, previousLabel, nextLabel);
            return Text(step.Content.ActionInstruction.Key + ".binding", fallback);
        }

        private static string ResolveBindingLabel(
            InputBindingDisplayService bindingDisplay,
            string actionId,
            string fallback)
        {
            return bindingDisplay != null
                && bindingDisplay.TryGetDisplayString(actionId, 0, out string label)
                && !string.IsNullOrWhiteSpace(label)
                    ? label
                    : fallback;
        }

        private static string Resolve(
            IMxUiTextProvider textProvider,
            MxUiLocalizedTextRequest request)
        {
            return textProvider != null && textProvider.TryGetText(request, out string text)
                ? text
                : request.FallbackText ?? string.Empty;
        }

        private static string Format(string format, params object[] arguments)
        {
            try
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    format ?? string.Empty,
                    arguments ?? Array.Empty<object>());
            }
            catch (FormatException)
            {
                return format ?? string.Empty;
            }
        }

        private CharacterTestGuidanceObservationContext CaptureContext()
        {
            CharacterTestGuidanceObservation observation = CaptureObservation();
            IReadOnlyList<CharacterTestRuntimeEvent> events = _slice?.RecentRuntimeEvents;
            IReadOnlyList<DamageTelemetryRow> damageRows =
                _slice?.CombatTraceSystem?.DamageTelemetryRows;
            return new CharacterTestGuidanceObservationContext(
                events,
                damageRows,
                in observation);
        }

        private CharacterTestGuidanceObservation CaptureObservation()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _slice?.CharacterEntities;
            for (int i = 0; i < (entities?.Count ?? 0); i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;

                int horizontalSpeed = CharacterTestDeterministicMath.Magnitude(
                    entity.HorizontalVelocityXMillimetersPerSecond,
                    entity.HorizontalVelocityYMillimetersPerSecond);
                return new CharacterTestGuidanceObservation(
                    true,
                    entity.Id.Value,
                    entity.Transform.XMillimeters,
                    entity.Transform.ZMillimeters,
                    horizontalSpeed,
                    entity.IsGrounded,
                    _slice.CameraMode,
                    _slice.LockedTargetEntityId > 0);
            }

            return new CharacterTestGuidanceObservation(
                false,
                0,
                0,
                0,
                0,
                false,
                _slice != null ? _slice.CameraMode : default,
                _slice?.LockedTargetEntityId > 0);
        }

        private static MxUiGuidanceCardSnapshot CreateDevelopmentPreviewCard()
        {
            return new MxUiGuidanceCardSnapshot(
                DevelopmentPreviewCardScope,
                1L,
                true,
                Text(
                    "ui.character_test.guidance.preview.title",
                    "开发预览：边界接近引导"),
                new MxUiGuidanceLine(
                    MxUiGuidanceGlyph.Move,
                    MxUiGuidanceTone.Info,
                    Text("ui.guidance.label.action", "步骤："),
                    Text(
                        "ui.character_test.guidance.preview.action",
                        "从 ApproachStart 接近边界")),
                new MxUiGuidanceLine(
                    MxUiGuidanceGlyph.Focus,
                    MxUiGuidanceTone.Info,
                    Text("ui.guidance.label.focus", "观察："),
                    Text(
                        "ui.character_test.guidance.preview.focus",
                        "接触、停步、滑动与离地反馈")),
                new MxUiGuidanceLine(
                    MxUiGuidanceGlyph.Progress,
                    MxUiGuidanceTone.Neutral,
                    Text("ui.guidance.label.status", "状态："),
                    Text(
                        "ui.character_test.guidance.preview.status",
                        "开发预览，等待正式观察结果")));
        }

        private static MxUiLocalizedTextRequest Text(string key, string fallback)
        {
            return new MxUiLocalizedTextRequest(new MxUiTextKey(key), fallback);
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Player Guidance FairyGUI catalog diagnostics failed.";
            Warn(message);
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI.Guidance", _lastStatusMessage);
        }
    }
}
