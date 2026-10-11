using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private readonly HashSet<int> _expandedDamageWindows = new HashSet<int>();

        private void ToggleDamageComparison()
        {
            _showDamageComparison = !_showDamageComparison;
            if (_previewSurfaceHost != null)
                _previewSurfaceHost.style.display = _previewVisible && !_showDamageComparison
                    ? DisplayStyle.Flex : DisplayStyle.None;
            if (_damageComparisonHost != null)
                _damageComparisonHost.style.display = _showDamageComparison
                    ? DisplayStyle.Flex : DisplayStyle.None;
            if (_showDamageComparison)
                RefreshDamageComparisonTable();
            else if (_previewVisible)
                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
        }

        private void RefreshDamageComparisonTable()
        {
            if (!_showDamageComparison || _damageComparisonHost == null)
                return;
            _damageComparisonHost.Clear();
            CharacterActionJsonDto action = _model?.SelectedAction;
            if (action == null)
            {
                _damageComparisonHost.Add(new Label("请选择动作。"));
                return;
            }

            var windows = new List<CombatTrackEventJsonDto>();
            CombatTrackEventJsonDto[] events = action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            foreach (CombatTrackEventJsonDto trackEvent in events)
                if (trackEvent != null && string.Equals(trackEvent.Kind, "HitTraceWindow", StringComparison.Ordinal))
                    windows.Add(trackEvent);
            windows.Sort((left, right) => left.Frame != right.Frame
                ? left.Frame.CompareTo(right.Frame)
                : left.StableEventId.CompareTo(right.StableEventId));

            _damageComparisonHost.Add(new Label("攻击窗口伤害输入（目标防御在实际命中时结算）"));
            if (windows.Count == 0)
            {
                _damageComparisonHost.Add(new Label("当前动作没有攻击窗口。"));
                return;
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("ca-damage-comparison-row");
            foreach (string title in new[] { "窗口 / 帧", "基础伤害", "攻击系数", "冲击", "击飞", "伤害性质" })
                header.Add(new Label(title));
            _damageComparisonHost.Add(header);

            for (int i = 0; i < windows.Count; i++)
                AddDamageComparisonRow(windows, windows[i], i);
        }

        private void AddDamageComparisonRow(
            IReadOnlyList<CombatTrackEventJsonDto> windows,
            CombatTrackEventJsonDto trackEvent,
            int index)
        {
            CharacterActionHitDamageJsonDto current = trackEvent.HitEvent?.Damage;
            var group = new VisualElement();
            group.AddToClassList("ca-damage-comparison-group");
            var row = new VisualElement();
            row.AddToClassList("ca-damage-comparison-row");
            var select = new Button(() => SelectDamageComparisonWindow(trackEvent))
            {
                text = (index + 1) + "  " + trackEvent.Frame + "–" + trackEvent.EndFrame + " 帧"
            };
            row.Add(select);
            AddDamageComparisonInt(row, current?.BaseDamage ?? 0,
                value => EnsureDamage(trackEvent).BaseDamage = value,
                DamageValueIsOutlier(windows, index, d => d.BaseDamage));
            AddDamageComparisonFloat(row, ResolveAttackScale(current?.SourceAttributeScales),
                value => SetDamageComparisonAttackScale(EnsureDamage(trackEvent), value),
                DamageValueIsOutlier(windows, index, d => ResolveAttackScale(d.SourceAttributeScales)));
            AddDamageComparisonInt(row, current?.ImpactPower ?? 0,
                value => EnsureDamage(trackEvent).ImpactPower = value,
                DamageValueIsOutlier(windows, index, d => d.ImpactPower));
            AddDamageComparisonInt(row, current?.LaunchPower ?? 0,
                value => EnsureDamage(trackEvent).LaunchPower = value,
                DamageValueIsOutlier(windows, index, d => d.LaunchPower));
            string damageType = CharacterActionAuthoringIds.Label(
                current?.DamageTypeId ?? 0, 409, current?.DamageTypeLabel);
            AddDamageChoiceField(row, string.Empty, damageType, BuildDamageTypeChoices(damageType), value =>
                ApplyDamageComparisonChange(() =>
                {
                    CharacterActionHitDamageJsonDto damage = EnsureDamage(trackEvent);
                    damage.DamageTypeId = CharacterActionAuthoringIds.Resolve(value, 409);
                    damage.DamageTypeLabel = value;
                }));
            group.Add(row);

            int expansionId = trackEvent.StableEventId != 0 ? trackEvent.StableEventId : trackEvent.Frame;
            var details = new Foldout { text = "标签、分析分类与其它来源属性系数", value = _expandedDamageWindows.Contains(expansionId) };
            details.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) _expandedDamageWindows.Add(expansionId);
                else _expandedDamageWindows.Remove(expansionId);
            });
            string category = CharacterActionAuthoringIds.Label(
                current?.AnalysisCategory ?? 0, 411, current?.AnalysisCategoryLabel);
            AddDamageChoiceField(details, "分析分类", category, BuildAnalysisCategoryChoices(category), value =>
                ApplyDamageComparisonChange(() =>
                {
                    CharacterActionHitDamageJsonDto damage = EnsureDamage(trackEvent);
                    damage.AnalysisCategory = CharacterActionAuthoringIds.Resolve(value, 411);
                    damage.AnalysisCategoryLabel = value;
                }));
            string[] tags = CharacterActionAuthoringIds.Labels(current?.Tags, 410, current?.TagLabels);
            AddDamageTagToggles(details, tags, value => ApplyDamageComparisonChange(() =>
            {
                CharacterActionHitDamageJsonDto damage = EnsureDamage(trackEvent);
                damage.Tags = CharacterActionAuthoringIds.ResolveMany(value, 410);
                damage.TagLabels = value;
            }));
            CharacterActionDamageAttributeScaleJsonDto[] scales = current?.SourceAttributeScales
                ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
            for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
            {
                int captured = scaleIndex;
                CharacterActionDamageAttributeScaleJsonDto scale = scales[scaleIndex];
                if (scale == null || scale.AttributeId == 2) continue;
                var field = new FloatField("属性 " + scale.AttributeId + " 系数")
                {
                    value = scale.Scale,
                    isDelayed = true
                };
                field.RegisterValueChangedCallback(evt => ApplyDamageComparisonChange(() =>
                {
                    CharacterActionDamageAttributeScaleJsonDto[] target = EnsureDamage(trackEvent).SourceAttributeScales;
                    if (target != null && captured < target.Length && target[captured] != null)
                        target[captured].Scale = Math.Max(0f, evt.newValue);
                }));
                details.Add(field);
                details.Add(new Button(() => ApplyDamageComparisonChange(() =>
                {
                    var values = new List<CharacterActionDamageAttributeScaleJsonDto>(
                        EnsureDamage(trackEvent).SourceAttributeScales ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>());
                    if (captured < values.Count) values.RemoveAt(captured);
                    EnsureDamage(trackEvent).SourceAttributeScales = values.ToArray();
                })) { text = "移除属性 " + scale.AttributeId });
            }
            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var newAttribute = new IntegerField("属性 ID") { value = 1 };
            var newScale = new FloatField("系数") { value = 0f };
            addRow.Add(newAttribute);
            addRow.Add(newScale);
            addRow.Add(new Button(() =>
            {
                if (newAttribute.value <= 0 || newScale.value < 0f
                    || float.IsNaN(newScale.value) || float.IsInfinity(newScale.value)) return;
                ApplyDamageComparisonChange(() =>
                {
                    CharacterActionHitDamageJsonDto damage = EnsureDamage(trackEvent);
                    var values = new List<CharacterActionDamageAttributeScaleJsonDto>(
                        damage.SourceAttributeScales ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>());
                    if (values.Exists(value => value != null && value.AttributeId == newAttribute.value)) return;
                    values.Add(new CharacterActionDamageAttributeScaleJsonDto
                    {
                        AttributeId = newAttribute.value,
                        Scale = newScale.value
                    });
                    damage.SourceAttributeScales = values.ToArray();
                });
            }) { text = "添加来源属性" });
            details.Add(addRow);
            group.Add(details);
            _damageComparisonHost.Add(group);
        }

        private void AddDamageComparisonInt(VisualElement row, int value, Action<int> mutate, bool differs)
        {
            var field = new IntegerField { value = value, isDelayed = true };
            if (differs) field.AddToClassList("ca-damage-comparison-diff");
            field.RegisterValueChangedCallback(evt => ApplyDamageComparisonChange(() => mutate(Math.Max(0, evt.newValue))));
            row.Add(field);
        }

        private void AddDamageComparisonFloat(VisualElement row, float value, Action<float> mutate, bool differs)
        {
            var field = new FloatField { value = value, isDelayed = true };
            if (differs) field.AddToClassList("ca-damage-comparison-diff");
            field.RegisterValueChangedCallback(evt => ApplyDamageComparisonChange(() => mutate(Math.Max(0f, evt.newValue))));
            row.Add(field);
        }

        private void ApplyDamageComparisonChange(Action mutate)
        {
            ApplyTimelineFieldChange(mutate);
            if (rootVisualElement?.panel != null)
                rootVisualElement.schedule.Execute(RefreshDamageComparisonTable);
            else
                RefreshDamageComparisonTable();
        }

        private void SelectDamageComparisonWindow(CombatTrackEventJsonDto trackEvent)
        {
            if (_timelineView?.Snapshot?.ItemsByTrack.TryGetValue(
                    TimelineTrackKind.Combat, out IReadOnlyList<ITimelineItemAdapter> items) == true)
            {
                foreach (ITimelineItemAdapter item in items)
                {
                    if (item is HitTraceWindowTimelineAdapter adapter
                        && ReferenceEquals(adapter.GetEvent(), trackEvent))
                    {
                        _timelineView.SelectItem(adapter.Id);
                        break;
                    }
                }
            }
            SeekPreviewFrame(trackEvent.Frame);
        }

        private static bool DamageValueIsOutlier<T>(
            IReadOnlyList<CombatTrackEventJsonDto> windows,
            int rowIndex,
            Func<CharacterActionHitDamageJsonDto, T> read)
        {
            T current = read(windows[rowIndex].HitEvent?.Damage ?? new CharacterActionHitDamageJsonDto());
            int currentCount = 0;
            int largestCount = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                T candidate = read(windows[i].HitEvent?.Damage ?? new CharacterActionHitDamageJsonDto());
                int count = 0;
                for (int other = 0; other < windows.Count; other++)
                {
                    T value = read(windows[other].HitEvent?.Damage ?? new CharacterActionHitDamageJsonDto());
                    if (EqualityComparer<T>.Default.Equals(candidate, value)) count++;
                }
                if (count > largestCount) largestCount = count;
                if (EqualityComparer<T>.Default.Equals(candidate, current)) currentCount = count;
            }
            return currentCount < largestCount
                || (currentCount < windows.Count && currentCount == largestCount);
        }

        private static void SetDamageComparisonAttackScale(CharacterActionHitDamageJsonDto damage, float scale)
        {
            var values = new List<CharacterActionDamageAttributeScaleJsonDto>(
                damage.SourceAttributeScales ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>());
            int index = values.FindIndex(value => value != null && value.AttributeId == 2);
            if (index >= 0)
            {
                if (scale <= 0.0001f) values.RemoveAt(index);
                else values[index].Scale = scale;
            }
            else if (scale > 0.0001f)
                values.Add(new CharacterActionDamageAttributeScaleJsonDto { AttributeId = 2, Scale = scale });
            damage.SourceAttributeScales = values.ToArray();
        }
    }
}
