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
                        PackageBytes = packageBytes
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
                        RequiredResources = new[] { packageBytes },
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
                    "Generated FairyGUI manifest output is stale. Regenerate it with the documented noEngine tool command."));
            }

            return new MxFairyGuiManifestValidationResult(diagnostics);
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
            builder.AppendLine();
            builder.AppendLine("            return new MxFairyGuiManifest");
            builder.AppendLine("            {");
            builder.AppendLine("                Packages = new[]");
            builder.AppendLine("                {");
            MxFairyGuiManifestPackage package = manifest.Packages[0];
            builder.AppendLine("                    new MxFairyGuiManifestPackage");
            builder.AppendLine("                    {");
            string packageIdExpression = ExpressionOrLiteral(spec.PackageIdExpression, package.PackageId);
            builder.AppendLine("                        PackageId = " + packageIdExpression + ",");
            builder.AppendLine("                        PackageName = " + ExpressionOrLiteral(spec.PackageNameExpression, package.PackageName) + ",");
            builder.AppendLine("                        PackageSourcePath = " + Literal(package.PackageSourcePath) + ",");
            builder.AppendLine("                        PackageBytesPath = " + Literal(package.PackageBytesPath) + ",");
            builder.AppendLine("                        PackageBytes = packageBytes");
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
            builder.AppendLine("                        RequiredResources = new[] { packageBytes },");
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
            builder.AppendLine("                        Commands = new[]");
            builder.AppendLine("                        {");
            for (int i = 0; i < view.Commands.Count; i++)
            {
                MxFairyGuiCommandBinding command = view.Commands[i];
                builder.Append("                            new MxFairyGuiCommandBinding(");
                builder.Append(ExpressionOrLiteral(FindExpression(spec.CommandIdExpressions, command.CommandId), command.CommandId));
                builder.Append(", ");
                builder.Append(ExpressionOrLiteral(FindExpression(spec.ControlNameExpressions, command.ControlName), command.ControlName));
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
            builder.AppendLine("            return new MxFairyGuiPackageDescriptor(package.PackageId, package.PackageBytes);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
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
