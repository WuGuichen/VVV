using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class TrackEventTimelineAdapter : ITimelineItemAdapter, IVfxTimelineClipAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly TimelineTrackKind _trackKind;
        private readonly object _eventRef;
        private readonly string _stableEventId;
        private readonly string _id;
        private readonly Color _color;

        public TrackEventTimelineAdapter(
            CharacterActionJsonDto action,
            TimelineTrackKind trackKind,
            int index,
            Color color,
            bool canEdit)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _trackKind = trackKind;
            _eventRef = GetEventAt(index);
            _stableEventId = GetStableEventId(_eventRef);
            _id = CharacterActionTimelineIdentity.ItemId(_trackKind, _eventRef, _stableEventId);
            _color = color;
            CanEdit = canEdit && !(_eventRef is PresentationTrackEventJsonDto generated && generated.BladeTrailGenerated);
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => _trackKind;
        public TimelineItemKind ItemKind => (_trackKind == TimelineTrackKind.Presentation
            && IsPresentationRangeEvent(GetPresentation()))
            || (_trackKind == TimelineTrackKind.Motion && IsMotionRangeEvent(GetMotion()))
            || (_trackKind == TimelineTrackKind.Combat && IsHitTargetStateRange(GetCombat()))
            ? TimelineItemKind.Clip
            : TimelineItemKind.Marker;
        public bool CanEdit { get; }
        public bool AllowsTailBeyondAction => IsSpawnVisualCue(GetPresentation());

        public int StartFrame => GetFrame();
        public int EndFrame
        {
            get
            {
                if (_trackKind == TimelineTrackKind.Presentation)
                {
                    PresentationTrackEventJsonDto presentation = GetPresentation();
                    return IsPresentationRangeEvent(presentation)
                        ? Math.Max(presentation.Frame, presentation.EndFrame)
                        : GetFrame();
                }

                if (_trackKind == TimelineTrackKind.Combat)
                {
                    CombatTrackEventJsonDto combat = GetCombat();
                    return IsHitTargetStateRange(combat)
                        ? Math.Max(combat.Frame, combat.EndFrame)
                        : GetFrame();
                }

                if (_trackKind == TimelineTrackKind.Motion)
                {
                    MotionTrackEventJsonDto motion = GetMotion();
                    return IsMotionRangeEvent(motion)
                        ? Math.Max(motion.Frame, motion.EndFrame)
                        : GetFrame();
                }

                return GetFrame();
            }
        }

        public string Title
        {
            get
            {
                return GetSummary(compact: true);
            }
        }

        public string FullTitle
        {
            get
            {
                return GetSummary(compact: false);
            }
        }

        public Color Color => _color;

        public bool TryGetCurrentIndex(out int index)
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                    index = FindIndex(
                        _action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>(),
                        _eventRef as MotionTrackEventJsonDto,
                        _stableEventId,
                        evt => CharacterActionAuthoringIds.Label(evt.StableEventId, 401));
                    return index >= 0;
                case TimelineTrackKind.Animation:
                    index = FindIndex(
                        _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>(),
                        _eventRef as AnimationTrackEventJsonDto,
                        _stableEventId,
                        evt => CharacterActionAuthoringIds.Label(evt.StableEventId, 401));
                    return index >= 0;
                case TimelineTrackKind.Combat:
                    index = FindIndex(
                        _action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>(),
                        _eventRef as CombatTrackEventJsonDto,
                        _stableEventId,
                        evt => CharacterActionAuthoringIds.Label(evt.StableEventId, 401));
                    return index >= 0;
                case TimelineTrackKind.Gameplay:
                    index = FindIndex(
                        _action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>(),
                        _eventRef as GameplayTrackEventJsonDto,
                        _stableEventId,
                        evt => CharacterActionAuthoringIds.Label(evt.StableEventId, 401));
                    return index >= 0;
                case TimelineTrackKind.Presentation:
                    index = FindIndex(
                        _action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>(),
                        _eventRef as PresentationTrackEventJsonDto,
                        _stableEventId,
                        evt => CharacterActionAuthoringIds.Label(evt.StableEventId, 401));
                    return index >= 0;
                default:
                    index = -1;
                    return false;
            }
        }

        public void SetRange(int startFrame, int endFrame)
        {
            if (!CanEdit)
                return;
            SetRangeInternal(startFrame, endFrame);
        }

        public void SetVfxRange(int startFrame, int endFrame, bool trimSource)
        {
            if (!CanEdit)
                return;

            PresentationTrackEventJsonDto presentation = GetPresentation();
            if (!IsSpawnVisualCue(presentation))
            {
                SetRangeInternal(startFrame, endFrame);
                return;
            }

            CharacterActionVfxTimelineResolver.ApplyRange(
                presentation,
                startFrame,
                endFrame,
                trimSource);
        }

        public void Remove()
        {
            if (!CanEdit)
                return;

            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                {
                    var events = new System.Collections.Generic.List<MotionTrackEventJsonDto>(
                        _action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                    if (TryGetCurrentIndex(out int index))
                        events.RemoveAt(index);
                    if (_action.MotionTrack != null)
                        _action.MotionTrack.Events = events.ToArray();
                    break;
                }
                case TimelineTrackKind.Animation:
                {
                    var events = new System.Collections.Generic.List<AnimationTrackEventJsonDto>(
                        _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>());
                    if (TryGetCurrentIndex(out int index))
                        events.RemoveAt(index);
                    if (_action.AnimationTrack != null)
                        _action.AnimationTrack.Events = events.ToArray();
                    break;
                }
                case TimelineTrackKind.Combat:
                {
                    var events = new System.Collections.Generic.List<CombatTrackEventJsonDto>(
                        _action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                    if (TryGetCurrentIndex(out int index))
                        events.RemoveAt(index);
                    if (_action.CombatTrack != null)
                        _action.CombatTrack.Events = events.ToArray();
                    break;
                }
                case TimelineTrackKind.Gameplay:
                {
                    var events = new System.Collections.Generic.List<GameplayTrackEventJsonDto>(
                        _action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>());
                    if (TryGetCurrentIndex(out int index))
                        events.RemoveAt(index);
                    if (_action.GameplayTrack != null)
                        _action.GameplayTrack.Events = events.ToArray();
                    break;
                }
                case TimelineTrackKind.Presentation:
                {
                    var events = new System.Collections.Generic.List<PresentationTrackEventJsonDto>(
                        _action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>());
                    if (TryGetCurrentIndex(out int index))
                        events.RemoveAt(index);
                    if (_action.PresentationTrack != null)
                        _action.PresentationTrack.Events = events.ToArray();
                    break;
                }
            }
        }

        private int GetFrame()
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                    return GetMotion()?.Frame ?? 0;
                case TimelineTrackKind.Animation:
                    return GetAnimation()?.Frame ?? 0;
                case TimelineTrackKind.Combat:
                    return GetCombat()?.Frame ?? 0;
                case TimelineTrackKind.Gameplay:
                    return GetGameplay()?.Frame ?? 0;
                case TimelineTrackKind.Presentation:
                    return GetPresentation()?.Frame ?? 0;
                default:
                    return 0;
            }
        }

        private void SetRangeInternal(int startFrame, int endFrame)
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                    MotionTrackEventJsonDto motion = GetMotion();
                    if (motion != null)
                    {
                        motion.Frame = startFrame;
                        if (IsMotionRangeEvent(motion))
                            motion.EndFrame = Math.Max(startFrame, endFrame);
                    }
                    break;
                case TimelineTrackKind.Animation:
                    AnimationTrackEventJsonDto animation = GetAnimation();
                    if (animation != null)
                    {
                        animation.Frame = startFrame;
                        animation.EndFrame = Math.Max(startFrame, endFrame);
                    }
                    break;
                case TimelineTrackKind.Combat:
                    CombatTrackEventJsonDto combat = GetCombat();
                    if (combat != null)
                    {
                        combat.Frame = startFrame;
                        if (IsHitTargetStateRange(combat))
                            combat.EndFrame = Math.Max(startFrame, endFrame);
                    }
                    break;
                case TimelineTrackKind.Gameplay:
                    GameplayTrackEventJsonDto gameplay = GetGameplay();
                    if (gameplay != null)
                        gameplay.Frame = startFrame;
                    break;
                case TimelineTrackKind.Presentation:
                    PresentationTrackEventJsonDto presentation = GetPresentation();
                    if (presentation != null)
                    {
                        presentation.Frame = startFrame;
                        presentation.EndFrame = CanUsePresentationRange(presentation)
                            ? Math.Max(startFrame, endFrame)
                            : startFrame;
                    }
                    break;
            }
        }

        private string GetKind()
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                    return GetMotion()?.Kind ?? string.Empty;
                case TimelineTrackKind.Animation:
                    return GetAnimation()?.Kind ?? string.Empty;
                case TimelineTrackKind.Combat:
                    return GetCombat()?.Kind ?? string.Empty;
                case TimelineTrackKind.Gameplay:
                    return GetGameplay()?.Kind ?? string.Empty;
                case TimelineTrackKind.Presentation:
                    return GetPresentation()?.Kind ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        private string GetSummary(bool compact)
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                    return CharacterActionAuthoringText.MotionSummary(GetMotion(), compact);
                case TimelineTrackKind.Animation:
                    return CharacterActionAuthoringText.AnimationSummary(GetAnimation(), compact);
                case TimelineTrackKind.Combat:
                    return CharacterActionAuthoringText.CombatSummary(GetCombat(), compact);
                case TimelineTrackKind.Gameplay:
                    return GetGameplaySummary(GetGameplay(), compact);
                case TimelineTrackKind.Presentation:
                    return GetPresentationSummary(GetPresentation(), compact);
                default:
                    return "事件";
            }
        }

        private static string GetPresentationSummary(PresentationTrackEventJsonDto evt, bool compact)
        {
            evt = evt ?? new PresentationTrackEventJsonDto();
            string kind = CharacterActionAuthoringText.EnumDisplayLabel(
                typeof(CharacterActionTrackEventKind),
                evt.Kind);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SpawnVisualCue.ToString(), StringComparison.Ordinal))
                return kind + "：" + EmptyAsUnset(evt.ResourceKey);
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.PlayAudioCue.ToString(), StringComparison.Ordinal))
                return kind + "：" + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.CueId, 401));

            return kind
                + "：音效=" + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.CueId, 401))
                + "，资源=" + EmptyAsUnset(evt.ResourceKey);
        }

        private static string GetGameplaySummary(GameplayTrackEventJsonDto evt, bool compact)
        {
            evt = evt ?? new GameplayTrackEventJsonDto();
            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.StartEffectTimeline.ToString(), StringComparison.Ordinal))
                return "Start effect timeline: " + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.EffectTimelineId, 407));

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.SendGameplayRequest.ToString(), StringComparison.Ordinal))
            {
                string requestLabel = CharacterActionAuthoringIds.Label(evt.RequestId, 408);
                if (string.Equals(requestLabel, "character_test.arc_mine.deploy", StringComparison.Ordinal))
                {
                    return compact
                        ? "投掷地雷：Arc Mine"
                        : "投掷地雷 / Arc Mine：抛物线飞行体命中后转下落，落地布雷；入圈后引信延迟爆炸，数值见请求面板";
                }

                string label = IsProjectileRequestId(requestLabel) ? "发射投射物" : "发送玩法请求";
                return label + "：" + EmptyAsUnset(requestLabel);
            }

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.ApplyGameplayEffect.ToString(), StringComparison.Ordinal))
            {
                string damage = CharacterActionDamageAuthoringText.HasDamageData(evt.HitEvent?.Damage)
                    ? CharacterActionDamageAuthoringText.FormatDamageSummary(
                        CharacterActionAuthoringIds.Label(evt.HitEvent.Damage.DamageTypeId, 409, evt.HitEvent.Damage.DamageTypeLabel),
                        evt.HitEvent.Damage.BaseDamage,
                        evt.HitEvent.Damage.ImpactPower,
                        CharacterActionHitEventAuthoringModel.ResolveAttackScale(evt.HitEvent.Damage.SourceAttributeScales),
                        evt.HitEvent.Damage.LaunchPower)
                    : "未设置";
                return "应用玩法效果：伤害=" + damage
                    + "，目标=" + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.TargetPolicyId, 406));
            }

            if (string.Equals(evt.Kind, CharacterActionTrackEventKind.CastAbility.ToString(), StringComparison.Ordinal))
                return "释放能力：" + EmptyAsUnset(CharacterActionAuthoringIds.Label(evt.AbilityStableId, 404));

            string kind = CharacterActionAuthoringText.EnumDisplayLabel(
                typeof(CharacterActionTrackEventKind),
                evt.Kind);
            return kind;
        }

        private static bool IsProjectileRequestId(string requestId)
        {
            return CharacterActionProjectileRequestMembership.Contains(requestId);
        }

        private static string EmptyAsUnset(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

        private static string EmptyAsUnset(int value) =>
            value <= 0 ? "未设置" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private MotionTrackEventJsonDto GetMotion()
        {
            if (_trackKind != TimelineTrackKind.Motion)
                return null;

            MotionTrackEventJsonDto[] events = _action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

        private AnimationTrackEventJsonDto GetAnimation()
        {
            if (_trackKind != TimelineTrackKind.Animation)
                return null;

            AnimationTrackEventJsonDto[] events = _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

        private CombatTrackEventJsonDto GetCombat()
        {
            if (_trackKind != TimelineTrackKind.Combat)
                return null;

            CombatTrackEventJsonDto[] events = _action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

        private GameplayTrackEventJsonDto GetGameplay()
        {
            if (_trackKind != TimelineTrackKind.Gameplay)
                return null;

            GameplayTrackEventJsonDto[] events = _action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

        private PresentationTrackEventJsonDto GetPresentation()
        {
            if (_trackKind != TimelineTrackKind.Presentation)
                return null;

            PresentationTrackEventJsonDto[] events = _action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            return TryGetCurrentIndex(out int index) && index < events.Length ? events[index] : null;
        }

        private static bool IsPresentationRangeEvent(PresentationTrackEventJsonDto evt)
        {
            return CanUsePresentationRange(evt);
        }

        private static bool IsMotionRangeEvent(MotionTrackEventJsonDto evt)
        {
            if (evt == null)
                return false;
            string kind = evt.Kind;
            return string.Equals(kind, CharacterActionTrackEventKind.LockMovement.ToString(), StringComparison.Ordinal)
                || string.Equals(kind, CharacterActionTrackEventKind.SetControlScale.ToString(), StringComparison.Ordinal)
                || string.Equals(kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal)
                || string.Equals(kind, CharacterActionTrackEventKind.SetVerticalVelocity.ToString(), StringComparison.Ordinal)
                || string.Equals(kind, CharacterActionTrackEventKind.IgnoreGravity.ToString(), StringComparison.Ordinal)
                || string.Equals(kind, CharacterActionTrackEventKind.SampleRootMotionY.ToString(), StringComparison.Ordinal);
        }

        private static bool CanUsePresentationRange(PresentationTrackEventJsonDto evt)
        {
            return IsSpawnVisualCue(evt);
        }

        private static bool IsSpawnVisualCue(PresentationTrackEventJsonDto evt)
        {
            return evt != null
                && string.Equals(
                    evt.Kind,
                    CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                    StringComparison.Ordinal);
        }

        private static bool IsHitTargetStateRange(CombatTrackEventJsonDto evt)
        {
            return evt != null
                && string.Equals(
                    evt.Kind,
                    CharacterActionTrackEventKind.SetHitTargetState.ToString(),
                    StringComparison.Ordinal);
        }

        private object GetEventAt(int index)
        {
            switch (_trackKind)
            {
                case TimelineTrackKind.Motion:
                {
                    MotionTrackEventJsonDto[] events = _action.MotionTrack?.Events ?? Array.Empty<MotionTrackEventJsonDto>();
                    return index >= 0 && index < events.Length ? events[index] : null;
                }
                case TimelineTrackKind.Animation:
                {
                    AnimationTrackEventJsonDto[] events = _action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
                    return index >= 0 && index < events.Length ? events[index] : null;
                }
                case TimelineTrackKind.Combat:
                {
                    CombatTrackEventJsonDto[] events = _action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
                    return index >= 0 && index < events.Length ? events[index] : null;
                }
                case TimelineTrackKind.Gameplay:
                {
                    GameplayTrackEventJsonDto[] events = _action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
                    return index >= 0 && index < events.Length ? events[index] : null;
                }
                case TimelineTrackKind.Presentation:
                {
                    PresentationTrackEventJsonDto[] events = _action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
                    return index >= 0 && index < events.Length ? events[index] : null;
                }
                default:
                    return null;
            }
        }

        private static string GetStableEventId(object evt)
        {
            switch (evt)
            {
                case MotionTrackEventJsonDto motion:
                    return CharacterActionAuthoringIds.Label(motion.StableEventId, 401);
                case AnimationTrackEventJsonDto animation:
                    return CharacterActionAuthoringIds.Label(animation.StableEventId, 401);
                case CombatTrackEventJsonDto combat:
                    return CharacterActionAuthoringIds.Label(combat.StableEventId, 401);
                case GameplayTrackEventJsonDto gameplay:
                    return CharacterActionAuthoringIds.Label(gameplay.StableEventId, 401);
                case PresentationTrackEventJsonDto presentation:
                    return CharacterActionAuthoringIds.Label(presentation.StableEventId, 401);
                default:
                    return string.Empty;
            }
        }

        private static int FindIndex<T>(T[] events, T eventRef, string stableEventId, Func<T, string> getStableEventId)
            where T : class
        {
            if (events == null)
                return -1;

            if (eventRef != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    T current = events[i];
                    if (current != null && ReferenceEquals(current, eventRef))
                        return i;
                }
            }

            if (string.IsNullOrWhiteSpace(stableEventId))
                return -1;

            int match = -1;
            for (int i = 0; i < events.Length; i++)
            {
                T current = events[i];
                if (current == null
                    || !string.Equals(getStableEventId(current), stableEventId, StringComparison.Ordinal))
                    continue;
                if (match >= 0)
                    return -1;
                match = i;
            }

            return match;
        }

    }

    internal sealed class HitTraceWindowTimelineAdapter : ITimelineItemAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly CombatTrackEventJsonDto _eventRef;
        private readonly string _stableEventId;
        private readonly string _id;
        private readonly Color _color;

        public HitTraceWindowTimelineAdapter(
            CharacterActionJsonDto action,
            CombatTrackEventJsonDto trackEvent,
            Color color,
            bool canEdit)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _eventRef = trackEvent ?? throw new ArgumentNullException(nameof(trackEvent));
            _stableEventId = CharacterActionAuthoringIds.Label(_eventRef.StableEventId, 401);
            _id = "combat:hitTraceWindow:" + StablePart(_stableEventId, _eventRef);
            _color = color;
            CanEdit = canEdit;
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => TimelineTrackKind.Combat;
        public TimelineItemKind ItemKind => TimelineItemKind.Clip;
        public bool CanEdit { get; }
        public int StartFrame => GetEvent()?.Frame ?? 0;
        public int EndFrame => Math.Max(StartFrame, GetEvent()?.EndFrame ?? StartFrame);
        public string Title => CharacterActionAuthoringText.HitTraceWindowSummary(GetEvent(), compact: true);
        public string FullTitle => CharacterActionAuthoringText.HitTraceWindowSummary(GetEvent());
        public Color Color => _color;

        public bool TryGetCurrentIndex(out int index)
        {
            return TryGetEventIndex(out index);
        }

        public CombatTrackEventJsonDto GetEvent()
        {
            CombatTrackEventJsonDto[] events = _action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            return TryGetEventIndex(out int index) && index < events.Length ? events[index] : null;
        }

        public void SetRange(int startFrame, int endFrame)
        {
            if (!CanEdit)
                return;

            CombatTrackEventJsonDto trackEvent = GetEvent();
            if (trackEvent == null)
                return;

            int safeStart = Math.Max(0, startFrame);
            int safeEnd = Math.Max(safeStart, endFrame);
            trackEvent.Frame = safeStart;
            trackEvent.EndFrame = safeEnd;
            trackEvent.Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString();
        }

        public void Remove()
        {
            if (!CanEdit || _action.CombatTrack == null)
                return;

            var events = new System.Collections.Generic.List<CombatTrackEventJsonDto>(
                _action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
            if (TryGetEventIndex(out int eventIndex))
                events.RemoveAt(eventIndex);

            _action.CombatTrack.Events = events.ToArray();
        }

        public static bool IsHitTraceWindow(CombatTrackEventJsonDto evt)
        {
            return string.Equals(evt?.Kind, CharacterActionTrackEventKind.HitTraceWindow.ToString(), StringComparison.Ordinal);
        }

        public static string[] BuildWindowWarnings(CombatTrackEventJsonDto[] events)
        {
            events = events ?? Array.Empty<CombatTrackEventJsonDto>();
            var warnings = new System.Collections.Generic.List<string>();
            var ranges = new System.Collections.Generic.List<HitTraceWindowRange>();

            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEventJsonDto evt = events[i];
                if (!IsHitTraceWindow(evt))
                    continue;
                if (evt.EndFrame < evt.Frame)
                {
                    warnings.Add("HitTraceWindow 的 endFrame 必须大于或等于 frame: trace="
                        + EmptyAsDash(SelectorKey(evt)));
                    continue;
                }
                ranges.Add(new HitTraceWindowRange(SelectorKey(evt), evt.Frame, evt.EndFrame));
            }

            for (int i = 0; i < ranges.Count; i++)
            {
                for (int j = i + 1; j < ranges.Count; j++)
                {
                    if (!string.Equals(ranges[i].SelectorKey, ranges[j].SelectorKey, StringComparison.Ordinal))
                        continue;
                    if (ranges[j].StartFrame > ranges[i].EndFrame || ranges[i].StartFrame > ranges[j].EndFrame)
                        continue;

                    warnings.Add("HitTraceWindow 存在重叠: trace="
                        + EmptyAsDash(ranges[i].SelectorKey)
                        + " "
                        + ranges[i].StartFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "-"
                        + ranges[i].EndFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " overlaps "
                        + ranges[j].StartFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "-"
                        + ranges[j].EndFrame.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            return warnings.ToArray();
        }

        private bool TryGetEventIndex(out int index)
        {
            return FindCombatIndex(_action.CombatTrack?.Events, _eventRef, _stableEventId, IsHitTraceWindow, out index);
        }

        private static bool FindCombatIndex(
            CombatTrackEventJsonDto[] events,
            CombatTrackEventJsonDto eventRef,
            string stableEventId,
            Func<CombatTrackEventJsonDto, bool> kindPredicate,
            out int index)
        {
            events = events ?? Array.Empty<CombatTrackEventJsonDto>();
            if (eventRef != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    CombatTrackEventJsonDto current = events[i];
                    if (current != null && ReferenceEquals(current, eventRef) && kindPredicate(current))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(stableEventId))
            {
                int match = -1;
                for (int i = 0; i < events.Length; i++)
                {
                    CombatTrackEventJsonDto current = events[i];
                    if (current == null
                        || !kindPredicate(current)
                        || !string.Equals(CharacterActionAuthoringIds.Label(current.StableEventId, 401), stableEventId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (match >= 0)
                    {
                        index = -1;
                        return false;
                    }

                    match = i;
                }

                if (match >= 0)
                {
                    index = match;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static string SelectorKey(CombatTrackEventJsonDto evt)
        {
            if (evt?.EquipmentSlotId > 0)
                return "slot:" + CharacterActionAuthoringIds.Label(evt.EquipmentSlotId, 405);
            if (evt?.TraceProfileId > 0)
                return "trace:" + CharacterActionAuthoringIds.Label(evt.TraceProfileId, 403);
            return string.Empty;
        }

        private static string StablePart(string stableEventId, object fallback)
        {
            if (!string.IsNullOrWhiteSpace(stableEventId))
                return stableEventId.Trim();
            return "ref_" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(fallback).ToString("x");
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private readonly struct HitTraceWindowRange
        {
            public HitTraceWindowRange(string selectorKey, int startFrame, int endFrame)
            {
                SelectorKey = selectorKey ?? string.Empty;
                StartFrame = startFrame;
                EndFrame = endFrame;
            }

            public string SelectorKey { get; }
            public int StartFrame { get; }
            public int EndFrame { get; }
        }
    }

    internal sealed class RootMotionTimelineAdapter : ITimelineItemAdapter
    {
        public const string RootMotionItemId = "motion:rootMotion";

        private readonly CharacterActionJsonDto _action;
        private readonly Color _color;

        public RootMotionTimelineAdapter(CharacterActionJsonDto action, Color color)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _color = color;
        }

        public string Id => RootMotionItemId;
        public TimelineTrackKind TrackKind => TimelineTrackKind.Motion;
        public TimelineItemKind ItemKind => TimelineItemKind.Clip;
        public bool CanEdit => false;
        public int StartFrame => ResolveStartFrame(_action, GetRootMotion());
        public int EndFrame => ResolveEndFrame(_action, GetRootMotion());
        public string Title => CharacterActionAuthoringText.RootMotionSummary(GetRootMotion(), compact: true);
        public string FullTitle => CharacterActionAuthoringText.RootMotionSummary(GetRootMotion());
        public Color Color => _color;

        public bool TryGetCurrentIndex(out int index)
        {
            index = 0;
            return _action.MotionTrack?.RootMotion != null;
        }

        public void SetRange(int startFrame, int endFrame)
        {
        }

        public void Remove()
        {
            if (_action.MotionTrack != null)
                _action.MotionTrack.RootMotion = null;
        }

        private RootMotionTrackJsonDto GetRootMotion()
        {
            return _action.MotionTrack?.RootMotion;
        }

        private static int ResolveEndFrame(CharacterActionJsonDto action, RootMotionTrackJsonDto rootMotion)
        {
            RootMotionFrameJsonDto[] frames = rootMotion?.Frames ?? Array.Empty<RootMotionFrameJsonDto>();
            if (frames.Length == 0)
            {
                return CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                    action,
                    rootMotion,
                    preferredFrame: -1,
                    out AnimationTrackEventJsonDto animationEvent)
                        ? Math.Max(animationEvent.Frame, animationEvent.EndFrame)
                        : 0;
            }

            int endFrame = frames[0]?.Frame ?? 0;
            for (int i = 1; i < frames.Length; i++)
                endFrame = Math.Max(endFrame, frames[i]?.Frame ?? 0);
            return endFrame;
        }

        private static int ResolveStartFrame(CharacterActionJsonDto action, RootMotionTrackJsonDto rootMotion)
        {
            RootMotionFrameJsonDto[] frames = rootMotion?.Frames ?? Array.Empty<RootMotionFrameJsonDto>();
            if (frames.Length == 0)
            {
                return CharacterActionAuthoringModel.TryResolveRootMotionSourceAnimationEvent(
                    action,
                    rootMotion,
                    preferredFrame: -1,
                    out AnimationTrackEventJsonDto animationEvent)
                        ? Math.Min(animationEvent.Frame, animationEvent.EndFrame)
                        : 0;
            }

            int startFrame = frames[0]?.Frame ?? 0;
            for (int i = 1; i < frames.Length; i++)
                startFrame = Math.Min(startFrame, frames[i]?.Frame ?? 0);
            return startFrame;
        }
    }
}
