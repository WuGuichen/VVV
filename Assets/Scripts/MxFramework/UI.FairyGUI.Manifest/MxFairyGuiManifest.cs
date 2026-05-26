using System;
using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    public static class MxFairyGuiManifestResourceTypeIds
    {
        public const string PackageBytes = "MxFairyGuiPackageBytes";
    }

    public sealed class MxFairyGuiManifest
    {
        public const string CurrentSchemaVersion = "mx.fairygui.manifest.v1";

        public MxFairyGuiManifest()
        {
            SchemaVersion = CurrentSchemaVersion;
            Packages = Array.Empty<MxFairyGuiManifestPackage>();
            Views = Array.Empty<MxFairyGuiManifestView>();
        }

        public string SchemaVersion { get; set; }
        public IReadOnlyList<MxFairyGuiManifestPackage> Packages { get; set; }
        public IReadOnlyList<MxFairyGuiManifestView> Views { get; set; }
    }

    public sealed class MxFairyGuiManifestPackage
    {
        public MxFairyGuiManifestPackage()
        {
            PackageId = string.Empty;
            PackageName = string.Empty;
            PackageSourcePath = string.Empty;
            PackageBytesPath = string.Empty;
            PackageBytes = default;
            RequiredResources = Array.Empty<MxFairyGuiManifestResource>();
        }

        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public string PackageSourcePath { get; set; }
        public string PackageBytesPath { get; set; }
        public ResourceKey PackageBytes { get; set; }
        public IReadOnlyList<MxFairyGuiManifestResource> RequiredResources { get; set; }
    }

    public sealed class MxFairyGuiManifestView
    {
        public MxFairyGuiManifestView()
        {
            ViewId = default;
            PackageId = string.Empty;
            ComponentName = string.Empty;
            ComponentSourcePath = string.Empty;
            ViewModelType = string.Empty;
            Layer = MxUiLayer.Panel;
            RequiredResources = Array.Empty<ResourceKey>();
            NamedControls = Array.Empty<MxFairyGuiNamedControl>();
            LocalizedTexts = Array.Empty<MxFairyGuiLocalizedTextBinding>();
            Commands = Array.Empty<MxFairyGuiCommandBinding>();
        }

        public MxUiViewId ViewId { get; set; }
        public string PackageId { get; set; }
        public string ComponentName { get; set; }
        public string ComponentSourcePath { get; set; }
        public string ViewModelType { get; set; }
        public MxUiLayer Layer { get; set; }
        public IReadOnlyList<ResourceKey> RequiredResources { get; set; }
        public IReadOnlyList<MxFairyGuiNamedControl> NamedControls { get; set; }
        public IReadOnlyList<MxFairyGuiLocalizedTextBinding> LocalizedTexts { get; set; }
        public IReadOnlyList<MxFairyGuiCommandBinding> Commands { get; set; }
    }

    public readonly struct MxFairyGuiManifestResource
    {
        public MxFairyGuiManifestResource(
            ResourceKey key,
            MxFairyGuiManifestResourceKind kind,
            bool required = true,
            string publishedPath = "")
        {
            Key = key;
            Kind = kind;
            Required = required;
            PublishedPath = publishedPath ?? string.Empty;
        }

        public ResourceKey Key { get; }
        public MxFairyGuiManifestResourceKind Kind { get; }
        public bool Required { get; }
        public string PublishedPath { get; }
    }

    public enum MxFairyGuiManifestResourceKind
    {
        Resource = 0,
        PackageBytes = 1,
        Atlas = 2,
        Audio = 3,
        Font = 4
    }

    public readonly struct MxFairyGuiNamedControl
    {
        public MxFairyGuiNamedControl(string name, string controlType, bool required = true, string bindPath = "")
        {
            Name = name ?? string.Empty;
            ControlType = controlType ?? string.Empty;
            Required = required;
            BindPath = bindPath ?? string.Empty;
        }

        public string Name { get; }
        public string ControlType { get; }
        public bool Required { get; }
        public string BindPath { get; }
    }

    public readonly struct MxFairyGuiLocalizedTextBinding
    {
        public MxFairyGuiLocalizedTextBinding(
            string controlName,
            string textKey,
            string fallbackText = "",
            bool required = true)
        {
            ControlName = controlName ?? string.Empty;
            TextKey = textKey ?? string.Empty;
            FallbackText = fallbackText ?? string.Empty;
            Required = required;
        }

        public string ControlName { get; }
        public string TextKey { get; }
        public string FallbackText { get; }
        public bool Required { get; }

        public MxUiLocalizedTextRequest ToRequest()
        {
            return new MxUiLocalizedTextRequest(new MxUiTextKey(TextKey), FallbackText);
        }
    }

    public readonly struct MxFairyGuiCommandBinding
    {
        public MxFairyGuiCommandBinding(string commandId, string controlName, string payloadType = "", bool requiresConfirmation = false)
        {
            CommandId = commandId ?? string.Empty;
            ControlName = controlName ?? string.Empty;
            PayloadType = payloadType ?? string.Empty;
            RequiresConfirmation = requiresConfirmation;
        }

        public string CommandId { get; }
        public string ControlName { get; }
        public string PayloadType { get; }
        public bool RequiresConfirmation { get; }
    }

    public static class MxFairyGuiManifestProjection
    {
        public static IReadOnlyList<MxUiViewContract> CreateViewContracts(MxFairyGuiManifest manifest)
        {
            if (manifest == null || manifest.Views == null)
                return Array.Empty<MxUiViewContract>();

            var contracts = new List<MxUiViewContract>();
            for (int i = 0; i < manifest.Views.Count; i++)
            {
                MxFairyGuiManifestView view = manifest.Views[i];
                if (view == null)
                    continue;

                var contract = new MxUiViewContract(new MxUiViewDescriptor(
                    view.ViewId,
                    view.PackageId,
                    view.ComponentName,
                    view.Layer))
                {
                    ViewModelType = view.ViewModelType ?? string.Empty,
                    RequiredResources = ToResourceIds(view.RequiredResources),
                    Commands = ToCommandDescriptors(view.Commands),
                    DiagnosticsTags = new[] { "fairygui", "generated-manifest" }
                };
                contracts.Add(contract);
            }

            return contracts;
        }

        private static string[] ToResourceIds(IReadOnlyList<ResourceKey> keys)
        {
            if (keys == null || keys.Count == 0)
                return Array.Empty<string>();

            var values = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                values[i] = keys[i].Id ?? string.Empty;

            return values;
        }

        private static MxUiCommandDescriptor[] ToCommandDescriptors(IReadOnlyList<MxFairyGuiCommandBinding> commands)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<MxUiCommandDescriptor>();

            var values = new MxUiCommandDescriptor[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                values[i] = new MxUiCommandDescriptor
                {
                    CommandId = commands[i].CommandId,
                    Owner = "FairyGUI"
                };
            }

            return values;
        }
    }
}
