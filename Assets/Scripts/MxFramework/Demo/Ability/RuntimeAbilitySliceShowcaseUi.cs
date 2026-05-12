using System.Collections.Generic;
using MxFramework.Buffs;
using MxFramework.Gameplay;
using MxFramework.UI.Toolkit;
using UnityEngine;

namespace MxFramework.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeAbilitySliceRunner))]
    [AddComponentMenu("MxFramework/Demo/Runtime Ability Slice Showcase UI")]
    public sealed class RuntimeAbilitySliceShowcaseUi : MonoBehaviour, IMxRuntimeHudManualControlSink
    {
        private RuntimeAbilitySliceRunner _runner;
        private MxRuntimeHudController _hud;
        private readonly MxRuntimeHudViewModel _viewModel = new MxRuntimeHudViewModel();

        private void Awake()
        {
            _runner = GetComponent<RuntimeAbilitySliceRunner>();
            _hud = GetComponent<MxRuntimeHudController>() ?? gameObject.AddComponent<MxRuntimeHudController>();
            _hud.RegisterManualControlSink(this);
        }

        private void OnDestroy()
        {
            if (_hud != null)
                _hud.UnregisterManualControlSink(this);
        }

        private void LateUpdate()
        {
            if (_runner == null || _hud == null || !_runner.IsInitialized)
                return;

            BuildViewModel();
            _hud.Refresh(_viewModel);
            _hud.SetManualControlState(_runner.AutoSequenceEnabled, _runner.LiveTickEnabled, _runner.UseConfigDriven);
        }

        public void OnRuntimeHudManualCommand(MxRuntimeHudManualCommand command)
        {
            if (_runner == null || !_runner.IsInitialized)
                return;

            switch (command)
            {
                case MxRuntimeHudManualCommand.Strike:
                    PauseAutoForManualCommand();
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.Ignite:
                    PauseAutoForManualCommand();
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.ApplyBuff:
                    PauseAutoForManualCommand();
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.Tick:
                    PauseAutoForManualCommand();
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.ApplyModifier:
                    PauseAutoForManualCommand();
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.Reset:
                    _runner.EnqueueManualCommand(command);
                    break;
                case MxRuntimeHudManualCommand.ToggleAuto:
                    _runner.SetAutoSequenceEnabled(!_runner.AutoSequenceEnabled);
                    break;
                case MxRuntimeHudManualCommand.ToggleLiveTick:
                    _runner.SetLiveTickEnabled(!_runner.LiveTickEnabled);
                    break;
                case MxRuntimeHudManualCommand.ToggleMode:
                    PauseAutoForManualCommand();
                    _runner.SetConfigDrivenMode(!_runner.UseConfigDriven);
                    break;
                case MxRuntimeHudManualCommand.LoadPatch:
                    PauseAutoForManualCommand();
                    _runner.LoadPatchConfig();
                    break;
                case MxRuntimeHudManualCommand.LoadModPackage:
                    PauseAutoForManualCommand();
                    _runner.LoadModPackageConfig();
                    break;
                case MxRuntimeHudManualCommand.RebuildAbility:
                    PauseAutoForManualCommand();
                    _runner.RebuildConfiguredAbilities();
                    break;
                case MxRuntimeHudManualCommand.CompareConfig:
                    PauseAutoForManualCommand();
                    _runner.CompareOldAndNewConfig();
                    break;
            }
        }

        private void PauseAutoForManualCommand()
        {
            if (_runner.AutoSequenceEnabled)
                _runner.SetAutoSequenceEnabled(false);
        }

        private void BuildViewModel()
        {
            GameplayDiagnosticSnapshot snapshot = _runner.LastSnapshot;
            _viewModel.Title = "MxFramework 运行预览";
            _viewModel.ModeName = _runner.UseConfigDriven ? "配置驱动技能" : "代码内置技能";
            _viewModel.AbilitySource = _runner.UseConfigDriven
                ? "BasicAbilityConfig -> ConfigAbilityFactory"
                : "Hardcoded SimpleAbility";
            _viewModel.ConfigSummary = _runner.UseConfigDriven
                ? ToProducerConfigSummary(_runner.ConfigSummary)
                : "未启用配置表路径";
            _viewModel.SnapshotSummary = snapshot != null
                ? $"实体 {snapshot.Entities.Count}，技能事件 {snapshot.AbilityEvents.Count}，属性事件 {snapshot.AttributeEvents.Count}"
                : "等待首帧诊断快照";
            _viewModel.ConfigModeStatus = _runner.ConfigModeStatus;
            _viewModel.RuntimeConfigChangeSummary = _runner.RuntimeConfigChangeSummaryText;
            _viewModel.AbilityRebuildSummary = _runner.AbilityRebuildSummary;
            _viewModel.ConfigComparisonSummary = _runner.ConfigComparisonSummary;
            _viewModel.EventLog = _runner.EventLog;
            RuntimeAbilitySliceDiagnosticViewModelBuilder.Fill(
                _viewModel.Diagnostic,
                snapshot,
                _runner.RuntimeConfigChangeSummary);

            FillEntity(_viewModel.Player, "Player", _runner.Player, _runner.PlayerMaxHp);
            FillEntity(_viewModel.Enemy, "Enemy", _runner.Enemy, _runner.EnemyMaxHp);
            FillMiniGameFeedback(
                _viewModel.MiniGameFeedback,
                _runner.Player,
                _runner.PlayerMaxHp,
                _runner.Enemy,
                _runner.EnemyMaxHp,
                _runner.EventLog,
                _runner.UseConfigDriven);
        }

        private static string ToProducerConfigSummary(string rawSummary)
        {
            if (string.IsNullOrEmpty(rawSummary))
                return "配置解析器未返回摘要";

            bool failed = rawSummary.Contains("failed=0") == false;
            return failed
                ? "配置已加载，但存在解析失败，查看 Console 详情"
                : "配置已加载，技能/Buff/Modifier 解析正常";
        }

        private static void FillEntity(MxRuntimeEntityViewModel view, string name, RuntimeEntity entity, int maxHp)
        {
            if (view == null || entity == null)
                return;

            view.DisplayName = name;
            view.EntityId = entity.EntityId;
            view.TeamId = entity.TeamId;
            view.Hp = entity.Store.GetAttribute(AbilityConst.AttrHp);
            view.MaxHp = maxHp;
            view.Attack = entity.Store.GetAttribute(AbilityConst.AttrAttack);
            view.Defense = entity.Store.GetAttribute(AbilityConst.AttrDefense);
            view.IsAlive = entity.IsAlive;
            view.BuffSummary = RuntimeAbilitySliceRunner.FormatBuffsForUi(entity);
        }

        public static void FillMiniGameFeedback(
            MxRuntimeMiniGameFeedbackViewModel view,
            RuntimeEntity player,
            int playerMaxHp,
            RuntimeEntity enemy,
            int enemyMaxHp,
            IReadOnlyList<string> eventLog,
            bool configDriven)
        {
            if (view == null)
                return;

            view.PlayerStatusText = BuildStatusBadgeText("Player", player, playerMaxHp);
            view.PlayerStatusTone = BuildStatusTone(player, playerMaxHp);
            view.EnemyStatusText = BuildStatusBadgeText("Enemy", enemy, enemyMaxHp);
            view.EnemyStatusTone = BuildStatusTone(enemy, enemyMaxHp);
            view.PlayerBuffText = BuildBuffFeedbackText("Player", player);
            view.EnemyBuffText = BuildBuffFeedbackText("Enemy", enemy);
            view.RecentActionText = BuildRecentActionText(eventLog);

            bool enemyAlive = enemy != null && enemy.IsAlive;
            int burningLayers = enemy != null ? enemy.Buffs.GetBuffLayer(AbilityConst.BuffBurning) : 0;
            bool burningActive = burningLayers > 0;
            bool burningCanStack = enemyAlive && burningLayers < 3;

            view.StrikeButtonHot = enemyAlive;
            view.IgniteButtonHot = enemyAlive;
            view.BuffButtonHot = burningCanStack;
            view.StrikeButtonFeedbackText = enemyAlive
                ? "Strike ready: damage current enemy"
                : "Strike blocked: no living enemy target";
            view.IgniteButtonFeedbackText = enemyAlive
                ? burningActive
                    ? "Ignite ready: refresh Burning"
                    : "Ignite ready: apply Burning"
                : "Ignite blocked: no living enemy target";
            view.BuffButtonFeedbackText = enemyAlive
                ? burningCanStack
                    ? "Burning ready: add or refresh layer"
                    : "Burning at max layers"
                : "Burning blocked: no living enemy target";
            view.SkillFeedbackText = "Skill feedback: "
                + view.StrikeButtonFeedbackText
                + " | "
                + view.IgniteButtonFeedbackText
                + " | "
                + view.BuffButtonFeedbackText
                + (configDriven ? " | Config mode" : " | Hardcoded mode");
        }

        private static string BuildStatusBadgeText(string label, RuntimeEntity entity, int maxHp)
        {
            if (entity == null)
                return label + ": waiting";

            int hp = entity.Store.GetAttribute(AbilityConst.AttrHp);
            if (!entity.IsAlive || hp <= 0)
                return label + ": Down";

            if (entity.Buffs.HasBuff(AbilityConst.BuffBurning))
                return label + ": Burning";

            float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (ratio <= 0.25f)
                return label + ": Critical";

            if (ratio <= 0.5f)
                return label + ": Wounded";

            return label + ": Stable";
        }

        private static string BuildStatusTone(RuntimeEntity entity, int maxHp)
        {
            if (entity == null)
                return "neutral";

            int hp = entity.Store.GetAttribute(AbilityConst.AttrHp);
            if (!entity.IsAlive || hp <= 0)
                return "danger";

            if (entity.Buffs.HasBuff(AbilityConst.BuffBurning))
                return "warning";

            float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (ratio <= 0.25f)
                return "danger";

            return ratio <= 0.5f ? "warning" : "positive";
        }

        private static string BuildBuffFeedbackText(string label, RuntimeEntity entity)
        {
            if (entity == null)
                return label + " Buff: waiting";

            BuffSnapshot[] snapshots = entity.Buffs.CreateSnapshot();
            if (snapshots == null || snapshots.Length == 0)
                return label + " Buff: none";

            var parts = new System.Text.StringBuilder(label).Append(" Buff: ");
            for (int i = 0; i < snapshots.Length; i++)
            {
                if (i > 0)
                    parts.Append(", ");

                ref BuffSnapshot buff = ref snapshots[i];
                parts.Append("#")
                    .Append(buff.Id)
                    .Append(" ")
                    .Append(buff.CurrentLayers)
                    .Append("L ");

                if (buff.IsPermanent)
                    parts.Append("permanent");
                else
                    parts.Append(buff.RemainingTime.ToString("0.0")).Append("s");
            }

            return parts.ToString();
        }

        private static string BuildRecentActionText(IReadOnlyList<string> eventLog)
        {
            if (eventLog == null || eventLog.Count == 0)
                return "最近动作: waiting";

            return "最近动作: " + eventLog[eventLog.Count - 1];
        }
    }
}
