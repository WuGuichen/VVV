using System;
using System.Collections.Generic;
using MxFramework.Buffs;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;

namespace MxFramework.Demo
{
    public static class RuntimeAbilitySliceHudCommandIds
    {
        public const string Strike = "runtimeHud.strike";
        public const string Reset = "runtimeHud.reset";
    }

    public sealed class RuntimeAbilitySliceHudViewModel
    {
        public string Title { get; set; }
        public string ModeName { get; set; }
        public string AbilitySource { get; set; }
        public string ConfigSummary { get; set; }
        public string SnapshotSummary { get; set; }
        public string ConfigModeStatus { get; set; }
        public string RuntimeConfigChangeSummary { get; set; }
        public string AbilityRebuildSummary { get; set; }
        public string ConfigComparisonSummary { get; set; }
        public RuntimeAbilitySliceHudEntityViewModel Player { get; } = new RuntimeAbilitySliceHudEntityViewModel();
        public RuntimeAbilitySliceHudEntityViewModel Enemy { get; } = new RuntimeAbilitySliceHudEntityViewModel();
        public RuntimeAbilitySliceHudFeedbackViewModel Feedback { get; } = new RuntimeAbilitySliceHudFeedbackViewModel();
        public RuntimeAbilitySliceHudDiagnosticViewModel Diagnostic { get; } = new RuntimeAbilitySliceHudDiagnosticViewModel();
        public IReadOnlyList<string> EventLog { get; set; } = Array.Empty<string>();
        public IReadOnlyList<RuntimeAbilitySliceHudCommandDescriptor> Commands { get; set; } = Array.Empty<RuntimeAbilitySliceHudCommandDescriptor>();
    }

    public sealed class RuntimeAbilitySliceHudEntityViewModel
    {
        public string DisplayName { get; set; }
        public int EntityId { get; set; }
        public int TeamId { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public bool IsAlive { get; set; }
        public string BuffSummary { get; set; }
    }

    public sealed class RuntimeAbilitySliceHudFeedbackViewModel
    {
        public string PlayerStatusText { get; set; }
        public string EnemyStatusText { get; set; }
        public string PlayerStatusTone { get; set; }
        public string EnemyStatusTone { get; set; }
        public string PlayerBuffText { get; set; }
        public string EnemyBuffText { get; set; }
        public string SkillFeedbackText { get; set; }
        public string RecentActionText { get; set; }
        public string StrikeButtonFeedbackText { get; set; }
        public bool StrikeButtonHot { get; set; }
    }

    public sealed class RuntimeAbilitySliceHudDiagnosticViewModel
    {
        public string HeaderText { get; set; }
        public string LastCastText { get; set; }
        public string ConfigSourceText { get; set; }
        public string ErrorSummaryText { get; set; }
        public IReadOnlyList<string> EntitySummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AbilityEventSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AttributeEventSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ConfigSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ErrorSummaryLines { get; set; } = Array.Empty<string>();
    }

    public readonly struct RuntimeAbilitySliceHudCommandDescriptor
    {
        public RuntimeAbilitySliceHudCommandDescriptor(string commandId, string label, bool enabled)
        {
            CommandId = commandId ?? string.Empty;
            Label = label ?? string.Empty;
            Enabled = enabled;
        }

        public string CommandId { get; }
        public string Label { get; }
        public bool Enabled { get; }
    }

    public sealed class RuntimeAbilitySliceHudBuilderInput
    {
        public bool IsInitialized { get; set; }
        public bool UseConfigDriven { get; set; }
        public string ConfigSummary { get; set; }
        public string ConfigModeStatus { get; set; }
        public string RuntimeConfigChangeSummaryText { get; set; }
        public RuntimeConfigChangeSummary RuntimeConfigChangeSummary { get; set; }
        public string AbilityRebuildSummary { get; set; }
        public string ConfigComparisonSummary { get; set; }
        public GameplayDiagnosticSnapshot Snapshot { get; set; }
        public RuntimeEntity Player { get; set; }
        public RuntimeEntity Enemy { get; set; }
        public int PlayerMaxHp { get; set; }
        public int EnemyMaxHp { get; set; }
        public IReadOnlyList<string> EventLog { get; set; }
    }

    public static class RuntimeAbilitySliceHudViewModelBuilder
    {
        public static RuntimeAbilitySliceHudViewModel Build(RuntimeAbilitySliceRunner runner)
        {
            if (runner == null)
                return Build(new RuntimeAbilitySliceHudBuilderInput());

            return Build(new RuntimeAbilitySliceHudBuilderInput
            {
                IsInitialized = runner.IsInitialized,
                UseConfigDriven = runner.UseConfigDriven,
                ConfigSummary = runner.ConfigSummary,
                ConfigModeStatus = runner.ConfigModeStatus,
                RuntimeConfigChangeSummaryText = runner.RuntimeConfigChangeSummaryText,
                RuntimeConfigChangeSummary = runner.RuntimeConfigChangeSummary,
                AbilityRebuildSummary = runner.AbilityRebuildSummary,
                ConfigComparisonSummary = runner.ConfigComparisonSummary,
                Snapshot = runner.LastSnapshot,
                Player = runner.Player,
                Enemy = runner.Enemy,
                PlayerMaxHp = runner.PlayerMaxHp,
                EnemyMaxHp = runner.EnemyMaxHp,
                EventLog = runner.EventLog
            });
        }

        public static RuntimeAbilitySliceHudViewModel Build(RuntimeAbilitySliceHudBuilderInput input)
        {
            input = input ?? new RuntimeAbilitySliceHudBuilderInput();

            var view = new RuntimeAbilitySliceHudViewModel
            {
                Title = "MxFramework RuntimeAbilitySlice",
                ModeName = input.UseConfigDriven ? "Config Driven Ability" : "Hardcoded Ability",
                AbilitySource = input.UseConfigDriven
                    ? "BasicAbilityConfig -> ConfigAbilityFactory"
                    : "Hardcoded SimpleAbility",
                ConfigSummary = input.UseConfigDriven ? NonEmpty(input.ConfigSummary, "No config summary") : "Hardcoded mode",
                SnapshotSummary = BuildSnapshotSummary(input.Snapshot),
                ConfigModeStatus = NonEmpty(input.ConfigModeStatus, "Runtime mode waiting"),
                RuntimeConfigChangeSummary = NonEmpty(input.RuntimeConfigChangeSummaryText, "No runtime config change summary"),
                AbilityRebuildSummary = NonEmpty(input.AbilityRebuildSummary, "No ability rebuild requested"),
                ConfigComparisonSummary = NonEmpty(input.ConfigComparisonSummary, "No config comparison requested"),
                EventLog = input.EventLog ?? Array.Empty<string>(),
                Commands = BuildCommands(input)
            };

            FillEntity(view.Player, "Player", input.Player, input.PlayerMaxHp);
            FillEntity(view.Enemy, "Enemy", input.Enemy, input.EnemyMaxHp);
            FillFeedback(view.Feedback, input.Player, input.PlayerMaxHp, input.Enemy, input.EnemyMaxHp, view.EventLog);
            FillDiagnostic(view.Diagnostic, input.Snapshot, input.RuntimeConfigChangeSummary);
            return view;
        }

        private static IReadOnlyList<RuntimeAbilitySliceHudCommandDescriptor> BuildCommands(RuntimeAbilitySliceHudBuilderInput input)
        {
            bool enemyAlive = input.Enemy != null && input.Enemy.IsAlive;
            return new[]
            {
                new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Strike, "Strike", input.IsInitialized && enemyAlive),
                new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Reset, "Reset", input.IsInitialized)
            };
        }

        private static string BuildSnapshotSummary(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return "Waiting for diagnostic snapshot";

            return "Entities "
                + snapshot.Entities.Count
                + ", ability events "
                + snapshot.AbilityEvents.Count
                + ", attribute events "
                + snapshot.AttributeEvents.Count;
        }

        private static void FillEntity(RuntimeAbilitySliceHudEntityViewModel view, string label, RuntimeEntity entity, int maxHp)
        {
            view.DisplayName = label;
            if (entity == null)
            {
                view.BuffSummary = "waiting";
                return;
            }

            view.EntityId = entity.EntityId;
            view.TeamId = entity.TeamId;
            view.Hp = entity.Store.GetAttribute(AbilityConst.AttrHp);
            view.MaxHp = maxHp;
            view.Attack = entity.Store.GetAttribute(AbilityConst.AttrAttack);
            view.Defense = entity.Store.GetAttribute(AbilityConst.AttrDefense);
            view.IsAlive = entity.IsAlive;
            view.BuffSummary = RuntimeAbilitySliceRunner.FormatBuffsForUi(entity);
        }

        private static void FillFeedback(
            RuntimeAbilitySliceHudFeedbackViewModel view,
            RuntimeEntity player,
            int playerMaxHp,
            RuntimeEntity enemy,
            int enemyMaxHp,
            IReadOnlyList<string> eventLog)
        {
            view.PlayerStatusText = BuildStatusBadgeText("Player", player, playerMaxHp);
            view.PlayerStatusTone = BuildStatusTone(player, playerMaxHp);
            view.EnemyStatusText = BuildStatusBadgeText("Enemy", enemy, enemyMaxHp);
            view.EnemyStatusTone = BuildStatusTone(enemy, enemyMaxHp);
            view.PlayerBuffText = BuildBuffFeedbackText("Player", player);
            view.EnemyBuffText = BuildBuffFeedbackText("Enemy", enemy);
            view.RecentActionText = eventLog != null && eventLog.Count > 0
                ? "Recent action: " + eventLog[eventLog.Count - 1]
                : "Recent action: waiting";

            bool enemyAlive = enemy != null && enemy.IsAlive;
            view.StrikeButtonHot = enemyAlive;
            view.StrikeButtonFeedbackText = enemyAlive
                ? "Strike ready: damage current enemy"
                : "Strike blocked: no living enemy target";
            view.SkillFeedbackText = "Skill feedback: " + view.StrikeButtonFeedbackText;
        }

        private static void FillDiagnostic(
            RuntimeAbilitySliceHudDiagnosticViewModel view,
            GameplayDiagnosticSnapshot snapshot,
            RuntimeConfigChangeSummary configSummary)
        {
            view.HeaderText = snapshot == null ? "Snapshot: waiting for runtime data" : BuildSnapshotSummary(snapshot);
            view.LastCastText = snapshot == null
                ? "Last cast: waiting"
                : snapshot.LastCastSuccess
                    ? "Last cast: success"
                    : string.IsNullOrEmpty(snapshot.LastFailureReason)
                        ? "Last cast: not recorded"
                        : "Last cast failure: " + snapshot.LastFailureReason;
            view.ConfigSourceText = configSummary == null
                ? "Config Source: no runtime config summary"
                : "Config Source: " + NonEmpty(configSummary.SourceName, "unknown");
            view.ErrorSummaryText = BuildErrorSummaryText(snapshot, configSummary);
            view.EntitySummaryLines = BuildEntitySummaryLines(snapshot);
            view.AbilityEventSummaryLines = BuildAbilityEventLines(snapshot);
            view.AttributeEventSummaryLines = BuildAttributeEventLines(snapshot);
            view.ConfigSummaryLines = BuildConfigLines(configSummary);
            view.ErrorSummaryLines = view.ErrorSummaryText == "No runtime errors"
                ? Array.Empty<string>()
                : new[] { view.ErrorSummaryText };
        }

        private static string BuildErrorSummaryText(GameplayDiagnosticSnapshot snapshot, RuntimeConfigChangeSummary configSummary)
        {
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.LastFailureReason))
                return "Last cast failure: " + snapshot.LastFailureReason;

            if (configSummary != null && configSummary.Errors.Count > 0)
                return "Config error: " + configSummary.Errors[configSummary.Errors.Count - 1];

            return "No runtime errors";
        }

        private static IReadOnlyList<string> BuildEntitySummaryLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Entities.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.Entities.Count);
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                GameplayEntitySnapshot entity = snapshot.Entities[i];
                lines.Add("Entity#" + entity.EntityId + " team=" + entity.TeamId + " alive=" + entity.IsAlive);
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildAbilityEventLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AbilityEvents.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.AbilityEvents.Count);
            for (int i = 0; i < snapshot.AbilityEvents.Count; i++)
            {
                GameplayAbilityEventSnapshot item = snapshot.AbilityEvents[i];
                lines.Add(item.EventType + " ability=" + item.AbilityId + " caster=" + item.CasterEntityId);
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildAttributeEventLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AttributeEvents.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.AttributeEvents.Count);
            for (int i = 0; i < snapshot.AttributeEvents.Count; i++)
            {
                GameplayAttributeEventSnapshot item = snapshot.AttributeEvents[i];
                lines.Add("attr=" + item.AttributeId + " old=" + item.OldValue + " new=" + item.NewValue + " delta=" + item.Delta);
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildConfigLines(RuntimeConfigChangeSummary configSummary)
        {
            if (configSummary == null)
                return Array.Empty<string>();

            return new[]
            {
                "source=" + NonEmpty(configSummary.SourceName, "unknown"),
                "changed abilities=" + configSummary.ChangedAbilityCount
                    + " buffs=" + configSummary.ChangedBuffCount
                    + " modifiers=" + configSummary.ChangedModifierCount
            };
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

            return ratio <= 0.5f ? label + ": Wounded" : label + ": Stable";
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

            return label + " Buff: " + snapshots.Length + " active";
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
