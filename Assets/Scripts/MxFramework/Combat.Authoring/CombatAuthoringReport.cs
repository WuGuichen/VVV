using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Authoring
{
    public enum CombatAuthoringSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public enum CombatAuthoringQuickActionKind
    {
        None = 0,
        SelectAsset = 1,
        FocusSceneMarker = 2,
        CreatePreviewMarker = 3,
        RelinkSelectedTransform = 4,
        ClampFrameRange = 5,
        DisableInvalidShape = 6,
        CopyIssueReport = 7,
        FitTotalFrames = 8,
        SelectIssueTarget = 9,
    }

    public readonly struct CombatAuthoringIssue : IComparable<CombatAuthoringIssue>
    {
        public CombatAuthoringIssue(
            CombatAuthoringSeverity severity,
            string sourceAsset,
            string section,
            int trackId,
            CombatAuthoringFrameRange frameRange,
            string field,
            string message,
            string suggestedFix,
            CombatAuthoringQuickActionKind quickAction,
            int sourceOrder = 0)
        {
            Severity = severity;
            SourceAsset = sourceAsset ?? string.Empty;
            Section = section ?? string.Empty;
            TrackId = trackId;
            FrameRange = frameRange;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
            SuggestedFix = suggestedFix ?? string.Empty;
            QuickAction = quickAction;
            SourceOrder = sourceOrder;
        }

        public CombatAuthoringSeverity Severity { get; }

        public string SourceAsset { get; }

        public string Section { get; }

        public int TrackId { get; }

        public int Frame => FrameRange.IsEmpty ? -1 : FrameRange.StartFrame;

        public CombatAuthoringFrameRange FrameRange { get; }

        public string Field { get; }

        public string Message { get; }

        public string SuggestedFix { get; }

        public CombatAuthoringQuickActionKind QuickAction { get; }

        public int SourceOrder { get; }

        public int CompareTo(CombatAuthoringIssue other)
        {
            int compare = SourceAsset.CompareTo(other.SourceAsset);
            if (compare != 0)
            {
                return compare;
            }

            compare = Section.CompareTo(other.Section);
            if (compare != 0)
            {
                return compare;
            }

            compare = TrackId.CompareTo(other.TrackId);
            if (compare != 0)
            {
                return compare;
            }

            compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = Field.CompareTo(other.Field);
            if (compare != 0)
            {
                return compare;
            }

            return SourceOrder.CompareTo(other.SourceOrder);
        }
    }

    public readonly struct CombatAuthoringQueryRow : IComparable<CombatAuthoringQueryRow>
    {
        public CombatAuthoringQueryRow(
            int frame,
            int queryId,
            int entityId,
            int bodyId,
            int colliderId,
            int traceId,
            int actionId,
            int sourceOrder,
            string label = null)
        {
            Frame = frame;
            QueryId = queryId;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            TraceId = traceId;
            ActionId = actionId;
            SourceOrder = sourceOrder;
            Label = label ?? string.Empty;
        }

        public int Frame { get; }

        public int QueryId { get; }

        public int EntityId { get; }

        public int BodyId { get; }

        public int ColliderId { get; }

        public int TraceId { get; }

        public int ActionId { get; }

        public int SourceOrder { get; }

        public string Label { get; }

        public int CompareTo(CombatAuthoringQueryRow other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = EntityId.CompareTo(other.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = BodyId.CompareTo(other.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ColliderId.CompareTo(other.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = TraceId.CompareTo(other.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ActionId.CompareTo(other.ActionId);
            if (compare != 0)
            {
                return compare;
            }

            compare = SourceOrder.CompareTo(other.SourceOrder);
            if (compare != 0)
            {
                return compare;
            }

            return QueryId.CompareTo(other.QueryId);
        }
    }

    public sealed class CombatAuthoringReport
    {
        private readonly CombatAuthoringIssue[] _issues;
        private readonly CombatAuthoringQueryRow[] _queryRows;

        public CombatAuthoringReport(
            IEnumerable<CombatAuthoringIssue> issues,
            IEnumerable<CombatAuthoringQueryRow> queryRows = null)
        {
            _issues = ToSortedArray(issues);
            _queryRows = ToSortedArray(queryRows);
        }

        public int IssueCount => _issues.Length;

        public int QueryRowCount => _queryRows.Length;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Length; i++)
                {
                    if (_issues[i].Severity == CombatAuthoringSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public CombatAuthoringIssue GetIssue(int index)
        {
            return _issues[index];
        }

        public CombatAuthoringQueryRow GetQueryRow(int index)
        {
            return _queryRows[index];
        }

        private static T[] ToSortedArray<T>(IEnumerable<T> values)
            where T : IComparable<T>
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            var list = new List<T>(values);
            list.Sort();
            return list.ToArray();
        }
    }
}
