using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private CharacterTestLevelGameplayTriggerAuthoringDocument
            _triggerDocument;
        private bool _triggerModulePresent;
        private bool _triggerDirty;
        private readonly HashSet<string> _expandedTriggerRuleEntryIds =
            new HashSet<string>(StringComparer.Ordinal);
        private LevelWorldObjectAuthoringValidationReport _triggerLoadReport =
            LevelWorldObjectAuthoringValidationReport.Empty;
        private GameplayTriggerCompileResult _triggerCompileResult;

        internal int TriggerEntryCountForTests =>
            _triggerDocument?.Entries?.Count ?? 0;

        internal int TriggerRetiredIdCountForTests =>
            _triggerDocument?.IdState?.RetiredIds?.Count ?? 0;

        private void LoadTriggerDocumentFromEnvelope()
        {
            if (CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    _envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        document,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport report))
            {
                _triggerDocument = document;
                _triggerModulePresent = modulePresent;
                _triggerLoadReport = report;
                return;
            }

            _triggerDocument = null;
            _triggerModulePresent = modulePresent;
            _triggerLoadReport = report;
        }

        private void SyncTriggerEnvelope()
        {
            if (!_triggerModulePresent || _triggerDocument == null)
                return;

            _envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    _envelope,
                    _triggerDocument);
        }

        private LevelWorldObjectAuthoringValidationReport
            AppendTriggerValidation(
                LevelWorldObjectAuthoringValidationReport worldReport,
                Transform root)
        {
            LevelWorldObjectAuthoringValidationReport combined =
                worldReport
                ?? LevelWorldObjectAuthoringValidationReport.Empty;
            if (_triggerLoadReport?.Issues?.Count > 0)
                combined = combined.Append(_triggerLoadReport.Issues);
            if (_triggerDocument == null)
                return combined;

            LevelWorldObjectAuthoringValidationReport triggerReport =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .ValidateAndCompile(
                        _triggerDocument,
                        _document,
                        out _triggerCompileResult);
            combined = combined.Append(triggerReport.Issues);
            if (_triggerModulePresent)
            {
                IReadOnlyList<LevelWorldObjectAuthoringIssue> sceneIssues =
                    ValidateTriggerRegionSceneBindings(root);
                combined = combined.Append(sceneIssues);
            }
            return combined;
        }

        private IReadOnlyList<LevelWorldObjectAuthoringIssue>
            ValidateTriggerRegionSceneBindings(Transform root)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (_triggerDocument?.RegionBindings == null
                || _document?.Regions == null)
            {
                return issues;
            }

            for (int bindingIndex = 0;
                 bindingIndex < _triggerDocument.RegionBindings.Count;
                 bindingIndex++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    _triggerDocument.RegionBindings[bindingIndex];
                if (binding == null)
                    continue;

                LevelWorldObjectRegionDefinition region = null;
                for (int regionIndex = 0;
                     regionIndex < _document.Regions.Count;
                     regionIndex++)
                {
                    LevelWorldObjectRegionDefinition candidate =
                        _document.Regions[regionIndex];
                    if (candidate != null
                        && string.Equals(
                            candidate.StableId,
                            binding.RegionStableId,
                            StringComparison.Ordinal))
                    {
                        region = candidate;
                        break;
                    }
                }
                if (region == null)
                    continue;

                string path = "$.modules."
                    + CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId
                    + ".payload.regionBindings["
                    + bindingIndex
                    + "].regionStableId";
                if (!CharacterTestWorldObjectAuthoringAdapter
                        .TryResolveTransform(
                            root,
                            region.TransformRef,
                            out Transform transform)
                    || transform == null)
                {
                    issues.Add(TriggerSceneError(
                        "CT_TRIGGER_REGION_TRANSFORM_MISSING",
                        path,
                        "Trigger region Transform cannot be resolved."));
                    continue;
                }

                Quaternion rotation = transform.rotation;
                if (!IsFinite(rotation)
                    || Quaternion.Angle(rotation, Quaternion.identity) > 0.01f)
                {
                    issues.Add(TriggerSceneError(
                        "CT_TRIGGER_REGION_ROTATION_UNSUPPORTED",
                        path,
                        "First-version Trigger regions must have identity world rotation for Combat AABB compilation."));
                }

                Vector3 scale = transform.lossyScale;
                if (!IsFinitePositive(scale.x)
                    || !IsFinitePositive(scale.y)
                    || !IsFinitePositive(scale.z))
                {
                    issues.Add(TriggerSceneError(
                        "CT_TRIGGER_REGION_SCALE_INVALID",
                        path,
                        "Trigger region world scale must be finite and positive."));
                }
            }

            return issues;
        }

        private void MarkTriggerDirty()
        {
            _triggerModulePresent = true;
            _triggerDirty = true;
            MarkDirty();
        }

        private bool TryGetGameplayTriggerReferenceSource(
            Transform transform,
            out string source)
        {
            source = string.Empty;
            if (transform == null
                || _triggerDocument == null
                || !TryFindRegionStableId(transform, out string regionStableId)
                || !CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .TryFindEntryForRegion(
                        _triggerDocument,
                        regionStableId,
                        out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.StableId
                : entry.DisplayName;
            source = string.IsNullOrWhiteSpace(displayName)
                ? CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId
                : "Trigger“" + displayName + "”";
            return true;
        }

        private bool TryGetGameplayTriggerReferenceSource(
            Row row,
            out string source)
        {
            source = string.Empty;
            if (row == null
                || row.Kind != RowKind.Region
                || _document?.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count)
            {
                return false;
            }

            LevelWorldObjectRegionDefinition region =
                _document.Regions[row.Index];
            if (region == null
                || !CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .TryFindEntryForRegion(
                        _triggerDocument,
                        region.StableId,
                        out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.StableId
                : entry.DisplayName;
            source = string.IsNullOrWhiteSpace(displayName)
                ? CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId
                : "Trigger“" + displayName + "”";
            return true;
        }

        private bool TryFindRegionStableId(
            Transform transform,
            out string stableId)
        {
            stableId = string.Empty;
            Transform root = FindSceneCacheTarget()?.Root;
            if (root == null || _document?.Regions == null)
                return false;

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region =
                    _document.Regions[i];
                if (region != null
                    && CharacterTestWorldObjectAuthoringAdapter
                        .TryResolveTransform(
                            root,
                            region.TransformRef,
                            out Transform resolved)
                    && resolved == transform)
                {
                    stableId = region.StableId;
                    return !string.IsNullOrWhiteSpace(stableId);
                }
            }

            return false;
        }

        private void ResetTriggerDocumentForTests()
        {
            _triggerDocument =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            _triggerModulePresent = false;
            _triggerDirty = false;
            _triggerLoadReport =
                LevelWorldObjectAuthoringValidationReport.Empty;
            _triggerCompileResult = null;
        }

        internal void SetTriggerDocumentForTests(
            CharacterTestLevelGameplayTriggerAuthoringDocument document,
            bool markDirty)
        {
            _triggerDocument = (document
                ?? new CharacterTestLevelGameplayTriggerAuthoringDocument())
                .Normalize();
            _triggerModulePresent = true;
            _triggerLoadReport =
                LevelWorldObjectAuthoringValidationReport.Empty;
            SyncTriggerEnvelope();
            if (markDirty)
            {
                MarkTriggerDirty();
                return;
            }

            _triggerDirty = false;
            _dirty = false;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            _report = ValidateLoadedDocument();
        }

        internal string CaptureDocumentSnapshotForTests()
        {
            return CaptureDocumentSnapshot();
        }

        internal bool RemoveTriggerEntryForTests(string stableId)
        {
            return RemoveTriggerEntry(stableId);
        }

        internal CharacterTestLevelGameplayTriggerEntry AddTriggerTemplateForTests(
            string regionStableId,
            string templateId)
        {
            return AddTriggerTemplate(regionStableId, templateId);
        }

        internal bool UpdateTriggerPayloadsForTests(
            string stableId,
            long payload0,
            long payload1,
            long payload2 = 0L)
        {
            return UpdateTriggerPayloads(
                stableId,
                payload0,
                payload1,
                payload2);
        }

        internal bool SetTriggerEnabledForTests(
            string stableId,
            bool enabled)
        {
            return SetTriggerEnabled(stableId, enabled);
        }

        internal bool UpdateTriggerPolicyForTests(
            string stableId,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            return UpdateTriggerPolicy(
                stableId,
                stateScope,
                presenceLimit,
                cooldownFrames,
                maxActivations);
        }

        internal bool UpdateTriggerSubjectRelationForTests(
            string stableId,
            GameplayTargetRelationFilter relationFilter,
            int referenceTeamId)
        {
            return UpdateTriggerSubjectRelation(
                stableId,
                relationFilter,
                referenceTeamId);
        }

        internal bool UpdateTriggerSubjectCapabilitiesForTests(
            string stableId,
            GameplayTargetCapabilities requiredCapabilities)
        {
            return UpdateTriggerSubjectCapabilities(
                stableId,
                requiredCapabilities);
        }

        internal bool TryGetTriggerSubjectFilterForTests(
            string stableId,
            out CharacterTestLevelGameplayTriggerSubjectFilter filter)
        {
            filter = null;
            return _triggerDocument != null
                && _triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                && _triggerDocument.TryGetSubjectFilter(
                    entry,
                    out filter);
        }

        internal bool DuplicateTriggerEntryForTests(
            string stableId,
            out CharacterTestLevelGameplayTriggerEntry duplicate)
        {
            return DuplicateTriggerEntry(stableId, out duplicate);
        }

        private CharacterTestLevelGameplayTriggerEntry AddTriggerTemplate(
            string regionStableId,
            string templateId)
        {
            if (_triggerDocument == null
                || !IsEditableTriggerRegion(regionStableId))
            {
                return null;
            }

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "添加 CharacterTest 关卡触发器"))
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    _triggerDocument.AddTemplate(
                        templateId,
                        regionStableId,
                        payload0: IsBuffTriggerTemplate(templateId)
                            ? 0L
                            : IsStoryTriggerTemplate(templateId)
                                ? ResolveDefaultTriggerStoryReferenceId()
                                : 1L);
                MarkTriggerDirty();
                transaction.Commit();
                return entry;
            }
        }

        private CharacterTestLevelGameplayTriggerEntry
            AddPlayerHpLowStoryTrigger()
        {
            if (_triggerDocument == null)
                return null;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "添加当前玩家生命阈值触发器"))
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    _triggerDocument.AddPlayerHpLowStoryTemplate(
                        checked((int)
                            ResolveDefaultTriggerStoryReferenceId()));
                MarkTriggerDirty();
                transaction.Commit();
                return entry;
            }
        }

        private bool UpdatePlayerHpThresholds(
            string stableId,
            int enterThreshold,
            int exitThreshold)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改当前玩家生命阈值"))
            {
                if (!_triggerDocument.TryUpdatePlayerHpThresholds(
                        stableId,
                        enterThreshold,
                        exitThreshold))
                {
                    return false;
                }
                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        internal CharacterTestLevelGameplayTriggerEntry
            AddPlayerHpLowStoryTriggerForTests()
        {
            return AddPlayerHpLowStoryTrigger();
        }

        internal bool UpdatePlayerHpThresholdsForTests(
            string stableId,
            int enterThreshold,
            int exitThreshold)
        {
            return UpdatePlayerHpThresholds(
                stableId,
                enterThreshold,
                exitThreshold);
        }

        private bool UpdateTriggerConditionRoot(
            string stableId,
            GameplayTriggerConditionNodeAuthoring root)
        {
            if (_triggerDocument == null || root == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发条件"))
            {
                if (!_triggerDocument.TryUpdateConditionRoot(
                        stableId,
                        root))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        internal bool UpdateTriggerConditionRootForTests(
            string stableId,
            GameplayTriggerConditionNodeAuthoring root)
        {
            return UpdateTriggerConditionRoot(stableId, root);
        }

        private static bool IsBuffTriggerTemplate(string templateId)
        {
            return string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal);
        }

        private bool UpdateTriggerPayloads(
            string stableId,
            long payload0,
            long payload1,
            long payload2 = 0L)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发器"))
            {
                if (!_triggerDocument.TryUpdateTemplatePayloads(
                        stableId,
                        payload0,
                        payload1,
                        payload2))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool SetTriggerEnabled(
            string stableId,
            bool enabled)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发器启用状态"))
            {
                if (!_triggerDocument.TrySetTemplateEnabled(
                        stableId,
                        enabled))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool UpdateTriggerPolicy(
            string stableId,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发器策略"))
            {
                if (!_triggerDocument.TryUpdateTemplatePolicy(
                        stableId,
                        stateScope,
                        presenceLimit,
                        cooldownFrames,
                        maxActivations))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool UpdateTriggerSubjectRelation(
            string stableId,
            GameplayTargetRelationFilter relationFilter,
            int referenceTeamId)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发对象筛选"))
            {
                if (!_triggerDocument.TryUpdateSubjectRelation(
                        stableId,
                        relationFilter,
                        referenceTeamId))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool UpdateTriggerSubjectCapabilities(
            string stableId,
            GameplayTargetCapabilities requiredCapabilities)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "修改 CharacterTest 关卡触发对象能力"))
            {
                if (!_triggerDocument.TryUpdateSubjectRequiredCapabilities(
                        stableId,
                        requiredCapabilities))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool UpdateTriggerCooldown(
            string stableId,
            long cooldownFrames)
        {
            if (!TryGetTriggerPolicy(
                    stableId,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out _,
                    out int maxActivations))
            {
                return false;
            }

            return UpdateTriggerPolicy(
                stableId,
                stateScope,
                presenceLimit,
                cooldownFrames,
                maxActivations);
        }

        private bool UpdateTriggerActivationLimit(
            string stableId,
            int maxActivations)
        {
            if (!TryGetTriggerPolicy(
                    stableId,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out long cooldownFrames,
                    out _))
            {
                return false;
            }

            return UpdateTriggerPolicy(
                stableId,
                stateScope,
                presenceLimit,
                cooldownFrames,
                maxActivations);
        }

        private bool TryGetTriggerPolicy(
            string stableId,
            out GameplayTriggerStateScope stateScope,
            out GameplayTriggerPresenceLimit presenceLimit,
            out long cooldownFrames,
            out int maxActivations)
        {
            stateScope = GameplayTriggerStateScope.PerInstance;
            presenceLimit = GameplayTriggerPresenceLimit.Ignore;
            cooldownFrames = 0L;
            maxActivations = 0;
            return _triggerDocument != null
                && _triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                && _triggerDocument.TryGetTemplateSettings(
                    entry,
                    out _,
                    out stateScope,
                    out presenceLimit,
                    out cooldownFrames,
                    out maxActivations);
        }

        private bool DuplicateTriggerEntry(
            string stableId,
            out CharacterTestLevelGameplayTriggerEntry duplicate)
        {
            duplicate = null;
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "复制 CharacterTest 关卡触发器"))
            {
                if (!_triggerDocument.TryDuplicateEntry(
                        stableId,
                        out duplicate))
                {
                    return false;
                }

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool RemoveTriggerEntry(string stableId)
        {
            if (_triggerDocument == null)
                return false;

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "删除 CharacterTest 关卡触发器"))
            {
                if (!_triggerDocument.TryRemoveEntry(stableId))
                    return false;

                MarkTriggerDirty();
                transaction.Commit();
                return true;
            }
        }

        private bool IsEditableTriggerRegion(string regionStableId)
        {
            if (_document?.Regions == null
                || string.IsNullOrWhiteSpace(regionStableId))
            {
                return false;
            }

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region =
                    _document.Regions[i];
                if (region != null
                    && region.Enabled
                    && region.RegionKind
                        == LevelWorldObjectRegionKind.TriggerRegion
                    && string.Equals(
                        region.StableId,
                        regionStableId.Trim(),
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal bool HasTriggerReferenceForTests(string regionStableId)
        {
            return CharacterTestLevelGameplayTriggerAuthoringAdapter
                .TryFindEntryForRegion(
                    _triggerDocument,
                    regionStableId,
                    out _);
        }

        internal bool TryGetTriggerPayloadsForTests(
            string stableId,
            out long payload0,
            out long payload1,
            out long payload2)
        {
            payload0 = 0L;
            payload1 = 0L;
            payload2 = 0L;
            return _triggerDocument != null
                && _triggerDocument.TryFindEntry(stableId, out CharacterTestLevelGameplayTriggerEntry entry)
                && _triggerDocument.TryGetTemplatePayloads(
                    entry,
                    out payload0,
                    out payload1,
                    out payload2);
        }

        internal bool TryGetTriggerSettingsForTests(
            string stableId,
            out bool initiallyEnabled,
            out GameplayTriggerStateScope stateScope,
            out GameplayTriggerPresenceLimit presenceLimit,
            out long cooldownFrames,
            out int maxActivations)
        {
            initiallyEnabled = false;
            stateScope = GameplayTriggerStateScope.PerInstance;
            presenceLimit = GameplayTriggerPresenceLimit.Ignore;
            cooldownFrames = 0L;
            maxActivations = 0;
            return _triggerDocument != null
                && _triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                && _triggerDocument.TryGetTemplateSettings(
                    entry,
                    out initiallyEnabled,
                    out stateScope,
                    out presenceLimit,
                    out cooldownFrames,
                    out maxActivations);
        }

        internal bool HasTriggerValidationIssueForTests(string code)
        {
            ForceValidate();
            if (_report?.Issues == null || string.IsNullOrWhiteSpace(code))
                return false;
            for (int i = 0; i < _report.Issues.Count; i++)
            {
                if (string.Equals(
                        _report.Issues[i]?.Code,
                        code.Trim(),
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static LevelWorldObjectAuthoringIssue TriggerSceneError(
            string code,
            string path,
            string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }
    }
}
