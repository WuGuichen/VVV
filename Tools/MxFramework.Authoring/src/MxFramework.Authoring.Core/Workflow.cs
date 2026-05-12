using System.Collections.Generic;
using System.Text;

namespace MxFramework.Authoring
{
    public enum WorkflowStatus
    {
        NotStarted,
        InProgress,
        Blocked,
        Ready,
        Completed
    }

    public enum WorkflowMode
    {
        Mod,
        Developer
    }

    public enum WorkflowActor
    {
        Human,
        AI,
        Tool,
        Runtime
    }

    public sealed class WorkflowTarget
    {
        public string Source { get; set; } = string.Empty;
        public string RowId { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
    }

    public sealed class QuickAction
    {
        public string Kind { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;
    }

    public sealed class WorkflowStep
    {
        public string StepId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorkflowStatus Status { get; set; }
        public WorkflowActor Actor { get; set; }
        public bool AvailableInModMode { get; set; } = true;
        public bool RequiresUnity { get; set; }
        public bool RequiresSourceCode { get; set; }
        public bool RequiresDeveloperMode { get; set; }
        public string AiPromptHint { get; set; } = string.Empty;
        public List<string> Inputs { get; set; } = new List<string>();
        public List<string> Outputs { get; set; } = new List<string>();
        public List<string> Checks { get; set; } = new List<string>();
        public List<QuickAction> QuickActions { get; set; } = new List<QuickAction>();
    }

    public sealed class AuthoringWorkflow
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public WorkflowStatus Status { get; set; }
        public WorkflowMode Mode { get; set; }
        public WorkflowTarget Target { get; set; } = new WorkflowTarget();
        public string CurrentStepId { get; set; } = string.Empty;
        public List<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();

        public WorkflowStep GetStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
                return null;

            for (int i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].StepId == stepId)
                    return Steps[i];
            }

            return null;
        }

        public WorkflowStep GetCurrentStep()
        {
            WorkflowStep step = GetStep(CurrentStepId);
            return step ?? (Steps.Count > 0 ? Steps[0] : null);
        }

        public string CreateStepContext(string stepId)
        {
            WorkflowStep step = GetStep(stepId) ?? GetCurrentStep();
            var builder = new StringBuilder();
            builder.Append("MxFramework Authoring Step Context\n");
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

        public string CreateAiStepContext(string stepId, ProjectAuthoringManifest manifest, ModPackageManifest mod, IEnumerable<PatchDocument> patches)
        {
            WorkflowStep step = GetStep(stepId) ?? GetCurrentStep();
            var builder = new StringBuilder();
            builder.Append("MxFramework Authoring AI Step Context\n");
            builder.Append("workflow=").Append(WorkflowId).Append('\n');
            builder.Append("title=").Append(Title).Append('\n');
            builder.Append("category=").Append(Category).Append('\n');
            builder.Append("mode=").Append(Mode).Append('\n');
            builder.Append("targetSource=").Append(Target.Source).Append('\n');
            builder.Append("targetRow=").Append(Target.RowId).Append('\n');
            builder.Append("targetLayer=").Append(Target.Layer).Append('\n');
            builder.Append("packageId=").Append(mod != null ? mod.PackageId : string.Empty).Append('\n');
            builder.Append("packageKind=").Append(mod != null ? mod.Kind.ToString() : string.Empty).Append('\n');

            if (step == null)
            {
                builder.Append("step=none\n");
                builder.Append("schemaSlice=\n");
                builder.Append("draftSlice=\n");
                builder.Append("validationIssues=\n");
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

            PatchEntry draftEntry = FindDraftEntry(patches);
            string buffType = draftEntry != null ? draftEntry.Fields.GetScalar("Type") : string.Empty;
            ConfigSchema schema = ResolveSchema(manifest);

            builder.Append("buffType=").Append(buffType).Append('\n');
            builder.Append("schemaSlice=").Append(BuildSchemaSlice(schema, buffType)).Append('\n');
            builder.Append("draftSlice=").Append(BuildDraftSlice(draftEntry, schema, buffType)).Append('\n');
            builder.Append("validationIssues=").Append(BuildValidationSlice(manifest, mod, patches, draftEntry)).Append('\n');
            builder.Append("enumSlice=").Append(BuildEnumSlice(schema, buffType, manifest)).Append('\n');
            builder.Append("referenceSummary=").Append(BuildReferenceSummary(schema, buffType, manifest)).Append('\n');
            builder.Append("allowedActions=").Append(BuildAllowedActions()).Append('\n');
            return builder.ToString();
        }

        private string BuildAllowedActions()
        {
            string baseActions = "editField, addRow, removeRow, validate, mergePreview, exportMod";
            if (Mode == WorkflowMode.Developer)
                return baseActions + ", viewBaseLayer, exportDeveloperReport, openUnityBridge";
            return baseActions;
        }

        private static string BuildEnumSlice(ConfigSchema schema, string buffType, ProjectAuthoringManifest manifest)
        {
            if (schema == null || manifest == null) return "";
            var seen = new List<string>();
            var parts = new List<string>();
            for (int i = 0; i < schema.Fields.Count && parts.Count < 8; i++)
            {
                SchemaField f = schema.Fields[i];
                if (f.Type != FieldType.Enum || string.IsNullOrEmpty(f.EnumId)) continue;
                if (!ValidationHelpers.IsVisibleForBuffType(f, buffType)) continue;
                if (seen.Contains(f.EnumId)) continue;
                seen.Add(f.EnumId);
                EnumDomain dom = null;
                for (int j = 0; j < manifest.Enums.Count; j++)
                {
                    if (manifest.Enums[j].EnumId == f.EnumId) { dom = manifest.Enums[j]; break; }
                }
                if (dom == null) continue;
                var opts = new List<string>();
                for (int k = 0; k < dom.Options.Count; k++)
                    opts.Add(dom.Options[k].Name + "=" + dom.Options[k].DisplayName);
                parts.Add(f.EnumId + ":" + string.Join(",", opts));
            }
            return string.Join("|", parts);
        }

        private static string BuildReferenceSummary(ConfigSchema schema, string buffType, ProjectAuthoringManifest manifest)
        {
            if (schema == null || manifest == null) return "";
            var seen = new List<string>();
            var parts = new List<string>();
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                SchemaField f = schema.Fields[i];
                if (f.Type != FieldType.Reference || string.IsNullOrEmpty(f.ReferenceSource)) continue;
                if (!ValidationHelpers.IsVisibleForBuffType(f, buffType)) continue;
                if (seen.Contains(f.ReferenceSource)) continue;
                seen.Add(f.ReferenceSource);
                ReferenceIndex idx = null;
                for (int j = 0; j < manifest.References.Count; j++)
                {
                    if (manifest.References[j].Source == f.ReferenceSource) { idx = manifest.References[j]; break; }
                }
                if (idx == null) continue;
                var ids = new List<string>();
                for (int k = 0; k < idx.Entries.Count && ids.Count < 16; k++)
                {
                    if (!string.IsNullOrEmpty(idx.Entries[k].Id))
                        ids.Add(idx.Entries[k].Id);
                }
                parts.Add(f.ReferenceSource + ":" + string.Join(",", ids));
            }
            return string.Join("|", parts);
        }

        private PatchEntry FindDraftEntry(IEnumerable<PatchDocument> patches)
        {
            if (patches == null) return null;
            PatchEntry firstEntry = null;
            foreach (PatchDocument doc in patches)
            {
                if (doc == null) continue;
                for (int i = 0; i < doc.Entries.Count; i++)
                {
                    PatchEntry entry = doc.Entries[i];
                    if (firstEntry == null) firstEntry = entry;
                    if (!string.IsNullOrEmpty(Target.RowId) && entry.Id == Target.RowId)
                        return entry;
                    string entrySource = string.IsNullOrEmpty(entry.Source) ? doc.Source : entry.Source;
                    if (!string.IsNullOrEmpty(Target.Source) && entrySource == Target.Source && firstEntry == null)
                        firstEntry = entry;
                }
            }
            return firstEntry;
        }

        private ConfigSchema ResolveSchema(ProjectAuthoringManifest manifest)
        {
            if (manifest == null) return null;
            for (int i = 0; i < manifest.Schemas.Count; i++)
            {
                if (manifest.Schemas[i].SchemaId == Target.Source) return manifest.Schemas[i];
            }
            return manifest.Schemas.Count > 0 ? manifest.Schemas[0] : null;
        }

        private static string BuildSchemaSlice(ConfigSchema schema, string buffType)
        {
            if (schema == null) return "";
            var parts = new List<string>();
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                SchemaField f = schema.Fields[i];
                if (!ValidationHelpers.IsVisibleForBuffType(f, buffType)) continue;
                string flag = f.Required ? "!" : "?";
                string type = f.Type.ToString();
                parts.Add(f.Name + ":" + type + flag);
            }
            return string.Join(",", parts);
        }

        private static string BuildDraftSlice(PatchEntry entry, ConfigSchema schema, string buffType)
        {
            if (entry == null) return "";
            var parts = new List<string>();
            foreach (KeyValuePair<string, FieldValue> kv in entry.Fields)
            {
                if (schema != null)
                {
                    SchemaField f = null;
                    for (int i = 0; i < schema.Fields.Count; i++)
                    {
                        if (schema.Fields[i].Name == kv.Key) { f = schema.Fields[i]; break; }
                    }
                    if (f != null && !ValidationHelpers.IsVisibleForBuffType(f, buffType)) continue;
                }
                string val = kv.Value != null && kv.Value.Kind == FieldValueKind.Scalar
                    ? (kv.Value.Scalar ?? string.Empty)
                    : (kv.Value != null ? kv.Value.Kind.ToString() : "");
                parts.Add(kv.Key + "=" + val);
            }
            return string.Join(";", parts);
        }

        private static string BuildValidationSlice(ProjectAuthoringManifest manifest, ModPackageManifest mod, IEnumerable<PatchDocument> patches, PatchEntry entry)
        {
            ValidationReport report = AuthoringValidate.Run(manifest, mod, patches);
            var parts = new List<string>();
            string rowId = entry != null ? entry.Id : null;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                if (!string.IsNullOrEmpty(rowId) && !string.IsNullOrEmpty(issue.RowId) && issue.RowId != rowId) continue;
                parts.Add(issue.Severity + ":" + issue.Code + ":" + issue.Field);
                if (parts.Count >= 16) break;
            }
            return string.Join("|", parts);
        }
    }
}
