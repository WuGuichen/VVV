using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns RuntimeFrame event detail presentation, selection, paging and navigation.</summary>
    /// <remarks>NoAlloc after stable FairyGUI listeners are bound.</remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder
    {
        private const float DetailWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth;
        private const float DefaultHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        private const float NormalHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        private const float ClusterHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        private const float AggregateHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        private const float DecisionContentY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY;
        internal const int VisibleMemberCount = 5;
        private const int TitleFontSize = 26;
        private const int MetaFontSize = 15;
        private const int FrameFontSize = 24;
        private const int SummaryFontSize = 18;
        private const int SecondaryFontSize = 18;
        private const int EvidenceFontSize = 18;
        private const int ParticipantsFontSize = 16;
        private const int ButtonFontSize = 17;
        private const int HintFontSize = 14;
        private const float CausalCardWidth = 464f;
        private const float CausalCardHeight = 88f;
        private const float CausalIconSize = 52f;
        private const int CausalTitleFontSize = 18;
        private const int CausalValueFontSize = 16;

        private static readonly string[] TrackIconResourceNames =
        {
            "timeline_track_perception_64x64",
            "timeline_track_decision_64x64",
            "timeline_track_action_64x64",
            "timeline_track_result_64x64",
            "timeline_track_warning_64x64"
        };

        private static readonly string[] DecisionFieldNames =
        {
            "planActionBackground", "planActionTitle",
            "planActionStartLabel", "planActionStartValue",
            "planActionDurationLabel", "planActionDurationValue",
            "planActionCompleteLabel", "planActionCompleteValue",
            "planActionFailureLabel", "planActionFailureValue"
        };

        private readonly ICharacterTestRuntimeAiLabTimelineViewportIntentSink _intentSink;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private readonly Action<CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel, int>
            _selectCausalItem;

        internal int ClusterMemberOffset { get; private set; }
        internal Fgui.GComponent Details { get; private set; }
        internal CharacterTestRuntimeAiLabTimelineItem Item { get; private set; }
        internal CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel Workspace { get; private set; }
        private bool InteractionsBound { get; set; }

        internal CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder(
            ICharacterTestRuntimeAiLabTimelineViewportIntentSink intentSink,
            Action<Fgui.GObject, string> bindTooltip,
            Action<CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel, int> selectCausalItem)
        {
            _intentSink = intentSink;
            _bindTooltip = bindTooltip;
            _selectCausalItem = selectCausalItem;
        }

        internal void Bind(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent details = timeline?.GetChild("timelineEventDetails")?.asCom;
            CharacterTestRuntimeAiLabTimelineItem item = viewModel?.TimelineSelectedItem;
            if (details == null)
                return;
            Workspace = viewModel.RuntimeFrame;
            details.visible = true;
            SetDecisionFieldsVisible(details, false);
            if (item == null)
            {
                Item = null;
                ClusterMemberOffset = 0;
                if (details.scrollPane != null)
                    details.scrollPane.SetPosY(0f, false);
                ApplyTimelineDetailGeometry(details, null);
                SetTimelineEventInspectorChromeVisible(details, false);
                BindText(details, "detailTitle", "当前运行决策");
                BindText(
                    details,
                    "detailMeta",
                    viewModel.RuntimeFrame?.ContextText ?? string.Empty);
                BindText(details, "detailFrame", string.Empty);
                BindText(details, "detailEntities", string.Empty);
                BindText(details, "detailTrace", string.Empty);
                BindText(details, "detailEvidence", string.Empty);
                BindText(details, "detailParticipants", string.Empty);
                SetTimelineChildVisible(details, "detailMeta", false);
                SetTimelineChildVisible(details, "detailFrame", false);
                SetTimelineChildVisible(details, "detailEntities", false);
                SetTimelineChildVisible(details, "detailTrace", false);
                SetTimelineChildVisible(details, "detailEvidence", false);
                SetTimelineChildVisible(details, "detailParticipants", false);
                SetTimelineChildVisible(details, "memberScrollHint", false);
                SetTimelineChildVisible(details, "btnLocateStart", false);
                SetTimelineChildVisible(details, "btnLocateEnd", false);
                SetTimelineChildVisible(details, "btnCloseDetail", false);
                RenderRuntimeFrameCausalSummary(details, viewModel, compact: true);
                return;
            }

            if (Item == null
                || !string.Equals(Item.StableKey, item.StableKey, StringComparison.Ordinal))
            {
                ClusterMemberOffset = 0;
                if (details.scrollPane != null)
                    details.scrollPane.SetPosY(0f, false);
            }
            Details = details;
            Item = item;
            EnsureInteractions(details);
            SetRuntimeFrameCausalSummaryVisible(details, false);
            ApplyTimelineDetailGeometry(details, item);
            SetTimelineEventInspectorChromeVisible(details, true);
            SetTimelineChildVisible(details, "detailMeta", true);
            SetTimelineChildVisible(details, "btnCloseDetail", true);

            bool aggregate = item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack
                || item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate;
            bool exactStack = item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack;
            CharacterTestRuntimeAiLabTimelineDetailViewModel semanticDetail =
                ResolveTimelineDetail(item);
            BindText(details, "detailTitle", aggregate
                ? "事件聚合（" + item.Count.ToString(CultureInfo.InvariantCulture) + "）"
                : semanticDetail?.Title ?? "时间轴事件");
            BindText(details, "detailMeta", semanticDetail?.MetaText ?? string.Empty);
            BindText(details, "detailFrame", semanticDetail?.FrameText ?? string.Empty);
            BindText(details, "detailEntities", semanticDetail?.DetailLine1 ?? string.Empty);
            BindText(details, "detailTrace", semanticDetail?.DetailLine2 ?? string.Empty);
            BindText(details, "detailEvidence", semanticDetail?.DetailLine3 ?? string.Empty);
            BindText(
                details,
                "detailParticipants",
                semanticDetail?.ParticipantsText ?? string.Empty);
            BindTimelineDetailTrackIcon(details, item.TrackKind);
            BindTooltipValue(
                details.GetChild("detailEvidenceBackground"),
                semanticDetail?.TooltipText ?? string.Empty);
            BindTooltipValue(
                details.GetChild("detailSummaryBackground"),
                semanticDetail?.TooltipText ?? string.Empty);

            SetTimelineChildVisible(details, "detailFrame", !aggregate);
            SetTimelineChildVisible(details, "detailEntities", !aggregate);
            SetTimelineChildVisible(details, "detailTrace", !aggregate);
            SetTimelineChildVisible(details, "detailEvidence", !aggregate);
            SetTimelineChildVisible(details, "detailParticipants", !aggregate);
            SetTimelineChildVisible(details, "detailRangeBackground", !aggregate);
            SetTimelineChildVisible(details, "detailSummaryBackground", !aggregate);
            SetTimelineChildVisible(details, "detailEvidenceBackground", !aggregate);
            SetTimelineChildVisible(
                details,
                "memberScrollHint",
                exactStack && item.Members.Count > VisibleMemberCount);
            SetTimelineChildVisible(details, "btnLocateStart", !aggregate);
            SetTimelineChildVisible(details, "btnLocateEnd", !aggregate && item.EndFrame > item.StartFrame);
            Fgui.GButton locateStart = details.GetChild("btnLocateStart")?.asButton;
            if (locateStart != null)
            {
                locateStart.text = item.EndFrame > item.StartFrame
                    ? "起点  " + item.StartFrame.ToString(CultureInfo.InvariantCulture)
                    : "定位第 " + item.StartFrame.ToString(CultureInfo.InvariantCulture) + " 帧";
                BindTooltipValue(
                    locateStart,
                    "定位到事件起点，第 " + item.StartFrame.ToString(CultureInfo.InvariantCulture) + " 帧");
            }
            Fgui.GButton locateEnd = details.GetChild("btnLocateEnd")?.asButton;
            if (locateEnd != null)
            {
                locateEnd.text = "终点  " + item.EndFrame.ToString(CultureInfo.InvariantCulture);
                BindTooltipValue(
                    locateEnd,
                    "定位到事件终点，第 " + item.EndFrame.ToString(CultureInfo.InvariantCulture) + " 帧");
            }
            Fgui.GButton close = details.GetChild("btnCloseDetail")?.asButton;
            if (close != null)
            {
                close.text = string.Empty;
                BindTooltipValue(close, "关闭事件详情");
            }
            RenderTimelineClusterMembers(details, item);
        }

        internal void BindCausalSummary(
            Fgui.GComponent details,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool compact)
        {
            RenderRuntimeFrameCausalSummary(details, viewModel, compact);
        }

        internal void SetCausalSummaryVisible(Fgui.GComponent details, bool visible)
        {
            SetRuntimeFrameCausalSummaryVisible(details, visible);
        }

        internal void SetInspectorChromeVisible(Fgui.GComponent details, bool visible)
        {
            SetTimelineEventInspectorChromeVisible(details, visible);
        }

        internal void Suspend()
        {
            ClearSelection();
            InteractionsBound = false;
        }

        internal void ClearSelection()
        {
            ClusterMemberOffset = 0;
            Item = null;
            Workspace = null;
        }

        internal void ResetForRoot()
        {
            ClearSelection();
            Details = null;
            InteractionsBound = false;
        }

        private void BindTooltipValue(Fgui.GObject target, string value)
        {
            _bindTooltip?.Invoke(target, value);
        }

        private static void SetDecisionFieldsVisible(Fgui.GComponent details, bool visible)
        {
            for (int index = 0; index < DecisionFieldNames.Length; index++)
                SetTimelineChildVisible(details, DecisionFieldNames[index], visible);
        }

        private void EnsureInteractions(Fgui.GComponent details)
        {
            if (InteractionsBound && ReferenceEquals(Details, details))
                return;
            Details = details;
            InteractionsBound = true;
            details.onTouchBegin.Set(context => context.StopPropagation());
            details.onTouchMove.Set(context => context.StopPropagation());
            details.onTouchEnd.Set(context => context.StopPropagation());
            details.onClick.Set(context => context.StopPropagation());

            BindStableButton(details, "btnCloseDetail", () => _intentSink?.ClearSelection());
            BindStableButton(details, "btnLocateStart", () => Navigate(Item, false));
            BindStableButton(details, "btnLocateEnd", () => Navigate(Item, true));

            for (int index = 1; index <= 4; index++)
            {
                Fgui.GButton row = details.GetChild("memberRow" + index)?.asButton;
                if (row == null)
                    continue;
                row.onTouchBegin.Set(context => context.StopPropagation());
                row.onTouchMove.Set(context => context.StopPropagation());
                row.onTouchEnd.Set(context => context.StopPropagation());
                row.onClick.Set(context =>
                {
                    context.StopPropagation();
                    CharacterTestRuntimeAiLabTimelineItem member =
                        row.data as CharacterTestRuntimeAiLabTimelineItem;
                    if (member == null)
                        return;
                    if (context.inputEvent != null && context.inputEvent.isDoubleClick)
                    {
                        Navigate(member, false);
                        return;
                    }
                    for (int rowIndex = 1; rowIndex <= VisibleMemberCount; rowIndex++)
                    {
                        Fgui.GButton candidate =
                            details.GetChild("memberRow" + rowIndex)?.asButton;
                        if (candidate != null)
                            candidate.selected = ReferenceEquals(candidate, row);
                    }
                    CharacterTestRuntimeAiLabTimelineDetailViewModel memberDetail =
                        ResolveTimelineDetail(member);
                    BindText(details, "detailMeta", memberDetail?.MetaText ?? string.Empty);
                });
            }

            if (details.displayObject != null)
            {
                details.displayObject.onMouseWheel.Set(context =>
                {
                    context.StopPropagation();
                    if (Item == null
                        || Item.ItemKind != CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack
                        || Item.Members.Count <= VisibleMemberCount)
                    {
                        return;
                    }
                    float delta = context.inputEvent?.mouseWheelDelta ?? 0f;
                    int maxOffset = Math.Max(0, Item.Members.Count - VisibleMemberCount);
                    ClusterMemberOffset = Mathf.Clamp(
                        ClusterMemberOffset + (delta < 0f ? 1 : -1),
                        0,
                        maxOffset);
                    RenderTimelineClusterMembers(Details, Item);
                });
            }
        }

        private static void BindStableButton(
            Fgui.GComponent details,
            string name,
            Action action)
        {
            Fgui.GButton button = details.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.onTouchBegin.Set(context => context.StopPropagation());
            button.onTouchMove.Set(context => context.StopPropagation());
            button.onTouchEnd.Set(context => context.StopPropagation());
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                action?.Invoke();
            });
        }

        private void Navigate(CharacterTestRuntimeAiLabTimelineItem item, bool useEndFrame)
        {
            if (item != null)
                _intentSink?.Navigate(item.CreateLocator(), useEndFrame);
        }

        private static Color32 RuntimeFrameTrackAccent(
            CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception:
                    return new Color32(78, 219, 213, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision:
                    return new Color32(76, 190, 232, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action:
                    return new Color32(234, 190, 55, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result:
                    return new Color32(105, 199, 112, 255);
                default:
                    return new Color32(244, 178, 56, 255);
            }
        }

        private static Color RuntimeFrameTrackAccentColor(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            byte alpha = 255)
        {
            Color32 accent = RuntimeFrameTrackAccent(kind);
            return RuntimeFrameSrgb(accent.r, accent.g, accent.b, alpha);
        }

        private CharacterTestRuntimeAiLabTimelineDetailViewModel ResolveTimelineDetail(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (Workspace != null
                && Workspace.TryGetTimelineDetail(
                    item,
                    out CharacterTestRuntimeAiLabTimelineDetailViewModel detail))
            {
                return detail;
            }
            return CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
        }
        private static void ApplyTimelineDetailGeometry(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (details == null)
                return;

            float height = ResolveTimelineDetailHeight(item);
            details.SetSize(DetailWidth, height);
            SetObjectGeometry(
                details.GetChild("detailBackground"),
                0f,
                0f,
                DetailWidth,
                height);
            Fgui.GGraph background = details.GetChild("detailBackground")?.asGraph;
            if (background != null)
            {
                background.DrawRect(
                    background.width,
                    background.height,
                    1,
                    RuntimeFrameSrgb(51, 73, 86, item == null ? (byte)160 : (byte)220),
                    RuntimeFrameSrgb(7, 20, 30, item == null ? (byte)255 : (byte)245));
            }

            if (item == null)
            {
                SetObjectGeometry(
                    details.GetChild("detailTitle"),
                    24f,
                    DecisionContentY + 4f,
                    300f,
                    42f);
                Fgui.GTextField title = details.GetChild("detailTitle")?.asTextField;
                SetTimelineFontSize(title, 36);
                return;
            }

            SetObjectGeometry(details.GetChild("detailAccent"), 24f, DecisionContentY + 12f, 5f, 58f);
            SetObjectGeometry(details.GetChild("detailTrackIcon"), 40f, DecisionContentY + 16f, 44f, 44f);
            SetObjectGeometry(details.GetChild("detailTitle"), 100f, DecisionContentY + 10f, 324f, 34f);
            SetObjectGeometry(details.GetChild("detailMeta"), 100f, DecisionContentY + 44f, 324f, 22f);
            SetObjectGeometry(details.GetChild("btnCloseDetail"), 452f, DecisionContentY + 14f, 44f, 44f);
            Fgui.GTextField eventTitle = details.GetChild("detailTitle")?.asTextField;
            SetTimelineFontSize(eventTitle, TitleFontSize);
            SetTimelineFontSize(
                details.GetChild("detailMeta")?.asTextField,
                MetaFontSize);

            bool aggregate = item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack
                || item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate;
            if (aggregate)
                return;

            SetObjectGeometry(details.GetChild("detailRangeBackground"), 24f, DecisionContentY + 82f, 472f, 58f);
            SetObjectGeometry(details.GetChild("detailFrame"), 44f, DecisionContentY + 82f, 432f, 58f);
            SetObjectGeometry(details.GetChild("detailSummaryBackground"), 24f, DecisionContentY + 150f, 472f, 150f);
            SetObjectGeometry(details.GetChild("detailEntities"), 44f, DecisionContentY + 158f, 432f, 40f);
            SetObjectGeometry(details.GetChild("detailTrace"), 44f, DecisionContentY + 204f, 432f, 40f);
            SetObjectGeometry(details.GetChild("detailEvidence"), 44f, DecisionContentY + 250f, 432f, 42f);
            SetObjectGeometry(details.GetChild("detailEvidenceBackground"), 24f, DecisionContentY + 310f, 472f, 40f);
            SetObjectGeometry(details.GetChild("detailParticipants"), 44f, DecisionContentY + 312f, 432f, 36f);
            SetObjectGeometry(
                details.GetChild("btnLocateStart"),
                24f,
                DecisionContentY + 360f,
                item.EndFrame > item.StartFrame ? 226f : 472f,
                40f);
            SetObjectGeometry(
                details.GetChild("btnLocateEnd"),
                262f,
                DecisionContentY + 360f,
                234f,
                40f);
            SetTimelineFontSize(
                details.GetChild("detailFrame")?.asTextField,
                FrameFontSize);
            SetTimelineFontSize(
                details.GetChild("detailEntities")?.asTextField,
                SummaryFontSize);
            SetTimelineFontSize(
                details.GetChild("detailTrace")?.asTextField,
                SecondaryFontSize);
            SetTimelineFontSize(
                details.GetChild("detailEvidence")?.asTextField,
                EvidenceFontSize);
            SetTimelineFontSize(
                details.GetChild("detailParticipants")?.asTextField,
                ParticipantsFontSize);
            SetTimelineFontSize(
                details.GetChild("btnLocateStart")?.asButton?.GetChild("title")?.asTextField,
                ButtonFontSize);
            SetTimelineFontSize(
                details.GetChild("btnLocateEnd")?.asButton?.GetChild("title")?.asTextField,
                ButtonFontSize);
            DrawTimelineDetailSurface(details.GetChild("detailRangeBackground"), 13, 33, 46, 58, 82, 96);
            DrawTimelineDetailSurface(details.GetChild("detailSummaryBackground"), 11, 29, 40, 44, 65, 78);
            DrawTimelineDetailSurface(details.GetChild("detailEvidenceBackground"), 8, 23, 32, 35, 53, 65);
        }

        internal static float ResolveTimelineDetailHeight(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return DefaultHeight;
            if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack)
                return ClusterHeight;
            if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate)
                return AggregateHeight;
            return NormalHeight;
        }

        private static void DrawTimelineDetailSurface(
            Fgui.GObject target,
            byte fillRed,
            byte fillGreen,
            byte fillBlue,
            byte borderRed,
            byte borderGreen,
            byte borderBlue)
        {
            Fgui.GGraph graph = target?.asGraph;
            if (graph == null)
                return;
            graph.DrawRect(
                graph.width,
                graph.height,
                1,
                RuntimeFrameSrgb(borderRed, borderGreen, borderBlue, 210),
                RuntimeFrameSrgb(fillRed, fillGreen, fillBlue, 224));
        }

        private static void SetTimelineEventInspectorChromeVisible(
            Fgui.GComponent details,
            bool visible)
        {
            SetTimelineChildVisible(details, "detailAccent", visible);
            SetTimelineChildVisible(details, "detailTrackIcon", visible);
            SetTimelineChildVisible(details, "detailRangeBackground", visible);
            SetTimelineChildVisible(details, "detailSummaryBackground", visible);
            SetTimelineChildVisible(details, "detailEvidenceBackground", visible);
        }

        private static void BindTimelineDetailTrackIcon(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabTimelineTrackKind trackKind)
        {
            int index = Mathf.Clamp((int)trackKind, 0, TrackIconResourceNames.Length - 1);
            Color32 accent = RuntimeFrameTrackAccentColor(trackKind);
            Fgui.GImage icon = details.GetChild("detailTrackIcon")?.asImage;
            if (icon != null)
            {
                string iconUrl = Fgui.UIPackage.GetItemURL(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    TrackIconResourceNames[index]);
                Fgui.PackageItem iconItem = Fgui.UIPackage.GetItemByURL(iconUrl);
                icon.texture = iconItem?.owner.GetItemAsset(iconItem) as Fgui.NTexture;
                icon.color = accent;
            }

            Fgui.GGraph detailAccent = details.GetChild("detailAccent")?.asGraph;
            if (detailAccent != null)
            {
                detailAccent.DrawRect(
                    detailAccent.width,
                    detailAccent.height,
                    0,
                    Color.clear,
                    accent);
            }
            Fgui.GTextField meta = details.GetChild("detailMeta")?.asTextField;
            if (meta != null)
                meta.color = accent;
        }

        internal static string BuildTimelineDetailMeta(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildMetaText(item);
        }

        internal static string BuildTimelineDetailFrame(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildFrameText(item);
        }

        internal static string BuildTimelineDetailSummary(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null)
                ?.DetailLine1 ?? string.Empty;
        }

        internal static string BuildTimelineDetailParticipants(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildParticipantsText(item);
        }

        internal static string BuildTimelineDetailEvidence(
            CharacterTestRuntimeAiLabTimelineItem item,
            bool full)
        {
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
            return full ? detail?.TooltipText ?? string.Empty : detail?.DetailLine3 ?? string.Empty;
        }

        private void RenderRuntimeFrameCausalSummary(
            Fgui.GComponent details,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool compact = false)
        {
            CharacterTestRuntimeAiLabFactsCausalStepViewModel[] steps =
                viewModel.Facts?.CausalFlow;
            int count = steps?.Length ?? 0;
            for (int i = 0; i < 4; i++)
            {
                Fgui.GButton row = details.GetChild("memberRow" + (i + 1))?.asButton;
                if (row != null)
                {
                    row.visible = false;
                    row.data = null;
                    row.selected = false;
                    row.touchable = false;
                    BindTooltipValue(row, string.Empty);
                }

                string title;
                string summary;
                string tooltip;
                CharacterTestRuntimeAiLabFactsStatus status =
                    CharacterTestRuntimeAiLabFactsStatus.Neutral;
                CharacterTestRuntimeAiLabTimelineItem causalItem =
                    FindRuntimeTestCausalItem(viewModel, i);
                if (i >= count)
                {
                    title = CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildCausalStageTitle(i);
                    summary = RuntimeFrameFallbackSummary(viewModel, i);
                    tooltip = summary;
                    status = ResolveRuntimeTestCausalStatus(causalItem);
                }
                else
                {
                    CharacterTestRuntimeAiLabFactsCausalStepViewModel step = steps[i];
                    title = string.IsNullOrEmpty(step.Title)
                        ? CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildCausalStageTitle(i)
                        : step.Title;
                    summary = step.PrimaryText;
                    tooltip = string.IsNullOrEmpty(step.DetailText)
                        ? step.PrimaryText
                        : step.PrimaryText + "\n" + step.DetailText;
                    status = step.Status;
                }
                bool canDrillDown = causalItem != null
                    && !string.IsNullOrEmpty(summary)
                    && summary != "—";
                if (compact)
                    summary = BuildBehaviorPlanCausalSummary(i, summary, causalItem, status);
                if (canDrillDown)
                    summary += "  ›";

                BindText(details, "causalTitle" + (i + 1), title);
                BindText(details, "causalValue" + (i + 1), summary);
                Fgui.GObject card = details.GetChild("causalCard" + (i + 1));
                Fgui.GObject icon = details.GetChild("causalIcon" + (i + 1));
                float cardX = compact ? 24f : 28f;
                float cardStartY = DecisionContentY + (compact ? 56f : 80f);
                float cardStride = compact ? 82f : 108f;
                float cardWidth = compact ? 472f : CausalCardWidth;
                float cardHeight = compact ? 70f : CausalCardHeight;
                float iconX = compact ? 36f : 44f;
                float iconSize = compact ? 40f : CausalIconSize;
                float textYInset = compact ? 15f : 18f;
                float cardY = cardStartY + i * cardStride;
                if (card != null)
                {
                    card.sortingOrder = 1;
                    card.SetSize(cardWidth, cardHeight);
                    card.SetXY(cardX, cardY);
                    card.touchable = canDrillDown;
                    card.grayed = false;
                    Fgui.GGraph graph = card.asGraph;
                    if (graph != null)
                    {
                        ResolveRuntimeTestCausalTone(
                            status,
                            out Color borderColor,
                            out Color fillColor);
                        graph.color = Color.white;
                        graph.DrawRect(
                            card.width,
                            card.height,
                            1,
                            borderColor,
                            fillColor);
                    }
                    int stageIndex = i;
                    card.onClick.Set(() =>
                        _selectCausalItem?.Invoke(viewModel, stageIndex));
                    BindTooltipValue(card, tooltip);
                }
                if (icon != null)
                {
                    icon.sortingOrder = 3;
                    icon.SetSize(iconSize, iconSize);
                    icon.SetXY(iconX, cardY + textYInset);
                    icon.touchable = false;
                    icon.grayed = false;
                    Fgui.GLoader glyph = icon.asButton?.GetChild("icon")?.asLoader;
                    bool pendingRuntimeResult = compact
                        && i == 3
                        && IsPendingRuntimeResult(causalItem, status);
                    Fgui.GButton iconButton = icon.asButton;
                    if (iconButton != null && compact && i == 3)
                    {
                        iconButton.icon = Fgui.UIPackage.GetItemURL(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                            pendingRuntimeResult
                                ? TrackIconResourceNames[4]
                                : "action_valid_32x32");
                    }
                    if (glyph != null)
                    {
                        glyph.visible = true;
                        glyph.color = pendingRuntimeResult
                                ? RuntimeFrameSrgb(242, 181, 67)
                                : RuntimeFrameTrackAccentColor(
                                    (CharacterTestRuntimeAiLabTimelineTrackKind)i);
                    }
                    if (iconButton != null)
                    {
                        iconButton.title = string.Empty;
                        iconButton.titleFontSize = 0;
                    }
                }
                Fgui.GTextField titleText = details.GetChild(
                    "causalTitle" + (i + 1))?.asTextField;
                if (titleText != null)
                {
                    titleText.sortingOrder = 3;
                    titleText.SetSize(compact ? 88f : 110f, compact ? 54f : iconSize);
                    titleText.SetXY(compact ? 88f : 116f, cardY + 8f);
                    titleText.align = Fgui.AlignType.Left;
                    titleText.verticalAlign = Fgui.VertAlignType.Middle;
                    SetTimelineFontSize(
                        titleText,
                        compact ? 22 : CausalTitleFontSize);
                }
                Fgui.GTextField valueText = details.GetChild(
                    "causalValue" + (i + 1))?.asTextField;
                if (valueText != null)
                {
                    valueText.sortingOrder = 3;
                    valueText.SetSize(compact ? 202f : 244f, compact ? 54f : iconSize);
                    valueText.SetXY(compact ? 180f : 230f, cardY + 8f);
                    valueText.align = compact ? Fgui.AlignType.Left : Fgui.AlignType.Right;
                    valueText.verticalAlign = Fgui.VertAlignType.Middle;
                    SetTimelineFontSize(
                        valueText,
                        compact ? 20 : CausalValueFontSize);
                }
                if (compact && row != null)
                {
                    row.sortingOrder = 4;
                    row.visible = true;
                    row.touchable = false;
                    row.selected = false;
                    row.text = ResolveBehaviorPlanCausalStatusLabel(i, status, causalItem);
                    row.SetSize(90f, 34f);
                    row.SetXY(390f, cardY + 18f);
                    row.titleFontSize = 18;
                    row.grayed = false;
                }
                if (i < 3)
                {
                    Fgui.GGraph connector = details.GetChild(
                        "causalConnector" + (i + 1))?.asGraph;
                    if (connector != null)
                    {
                        connector.sortingOrder = 2;
                        connector.SetXY(
                            iconX + iconSize * 0.5f - 1f,
                            cardY + cardHeight);
                        connector.DrawRect(
                            2f,
                            cardStride - cardHeight,
                            0,
                            Color.clear,
                            RuntimeFrameSrgb(161, 178, 187, 180));
                    }
                }
            }
            Fgui.GButton fifthMemberRow = details.GetChild("memberRow5")?.asButton;
            if (fifthMemberRow != null)
            {
                fifthMemberRow.visible = false;
                fifthMemberRow.data = null;
                fifthMemberRow.selected = false;
                fifthMemberRow.touchable = false;
                BindTooltipValue(fifthMemberRow, string.Empty);
            }
            SetRuntimeFrameCausalSummaryVisible(details, true);
        }

        private static void SetRuntimeFrameCausalSummaryVisible(
            Fgui.GComponent details,
            bool visible)
        {
            for (int i = 1; i <= 4; i++)
            {
                SetTimelineChildVisible(details, "causalCard" + i, visible);
                SetTimelineChildVisible(details, "causalIcon" + i, visible);
                SetTimelineChildVisible(details, "causalTitle" + i, visible);
                SetTimelineChildVisible(details, "causalValue" + i, visible);
                if (i < 4)
                    SetTimelineChildVisible(details, "causalConnector" + i, visible);
            }
        }

        private static string BuildBehaviorPlanCausalSummary(
            int index,
            string summary,
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabFactsStatus status)
        {
            string value = string.IsNullOrWhiteSpace(summary) ? "—" : summary.Trim();
            if (index == 2 && value == "—")
                return "无命令";
            if (value == "—")
                return value;
            if (index == 0)
                return value.StartsWith("目标：", StringComparison.Ordinal) ? value : "目标：" + value;
            if (index == 1)
                return value.StartsWith("选择：", StringComparison.Ordinal) ? value : "选择：" + value;
            if (index == 2)
            {
                if (item?.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued)
                    return "能力命令进入队列";
                return value.StartsWith("命令：", StringComparison.Ordinal) ? value : "命令：" + value;
            }
            if (IsPendingRuntimeResult(item, status))
                return "等待 Runtime 执行";
            return value;
        }

        private static string ResolveBehaviorPlanCausalStatusLabel(
            int index,
            CharacterTestRuntimeAiLabFactsStatus status,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (status == CharacterTestRuntimeAiLabFactsStatus.Error)
                return "失败";
            if (index == 0)
                return status == CharacterTestRuntimeAiLabFactsStatus.Neutral ? "等待" : "已确认";
            if (index == 1)
                return status == CharacterTestRuntimeAiLabFactsStatus.Neutral ? "等待" : "已选择";
            if (index == 2)
            {
                return item?.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                    ? "已提交"
                    : status == CharacterTestRuntimeAiLabFactsStatus.Neutral ? "等待" : "已选择";
            }
            if (IsPendingRuntimeResult(item, status))
                return "待观察";
            return status == CharacterTestRuntimeAiLabFactsStatus.Success ? "已完成" : "等待";
        }

        private static bool IsPendingRuntimeResult(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabFactsStatus status)
        {
            return status == CharacterTestRuntimeAiLabFactsStatus.Warning
                && (item == null
                    || item.RuntimeEffectKind
                        == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved);
        }

        internal static string RuntimeFrameFallbackSummary(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            int index)
        {
            CharacterTestRuntimeAiLabTimelineItem item =
                FindRuntimeTestCausalItem(viewModel, index);
            if (item == null)
                return "—";
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail = null;
            if (viewModel.RuntimeFrame != null)
            {
                viewModel.RuntimeFrame.TryGetTimelineDetail(item, out detail);
            }
            detail ??= CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
            if (index == 1)
                return StripRuntimeTestSemanticPrefix(detail?.DetailLine1);
            if (index == 2)
            {
                string action = StripRuntimeTestSemanticPrefix(detail?.DetailLine2);
                return string.IsNullOrEmpty(action)
                    ? StripRuntimeTestSemanticPrefix(detail?.DetailLine1)
                    : action;
            }
            if (index == 3)
            {
                return CharacterTestRuntimeAiLabTimelineDetailProjector
                    .ResolveExecutionStatus(item);
            }
            return detail?.Title ?? "—";
        }

        private void RenderTimelineClusterMembers(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            bool cluster = item != null && item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack;
            for (int i = 0; i < VisibleMemberCount; i++)
            {
                Fgui.GButton row = details.GetChild("memberRow" + (i + 1))?.asButton;
                if (row == null)
                    continue;
                SetObjectGeometry(
                    row,
                    24f,
                    DecisionContentY + 84f + i * 58f,
                    472f,
                    50f);
                SetTimelineFontSize(
                    row.GetChild("title")?.asTextField,
                    ButtonFontSize);
                int memberIndex = ClusterMemberOffset + i;
                bool rowVisible = cluster && memberIndex < item.Members.Count;
                row.visible = rowVisible;
                row.touchable = rowVisible;
                if (!rowVisible)
                {
                    row.data = null;
                    BindTooltipValue(row, string.Empty);
                    row.selected = false;
                    continue;
                }
                CharacterTestRuntimeAiLabTimelineItem member = item.Members[memberIndex];
                CharacterTestRuntimeAiLabTimelineDetailViewModel memberDetail =
                    ResolveTimelineDetail(member);
                row.data = member;
                row.selected = false;
                BindTooltipValue(row, memberDetail?.TooltipText ?? string.Empty);
                row.text = member.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "  ·  " + (memberDetail?.Title ?? "时间轴事件")
                    + (member.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Warning
                        ? "  ·  警告"
                        : member.Severity == CharacterTestRuntimeAiLabTimelineSeverity.Error
                            ? "  ·  错误"
                            : string.Empty);
            }
            if (cluster)
            {
                BindText(
                    details,
                    "memberScrollHint",
                    item.Members.Count > VisibleMemberCount
                        ? "滚轮查看更多  ·  "
                            + (ClusterMemberOffset + 1).ToString(CultureInfo.InvariantCulture)
                            + "–"
                            + Math.Min(
                                ClusterMemberOffset
                                    + VisibleMemberCount,
                                item.Members.Count)
                                .ToString(CultureInfo.InvariantCulture)
                            + " / "
                            + item.Members.Count.ToString(CultureInfo.InvariantCulture)
                        : string.Empty);
                SetObjectGeometry(
                    details.GetChild("memberScrollHint"),
                    24f,
                    DecisionContentY + 378f,
                    472f,
                    24f);
                SetTimelineFontSize(
                    details.GetChild("memberScrollHint")?.asTextField,
                    HintFontSize);
            }
        }
    }
}
