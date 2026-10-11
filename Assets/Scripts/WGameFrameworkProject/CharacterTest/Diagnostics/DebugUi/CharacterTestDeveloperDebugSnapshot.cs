using System;
using System.Collections.Generic;
using MxFramework.DebugUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestDeveloperDebugSnapshot
    {
        public static readonly string[] TabNames =
        {
            "Status",
            "Characters",
            "Movables",
            "Camera/Input",
            "Commands",
            "Story",
            "Resources"
        };

        public CharacterTestDeveloperDebugSnapshot(
            bool isAvailable,
            long frame,
            bool refreshPaused,
            int issueCount,
            string localPlayerSummary,
            string actionSummary,
            string targetLockSummary,
            bool hasCameraBridge,
            bool cursorCaptured,
            IReadOnlyList<CharacterTestDeveloperDebugTabSnapshot> tabs)
        {
            IsAvailable = isAvailable;
            Frame = frame;
            RefreshPaused = refreshPaused;
            IssueCount = issueCount;
            LocalPlayerSummary = localPlayerSummary ?? string.Empty;
            ActionSummary = actionSummary ?? string.Empty;
            TargetLockSummary = targetLockSummary ?? string.Empty;
            HasCameraBridge = hasCameraBridge;
            CursorCaptured = cursorCaptured;
            Tabs = CopyTabs(tabs);
        }

        public bool IsAvailable { get; }
        public long Frame { get; }
        public bool RefreshPaused { get; }
        public int IssueCount { get; }
        public string LocalPlayerSummary { get; }
        public string ActionSummary { get; }
        public string TargetLockSummary { get; }
        public bool HasCameraBridge { get; }
        public bool CursorCaptured { get; }
        public IReadOnlyList<CharacterTestDeveloperDebugTabSnapshot> Tabs { get; }
        public string MouseSummary => HasCameraBridge
            ? CursorCaptured ? "captured" : "free"
            : "n/a";
        public string CollapsedSummary => FormatCollapsedSummary(0);

        public string FormatCollapsedSummary(int activeTab)
        {
            if (!IsAvailable)
                return "Developer debug unavailable";

            activeTab = ClampTabIndex(activeTab);
            return "Issues " + IssueCount
                + " | f" + Frame
                + " | " + TabNames[activeTab]
                + " | player " + EmptyAsDash(LocalPlayerSummary)
                + " | action " + EmptyAsDash(ActionSummary)
                + " | target " + EmptyAsDash(TargetLockSummary)
                + " | mouse " + MouseSummary;
        }

        public static CharacterTestDeveloperDebugSnapshot Unavailable(bool refreshPaused)
        {
            return new CharacterTestDeveloperDebugSnapshot(
                false,
                0L,
                refreshPaused,
                0,
                "n/a",
                "n/a",
                "n/a",
                false,
                false,
                new[]
                {
                    new CharacterTestDeveloperDebugTabSnapshot(
                        TabNames[0],
                        new[]
                        {
                            CharacterTestDeveloperDebugCard.StatusGrid(
                                "Status",
                                new[] { CharacterTestDeveloperDebugLine.StatusChip("slice", "unavailable", true) })
                        })
                });
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        public static int ClampTabIndex(int index)
        {
            if (index < 0)
                return 0;
            if (index >= TabNames.Length)
                return TabNames.Length - 1;
            return index;
        }

        private static CharacterTestDeveloperDebugTabSnapshot[] CopyTabs(
            IReadOnlyList<CharacterTestDeveloperDebugTabSnapshot> tabs)
        {
            if (tabs == null || tabs.Count == 0)
                return Array.Empty<CharacterTestDeveloperDebugTabSnapshot>();

            var copy = new CharacterTestDeveloperDebugTabSnapshot[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
                copy[i] = tabs[i];
            return copy;
        }
    }

    public sealed class CharacterTestDeveloperDebugTabSnapshot
    {
        private readonly CharacterTestDeveloperDebugCard[] _cards;

        public CharacterTestDeveloperDebugTabSnapshot(
            string name,
            IReadOnlyList<CharacterTestDeveloperDebugCard> cards)
        {
            Name = name ?? string.Empty;
            _cards = CopyCards(cards);
        }

        public string Name { get; }
        public IReadOnlyList<CharacterTestDeveloperDebugCard> Cards => _cards;

        private static CharacterTestDeveloperDebugCard[] CopyCards(IReadOnlyList<CharacterTestDeveloperDebugCard> cards)
        {
            if (cards == null || cards.Count == 0)
                return Array.Empty<CharacterTestDeveloperDebugCard>();

            var copy = new CharacterTestDeveloperDebugCard[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                copy[i] = cards[i];
            return copy;
        }
    }

    public enum CharacterTestDeveloperDebugCardLayout
    {
        List = 0,
        MetricGrid = 1,
        StatusGrid = 2,
        FlowPipeline = 3,
        Log = 4,
        Entity = 5,
        InputPanel = 6
    }

    public sealed class CharacterTestDeveloperDebugCard
    {
        private readonly CharacterTestDeveloperDebugLine[] _lines;

        public CharacterTestDeveloperDebugCard(
            string title,
            IReadOnlyList<CharacterTestDeveloperDebugLine> lines,
            CharacterTestDeveloperDebugCardLayout layout = CharacterTestDeveloperDebugCardLayout.List)
        {
            Title = title ?? string.Empty;
            Layout = layout;
            _lines = CopyLines(lines);
        }

        public string Title { get; }
        public CharacterTestDeveloperDebugCardLayout Layout { get; }
        public IReadOnlyList<CharacterTestDeveloperDebugLine> Lines => _lines;

        public static CharacterTestDeveloperDebugCard FromRows(
            string title,
            IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.List);
        }

        public static CharacterTestDeveloperDebugCard MetricGrid(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.MetricGrid);
        }

        public static CharacterTestDeveloperDebugCard StatusGrid(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.StatusGrid);
        }

        public static CharacterTestDeveloperDebugCard FlowPipeline(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.FlowPipeline);
        }

        public static CharacterTestDeveloperDebugCard Log(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.Log);
        }

        public static CharacterTestDeveloperDebugCard Entity(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.Entity);
        }

        public static CharacterTestDeveloperDebugCard InputPanel(string title, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            return new CharacterTestDeveloperDebugCard(title, lines, CharacterTestDeveloperDebugCardLayout.InputPanel);
        }

        private static CharacterTestDeveloperDebugLine[] CopyLines(IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            if (lines == null || lines.Count == 0)
                return Array.Empty<CharacterTestDeveloperDebugLine>();

            var copy = new CharacterTestDeveloperDebugLine[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                copy[i] = lines[i];
            return copy;
        }
    }

    public readonly struct CharacterTestDeveloperDebugLine
    {
        private CharacterTestDeveloperDebugLine(
            CharacterTestDeveloperDebugLineKind kind,
            string name,
            string value,
            bool warning,
            float number0,
            float number1,
            bool flag)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            Warning = warning;
            Number0 = number0;
            Number1 = number1;
            Flag = flag;
        }

        public CharacterTestDeveloperDebugLineKind Kind { get; }
        public string Name { get; }
        public string Value { get; }
        public bool Warning { get; }
        public float Number0 { get; }
        public float Number1 { get; }
        public bool Flag { get; }

        public static CharacterTestDeveloperDebugLine Row(string name, string value, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Row,
                name,
                value,
                warning,
                0f,
                0f,
                false);
        }

        public static CharacterTestDeveloperDebugLine Text(string value, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Text,
                string.Empty,
                value,
                warning,
                0f,
                0f,
                false);
        }

        public static CharacterTestDeveloperDebugLine Metric(string label, string value, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Metric,
                label,
                value,
                warning,
                0f,
                0f,
                false);
        }

        public static CharacterTestDeveloperDebugLine StatusChip(string label, string value, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.StatusChip,
                label,
                value,
                warning,
                0f,
                0f,
                false);
        }

        public static CharacterTestDeveloperDebugLine Bool(string label, bool on)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Bool,
                label,
                on ? "ON" : "OFF",
                !on,
                0f,
                0f,
                on);
        }

        public static CharacterTestDeveloperDebugLine Vector2(string label, float x, float y, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Vector2,
                label,
                "(" + x.ToString("F2") + ", " + y.ToString("F2") + ")",
                warning,
                x,
                y,
                false);
        }

        public static CharacterTestDeveloperDebugLine Progress(string label, float current, float max, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Progress,
                label,
                current.ToString("F0") + " / " + max.ToString("F0"),
                warning,
                current,
                max,
                false);
        }

        public static CharacterTestDeveloperDebugLine LogEntry(string message, bool warning = false)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.Log,
                string.Empty,
                message,
                warning,
                0f,
                0f,
                false);
        }

        public static CharacterTestDeveloperDebugLine FlowStep(string step, string detail, bool ok)
        {
            return new CharacterTestDeveloperDebugLine(
                CharacterTestDeveloperDebugLineKind.FlowStep,
                step,
                detail,
                !ok,
                0f,
                0f,
                ok);
        }
    }

    public enum CharacterTestDeveloperDebugLineKind
    {
        Row = 0,
        Text = 1,
        Metric = 2,
        StatusChip = 3,
        Bool = 4,
        Vector2 = 5,
        Progress = 6,
        Log = 7,
        FlowStep = 8
    }
}
