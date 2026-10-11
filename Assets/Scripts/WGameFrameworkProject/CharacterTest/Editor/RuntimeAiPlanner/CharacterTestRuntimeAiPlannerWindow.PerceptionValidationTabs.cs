using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildPerceptionTab(VisualElement root)
        {
            root.Add(CreateSectionTitle("感知"));
            TextField profileId = CreateDelayedTextField("Profile Id", _model.ProfileId);
            profileId.RegisterValueChangedCallback(evt =>
            {
                _model.ProfileId = evt.newValue ?? string.Empty;
                MarkDirty("已修改 Profile Id");
            });
            root.Add(profileId);

            root.Add(CreateStatePopup("初始状态", _model.InitialStateId, false, value =>
            {
                _model.InitialStateId = value;
                MarkDirty("已修改初始状态");
            }));

            EnumField targetMode = new EnumField("目标选择", _model.TargetSelectionMode);
            targetMode.RegisterValueChangedCallback(evt =>
            {
                _model.TargetSelectionMode = (CharacterTestAiTargetSelectionMode)(object)evt.newValue;
                MarkDirty("已修改目标选择");
            });
            root.Add(targetMode);

            FloatField attackRange = new FloatField("攻击距离") { value = _model.AttackRange, isDelayed = true };
            attackRange.RegisterValueChangedCallback(evt =>
            {
                _model.AttackRange = evt.newValue;
                MarkDirty("已修改攻击距离");
            });
            root.Add(attackRange);

            FloatField visionRange = new FloatField("视野距离") { value = _model.VisionRange, isDelayed = true };
            visionRange.RegisterValueChangedCallback(evt =>
            {
                _model.VisionRange = evt.newValue;
                MarkDirty("已修改视野距离");
            });
            root.Add(visionRange);

            IntegerField memoryFrames = new IntegerField("目标记忆帧数")
            {
                value = _model.TargetMemoryFrames,
                isDelayed = true
            };
            memoryFrames.RegisterValueChangedCallback(evt =>
            {
                _model.TargetMemoryFrames = evt.newValue;
                MarkDirty("已修改目标记忆帧数");
            });
            root.Add(memoryFrames);

            IntegerField lineOfSightMask = new IntegerField("视线遮挡层 Mask")
            {
                value = _model.LineOfSightObstacleLayerMask,
                isDelayed = true
            };
            lineOfSightMask.RegisterValueChangedCallback(evt =>
            {
                _model.LineOfSightObstacleLayerMask = evt.newValue;
                MarkDirty("已修改视线遮挡层 Mask");
            });
            root.Add(lineOfSightMask);

            FloatField environmentRadius = new FloatField("环境采样半径")
            {
                value = _model.EnvironmentSampleRadius,
                isDelayed = true
            };
            environmentRadius.RegisterValueChangedCallback(evt =>
            {
                _model.EnvironmentSampleRadius = evt.newValue;
                MarkDirty("已修改环境采样半径");
            });
            root.Add(environmentRadius);

            FloatField threatWeight = new FloatField("威胁距离权重")
            {
                value = _model.ThreatDistanceWeight,
                isDelayed = true
            };
            threatWeight.RegisterValueChangedCallback(evt =>
            {
                _model.ThreatDistanceWeight = evt.newValue;
                MarkDirty("已修改威胁距离权重");
            });
            root.Add(threatWeight);

            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("移动风格默认值"));
            AddProfileMovementFloatField(
                root,
                "速度倍率",
                _model.MovementSpeedScale,
                value => _model.MovementSpeedScale = value);
            AddProfileMovementFloatField(
                root,
                "转向响应",
                _model.MovementSteeringResponsiveness,
                value => _model.MovementSteeringResponsiveness = value);
            AddProfileMovementFloatField(
                root,
                "接近侧向混合",
                _model.MovementApproachLateralBlend,
                value => _model.MovementApproachLateralBlend = value);
            AddProfileMovementFloatField(
                root,
                "偏好距离最小",
                _model.MovementPreferredDistanceMin,
                value => _model.MovementPreferredDistanceMin = value);
            AddProfileMovementFloatField(
                root,
                "偏好距离最大",
                _model.MovementPreferredDistanceMax,
                value => _model.MovementPreferredDistanceMax = value);
            AddProfileMovementFloatField(
                root,
                "到达减速距离",
                _model.MovementArrivalSlowdownDistance,
                value => _model.MovementArrivalSlowdownDistance = value);
            AddProfileMovementIntegerField(
                root,
                "侧移换边最小帧",
                _model.MovementStrafeSwitchMinFrames,
                value => _model.MovementStrafeSwitchMinFrames = value);
            AddProfileMovementIntegerField(
                root,
                "侧移换边最大帧",
                _model.MovementStrafeSwitchMaxFrames,
                value => _model.MovementStrafeSwitchMaxFrames = value);
            AddProfileMovementIntegerField(
                root,
                "方向保持帧",
                _model.MovementDirectionHoldFrames,
                value => _model.MovementDirectionHoldFrames = value);
            AddProfileMovementIntegerField(
                root,
                "反应延迟帧",
                _model.MovementReactionDelayFrames,
                value => _model.MovementReactionDelayFrames = value);

            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("Facts 清单"));
            root.Add(CreateReadOnlyLine("target.exists", "是否找到目标"));
            root.Add(CreateReadOnlyLine("target.visible", "目标是否在视野内且未被遮挡"));
            root.Add(CreateReadOnlyLine("target.lineOfSightBlocked", "目标视线是否被障碍阻挡"));
            root.Add(CreateReadOnlyLine("target.lastSeenAgeFrames", "距上次看见目标的帧数"));
            root.Add(CreateReadOnlyLine("target.inAttackRange", "目标是否在攻击距离内"));
            root.Add(CreateReadOnlyLine("target.distance", "目标距离"));
            root.Add(CreateReadOnlyLine("target.threatScore", "按距离和可见性计算的威胁分"));
            root.Add(CreateReadOnlyLine("self.actionRunning", "自身动作是否运行中"));
            root.Add(CreateReadOnlyLine("self.surfaceKind", "当前地表材质枚举值"));
            root.Add(CreateReadOnlyLine("self.onHardSlick", "是否位于硬滑地表"));
            root.Add(CreateReadOnlyLine("self.nearMovableObject", "附近是否有可移动自然物体"));
            root.Add(CreateReadOnlyLine("move.pathAvailable", "是否存在移动路径"));
            root.Add(CreateReadOnlyLine("move.directPathClear", "直线路径是否无障碍"));
            root.Add(CreateReadOnlyLine("move.candidateIndex", "绕行候选方向索引"));
            root.Add(CreateReadOnlyLine("environment.nearObstacle", "附近是否有静态障碍"));
            root.Add(CreateReadOnlyLine("environment.nearHazard", "附近是否有危险环境对象"));
            root.Add(CreateReadOnlyLine("environment.nearUsefulObject", "附近是否有可利用环境对象"));
        }

        private static void BuildPerceptionInspector(VisualElement root)
        {
            root.Add(CreateSectionTitle("感知 Inspector"));
            root.Add(CreateNotice("感知与移动风格参数保存到 JSON profile。Play Mode 不热更新，重建 CharacterTest 会话后生效。"));
        }

        private void AddProfileMovementFloatField(
            VisualElement root,
            string label,
            float value,
            Action<float> setter)
        {
            FloatField field = new FloatField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty("已修改 Profile 移动风格");
            });
            root.Add(field);
        }

        private void AddProfileMovementIntegerField(
            VisualElement root,
            string label,
            int value,
            Action<int> setter)
        {
            IntegerField field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty("已修改 Profile 移动风格");
            });
            root.Add(field);
        }

        private void BuildValidationTab(VisualElement root)
        {
            root.Add(CreateSectionTitle("校验"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            _lastReport = report.ToText();
            root.Add(CreateReportField(_lastReport));
        }

        private void BuildValidationInspector(VisualElement root)
        {
            root.Add(CreateSectionTitle("诊断"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            if (report.Diagnostics.Count == 0)
            {
                root.Add(new Label("校验通过。"));
                return;
            }

            for (int i = 0; i < report.Diagnostics.Count; i++)
                root.Add(new Label(report.Diagnostics[i].ToString()));
        }

    }
}
