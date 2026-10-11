using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private static void AddMechanicTuningChip(VisualElement parent, string text)
        {
            var chip = new Label(text ?? string.Empty);
            chip.AddToClassList("ca-projectile-chip");
            parent.Add(chip);
        }

        private bool HasPendingMechanicTuningAuthoringChanges => _pendingMechanicTuningDrafts.Count > 0;

        private CharacterActionMechanicTuningAuthoringStore LoadMechanicTuningAuthoringStoreForWindow()
        {
            CharacterActionMechanicTuningAuthoringStore store =
                CharacterActionMechanicTuningAuthoringStore.Load(CreateMechanicTuningStoreOptions());
            return _pendingMechanicTuningDrafts.Count == 0
                ? store
                : store.WithDraftOverrides(_pendingMechanicTuningDrafts);
        }

        private CharacterActionMechanicTuningAuthoringStore LoadMechanicTuningAuthoringStoreFromDisk()
        {
            return CharacterActionMechanicTuningAuthoringStore.Load(CreateMechanicTuningStoreOptions());
        }

        private CharacterActionMechanicTuningAuthoringStoreOptions CreateMechanicTuningStoreOptions()
        {
            return new CharacterActionMechanicTuningAuthoringStoreOptions(
                string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                    ? CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath
                    : _abilityAuthoringJsonPath,
                CharacterActionMechanicTuningAuthoringStore.DefaultGeneratedMechanicTuningsPath,
                CharacterActionMechanicTuningAuthoringStore.DefaultPlayerMirrorMechanicTuningsPath);
        }

        private CharacterActionVfxDefinitionCatalog LoadVfxDefinitionCatalogForWindow()
        {
            return CharacterActionVfxDefinitionCatalog.Load(
                CharacterActionVfxDefinitionCatalog.DefaultGeneratedDefinitionsPath,
                string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                    ? CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath
                    : _abilityAuthoringJsonPath);
        }

        private void AddArcMineRequestInspector(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("ca-projectile-inspector");
            parent.Add(section);

            CharacterActionMechanicTuningAuthoringStore store = LoadMechanicTuningAuthoringStoreForWindow();
            if (!store.TryGetArcMine(out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record))
            {
                section.Add(new HelpBox(
                    "缺少数值源: "
                    + CharacterActionMechanicTuningAuthoringStore.ArcMineTuningStableId
                    + "。运行时 fallback 仍可工作，但 ability_authoring.json 中没有这条 tuning 时不能在这里编辑。",
                    HelpBoxMessageType.Warning));
                return;
            }

            AddArcMineTuningSummary(section, record);
            if (!record.HasSource)
            {
                section.Add(new HelpBox(
                    "触发地雷当前只来自 fallback 数据。需要先在 characterTestMechanicTunings 中补 source 数据，才能编辑。",
                    HelpBoxMessageType.Warning));
                return;
            }

            AddArcMineTuningFields(section, record.CreateDraft());
            section.Add(new HelpBox(
                "飞行由抛物线飞行体和命中后的下落飞行体决定；落地后布雷、入圈启动引信和引信结束爆炸由 CharacterTest 陷阱 runtime、CombatPhysicsWorld 和 HitResolveSystem 决定。VFX 只读取运行时事件，不参与触发或伤害判定。",
                HelpBoxMessageType.Info));
        }

        private void AddRuneTurretRequestInspector(VisualElement parent)
        {
            var section = new VisualElement();
            section.AddToClassList("ca-projectile-inspector");
            parent.Add(section);

            CharacterActionMechanicTuningAuthoringStore store = LoadMechanicTuningAuthoringStoreForWindow();
            if (!store.TryGetRuneTurret(out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record))
            {
                section.Add(new HelpBox(
                    "缺少数值源: "
                    + CharacterActionMechanicTuningAuthoringStore.RuneTurretTuningStableId
                    + "。运行时 fallback 仍可工作，但 ability_authoring.json 中没有这条 tuning 时不能在这里编辑。",
                    HelpBoxMessageType.Warning));
                return;
            }

            AddRuneTurretTuningSummary(section, record);
            if (!record.HasSource)
            {
                section.Add(new HelpBox(
                    "符文炮台当前只来自 fallback 数据。需要先在 characterTestMechanicTunings 中补 source 数据，才能编辑。",
                    HelpBoxMessageType.Warning));
                return;
            }

            AddRuneTurretTuningFields(section, record.CreateDraft());
            section.Add(new HelpBox(
                "投放飞行由抛物线 projectile 决定；炮台索敌只在固定 think frame 查询，开火只生成标准 projectile。VFX 只读运行时事件，不参与索敌、命中或伤害判定。",
                HelpBoxMessageType.Info));
        }

        private void AddRuneTurretTuningSummary(
            VisualElement parent,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record)
        {
            var header = new VisualElement();
            header.AddToClassList("ca-projectile-summary");

            var title = new Label("符文炮台 / Rune Turret");
            title.AddToClassList("ca-projectile-summary__title");
            header.Add(title);

            var chips = new VisualElement();
            chips.AddToClassList("ca-projectile-chip-row");
            AddMechanicTuningChip(chips, "玩法请求 " + CharacterActionMechanicTuningAuthoringStore.RuneTurretGameplayRequestId);
            AddMechanicTuningChip(chips, record.Summary);
            AddMechanicTuningChip(chips, "抛物线投放");
            AddMechanicTuningChip(chips, "自动开火");
            AddMechanicTuningChip(chips, "数值源");
            if (_pendingMechanicTuningDrafts.ContainsKey(record.StableId))
                AddMechanicTuningChip(chips, "未保存");
            header.Add(chips);
            parent.Add(header);
        }

        private void AddRuneTurretTuningFields(
            VisualElement parent,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft)
        {
            CharacterActionVfxDefinitionCatalog vfxCatalog = LoadVfxDefinitionCatalogForWindow();

            VisualElement deployFields = AddCard(parent, "投放规则");
            AddMechanicIntegerField(
                deployFields,
                "锁定投放范围 (m)",
                draft.DeployRangeMeters,
                value => StageRuneTurretTuningChange(next => next.DeployRangeMeters = Math.Max(1, value)),
                "锁定敌方目标在该范围内时，炮台投放体会投向目标脚下。");
            AddMechanicIntegerField(
                deployFields,
                "无锁投放距离 (m)",
                draft.FallbackForwardMeters,
                value => StageRuneTurretTuningChange(next => next.FallbackForwardMeters = Math.Max(1, value)),
                "没有有效锁定敌方目标时，炮台投放体会投向施法者前方距离。");

            VisualElement turretFields = AddCard(parent, "炮台规则");
            AddMechanicIntegerField(
                turretFields,
                "激活延迟 (帧)",
                draft.WarmupFrames,
                value => StageRuneTurretTuningChange(next => next.WarmupFrames = Math.Max(0, value)),
                "炮台落地创建后等待多少帧才开始索敌开火。");
            AddMechanicIntegerField(
                turretFields,
                "炮台存在时间 (帧)",
                draft.DurationFrames,
                value => StageRuneTurretTuningChange(next => next.DurationFrames = Math.Max(1, value)),
                "炮台没有提前耗尽开火次数时的最大存在时间。");
            AddMechanicMetersField(
                turretFields,
                "炮口高度 (m)",
                draft.MuzzleHeightCentimeters,
                value => StageRuneTurretTuningChange(next => next.MuzzleHeightCentimeters = Math.Max(1, value)),
                "炮台 projectile 出生点相对地面中心的高度。");

            VisualElement fireFields = AddCard(parent, "索敌与开火");
            AddMechanicMetersField(
                fireFields,
                "索敌半径 (m)",
                draft.ScanRadiusCentimeters,
                value => StageRuneTurretTuningChange(next => next.ScanRadiusCentimeters = Math.Max(1, value)),
                "炮台扫描 hostile 目标的半径。");
            AddMechanicIntegerField(
                fireFields,
                "空闲扫描间隔 (帧)",
                draft.ScanIntervalFrames,
                value => StageRuneTurretTuningChange(next => next.ScanIntervalFrames = Math.Max(1, value)),
                "没有目标时，隔多少帧重新扫描一次。");
            AddMechanicIntegerField(
                fireFields,
                "开火间隔 (帧)",
                draft.FireIntervalFrames,
                value => StageRuneTurretTuningChange(next => next.FireIntervalFrames = Math.Max(1, value)),
                "每次成功开火后的下一次 think frame 间隔。");
            AddMechanicIntegerField(
                fireFields,
                "最多开火次数",
                draft.MaxShots,
                value => StageRuneTurretTuningChange(next => next.MaxShots = Math.Max(1, value)),
                "炮台最多发射多少枚符文弹，用完后移除。");

            VisualElement vfxFields = AddCard(parent, "表现特效");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "部署特效",
                draft.DeployVfxDefinitionId,
                value => StageRuneTurretTuningChange(next => next.DeployVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "炮台落地创建时播放的 VFX 定义 ID。");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "待机特效",
                draft.IdleVfxDefinitionId,
                value => StageRuneTurretTuningChange(next => next.IdleVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "炮台存在期间播放的待机 VFX 定义 ID。");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "开火特效",
                draft.FireVfxDefinitionId,
                value => StageRuneTurretTuningChange(next => next.FireVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "炮台每次生成 shot projectile 时播放的 VFX 定义 ID。");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "消失特效",
                draft.ExpireVfxDefinitionId,
                value => StageRuneTurretTuningChange(next => next.ExpireVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "炮台持续时间结束、施法者死亡或耗尽开火次数时播放的 VFX 定义 ID。");
        }

        private void AddArcMineTuningSummary(
            VisualElement parent,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record)
        {
            var header = new VisualElement();
            header.AddToClassList("ca-projectile-summary");

            var title = new Label("投掷地雷 / Arc Mine");
            title.AddToClassList("ca-projectile-summary__title");
            header.Add(title);

            var chips = new VisualElement();
            chips.AddToClassList("ca-projectile-chip-row");
            AddMechanicTuningChip(chips, "玩法请求 " + CharacterActionMechanicTuningAuthoringStore.ArcMineGameplayRequestId);
            AddMechanicTuningChip(chips, record.Summary);
            AddMechanicTuningChip(chips, "抛物线投掷");
            AddMechanicTuningChip(chips, "命中后下落");
            AddMechanicTuningChip(chips, "数值源");
            if (_pendingMechanicTuningDrafts.ContainsKey(record.StableId))
                AddMechanicTuningChip(chips, "未保存");
            header.Add(chips);
            parent.Add(header);
        }

        private void AddArcMineTuningFields(
            VisualElement parent,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft)
        {
            CharacterActionVfxDefinitionCatalog vfxCatalog = LoadVfxDefinitionCatalogForWindow();

            VisualElement throwFields = AddCard(parent, "投掷规则");
            AddMechanicIntegerField(
                throwFields,
                "锁定投掷范围 (m)",
                draft.DeployRangeMeters,
                value => StageArcMineTuningChange(next => next.DeployRangeMeters = Math.Max(1, value)),
                "锁定敌方目标在该范围内时，地雷飞行体会投向目标脚下。");
            AddMechanicIntegerField(
                throwFields,
                "无锁投掷距离 (m)",
                draft.FallbackForwardMeters,
                value => StageArcMineTuningChange(next => next.FallbackForwardMeters = Math.Max(1, value)),
                "没有有效锁定敌方目标时，地雷飞行体会投向施法者前方的距离。");

            VisualElement triggerFields = AddCard(parent, "触发与爆炸");
            AddMechanicIntegerField(
                triggerFields,
                "入圈后爆炸延迟 (帧)",
                draft.FuseDelayFrames,
                value => StageArcMineTuningChange(next => next.FuseDelayFrames = Math.Max(1, value)),
                "敌方进入触发半径后，等待多少帧才爆炸。");
            AddMechanicIntegerField(
                triggerFields,
                "地雷存在时间 (帧)",
                draft.DurationFrames,
                value => StageArcMineTuningChange(next => next.DurationFrames = Math.Max(1, value)),
                "未触发地雷的最大存在时间；已点燃引信后会按引信时间爆炸。");
            AddMechanicMetersField(
                triggerFields,
                "触发半径 (m)",
                draft.TriggerRadiusCentimeters,
                value => StageArcMineTuningChange(next => next.TriggerRadiusCentimeters = Math.Max(1, value)),
                "敌方进入该半径后会启动地雷引信。");
            AddMechanicMetersField(
                triggerFields,
                "爆炸半径 (m)",
                draft.BlastRadiusCentimeters,
                value => StageArcMineTuningChange(next => next.BlastRadiusCentimeters = Math.Max(1, value)),
                "地雷引信结束后用于 HitResolve 的范围半径。");
            AddMechanicIntegerField(
                triggerFields,
                "爆炸最多命中目标",
                draft.MaxBlastTargets,
                value => StageArcMineTuningChange(next => next.MaxBlastTargets = Math.Max(1, value)),
                "爆发最多接受命中的敌方目标数量。");

            VisualElement vfxFields = AddCard(parent, "表现特效");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "落地特效",
                draft.DeployVfxDefinitionId,
                value => StageArcMineTuningChange(next => next.DeployVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "地雷落地部署时播放的 VFX 定义 ID。");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "引信特效",
                draft.FuseVfxDefinitionId,
                value => StageArcMineTuningChange(next => next.FuseVfxDefinitionId = Math.Max(0, value)),
                vfxCatalog,
                allowNone: true,
                "敌方进入触发半径、引信开始时播放的 VFX；0 表示不播放。");
            AddMechanicVfxDefinitionField(
                vfxFields,
                "爆炸特效",
                draft.TriggerVfxDefinitionId,
                value => StageArcMineTuningChange(next => next.TriggerVfxDefinitionId = Math.Max(1, value)),
                vfxCatalog,
                allowNone: false,
                "地雷引信结束爆炸时播放的 VFX 定义 ID。");
        }

        private void AddMechanicVfxDefinitionField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            CharacterActionVfxDefinitionCatalog catalog,
            bool allowNone,
            string tooltip)
        {
            CharacterActionVfxDefinitionPickerUi.AddField(
                parent,
                label,
                value,
                allowNone,
                catalog,
                setter,
                tooltip + " VFX 只读取运行时事件，不参与命中、触发、伤害或落点。");
        }

        private void AddMechanicIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            string tooltip)
        {
            var field = new IntegerField(label)
            {
                value = value,
                isDelayed = true,
                tooltip = tooltip ?? string.Empty,
            };
            field.RegisterValueChangedCallback(evt => setter?.Invoke(evt.newValue));
            parent.Add(field);
        }

        private void AddMechanicMetersField(
            VisualElement parent,
            string label,
            int centimeters,
            Action<int> setter,
            string tooltip)
        {
            var field = new FloatField(label)
            {
                value = centimeters / 100f,
                isDelayed = true,
                tooltip = tooltip ?? string.Empty,
            };
            field.RegisterValueChangedCallback(evt =>
            {
                int nextCentimeters = (int)Math.Round(Math.Max(0.01f, evt.newValue) * 100f);
                setter?.Invoke(nextCentimeters);
            });
            parent.Add(field);
        }

        private void StageArcMineTuningChange(
            Action<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> mutate)
        {
            PushUndoSnapshot();
            _redoHistory.Clear();

            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                ApplyPendingMechanicTuningDraftChange(
                    CharacterActionMechanicTuningAuthoringStore.ArcMineTuningStableId,
                    mutate);
            if (!result.Success)
            {
                RemoveLastUndoSnapshot();
                EditorUtility.DisplayDialog("投掷地雷数值修改失败", result.Message, "OK");
                return;
            }

            _validationDirty = true;
            _timelineView?.RefreshCommandState();
            UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            RefreshUi(UiRefreshScope.Tracks);
        }

        private void StageRuneTurretTuningChange(
            Action<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> mutate)
        {
            PushUndoSnapshot();
            _redoHistory.Clear();

            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                ApplyPendingMechanicTuningDraftChange(
                    CharacterActionMechanicTuningAuthoringStore.RuneTurretTuningStableId,
                    mutate);
            if (!result.Success)
            {
                RemoveLastUndoSnapshot();
                EditorUtility.DisplayDialog("符文炮台数值修改失败", result.Message, "OK");
                return;
            }

            _validationDirty = true;
            _timelineView?.RefreshCommandState();
            UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            RefreshUi(UiRefreshScope.Tracks);
        }

        private CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult
            ApplyPendingMechanicTuningDraftChange(
            string stableId,
            Action<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> mutate)
        {
            CharacterActionMechanicTuningAuthoringStore windowStore = LoadMechanicTuningAuthoringStoreForWindow();
            if (!TryGetMechanicTuningRecord(
                    windowStore,
                    stableId,
                    out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record))
            {
                return CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "找不到玩法数值: " + (stableId ?? string.Empty));
            }

            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft =
                record.CreateDraft();
            mutate?.Invoke(draft);
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult validation =
                LoadMechanicTuningAuthoringStoreFromDisk().ValidateDraft(draft);
            if (!validation.Success)
                return validation;

            StorePendingMechanicTuningDraft(draft);
            return CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult.SuccessResult(
                "玩法数值修改已暂存。",
                null);
        }

        private static bool TryGetMechanicTuningRecord(
            CharacterActionMechanicTuningAuthoringStore store,
            string stableId,
            out CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringRecord record)
        {
            record = null;
            if (store == null || string.IsNullOrWhiteSpace(stableId))
                return false;
            if (string.Equals(
                    stableId,
                    CharacterActionMechanicTuningAuthoringStore.ArcMineTuningStableId,
                    StringComparison.Ordinal))
            {
                return store.TryGetArcMine(out record);
            }

            if (string.Equals(
                    stableId,
                    CharacterActionMechanicTuningAuthoringStore.RuneTurretTuningStableId,
                    StringComparison.Ordinal))
            {
                return store.TryGetRuneTurret(out record);
            }

            return false;
        }

        private void StorePendingMechanicTuningDraft(
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.StableId))
                return;

            CaptureMechanicTuningDraftBaselineForSession();
            _pendingMechanicTuningDrafts[draft.StableId] = draft.Clone();
        }

        private void RestorePendingMechanicTuningDrafts(
            IEnumerable<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> drafts)
        {
            _pendingMechanicTuningDrafts.Clear();
            if (drafts == null)
                return;

            foreach (CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft in drafts)
            {
                if (draft == null || string.IsNullOrWhiteSpace(draft.StableId))
                    continue;
                _pendingMechanicTuningDrafts[draft.StableId] = draft.Clone();
            }
        }

        private CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
            CreatePendingMechanicTuningDrafts()
        {
            var drafts =
                new CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[
                    _pendingMechanicTuningDrafts.Count];
            int index = 0;
            foreach (KeyValuePair<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> pair
                in _pendingMechanicTuningDrafts)
            {
                drafts[index++] = pair.Value?.Clone();
            }

            return drafts;
        }

        private bool SavePendingMechanicTuningAuthoringChanges()
        {
            if (_pendingMechanicTuningDrafts.Count == 0)
                return true;

            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                LoadMechanicTuningAuthoringStoreFromDisk().UpdateTunings(CreatePendingMechanicTuningDrafts());
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("保存玩法数值失败", result.Message, "OK");
                _timelineView?.RefreshCommandState();
                return false;
            }

            _pendingMechanicTuningDrafts.Clear();
            if (result.SourceRoot != null)
                _abilityAuthoringRoot = (JObject)result.SourceRoot.DeepClone();
            _validationDirty = true;
            _timelineView?.RefreshCommandState();
            return true;
        }
    }

    internal sealed class CharacterActionVfxDefinitionCatalog
    {
        public const string DefaultGeneratedDefinitionsPath = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";

        private readonly Dictionary<int, CharacterActionVfxDefinitionChoice> _choicesById;
        private readonly CharacterActionVfxDefinitionChoice[] _choices;
        private readonly string[] _diagnostics;

        private CharacterActionVfxDefinitionCatalog(
            CharacterActionVfxDefinitionChoice[] choices,
            string sourcePath,
            string[] diagnostics)
        {
            _choices = choices ?? Array.Empty<CharacterActionVfxDefinitionChoice>();
            SourcePath = sourcePath ?? string.Empty;
            _diagnostics = diagnostics ?? Array.Empty<string>();
            _choicesById = new Dictionary<int, CharacterActionVfxDefinitionChoice>(_choices.Length);
            for (int i = 0; i < _choices.Length; i++)
            {
                CharacterActionVfxDefinitionChoice choice = _choices[i];
                if (choice != null && choice.DefinitionId > 0 && !_choicesById.ContainsKey(choice.DefinitionId))
                    _choicesById.Add(choice.DefinitionId, choice);
            }
        }

        public string SourcePath { get; }
        public bool HasDefinitions => _choices.Length > 0;
        public IReadOnlyList<string> Diagnostics => _diagnostics;

        public static CharacterActionVfxDefinitionCatalog Load(
            string generatedDefinitionsPath,
            string abilityAuthoringPath)
        {
            var diagnostics = new List<string>();
            string generatedPath = string.IsNullOrWhiteSpace(generatedDefinitionsPath)
                ? DefaultGeneratedDefinitionsPath
                : generatedDefinitionsPath;
            CharacterActionVfxDefinitionChoice[] generated =
                TryReadDefinitionsFile(generatedPath, "definitions", diagnostics);
            if (generated.Length > 0)
                return new CharacterActionVfxDefinitionCatalog(generated, generatedPath, diagnostics.ToArray());

            CharacterActionVfxDefinitionChoice[] source =
                TryReadDefinitionsFile(abilityAuthoringPath, "vfxDefinitions", diagnostics);
            return new CharacterActionVfxDefinitionCatalog(
                source,
                source.Length > 0 ? abilityAuthoringPath : string.Empty,
                diagnostics.ToArray());
        }

        public bool TryGet(int definitionId, out CharacterActionVfxDefinitionChoice choice)
        {
            return _choicesById.TryGetValue(definitionId, out choice);
        }

        public CharacterActionVfxDefinitionChoice GetCurrentChoice(int definitionId, bool allowNone)
        {
            if (allowNone && definitionId == 0)
                return CharacterActionVfxDefinitionChoice.None;
            return TryGet(definitionId, out CharacterActionVfxDefinitionChoice choice)
                ? choice
                : CharacterActionVfxDefinitionChoice.CreateUnknown(definitionId);
        }

        public List<CharacterActionVfxDefinitionChoice> BuildChoices(
            int currentDefinitionId,
            bool allowNone,
            string query)
        {
            var result = new List<CharacterActionVfxDefinitionChoice>(_choices.Length + 2);
            query = query ?? string.Empty;
            CharacterActionVfxDefinitionChoice current = GetCurrentChoice(currentDefinitionId, allowNone);

            if (allowNone && CharacterActionVfxDefinitionChoice.None.Matches(query))
                result.Add(CharacterActionVfxDefinitionChoice.None);

            for (int i = 0; i < _choices.Length; i++)
            {
                CharacterActionVfxDefinitionChoice choice = _choices[i];
                if (choice != null && choice.Matches(query))
                    result.Add(choice);
            }

            if (!ContainsDefinitionId(result, current.DefinitionId))
                result.Insert(0, current);
            if (result.Count == 0)
                result.Add(current);
            return result;
        }

        private static bool ContainsDefinitionId(
            IReadOnlyList<CharacterActionVfxDefinitionChoice> choices,
            int definitionId)
        {
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                if (choices[i] != null && choices[i].DefinitionId == definitionId)
                    return true;
            }

            return false;
        }

        private static CharacterActionVfxDefinitionChoice[] TryReadDefinitionsFile(
            string path,
            string arrayName,
            List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (diagnostics != null && !string.IsNullOrWhiteSpace(path))
                    diagnostics.Add("未找到 VFX 定义文件: " + path);
                return Array.Empty<CharacterActionVfxDefinitionChoice>();
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                JArray definitions = root[arrayName] as JArray;
                var choices = new List<CharacterActionVfxDefinitionChoice>();
                for (int i = 0; definitions != null && i < definitions.Count; i++)
                {
                    if (definitions[i] is JObject fields)
                    {
                        CharacterActionVfxDefinitionChoice choice = CharacterActionVfxDefinitionChoice.FromJson(fields);
                        if (choice.DefinitionId > 0)
                            choices.Add(choice);
                    }
                }

                choices.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));
                return choices.ToArray();
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                if (diagnostics != null)
                    diagnostics.Add("读取 VFX 定义失败: " + path + " - " + ex.Message);
                return Array.Empty<CharacterActionVfxDefinitionChoice>();
            }
        }
    }

    internal sealed class CharacterActionVfxDefinitionChoice
    {
        public static readonly CharacterActionVfxDefinitionChoice None =
            new CharacterActionVfxDefinitionChoice(0, "不播放", string.Empty, string.Empty, 0, string.Empty, false);

        private CharacterActionVfxDefinitionChoice(
            int definitionId,
            string displayName,
            string stableId,
            string resourceKey,
            int defaultLifetimeFrames,
            string definitionHash,
            bool isUnknown)
        {
            DefinitionId = definitionId < 0 ? 0 : definitionId;
            DisplayName = displayName ?? string.Empty;
            StableId = stableId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
            DefaultLifetimeFrames = defaultLifetimeFrames < 0 ? 0 : defaultLifetimeFrames;
            DefinitionHash = definitionHash ?? string.Empty;
            IsUnknown = isUnknown;
        }

        public int DefinitionId { get; }
        public string DisplayName { get; }
        public string StableId { get; }
        public string ResourceKey { get; }
        public int DefaultLifetimeFrames { get; }
        public string DefinitionHash { get; }
        public bool IsUnknown { get; }

        public string Label
        {
            get
            {
                if (DefinitionId == 0 && string.IsNullOrWhiteSpace(StableId))
                    return "不播放";
                if (IsUnknown)
                    return "未知 VFX #" + DefinitionId;
                return DisplayName + " (" + DefinitionId + ") / " + StableId;
            }
        }

        public string Detail
        {
            get
            {
                if (DefinitionId == 0 && string.IsNullOrWhiteSpace(StableId))
                    return "不播放引信特效。";
                if (IsUnknown)
                    return "当前 ID 不在 VFX 定义表中，保存时会保留该整数值。";

                return "stable id: " + StableId
                    + " | 资源: " + ResourceKey
                    + " | 默认生命周期: " + DefaultLifetimeFrames + "f"
                    + " | hash: " + DefinitionHash;
            }
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            query = query.Trim();
            return Contains(DisplayName, query)
                || Contains(StableId, query)
                || Contains(ResourceKey, query)
                || Contains(DefinitionHash, query)
                || DefinitionId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query);
        }

        public static CharacterActionVfxDefinitionChoice CreateUnknown(int definitionId)
        {
            return new CharacterActionVfxDefinitionChoice(
                Math.Max(0, definitionId),
                "未知 VFX",
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                true);
        }

        public static CharacterActionVfxDefinitionChoice FromJson(JObject fields)
        {
            int definitionId = ReadInt(fields, "definitionId");
            string stableId = ReadString(fields, "stableId");
            return new CharacterActionVfxDefinitionChoice(
                definitionId,
                ResolveDisplayName(stableId),
                stableId,
                ReadResourceKey(fields["prefabResource"] as JObject),
                ReadInt(fields, "defaultLifetimeFrames"),
                ReadString(fields, "definitionHash"),
                false);
        }

        private static string ResolveDisplayName(string stableId)
        {
            if (string.Equals(stableId, "vfx.magic.arc_mine.flight", StringComparison.Ordinal))
                return "投掷地雷·飞行";
            if (string.Equals(stableId, "vfx.magic.arc_mine.deploy", StringComparison.Ordinal))
                return "投掷地雷·落地";
            if (string.Equals(stableId, "vfx.magic.arc_mine.fuse", StringComparison.Ordinal))
                return "投掷地雷·引信";
            if (string.Equals(stableId, "vfx.magic.arc_mine.trigger", StringComparison.Ordinal))
                return "投掷地雷·爆炸";
            if (string.Equals(stableId, "vfx.magic.rune_turret.deploy", StringComparison.Ordinal))
                return "符文炮台·部署";
            if (string.Equals(stableId, "vfx.magic.rune_turret.idle", StringComparison.Ordinal))
                return "符文炮台·待机";
            if (string.Equals(stableId, "vfx.magic.rune_turret.fire", StringComparison.Ordinal))
                return "符文炮台·开火";
            if (string.Equals(stableId, "vfx.magic.rune_turret.flight", StringComparison.Ordinal))
                return "符文炮台·飞行";
            if (string.Equals(stableId, "vfx.magic.rune_turret.impact", StringComparison.Ordinal))
                return "符文炮台·命中";
            if (string.Equals(stableId, "vfx.magic.rune_turret.expire", StringComparison.Ordinal))
                return "符文炮台·消失";

            return string.IsNullOrWhiteSpace(stableId) ? "未命名 VFX" : stableId;
        }

        private static string ReadResourceKey(JObject fields)
        {
            if (fields == null)
                return string.Empty;

            string id = ReadString(fields, "id");
            string typeId = ReadString(fields, "typeId");
            string variant = ReadString(fields, "variant");
            string packageId = ReadString(fields, "packageId");
            string resource = string.IsNullOrWhiteSpace(packageId) ? id : packageId + ":" + id;
            if (!string.IsNullOrWhiteSpace(variant))
                resource += "@" + variant;
            if (!string.IsNullOrWhiteSpace(typeId))
                resource += " [" + typeId + "]";
            return resource;
        }

        private static string ReadString(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return string.Empty;

            JToken token = fields[name];
            return token == null ? string.Empty : token.Value<string>() ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return 0;

            JToken token = fields[name];
            return token == null ? 0 : token.Value<int>();
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal static class CharacterActionVfxDefinitionPickerUi
    {
        public static void AddField(
            VisualElement parent,
            string label,
            int value,
            bool allowNone,
            CharacterActionVfxDefinitionCatalog catalog,
            Action<int> setter,
            string tooltip)
        {
            catalog ??= CharacterActionVfxDefinitionCatalog.Load(
                CharacterActionVfxDefinitionCatalog.DefaultGeneratedDefinitionsPath,
                CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath);

            var container = new VisualElement();
            container.AddToClassList("ca-vfx-definition-field");

            var labelElement = new Label(label);
            labelElement.tooltip = tooltip ?? string.Empty;
            labelElement.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            container.Add(labelElement);

            if (!catalog.HasDefinitions)
            {
                var fallback = new IntegerField("Definition ID")
                {
                    value = value,
                    isDelayed = true,
                    tooltip = tooltip ?? string.Empty,
                };
                fallback.RegisterValueChangedCallback(evt => setter?.Invoke(evt.newValue));
                container.Add(fallback);
                container.Add(new HelpBox(
                    "没有找到 VFX 定义 catalog，暂时只能编辑整数 ID。",
                    HelpBoxMessageType.Warning));
                parent.Add(container);
                return;
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;

            var search = new ToolbarSearchField();
            search.style.minWidth = 160f;
            search.style.maxWidth = 260f;
            search.tooltip = "按中文名、stable id 或数字 ID 过滤。";
            row.Add(search);

            List<CharacterActionVfxDefinitionChoice> choices = catalog.BuildChoices(value, allowNone, string.Empty);
            List<int> choiceIds = BuildChoiceIds(choices);
            int selectedIndex = FindChoiceIndex(choices, value);
            var popup = new PopupField<int>(string.Empty, choiceIds, Math.Max(0, selectedIndex));
            popup.formatSelectedValueCallback = FormatChoiceLabel;
            popup.formatListItemCallback = FormatChoiceLabel;
            popup.SetValueWithoutNotify(choiceIds[Math.Max(0, selectedIndex)]);
            popup.style.flexGrow = 1f;
            popup.style.minWidth = 280f;
            row.Add(popup);

            popup.RegisterValueChangedCallback(evt =>
            {
                setter?.Invoke(evt.newValue);
            });

            search.RegisterValueChangedCallback(evt =>
            {
                choices = catalog.BuildChoices(value, allowNone, evt.newValue);
                choiceIds = BuildChoiceIds(choices);
                popup.choices = choiceIds;
                int nextIndex = FindChoiceIndex(choices, value);
                popup.SetValueWithoutNotify(choiceIds[Math.Max(0, nextIndex)]);
            });

            container.Add(row);

            CharacterActionVfxDefinitionChoice current = catalog.GetCurrentChoice(value, allowNone);
            var detail = new Label(current.Detail);
            detail.AddToClassList("ca-vfx-definition-detail");
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.fontSize = 11f;
            detail.style.opacity = 0.78f;
            container.Add(detail);

            if (current.IsUnknown)
            {
                container.Add(new HelpBox(
                    "当前 VFX ID 不在已加载定义中，保存时会保留原整数值。",
                    HelpBoxMessageType.Warning));
            }

            parent.Add(container);

            string FormatChoiceLabel(int definitionId)
            {
                for (int i = 0; choices != null && i < choices.Count; i++)
                {
                    CharacterActionVfxDefinitionChoice choice = choices[i];
                    if (choice != null && choice.DefinitionId == definitionId)
                        return choice.Label;
                }

                return catalog.GetCurrentChoice(definitionId, allowNone).Label;
            }
        }

        private static List<string> BuildLabels(IReadOnlyList<CharacterActionVfxDefinitionChoice> choices)
        {
            var labels = new List<string>(choices?.Count ?? 0);
            for (int i = 0; choices != null && i < choices.Count; i++)
                labels.Add(choices[i]?.Label ?? string.Empty);
            if (labels.Count == 0)
                labels.Add("无可选 VFX");
            return labels;
        }

        private static List<int> BuildChoiceIds(IReadOnlyList<CharacterActionVfxDefinitionChoice> choices)
        {
            var choiceIds = new List<int>(choices?.Count ?? 0);
            for (int i = 0; choices != null && i < choices.Count; i++)
                choiceIds.Add(choices[i]?.DefinitionId ?? 0);
            if (choiceIds.Count == 0)
                choiceIds.Add(0);
            return choiceIds;
        }

        private static int FindChoiceIndex(
            IReadOnlyList<CharacterActionVfxDefinitionChoice> choices,
            int definitionId)
        {
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                if (choices[i] != null && choices[i].DefinitionId == definitionId)
                    return i;
            }

            return 0;
        }
    }
}
