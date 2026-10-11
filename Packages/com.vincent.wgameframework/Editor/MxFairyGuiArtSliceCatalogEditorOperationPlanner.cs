using System;
using System.Collections.Generic;

namespace MxFramework.Editor
{
    public static class MxFairyGuiArtSliceCatalogEditorOperationPlanner
    {
        public static MxFairyGuiArtSliceEditorOperationPlan PlanSaveCatalogSemantics(
            string catalogPath,
            MxFairyGuiArtSliceEditorSliceRecord selected)
        {
            var diagnostics = new List<MxFairyGuiArtSliceEditorOperationDiagnostic>();
            if (selected == null)
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR001", "No slice is selected.", string.Empty));

            if (string.IsNullOrWhiteSpace(catalogPath))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR002", "Catalog path is empty.", string.Empty));

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Info(
                    "UIARTEDITOR003",
                    "Catalog semantic and replacement constraint fields will be saved. FairyGUI XML and PNG files will not be modified.",
                    catalogPath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new MxFairyGuiArtSliceEditorOperationPlan(
                MxFairyGuiArtSliceEditorOperationKind.SaveCatalogSemantics,
                selected != null ? selected.SliceId : string.Empty,
                catalogPath,
                string.Empty,
                !blocked,
                false,
                false,
                false,
                false,
                diagnostics);
        }

        public static MxFairyGuiArtSliceEditorOperationPlan PlanReplaceExistingPng(
            string packageRootAbsolutePath,
            MxFairyGuiArtSliceEditorSliceRecord selected,
            string targetPath,
            string replacementPath)
        {
            var diagnostics = new List<MxFairyGuiArtSliceEditorOperationDiagnostic>();
            if (selected == null)
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR010", "No slice is selected.", string.Empty));

            if (string.IsNullOrWhiteSpace(packageRootAbsolutePath))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR011", "Package root path is empty.", string.Empty));

            if (string.IsNullOrWhiteSpace(targetPath))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR012", "Replacement target path is empty.", string.Empty));
            else if (!string.IsNullOrWhiteSpace(packageRootAbsolutePath)
                && !MxFairyGuiArtSliceCatalogEditorService.IsPathInside(packageRootAbsolutePath, targetPath))
            {
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error(
                    "UIARTEDITOR013",
                    "Replacement target is outside the FairyGUI package root.",
                    targetPath));
            }

            if (string.IsNullOrWhiteSpace(replacementPath))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR014", "Replacement PNG path is empty.", string.Empty));

            if (diagnostics.Count == 0)
            {
                MxFairyGuiArtSliceEditorReplacementCheck check =
                    MxFairyGuiArtSliceCatalogEditorService.ValidateReplacement(selected, targetPath, replacementPath);
                if (!check.Success)
                {
                    diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error(
                        "UIARTEDITOR015",
                        check.Message,
                        replacementPath));
                }
                else
                {
                    diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Info(
                        "UIARTEDITOR016",
                        check.Message,
                        targetPath));
                }
            }

            if (selected != null && (!string.IsNullOrWhiteSpace(selected.GroupId) || selected.ReplaceTogether))
            {
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Warning(
                    "UIARTEDITOR017",
                    "Slice belongs to a group or is marked replaceTogether; review sibling state images before replacing only this PNG.",
                    selected.SourcePath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new MxFairyGuiArtSliceEditorOperationPlan(
                MxFairyGuiArtSliceEditorOperationKind.ReplaceExistingPng,
                selected != null ? selected.SliceId : string.Empty,
                targetPath,
                replacementPath,
                false,
                !blocked,
                false,
                false,
                !blocked,
                diagnostics);
        }

        public static MxFairyGuiArtSliceEditorOperationPlan PlanRegisterDraftSlice(
            string catalogPath,
            string sliceId,
            string expectedSourcePath)
        {
            var diagnostics = new List<MxFairyGuiArtSliceEditorOperationDiagnostic>();
            if (string.IsNullOrWhiteSpace(catalogPath))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR020", "Catalog path is empty.", string.Empty));

            if (string.IsNullOrWhiteSpace(sliceId))
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Error("UIARTEDITOR021", "Draft slice id is empty.", catalogPath));

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Info(
                    "UIARTEDITOR022",
                    "Draft slice registration would update the sidecar catalog only.",
                    catalogPath));
                diagnostics.Add(MxFairyGuiArtSliceEditorOperationDiagnostic.Warning(
                    "UIARTEDITOR023",
                    "Draft registration does not create package.xml image resources or component XML usages; complete real FairyGUI authoring and publishing separately.",
                    expectedSourcePath));
            }

            bool blocked = HasBlockingDiagnostic(diagnostics);
            return new MxFairyGuiArtSliceEditorOperationPlan(
                MxFairyGuiArtSliceEditorOperationKind.RegisterDraftSlice,
                sliceId,
                catalogPath,
                expectedSourcePath,
                !blocked,
                false,
                false,
                false,
                false,
                diagnostics);
        }

        private static bool HasBlockingDiagnostic(IReadOnlyList<MxFairyGuiArtSliceEditorOperationDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Blocking)
                    return true;
            }

            return false;
        }
    }

    public enum MxFairyGuiArtSliceEditorOperationKind
    {
        SaveCatalogSemantics,
        ReplaceExistingPng,
        RegisterDraftSlice
    }

    public enum MxFairyGuiArtSliceEditorOperationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class MxFairyGuiArtSliceEditorOperationPlan
    {
        internal MxFairyGuiArtSliceEditorOperationPlan(
            MxFairyGuiArtSliceEditorOperationKind kind,
            string sliceId,
            string targetPath,
            string sourcePath,
            bool wouldModifyCatalog,
            bool wouldModifyPng,
            bool wouldModifyFairyGuiXml,
            bool wouldModifyPublishedOutput,
            bool requiresFairyGuiPublish,
            IReadOnlyList<MxFairyGuiArtSliceEditorOperationDiagnostic> diagnostics)
        {
            Kind = kind;
            SliceId = sliceId ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            WouldModifyCatalog = wouldModifyCatalog;
            WouldModifyPng = wouldModifyPng;
            WouldModifyFairyGuiXml = wouldModifyFairyGuiXml;
            WouldModifyPublishedOutput = wouldModifyPublishedOutput;
            RequiresFairyGuiPublish = requiresFairyGuiPublish;
            Diagnostics = diagnostics ?? Array.Empty<MxFairyGuiArtSliceEditorOperationDiagnostic>();
        }

        public MxFairyGuiArtSliceEditorOperationKind Kind { get; }
        public string SliceId { get; }
        public string TargetPath { get; }
        public string SourcePath { get; }
        public bool WouldModifyCatalog { get; }
        public bool WouldModifyPng { get; }
        public bool WouldModifyFairyGuiXml { get; }
        public bool WouldModifyPublishedOutput { get; }
        public bool RequiresFairyGuiPublish { get; }
        public IReadOnlyList<MxFairyGuiArtSliceEditorOperationDiagnostic> Diagnostics { get; }

        public bool Blocked
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Blocking)
                        return true;
                }

                return false;
            }
        }

        public string Summary
        {
            get
            {
                return Kind
                    + " plan: catalog="
                    + WouldModifyCatalog
                    + ", png="
                    + WouldModifyPng
                    + ", fguiXml="
                    + WouldModifyFairyGuiXml
                    + ", publishedOutput="
                    + WouldModifyPublishedOutput
                    + ", requiresPublish="
                    + RequiresFairyGuiPublish
                    + ", blocked="
                    + Blocked
                    + ".";
            }
        }

        public string ToDisplayText()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine(Summary);
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                MxFairyGuiArtSliceEditorOperationDiagnostic diagnostic = Diagnostics[i];
                builder.Append(diagnostic.Severity);
                builder.Append(" ");
                builder.Append(diagnostic.Code);
                builder.Append(": ");
                builder.Append(diagnostic.Message);
                if (!string.IsNullOrWhiteSpace(diagnostic.TargetPath))
                {
                    builder.Append(" [");
                    builder.Append(diagnostic.TargetPath);
                    builder.Append("]");
                }

                if (i + 1 < Diagnostics.Count)
                    builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    public readonly struct MxFairyGuiArtSliceEditorOperationDiagnostic
    {
        private MxFairyGuiArtSliceEditorOperationDiagnostic(
            MxFairyGuiArtSliceEditorOperationSeverity severity,
            string code,
            string message,
            string targetPath)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
        }

        public MxFairyGuiArtSliceEditorOperationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string TargetPath { get; }
        public bool Blocking => Severity == MxFairyGuiArtSliceEditorOperationSeverity.Error;

        internal static MxFairyGuiArtSliceEditorOperationDiagnostic Info(string code, string message, string targetPath)
        {
            return new MxFairyGuiArtSliceEditorOperationDiagnostic(
                MxFairyGuiArtSliceEditorOperationSeverity.Info,
                code,
                message,
                targetPath);
        }

        internal static MxFairyGuiArtSliceEditorOperationDiagnostic Warning(string code, string message, string targetPath)
        {
            return new MxFairyGuiArtSliceEditorOperationDiagnostic(
                MxFairyGuiArtSliceEditorOperationSeverity.Warning,
                code,
                message,
                targetPath);
        }

        internal static MxFairyGuiArtSliceEditorOperationDiagnostic Error(string code, string message, string targetPath)
        {
            return new MxFairyGuiArtSliceEditorOperationDiagnostic(
                MxFairyGuiArtSliceEditorOperationSeverity.Error,
                code,
                message,
                targetPath);
        }
    }
}
