using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestSkillPanelView
    {
        public const string RootName = "character-test-skill-panel";
        public const string SlotContainerName = "character-test-skill-panel-slots";
        public const string CandidateContainerName = "character-test-skill-panel-candidates";
        public const string MessageName = "character-test-skill-panel-message";

        private readonly List<Button> _slotButtons = new List<Button>(CharacterTestSkillSlotRuntime.SlotCount);
        private readonly List<Button> _candidateButtons = new List<Button>(8);
        private VisualElement _root;
        private Label _title;
        private Label _message;
        private VisualElement _slotContainer;
        private VisualElement _candidateContainer;
        private bool _eventsBound;

        public event Action<int> SlotClicked;
        public event Action<int> CandidateClicked;

        public bool IsBuiltFor(VisualElement root)
        {
            return root != null && _root != null && ReferenceEquals(_root.parent, root);
        }

        public void Build(VisualElement root)
        {
            if (root == null)
                return;

            root.Clear();
            _slotButtons.Clear();
            _candidateButtons.Clear();
            _root = new VisualElement { name = RootName };
            StyleRoot(_root);

            _title = new Label("临时技能栏");
            StyleTitle(_title);
            _root.Add(_title);

            _slotContainer = new VisualElement { name = SlotContainerName };
            StyleRow(_slotContainer);
            _root.Add(_slotContainer);

            for (int i = 0; i < CharacterTestSkillSlotRuntime.SlotCount; i++)
            {
                int slotIndex = i;
                var button = new Button(() => SlotClicked?.Invoke(slotIndex))
                {
                    name = "character-test-skill-slot-" + (i + 1)
                };
                StyleSlot(button);
                _slotButtons.Add(button);
                _slotContainer.Add(button);
            }

            _candidateContainer = new VisualElement { name = CandidateContainerName };
            StyleCandidateContainer(_candidateContainer);
            _root.Add(_candidateContainer);

            _message = new Label { name = MessageName };
            StyleMessage(_message);
            _root.Add(_message);

            root.Add(_root);
            _eventsBound = true;
        }

        public void Bind(CharacterTestSkillPanelSnapshot snapshot)
        {
            if (_root == null)
                return;

            snapshot ??= new CharacterTestSkillPanelSnapshot(
                Array.Empty<CharacterTestSkillSlotSnapshot>(),
                Array.Empty<CharacterTestSkillCandidate>(),
                0,
                string.Empty);

            for (int i = 0; i < _slotButtons.Count; i++)
            {
                CharacterTestSkillSlotSnapshot slot = i < snapshot.Slots.Count
                    ? snapshot.Slots[i]
                    : new CharacterTestSkillSlotSnapshot(i, 0, string.Empty, "空", 0, false, 0L);
                Button button = _slotButtons[i];
                button.text = FormatSlotText(slot);
                ApplySlotState(button, slot, i == snapshot.SelectedSlotIndex);
                button.tooltip = FormatSlotTooltip(slot);
            }

            RebuildCandidates(snapshot.Candidates);
            _message.text = string.IsNullOrWhiteSpace(snapshot.Message)
                ? "选择槽位后点击下方技能装备；点击已装备槽位或按数字键释放。"
                : snapshot.Message;
        }

        public void Release()
        {
            if (!_eventsBound)
                return;

            SlotClicked = null;
            CandidateClicked = null;
            _eventsBound = false;
        }

        private void RebuildCandidates(IReadOnlyList<CharacterTestSkillCandidate> candidates)
        {
            _candidateContainer.Clear();
            _candidateButtons.Clear();
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                CharacterTestSkillCandidate candidate = candidates[i];
                var button = new Button(() => CandidateClicked?.Invoke(candidate.InputIntentId))
                {
                    text = candidate.DisplayNameCn,
                    tooltip = candidate.DisplayName + "\n" + candidate.InputIntentId
                };
                StyleCandidate(button);
                _candidateButtons.Add(button);
                _candidateContainer.Add(button);
            }
        }

        private static string FormatSlotText(CharacterTestSkillSlotSnapshot slot)
        {
            string title = slot.IsEmpty ? "空" : slot.DisplayNameCn;
            string cooldown = slot.IsOnCooldown
                ? "\nCD " + slot.RemainingCooldownFrames.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : slot.IsEmpty ? "\n未装备" : "\n就绪";
            return (slot.SlotIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\n"
                + title
                + cooldown;
        }

        private static string FormatSlotTooltip(CharacterTestSkillSlotSnapshot slot)
        {
            if (slot.IsEmpty)
                return "空槽位。点击只会选中，不会释放技能。";

            return slot.DisplayNameCn
                + "\n输入意图: "
                + slot.InputIntentId
                + "\n"
                + (slot.IsOnCooldown ? "冷却剩余 " + slot.RemainingCooldownFrames + " 帧" : "点击或按数字键释放");
        }

        private static void ApplySlotState(Button button, CharacterTestSkillSlotSnapshot slot, bool selected)
        {
            button.style.borderTopColor = selected ? new Color(1f, 0.84f, 0.24f, 1f) : new Color(0.2f, 0.22f, 0.24f, 1f);
            button.style.borderBottomColor = button.style.borderTopColor.value;
            button.style.borderLeftColor = button.style.borderTopColor.value;
            button.style.borderRightColor = button.style.borderTopColor.value;
            button.style.backgroundColor = slot.IsOnCooldown
                ? new Color(0.16f, 0.16f, 0.18f, 0.9f)
                : slot.IsEmpty
                    ? new Color(0.09f, 0.1f, 0.11f, 0.72f)
                    : new Color(0.13f, 0.19f, 0.21f, 0.86f);
        }

        private static void StyleRoot(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.left = 24;
            root.style.right = 24;
            root.style.bottom = 18;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.backgroundColor = new Color(0.07f, 0.075f, 0.085f, 0.86f);
            root.style.borderTopLeftRadius = 6;
            root.style.borderTopRightRadius = 6;
            root.style.borderBottomLeftRadius = 6;
            root.style.borderBottomRightRadius = 6;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopColor = new Color(0.25f, 0.28f, 0.31f, 0.95f);
            root.style.borderBottomColor = root.style.borderTopColor.value;
            root.style.borderLeftColor = root.style.borderTopColor.value;
            root.style.borderRightColor = root.style.borderTopColor.value;
        }

        private static void StyleTitle(Label label)
        {
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            label.style.marginBottom = 5;
        }

        private static void StyleRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.NoWrap;
        }

        private static void StyleSlot(Button button)
        {
            button.style.flexGrow = 1;
            button.style.height = 58;
            button.style.marginRight = 5;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.paddingTop = 2;
            button.style.paddingBottom = 2;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.fontSize = 11;
            button.style.color = new Color(0.93f, 0.95f, 0.96f, 1f);
            button.style.borderTopWidth = 2;
            button.style.borderBottomWidth = 2;
            button.style.borderLeftWidth = 2;
            button.style.borderRightWidth = 2;
        }

        private static void StyleCandidateContainer(VisualElement container)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.marginTop = 7;
        }

        private static void StyleCandidate(Button button)
        {
            button.style.height = 28;
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 9;
            button.style.paddingRight = 9;
            button.style.fontSize = 12;
            button.style.backgroundColor = new Color(0.19f, 0.21f, 0.24f, 0.9f);
            button.style.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        }

        private static void StyleMessage(Label label)
        {
            label.style.marginTop = 2;
            label.style.fontSize = 11;
            label.style.color = new Color(0.78f, 0.82f, 0.86f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}
