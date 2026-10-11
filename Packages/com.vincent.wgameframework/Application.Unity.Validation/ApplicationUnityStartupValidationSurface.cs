using System;
using MxFramework.ApplicationStartup.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.ApplicationStartup.Unity.Validation
{
    /// <summary>Presentation-only UI Toolkit adapter for the Application Unity startup surface.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationUnityStartupValidationSurface : MonoBehaviour, IApplicationUnityStartupSurface
    {
        private const int DefaultRequestCapacity = 8;

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private int _requestCapacity = DefaultRequestCapacity;

        private ApplicationUnityStartupRequestQueue _requests;
        private VisualElement _root;
        private Label _state;
        private Label _attempt;
        private Label _plan;
        private Label _run;
        private Label _phase;
        private Label _step;
        private Label _progress;
        private Label _error;
        private Label _live;
        private Label _lifecycle;
        private Label _retirement;
        private Button _start;
        private Button _retry;
        private Button _cancel;
        private bool _isBound;
        private bool _hasAppliedModel;
        private ApplicationUnityStartupSurfaceModel _lastModel;

        internal long DroppedRequestCount => _requests == null ? 0L : _requests.DroppedRequestCount;

        internal bool TryBindForTests(VisualElement documentRoot, int requestCapacity)
        {
            if (_isBound || documentRoot == null || requestCapacity <= 0 || !TryBindTree(documentRoot))
                return false;

            _requests = new ApplicationUnityStartupRequestQueue(requestCapacity);
            Subscribe();
            _hasAppliedModel = false;
            _isBound = true;
            return true;
        }

        internal void UnbindForTests() => OnDisable();

        private void OnEnable()
        {
            if (_isBound)
                return;

            if (!TryBindSerializedTree())
            {
                enabled = false;
                return;
            }

            _requests = new ApplicationUnityStartupRequestQueue(_requestCapacity);
            Subscribe();
            _hasAppliedModel = false;
            _isBound = true;
        }

        private void OnDisable()
        {
            if (!_isBound)
                return;

            Unsubscribe();
            _hasAppliedModel = false;
            _isBound = false;
        }

        /// <inheritdoc />
        /// <remarks>NoAlloc after OnEnable initialization.</remarks>
        public bool TryDequeueRequest(out ApplicationUnityStartupSurfaceRequest request)
        {
            if (!_isBound)
            {
                request = default;
                return false;
            }

            return _requests.TryDequeue(out request);
        }

        /// <inheritdoc />
        /// <remarks>AllocByDesign only when a displayed state value changes.</remarks>
        public void Apply(in ApplicationUnityStartupSurfaceModel model)
        {
            if (!_isBound)
                return;

            if (_hasAppliedModel && ModelEquals(in _lastModel, in model))
                return;

            StartupDisplayState displayState = ResolveDisplayState(in model);
            SetTextIfChanged(_state, DisplayText(displayState));
            SetDisplayStateClass(displayState);
            SetTextIfChanged(_attempt, "Attempt " + model.Attempt);
            SetTextIfChanged(_plan, "Plan " + (model.PlanId.IsValid ? model.PlanId.Value : "-"));
            SetTextIfChanged(_run, "Run " + (string.IsNullOrEmpty(model.RunId) ? "-" : model.RunId));
            SetTextIfChanged(_phase, "Phase " + model.Phase);
            SetTextIfChanged(_step, "Step " + (model.StepId.IsValid ? model.StepId.Value : "-"));
            SetTextIfChanged(_progress, "Forward " + FormatPercent(model.ForwardProgress) + "  Rollback " + FormatPercent(model.RollbackProgress));
            SetTextIfChanged(_error, FormatError(in model));
            SetTextIfChanged(_live, "Live revision " + model.LiveRevision + "  session " + (model.HasRuntimeSession ? "yes" : "no") + "  frame " + model.LiveFrameStatus);
            SetTextIfChanged(_lifecycle, "Lifecycle " + model.LifecyclePumpStatus + "  checkpoint " + model.LifecycleCheckpointStatus);
            SetTextIfChanged(_retirement, "Retirement occupied " + model.OccupiedRetirementCount + "  failed " + model.FailedRetirementCount + FormatDebt(in model));

            SetEnabledIfChanged(_start, model.CanStart);
            SetEnabledIfChanged(_retry, model.CanRetry);
            SetEnabledIfChanged(_cancel, model.CanCancel);
            _lastModel = model;
            _hasAppliedModel = true;
        }

        private bool TryBindSerializedTree()
        {
            if (_document == null || _panelSettings == null || _document.visualTreeAsset == null ||
                _requestCapacity <= 0)
            {
                Debug.LogError("Application startup Validation surface requires an explicit UIDocument with VisualTreeAsset, exact PanelSettings, and positive request capacity.", this);
                return false;
            }

            _document.panelSettings = _panelSettings;
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return false;

            return TryBindTree(documentRoot);
        }

        private bool TryBindTree(VisualElement documentRoot)
        {
            _root = documentRoot.Q<VisualElement>(ApplicationUnityStartupValidationElementNames.Root);
            _state = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.State);
            _attempt = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Attempt);
            _plan = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Plan);
            _run = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Run);
            _phase = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Phase);
            _step = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Step);
            _progress = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Progress);
            _error = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error);
            _live = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Live);
            _lifecycle = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Lifecycle);
            _retirement = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Retirement);
            _start = documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Start);
            _retry = documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry);
            _cancel = documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Cancel);

            if (_root != null && _state != null && _attempt != null && _plan != null && _run != null &&
                _phase != null && _step != null && _progress != null && _error != null && _live != null &&
                _lifecycle != null && _retirement != null && _start != null && _retry != null && _cancel != null)
                return true;

            Debug.LogError("Application startup Validation UXML is missing one or more required named elements.", this);
            return false;
        }

        private void Subscribe()
        {
            _start.clicked += OnStartClicked;
            _retry.clicked += OnRetryClicked;
            _cancel.clicked += OnCancelClicked;
        }

        private void Unsubscribe()
        {
            _start.clicked -= OnStartClicked;
            _retry.clicked -= OnRetryClicked;
            _cancel.clicked -= OnCancelClicked;
        }

        private void OnStartClicked() => TryEnqueueFromUi(ApplicationUnityStartupRequestKind.Start);
        private void OnRetryClicked() => TryEnqueueFromUi(ApplicationUnityStartupRequestKind.Retry);
        private void OnCancelClicked() => TryEnqueueFromUi(ApplicationUnityStartupRequestKind.Cancel);

        private void TryEnqueueFromUi(ApplicationUnityStartupRequestKind kind)
        {
            if (!_requests.TryEnqueue(kind))
                SetTextIfChanged(_error, "Surface request rejected; dropped " + _requests.DroppedRequestCount);
        }

        private static StartupDisplayState ResolveDisplayState(in ApplicationUnityStartupSurfaceModel model)
        {
            if (model.RootState == ApplicationUnityBootRootState.Failed || model.RootState == ApplicationUnityBootRootState.ShutdownDebt ||
                model.StartupState == ApplicationStartupState.Failed || model.StartupState == ApplicationStartupState.Cancelled ||
                model.StartupState == ApplicationStartupState.TimedOut || model.StartupState == ApplicationStartupState.RollbackFailed)
                return StartupDisplayState.Error;

            if (model.StartupState == ApplicationStartupState.RollingBack)
                return StartupDisplayState.RollingBack;

            if (model.StartupState == ApplicationStartupState.Succeeded || (model.RootState == ApplicationUnityBootRootState.Ready && model.HasRuntimeSession))
                return StartupDisplayState.Ready;

            if (model.StartupState == ApplicationStartupState.Running || model.StartupState == ApplicationStartupState.Draining || model.RootState == ApplicationUnityBootRootState.Running)
                return StartupDisplayState.Loading;

            return StartupDisplayState.Boot;
        }

        private static string DisplayText(StartupDisplayState state)
        {
            switch (state)
            {
                case StartupDisplayState.Loading: return "Loading";
                case StartupDisplayState.RollingBack: return "Rolling Back";
                case StartupDisplayState.Error: return "Error";
                case StartupDisplayState.Ready: return "Ready";
                default: return "Boot";
            }
        }

        private void SetDisplayStateClass(StartupDisplayState state)
        {
            _root.EnableInClassList("application-boot--boot", state == StartupDisplayState.Boot);
            _root.EnableInClassList("application-boot--loading", state == StartupDisplayState.Loading);
            _root.EnableInClassList("application-boot--rolling-back", state == StartupDisplayState.RollingBack);
            _root.EnableInClassList("application-boot--error", state == StartupDisplayState.Error);
            _root.EnableInClassList("application-boot--ready", state == StartupDisplayState.Ready);
        }

        private static string FormatError(in ApplicationUnityStartupSurfaceModel model)
        {
            if (model.RootError == ApplicationUnityBootErrorCode.None &&
                model.ApplicationErrorCode == ApplicationStartupErrorCode.None &&
                string.IsNullOrEmpty(model.RuntimeErrorCode) &&
                string.IsNullOrEmpty(model.CreationStableErrorCode))
                return model.CancelRequested ? "Cancellation requested" : "No error";

            return "Root " + model.RootError + "  Application " + model.ApplicationErrorCode +
                "  Runtime " + model.RuntimeErrorCode + "  Creation " + model.CreationStableErrorCode;
        }

        private static string FormatDebt(in ApplicationUnityStartupSurfaceModel model)
        {
            if (string.IsNullOrEmpty(model.OwnershipDebtDomainId) && string.IsNullOrEmpty(model.OwnershipDebtStableCode))
                return string.Empty;

            return "  debt " + model.OwnershipDebtDomainId + "/" + model.OwnershipDebtStableCode;
        }

        private static string FormatPercent(float value) => (Mathf.Clamp01(value) * 100f).ToString("0.#") + "%";

        private static void SetTextIfChanged(Label label, string value)
        {
            if (!string.Equals(label.text, value, StringComparison.Ordinal))
                label.text = value;
        }

        private static void SetEnabledIfChanged(VisualElement element, bool enabledValue)
        {
            if (element.enabledSelf != enabledValue)
                element.SetEnabled(enabledValue);
        }

        private static bool ModelEquals(in ApplicationUnityStartupSurfaceModel left, in ApplicationUnityStartupSurfaceModel right) =>
            left.RootState == right.RootState && left.RootError == right.RootError && left.Attempt == right.Attempt &&
            left.PlanId == right.PlanId && string.Equals(left.RunId, right.RunId, StringComparison.Ordinal) &&
            left.StartupState == right.StartupState && left.Phase == right.Phase && left.StepId == right.StepId &&
            left.ForwardProgress.Equals(right.ForwardProgress) && left.RollbackProgress.Equals(right.RollbackProgress) &&
            left.CancelRequested == right.CancelRequested && left.ApplicationErrorCode == right.ApplicationErrorCode &&
            string.Equals(left.RuntimeErrorCode, right.RuntimeErrorCode, StringComparison.Ordinal) &&
            left.LiveRevision == right.LiveRevision && left.HasRuntimeSession == right.HasRuntimeSession &&
            left.LiveFrameStatus == right.LiveFrameStatus && left.LifecyclePumpStatus == right.LifecyclePumpStatus &&
            left.LifecycleCheckpointStatus == right.LifecycleCheckpointStatus &&
            left.OccupiedRetirementCount == right.OccupiedRetirementCount && left.FailedRetirementCount == right.FailedRetirementCount &&
            string.Equals(left.OwnershipDebtDomainId, right.OwnershipDebtDomainId, StringComparison.Ordinal) &&
            string.Equals(left.OwnershipDebtStableCode, right.OwnershipDebtStableCode, StringComparison.Ordinal) &&
            string.Equals(left.CreationStableErrorCode, right.CreationStableErrorCode, StringComparison.Ordinal);

        private enum StartupDisplayState
        {
            Boot,
            Loading,
            RollingBack,
            Error,
            Ready
        }
    }
}
