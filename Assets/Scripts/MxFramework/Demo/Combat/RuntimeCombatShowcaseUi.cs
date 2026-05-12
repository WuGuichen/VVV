using MxFramework.Combat.Diagnostics;
using MxFramework.UI.Toolkit;
using UnityEngine;

namespace MxFramework.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeCombatShowcaseRunner))]
    [AddComponentMenu("MxFramework/Demo/Runtime Combat Showcase UI")]
    public sealed class RuntimeCombatShowcaseUi : MonoBehaviour, IMxRuntimeHudManualControlSink
    {
        private RuntimeCombatShowcaseRunner _runner;
        private MxRuntimeHudController _hud;
        private readonly MxRuntimeHudViewModel _viewModel = new MxRuntimeHudViewModel();

        private void Awake()
        {
            _runner = GetComponent<RuntimeCombatShowcaseRunner>();
            _hud = GetComponent<MxRuntimeHudController>() ?? gameObject.AddComponent<MxRuntimeHudController>();
            _hud.SetLayoutPreset(MxRuntimeHudLayoutPreset.RightCompact);
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
            _hud.SetManualControlState(false, false, true);
            _hud.SetManualButtonLabels("Step", "Probe", "Attack", "Shape", "Snapshot", "Auto: Off", "Time: Off", "Mode: Combat", "Reset");
        }

        public void OnRuntimeHudManualCommand(MxRuntimeHudManualCommand command)
        {
            if (_runner == null || !_runner.IsInitialized)
                return;

            switch (command)
            {
                case MxRuntimeHudManualCommand.Strike:
                    _runner.StepFrame();
                    break;
                case MxRuntimeHudManualCommand.Ignite:
                    _runner.ProbeFromSelected();
                    break;
                case MxRuntimeHudManualCommand.ApplyBuff:
                    _runner.AttackFromSelected();
                    break;
                case MxRuntimeHudManualCommand.Tick:
                    _runner.CycleQueryShape();
                    break;
                case MxRuntimeHudManualCommand.ApplyModifier:
                    _runner.LogSnapshotSummary();
                    break;
                case MxRuntimeHudManualCommand.Reset:
                    _runner.ResetShowcase();
                    break;
            }
        }

        private void BuildViewModel()
        {
            CombatDebugSnapshot snapshot = _runner.LastSnapshot;
            _viewModel.Title = "MxFramework Combat 预览";
            _viewModel.ModeName = "Physics Game";
            _viewModel.AbilitySource = _runner.AuthoringPreviewSummary;
            _viewModel.ConfigSummary = $"{_runner.PhysicsPlaygroundSummary} | {_runner.InteractionSummary}";
            _viewModel.SnapshotSummary = snapshot != null
                ? snapshot.Summary
                : "等待 CombatDebugSnapshot";
            _viewModel.EventLog = _runner.EventLog;
            FillMiniGameFeedback(_viewModel.MiniGameFeedback, _runner);

            FillEntity(_viewModel.Player, "Player", 1, 1, _runner.PlayerHp, _runner.PlayerMaxHp, _runner.PlayerAttack, _runner.PlayerDefense);
            FillEntity(_viewModel.Enemy, "Enemy", 2, 2, _runner.EnemyHp, _runner.EnemyMaxHp, 0, _runner.EnemyDefense);
        }

        private static void FillMiniGameFeedback(MxRuntimeMiniGameFeedbackViewModel feedback, RuntimeCombatShowcaseRunner runner)
        {
            feedback.PlayerStatusText = $"Player HP {runner.PlayerHp}/{runner.PlayerMaxHp}";
            feedback.PlayerStatusTone = runner.PlayerHp <= 0 ? "danger" : "positive";
            feedback.EnemyStatusText = $"Enemy HP {runner.EnemyHp}/{runner.EnemyMaxHp}";
            feedback.EnemyStatusTone = runner.EnemyHp <= 0 ? "danger" : runner.EnemyHp < runner.EnemyMaxHp / 2 ? "warning" : "neutral";
            feedback.PlayerBuffText = runner.MotionSummary;
            feedback.EnemyBuffText = $"{runner.LastQueryDebugSummary} | {runner.MotionCollisionSummary}";
            feedback.SkillFeedbackText = "WASD/Arrows Move. Space Jump. H Hide UI. P Probe. J Attack.";
            feedback.RecentActionText = runner.InteractionSummary;
            feedback.StrikeButtonFeedbackText = "Advance deterministic combat frame.";
            feedback.IgniteButtonFeedbackText = $"Probe with {runner.QueryShapeName}.";
            feedback.BuffButtonFeedbackText = $"Attack with {runner.QueryShapeName}.";
            feedback.StrikeButtonHot = true;
            feedback.IgniteButtonHot = runner.EnemyHp > 0;
            feedback.BuffButtonHot = runner.EnemyHp > 0;
        }

        private static void FillEntity(
            MxRuntimeEntityViewModel view,
            string name,
            int entityId,
            int teamId,
            int hp,
            int maxHp,
            int attack,
            int defense)
        {
            view.DisplayName = name;
            view.EntityId = entityId;
            view.TeamId = teamId;
            view.Hp = hp;
            view.MaxHp = maxHp;
            view.Attack = attack;
            view.Defense = defense;
            view.IsAlive = hp > 0;
            view.BuffSummary = "(combat debug)";
        }
    }
}
