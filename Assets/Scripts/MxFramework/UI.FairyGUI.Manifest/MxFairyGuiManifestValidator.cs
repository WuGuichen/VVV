using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Linq;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public static class MxFairyGuiManifestValidator
    {
        public static MxFairyGuiManifestValidationResult Validate(MxFairyGuiManifest manifest)
        {
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>();
            if (manifest == null)
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ManifestMissing, MxFairyGuiManifestDiagnosticTarget.Manifest, string.Empty, string.Empty, string.Empty, "FairyGUI manifest is required.");
                return new MxFairyGuiManifestValidationResult(diagnostics);
            }

            IReadOnlyList<MxFairyGuiManifestPackage> packages = manifest.Packages ?? Array.Empty<MxFairyGuiManifestPackage>();
            IReadOnlyList<MxFairyGuiManifestView> views = manifest.Views ?? Array.Empty<MxFairyGuiManifestView>();
            ValidateSchemaVersion(manifest, diagnostics);
            var packageIds = new HashSet<string>(StringComparer.Ordinal);
            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            ValidatePackages(packages, packageIds, packageNames, diagnostics);
            ValidateViews(views, packageIds, diagnostics);
            return new MxFairyGuiManifestValidationResult(diagnostics);
        }

        public static MxFairyGuiManifestValidationResult ValidateSources(
            MxFairyGuiManifest manifest,
            string projectRootPath,
            ResourceCatalog catalog = null)
        {
            var diagnostics = new List<MxFairyGuiManifestDiagnostic>(Validate(manifest).Diagnostics);
            if (manifest == null)
                return new MxFairyGuiManifestValidationResult(diagnostics);

            string root = projectRootPath ?? string.Empty;
            IReadOnlyList<MxFairyGuiManifestPackage> packages = manifest.Packages ?? Array.Empty<MxFairyGuiManifestPackage>();
            IReadOnlyList<MxFairyGuiManifestView> views = manifest.Views ?? Array.Empty<MxFairyGuiManifestView>();
            var packagesById = new Dictionary<string, MxFairyGuiManifestPackage>(StringComparer.Ordinal);
            var exportedComponentsByPackageId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            for (int i = 0; i < packages.Count; i++)
            {
                MxFairyGuiManifestPackage package = packages[i];
                if (package == null)
                    continue;

                packagesById[package.PackageId ?? string.Empty] = package;
                ValidatePackageSource(package, root, catalog, exportedComponentsByPackageId, diagnostics);
            }

            for (int i = 0; i < views.Count; i++)
            {
                MxFairyGuiManifestView view = views[i];
                if (view == null)
                    continue;

                MxFairyGuiManifestPackage package;
                packagesById.TryGetValue(view.PackageId ?? string.Empty, out package);
                ValidateViewSource(view, package, root, exportedComponentsByPackageId, diagnostics);
            }

            return new MxFairyGuiManifestValidationResult(diagnostics);
        }

        private static void ValidateSchemaVersion(
            MxFairyGuiManifest manifest,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            string schemaVersion = manifest.SchemaVersion ?? string.Empty;
            if (string.IsNullOrWhiteSpace(schemaVersion))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ManifestSchemaVersionMissing, MxFairyGuiManifestDiagnosticTarget.Manifest, string.Empty, string.Empty, "schemaVersion", "FairyGUI manifest schema version is required.");
                return;
            }

            if (!string.Equals(schemaVersion, MxFairyGuiManifest.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ManifestSchemaVersionUnsupported, MxFairyGuiManifestDiagnosticTarget.Manifest, string.Empty, string.Empty, "schemaVersion", "Unsupported FairyGUI manifest schema version: " + schemaVersion + ".");
            }
        }

        private static void ValidatePackages(
            IReadOnlyList<MxFairyGuiManifestPackage> packages,
            HashSet<string> packageIds,
            HashSet<string> packageNames,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                MxFairyGuiManifestPackage package = packages[i];
                string sourcePath = "packages[" + i + "]";
                if (package == null)
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageMissing, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, string.Empty, string.Empty, "FairyGUI package entry is required.");
                    continue;
                }

                string packageId = package.PackageId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(packageId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageIdMissing, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, string.Empty, "packageId", "FairyGUI package id is required.");
                else if (!packageIds.Add(packageId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageIdDuplicate, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, packageId, "packageId", "Duplicate FairyGUI package id: " + packageId + ".");

                string packageName = package.PackageName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(packageName))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageNameMissing, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, packageId, "packageName", "FairyGUI package name is required.");
                else if (!packageNames.Add(packageName))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageNameDuplicate, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, packageId, "packageName", "Duplicate FairyGUI package name: " + packageName + ".");

                if (!package.PackageBytes.IsValid)
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceKeyInvalid, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, packageId, "packageBytes", "FairyGUI package bytes resource key is invalid.");

                ValidatePackageResources(package.RequiredResources, sourcePath, packageId, diagnostics);
            }
        }

        private static void ValidatePackageResources(
            IReadOnlyList<MxFairyGuiManifestResource> resources,
            string sourcePath,
            string packageId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (resources == null)
                return;

            var resourceKeys = new HashSet<ResourceKey>();
            for (int i = 0; i < resources.Count; i++)
            {
                MxFairyGuiManifestResource resource = resources[i];
                string field = "requiredResources[" + i + "]";
                if (!resource.Key.IsValid)
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceKeyInvalid, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, field, "FairyGUI package required resource key is invalid.");
                    continue;
                }

                if (!resourceKeys.Add(resource.Key))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceDuplicate, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, field, "Duplicate FairyGUI package resource key: " + resource.Key + ".");
            }
        }

        private static void ValidateViews(
            IReadOnlyList<MxFairyGuiManifestView> views,
            HashSet<string> packageIds,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            var viewIds = new HashSet<MxUiViewId>();
            for (int i = 0; i < views.Count; i++)
            {
                MxFairyGuiManifestView view = views[i];
                string sourcePath = "views[" + i + "]";
                if (view == null)
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewMissing, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, string.Empty, string.Empty, "FairyGUI view entry is required.");
                    continue;
                }

                string viewId = view.ViewId.Value ?? string.Empty;
                if (!view.ViewId.IsValid)
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewIdMissing, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, view.PackageId, "viewId", "FairyGUI view id is required.");
                else if (!viewIds.Add(view.ViewId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewIdDuplicate, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, view.PackageId, "viewId", "Duplicate FairyGUI view id: " + viewId + ".");

                string packageId = view.PackageId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(packageId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewPackageMissing, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, packageId, "packageId", "FairyGUI view package id is required.");
                else if (!packageIds.Contains(packageId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewPackageUnknown, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, packageId, "packageId", "FairyGUI view references an unknown package id: " + packageId + ".");

                if (string.IsNullOrWhiteSpace(view.ComponentName))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ComponentMissing, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, packageId, "componentName", "FairyGUI view component name is required.");

                if (string.IsNullOrWhiteSpace(view.ViewModelType))
                    AddWarning(diagnostics, MxFairyGuiManifestDiagnosticCode.ViewModelTypeMissing, MxFairyGuiManifestDiagnosticTarget.View, sourcePath, packageId, "viewModelType", "FairyGUI view model type is not declared.", viewId);

                ValidateViewResources(view.RequiredResources, sourcePath, packageId, viewId, diagnostics);
                Dictionary<string, string> controlTypes = ValidateNamedControls(view.NamedControls, sourcePath, packageId, viewId, diagnostics);
                ValidateCommands(view.Commands, controlTypes, sourcePath, packageId, viewId, diagnostics);
            }
        }

        private static void ValidatePackageSource(
            MxFairyGuiManifestPackage package,
            string root,
            ResourceCatalog catalog,
            Dictionary<string, HashSet<string>> exportedComponentsByPackageId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            string packageId = package.PackageId ?? string.Empty;
            string sourcePath = package.PackageSourcePath ?? string.Empty;
            string absoluteSourcePath = ResolvePath(root, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(absoluteSourcePath))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageSourceMissing, MxFairyGuiManifestDiagnosticTarget.Package, sourcePath, packageId, "packageSourcePath", "FairyGUI package source XML is missing.");
            }
            else
            {
                TryValidatePackageXml(package, absoluteSourcePath, exportedComponentsByPackageId, diagnostics);
            }

            string packageBytesPath = package.PackageBytesPath ?? string.Empty;
            string absoluteBytesPath = ResolvePath(root, packageBytesPath);
            if (string.IsNullOrWhiteSpace(packageBytesPath) || !File.Exists(absoluteBytesPath))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageBytesMissing, MxFairyGuiManifestDiagnosticTarget.Resource, packageBytesPath, packageId, "packageBytesPath", "FairyGUI package bytes file is missing.");
            }
            else if (!HasFairyGuiHeader(absoluteBytesPath))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageBytesInvalidHeader, MxFairyGuiManifestDiagnosticTarget.Resource, packageBytesPath, packageId, "packageBytesPath", "FairyGUI package bytes must start with FGUI header.");
            }

            ValidateCatalogEntry(package.PackageBytes, packageId, catalog, packageBytesPath, "packageBytes", diagnostics);

            IReadOnlyList<MxFairyGuiManifestResource> resources = package.RequiredResources ?? Array.Empty<MxFairyGuiManifestResource>();
            for (int i = 0; i < resources.Count; i++)
            {
                MxFairyGuiManifestResource resource = resources[i];
                if (!IsSupportedResourceKind(resource.Kind))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceKindUnsupported, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, "requiredResources[" + i + "].kind", "FairyGUI resource kind is unsupported.");
                    continue;
                }

                if (resource.Required)
                    ValidateCatalogEntry(resource.Key, packageId, catalog, sourcePath, "requiredResources[" + i + "]", diagnostics);
            }
        }

        private static void ValidateViewSource(
            MxFairyGuiManifestView view,
            MxFairyGuiManifestPackage package,
            string root,
            Dictionary<string, HashSet<string>> exportedComponentsByPackageId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            string packageId = view.PackageId ?? string.Empty;
            string viewId = view.ViewId.Value ?? string.Empty;
            HashSet<string> exportedComponents;
            if (exportedComponentsByPackageId.TryGetValue(packageId, out exportedComponents)
                && !exportedComponents.Contains(view.ComponentName ?? string.Empty))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ComponentNotExported, MxFairyGuiManifestDiagnosticTarget.View, package != null ? package.PackageSourcePath : string.Empty, packageId, "componentName", "FairyGUI component is not exported by package.xml: " + view.ComponentName + ".", viewId);
            }

            string componentSourcePath = view.ComponentSourcePath ?? string.Empty;
            string absoluteComponentPath = ResolvePath(root, componentSourcePath);
            if (string.IsNullOrWhiteSpace(componentSourcePath) || !File.Exists(absoluteComponentPath))
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ComponentSourceMissing, MxFairyGuiManifestDiagnosticTarget.View, componentSourcePath, packageId, "componentSourcePath", "FairyGUI component source XML is missing.", viewId);
                return;
            }

            Dictionary<string, string> controlKinds;
            if (!TryReadComponentControls(absoluteComponentPath, out controlKinds, diagnostics, packageId, viewId))
                return;

            IReadOnlyList<MxFairyGuiNamedControl> controls = view.NamedControls ?? Array.Empty<MxFairyGuiNamedControl>();
            for (int i = 0; i < controls.Count; i++)
            {
                MxFairyGuiNamedControl control = controls[i];
                if (!control.Required)
                    continue;

                string actualKind;
                if (!controlKinds.TryGetValue(control.Name ?? string.Empty, out actualKind))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ControlMissing, MxFairyGuiManifestDiagnosticTarget.Control, componentSourcePath, packageId, "namedControls[" + i + "].name", "Required FairyGUI control is missing: " + control.Name + ".", viewId);
                    continue;
                }

                if (!ControlKindMatches(control.ControlType, actualKind))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ControlKindMismatch, MxFairyGuiManifestDiagnosticTarget.Control, componentSourcePath, packageId, "namedControls[" + i + "].controlType", "FairyGUI control kind mismatch for " + control.Name + ": expected " + control.ControlType + ", actual " + actualKind + ".", viewId);
            }

            ValidateCommandControlSources(view.Commands, controlKinds, componentSourcePath, packageId, viewId, diagnostics);
        }

        private static void ValidateViewResources(
            IReadOnlyList<ResourceKey> resources,
            string sourcePath,
            string packageId,
            string viewId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (resources == null)
                return;

            var resourceKeys = new HashSet<ResourceKey>();
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceKey key = resources[i];
                string field = "requiredResources[" + i + "]";
                if (!key.IsValid)
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceKeyInvalid, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, field, "FairyGUI view required resource key is invalid.", viewId);
                    continue;
                }

                if (!resourceKeys.Add(key))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceDuplicate, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, field, "Duplicate FairyGUI view resource key: " + key + ".", viewId);
            }
        }

        private static Dictionary<string, string> ValidateNamedControls(
            IReadOnlyList<MxFairyGuiNamedControl> controls,
            string sourcePath,
            string packageId,
            string viewId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            var controlTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            if (controls == null)
                return controlTypes;

            for (int i = 0; i < controls.Count; i++)
            {
                MxFairyGuiNamedControl control = controls[i];
                string field = "namedControls[" + i + "]";
                if (string.IsNullOrWhiteSpace(control.Name))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ControlNameMissing, MxFairyGuiManifestDiagnosticTarget.Control, sourcePath, packageId, field + ".name", "FairyGUI named control name is required.", viewId);
                    continue;
                }

                if (controlTypes.ContainsKey(control.Name))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ControlDuplicate, MxFairyGuiManifestDiagnosticTarget.Control, sourcePath, packageId, field + ".name", "Duplicate FairyGUI named control: " + control.Name + ".", viewId);
                }
                else
                {
                    controlTypes.Add(control.Name, control.ControlType ?? string.Empty);
                }

                if (control.Required && string.IsNullOrWhiteSpace(control.ControlType))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ControlTypeMissing, MxFairyGuiManifestDiagnosticTarget.Control, sourcePath, packageId, field + ".controlType", "Required FairyGUI named control type is required.", viewId);
            }

            return controlTypes;
        }

        private static void ValidateCommands(
            IReadOnlyList<MxFairyGuiCommandBinding> commands,
            Dictionary<string, string> controlTypes,
            string sourcePath,
            string packageId,
            string viewId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (commands == null)
                return;

            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < commands.Count; i++)
            {
                MxFairyGuiCommandBinding command = commands[i];
                string field = "commands[" + i + "]";
                if (string.IsNullOrWhiteSpace(command.CommandId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandIdMissing, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, field + ".commandId", "FairyGUI command id is required.", viewId);
                else if (!commandIds.Add(command.CommandId))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandDuplicate, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, field + ".commandId", "Duplicate FairyGUI command id: " + command.CommandId + ".", viewId);

                if (string.IsNullOrWhiteSpace(command.ControlName))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandControlMissing, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, field + ".controlName", "FairyGUI command control name is required.", viewId);
                else if (!controlTypes.ContainsKey(command.ControlName))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandControlUnknown, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, field + ".controlName", "FairyGUI command references an unknown named control: " + command.ControlName + ".", viewId);
                }
                else if (!IsButtonControlKind(controlTypes[command.ControlName]))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandControlNotButton, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, field + ".controlName", "FairyGUI command control must be a button: " + command.ControlName + ".", viewId);
                }
            }
        }

        private static void ValidateCommandControlSources(
            IReadOnlyList<MxFairyGuiCommandBinding> commands,
            Dictionary<string, string> actualControlKinds,
            string sourcePath,
            string packageId,
            string viewId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (commands == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                MxFairyGuiCommandBinding command = commands[i];
                if (string.IsNullOrWhiteSpace(command.ControlName))
                    continue;

                string actualKind;
                if (!actualControlKinds.TryGetValue(command.ControlName, out actualKind))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandControlUnknown, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, "commands[" + i + "].controlName", "FairyGUI command control is missing from component source XML: " + command.ControlName + ".", viewId);
                    continue;
                }

                if (!IsButtonControlKind(actualKind))
                {
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.CommandControlNotButton, MxFairyGuiManifestDiagnosticTarget.Command, sourcePath, packageId, "commands[" + i + "].controlName", "FairyGUI command control source must be a button: " + command.ControlName + ".", viewId);
                }
            }
        }

        private static bool TryValidatePackageXml(
            MxFairyGuiManifestPackage package,
            string absoluteSourcePath,
            Dictionary<string, HashSet<string>> exportedComponentsByPackageId,
            List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            try
            {
                XDocument document = XDocument.Load(absoluteSourcePath);
                string packageId = package.PackageId ?? string.Empty;
                string folderName = new DirectoryInfo(Path.GetDirectoryName(absoluteSourcePath) ?? string.Empty).Name;
                if (!string.IsNullOrWhiteSpace(package.PackageName) && !string.Equals(folderName, package.PackageName, StringComparison.Ordinal))
                    AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageNameMismatch, MxFairyGuiManifestDiagnosticTarget.Package, package.PackageSourcePath, packageId, "packageName", "FairyGUI package source folder does not match package name.");

                var components = new HashSet<string>(StringComparer.Ordinal);
                foreach (XElement element in document.Descendants("component"))
                {
                    if (!string.Equals(ReadAttribute(element, "exported"), "true", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = ReadAttribute(element, "name");
                    if (name.EndsWith(".xml", StringComparison.Ordinal))
                        name = name.Substring(0, name.Length - 4);

                    if (!string.IsNullOrWhiteSpace(name))
                        components.Add(name);
                }

                exportedComponentsByPackageId[packageId] = components;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.PackageSourceInvalid, MxFairyGuiManifestDiagnosticTarget.Package, package.PackageSourcePath, package.PackageId, "packageSourcePath", "FairyGUI package source XML could not be read: " + ex.Message);
                return false;
            }
        }

        private static bool TryReadComponentControls(
            string absoluteComponentPath,
            out Dictionary<string, string> controls,
            List<MxFairyGuiManifestDiagnostic> diagnostics,
            string packageId,
            string viewId)
        {
            controls = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                XDocument document = XDocument.Load(absoluteComponentPath);
                foreach (XElement element in document.Descendants())
                {
                    string name = ReadAttribute(element, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                        controls[name] = InferControlKind(element);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            {
                AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ComponentSourceInvalid, MxFairyGuiManifestDiagnosticTarget.View, absoluteComponentPath, packageId, "componentSourcePath", "FairyGUI component source XML could not be read: " + ex.Message, viewId);
                return false;
            }
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

        private static bool ControlKindMatches(string expected, string actual)
        {
            expected = expected ?? string.Empty;
            actual = actual ?? string.Empty;
            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(expected, "GTextField", StringComparison.OrdinalIgnoreCase))
                return string.Equals(actual, "Text", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(expected, "GButton", StringComparison.OrdinalIgnoreCase))
                return string.Equals(actual, "Button", StringComparison.OrdinalIgnoreCase);

            return false;
        }

        private static bool IsButtonControlKind(string controlType)
        {
            return string.Equals(controlType, "Button", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controlType, "GButton", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFairyGuiHeader(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length < 4)
                    return false;

                return stream.ReadByte() == 'F'
                    && stream.ReadByte() == 'G'
                    && stream.ReadByte() == 'U'
                    && stream.ReadByte() == 'I';
            }
        }

        private static void ValidateCatalogEntry(ResourceKey key, string packageId, ResourceCatalog catalog, string sourcePath, string field, List<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            if (!key.IsValid || catalog == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                if (catalog.Entries[i].CreateKey(catalog.PackageId) == key)
                    return;
            }

            AddError(diagnostics, MxFairyGuiManifestDiagnosticCode.ResourceCatalogEntryMissing, MxFairyGuiManifestDiagnosticTarget.Resource, sourcePath, packageId, field, "FairyGUI resource key is missing from ResourceCatalog: " + key + ".");
        }

        private static bool IsSupportedResourceKind(MxFairyGuiManifestResourceKind kind)
        {
            return kind == MxFairyGuiManifestResourceKind.Resource
                || kind == MxFairyGuiManifestResourceKind.PackageBytes
                || kind == MxFairyGuiManifestResourceKind.Atlas
                || kind == MxFairyGuiManifestResourceKind.Audio
                || kind == MxFairyGuiManifestResourceKind.Font;
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

        private static void AddError(List<MxFairyGuiManifestDiagnostic> diagnostics, MxFairyGuiManifestDiagnosticCode code, MxFairyGuiManifestDiagnosticTarget target, string sourcePath, string packageId, string field, string message, string viewId = "")
        {
            diagnostics.Add(MxFairyGuiManifestDiagnostic.Error(code, target, sourcePath, packageId, viewId, field, message));
        }

        private static void AddWarning(List<MxFairyGuiManifestDiagnostic> diagnostics, MxFairyGuiManifestDiagnosticCode code, MxFairyGuiManifestDiagnosticTarget target, string sourcePath, string packageId, string field, string message, string viewId = "")
        {
            diagnostics.Add(MxFairyGuiManifestDiagnostic.Warning(code, target, sourcePath, packageId, viewId, field, message));
        }
    }

    public sealed class MxFairyGuiManifestValidationResult
    {
        public MxFairyGuiManifestValidationResult(IReadOnlyList<MxFairyGuiManifestDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics == null
                ? Array.Empty<MxFairyGuiManifestDiagnostic>()
                : new ReadOnlyCollection<MxFairyGuiManifestDiagnostic>(new List<MxFairyGuiManifestDiagnostic>(diagnostics));
        }

        public IReadOnlyList<MxFairyGuiManifestDiagnostic> Diagnostics { get; }
        public bool Success => ErrorCount == 0;
        public int ErrorCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Error);
        public int WarningCount => CountSeverity(MxFairyGuiManifestDiagnosticSeverity.Warning);

        public bool HasCode(MxFairyGuiManifestDiagnosticCode code)
        {
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }

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

    public readonly struct MxFairyGuiManifestDiagnostic
    {
        public MxFairyGuiManifestDiagnostic(
            MxFairyGuiManifestDiagnosticSeverity severity,
            MxFairyGuiManifestDiagnosticCode code,
            MxFairyGuiManifestDiagnosticTarget target,
            string sourcePath,
            string packageId,
            string viewId,
            string field,
            string message,
            string suggestedFix = "")
        {
            Severity = severity;
            Code = code;
            Target = target;
            SourcePath = sourcePath ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            ViewId = viewId ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
            SuggestedFix = suggestedFix ?? string.Empty;
        }

        public MxFairyGuiManifestDiagnosticSeverity Severity { get; }
        public MxFairyGuiManifestDiagnosticCode Code { get; }
        public MxFairyGuiManifestDiagnosticTarget Target { get; }
        public string SourcePath { get; }
        public string PackageId { get; }
        public string ViewId { get; }
        public string Field { get; }
        public string Message { get; }
        public string SuggestedFix { get; }

        public static MxFairyGuiManifestDiagnostic Error(MxFairyGuiManifestDiagnosticCode code, MxFairyGuiManifestDiagnosticTarget target, string sourcePath, string packageId, string viewId, string field, string message)
        {
            return new MxFairyGuiManifestDiagnostic(MxFairyGuiManifestDiagnosticSeverity.Error, code, target, sourcePath, packageId, viewId, field, message);
        }

        public static MxFairyGuiManifestDiagnostic Warning(MxFairyGuiManifestDiagnosticCode code, MxFairyGuiManifestDiagnosticTarget target, string sourcePath, string packageId, string viewId, string field, string message)
        {
            return new MxFairyGuiManifestDiagnostic(MxFairyGuiManifestDiagnosticSeverity.Warning, code, target, sourcePath, packageId, viewId, field, message);
        }
    }

    public enum MxFairyGuiManifestDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum MxFairyGuiManifestDiagnosticTarget
    {
        Manifest = 0,
        Package = 1,
        View = 2,
        Resource = 3,
        Control = 4,
        Command = 5
    }

        public enum MxFairyGuiManifestDiagnosticCode
        {
        ManifestMissing = 0,
        PackageMissing = 1,
        PackageIdMissing = 2,
        PackageIdDuplicate = 3,
        PackageNameMissing = 4,
        PackageNameDuplicate = 5,
        ResourceKeyInvalid = 6,
        ResourceDuplicate = 7,
        ViewMissing = 8,
        ViewIdMissing = 9,
        ViewIdDuplicate = 10,
        ViewPackageMissing = 11,
        ViewPackageUnknown = 12,
        ComponentMissing = 13,
        ControlNameMissing = 14,
        ControlTypeMissing = 15,
        ControlDuplicate = 16,
        CommandIdMissing = 17,
        CommandDuplicate = 18,
        CommandControlMissing = 19,
        CommandControlUnknown = 20,
        ViewModelTypeMissing = 21,
        PackageSourceMissing = 22,
        PackageSourceInvalid = 23,
        PackageNameMismatch = 24,
        PackageBytesMissing = 25,
        PackageBytesInvalidHeader = 26,
        ResourceCatalogEntryMissing = 27,
        ResourceKindUnsupported = 28,
        ComponentNotExported = 29,
        ComponentSourceMissing = 30,
        ComponentSourceInvalid = 31,
        ControlMissing = 32,
        ControlKindMismatch = 33,
        ManifestSchemaVersionMissing = 34,
        ManifestSchemaVersionUnsupported = 35,
        CommandControlNotButton = 36
    }
}
