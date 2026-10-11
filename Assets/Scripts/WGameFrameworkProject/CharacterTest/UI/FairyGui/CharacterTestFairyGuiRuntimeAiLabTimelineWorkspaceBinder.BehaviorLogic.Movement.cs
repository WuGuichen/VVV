using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder
    {
        internal bool BindBehaviorCardLibraryMovementDetail(
            Fgui.GComponent parent,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card,
            bool visible)
        {
            if (card?.HasPrimaryStage != true
                || card.PrimaryStage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                return false;

            Fgui.GComponent details =
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardSelectionDetailPresenter.EnsureRoot(parent);
            if (details == null)
                return true;
            details.visible = visible;
            if (!visible)
                return true;
            if (HasFocusedBehaviorLogicTypedInput(details))
                return true;

            CharacterTestFairyGuiRuntimeAiLabBehaviorCardSelectionDetailPresenter.ResetRootForMode(
                details,
                "movement-editor");
            details.SetSize(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentHeight);
            details.SetXY(
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY);
            details.sortingOrder = 135;
            details.opaque = true;
            details.touchable = true;

            ResetBehaviorLogicTypedControlHost(details);
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            CharacterTestRuntimeAiLabLogicStageViewModel stage = card.PrimaryStage;
            bool expanded = stage.MovementTargetKind
                == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                || stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity;
            // Library selection is a read-only preview. Editing starts only after the card has
            // been explicitly added and opened in the behavior-logic workspace.
            const bool enabled = false;

            AddBehaviorLogicMovementRect(
                host,
                "movementSelectionOpaqueBackground",
                0f,
                0f,
                520f,
                608f,
                RuntimeFrameSrgb(2, 8, 12, 255),
                sortingOrder: 0);
            AddBehaviorLogicMovementSurface(
                host,
                "movementSelectionBackground",
                0f,
                0f,
                520f,
                608f,
                emphasized: false);
            AddBehaviorLogicMovementText(
                host,
                "movementSelectionTitle",
                card.Title + (card.RuntimeActive ? " · 正在执行" : string.Empty),
                28f,
                10f,
                300f,
                42f,
                28,
                RuntimeFrameSrgb(240, 248, 250));
            Fgui.GTextField source = AddBehaviorLogicMovementText(
                host,
                "movementSelectionSource",
                card.ConfigurationSourceText,
                300f,
                12f,
                190f,
                38f,
                16,
                card.InCurrentBehaviorGroup
                    ? RuntimeFrameSrgb(82, 227, 156)
                    : RuntimeFrameSrgb(111, 199, 240));
            if (source != null)
                source.align = Fgui.AlignType.Right;

            const float editorY = 66f;
            float editorHeight = expanded ? 196f : 128f;
            float parameterY = editorY + 14f;
            AddBehaviorLogicMovementSurface(
                host,
                "movementEditorSurface",
                36f,
                editorY,
                448f,
                editorHeight,
                emphasized: true);
            BindBehaviorLogicMovementParameters(host, stage, enabled, parameterY);
            AddBehaviorLogicMovementText(
                host,
                "movementInputError",
                string.Empty,
                48f,
                parameterY + editorHeight - 44f,
                408f,
                26f,
                17,
                RuntimeFrameSrgb(255, 130, 138));

            float feedbackY = editorY + editorHeight + 16f;
            float feedbackHeight = 600f - feedbackY;
            AddBehaviorLogicMovementSurface(
                host,
                "movementFeedbackSurface",
                36f,
                feedbackY,
                448f,
                feedbackHeight,
                emphasized: false);
            string noLiveValue = null;
            string noLiveStatus = null;
            string noLiveMeaning = null;
            string feedbackTitle = null;
            if (!card.RuntimeActive)
            {
                if (card.InCurrentBehaviorGroup)
                {
                    noLiveValue = "当前未执行";
                    noLiveStatus = "待机";
                    noLiveMeaning = "执行这张卡时在这里显示目标与停止线的位置";
                    feedbackTitle = "运行反馈";
                }
                else
                {
                    noLiveValue = "项目默认参数";
                    noLiveStatus = "只读预览";
                    noLiveMeaning = "装配到当前行为组后可以编辑，并显示实时距离";
                    feedbackTitle = "配置预览";
                }
            }
            BindBehaviorLogicMovementFeedback(
                host,
                card.ObservedTargetDistanceDecimeters,
                stage,
                feedbackY,
                feedbackHeight,
                noLiveValue,
                noLiveStatus,
                noLiveMeaning,
                feedbackTitle);
            return true;
        }

        private static void ConfigureBehaviorLogicMovementInspectorGeometry(
            Fgui.GComponent details)
        {
            details.SetSize(RuntimeFrameEventDetailWidth, BehaviorPlanDecisionHeight);
            SetObjectGeometry(
                details.GetChild("detailBackground"),
                0f,
                0f,
                RuntimeFrameEventDetailWidth,
                BehaviorPlanDecisionHeight);
            SetObjectGeometry(
                details.GetChild("planActionBackground"),
                24f,
                DecisionContentY + 52f,
                472f,
                528f);
            SetTimelineChildVisible(details, "planActionTitle", false);
        }

        private void BindBehaviorLogicMovementParameters(
            Fgui.GComponent host,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            bool enabled,
            float baseY)
        {
            AddBehaviorLogicMovementText(
                host,
                "movementActionSummary",
                ResolveBehaviorLogicMovementActionSummary(stage),
                48f,
                baseY,
                278f,
                28f,
                19,
                RuntimeFrameSrgb(75, 222, 209));
            AddBehaviorLogicMovementText(
                host,
                "movementExecutionSource",
                "步态：角色默认",
                330f,
                baseY,
                126f,
                28f,
                15,
                RuntimeFrameSrgb(148, 184, 191));
            float parameterY = baseY + 34f;
            float distanceY = parameterY;
            switch (stage.MovementTargetKind)
            {
                case CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition:
                    AddBehaviorLogicMovementText(
                        host, "movementPositionLabel", "目的地", 48f, parameterY, 92f, 56f, 20,
                        RuntimeFrameSrgb(169, 190, 198));
                    AddBehaviorLogicMovementText(
                        host, "movementTargetXLabel", "X", 140f, parameterY, 26f, 56f, 20,
                        RuntimeFrameSrgb(148, 184, 191));
                    Fgui.GTextInput xInput = AddBehaviorLogicMovementInput(
                        host,
                        "movementTargetX",
                        stage.MoveTargetPositionX.ToString("0.###", CultureInfo.InvariantCulture),
                        "X",
                        168f,
                        parameterY,
                        112f,
                        56f,
                        enabled);
                    AddBehaviorLogicMovementText(
                        host, "movementTargetZLabel", "Z", 294f, parameterY, 26f, 56f, 20,
                        RuntimeFrameSrgb(148, 184, 191));
                    Fgui.GTextInput zInput = AddBehaviorLogicMovementInput(
                        host,
                        "movementTargetZ",
                        stage.MoveTargetPositionZ.ToString("0.###", CultureInfo.InvariantCulture),
                        "Z",
                        322f,
                        parameterY,
                        134f,
                        56f,
                        enabled);
                    bool positionCommitted = false;
                    Action commitPosition = () =>
                    {
                        if (positionCommitted || !enabled)
                            return;
                        if (!TryParseBehaviorLogicMovementFloat(xInput?.text, out float x)
                            || !TryParseBehaviorLogicMovementFloat(zInput?.text, out float z))
                        {
                            SetBehaviorLogicMovementInputError(host, "世界位置需要有效的 X / Z 数字。");
                            return;
                        }
                        ClearBehaviorLogicMovementInputError(host);
                        positionCommitted = x.Equals(stage.MoveTargetPositionX)
                            && z.Equals(stage.MoveTargetPositionZ);
                        if (!positionCommitted)
                            positionCommitted = _behaviorCardWorkspaceBinder.TrySetMoveTargetPosition(
                                stage.StageId,
                                x,
                                z);
                        if (!positionCommitted)
                            SetBehaviorLogicMovementInputError(host, "世界位置修改未生效，请检查当前阶段。");
                    };
                    BindBehaviorLogicMovementInputCommit(xInput, commitPosition);
                    BindBehaviorLogicMovementInputCommit(zInput, commitPosition);
                    distanceY = parameterY + 60f;
                    break;
                case CharacterTestRuntimeAiLabMovementTargetKind.Entity:
                    AddBehaviorLogicMovementText(
                        host, "movementEntityLabel", "跟随单位", 48f, parameterY, 112f, 56f, 20,
                        RuntimeFrameSrgb(148, 184, 191));
                    Fgui.GTextInput entityInput = AddBehaviorLogicMovementInput(
                        host,
                        "movementEntityId",
                        stage.MoveTargetEntityId.ToString(CultureInfo.InvariantCulture),
                        "0 = 自动友军",
                        164f,
                        parameterY,
                        292f,
                        56f,
                        enabled);
                    bool entityCommitted = false;
                    BindBehaviorLogicMovementInputCommit(entityInput, () =>
                    {
                        if (entityCommitted || !enabled)
                            return;
                        if (!int.TryParse(
                                entityInput?.text?.Trim(),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int entityId)
                            || entityId < 0)
                        {
                            SetBehaviorLogicMovementInputError(host, "单位 ID 必须是 0 或正整数。");
                            return;
                        }
                        ClearBehaviorLogicMovementInputError(host);
                        entityCommitted = entityId == stage.MoveTargetEntityId
                            || _behaviorCardWorkspaceBinder.TrySetMoveTargetEntityId(
                                stage.StageId,
                                entityId);
                        if (!entityCommitted)
                            SetBehaviorLogicMovementInputError(host, "单位 ID 修改未生效，请检查当前阶段。");
                    });
                    distanceY = parameterY + 60f;
                    break;
            }

            BindBehaviorLogicMovementDistanceParameters(host, stage, enabled, distanceY);
        }

        private void BindBehaviorLogicMovementDistanceParameters(
            Fgui.GComponent host,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            bool enabled,
            float y)
        {
            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    BindBehaviorLogicMovementSingleDistance(
                        host, stage, enabled, y, "停止距离", stage.MoveMinDistance,
                        stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition);
                    return;
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    BindBehaviorLogicMovementSingleDistance(
                        host, stage, enabled, y, "触发距离", stage.MoveMinDistance, setBothValues: false);
                    return;
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    BindBehaviorLogicMovementDistanceBand(host, stage, enabled, y);
                    return;
                default:
                    AddBehaviorLogicMovementText(
                        host,
                        "movementNoParameters",
                        "无需额外参数",
                        56f,
                        y + 8f,
                        408f,
                        40f,
                        19,
                        RuntimeFrameSrgb(185, 210, 216));
                    return;
            }
        }

        private void BindBehaviorLogicMovementSingleDistance(
            Fgui.GComponent host,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            bool enabled,
            float y,
            string label,
            float value,
            bool setBothValues)
        {
            AddBehaviorLogicMovementText(
                host, "movementDistanceLabel", label, 48f, y, 136f, 48f, 21,
                RuntimeFrameSrgb(169, 190, 198));
            Fgui.GTextInput input = AddBehaviorLogicMovementInput(
                host,
                "movementDistance",
                value.ToString("0.###", CultureInfo.InvariantCulture),
                "0",
                188f,
                y,
                148f,
                48f,
                enabled);
            AddBehaviorLogicMovementText(
                host, "movementDistanceUnit", "米", 348f, y, 48f, 48f, 22,
                RuntimeFrameSrgb(185, 210, 216));
            bool committed = false;
            BindBehaviorLogicMovementInputCommit(input, () =>
            {
                if (committed || !enabled)
                    return;
                if (!TryParseBehaviorLogicMovementFloat(input?.text, out float next) || next < 0f)
                {
                    SetBehaviorLogicMovementInputError(host, label + "必须是大于或等于 0 的数字。");
                    return;
                }
                ClearBehaviorLogicMovementInputError(host);
                float maximum = setBothValues
                    ? next
                    : Math.Max(stage.MoveMaxDistance, next);
                committed = next.Equals(stage.MoveMinDistance) && maximum.Equals(stage.MoveMaxDistance);
                if (!committed)
                    committed = _behaviorCardWorkspaceBinder.TrySetMoveDistanceBand(
                        stage.StageId,
                        next,
                        maximum);
                if (!committed)
                    SetBehaviorLogicMovementInputError(host, label + "修改未生效，请检查当前行为。");
            });
        }

        private void BindBehaviorLogicMovementDistanceBand(
            Fgui.GComponent host,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            bool enabled,
            float y)
        {
            AddBehaviorLogicMovementText(
                host, "movementBandLabel", "理想距离", 48f, y, 112f, 48f, 21,
                RuntimeFrameSrgb(169, 190, 198));
            Fgui.GTextInput minimumInput = AddBehaviorLogicMovementInput(
                host,
                "movementMinimum",
                stage.MoveMinDistance.ToString("0.###", CultureInfo.InvariantCulture),
                "最小",
                164f,
                y,
                104f,
                48f,
                enabled);
            AddBehaviorLogicMovementText(
                host, "movementBandSeparator", "—", 274f, y, 28f, 48f, 22,
                RuntimeFrameSrgb(148, 184, 191));
            Fgui.GTextInput maximumInput = AddBehaviorLogicMovementInput(
                host,
                "movementMaximum",
                stage.MoveMaxDistance.ToString("0.###", CultureInfo.InvariantCulture),
                "最大",
                306f,
                y,
                104f,
                48f,
                enabled);
            AddBehaviorLogicMovementText(
                host, "movementBandUnit", "米", 416f, y, 40f, 48f, 22,
                RuntimeFrameSrgb(185, 210, 216));
            bool committed = false;
            Action commit = () =>
            {
                if (committed || !enabled)
                    return;
                if (!TryParseBehaviorLogicMovementFloat(minimumInput?.text, out float minimum)
                    || !TryParseBehaviorLogicMovementFloat(maximumInput?.text, out float maximum)
                    || minimum < 0f
                    || maximum < minimum)
                {
                    SetBehaviorLogicMovementInputError(host, "理想距离需要满足 0 ≤ 最小值 ≤ 最大值。");
                    return;
                }
                ClearBehaviorLogicMovementInputError(host);
                committed = minimum.Equals(stage.MoveMinDistance)
                    && maximum.Equals(stage.MoveMaxDistance);
                if (!committed)
                    committed = _behaviorCardWorkspaceBinder.TrySetMoveDistanceBand(
                        stage.StageId,
                        minimum,
                        maximum);
                if (!committed)
                    SetBehaviorLogicMovementInputError(host, "理想距离修改未生效，请检查当前行为。");
            };
            BindBehaviorLogicMovementInputCommit(minimumInput, commit);
            BindBehaviorLogicMovementInputCommit(maximumInput, commit);
        }

        private static void BindBehaviorLogicMovementFeedback(
            Fgui.GComponent host,
            int distanceDecimeters,
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            float y,
            float height,
            string noLiveValue = null,
            string noLiveStatus = null,
            string noLiveMeaning = null,
            string feedbackTitle = null)
        {
            bool currentCombatTarget = stage.MovementTargetKind
                == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            bool hasLiveDistance = currentCombatTarget && distanceDecimeters >= 0;
            float currentDistance = hasLiveDistance ? distanceDecimeters * 0.1f : -1f;
            float threshold = Math.Max(0f, stage.MoveMinDistance);

            AddBehaviorLogicMovementText(
                host,
                "movementFeedbackTitle",
                !string.IsNullOrWhiteSpace(feedbackTitle)
                    ? feedbackTitle
                    : currentCombatTarget ? "实时距离" : "目标配置",
                56f,
                y + 16f,
                160f,
                30f,
                20,
                RuntimeFrameSrgb(148, 184, 191));
            string value = hasLiveDistance
                ? currentDistance.ToString("0.0", CultureInfo.InvariantCulture) + " 米"
                : currentCombatTarget && !string.IsNullOrWhiteSpace(noLiveValue)
                    ? noLiveValue
                : stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                    ? "X " + stage.MoveTargetPositionX.ToString("0.##", CultureInfo.InvariantCulture)
                        + "  ·  Z "
                        + stage.MoveTargetPositionZ.ToString("0.##", CultureInfo.InvariantCulture)
                    : stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                        ? stage.MoveTargetEntityId == 0 ? "自动友军" : "单位 " + stage.MoveTargetEntityId
                        : "等待运行数据";
            AddBehaviorLogicMovementText(
                host,
                "movementFeedbackValue",
                value,
                56f,
                y + 46f,
                244f,
                48f,
                30,
                RuntimeFrameSrgb(231, 248, 247));
            Fgui.GTextField status = AddBehaviorLogicMovementText(
                host,
                "movementFeedbackStatus",
                !hasLiveDistance && !string.IsNullOrWhiteSpace(noLiveStatus)
                    ? noLiveStatus
                    : ResolveBehaviorLogicMovementFeedbackStatus(stage, currentDistance),
                300f,
                y + 50f,
                164f,
                44f,
                21,
                ResolveBehaviorLogicMovementFeedbackColor(stage, currentDistance));
            if (status != null)
                status.align = Fgui.AlignType.Right;

            const float axisLeft = 72f;
            const float axisRight = 448f;
            const float stopX = 204f;
            float axisY = y + Math.Min(154f, height - 104f);
            bool distanceDriven = stage.MovementStrategy
                == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                || stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose
                || stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                || stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget;
            if (distanceDriven)
            {
                AddBehaviorLogicMovementRect(
                    host,
                    "movementStopZone",
                    axisLeft,
                    axisY - 11f,
                    stopX - axisLeft,
                    28f,
                    RuntimeFrameSrgb(31, 142, 140, 74),
                    sortingOrder: 2);
            }
            AddBehaviorLogicMovementRect(
                host,
                "movementDistanceTrack",
                axisLeft,
                axisY,
                axisRight - axisLeft,
                6f,
                RuntimeFrameSrgb(60, 92, 103, 245),
                sortingOrder: 3);
            if (distanceDriven)
            {
                AddBehaviorLogicMovementRect(
                    host,
                    "movementStopMarker",
                    stopX,
                    axisY - 20f,
                    4f,
                    46f,
                    RuntimeFrameSrgb(75, 222, 209, 255),
                    sortingOrder: 4);
            }
            AddBehaviorLogicMovementEllipse(
                host,
                "movementSelfMarker",
                axisLeft - 9f,
                axisY - 6f,
                18f,
                18f,
                RuntimeFrameSrgb(122, 159, 169, 255),
                sortingOrder: 5);
            float targetX = ResolveBehaviorLogicMovementTargetMarkerX(
                hasLiveDistance ? currentDistance : threshold,
                threshold,
                axisLeft,
                stopX,
                axisRight);
            AddBehaviorLogicMovementEllipse(
                host,
                "movementTargetMarker",
                targetX - 12f,
                axisY - 9f,
                24f,
                24f,
                RuntimeFrameSrgb(238, 190, 60, 255),
                sortingOrder: 6,
                visible: hasLiveDistance && distanceDriven);

            AddBehaviorLogicMovementText(
                host,
                "movementSelfLabel",
                "自身",
                56f,
                axisY + 28f,
                64f,
                28f,
                18,
                RuntimeFrameSrgb(122, 159, 169));
            AddBehaviorLogicMovementText(
                host,
                "movementStopLabel",
                distanceDriven
                    ? "停止 " + threshold.ToString("0.##", CultureInfo.InvariantCulture) + " 米"
                    : "由检测器触发",
                stopX - 58f,
                axisY + 28f,
                120f,
                28f,
                18,
                RuntimeFrameSrgb(75, 222, 209));
            AddBehaviorLogicMovementText(
                host,
                "movementFeedbackMeaning",
                !hasLiveDistance && !string.IsNullOrWhiteSpace(noLiveMeaning)
                    ? noLiveMeaning
                    : ResolveBehaviorLogicMovementFeedbackMeaning(stage, currentDistance),
                56f,
                y + height - 52f,
                408f,
                34f,
                19,
                RuntimeFrameSrgb(148, 184, 191));
        }

        private static string ResolveBehaviorLogicMovementFeedbackStatus(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            float currentDistance)
        {
            if (currentDistance < 0f)
            {
                return stage.MovementTargetKind
                    == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        ? "固定位置"
                        : stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                            ? "跟随单位"
                            : "等待数据";
            }
            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    return currentDistance > stage.MoveMinDistance ? "正在接近" : "已经到达";
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return currentDistance < stage.MoveMinDistance ? "正在后撤" : "距离安全";
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return currentDistance < stage.MoveMinDistance
                        ? "距离过近"
                        : currentDistance > stage.MoveMaxDistance ? "距离过远" : "距离合适";
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return "正在避险";
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return "正在横移";
                default:
                    return "运行中";
            }
        }

        private static Color ResolveBehaviorLogicMovementFeedbackColor(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            float currentDistance)
        {
            if (currentDistance < 0f)
                return RuntimeFrameSrgb(122, 159, 169);
            bool settled = stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                ? currentDistance <= stage.MoveMinDistance
                : stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose
                    ? currentDistance >= stage.MoveMinDistance
                    : (stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                        || stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget)
                        && currentDistance >= stage.MoveMinDistance
                        && currentDistance <= stage.MoveMaxDistance;
            return settled
                ? RuntimeFrameSrgb(75, 222, 209)
                : RuntimeFrameSrgb(238, 190, 60);
        }

        private static string ResolveBehaviorLogicMovementFeedbackMeaning(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            float currentDistance)
        {
            if (currentDistance < 0f)
            {
                return stage.MovementTargetKind
                    == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                        ? "抵达指定坐标的停止范围后结束"
                        : stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity
                            ? "根据理想距离自动跟随指定单位"
                            : "运行后在这里显示目标与停止线的位置";
            }
            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    return currentDistance > stage.MoveMinDistance
                        ? "目标仍在停止线外，继续接近"
                        : "目标已经进入停止距离";
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return currentDistance < stage.MoveMinDistance
                        ? "目标位于触发线内，继续后撤"
                        : "目标已经离开触发范围";
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return "移动会把目标距离修正到理想范围";
                default:
                    return "图示随目标距离实时变化";
            }
        }

        private static float ResolveBehaviorLogicMovementTargetMarkerX(
            float currentDistance,
            float threshold,
            float left,
            float stop,
            float right)
        {
            if (threshold <= 0.001f)
                return right;
            float ratio = Math.Max(0f, currentDistance) / threshold;
            if (ratio <= 1f)
                return left + ((stop - left) * ratio);
            return stop + ((right - stop) * Math.Min(1f, (ratio - 1f) * 0.5f));
        }

        private static void AddBehaviorLogicMovementSurface(
            Fgui.GComponent host,
            string name,
            float x,
            float y,
            float width,
            float height,
            bool emphasized)
        {
            var surface = new Fgui.GGraph
            {
                name = name,
                touchable = false,
                sortingOrder = 1
            };
            surface.SetXY(x, y);
            surface.SetSize(width, height);
            surface.DrawRect(
                width,
                height,
                emphasized ? 2 : 1,
                emphasized
                    ? RuntimeFrameSrgb(42, 188, 184, 220)
                    : RuntimeFrameSrgb(42, 78, 87, 210),
                emphasized
                    ? RuntimeFrameSrgb(8, 48, 55, 238)
                    : RuntimeFrameSrgb(7, 27, 34, 236));
            host.AddChild(surface);
        }

        private static void AddBehaviorLogicMovementRect(
            Fgui.GComponent host,
            string name,
            float x,
            float y,
            float width,
            float height,
            Color color,
            int sortingOrder)
        {
            var graph = new Fgui.GGraph
            {
                name = name,
                touchable = false,
                sortingOrder = sortingOrder
            };
            graph.SetXY(x, y);
            graph.SetSize(width, height);
            graph.DrawRect(width, height, 0, Color.clear, color);
            host.AddChild(graph);
        }

        private static void AddBehaviorLogicMovementEllipse(
            Fgui.GComponent host,
            string name,
            float x,
            float y,
            float width,
            float height,
            Color color,
            int sortingOrder,
            bool visible = true)
        {
            var graph = new Fgui.GGraph
            {
                name = name,
                touchable = false,
                sortingOrder = sortingOrder,
                visible = visible
            };
            graph.SetXY(x, y);
            graph.SetSize(width, height);
            graph.DrawEllipse(width, height, color);
            host.AddChild(graph);
        }

        private static void HideBehaviorLogicStageActions(Fgui.GComponent details)
        {
            SetTimelineChildVisible(details, "btnLocateStart", false);
            SetTimelineChildVisible(details, "btnLocateEnd", false);
        }

        private static void HideBehaviorLogicInspectorRows(Fgui.GComponent details)
        {
            string[] names =
            {
                "planActionStartLabel", "planActionStartValue",
                "planActionDurationLabel", "planActionDurationValue",
                "planActionCompleteLabel", "planActionCompleteValue",
                "planActionFailureLabel", "planActionFailureValue"
            };
            for (int i = 0; i < names.Length; i++)
                SetTimelineChildVisible(details, names[i], false);
            SetBehaviorLogicInspectorFieldSurfacesVisible(details, false);
        }

        private Fgui.GTextInput AddBehaviorLogicMovementInput(
            Fgui.GComponent host,
            string name,
            string value,
            string prompt,
            float x,
            float y,
            float width,
            float height,
            bool enabled)
        {
            var input = new Fgui.GTextInput
            {
                name = name,
                text = value ?? string.Empty,
                promptText = prompt ?? string.Empty,
                color = RuntimeFrameSrgb(231, 246, 247),
                align = Fgui.AlignType.Center,
                verticalAlign = Fgui.VertAlignType.Middle,
                singleLine = true,
                border = 1,
                corner = 4,
                borderColor = RuntimeFrameSrgb(54, 103, 111, enabled ? (byte)235 : (byte)130),
                backgroundColor = RuntimeFrameSrgb(7, 34, 42, enabled ? (byte)245 : (byte)175),
                editable = enabled,
                touchable = enabled,
                grayed = !enabled,
                sortingOrder = 6,
                maxLength = 16
            };
            input.SetXY(x, y);
            input.SetSize(width, height);
            SetTimelineFontSize(input, 21);
            input.onTouchBegin.Set(context =>
            {
                input.RequestFocus();
                context.StopPropagation();
            });
            input.onFocusIn.Set(() =>
                _activeBehaviorLogicTypedInput = input);
            input.onKeyDown.Set(context => context.StopPropagation());
            host.AddChild(input);
            return input;
        }

        private void BindBehaviorLogicMovementInputCommit(
            Fgui.GTextInput input,
            Action commit)
        {
            if (input == null)
                return;
            input.onSubmit.Set(() => commit?.Invoke());
            input.onFocusOut.Set(() =>
            {
                if (ReferenceEquals(_activeBehaviorLogicTypedInput, input))
                    _activeBehaviorLogicTypedInput = null;
                commit?.Invoke();
            });
        }

        private static Fgui.GTextField AddBehaviorLogicMovementText(
            Fgui.GComponent host,
            string name,
            string value,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color)
        {
            var text = new Fgui.GTextField
            {
                name = name,
                text = value ?? string.Empty,
                color = color,
                singleLine = true,
                verticalAlign = Fgui.VertAlignType.Middle,
                touchable = false,
                sortingOrder = 7
            };
            text.SetXY(x, y);
            text.SetSize(width, height);
            SetTimelineFontSize(text, fontSize);
            host.AddChild(text);
            return text;
        }

        private static bool TryParseBehaviorLogicMovementFloat(string value, out float result)
        {
            return float.TryParse(
                    value?.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result)
                && !float.IsNaN(result)
                && !float.IsInfinity(result);
        }

        private static void SetBehaviorLogicMovementInputError(
            Fgui.GComponent host,
            string message)
        {
            Fgui.GTextField error = host?.GetChild("movementInputError")?.asTextField;
            if (error != null)
                error.text = message ?? string.Empty;
            if (Application.isPlaying)
                Fgui.Stage.inst?.ForceUpdate();
        }

        private static void ClearBehaviorLogicMovementInputError(Fgui.GComponent host)
        {
            Fgui.GTextField error = host?.GetChild("movementInputError")?.asTextField;
            if (error != null)
                error.text = string.Empty;
        }

    }
}
