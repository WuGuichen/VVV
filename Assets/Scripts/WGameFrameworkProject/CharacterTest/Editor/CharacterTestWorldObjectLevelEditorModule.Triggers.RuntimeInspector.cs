using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private VisualElement CreateGameplayTriggerRuntimeInspector(
            LevelWorldObjectRegionDefinition region)
        {
            IReadOnlyList<CharacterTestLevelGameplayTriggerEntry> entries =
                _triggerDocument?.FindEntriesForRegion(region.StableId)
                ?? Array.Empty<CharacterTestLevelGameplayTriggerEntry>();
            return CreateGameplayTriggerRuntimeInspector(entries);
        }

        private VisualElement CreateGameplayTriggerRuntimeInspector(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            return CreateGameplayTriggerRuntimeInspector(
                entry == null
                    ? Array.Empty<
                        CharacterTestLevelGameplayTriggerEntry>()
                    : new[] { entry });
        }

        private VisualElement CreateGameplayTriggerRuntimeInspector(
            IReadOnlyList<CharacterTestLevelGameplayTriggerEntry> entries)
        {
            var root = new VisualElement
            {
                name = "character-test-trigger-runtime-inspector"
            };
            if (entries.Count == 0)
            {
                VisualElement empty = CreateInspectorSection("玩法触发器");
                empty.Add(new Label("该区域尚未配置玩法触发器。"));
                root.Add(empty);
                return root;
            }

            GameManager gameManager = ResolveTriggerRuntimeGameManager();
            CharacterTestGameplayTriggerSceneCache cache =
                gameManager?.GameplayTriggerSceneCache;

            VisualElement overview = CreateInspectorSection("触发器运行状态");
            var startupLabel = new Label
            {
                name = "character-test-trigger-runtime-startup"
            };
            var catalogLabel = new Label
            {
                name = "character-test-trigger-runtime-catalog"
            };
            overview.Add(startupLabel);
            overview.Add(catalogLabel);
            root.Add(overview);

            var rows = new TriggerRuntimeInspectorRow[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry entry = entries[i];
                rows[i] = CreateTriggerRuntimeInspectorRow(entry, cache);
                root.Add(rows[i].Root);
            }

            void RefreshRuntimeDiagnostics()
            {
                CharacterTestLevelGameplayTriggerRuntimeStatus status =
                    gameManager?.RuntimeContext?.Slice
                        ?.LevelGameplayTriggerStatus;
                UpdateTriggerRuntimeOverview(
                    startupLabel,
                    catalogLabel,
                    cache,
                    status);
                for (int i = 0; i < rows.Length; i++)
                    UpdateTriggerRuntimeInspectorRow(rows[i], status);
            }

            RefreshRuntimeDiagnostics();
            root.schedule.Execute(RefreshRuntimeDiagnostics).Every(200);
            return root;
        }

        private GameManager ResolveTriggerRuntimeGameManager()
        {
            if (_selectedRow != null
                && TryResolveRowTransform(
                    _selectedRow,
                    out Transform transform)
                && transform != null)
            {
                GameManager parent =
                    transform.GetComponentInParent<GameManager>();
                if (parent != null)
                    return parent;
            }

            return UnityEngine.Object.FindAnyObjectByType<GameManager>();
        }

        private static TriggerRuntimeInspectorRow
            CreateTriggerRuntimeInspectorRow(
                CharacterTestLevelGameplayTriggerEntry entry,
                CharacterTestGameplayTriggerSceneCache cache)
        {
            VisualElement section = CreateInspectorSection(
                string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? entry.StableId
                    : entry.DisplayName);
            section.name =
                "character-test-trigger-runtime-entry-" + entry.StableId;

            CharacterTestGameplayTriggerSensorCacheRow cacheRow = null;
            cache?.TryGetSensorRow(entry.SourceBindingId, out cacheRow);
            CharacterTestGameplayTriggerAttributeThresholdCacheRow
                thresholdCacheRow = null;
            cache?.TryGetAttributeThresholdRow(
                entry.SourceBindingId,
                out thresholdCacheRow);
            section.Add(new Label(
                "稳定 ID：" + entry.StableId
                + "  ·  Source：" + entry.SourceBindingId));

            var queryLabel = new Label
            {
                name = "character-test-trigger-runtime-query"
            };
            queryLabel.text = cacheRow == null
                ? thresholdCacheRow == null
                    ? "来源：运行时缓存中没有该 Source"
                    : "来源：当前玩家生命值"
                        + "  ·  Attribute "
                        + thresholdCacheRow.AttributeId
                        + "  ·  Provider "
                        + thresholdCacheRow.ProviderId
                : FormatTriggerQuery(cacheRow);
            section.Add(queryLabel);

            var sensorLabel = new Label
            {
                name = "character-test-trigger-runtime-sensor"
            };
            var occurrenceLabel = new Label
            {
                name = "character-test-trigger-runtime-occurrence"
            };
            var actionLabel = new Label
            {
                name = "character-test-trigger-runtime-action"
            };
            section.Add(sensorLabel);
            section.Add(occurrenceLabel);
            section.Add(actionLabel);
            return new TriggerRuntimeInspectorRow(
                section,
                entry.SourceBindingId,
                sensorLabel,
                occurrenceLabel,
                actionLabel);
        }

        private static void UpdateTriggerRuntimeOverview(
            Label startupLabel,
            Label catalogLabel,
            CharacterTestGameplayTriggerSceneCache cache,
            CharacterTestLevelGameplayTriggerRuntimeStatus status)
        {
            if (!Application.isPlaying)
            {
                startupLabel.text = "运行状态：未进入 Play Mode";
            }
            else if (status == null)
            {
                startupLabel.text = "运行状态：尚未创建触发器运行时";
            }
            else
            {
                startupLabel.text =
                    "运行状态：" + status.StartupState
                    + (status.StartupFailure
                        == CharacterTestLevelGameplayTriggerStartupFailure.None
                        ? string.Empty
                        : "  ·  " + status.StartupFailure)
                    + (string.IsNullOrWhiteSpace(status.StartupMessage)
                        ? string.Empty
                        : "\n" + status.StartupMessage);
            }

            catalogLabel.text = cache == null
                ? "缓存：当前场景没有 GameManager 缓存"
                : "缓存：Trigger " + cache.TriggerCatalogHash
                    + "  ·  Sensor " + cache.SensorCatalogHash
                    + "  ·  Attribute "
                    + cache.AttributeThresholdCatalogHash
                    + "  ·  Combined " + cache.CombinedCatalogHash;
        }

        private static void UpdateTriggerRuntimeInspectorRow(
            TriggerRuntimeInspectorRow row,
            CharacterTestLevelGameplayTriggerRuntimeStatus status)
        {
            if (status == null
                || !status.TryCapture(
                    row.SourceBindingId,
                    out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                        snapshot))
            {
                row.SensorLabel.text = "感知：无运行数据";
                row.OccurrenceLabel.text = "事件：无运行数据";
                row.ActionLabel.text = "动作：无运行数据";
                return;
            }

            row.SensorLabel.text = snapshot.HasAttributeThresholdResult
                ? "采样："
                    + (snapshot.AttributeThresholdResult.HasSubject
                        ? "HP "
                            + snapshot.AttributeThresholdResult.SampledValue
                            + "  ·  Subject "
                            + snapshot.AttributeThresholdResult.Subject
                        : "当前玩家不存在")
                    + "  ·  "
                    + (snapshot.AttributeThresholdResult.SubmitResult.Success
                        ? "已提交"
                        : snapshot.AttributeThresholdResult.SubmitResult
                            .ErrorCode.ToString())
                : snapshot.HasSensorResult
                    ? "感知：" + snapshot.SensorResult.Status
                    + "  ·  命中 "
                    + snapshot.SensorResult.QueryHitCount
                    + "  ·  通过 "
                    + snapshot.SensorResult.SubmittedSubjectCount
                    + "  ·  过滤 "
                    + snapshot.SensorResult.FilteredSubjectCount
                    + (snapshot.SensorResult.FirstFilterRejectReason
                        == GameplayTargetRejectReason.None
                            ? string.Empty
                            : "  ·  首个原因 "
                                + snapshot.SensorResult
                                    .FirstFilterRejectReason)
                    + (snapshot.SensorResult.TriggerErrorCode
                        == GameplayTriggerErrorCode.None
                        ? string.Empty
                        : "  ·  " + snapshot.SensorResult.TriggerErrorCode)
                    : "感知：等待首次 Tick";

            row.OccurrenceLabel.text = snapshot.HasOccurrence
                ? "事件：" + snapshot.Occurrence.OccurrenceKind
                    + "  ·  Frame "
                    + snapshot.Occurrence.Frame.Value
                    + "  ·  Subject "
                    + snapshot.Occurrence.Subject
                : "事件：尚未发生";

            if (snapshot.HasActionResult)
            {
                row.ActionLabel.text =
                    "动作：" + snapshot.ActionResult.ActionKindId.Value
                    + "  ·  " + snapshot.ActionResult.Status
                    + "  ·  Reason "
                    + snapshot.ActionResult.ReasonCode
                    + (snapshot.DroppedEventCount > 0
                        ? "  ·  丢弃 " + snapshot.DroppedEventCount
                        : string.Empty);
            }
            else if (snapshot.HasActionDispatch)
            {
                row.ActionLabel.text =
                    "动作：" + snapshot.ActionDispatch.ActionKindId.Value
                    + "  ·  已派发，等待结果";
            }
            else
            {
                row.ActionLabel.text = "动作：尚未派发";
            }
        }

        private static string FormatTriggerQuery(
            CharacterTestGameplayTriggerSensorCacheRow row)
        {
            FixVector3 center = row.Center;
            FixVector3 halfExtents = row.HalfExtents;
            return "查询：Combat AABB"
                + "  ·  Center " + FormatFixVector(center)
                + "  ·  Half " + FormatFixVector(halfExtents)
                + "\nLayer " + row.LayerMask
                + "  ·  Hits " + row.MaxQueryHits
                + "  ·  Subjects " + row.MaxMappedSubjects
                + "\n筛选："
                + FormatTargetIdentityType(
                    row.SubjectFilter.AcceptedIdentityTypes)
                + " · "
                + FormatTargetCapabilities(
                    row.SubjectFilter.RequiredCapabilities)
                + (row.SubjectFilter.RequireAlive
                    ? " · 存活"
                    : string.Empty)
                + " · "
                + FormatTargetAudience(
                    row.SubjectFilter.RelationFilter)
                + (row.SubjectFilter.RelationFilter
                    == GameplayTargetRelationFilter.Any
                        ? string.Empty
                        : "（相对队伍 "
                            + row.SubjectFilter.ReferenceTeamId
                            + "）");
        }

        private static string FormatFixVector(in FixVector3 value)
        {
            return "("
                + value.X
                + ", "
                + value.Y
                + ", "
                + value.Z
                + ")";
        }

        private readonly struct TriggerRuntimeInspectorRow
        {
            internal TriggerRuntimeInspectorRow(
                VisualElement root,
                int sourceBindingId,
                Label sensorLabel,
                Label occurrenceLabel,
                Label actionLabel)
            {
                Root = root;
                SourceBindingId = sourceBindingId;
                SensorLabel = sensorLabel;
                OccurrenceLabel = occurrenceLabel;
                ActionLabel = actionLabel;
            }

            internal VisualElement Root { get; }
            internal int SourceBindingId { get; }
            internal Label SensorLabel { get; }
            internal Label OccurrenceLabel { get; }
            internal Label ActionLabel { get; }
        }
    }
}
