using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static string FormatFactsForEditor(
            IReadOnlyList<CharacterTestRuntimeAiPlannerFactKind> facts)
        {
            if (facts == null || facts.Count == 0)
                return "none";

            var names = new string[facts.Count];
            for (int i = 0; i < facts.Count; i++)
                names[i] = facts[i].ToString();
            return string.Join(", ", names);
        }

        private void MoveSelectedBehaviorEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            int direction)
        {
            int index = _selectedBehaviorGroupEntryIndex;
            int next = index + direction;
            if (profile == null || index < 0 || index >= profile.Entries.Count || next < 0 || next >= profile.Entries.Count)
                return;

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry current = profile.Entries[index];
            profile.Entries[index] = profile.Entries[next];
            profile.Entries[next] = current;
            int order = profile.Entries[index].Order;
            profile.Entries[index].Order = profile.Entries[next].Order;
            profile.Entries[next].Order = order;
            _selectedBehaviorGroupEntryIndex = next;
            RefreshView();
        }

        private void SelectBehaviorGroupEntry(string entryId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                EnsureBehaviorCompositionProfile();
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                if (!string.Equals(profile.Entries[i].EntryId, entryId, StringComparison.Ordinal))
                    continue;

                _selectedBehaviorGroupEntryIndex = i;
                _statusText = "已定位行为实例 " + entryId;
                RefreshView();
                return;
            }
        }

        private void SwitchToBehaviorGroupPreviewCurrentDraft()
        {
            _selectedBehaviorGroupPreviewCandidateKey = BehaviorGroupPreviewCandidateCurrentDraftKey;
            _activeTab = BehaviorGroupPreviewTabIndex;
            RefreshView();
        }

        private void DuplicateSelectedBehaviorEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            int index = _selectedBehaviorGroupEntryIndex;
            if (profile == null || index < 0 || index >= profile.Entries.Count)
                return;

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry copy = profile.Entries[index].Clone();
            copy.EntryId = CreateUniqueBehaviorEntryId(profile, copy.BehaviorId);
            copy.Order = profile.Entries[index].Order + 1;
            profile.Entries.Insert(index + 1, copy);
            _selectedBehaviorGroupEntryIndex = index + 1;
            RefreshView();
        }

        private void DeleteSelectedBehaviorEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            int index = _selectedBehaviorGroupEntryIndex;
            if (profile == null || index < 0 || index >= profile.Entries.Count)
                return;

            profile.Entries.RemoveAt(index);
            if (_selectedBehaviorGroupEntryIndex >= profile.Entries.Count)
                _selectedBehaviorGroupEntryIndex = profile.Entries.Count - 1;
            RefreshView();
        }

        private List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> GetFilteredBehaviorDefinitions(
            string searchText)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> definitions =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions;
            var filtered = new List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition>(definitions.Count);
            string search = searchText ?? string.Empty;
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = definitions[i];
                if (MatchesBehaviorDefinitionSearch(definition, search))
                    filtered.Add(definition);
            }

            return filtered;
        }

        private static bool MatchesBehaviorDefinitionSearch(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string searchText)
        {
            if (definition == null)
                return false;
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return ContainsOrdinalIgnoreCase(definition.BehaviorId, searchText)
                || ContainsOrdinalIgnoreCase(definition.DisplayName, searchText)
                || ContainsOrdinalIgnoreCase(definition.Category.ToString(), searchText)
                || ContainsOrdinalIgnoreCase(definition.ActionPrimitive.ToString(), searchText)
                || ContainsOrdinalIgnoreCase(definition.Intent, searchText);
        }

        private static bool ContainsOrdinalIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddBehaviorDefinitionToGroup(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (profile == null || definition == null)
                return;

            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                definition.BehaviorId,
                true,
                ResolveNextBehaviorOrder(profile))
            {
                EntryId = CreateUniqueBehaviorEntryId(profile, definition.BehaviorId),
                BehaviorId = definition.BehaviorId
            };
            CopyDefaultBehaviorParameters(definition, entry);
            profile.Entries.Add(entry);
            _selectedBehaviorGroupEntryIndex = profile.Entries.Count - 1;
            _lastBehaviorCompositionReport = string.Empty;
            _statusText = "已加入行为 " + definition.DisplayName;
            RefreshView();
        }

        private static void CopyDefaultBehaviorParameters(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                    definition.Parameters[i];
                switch (parameter.Kind)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                        entry.SetBool(parameter.Key, parameter.DefaultBool);
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                        entry.SetInt(parameter.Key, parameter.DefaultInt);
                        break;
                    default:
                        entry.SetFloat(parameter.Key, parameter.DefaultFloat);
                        break;
                }
            }
        }

        private static int ResolveNextBehaviorOrder(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            int max = -10;
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                if (profile.Entries[i].Order > max)
                    max = profile.Entries[i].Order;
            }

            return max + 10;
        }

        private static string CreateUniqueBehaviorEntryId(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string behaviorId)
        {
            string baseId = string.IsNullOrWhiteSpace(behaviorId)
                ? "behavior"
                : behaviorId;
            for (int suffix = 1; suffix < 10000; suffix++)
            {
                string candidate = baseId + "#" + suffix.ToString(CultureInfo.InvariantCulture);
                if (!ContainsBehaviorEntryId(profile, candidate))
                    return candidate;
            }

            return baseId + "#" + Guid.NewGuid().ToString("N");
        }

        private static bool ContainsBehaviorEntryId(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string entryId)
        {
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                if (string.Equals(profile.Entries[i].EntryId, entryId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private CharacterTestRuntimeAiPlannerBehaviorCompositionProfile EnsureBehaviorCompositionProfile()
        {
            if (_behaviorCompositionProfile == null)
            {
                _behaviorCompositionProfile =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(
                        _profileLoaded ? _model.ToRuntimeProfile() : null);
            }

            if (_profileLoaded)
            {
                _behaviorCompositionProfile.TargetSelectionMode = _model.TargetSelectionMode;
                _behaviorCompositionProfile.AttackRange = _model.AttackRange;
                _behaviorCompositionProfile.PerceptionSettings =
                    new CharacterTestRuntimeAiPlannerPerceptionSettings(
                        _model.VisionRange,
                        _model.TargetMemoryFrames,
                        _model.LineOfSightObstacleLayerMask,
                        _model.EnvironmentSampleRadius,
                        _model.ThreatDistanceWeight);
                _behaviorCompositionProfile.MovementSettings =
                    new CharacterTestRuntimeAiPlannerMovementSettings(
                        _model.MovementSpeedScale,
                        _model.MovementSteeringResponsiveness,
                        _model.MovementApproachLateralBlend,
                        _model.MovementPreferredDistanceMin,
                        _model.MovementPreferredDistanceMax,
                        _model.MovementArrivalSlowdownDistance,
                        _model.MovementStrafeSwitchMinFrames,
                        _model.MovementStrafeSwitchMaxFrames,
                        _model.MovementDirectionHoldFrames,
                        _model.MovementReactionDelayFrames);
            }

            return _behaviorCompositionProfile;
        }

        private void ApplyCompiledBehaviorCompositionToEditorProfile()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    EnsureBehaviorCompositionProfile());
            _lastBehaviorCompositionReport = compile.Report != null
                ? compile.Report.ToText()
                : string.Empty;
            if (!compile.Success)
            {
                _statusText = "行为组编译失败";
                RefreshView();
                return;
            }

            _model.Load(compile.Profile);
            _selectedStateId = compile.Profile.InitialStateId.ToString();
            _selectedTransitionIndex = -1;
            _selectedActionIndex = 0;
            MarkDirty("已用行为组编译结果覆盖底层 Profile 草稿");
        }

        private void CopyBehaviorCompositionJson()
        {
            GUIUtility.systemCopyBuffer =
                CharacterTestRuntimeAiPlannerBehaviorGroupJson.ToJson(
                    EnsureBehaviorCompositionProfile());
            _statusText = "已复制行为组 JSON";
            RefreshView();
        }

        private static CharacterTestRuntimeAiPlannerEditorModel CreateBehaviorGroupFlowPreviewModel(
            CharacterTestRuntimeAiPlannerProfile profile)
        {
            var model = new CharacterTestRuntimeAiPlannerEditorModel();
            model.Load(profile);
            return model;
        }

        private void OpenBehaviorGroupFlowPreviewWindow(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            if (compile == null || compile.Profile == null)
                return;

            CharacterTestRuntimeAiPlannerEditorModel previewModel =
                CreateBehaviorGroupFlowPreviewModel(compile.Profile);
            CharacterTestRuntimeAiPlannerFlowPreviewWindow.Open(
                previewModel,
                previewModel.InitialStateId,
                -1,
                SelectBehaviorGroupFlowState,
                SelectBehaviorGroupFlowTransition);
        }

        private void SelectBehaviorGroupFlowState(string stateId)
        {
            _statusText = string.IsNullOrWhiteSpace(stateId)
                ? "底层 Runtime AI Planner 流程已定位状态"
                : "底层 Runtime AI Planner 流程状态: " + stateId;
            RefreshView();
        }

        private void SelectBehaviorGroupFlowTransition(int transitionIndex)
        {
            _statusText = "底层 Runtime AI Planner 流程切换: " + transitionIndex.ToString(CultureInfo.InvariantCulture);
            RefreshView();
        }

        private void SetBehaviorGroupFlowPreviewZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                zoom = 1f;

            _behaviorGroupFlowPreviewZoom = Mathf.Clamp(
                zoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MinDetailZoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MaxZoom);
            RefreshView();
        }

        private void FitBehaviorGroupFlowPreviewZoom()
        {
            _behaviorGroupFlowPreviewZoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
            RefreshView();
        }

        private void ApplyBehaviorCompositionHotUpdate()
        {
            CharacterTestRuntimeAiPlannerEditorDebugTarget target = FindBehaviorHotUpdateTarget();
            if (target == null || !BehaviorHotUpdateEnabled)
            {
                _lastBehaviorCompositionReport = "Behavior composition hot update is disabled or no Play Mode target is registered.";
                _statusText = "行为组热更新不可用";
                RefreshView();
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                EnsureBehaviorCompositionProfile().Clone();
            target.TryApplyBehaviorCompositionOverlay(
                profile,
                0,
                out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result);
            _lastBehaviorCompositionReport = result.ReportText;
            _statusText = result.Success ? "行为组热更新已应用" : "行为组热更新失败";
            RefreshView();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleSummary FindBehaviorModuleSummary(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            string entryId,
            string moduleId)
        {
            if (compile != null)
            {
                for (int i = 0; i < compile.ModuleSummaries.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary =
                        compile.ModuleSummaries[i];
                    if (string.Equals(summary.EntryId, entryId, StringComparison.Ordinal))
                        return summary;
                }

                for (int i = 0; i < compile.ModuleSummaries.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary =
                        compile.ModuleSummaries[i];
                    if (string.Equals(summary.ModuleId, moduleId, StringComparison.Ordinal))
                        return summary;
                }
            }

            return new CharacterTestRuntimeAiPlannerBehaviorModuleSummary(
                moduleId,
                moduleId,
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Fallback,
                false,
                string.Empty,
                "none",
                "none",
                0,
                string.Empty);
        }

        private static CharacterTestRuntimeAiPlannerEditorDebugTarget FindBehaviorHotUpdateTarget()
        {
            var targets = CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorDebugTarget target = targets[i];
                if (target != null && target.IsAvailable && target.CanApplyBehaviorCompositionOverlay)
                    return target;
            }

            return null;
        }

        private static string ResolveBehaviorModuleActionChainText(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreview actionChainPreview)
        {
            CharacterTestRuntimeAiPlannerActionKind kind = ResolveBehaviorModuleActionKind(module.ModuleId);
            if (kind == CharacterTestRuntimeAiPlannerActionKind.Wait
                && !string.Equals(
                    module.ModuleId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal))
            {
                return "不触发 Ability / CharacterAction";
            }

            if (!actionChainPreview.TryFindByActionKind(
                    kind,
                    out CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item))
            {
                return module.Enabled ? "未生成 action" : "行为实例已禁用";
            }

            return item.PlayerSummary
                + (item.HasWarning ? " | warning: " + item.WarningText : string.Empty);
        }

        private static CharacterTestRuntimeAiPlannerActionKind ResolveBehaviorModuleActionKind(string moduleId)
        {
            return CharacterTestRuntimeAiPlannerActionKind.Wait;
        }
    }
}
