using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace MxFramework.Editor
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryGenerationTemplateCatalogDocument
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonProperty("catalogId")] public string CatalogId { get; set; } = string.Empty;
        [JsonProperty("catalogVersion")] public string CatalogVersion { get; set; } = string.Empty;
        [JsonProperty("providers")] public List<VfxLibraryGenerationProviderOption> Providers { get; set; } = new List<VfxLibraryGenerationProviderOption>();
        [JsonProperty("styleProfiles")] public List<VfxLibraryStyleProfileOption> StyleProfiles { get; set; } = new List<VfxLibraryStyleProfileOption>();
        [JsonProperty("templates")] public List<VfxLibraryGenerationTemplateOption> Templates { get; set; } = new List<VfxLibraryGenerationTemplateOption>();
        [JsonProperty("qualityProfiles")] public List<VfxLibraryQualityProfileOption> QualityProfiles { get; set; } = new List<VfxLibraryQualityProfileOption>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryGenerationProviderOption
    {
        [JsonProperty("providerId")] public string ProviderId { get; set; } = string.Empty;
        [JsonProperty("providerVersion")] public string ProviderVersion { get; set; } = string.Empty;
        [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
        [JsonProperty("enabledByDefault")] public bool EnabledByDefault { get; set; }
        [JsonProperty("allowsCloud")] public bool AllowsCloud { get; set; }
        [JsonProperty("modelId")] public string ModelId { get; set; } = string.Empty;
        [JsonProperty("modelVersion")] public string ModelVersion { get; set; } = string.Empty;
        [JsonProperty("allowedLicenseIds")] public string[] AllowedLicenseIds { get; set; } = Array.Empty<string>();

        [JsonIgnore] public string DisplayName { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryStyleProfileOption
    {
        [JsonProperty("styleProfileId")] public string StyleProfileId { get; set; } = string.Empty;
        [JsonProperty("version")] public string Version { get; set; } = string.Empty;
        [JsonProperty("themeIds")] public string[] ThemeIds { get; set; } = Array.Empty<string>();
        [JsonProperty("keywords")] public string[] Keywords { get; set; } = Array.Empty<string>();

        [JsonIgnore] public string DisplayName { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryGenerationTemplateOption
    {
        [JsonProperty("templateId")] public string TemplateId { get; set; } = string.Empty;
        [JsonProperty("templateVersion")] public string TemplateVersion { get; set; } = string.Empty;
        [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
        [JsonProperty("backend")] public string Backend { get; set; } = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath { get; set; } = string.Empty;
        [JsonProperty("certified")] public bool Certified { get; set; }
        [JsonProperty("arbitraryTopology")] public bool ArbitraryTopology { get; set; }
        [JsonProperty("experimentalAdapterId")] public string ExperimentalAdapterId { get; set; } = string.Empty;
        [JsonProperty("experimentalAdapterVersion")] public string ExperimentalAdapterVersion { get; set; } = string.Empty;
        [JsonProperty("allowedProviderIds")] public string[] AllowedProviderIds { get; set; } = Array.Empty<string>();

        [JsonIgnore] public string DisplayName { get; set; } = string.Empty;

        public bool AllowsProvider(string providerId)
        {
            for (int i = 0; i < AllowedProviderIds.Length; i++)
                if (string.Equals(AllowedProviderIds[i], providerId, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxLibraryQualityProfileOption
    {
        [JsonProperty("qualityProfileId")] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("version")] public string Version { get; set; } = string.Empty;
        [JsonProperty("platform")] public string Platform { get; set; } = string.Empty;
        [JsonProperty("graphicsApi")] public string GraphicsApi { get; set; } = string.Empty;
        [JsonProperty("width")] public int Width { get; set; }
        [JsonProperty("height")] public int Height { get; set; }
        [JsonProperty("framesPerSecond")] public int FramesPerSecond { get; set; }
        [JsonProperty("fixedStepSeconds")] public double FixedStepSeconds { get; set; }
        [JsonProperty("fixedSeeds")] public int[] FixedSeeds { get; set; } = Array.Empty<int>();
        [JsonProperty("oneShotSlicePercents")] public int[] OneShotSlicePercents { get; set; } = Array.Empty<int>();
        [JsonProperty("ghostCycleCount")] public int GhostCycleCount { get; set; }
        [JsonProperty("maximumCropPercent")] public double MaximumCropPercent { get; set; }
        [JsonProperty("maximumAnchorErrorPercent")] public double MaximumAnchorErrorPercent { get; set; }
        [JsonProperty("minimumTelegraphFootprintIou")] public double MinimumTelegraphFootprintIou { get; set; }
        [JsonProperty("maximumSteadyStateGcBytesPerFrame")] public int MaximumSteadyStateGcBytesPerFrame { get; set; }

        [JsonIgnore] public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>Read-only Editor projection of the versioned VFX generation template catalog.</summary>
    /// <remarks>Editor-only, AllocByDesign. Authoritative validation remains in Authoring Core.</remarks>
    internal sealed class VfxLibraryGenerationTemplateCatalog
    {
        internal const string DefaultPath = "Assets/Config/MxFramework/Vfx/vfx_generation_templates.json";

        public bool IsLoaded { get; private set; }
        public string Error { get; private set; } = string.Empty;
        public string SchemaVersion { get; private set; } = string.Empty;
        public string CatalogId { get; private set; } = string.Empty;
        public string CatalogVersion { get; private set; } = string.Empty;
        public string ContentHash { get; private set; } = string.Empty;
        public IReadOnlyList<VfxLibraryGenerationProviderOption> Providers => _providers;
        public IReadOnlyList<VfxLibraryStyleProfileOption> StyleProfiles => _styleProfiles;
        public IReadOnlyList<VfxLibraryGenerationTemplateOption> Templates => _templates;
        public IReadOnlyList<VfxLibraryQualityProfileOption> QualityProfiles => _qualityProfiles;

        private readonly List<VfxLibraryGenerationProviderOption> _providers = new List<VfxLibraryGenerationProviderOption>();
        private readonly List<VfxLibraryStyleProfileOption> _styleProfiles = new List<VfxLibraryStyleProfileOption>();
        private readonly List<VfxLibraryGenerationTemplateOption> _templates = new List<VfxLibraryGenerationTemplateOption>();
        private readonly List<VfxLibraryQualityProfileOption> _qualityProfiles = new List<VfxLibraryQualityProfileOption>();

        public static VfxLibraryGenerationTemplateCatalog Load(string path = DefaultPath)
        {
            var result = new VfxLibraryGenerationTemplateCatalog();
            try
            {
                if (!File.Exists(path))
                {
                    result.Error = "生成模板目录不存在：" + path;
                    return result;
                }

                byte[] bytes = File.ReadAllBytes(path);
                VfxLibraryGenerationTemplateCatalogDocument document = JsonConvert.DeserializeObject<VfxLibraryGenerationTemplateCatalogDocument>(System.Text.Encoding.UTF8.GetString(bytes));
                if (document == null || string.IsNullOrWhiteSpace(document.SchemaVersion) || string.IsNullOrWhiteSpace(document.CatalogVersion))
                {
                    result.Error = "生成模板目录缺少 schemaVersion 或 catalogVersion。";
                    return result;
                }

                result.SchemaVersion = document.SchemaVersion;
                result.CatalogId = document.CatalogId;
                result.CatalogVersion = document.CatalogVersion;
                result.ContentHash = Hash(bytes);
                AddValidProviders(result._providers, document.Providers);
                AddValidStyles(result._styleProfiles, document.StyleProfiles);
                AddValidTemplates(result._templates, document.Templates);
                AddValidQualityProfiles(result._qualityProfiles, document.QualityProfiles);
                if (result._providers.Count == 0 || result._styleProfiles.Count == 0 || result._templates.Count == 0 || result._qualityProfiles.Count == 0)
                {
                    result.Error = "生成模板目录缺少可用的提供器、样式、模板或质量档。";
                    return result;
                }
                result.IsLoaded = true;
            }
            catch (Exception exception)
            {
                result.Error = "读取生成模板目录失败：" + exception.Message;
            }
            return result;
        }

        public VfxLibraryGenerationProviderOption FindProviderByDisplayName(string displayName)
        {
            for (int i = 0; i < _providers.Count; i++)
                if (string.Equals(_providers[i].DisplayName, displayName, StringComparison.Ordinal))
                    return _providers[i];
            return null;
        }

        public VfxLibraryStyleProfileOption FindStyleByDisplayName(string displayName)
        {
            for (int i = 0; i < _styleProfiles.Count; i++)
                if (string.Equals(_styleProfiles[i].DisplayName, displayName, StringComparison.Ordinal))
                    return _styleProfiles[i];
            return null;
        }

        public VfxLibraryGenerationTemplateOption FindTemplateByDisplayName(string displayName)
        {
            for (int i = 0; i < _templates.Count; i++)
                if (string.Equals(_templates[i].DisplayName, displayName, StringComparison.Ordinal))
                    return _templates[i];
            return null;
        }

        public VfxLibraryQualityProfileOption FindQualityByDisplayName(string displayName)
        {
            for (int i = 0; i < _qualityProfiles.Count; i++)
                if (string.Equals(_qualityProfiles[i].DisplayName, displayName, StringComparison.Ordinal))
                    return _qualityProfiles[i];
            return null;
        }

        private static void AddValidProviders(List<VfxLibraryGenerationProviderOption> destination, List<VfxLibraryGenerationProviderOption> source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                VfxLibraryGenerationProviderOption option = source[i];
                if (string.IsNullOrWhiteSpace(option.ProviderId) || string.IsNullOrWhiteSpace(option.ProviderVersion))
                    continue;
                option.DisplayName = ProviderDisplayName(option);
                destination.Add(option);
            }
        }

        private static void AddValidStyles(List<VfxLibraryStyleProfileOption> destination, List<VfxLibraryStyleProfileOption> source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                VfxLibraryStyleProfileOption option = source[i];
                if (string.IsNullOrWhiteSpace(option.StyleProfileId) || string.IsNullOrWhiteSpace(option.Version))
                    continue;
                option.DisplayName = option.StyleProfileId == "mx.vfx.style.fire-readable"
                    ? "火焰可读性基准"
                    : option.StyleProfileId + " · 版本 " + option.Version;
                destination.Add(option);
            }
        }

        private static void AddValidTemplates(List<VfxLibraryGenerationTemplateOption> destination, List<VfxLibraryGenerationTemplateOption> source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                VfxLibraryGenerationTemplateOption option = source[i];
                if (string.IsNullOrWhiteSpace(option.TemplateId) || string.IsNullOrWhiteSpace(option.TemplateVersion))
                    continue;
                option.DisplayName = TemplateDisplayName(option);
                destination.Add(option);
            }
        }

        private static void AddValidQualityProfiles(List<VfxLibraryQualityProfileOption> destination, List<VfxLibraryQualityProfileOption> source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                VfxLibraryQualityProfileOption option = source[i];
                if (string.IsNullOrWhiteSpace(option.QualityProfileId) || string.IsNullOrWhiteSpace(option.Version))
                    continue;
                option.DisplayName = option.Platform + " · " + option.GraphicsApi + " · " + option.Width + "×" + option.Height + " · " + option.FramesPerSecond + " 帧/秒 · 版本 " + option.Version;
                destination.Add(option);
            }
        }

        private static string ProviderDisplayName(VfxLibraryGenerationProviderOption option)
        {
            string name;
            switch (option.Kind)
            {
                case "BuiltInProcedural": name = "内置程序生成器"; break;
                case "UnityTemplate": name = "Unity 认证模板提供器"; break;
                case "LocalCli": name = "本地命令行提供器"; break;
                case "LocalHttp": name = "本地 HTTP 提供器"; break;
                case "Cloud": name = "云端提供器"; break;
                case "ExperimentalUnityGraph": name = "VFX Graph 实验提供器"; break;
                default: name = option.ProviderId; break;
            }
            return name + " · " + option.ProviderVersion + (option.EnabledByDefault ? string.Empty : "（默认关闭）");
        }

        private static string TemplateDisplayName(VfxLibraryGenerationTemplateOption option)
        {
            string name;
            if (option.TemplateId == "mx.vfx.template.particle.fireball")
                name = "Fireball 认证模板 · 粒子系统";
            else if (option.TemplateId == "mx.vfx.template.line.path")
                name = "路径认证模板 · 线渲染器";
            else if (option.TemplateId == "mx.vfx.template.graph.impact-burst")
                name = "命中爆发认证模板 · VFX Graph";
            else if (option.ArbitraryTopology)
                name = "VFX Graph 实验拓扑 · 只限草稿";
            else
                name = option.TemplateId;
            return name + " · 版本 " + option.TemplateVersion;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
