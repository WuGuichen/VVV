using System;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(111)]
    [AddComponentMenu("MxFramework/Game/Character Test/Temporary Skill Panel")]
    public sealed class CharacterTestSkillPanel : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private float _refreshIntervalSeconds = 0.1f;

        private readonly CharacterTestSkillPanelView _view = new CharacterTestSkillPanelView();
        private CharacterTestSliceServices _services;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private int _selectedSlotIndex;
        private float _nextRefreshTime;
        private bool _viewEventsSubscribed;
        private string _lastMessage = string.Empty;

        public int SelectedSlotIndex => _selectedSlotIndex;
        public string LastMessage => _lastMessage;

        public void Configure(
            GameSlice slice,
            PanelSettings panelSettings = null,
            IRuntimeLogger logger = null)
        {
            Configure(slice?.Services, panelSettings, logger);
        }

        internal void Configure(
            CharacterTestSliceServices services,
            PanelSettings panelSettings = null,
            IRuntimeLogger logger = null)
        {
            _services = services;
            _logger = logger ?? _logger ?? NullRuntimeLogger.Instance;
            if (panelSettings != null)
                _panelSettings = panelSettings;

            _selectedSlotIndex = CharacterTestSkillSlotRuntime.ClampSlotIndex(_selectedSlotIndex);
            enabled = _services != null && _showOnStart;
            EnsureDocument();
            RefreshNow();
        }

        public void Release()
        {
            UnsubscribeViewEvents();
            _services = null;
            enabled = false;
            if (_document != null && _document.rootVisualElement != null)
                _document.rootVisualElement.Clear();
        }

        public void RefreshNow()
        {
            EnsureDocument();
            if (_services == null || _services.Modules.SkillSlots == null)
            {
                _view.Bind(new CharacterTestSkillPanelSnapshot(
                    Array.Empty<CharacterTestSkillSlotSnapshot>(),
                    Array.Empty<CharacterTestSkillCandidate>(),
                    _selectedSlotIndex,
                    "技能栏未连接 CharacterTest runtime。"));
                return;
            }

            CharacterTestCharacterEntity localPlayer = FindLocalPlayer(_services);
            int casterEntityId = localPlayer != null ? localPlayer.Id.Value : 0;
            CharacterTestSkillPanelSnapshot snapshot = _services.Modules.SkillSlots.CreateSnapshot(
                _services.Modules.AbilityRuntime,
                casterEntityId,
                _services.CurrentFrame,
                _selectedSlotIndex);
            if (!string.IsNullOrWhiteSpace(_lastMessage))
            {
                snapshot = new CharacterTestSkillPanelSnapshot(
                    snapshot.Slots,
                    snapshot.Candidates,
                    snapshot.SelectedSlotIndex,
                    _lastMessage);
            }

            _view.Bind(snapshot);
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshIntervalSeconds);
        }

        private void Awake()
        {
            EnsureDocument();
        }

        private void OnEnable()
        {
            EnsureDocument();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnsubscribeViewEvents();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Update()
        {
            if (_services == null)
                return;

            if (Time.unscaledTime >= _nextRefreshTime)
                RefreshNow();
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = ResolvePanelSettings();

            VisualElement root = _document.rootVisualElement;
            if (root == null)
                return;

            if (_view.IsBuiltFor(root))
            {
                SubscribeViewEvents();
                return;
            }

            _view.Build(root);
            SubscribeViewEvents();
        }

        private void SubscribeViewEvents()
        {
            if (_viewEventsSubscribed)
                return;

            _view.SlotClicked += OnSlotClicked;
            _view.CandidateClicked += OnCandidateClicked;
            _viewEventsSubscribed = true;
        }

        private void UnsubscribeViewEvents()
        {
            if (!_viewEventsSubscribed)
                return;

            _view.SlotClicked -= OnSlotClicked;
            _view.CandidateClicked -= OnCandidateClicked;
            _view.Release();
            _viewEventsSubscribed = false;
        }

        private void OnSlotClicked(int slotIndex)
        {
            _selectedSlotIndex = CharacterTestSkillSlotRuntime.ClampSlotIndex(slotIndex);
            if (_services == null || _services.Modules.SkillSlots == null)
            {
                _lastMessage = "技能栏未连接 CharacterTest runtime。";
                RefreshNow();
                return;
            }

            if (!_services.Modules.SkillSlots.TryResolveSlot(
                    _selectedSlotIndex,
                    out CharacterTestSkillSlotBinding binding)
                || binding.InputIntentId <= 0)
            {
                _lastMessage = "已选中空槽位 " + (_selectedSlotIndex + 1) + "。";
                RefreshNow();
                return;
            }

            RuntimeCommandValidationResult result = _services.Commands.EnqueueSkillSlotInput(_selectedSlotIndex);
            _lastMessage = result.Success
                ? "释放 " + binding.DisplayNameCn + "。"
                : "释放失败：" + result.Error.Message;
            RefreshNow();
        }

        private void OnCandidateClicked(int inputIntentId)
        {
            if (_services == null || _services.Modules.SkillSlots == null)
            {
                _lastMessage = "技能栏未连接 CharacterTest runtime。";
                RefreshNow();
                return;
            }

            if (_services.Modules.SkillSlots.TryEquip(_selectedSlotIndex, inputIntentId, out string message))
                _lastMessage = message;
            else
                _lastMessage = "装备失败：" + message;
            RefreshNow();
        }

        private PanelSettings ResolvePanelSettings()
        {
            if (_panelSettings != null)
                return _panelSettings;

            _logger?.Warning(
                "CharacterTest.SkillPanel",
                "Skill panel has no configured PanelSettings. Falling back to a temporary runtime PanelSettings.");
            return ScriptableObject.CreateInstance<PanelSettings>();
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(CharacterTestSliceServices services)
        {
            if (services == null)
                return null;

            for (int i = 0; i < services.Modules.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = services.Modules.CharacterEntities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }
    }
}
