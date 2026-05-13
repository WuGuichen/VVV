using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.CombatAnimation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Combat Animation HUD Controller")]
    public sealed class CombatAnimationHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;

        private Label _playerAction;
        private Label _playerPhase;
        private Label _playerFrame;
        private Label _playerHp;
        private Label _dummyHp;
        private Label _weaponTrace;
        private Label _instructions;
        private VisualElement _eventList;
        private bool _built;

        public void ConfigureAssets(UIDocument document, VisualTreeAsset visualTree, StyleSheet styleSheet)
        {
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        public void Refresh(CombatAnimationHudModel model)
        {
            EnsureBuilt();
            if (!_built)
            {
                return;
            }

            SetText(_playerAction, model.PlayerAction);
            SetText(_playerPhase, model.PlayerPhase);
            SetText(_playerFrame, model.PlayerLocalFrame);
            SetText(_playerHp, model.PlayerHp);
            SetText(_dummyHp, model.DummyHp);
            SetText(_weaponTrace, model.WeaponTrace);
            SetText(_instructions, model.Instructions);
            RefreshEvents(model.RecentEvents);
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _document = _document != null ? _document : GetComponent<UIDocument>();
            if (_document == null)
            {
                return;
            }

            if (_visualTree != null)
            {
                _document.visualTreeAsset = _visualTree;
            }

            VisualElement root = _document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            if (_styleSheet != null && !root.styleSheets.Contains(_styleSheet))
            {
                root.styleSheets.Add(_styleSheet);
            }

            _playerAction = root.Q<Label>("player-action");
            _playerPhase = root.Q<Label>("player-phase");
            _playerFrame = root.Q<Label>("player-frame");
            _playerHp = root.Q<Label>("player-hp");
            _dummyHp = root.Q<Label>("dummy-hp");
            _weaponTrace = root.Q<Label>("weapon-trace");
            _instructions = root.Q<Label>("instructions");
            _eventList = root.Q<VisualElement>("event-list");
            _built = _playerAction != null && _eventList != null;
        }

        private void RefreshEvents(IReadOnlyList<string> events)
        {
            _eventList.Clear();
            if (events == null || events.Count == 0)
            {
                _eventList.Add(new Label("No hit events yet.") { name = "event-empty" });
                return;
            }

            int start = Mathf.Max(0, events.Count - 5);
            for (int i = start; i < events.Count; i++)
            {
                _eventList.Add(new Label(events[i]) { name = "event-row" });
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(text) ? "-" : text;
            }
        }
    }

    public sealed class CombatAnimationHudModel
    {
        public string PlayerAction { get; set; }
        public string PlayerPhase { get; set; }
        public string PlayerLocalFrame { get; set; }
        public string PlayerHp { get; set; }
        public string DummyHp { get; set; }
        public string WeaponTrace { get; set; }
        public string Instructions { get; set; }
        public IReadOnlyList<string> RecentEvents { get; set; }
    }
}
