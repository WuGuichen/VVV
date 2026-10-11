using System.Collections.Generic;
using System.Text;

namespace MxFramework.Config
{
    public enum AuthoringWorkflowStatus
    {
        NotStarted,
        InProgress,
        Blocked,
        Ready,
        Completed
    }

    public enum AuthoringWorkflowMode
    {
        Player,
        Developer,
        AI,
        Tool
    }

    public enum AuthoringWorkflowActor
    {
        Human,
        AI,
        Tool,
        Runtime
    }

    public enum AuthoringQuickActionKind
    {
        OpenConfigSource,
        OpenField,
        OpenReferenceTarget,
        CopyAiContext,
        RunValidation,
        PreviewMergedResult,
        ExportReport,
        OpenDocument,
        ExportModPatch
    }

    public readonly struct AuthoringWorkflowTarget
    {
        public AuthoringWorkflowTarget(string source, int rowId = 0, string field = "", string layer = "")
        {
            Source = source ?? string.Empty;
            RowId = rowId;
            Field = field ?? string.Empty;
            Layer = layer ?? string.Empty;
        }

        public string Source { get; }
        public int RowId { get; }
        public string Field { get; }
        public string Layer { get; }
    }

    public sealed class AuthoringQuickAction
    {
        public AuthoringQuickAction(AuthoringQuickActionKind kind, string label, string source = "", string field = "", string document = "")
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Source = source ?? string.Empty;
            Field = field ?? string.Empty;
            Document = document ?? string.Empty;
        }

        public AuthoringQuickActionKind Kind { get; }
        public string Label { get; }
        public string Source { get; }
        public string Field { get; }
        public string Document { get; }
    }

    public sealed class AuthoringWorkflowStep
    {
        private readonly List<string> _inputs = new List<string>();
        private readonly List<string> _outputs = new List<string>();
        private readonly List<string> _checks = new List<string>();
        private readonly List<AuthoringQuickAction> _quickActions = new List<AuthoringQuickAction>();

        public AuthoringWorkflowStep(
            string stepId,
            string title,
            string description,
            AuthoringWorkflowStatus status,
            AuthoringWorkflowActor actor,
            bool availableInPlayerMode = true,
            bool requiresUnity = false,
            bool requiresSourceCode = false,
            bool requiresDeveloperMode = false,
            string aiPromptHint = "")
        {
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Status = status;
            Actor = actor;
            AvailableInPlayerMode = availableInPlayerMode;
            RequiresUnity = requiresUnity;
            RequiresSourceCode = requiresSourceCode;
            RequiresDeveloperMode = requiresDeveloperMode;
            AiPromptHint = aiPromptHint ?? string.Empty;
        }

        public string StepId { get; }
        public string Title { get; }
        public string Description { get; }
        public AuthoringWorkflowStatus Status { get; }
        public AuthoringWorkflowActor Actor { get; }
        public bool AvailableInPlayerMode { get; }
        public bool RequiresUnity { get; }
        public bool RequiresSourceCode { get; }
        public bool RequiresDeveloperMode { get; }
        public string AiPromptHint { get; }
        public IReadOnlyList<string> Inputs => _inputs;
        public IReadOnlyList<string> Outputs => _outputs;
        public IReadOnlyList<string> Checks => _checks;
        public IReadOnlyList<AuthoringQuickAction> QuickActions => _quickActions;

        public AuthoringWorkflowStep AddInput(string input)
        {
            if (!string.IsNullOrEmpty(input))
                _inputs.Add(input);
            return this;
        }

        public AuthoringWorkflowStep AddOutput(string output)
        {
            if (!string.IsNullOrEmpty(output))
                _outputs.Add(output);
            return this;
        }

        public AuthoringWorkflowStep AddCheck(string check)
        {
            if (!string.IsNullOrEmpty(check))
                _checks.Add(check);
            return this;
        }

        public AuthoringWorkflowStep AddAction(AuthoringQuickAction action)
        {
            if (action != null)
                _quickActions.Add(action);
            return this;
        }
    }

    public sealed class AuthoringWorkflow
    {
        private readonly List<AuthoringWorkflowStep> _steps = new List<AuthoringWorkflowStep>();

        public AuthoringWorkflow(
            string workflowId,
            string title,
            string category,
            AuthoringWorkflowStatus status,
            AuthoringWorkflowMode mode,
            AuthoringWorkflowTarget target,
            string currentStepId = "")
        {
            WorkflowId = workflowId ?? string.Empty;
            Title = title ?? string.Empty;
            Category = category ?? string.Empty;
            Status = status;
            Mode = mode;
            Target = target;
            CurrentStepId = currentStepId ?? string.Empty;
        }

        public string WorkflowId { get; }
        public string Title { get; }
        public string Category { get; }
        public AuthoringWorkflowStatus Status { get; }
        public AuthoringWorkflowMode Mode { get; }
        public AuthoringWorkflowTarget Target { get; }
        public string CurrentStepId { get; }
        public IReadOnlyList<AuthoringWorkflowStep> Steps => _steps;

        public AuthoringWorkflow AddStep(AuthoringWorkflowStep step)
        {
            if (step != null)
                _steps.Add(step);
            return this;
        }

        public AuthoringWorkflowStep GetCurrentStep()
        {
            if (string.IsNullOrEmpty(CurrentStepId))
                return _steps.Count > 0 ? _steps[0] : null;

            AuthoringWorkflowStep step = GetStep(CurrentStepId);
            return step ?? (_steps.Count > 0 ? _steps[0] : null);
        }

        public AuthoringWorkflowStep GetStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
                return null;

            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].StepId == stepId)
                    return _steps[i];
            }

            return null;
        }

        public string CreateStepAiContext(string stepId = "")
        {
            AuthoringWorkflowStep step = GetStep(stepId) ?? GetCurrentStep();
            var builder = new StringBuilder();
            builder.Append("MxFramework Authoring Workflow Step Context\n");
            builder.Append("workflow=").Append(WorkflowId).Append('\n');
            builder.Append("title=").Append(Title).Append('\n');
            builder.Append("category=").Append(Category).Append('\n');
            builder.Append("mode=").Append(Mode).Append('\n');
            builder.Append("targetSource=").Append(Target.Source).Append('\n');
            builder.Append("targetRow=").Append(Target.RowId).Append('\n');
            builder.Append("targetLayer=").Append(Target.Layer).Append('\n');
            if (step == null)
            {
                builder.Append("step=none\n");
                return builder.ToString();
            }

            builder.Append("step=").Append(step.StepId).Append('\n');
            builder.Append("stepTitle=").Append(step.Title).Append('\n');
            builder.Append("actor=").Append(step.Actor).Append('\n');
            builder.Append("status=").Append(step.Status).Append('\n');
            builder.Append("description=").Append(step.Description).Append('\n');
            builder.Append("inputs=").Append(string.Join(", ", step.Inputs)).Append('\n');
            builder.Append("outputs=").Append(string.Join(", ", step.Outputs)).Append('\n');
            builder.Append("checks=").Append(string.Join(", ", step.Checks)).Append('\n');
            builder.Append("aiPromptHint=").Append(step.AiPromptHint).Append('\n');
            return builder.ToString();
        }

    }
}
