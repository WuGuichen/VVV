using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml.Linq;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public sealed class MxFairyGuiManifestGenerationSpec
    {
        public MxFairyGuiManifestGenerationSpec()
        {
            NamespaceName = string.Empty;
            ClassName = string.Empty;
            PackageId = string.Empty;
            PackageName = string.Empty;
            PackageSourcePath = string.Empty;
            PackageBytesPath = string.Empty;
            PackageBytesResourceId = string.Empty;
            ComponentName = string.Empty;
            ComponentSourcePath = string.Empty;
            ViewId = string.Empty;
            ViewModelTypeName = string.Empty;
            PackageIdExpression = string.Empty;
            PackageNameExpression = string.Empty;
            PackageBytesExpression = string.Empty;
            ViewIdExpression = string.Empty;
            ComponentNameExpression = string.Empty;
            ViewModelTypeExpression = string.Empty;
            Layer = MxUiLayer.Panel;
            AdditionalUsings = Array.Empty<string>();
            ControlBindPaths = Array.Empty<MxFairyGuiControlBindPathSpec>();
            ControlNameExpressions = Array.Empty<MxFairyGuiCodeExpressionSpec>();
            AdditionalPackageResources = Array.Empty<MxFairyGuiPackageResourceGenerationSpec>();
            LocalizedTexts = Array.Empty<MxFairyGuiLocalizedTextBinding>();
            Commands = Array.Empty<MxFairyGuiCommandBinding>();
            CommandIdExpressions = Array.Empty<MxFairyGuiCodeExpressionSpec>();
        }

        public string NamespaceName { get; set; }
        public string ClassName { get; set; }
        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public string PackageSourcePath { get; set; }
        public string PackageBytesPath { get; set; }
        public string PackageBytesResourceId { get; set; }
        public string ComponentName { get; set; }
        public string ComponentSourcePath { get; set; }
        public string ViewId { get; set; }
        public string ViewModelTypeName { get; set; }
        public string PackageIdExpression { get; set; }
        public string PackageNameExpression { get; set; }
        public string PackageBytesExpression { get; set; }
        public string ViewIdExpression { get; set; }
        public string ComponentNameExpression { get; set; }
        public string ViewModelTypeExpression { get; set; }
        public MxUiLayer Layer { get; set; }
        public IReadOnlyList<string> AdditionalUsings { get; set; }
        public IReadOnlyList<MxFairyGuiControlBindPathSpec> ControlBindPaths { get; set; }
        public IReadOnlyList<MxFairyGuiCodeExpressionSpec> ControlNameExpressions { get; set; }
        public IReadOnlyList<MxFairyGuiPackageResourceGenerationSpec> AdditionalPackageResources { get; set; }
        public IReadOnlyList<MxFairyGuiLocalizedTextBinding> LocalizedTexts { get; set; }
        public IReadOnlyList<MxFairyGuiCommandBinding> Commands { get; set; }
        public IReadOnlyList<MxFairyGuiCodeExpressionSpec> CommandIdExpressions { get; set; }
    }

    public readonly struct MxFairyGuiControlBindPathSpec
    {
        public MxFairyGuiControlBindPathSpec(string controlName, string bindPath)
        {
            ControlName = controlName ?? string.Empty;
            BindPath = bindPath ?? string.Empty;
        }

        public string ControlName { get; }
        public string BindPath { get; }
    }

    public readonly struct MxFairyGuiCodeExpressionSpec
    {
        public MxFairyGuiCodeExpressionSpec(string value, string expression)
        {
            Value = value ?? string.Empty;
            Expression = expression ?? string.Empty;
        }

        public string Value { get; }
        public string Expression { get; }
    }

    public readonly struct MxFairyGuiPackageResourceGenerationSpec
    {
        public MxFairyGuiPackageResourceGenerationSpec(
            string resourceId,
            string resourceTypeId,
            MxFairyGuiManifestResourceKind kind,
            string packageId = "",
            string publishedPath = "",
            string resourceKeyExpression = "")
        {
            ResourceId = resourceId ?? string.Empty;
            ResourceTypeId = resourceTypeId ?? string.Empty;
            Kind = kind;
            PackageId = packageId ?? string.Empty;
            PublishedPath = publishedPath ?? string.Empty;
            ResourceKeyExpression = resourceKeyExpression ?? string.Empty;
        }

        public string ResourceId { get; }
        public string ResourceTypeId { get; }
        public MxFairyGuiManifestResourceKind Kind { get; }
        public string PackageId { get; }
        public string PublishedPath { get; }
        public string ResourceKeyExpression { get; }
    }

    public sealed class MxFairyGuiBinderGenerationSpec
    {
        public MxFairyGuiBinderGenerationSpec()
        {
            NamespaceName = string.Empty;
            ClassName = string.Empty;
            ViewModelTypeName = string.Empty;
            ViewModelDefaultExpression = string.Empty;
            ViewIdExpression = string.Empty;
            ComponentHandleTypeName = "MxFairyGuiComponentHandle";
            InvalidHandleMessage = "FairyGUI binder requires a concrete FairyGUI component handle.";
            LocalizedTextResolverExpression = string.Empty;
            RootDefaultWidthExpression = "1280f";
            RootDefaultHeightExpression = "720f";
            AdditionalUsings = Array.Empty<string>();
            TextBindings = Array.Empty<MxFairyGuiTextBindingSpec>();
            InputBindings = Array.Empty<MxFairyGuiInputBindingSpec>();
            ButtonBindings = Array.Empty<MxFairyGuiButtonBindingSpec>();
            RepeatedBindings = Array.Empty<MxFairyGuiRepeatedBindingSpec>();
            FocusOrderExpressions = Array.Empty<string>();
            FocusRepeatedNameExpressions = Array.Empty<string>();
            DefaultFocusExpression = string.Empty;
            RequestDefaultFocusInPlayMode = true;
        }

        public string NamespaceName { get; set; }
        public string ClassName { get; set; }
        public string ViewModelTypeName { get; set; }
        public string ViewModelDefaultExpression { get; set; }
        public string ViewIdExpression { get; set; }
        public string ComponentHandleTypeName { get; set; }
        public string InvalidHandleMessage { get; set; }
        public string LocalizedTextResolverExpression { get; set; }
        public string RootDefaultWidthExpression { get; set; }
        public string RootDefaultHeightExpression { get; set; }
        public IReadOnlyList<string> AdditionalUsings { get; set; }
        public IReadOnlyList<MxFairyGuiTextBindingSpec> TextBindings { get; set; }
        public IReadOnlyList<MxFairyGuiInputBindingSpec> InputBindings { get; set; }
        public IReadOnlyList<MxFairyGuiButtonBindingSpec> ButtonBindings { get; set; }
        public IReadOnlyList<MxFairyGuiRepeatedBindingSpec> RepeatedBindings { get; set; }
        public IReadOnlyList<string> FocusOrderExpressions { get; set; }
        public IReadOnlyList<string> FocusRepeatedNameExpressions { get; set; }
        public string DefaultFocusExpression { get; set; }
        public bool RequestDefaultFocusInPlayMode { get; set; }
    }

    public readonly struct MxFairyGuiTextBindingSpec
    {
        public MxFairyGuiTextBindingSpec(string controlExpression, string valueExpression)
        {
            ControlExpression = controlExpression ?? string.Empty;
            ValueExpression = valueExpression ?? string.Empty;
        }

        public string ControlExpression { get; }
        public string ValueExpression { get; }
    }

    public readonly struct MxFairyGuiInputBindingSpec
    {
        public MxFairyGuiInputBindingSpec(string controlExpression, string valueExpression)
        {
            ControlExpression = controlExpression ?? string.Empty;
            ValueExpression = valueExpression ?? string.Empty;
        }

        public string ControlExpression { get; }
        public string ValueExpression { get; }
    }

    public readonly struct MxFairyGuiButtonBindingSpec
    {
        public MxFairyGuiButtonBindingSpec(
            string controlExpression,
            string labelExpression,
            string enabledExpression,
            string commandIdExpression,
            string payloadFactoryExpression = "",
            string selectedExpression = "false",
            bool checkMode = false)
        {
            ControlExpression = controlExpression ?? string.Empty;
            LabelExpression = labelExpression ?? string.Empty;
            EnabledExpression = enabledExpression ?? string.Empty;
            CommandIdExpression = commandIdExpression ?? string.Empty;
            PayloadFactoryExpression = payloadFactoryExpression ?? string.Empty;
            SelectedExpression = selectedExpression ?? string.Empty;
            CheckMode = checkMode;
        }

        public string ControlExpression { get; }
        public string LabelExpression { get; }
        public string EnabledExpression { get; }
        public string CommandIdExpression { get; }
        public string PayloadFactoryExpression { get; }
        public string SelectedExpression { get; }
        public bool CheckMode { get; }
    }

    public sealed class MxFairyGuiRepeatedBindingSpec
    {
        public MxFairyGuiRepeatedBindingSpec()
        {
            CountExpression = string.Empty;
            ItemTypeName = string.Empty;
            ItemVariableName = "row";
            ItemExpression = string.Empty;
            LabelControlExpression = string.Empty;
            LabelValueExpression = string.Empty;
            ValueControlExpression = string.Empty;
            ValueValueExpression = string.Empty;
            ButtonControlExpression = string.Empty;
            ButtonLabelExpression = string.Empty;
            ButtonEnabledExpression = string.Empty;
            ButtonCommandIdExpression = string.Empty;
            ButtonPayloadFactoryExpression = string.Empty;
        }

        public string CountExpression { get; set; }
        public string ItemTypeName { get; set; }
        public string ItemVariableName { get; set; }
        public string ItemExpression { get; set; }
        public string LabelControlExpression { get; set; }
        public string LabelValueExpression { get; set; }
        public string ValueControlExpression { get; set; }
        public string ValueValueExpression { get; set; }
        public string ButtonControlExpression { get; set; }
        public string ButtonLabelExpression { get; set; }
        public string ButtonEnabledExpression { get; set; }
        public string ButtonCommandIdExpression { get; set; }
        public string ButtonPayloadFactoryExpression { get; set; }
    }

    public sealed class MxFairyGuiManifestGenerationResult
    {
        public MxFairyGuiManifestGenerationResult(
            MxFairyGuiManifest manifest,
            string sourceText,
            IReadOnlyList<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            Manifest = manifest;
            SourceText = sourceText ?? string.Empty;
            Diagnostics = diagnostics == null
                ? Array.Empty<MxFairyGuiManifestDiagnostic>()
                : new ReadOnlyCollection<MxFairyGuiManifestDiagnostic>(new List<MxFairyGuiManifestDiagnostic>(diagnostics));
        }

        public MxFairyGuiManifest Manifest { get; }
        public string SourceText { get; }
        public IReadOnlyList<MxFairyGuiManifestDiagnostic> Diagnostics { get; }
        public bool Success => ErrorCount == 0;
        public int ErrorCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Error);

        private int CountSeverity(MxFairyGuiManifestDiagnosticSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Severity == severity)
                    count++;
            }

            return count;
        }
    }

    public sealed class MxFairyGuiBinderGenerationResult
    {
        public MxFairyGuiBinderGenerationResult(
            string sourceText,
            IReadOnlyList<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            SourceText = sourceText ?? string.Empty;
            Diagnostics = diagnostics == null
                ? Array.Empty<MxFairyGuiManifestDiagnostic>()
                : new ReadOnlyCollection<MxFairyGuiManifestDiagnostic>(new List<MxFairyGuiManifestDiagnostic>(diagnostics));
        }

        public string SourceText { get; }
        public IReadOnlyList<MxFairyGuiManifestDiagnostic> Diagnostics { get; }
        public bool Success => ErrorCount == 0;
        public int ErrorCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Error);

        private int CountSeverity(MxFairyGuiManifestDiagnosticSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Severity == severity)
                    count++;
            }

            return count;
        }
    }

    public sealed class MxFairyGuiManifestGenerationBatchItem
    {
        public MxFairyGuiManifestGenerationBatchItem(
            string name,
            MxFairyGuiManifestGenerationSpec spec,
            string generatedSourcePath)
            : this(name, spec, generatedSourcePath, null, string.Empty)
        {
        }

        public MxFairyGuiManifestGenerationBatchItem(
            string name,
            MxFairyGuiManifestGenerationSpec spec,
            string generatedSourcePath,
            MxFairyGuiBinderGenerationSpec binderSpec,
            string generatedBinderSourcePath)
        {
            Name = name ?? string.Empty;
            Spec = spec;
            GeneratedSourcePath = generatedSourcePath ?? string.Empty;
            BinderSpec = binderSpec;
            GeneratedBinderSourcePath = generatedBinderSourcePath ?? string.Empty;
        }

        public string Name { get; }
        public MxFairyGuiManifestGenerationSpec Spec { get; }
        public string GeneratedSourcePath { get; }
        public MxFairyGuiBinderGenerationSpec BinderSpec { get; }
        public string GeneratedBinderSourcePath { get; }
        public bool HasBinder => BinderSpec != null && !string.IsNullOrWhiteSpace(GeneratedBinderSourcePath);
    }

    public sealed class MxFairyGuiManifestGenerationBatchItemResult
    {
        public MxFairyGuiManifestGenerationBatchItemResult(
            MxFairyGuiManifestGenerationBatchItem item,
            MxFairyGuiManifestGenerationResult generation,
            MxFairyGuiManifestValidationResult generatedOutputValidation)
            : this(item, generation, generatedOutputValidation, null, null)
        {
        }

        public MxFairyGuiManifestGenerationBatchItemResult(
            MxFairyGuiManifestGenerationBatchItem item,
            MxFairyGuiManifestGenerationResult generation,
            MxFairyGuiManifestValidationResult generatedOutputValidation,
            MxFairyGuiBinderGenerationResult binderGeneration,
            MxFairyGuiManifestValidationResult generatedBinderOutputValidation)
        {
            Item = item;
            Generation = generation;
            GeneratedOutputValidation = generatedOutputValidation;
            BinderGeneration = binderGeneration;
            GeneratedBinderOutputValidation = generatedBinderOutputValidation;
        }

        public MxFairyGuiManifestGenerationBatchItem Item { get; }
        public MxFairyGuiManifestGenerationResult Generation { get; }
        public MxFairyGuiManifestValidationResult GeneratedOutputValidation { get; }
        public MxFairyGuiBinderGenerationResult BinderGeneration { get; }
        public MxFairyGuiManifestValidationResult GeneratedBinderOutputValidation { get; }
        public bool Success => Generation != null
            && Generation.Success
            && (GeneratedOutputValidation == null || GeneratedOutputValidation.Success)
            && (BinderGeneration == null || BinderGeneration.Success)
            && (GeneratedBinderOutputValidation == null || GeneratedBinderOutputValidation.Success);
        public bool GeneratedOutputCurrent => GeneratedOutputValidation != null && GeneratedOutputValidation.Success;
        public bool GeneratedBinderOutputCurrent => GeneratedBinderOutputValidation != null && GeneratedBinderOutputValidation.Success;
    }

    public sealed class MxFairyGuiManifestGenerationBatchResult
    {
        public MxFairyGuiManifestGenerationBatchResult(
            IReadOnlyList<MxFairyGuiManifestGenerationBatchItemResult> items,
            IReadOnlyList<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            Items = items == null
                ? Array.Empty<MxFairyGuiManifestGenerationBatchItemResult>()
                : new ReadOnlyCollection<MxFairyGuiManifestGenerationBatchItemResult>(new List<MxFairyGuiManifestGenerationBatchItemResult>(items));
            Diagnostics = diagnostics == null
                ? Array.Empty<MxFairyGuiManifestDiagnostic>()
                : new ReadOnlyCollection<MxFairyGuiManifestDiagnostic>(new List<MxFairyGuiManifestDiagnostic>(diagnostics));
        }

        public IReadOnlyList<MxFairyGuiManifestGenerationBatchItemResult> Items { get; }
        public IReadOnlyList<MxFairyGuiManifestDiagnostic> Diagnostics { get; }
        public bool Success => ErrorCount == 0;
        public int ErrorCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Error);
        public int WarningCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Warning);

        private int CountSeverity(MxFairyGuiManifestDiagnosticSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Severity == severity)
                    count++;
            }

            return count;
        }
    }

    public static class MxFairyGuiManifestGenerator
    {
        public static MxFairyGuiManifestGenerationResult Generate(
            MxFairyGuiManifestGenerationSpec spec,
            string projectRootPath)
        {
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>();
            if (spec == null)
            {
                diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                    MxFairyGuiManifestDiagnosticCode.ManifestMissing,
                    MxFairyGuiManifestDiagnosticTarget.Manifest,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "FairyGUI manifest generation spec is required."));
                return new MxFairyGuiManifestGenerationResult(new MxFairyGuiManifest(), string.Empty, diagnostics);
            }

            List<MxFairyGuiGeneratedControl> controls = ReadGeneratedControls(spec, projectRootPath, diagnostics);
            var bindPathsByControl = new Dictionary<string, string>(StringComparer.Ordinal);
            IReadOnlyList<MxFairyGuiControlBindPathSpec> bindPaths = spec.ControlBindPaths ?? Array.Empty<MxFairyGuiControlBindPathSpec>();
            for (int i = 0; i < bindPaths.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(bindPaths[i].ControlName))
                    bindPathsByControl[bindPaths[i].ControlName] = bindPaths[i].BindPath ?? string.Empty;
            }

            var namedControls = new List<MxFairyGuiNamedControl>();
            for (int i = 0; i < controls.Count; i++)
            {
                string bindPath;
                bindPathsByControl.TryGetValue(controls[i].Name, out bindPath);
                namedControls.Add(new MxFairyGuiNamedControl(controls[i].Name, controls[i].Kind, bindPath: bindPath));
            }

            ResourceKey packageBytes = new ResourceKey(
                spec.PackageBytesResourceId,
                MxFairyGuiManifestResourceTypeIds.PackageBytes,
                packageId: spec.PackageId);
            MxFairyGuiManifestResource[] additionalPackageResources = CreatePackageResources(spec);
            ResourceKey[] viewRequiredResources = CreateViewRequiredResources(packageBytes, additionalPackageResources);

            var manifest = new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = spec.PackageId,
                        PackageName = spec.PackageName,
                        PackageSourcePath = spec.PackageSourcePath,
                        PackageBytesPath = spec.PackageBytesPath,
                        PackageBytes = packageBytes,
                        RequiredResources = additionalPackageResources
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId(spec.ViewId),
                        PackageId = spec.PackageId,
                        ComponentName = spec.ComponentName,
                        ComponentSourcePath = spec.ComponentSourcePath,
                        ViewModelType = spec.ViewModelTypeName,
                        Layer = spec.Layer,
                        RequiredResources = viewRequiredResources,
                        NamedControls = namedControls.ToArray(),
                        LocalizedTexts = CopyLocalizedTexts(spec.LocalizedTexts),
                        Commands = CopyCommands(spec.Commands)
                    }
                }
            };

            MxFairyGuiManifestValidationResult validation = MxFairyGuiManifestValidator.ValidateSources(manifest, projectRootPath);
            diagnostics.AddRange(validation.Diagnostics);
            return new MxFairyGuiManifestGenerationResult(manifest, GenerateSource(spec, manifest), diagnostics);
        }

        public static MxFairyGuiBinderGenerationResult GenerateBinder(MxFairyGuiBinderGenerationSpec spec)
        {
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>();
            if (spec == null)
            {
                diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                    MxFairyGuiManifestDiagnosticCode.ManifestMissing,
                    MxFairyGuiManifestDiagnosticTarget.Manifest,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "FairyGUI binder generation spec is required."));
                return new MxFairyGuiBinderGenerationResult(string.Empty, diagnostics);
            }

            ValidateBinderSpec(spec, diagnostics);
            return new MxFairyGuiBinderGenerationResult(GenerateBinderSource(spec), diagnostics);
        }

        public static MxFairyGuiManifestValidationResult ValidateGeneratedOutput(
            string currentSource,
            string expectedSource,
            string generatedPath)
        {
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>();
            if (!string.Equals(NormalizeNewLines(currentSource), NormalizeNewLines(expectedSource), StringComparison.Ordinal))
            {
                diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                    MxFairyGuiManifestDiagnosticCode.GeneratedDescriptorMismatch,
                    MxFairyGuiManifestDiagnosticTarget.Manifest,
                    generatedPath,
                    string.Empty,
                    string.Empty,
                    "generatedSource",
                    "Generated FairyGUI output is stale. Regenerate it with the documented noEngine tool command."));
            }

            return new MxFairyGuiManifestValidationResult(diagnostics);
        }

        public static MxFairyGuiManifestGenerationBatchResult GenerateBatch(
            IReadOnlyList<MxFairyGuiManifestGenerationBatchItem> items,
            string projectRootPath,
            bool validateGeneratedOutput)
        {
            var results = new List<MxFairyGuiManifestGenerationBatchItemResult>();
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>();
            IReadOnlyList<MxFairyGuiManifestGenerationBatchItem> batchItems = items ?? Array.Empty<MxFairyGuiManifestGenerationBatchItem>();
            for (int i = 0; i < batchItems.Count; i++)
            {
                MxFairyGuiManifestGenerationBatchItem item = batchItems[i];
                if (item == null)
                {
                    diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                        MxFairyGuiManifestDiagnosticCode.ManifestMissing,
                        MxFairyGuiManifestDiagnosticTarget.Manifest,
                        "batchItems[" + i + "]",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        "FairyGUI manifest generation batch item is required."));
                    continue;
                }

                MxFairyGuiManifestGenerationResult generation = Generate(item.Spec, projectRootPath);
                diagnostics.AddRange(generation.Diagnostics);

                MxFairyGuiManifestValidationResult generatedValidation = null;
                if (validateGeneratedOutput)
                {
                    generatedValidation = ValidateGeneratedOutputForItem(item, generation.SourceText, projectRootPath);
                    diagnostics.AddRange(generatedValidation.Diagnostics);
                }

                MxFairyGuiBinderGenerationResult binderGeneration = null;
                MxFairyGuiManifestValidationResult generatedBinderValidation = null;
                if (item.HasBinder)
                {
                    binderGeneration = GenerateBinder(item.BinderSpec);
                    diagnostics.AddRange(binderGeneration.Diagnostics);
                    if (validateGeneratedOutput)
                    {
                        generatedBinderValidation = ValidateGeneratedBinderOutputForItem(item, binderGeneration.SourceText, projectRootPath);
                        diagnostics.AddRange(generatedBinderValidation.Diagnostics);
                    }
                }

                results.Add(new MxFairyGuiManifestGenerationBatchItemResult(
                    item,
                    generation,
                    generatedValidation,
                    binderGeneration,
                    generatedBinderValidation));
            }

            return new MxFairyGuiManifestGenerationBatchResult(results, diagnostics);
        }

        public static string GenerateReviewReport(MxFairyGuiManifestGenerationBatchResult result)
        {
            var builder = new StringBuilder();
            MxFairyGuiManifestGenerationBatchResult batchResult = result
                ?? new MxFairyGuiManifestGenerationBatchResult(
                    Array.Empty<MxFairyGuiManifestGenerationBatchItemResult>(),
                    new[]
                    {
                        MxFairyGuiManifestDiagnostic.Error(
                            MxFairyGuiManifestDiagnosticCode.ManifestMissing,
                            MxFairyGuiManifestDiagnosticTarget.Manifest,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            "FairyGUI manifest generation batch result is required.")
                    });

            builder.AppendLine("FairyGUI Manifest Review Report");
            builder.AppendLine("schemaVersion: " + MxFairyGuiManifest.CurrentSchemaVersion);
            builder.AppendLine("status: " + (batchResult.Success ? "ok" : "failed"));
            builder.AppendLine("items: " + batchResult.Items.Count);
            builder.AppendLine("errors: " + batchResult.ErrorCount);
            builder.AppendLine("warnings: " + batchResult.WarningCount);
            builder.AppendLine();
            builder.AppendLine("Targets");
            for (int i = 0; i < batchResult.Items.Count; i++)
            {
                AppendReviewReportItem(builder, batchResult.Items[i]);
            }

            builder.AppendLine("Diagnostics");
            if (batchResult.Diagnostics.Count == 0)
            {
                builder.AppendLine("- none");
            }
            else
            {
                for (int i = 0; i < batchResult.Diagnostics.Count; i++)
                    AppendReviewReportDiagnostic(builder, batchResult.Diagnostics[i]);
            }

            return builder.ToString();
        }

        private static List<MxFairyGuiGeneratedControl> ReadGeneratedControls(
            MxFairyGuiManifestGenerationSpec spec,
            string projectRootPath,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            var controls = new List<MxFairyGuiGeneratedControl>();
            string absoluteComponentPath = ResolvePath(projectRootPath, spec.ComponentSourcePath);
            try
            {
                XDocument document = XDocument.Load(absoluteComponentPath);
                foreach (XElement element in document.Descendants())
                {
                    string name = ReadAttribute(element, "name");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string kind = InferControlKind(element);
                    if (string.Equals(kind, "Text", StringComparison.Ordinal)
                        || string.Equals(kind, "Button", StringComparison.Ordinal))
                    {
                        controls.Add(new MxFairyGuiGeneratedControl(name, kind));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            {
                diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                    MxFairyGuiManifestDiagnosticCode.ComponentSourceInvalid,
                    MxFairyGuiManifestDiagnosticTarget.View,
                    spec.ComponentSourcePath,
                    spec.PackageId,
                    spec.ViewId,
                    "componentSourcePath",
                    "FairyGUI component source XML could not be read by generator: " + ex.Message));
            }

            return controls;
        }

        private static MxFairyGuiManifestValidationResult ValidateGeneratedOutputForItem(
            MxFairyGuiManifestGenerationBatchItem item,
            string expectedSource,
            string projectRootPath)
        {
            string generatedPath = item.GeneratedSourcePath ?? string.Empty;
            string absolutePath = ResolvePath(projectRootPath, generatedPath);
            try
            {
                string currentSource = File.ReadAllText(absolutePath);
                return ValidateGeneratedOutput(currentSource, expectedSource, generatedPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new MxFairyGuiManifestValidationResult(new[]
                {
                    MxFairyGuiManifestDiagnostic.Error(
                        MxFairyGuiManifestDiagnosticCode.GeneratedDescriptorMismatch,
                        MxFairyGuiManifestDiagnosticTarget.Manifest,
                        generatedPath,
                        item.Spec != null ? item.Spec.PackageId : string.Empty,
                        item.Spec != null ? item.Spec.ViewId : string.Empty,
                        "generatedSource",
                        "Generated FairyGUI manifest output could not be read: " + ex.Message)
                });
            }
        }

        private static MxFairyGuiManifestValidationResult ValidateGeneratedBinderOutputForItem(
            MxFairyGuiManifestGenerationBatchItem item,
            string expectedSource,
            string projectRootPath)
        {
            string generatedPath = item.GeneratedBinderSourcePath ?? string.Empty;
            string absolutePath = ResolvePath(projectRootPath, generatedPath);
            try
            {
                string currentSource = File.ReadAllText(absolutePath);
                return ValidateGeneratedOutput(currentSource, expectedSource, generatedPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new MxFairyGuiManifestValidationResult(new[]
                {
                    MxFairyGuiManifestDiagnostic.Error(
                        MxFairyGuiManifestDiagnosticCode.GeneratedDescriptorMismatch,
                        MxFairyGuiManifestDiagnosticTarget.Manifest,
                        generatedPath,
                        item.Spec != null ? item.Spec.PackageId : string.Empty,
                        item.Spec != null ? item.Spec.ViewId : string.Empty,
                        "generatedBinderSource",
                        "Generated FairyGUI binder output could not be read: " + ex.Message)
                });
            }
        }

        private static void AppendReviewReportItem(
            StringBuilder builder,
            MxFairyGuiManifestGenerationBatchItemResult result)
        {
            MxFairyGuiManifestGenerationBatchItem item = result.Item;
            MxFairyGuiManifest manifest = result.Generation.Manifest;
            IReadOnlyList<MxFairyGuiManifestPackage> packages = manifest.Packages ?? Array.Empty<MxFairyGuiManifestPackage>();
            IReadOnlyList<MxFairyGuiManifestView> views = manifest.Views ?? Array.Empty<MxFairyGuiManifestView>();
            int controlCount = 0;
            int localizedTextCount = 0;
            int commandCount = 0;
            for (int i = 0; i < views.Count; i++)
            {
                MxFairyGuiManifestView view = views[i];
                if (view == null)
                    continue;

                controlCount += view.NamedControls != null ? view.NamedControls.Count : 0;
                localizedTextCount += view.LocalizedTexts != null ? view.LocalizedTexts.Count : 0;
                commandCount += view.Commands != null ? view.Commands.Count : 0;
            }

            builder.Append("- name: ");
            builder.AppendLine(item.Name);
            builder.AppendLine("  status: " + (result.Success ? "ok" : "failed"));
            builder.AppendLine("  generatedSourcePath: " + item.GeneratedSourcePath);
            builder.AppendLine("  generatedOutputCurrent: " + (result.GeneratedOutputValidation == null ? "not-checked" : (result.GeneratedOutputCurrent ? "true" : "false")));
            if (item.HasBinder)
            {
                builder.AppendLine("  generatedBinderSourcePath: " + item.GeneratedBinderSourcePath);
                builder.AppendLine("  generatedBinderOutputCurrent: " + (result.GeneratedBinderOutputValidation == null ? "not-checked" : (result.GeneratedBinderOutputCurrent ? "true" : "false")));
            }

            builder.AppendLine("  packageCount: " + packages.Count);
            builder.AppendLine("  viewCount: " + views.Count);
            builder.AppendLine("  controlCount: " + controlCount);
            builder.AppendLine("  localizedTextCount: " + localizedTextCount);
            builder.AppendLine("  commandCount: " + commandCount);
            for (int i = 0; i < packages.Count; i++)
            {
                MxFairyGuiManifestPackage package = packages[i];
                if (package == null)
                    continue;

                builder.AppendLine("  package[" + i + "]: " + package.PackageId + " -> " + package.PackageBytesPath);
            }

            for (int i = 0; i < views.Count; i++)
            {
                MxFairyGuiManifestView view = views[i];
                if (view == null)
                    continue;

                builder.AppendLine("  view[" + i + "]: " + view.ViewId.Value + " -> " + view.PackageId + "/" + view.ComponentName);
            }
        }

        private static void AppendReviewReportDiagnostic(
            StringBuilder builder,
            MxFairyGuiManifestDiagnostic diagnostic)
        {
            builder.Append("- ");
            builder.Append(diagnostic.Severity);
            builder.Append(" ");
            builder.Append(diagnostic.Code);
            builder.Append(" target=");
            builder.Append(diagnostic.Target);
            builder.Append(" package=");
            builder.Append(diagnostic.PackageId);
            builder.Append(" view=");
            builder.Append(diagnostic.ViewId);
            builder.Append(" field=");
            builder.Append(diagnostic.Field);
            builder.Append(" source=");
            builder.Append(diagnostic.SourcePath);
            builder.Append(" message=");
            builder.AppendLine(diagnostic.Message);
        }

        private static string GenerateSource(MxFairyGuiManifestGenerationSpec spec, MxFairyGuiManifest manifest)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("using MxFramework.Resources;");
            builder.AppendLine("using MxFramework.UI;");
            builder.AppendLine("using MxFramework.UI.FairyGui;");
            AppendAdditionalUsings(builder, spec.AdditionalUsings);
            builder.AppendLine();
            builder.AppendLine("namespace " + spec.NamespaceName);
            builder.AppendLine("{");
            builder.AppendLine("    public static class " + spec.ClassName);
            builder.AppendLine("    {");
            builder.AppendLine("        public static MxFairyGuiManifest Create()");
            builder.AppendLine("        {");
            builder.AppendLine("            ResourceKey packageBytes = " + ExpressionOrLiteral(spec.PackageBytesExpression, manifest.Packages[0].PackageBytes) + ";");
            MxFairyGuiManifestPackage package = manifest.Packages[0];
            IReadOnlyList<MxFairyGuiManifestResource> packageResources =
                package.RequiredResources ?? Array.Empty<MxFairyGuiManifestResource>();
            for (int i = 0; i < packageResources.Count; i++)
            {
                builder.AppendLine("            ResourceKey packageResource" + i + " = " + ExpressionOrLiteral(PackageResourceExpression(spec, i), packageResources[i].Key) + ";");
            }

            builder.AppendLine();
            builder.AppendLine("            return new MxFairyGuiManifest");
            builder.AppendLine("            {");
            builder.AppendLine("                Packages = new[]");
            builder.AppendLine("                {");
            builder.AppendLine("                    new MxFairyGuiManifestPackage");
            builder.AppendLine("                    {");
            string packageIdExpression = ExpressionOrLiteral(spec.PackageIdExpression, package.PackageId);
            builder.AppendLine("                        PackageId = " + packageIdExpression + ",");
            builder.AppendLine("                        PackageName = " + ExpressionOrLiteral(spec.PackageNameExpression, package.PackageName) + ",");
            builder.AppendLine("                        PackageSourcePath = " + Literal(package.PackageSourcePath) + ",");
            builder.AppendLine("                        PackageBytesPath = " + Literal(package.PackageBytesPath) + ",");
            builder.AppendLine(packageResources.Count > 0
                ? "                        PackageBytes = packageBytes,"
                : "                        PackageBytes = packageBytes");
            if (packageResources.Count > 0)
            {
                builder.AppendLine("                        RequiredResources = new[]");
                builder.AppendLine("                        {");
                for (int i = 0; i < packageResources.Count; i++)
                {
                    MxFairyGuiManifestResource resource = packageResources[i];
                    builder.Append("                            new MxFairyGuiManifestResource(packageResource");
                    builder.Append(i);
                    builder.Append(", MxFairyGuiManifestResourceKind.");
                    builder.Append(resource.Kind);
                    if (!resource.Required || !string.IsNullOrEmpty(resource.PublishedPath))
                    {
                        builder.Append(", required: ");
                        builder.Append(resource.Required ? "true" : "false");
                    }

                    if (!string.IsNullOrEmpty(resource.PublishedPath))
                    {
                        builder.Append(", publishedPath: ");
                        builder.Append(Literal(resource.PublishedPath));
                    }

                    builder.Append(")");
                    builder.AppendLine(i == packageResources.Count - 1 ? string.Empty : ",");
                }

                builder.AppendLine("                        }");
            }

            builder.AppendLine("                    }");
            builder.AppendLine("                },");
            builder.AppendLine("                Views = new[]");
            builder.AppendLine("                {");
            MxFairyGuiManifestView view = manifest.Views[0];
            builder.AppendLine("                    new MxFairyGuiManifestView");
            builder.AppendLine("                    {");
            builder.AppendLine("                        ViewId = " + ExpressionOrNewViewId(spec.ViewIdExpression, view.ViewId) + ",");
            builder.AppendLine("                        PackageId = " + packageIdExpression + ",");
            builder.AppendLine("                        ComponentName = " + ExpressionOrLiteral(spec.ComponentNameExpression, view.ComponentName) + ",");
            builder.AppendLine("                        ComponentSourcePath = " + Literal(view.ComponentSourcePath) + ",");
            builder.AppendLine("                        ViewModelType = " + ExpressionOrLiteral(spec.ViewModelTypeExpression, view.ViewModelType) + ",");
            builder.AppendLine("                        Layer = MxUiLayer." + view.Layer + ",");
            AppendViewRequiredResources(builder, packageResources.Count);
            builder.AppendLine("                        NamedControls = new[]");
            builder.AppendLine("                        {");
            for (int i = 0; i < view.NamedControls.Count; i++)
            {
                MxFairyGuiNamedControl control = view.NamedControls[i];
                string idExpression = ExpressionOrLiteral(FindExpression(spec.ControlNameExpressions, control.Name), control.Name);
                string suffix = string.IsNullOrWhiteSpace(control.BindPath)
                    ? string.Empty
                    : ", bindPath: " + Literal(control.BindPath);
                builder.Append("                            new MxFairyGuiNamedControl(");
                builder.Append(idExpression);
                builder.Append(", ");
                builder.Append(Literal(control.ControlType));
                builder.Append(suffix);
                builder.Append(")");
                builder.AppendLine(i == view.NamedControls.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("                        },");
            builder.AppendLine("                        LocalizedTexts = new MxFairyGuiLocalizedTextBinding[]");
            builder.AppendLine("                        {");
            for (int i = 0; i < view.LocalizedTexts.Count; i++)
            {
                MxFairyGuiLocalizedTextBinding binding = view.LocalizedTexts[i];
                builder.Append("                            new MxFairyGuiLocalizedTextBinding(");
                builder.Append(ExpressionOrLiteral(FindExpression(spec.ControlNameExpressions, binding.ControlName), binding.ControlName));
                builder.Append(", ");
                builder.Append(Literal(binding.TextKey));
                if (!string.IsNullOrEmpty(binding.FallbackText) || !binding.Required)
                {
                    builder.Append(", fallbackText: ");
                    builder.Append(Literal(binding.FallbackText));
                }

                if (!binding.Required)
                    builder.Append(", required: false");

                builder.Append(")");
                builder.AppendLine(i == view.LocalizedTexts.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("                        },");
            builder.AppendLine(view.Commands.Count == 0
                ? "                        Commands = new MxFairyGuiCommandBinding[]"
                : "                        Commands = new[]");
            builder.AppendLine("                        {");
            for (int i = 0; i < view.Commands.Count; i++)
            {
                MxFairyGuiCommandBinding command = view.Commands[i];
                builder.Append("                            new MxFairyGuiCommandBinding(");
                builder.Append(ExpressionOrLiteral(FindExpression(spec.CommandIdExpressions, command.CommandId), command.CommandId));
                builder.Append(", ");
                builder.Append(ExpressionOrLiteral(FindExpression(spec.ControlNameExpressions, command.ControlName), command.ControlName));
                builder.Append(", ");
                AppendFeedbackPolicyExpression(builder, command.Feedback);
                if (!string.IsNullOrEmpty(command.PayloadType) || command.RequiresConfirmation)
                {
                    builder.Append(", payloadType: ");
                    builder.Append(Literal(command.PayloadType));
                }

                if (command.RequiresConfirmation)
                    builder.Append(", requiresConfirmation: true");

                builder.Append(")");
                builder.AppendLine(i == view.Commands.Count - 1 ? string.Empty : ",");
            }

            builder.AppendLine("                        }");
            builder.AppendLine("                    }");
            builder.AppendLine("                }");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public static MxUiViewContract CreateViewContract()");
            builder.AppendLine("        {");
            builder.AppendLine("            return MxFairyGuiManifestProjection.CreateViewContracts(Create())[0];");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public static MxFairyGuiPackageDescriptor CreatePackageDescriptor()");
            builder.AppendLine("        {");
            builder.AppendLine("            MxFairyGuiManifest manifest = Create();");
            builder.AppendLine("            MxFairyGuiManifestPackage package = manifest.Packages[0];");
            builder.AppendLine("            var resources = new MxFairyGuiPackageResourceDescriptor[package.RequiredResources.Count];");
            builder.AppendLine("            for (int i = 0; i < resources.Length; i++)");
            builder.AppendLine("            {");
            builder.AppendLine("                MxFairyGuiManifestResource resource = package.RequiredResources[i];");
            builder.AppendLine("                resources[i] = new MxFairyGuiPackageResourceDescriptor(resource.Key, ToPackageResourceKind(resource.Kind), resource.Required);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return new MxFairyGuiPackageDescriptor(package.PackageId, package.PackageBytes, resources);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static MxFairyGuiPackageResourceKind ToPackageResourceKind(MxFairyGuiManifestResourceKind kind)");
            builder.AppendLine("        {");
            builder.AppendLine("            switch (kind)");
            builder.AppendLine("            {");
            builder.AppendLine("                case MxFairyGuiManifestResourceKind.Atlas:");
            builder.AppendLine("                    return MxFairyGuiPackageResourceKind.Atlas;");
            builder.AppendLine("                case MxFairyGuiManifestResourceKind.Audio:");
            builder.AppendLine("                    return MxFairyGuiPackageResourceKind.Audio;");
            builder.AppendLine("                case MxFairyGuiManifestResourceKind.Font:");
            builder.AppendLine("                    return MxFairyGuiPackageResourceKind.Font;");
            builder.AppendLine("                default:");
            builder.AppendLine("                    return MxFairyGuiPackageResourceKind.Resource;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string GenerateBinderSource(MxFairyGuiBinderGenerationSpec spec)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using MxFramework.UI;");
            builder.AppendLine("using MxFramework.UI.FairyGui;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine("using Fgui = global::FairyGUI;");
            AppendAdditionalUsings(builder, spec.AdditionalUsings);
            builder.AppendLine();
            builder.AppendLine("namespace " + spec.NamespaceName);
            builder.AppendLine("{");
            builder.AppendLine("    public sealed partial class " + spec.ClassName + " :");
            builder.AppendLine("        IMxFairyGuiViewBinder<" + spec.ViewModelTypeName + ">");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly IMxUiCommandSink _commandSink;");
            builder.AppendLine("        private readonly MxUiViewId _viewId;");
            builder.AppendLine("        private readonly IMxUiTextProvider _textProvider;");
            builder.AppendLine("        private MxFairyGuiFocusNavigationMetadata _focusNavigationMetadata;");
            builder.AppendLine();
            builder.AppendLine("        public " + spec.ClassName + "(IMxUiCommandSink commandSink)");
            builder.AppendLine("            : this(commandSink, " + spec.ViewIdExpression + ", null)");
            builder.AppendLine("        {");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public " + spec.ClassName + "(");
            builder.AppendLine("            IMxUiCommandSink commandSink,");
            builder.AppendLine("            MxUiViewId viewId,");
            builder.AppendLine("            IMxUiTextProvider textProvider = null)");
            builder.AppendLine("        {");
            builder.AppendLine("            _commandSink = commandSink;");
            builder.AppendLine("            _viewId = viewId;");
            builder.AppendLine("            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public void Bind(IMxFairyGuiComponentHandle component, " + spec.ViewModelTypeName + " viewModel)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (component == null)");
            builder.AppendLine("                throw new ArgumentNullException(nameof(component));");
            builder.AppendLine();
            builder.AppendLine("            var handle = component as " + CodeOrDefault(spec.ComponentHandleTypeName, "MxFairyGuiComponentHandle") + ";");
            builder.AppendLine("            if (handle == null)");
            builder.AppendLine("                throw new ArgumentException(" + Literal(spec.InvalidHandleMessage) + ", nameof(component));");
            builder.AppendLine();
            builder.AppendLine("            viewModel = viewModel ?? " + CodeOrDefault(spec.ViewModelDefaultExpression, "new " + spec.ViewModelTypeName + "()") + ";");
            builder.AppendLine("            Fgui.GComponent root = handle.Component;");
            builder.AppendLine("            root.SetSize(root.width > 0f ? root.width : " + CodeOrDefault(spec.RootDefaultWidthExpression, "1280f") + ", root.height > 0f ? root.height : " + CodeOrDefault(spec.RootDefaultHeightExpression, "720f") + ");");
            AppendTextBindingCalls(builder, spec.TextBindings, "            ");
            AppendInputBindingCalls(builder, spec.InputBindings, "            ");
            AppendButtonBindingCalls(builder, spec.ButtonBindings, "            ");
            IReadOnlyList<MxFairyGuiRepeatedBindingSpec> repeatedBindings = spec.RepeatedBindings ?? Array.Empty<MxFairyGuiRepeatedBindingSpec>();
            for (int i = 0; i < repeatedBindings.Count; i++)
                builder.AppendLine("            BindRepeated" + i + "(root, viewModel);");
            builder.AppendLine("            ConfigureFocus(root);");
            if (spec.RequestDefaultFocusInPlayMode)
            {
                builder.AppendLine("            if (Application.isPlaying)");
                builder.AppendLine("                MxFairyGuiFocusNavigation.RequestDefaultFocus(root);");
            }

            builder.AppendLine("            OnAfterGeneratedBind(root, viewModel);");
            builder.AppendLine("        }");

            for (int i = 0; i < repeatedBindings.Count; i++)
                AppendRepeatedBindingMethod(builder, repeatedBindings[i], i, spec.ViewModelTypeName);

            AppendBindButtonMethod(builder);
            AppendBindTextMethod(builder);
            AppendBindInputMethod(builder);
            AppendReadTextMethod(builder);
            AppendConfigureFocusMethod(builder, spec);
            builder.AppendLine();
            builder.AppendLine("        private string L(string key, string fallback)");
            builder.AppendLine("        {");
            if (string.IsNullOrWhiteSpace(spec.LocalizedTextResolverExpression))
                builder.AppendLine("            return fallback ?? string.Empty;");
            else
                builder.AppendLine("            return " + ResolveLocalizationExpression(spec.LocalizedTextResolverExpression) + ";");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        partial void OnAfterGeneratedBind(Fgui.GComponent root, " + spec.ViewModelTypeName + " viewModel);");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendTextBindingCalls(
            StringBuilder builder,
            IReadOnlyList<MxFairyGuiTextBindingSpec> bindings,
            string indent)
        {
            IReadOnlyList<MxFairyGuiTextBindingSpec> values = bindings ?? Array.Empty<MxFairyGuiTextBindingSpec>();
            for (int i = 0; i < values.Count; i++)
                builder.AppendLine(indent + "BindText(root, " + values[i].ControlExpression + ", " + values[i].ValueExpression + ");");
        }

        private static void AppendInputBindingCalls(
            StringBuilder builder,
            IReadOnlyList<MxFairyGuiInputBindingSpec> bindings,
            string indent)
        {
            IReadOnlyList<MxFairyGuiInputBindingSpec> values = bindings ?? Array.Empty<MxFairyGuiInputBindingSpec>();
            for (int i = 0; i < values.Count; i++)
                builder.AppendLine(indent + "BindInput(root, " + values[i].ControlExpression + ", " + values[i].ValueExpression + ");");
        }

        private static void AppendButtonBindingCalls(
            StringBuilder builder,
            IReadOnlyList<MxFairyGuiButtonBindingSpec> bindings,
            string indent)
        {
            IReadOnlyList<MxFairyGuiButtonBindingSpec> values = bindings ?? Array.Empty<MxFairyGuiButtonBindingSpec>();
            for (int i = 0; i < values.Count; i++)
                AppendButtonBindingCall(builder, values[i], indent);
        }

        private static void AppendButtonBindingCall(
            StringBuilder builder,
            MxFairyGuiButtonBindingSpec binding,
            string indent)
        {
            builder.Append(indent);
            builder.Append("BindButton(root, ");
            builder.Append(binding.ControlExpression);
            builder.Append(", ");
            builder.Append(CodeOrDefault(binding.LabelExpression, "string.Empty"));
            builder.Append(", ");
            builder.Append(CodeOrDefault(binding.EnabledExpression, "true"));
            builder.Append(", ");
            builder.Append(binding.CommandIdExpression);
            builder.Append(", ");
            builder.Append(CodeOrDefault(binding.PayloadFactoryExpression, "null"));
            builder.Append(", ");
            builder.Append(CodeOrDefault(binding.SelectedExpression, "false"));
            builder.Append(", checkMode: ");
            builder.Append(binding.CheckMode ? "true" : "false");
            builder.AppendLine(");");
        }

        private static void AppendRepeatedBindingMethod(
            StringBuilder builder,
            MxFairyGuiRepeatedBindingSpec spec,
            int methodIndex,
            string viewModelTypeName)
        {
            string itemVariableName = CodeOrDefault(spec.ItemVariableName, "row");
            builder.AppendLine();
            builder.AppendLine("        private void BindRepeated" + methodIndex + "(");
            builder.AppendLine("            Fgui.GComponent root,");
            builder.AppendLine("            " + viewModelTypeName + " viewModel)");
            builder.AppendLine("        {");
            builder.AppendLine("            for (int i = 0; i < " + spec.CountExpression + "; i++)");
            builder.AppendLine("            {");
            builder.AppendLine("                " + spec.ItemTypeName + " " + itemVariableName + " = " + spec.ItemExpression + ";");
            if (!string.IsNullOrWhiteSpace(spec.LabelControlExpression))
                builder.AppendLine("                BindText(root, " + spec.LabelControlExpression + ", " + spec.LabelValueExpression + ");");
            if (!string.IsNullOrWhiteSpace(spec.ValueControlExpression))
                builder.AppendLine("                BindText(root, " + spec.ValueControlExpression + ", " + spec.ValueValueExpression + ");");
            if (!string.IsNullOrWhiteSpace(spec.ButtonControlExpression))
            {
                builder.AppendLine("                int capturedIndex = i;");
                var button = new MxFairyGuiButtonBindingSpec(
                    spec.ButtonControlExpression,
                    spec.ButtonLabelExpression,
                    spec.ButtonEnabledExpression,
                    spec.ButtonCommandIdExpression,
                    spec.ButtonPayloadFactoryExpression);
                AppendButtonBindingCall(builder, button, "                ");
            }

            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        private static void AppendBindButtonMethod(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        private void BindButton(");
            builder.AppendLine("            Fgui.GComponent root,");
            builder.AppendLine("            string childName,");
            builder.AppendLine("            string label,");
            builder.AppendLine("            bool enabled,");
            builder.AppendLine("            string commandId,");
            builder.AppendLine("            Func<object> payloadFactory,");
            builder.AppendLine("            bool selected = false,");
            builder.AppendLine("            bool checkMode = false)");
            builder.AppendLine("        {");
            builder.AppendLine("            Fgui.GButton button = root.GetChild(childName)?.asButton;");
            builder.AppendLine("            if (button == null)");
            builder.AppendLine("                return;");
            builder.AppendLine();
            builder.AppendLine("            if (checkMode)");
            builder.AppendLine("                button.mode = Fgui.ButtonMode.Check;");
            builder.AppendLine("            button.text = label ?? string.Empty;");
            builder.AppendLine("            button.enabled = enabled;");
            builder.AppendLine("            button.grayed = !enabled;");
            builder.AppendLine("            button.selected = selected;");
            builder.AppendLine("            button.visible = true;");
            builder.AppendLine("            button.onClick.Set(() =>");
            builder.AppendLine("            {");
            builder.AppendLine("                if (!enabled)");
            builder.AppendLine("                    return;");
            builder.AppendLine();
            builder.AppendLine("                object payload = payloadFactory != null ? payloadFactory() : null;");
            builder.AppendLine("                _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));");
            builder.AppendLine("            });");
            builder.AppendLine("        }");
        }

        private static void AppendBindTextMethod(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        private static void BindText(Fgui.GComponent root, string childName, string value)");
            builder.AppendLine("        {");
            builder.AppendLine("            Fgui.GTextField text = root.GetChild(childName)?.asTextField;");
            builder.AppendLine("            if (text != null)");
            builder.AppendLine("                text.text = value ?? string.Empty;");
            builder.AppendLine("        }");
        }

        private static void AppendBindInputMethod(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        private static void BindInput(Fgui.GComponent root, string childName, string value)");
            builder.AppendLine("        {");
            builder.AppendLine("            Fgui.GTextField input = root.GetChild(childName)?.asTextField;");
            builder.AppendLine("            if (input == null)");
            builder.AppendLine("                return;");
            builder.AppendLine();
            builder.AppendLine("            if (!Application.isPlaying || !input.focused)");
            builder.AppendLine("                input.text = value ?? string.Empty;");
            builder.AppendLine();
            builder.AppendLine("            Fgui.GTextInput textInput = input as Fgui.GTextInput;");
            builder.AppendLine("            if (textInput != null)");
            builder.AppendLine("                textInput.editable = true;");
            builder.AppendLine("        }");
        }

        private static void AppendReadTextMethod(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        private static string ReadText(Fgui.GComponent root, string childName, string fallback)");
            builder.AppendLine("        {");
            builder.AppendLine("            Fgui.GObject child = root.GetChild(childName);");
            builder.AppendLine("            Fgui.GTextField text = child != null ? child.asTextField : null;");
            builder.AppendLine("            if (text == null)");
            builder.AppendLine("                return fallback;");
            builder.AppendLine();
            builder.AppendLine("            return string.IsNullOrWhiteSpace(text.text) ? fallback : text.text.Trim();");
            builder.AppendLine("        }");
        }

        private static void AppendConfigureFocusMethod(
            StringBuilder builder,
            MxFairyGuiBinderGenerationSpec spec)
        {
            builder.AppendLine();
            builder.AppendLine("        private void ConfigureFocus(Fgui.GComponent root)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (_focusNavigationMetadata == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                var names = new List<string>");
            builder.AppendLine("                {");
            IReadOnlyList<string> focusOrder = spec.FocusOrderExpressions ?? Array.Empty<string>();
            for (int i = 0; i < focusOrder.Count; i++)
            {
                builder.Append("                    ");
                builder.Append(focusOrder[i]);
                builder.AppendLine(i == focusOrder.Count - 1 && (spec.FocusRepeatedNameExpressions == null || spec.FocusRepeatedNameExpressions.Count == 0) ? string.Empty : ",");
            }

            builder.AppendLine("                };");
            IReadOnlyList<string> repeatedNames = spec.FocusRepeatedNameExpressions ?? Array.Empty<string>();
            if (repeatedNames.Count > 0)
            {
                string countExpression = FindFocusRepeatedCountExpression(spec);
                builder.AppendLine("                for (int i = 0; i < " + countExpression + "; i++)");
                builder.AppendLine("                {");
                for (int i = 0; i < repeatedNames.Count; i++)
                    builder.AppendLine("                    names.Add(" + repeatedNames[i] + ");");
                builder.AppendLine("                }");
                builder.AppendLine();
            }

            builder.AppendLine("                _focusNavigationMetadata = new MxFairyGuiFocusNavigationMetadata(");
            builder.AppendLine("                    _viewId,");
            builder.AppendLine("                    " + CodeOrDefault(spec.DefaultFocusExpression, "string.Empty") + ",");
            builder.AppendLine("                    names);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            MxFairyGuiFocusNavigation.Configure(");
            builder.AppendLine("                root,");
            builder.AppendLine("                _focusNavigationMetadata);");
            builder.AppendLine("        }");
        }

        private static void ValidateBinderSpec(
            MxFairyGuiBinderGenerationSpec spec,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(spec.NamespaceName))
                AddBinderGenerationError(diagnostics, "namespaceName", "FairyGUI binder namespace is required.");
            if (string.IsNullOrWhiteSpace(spec.ClassName))
                AddBinderGenerationError(diagnostics, "className", "FairyGUI binder class name is required.");
            if (string.IsNullOrWhiteSpace(spec.ViewModelTypeName))
                AddBinderGenerationError(diagnostics, "viewModelTypeName", "FairyGUI binder view model type is required.");
            if (string.IsNullOrWhiteSpace(spec.ViewIdExpression))
                AddBinderGenerationError(diagnostics, "viewIdExpression", "FairyGUI binder view id expression is required.");
        }

        private static void AddBinderGenerationError(
            List<MxFairyGuiManifestDiagnostic> diagnostics,
            string field,
            string message)
        {
            diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(
                MxFairyGuiManifestDiagnosticCode.ManifestMissing,
                MxFairyGuiManifestDiagnosticTarget.Manifest,
                string.Empty,
                string.Empty,
                string.Empty,
                field,
                message));
        }

        private static MxFairyGuiManifestResource[] CreatePackageResources(MxFairyGuiManifestGenerationSpec spec)
        {
            IReadOnlyList<MxFairyGuiPackageResourceGenerationSpec> source =
                spec.AdditionalPackageResources ?? Array.Empty<MxFairyGuiPackageResourceGenerationSpec>();
            if (source.Count == 0)
                return Array.Empty<MxFairyGuiManifestResource>();

            var resources = new MxFairyGuiManifestResource[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                string packageId = string.IsNullOrWhiteSpace(source[i].PackageId)
                    ? spec.PackageId
                    : source[i].PackageId;
                resources[i] = new MxFairyGuiManifestResource(
                    new ResourceKey(source[i].ResourceId, source[i].ResourceTypeId, packageId: packageId),
                    source[i].Kind,
                    publishedPath: source[i].PublishedPath);
            }

            return resources;
        }

        private static ResourceKey[] CreateViewRequiredResources(
            ResourceKey packageBytes,
            IReadOnlyList<MxFairyGuiManifestResource> packageResources)
        {
            int resourceCount = packageResources != null ? packageResources.Count : 0;
            var resources = new ResourceKey[resourceCount + 1];
            resources[0] = packageBytes;
            for (int i = 0; i < resourceCount; i++)
                resources[i + 1] = packageResources[i].Key;

            return resources;
        }

        private static void AppendViewRequiredResources(
            StringBuilder builder,
            int packageResourceCount)
        {
            if (packageResourceCount == 0)
            {
                builder.AppendLine("                        RequiredResources = new[] { packageBytes },");
                return;
            }

            builder.AppendLine("                        RequiredResources = new[]");
            builder.AppendLine("                        {");
            builder.AppendLine("                            packageBytes,");
            for (int i = 0; i < packageResourceCount; i++)
            {
                builder.Append("                            packageResource");
                builder.Append(i);
                builder.AppendLine(i == packageResourceCount - 1 ? string.Empty : ",");
            }

            builder.AppendLine("                        },");
        }

        private static string PackageResourceExpression(
            MxFairyGuiManifestGenerationSpec spec,
            int index)
        {
            IReadOnlyList<MxFairyGuiPackageResourceGenerationSpec> source =
                spec.AdditionalPackageResources ?? Array.Empty<MxFairyGuiPackageResourceGenerationSpec>();
            return index >= 0 && index < source.Count ? source[index].ResourceKeyExpression : string.Empty;
        }

        private static string FindFocusRepeatedCountExpression(MxFairyGuiBinderGenerationSpec spec)
        {
            IReadOnlyList<MxFairyGuiRepeatedBindingSpec> repeatedBindings =
                spec.RepeatedBindings ?? Array.Empty<MxFairyGuiRepeatedBindingSpec>();
            for (int i = 0; i < repeatedBindings.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(repeatedBindings[i].CountExpression))
                    return repeatedBindings[i].CountExpression;
            }

            return "0";
        }

        private static string ResolveLocalizationExpression(string expression)
        {
            return (expression ?? string.Empty)
                .Replace("{provider}", "_textProvider")
                .Replace("{key}", "key")
                .Replace("{fallback}", "fallback");
        }

        private static string CodeOrDefault(string expression, string fallback)
        {
            return string.IsNullOrWhiteSpace(expression) ? fallback : expression;
        }

        private static void AppendAdditionalUsings(StringBuilder builder, IReadOnlyList<string> namespaces)
        {
            if (namespaces == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal)
            {
                "MxFramework.Resources",
                "MxFramework.UI",
                "MxFramework.UI.FairyGui"
            };

            for (int i = 0; i < namespaces.Count; i++)
            {
                string value = namespaces[i] ?? string.Empty;
                value = value.Trim();
                if (value.Length == 0 || !seen.Add(value))
                    continue;

                builder.Append("using ");
                builder.Append(value);
                builder.AppendLine(";");
            }
        }

        private static MxFairyGuiLocalizedTextBinding[] CopyLocalizedTexts(IReadOnlyList<MxFairyGuiLocalizedTextBinding> localizedTexts)
        {
            if (localizedTexts == null || localizedTexts.Count == 0)
                return Array.Empty<MxFairyGuiLocalizedTextBinding>();

            var copy = new MxFairyGuiLocalizedTextBinding[localizedTexts.Count];
            for (int i = 0; i < localizedTexts.Count; i++)
                copy[i] = localizedTexts[i];

            return copy;
        }

        private static void AppendFeedbackPolicyExpression(
            StringBuilder builder,
            MxUiFeedbackPolicy policy)
        {
            builder.Append("new MxUiFeedbackPolicy(MxUiFeedbackKind.");
            builder.Append(policy.Activation);
            builder.Append(", MxUiFeedbackKind.");
            builder.Append(policy.Success);
            builder.Append(", MxUiFeedbackKind.");
            builder.Append(policy.Failure);
            builder.Append(")");
        }

        private static MxFairyGuiCommandBinding[] CopyCommands(IReadOnlyList<MxFairyGuiCommandBinding> commands)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<MxFairyGuiCommandBinding>();

            var copy = new MxFairyGuiCommandBinding[commands.Count];
            for (int i = 0; i < commands.Count; i++)
                copy[i] = commands[i];

            return copy;
        }

        private static string InferControlKind(XElement element)
        {
            string localName = element.Name.LocalName;
            if (string.Equals(localName, "text", StringComparison.OrdinalIgnoreCase))
                return "Text";

            if (string.Equals(localName, "component", StringComparison.OrdinalIgnoreCase) && element.Element("Button") != null)
                return "Button";

            return localName;
        }

        private static string FindExpression(IReadOnlyList<MxFairyGuiCodeExpressionSpec> expressions, string value)
        {
            if (expressions == null)
                return string.Empty;

            for (int i = 0; i < expressions.Count; i++)
            {
                if (string.Equals(expressions[i].Value, value, StringComparison.Ordinal))
                    return expressions[i].Expression;
            }

            return string.Empty;
        }

        private static string ExpressionOrLiteral(string expression, string literal)
        {
            return string.IsNullOrWhiteSpace(expression) ? Literal(literal) : expression;
        }

        private static string ExpressionOrLiteral(string expression, ResourceKey key)
        {
            if (!string.IsNullOrWhiteSpace(expression))
                return expression;

            return "new ResourceKey(" + Literal(key.Id) + ", " + Literal(key.TypeId) + ", packageId: " + Literal(key.PackageId) + ")";
        }

        private static string ExpressionOrNewViewId(string expression, MxUiViewId viewId)
        {
            return string.IsNullOrWhiteSpace(expression)
                ? "new MxUiViewId(" + Literal(viewId.Value) + ")"
                : expression;
        }

        private static string Literal(string value)
        {
            if (value == null)
                return "null";

            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string ResolvePath(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
                return path ?? string.Empty;

            return Path.Combine(root ?? string.Empty, path);
        }

        private static string ReadAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            return attribute != null ? attribute.Value : string.Empty;
        }

        private static string NormalizeNewLines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n");
        }

        private readonly struct MxFairyGuiGeneratedControl
        {
            public MxFairyGuiGeneratedControl(string name, string kind)
            {
                Name = name ?? string.Empty;
                Kind = kind ?? string.Empty;
            }

            public string Name { get; }
            public string Kind { get; }
        }
    }
}
