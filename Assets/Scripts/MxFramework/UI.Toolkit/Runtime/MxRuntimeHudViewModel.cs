using System;
using System.Collections.Generic;

namespace MxFramework.UI.Toolkit
{
    public sealed class MxRuntimeHudViewModel
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
        public MxRuntimeMiniGameFeedbackViewModel MiniGameFeedback { get; } = new MxRuntimeMiniGameFeedbackViewModel();
        public MxRuntimeDiagnosticViewModel Diagnostic { get; } = new MxRuntimeDiagnosticViewModel();
        public MxRuntimeEntityViewModel Player { get; } = new MxRuntimeEntityViewModel();
        public MxRuntimeEntityViewModel Enemy { get; } = new MxRuntimeEntityViewModel();
        public IReadOnlyList<string> EventLog { get; set; }
    }

    public sealed class MxRuntimeEntityViewModel
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

    public sealed class MxRuntimeMiniGameFeedbackViewModel
    {
        public string PlayerStatusText { get; set; }
        public string PlayerStatusTone { get; set; }
        public string EnemyStatusText { get; set; }
        public string EnemyStatusTone { get; set; }
        public string PlayerBuffText { get; set; }
        public string EnemyBuffText { get; set; }
        public string SkillFeedbackText { get; set; }
        public string RecentActionText { get; set; }
        public string StrikeButtonFeedbackText { get; set; }
        public string IgniteButtonFeedbackText { get; set; }
        public string BuffButtonFeedbackText { get; set; }
        public bool StrikeButtonHot { get; set; }
        public bool IgniteButtonHot { get; set; }
        public bool BuffButtonHot { get; set; }
    }

    public sealed class MxRuntimeDiagnosticViewModel
    {
        public string HeaderText { get; set; }
        public string LastCastText { get; set; }
        public string ConfigSourceText { get; set; }
        public string ErrorSummaryText { get; set; }
        public string EntityEmptyText { get; set; } = "No entities in snapshot";
        public string AbilityEventsEmptyText { get; set; } = "No ability events";
        public string AttributeEventsEmptyText { get; set; } = "No attribute changed events";
        public string ConfigSourceEmptyText { get; set; } = "No runtime config summary";
        public string ErrorsEmptyText { get; set; } = "No runtime errors";
        public IReadOnlyList<string> EntitySummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AbilityEventSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AttributeEventSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ConfigSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ErrorSummaryLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> EntityTechnicalLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AbilityEventTechnicalLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AttributeEventTechnicalLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ConfigTechnicalLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ErrorTechnicalLines { get; set; } = Array.Empty<string>();
    }
}
