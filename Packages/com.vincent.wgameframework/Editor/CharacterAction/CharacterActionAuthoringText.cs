using System;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionAuthoringText
    {
        internal static string EnumDisplayLabel(Type enumType, string rawValue)
        {
            string label = LocalizeEnumValue(enumType, rawValue);
            if (string.IsNullOrWhiteSpace(rawValue))
                return label;
            return string.Equals(label, rawValue, StringComparison.Ordinal)
                ? rawValue
                : label + " (" + rawValue + ")";
        }

        internal static string MotionEventKindDisplayLabel(string rawValue)
        {
            return EnumDisplayLabel(typeof(CharacterActionTrackEventKind), rawValue);
        }

        internal static string PhaseSummary(CharacterActionPhaseJsonDto phase)
        {
            phase = phase ?? new CharacterActionPhaseJsonDto();
            return LocalizeEnumValue(typeof(CharacterActionPhaseKind), phase.Kind) + "阶段";
        }

        internal static string MotionSummary(MotionTrackEventJsonDto evt, bool compact = false)
        {
            evt = evt ?? new MotionTrackEventJsonDto();
            string kind = MotionEventKindDisplayLabel(evt.Kind);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SetControlScale.ToString(), StringComparison.Ordinal))
            {
                string movement = evt.MovementControlScale.HasValue ? FormatNumber(evt.MovementControlScale.Value) : "未设置";
                string rotation = evt.RotationControlScale.HasValue ? FormatNumber(evt.RotationControlScale.Value) : "未设置";
                return "控制倍率：移动=" + movement + "，旋转=" + rotation;
            }

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal))
            {
                string profile = CharacterActionAuthoringIds.Label(evt.CollisionProfileId, 402, "unset");
                string restore = string.IsNullOrWhiteSpace(evt.CollisionRestorePolicy)
                    ? CharacterActionCollisionProfileRestorePolicy.OnActionEnd.ToString()
                    : evt.CollisionRestorePolicy;
                return "Authority collision profile: " + profile
                    + ", restore=" + restore
                    + ", transitionFrames=" + evt.CollisionTransitionFrames.ToString(CultureInfo.InvariantCulture);
            }

            string mode = LocalizeEnumValue(typeof(CharacterMovementMode), evt.MovementMode);
            string space = LocalizeMotionSpace(evt.Space);
            string vector = VectorText(evt.X, evt.Y, evt.Z);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.ApplyImpulse.ToString(), StringComparison.Ordinal))
                return kind + "：空间=" + space + "，冲量=" + vector;
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SetVerticalVelocity.ToString(), StringComparison.Ordinal))
                return "垂直速度=" + FormatNumber(evt.Y) + " 米/秒";
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.IgnoreGravity.ToString(), StringComparison.Ordinal))
                return "忽略重力 " + FrameRangeLabel(evt.Frame, Math.Max(evt.Frame, evt.EndFrame));
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SampleRootMotionY.ToString(), StringComparison.Ordinal))
                return "采样动画 Y 轴根运动 " + FrameRangeLabel(evt.Frame, Math.Max(evt.Frame, evt.EndFrame));

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SetMovementMode.ToString(), StringComparison.Ordinal)
                || string.Equals(evt.Kind, CharacterActionTrackEventKind.LockMovement.ToString(), StringComparison.Ordinal))
            {
                return kind + "：移动模式=" + mode;
            }

            return kind;
        }

        internal static string RootMotionSummary(RootMotionTrackJsonDto rootMotion, bool compact = false)
        {
            rootMotion = rootMotion ?? new RootMotionTrackJsonDto();
            RootMotionFrameJsonDto[] frames = rootMotion.Frames ?? Array.Empty<RootMotionFrameJsonDto>();
            if (frames.Length == 0)
                return compact ? "RootMotion：无采样" : "RootMotion：采样=0";

            float x = 0f;
            float y = 0f;
            float z = 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                RootMotionFrameJsonDto frame = frames[i] ?? new RootMotionFrameJsonDto();
                x += frame.X;
                y += frame.Y;
                z += frame.Z;
            }

            string obstaclePolicy = string.Equals(
                rootMotion.ObstaclePolicy,
                CharacterActionRootMotionObstaclePolicy.StaticOnly.ToString(),
                StringComparison.Ordinal)
                ? " StaticOnly"
                : string.Empty;
            if (compact && !string.IsNullOrEmpty(obstaclePolicy))
                return "RootMotion StaticOnly delta=" + VectorText(x, y, z);
            if (compact)
                return "RootMotion 位移=" + VectorText(x, y, z);
            return "RootMotion：采样=" + frames.Length.ToString(CultureInfo.InvariantCulture)
                + "，总位移=" + VectorText(x, y, z)
                + "，来源=" + EmptyAsUnset(rootMotion.SourceAnimationClipResourceKey);
        }

        internal static string AnimationSummary(AnimationTrackEventJsonDto evt, bool compact = false)
        {
            evt = evt ?? new AnimationTrackEventJsonDto();
            string kind = LocalizeEnumValue(typeof(CharacterActionTrackEventKind), evt.Kind);
            string key = EmptyAsUnset(CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                evt.AnimationClipResourceKey,
                evt.AnimationActionKey));
            if (compact)
                return kind + "：" + key;

            int transitionFrames = Timeline.CharacterActionTimelineLayout.TransitionFramesFromSeconds(evt.TransitionSeconds);
            float speed = evt.PlaybackSpeed <= 0.0001f ? 1f : evt.PlaybackSpeed;
            return kind + "：动画=" + key
                + "，速度=" + FormatNumber(speed) + "x"
                + "，起始偏移=" + Math.Max(0, evt.AnimationStartOffsetFrames) + " 帧"
                + "，结束偏移=" + Math.Max(0, evt.AnimationEndOffsetFrames) + " 帧"
                + "，过渡=" + transitionFrames + " 帧";
        }

        internal static string CombatSummary(CombatTrackEventJsonDto evt, bool compact = false)
        {
            evt = evt ?? new CombatTrackEventJsonDto();
            string kind = LocalizeEnumValue(typeof(CharacterActionTrackEventKind), evt.Kind);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.StartCombatAction.ToString(), StringComparison.Ordinal))
                return kind + "：战斗动作=" + EmptyAsUnset(evt.CombatActionId);

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SetHitTargetState.ToString(), StringComparison.Ordinal))
            {
                HitTargetStateFlags flags = (HitTargetStateFlags)evt.TargetStateMask;
                return kind + "：" + flags + " " + FrameRangeLabel(evt.Frame, Math.Max(evt.Frame, evt.EndFrame));
            }

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.HitTraceWindow.ToString(), StringComparison.Ordinal))
            {
                if (HasHitVolume(evt))
                {
                    string hitVolumeSummary = kind + "：" + HitVolumeSourceLabel(evt.HitVolume)
                        + "=" + HitVolumeSummary(evt.HitVolume, includePoints: true);
                    hitVolumeSummary += "，" + DamageSummary(evt);
                    return hitVolumeSummary;
                }

                string summary = evt.EquipmentSlotId <= 0
                    && evt.TraceProfileId <= 0
                    ? kind + "：命中判定窗口=当前动作实例"
                    : kind + "：武器轨迹判定，装备槽位="
                        + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.EquipmentSlotId, 405));
                summary += "，" + DamageSummary(evt);
                if (evt.TraceProfileId > 0)
                    summary += "，判定配置=" + CharacterActionAuthoringIds.Label(evt.TraceProfileId, 403);
                return summary;
            }

            return kind;
        }

        internal static string HitTraceWindowSummary(
            CombatTrackEventJsonDto trackEvent,
            bool compact = false)
        {
            trackEvent = trackEvent ?? new CombatTrackEventJsonDto();
            string slot = CharacterActionAuthoringIds.Label(trackEvent.EquipmentSlotId, 405);
            string trace = CharacterActionAuthoringIds.Label(trackEvent.TraceProfileId, 403);
            if (HasHitVolume(trackEvent))
            {
                return compact
                    ? HitVolumeSourceLabel(trackEvent.HitVolume) + "：" + HitVolumeSummary(trackEvent.HitVolume, includePoints: false)
                    : HitVolumeSourceLabel(trackEvent.HitVolume) + "："
                        + HitVolumeSourceEnglishLabel(trackEvent.HitVolume)
                        + "，"
                        + HitVolumeSummary(trackEvent.HitVolume, includePoints: true)
                        + "，" + DamageSummary(trackEvent);
            }

            if (compact)
                return "武器轨迹判定：" + EmptyAsUnset(slot);
            string summary = "武器轨迹判定：装备槽位=" + EmptyAsUnset(slot)
                + "，" + DamageSummary(trackEvent);
            if (trackEvent.TraceProfileId > 0)
                summary += "，判定配置=" + trace;
            return summary;
        }

        internal static string TransitionWindowSummary(CharacterCancelRuleJsonDto rule, bool compact = false)
        {
            rule = rule ?? new CharacterCancelRuleJsonDto();
            string requestTypes = FormatMask((CharacterActionRequestType)rule.RequestTypeMask);
            string slots = FormatMask((CharacterCancelInputMask)rule.ActionSlotMask);
            if (compact)
                return "切换窗口：请求=" + requestTypes + " / Slot=" + slots;

            return "切换窗口 / Transition Window："
                + FrameRangeLabel(rule.StartFrame, Math.Max(rule.StartFrame, rule.EndFrame))
                + "，请求类型=" + requestTypes
                + "，Slot=" + slots;
        }

        private static string FormatMask<T>(T value) where T : struct
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0
                ? "任意"
                : value.ToString();
        }

        internal static string DamageSummary(CombatTrackEventJsonDto evt)
        {
            if (evt == null)
                return "伤害=未设置";

            CharacterActionHitDamageJsonDto hitEventDamage = evt.HitEvent?.Damage;
            if (HasHitEventDamage(hitEventDamage))
            {
                return "伤害=" + CharacterActionDamageAuthoringText.FormatDamageSummary(
                    CharacterActionAuthoringIds.Label(hitEventDamage.DamageTypeId, 409, hitEventDamage.DamageTypeLabel),
                    hitEventDamage.BaseDamage,
                    hitEventDamage.ImpactPower,
                    CharacterActionHitEventAuthoringModel.ResolveAttackScale(hitEventDamage.SourceAttributeScales),
                    hitEventDamage.LaunchPower);
            }

            return "伤害=未设置";
        }

        internal static string PresentationSummary(PresentationTrackEventJsonDto evt, bool compact = false)
        {
            evt = evt ?? new PresentationTrackEventJsonDto();
            string kind = LocalizeEnumValue(typeof(CharacterActionTrackEventKind), evt.Kind);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SpawnVisualCue.ToString(), StringComparison.Ordinal))
                return kind + "：" + EmptyAsUnset(evt.ResourceKey);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.PlayAudioCue.ToString(), StringComparison.Ordinal))
                return kind + "：" + EmptyAsUnset(evt.CueId);
            return kind + "：音效=" + EmptyAsUnset(evt.CueId) + "，资源=" + EmptyAsUnset(evt.ResourceKey);
        }

        internal static string LocalizeEnumValue(Type enumType, string rawValue)
        {
            rawValue = rawValue ?? string.Empty;
            if (enumType == typeof(CharacterActionTrackEventKind))
                return LocalizeTrackEventKind(rawValue);
            if (enumType == typeof(CharacterMovementMode))
                return LocalizeMovementMode(rawValue);
            if (enumType == typeof(CharacterActionPhaseKind))
                return LocalizePhaseKind(rawValue);
            if (enumType == typeof(CharacterActionSourceKind))
                return LocalizeActionSourceKind(rawValue);
            if (enumType == typeof(CharacterActionHitVolumeSourceKind))
                return LocalizeHitVolumeSourceKind(rawValue);
            if (enumType == typeof(CharacterActionHitVolumeShapeKind))
                return LocalizeHitVolumeShapeKind(rawValue);
            return string.IsNullOrWhiteSpace(rawValue) ? "未设置" : rawValue;
        }

        internal static string FrameRangeLabel(int startFrame, int endFrame)
        {
            endFrame = Math.Max(startFrame, endFrame);
            return startFrame == endFrame
                ? "第 " + startFrame + " 帧"
                : "第 " + startFrame + "-" + endFrame + " 帧";
        }

        private static bool HasHitEventDamage(CharacterActionHitDamageJsonDto damage)
        {
            return damage != null
                && (damage.DamageTypeId > 0
                    || damage.FormulaId > 0
                    || damage.BaseDamage > 0
                    || damage.ImpactPower > 0
                    || damage.LaunchPower > 0
                    || (damage.SourceAttributeScales != null && damage.SourceAttributeScales.Length > 0));
        }

        private static bool HasHitVolume(CombatTrackEventJsonDto evt)
        {
            return evt?.HitVolume != null
                && (IsCasterBodyHitVolume(evt.HitVolume) || evt.HitVolume.Radius > 0f);
        }

        private static string HitVolumeSummary(CharacterActionHitVolumeJsonDto hitVolume, bool includePoints)
        {
            if (IsCasterBodyHitVolume(hitVolume))
                return CasterBodySummary(hitVolume, includePoints);

            if (hitVolume == null)
                return "未设置";

            if (IsCasterBodyHitVolume(hitVolume))
                return includePoints
                    ? "来自施法者角色物理体"
                    : "角色身体";

            string summary = EmptyAsUnset(hitVolume.ShapeKind)
                + " r=" + FormatNumber(hitVolume.Radius);
            if (!includePoints)
                return summary;

            return summary
                + " A=" + HitVolumePointSummary(hitVolume.LocalPointA)
                + " B=" + HitVolumePointSummary(hitVolume.LocalPointB);
        }

        private static string HitVolumeSourceLabel(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return IsCasterBodyHitVolume(hitVolume)
                ? "身体撞击判定"
                : "碰撞框判定";
        }

        private static string HitVolumeSourceEnglishLabel(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return IsCasterBodyHitVolume(hitVolume)
                ? "Caster Body"
                : "Action Hit Volume";
        }

        private static string CasterBodySummary(CharacterActionHitVolumeJsonDto hitVolume, bool includeDetails)
        {
            hitVolume = hitVolume ?? new CharacterActionHitVolumeJsonDto();
            string summary = includeDetails
                ? "来自施法者角色物理体"
                : "角色身体";
            CharacterActionHitVolumePointJsonDto offset = hitVolume.BodyOffset ?? new CharacterActionHitVolumePointJsonDto();
            if (includeDetails
                || !offset.X.Equals(0f)
                || !offset.Y.Equals(0f)
                || !offset.Z.Equals(0f))
                summary += " offset=" + HitVolumePointSummary(offset);
            if (includeDetails || !hitVolume.BodyRadiusScale.Equals(1f))
                summary += " rScale=" + FormatNumber(hitVolume.BodyRadiusScale <= 0f ? 1f : hitVolume.BodyRadiusScale);
            if (includeDetails || !hitVolume.BodyRadiusAdd.Equals(0f))
                summary += " rAdd=" + FormatNumber(hitVolume.BodyRadiusAdd);
            if (includeDetails || !hitVolume.BodyHeightScale.Equals(1f))
                summary += " hScale=" + FormatNumber(hitVolume.BodyHeightScale <= 0f ? 1f : hitVolume.BodyHeightScale);
            if (includeDetails || !hitVolume.BodyHeightAdd.Equals(0f))
                summary += " hAdd=" + FormatNumber(hitVolume.BodyHeightAdd);
            return summary;
        }

        private static bool IsCasterBodyHitVolume(CharacterActionHitVolumeJsonDto hitVolume)
        {
            return string.Equals(
                hitVolume?.SourceKind,
                CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                StringComparison.Ordinal);
        }

        private static string HitVolumePointSummary(CharacterActionHitVolumePointJsonDto point)
        {
            point = point ?? new CharacterActionHitVolumePointJsonDto();
            return "("
                + FormatNumber(point.X)
                + ", "
                + FormatNumber(point.Y)
                + ", "
                + FormatNumber(point.Z)
                + ")";
        }

        private static float ResolveAttackScale(CharacterActionDamageAttributeScaleJsonDto[] scales)
        {
            scales = scales ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
            for (int i = 0; i < scales.Length; i++)
            {
                CharacterActionDamageAttributeScaleJsonDto scale = scales[i];
                if (scale != null && scale.AttributeId == 2)
                    return scale.Scale;
            }

            return 0f;
        }

        private static string LocalizeTrackEventKind(string value)
        {
            switch (value)
            {
                case nameof(CharacterActionTrackEventKind.None): return "未设置";
                case nameof(CharacterActionTrackEventKind.SetMovementMode): return "设置移动模式";
                case nameof(CharacterActionTrackEventKind.ApplyImpulse): return "施加运动冲量";
                case nameof(CharacterActionTrackEventKind.LockMovement): return "锁定移动";
                case nameof(CharacterActionTrackEventKind.SetControlScale): return "设置控制倍率";
                case nameof(CharacterActionTrackEventKind.SetVerticalVelocity): return "持续垂直速度";
                case nameof(CharacterActionTrackEventKind.IgnoreGravity): return "忽略重力";
                case nameof(CharacterActionTrackEventKind.SampleRootMotionY): return "采样动画 Y 轴根运动";
                case nameof(CharacterActionTrackEventKind.StartCombatAction): return "启动战斗动作";
                case nameof(CharacterActionTrackEventKind.HitTraceWindow): return "命中判定窗口";
                case nameof(CharacterActionTrackEventKind.SetHitTargetState): return "设置受击状态";
                case nameof(CharacterActionTrackEventKind.SendGameplayRequest): return "发送玩法请求";
                case nameof(CharacterActionTrackEventKind.CastAbility): return "释放能力";
                case nameof(CharacterActionTrackEventKind.ApplyGameplayEffect): return "应用玩法效果";
                case nameof(CharacterActionTrackEventKind.PlayAnimation): return "播放动画";
                case nameof(CharacterActionTrackEventKind.CrossFadeAnimation): return "过渡到动画";
                case nameof(CharacterActionTrackEventKind.SetAnimationBlend): return "设置动画混合";
                case nameof(CharacterActionTrackEventKind.PlayAudioCue): return "播放音效";
                case nameof(CharacterActionTrackEventKind.SpawnVisualCue): return "生成特效";
                case nameof(CharacterActionTrackEventKind.CameraImpulse): return "镜头震动";
                case nameof(CharacterActionTrackEventKind.UiFeedback): return "UI 反馈";
                case nameof(CharacterActionTrackEventKind.EmitDebugMarker): return "输出调试标记";
                default: return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
            }
        }

        private static string LocalizeMovementMode(string value)
        {
            switch (value)
            {
                case nameof(CharacterMovementMode.Idle): return "待机";
                case nameof(CharacterMovementMode.Walk): return "行走";
                case nameof(CharacterMovementMode.Run): return "奔跑";
                case nameof(CharacterMovementMode.Strafe): return "横移";
                case nameof(CharacterMovementMode.TurnInPlace): return "原地转身";
                case nameof(CharacterMovementMode.ApproachTarget): return "接近目标";
                case nameof(CharacterMovementMode.Retreat): return "后退";
                case nameof(CharacterMovementMode.CircleTarget): return "绕目标移动";
                case nameof(CharacterMovementMode.RootMotionDriven): return "Root Motion 驱动";
                case nameof(CharacterMovementMode.Airborne): return "空中";
                case nameof(CharacterMovementMode.ControlLocked): return "控制锁定";
                default: return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
            }
        }

        private static string LocalizePhaseKind(string value)
        {
            switch (value)
            {
                case nameof(CharacterActionPhaseKind.None): return "未设置";
                case nameof(CharacterActionPhaseKind.Startup): return "前摇";
                case nameof(CharacterActionPhaseKind.Active): return "生效";
                case nameof(CharacterActionPhaseKind.Recovery): return "收招";
                case nameof(CharacterActionPhaseKind.Loop): return "循环";
                case nameof(CharacterActionPhaseKind.Airborne): return "腾空";
                case nameof(CharacterActionPhaseKind.Landing): return "落地";
                case nameof(CharacterActionPhaseKind.Channel): return "引导";
                case nameof(CharacterActionPhaseKind.Hold): return "保持";
                case nameof(CharacterActionPhaseKind.Exit): return "退出";
                case nameof(CharacterActionPhaseKind.Custom): return "自定义";
                default: return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
            }
        }

        private static string LocalizeActionSourceKind(string value)
        {
            switch (value)
            {
                case nameof(CharacterActionSourceKind.None): return "未设置";
                case nameof(CharacterActionSourceKind.Command): return "命令";
                case nameof(CharacterActionSourceKind.GameplayAbility): return "玩法能力";
                case nameof(CharacterActionSourceKind.LocalInput): return "本地输入";
                case nameof(CharacterActionSourceKind.RuntimeAiPlanner): return "Runtime AI Planner";
                case nameof(CharacterActionSourceKind.Replay): return "回放";
                case nameof(CharacterActionSourceKind.Scripted): return "脚本";
                case nameof(CharacterActionSourceKind.CohesionBreak): return "韧性崩解";
                case nameof(CharacterActionSourceKind.GuardBreak): return "破防";
                case nameof(CharacterActionSourceKind.ArmorBreak): return "破甲";
                case nameof(CharacterActionSourceKind.Hit): return "受击";
                case nameof(CharacterActionSourceKind.Death): return "死亡";
                case nameof(CharacterActionSourceKind.Reaction): return "反应";
                case nameof(CharacterActionSourceKind.PlayerIntervention): return "玩家介入";
                case nameof(CharacterActionSourceKind.Debug): return "调试";
                default: return string.IsNullOrWhiteSpace(value) ? "命令" : value;
            }
        }

        private static CharacterActionSourceKind ResolveSourceKind(string value)
        {
            return Enum.TryParse(value, out CharacterActionSourceKind sourceKind)
                ? sourceKind
                : CharacterActionSourceKind.Command;
        }

        private static string LocalizeHitVolumeSourceKind(string value)
        {
            switch (value)
            {
                case nameof(CharacterActionHitVolumeSourceKind.ManualCapsule): return "手动 Capsule";
                case nameof(CharacterActionHitVolumeSourceKind.CasterBody): return "角色身体";
                default: return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
            }
        }

        private static string LocalizeHitVolumeShapeKind(string value)
        {
            switch (value)
            {
                case nameof(CharacterActionHitVolumeShapeKind.None): return "未设置";
                case nameof(CharacterActionHitVolumeShapeKind.Capsule): return "Capsule";
                default: return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
            }
        }

        private static string LocalizeMotionSpace(string value)
        {
            if (string.Equals(value, CharacterActionMotionVectorSpace.Local.ToString(), StringComparison.Ordinal))
                return "本地";
            if (string.Equals(value, CharacterActionMotionVectorSpace.World.ToString(), StringComparison.Ordinal))
                return "世界";
            return string.IsNullOrWhiteSpace(value) ? "世界" : value;
        }

        private static string EmptyAsUnset(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

        private static string EmptyAsUnset(int value)
        {
            return value <= 0 ? "未设置" : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string VectorText(float x, float y, float z)
        {
            return "(" + FormatNumber(x) + ", " + FormatNumber(y) + ", " + FormatNumber(z) + ")";
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
