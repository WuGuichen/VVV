using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxSourceElementView
    {
        [JsonProperty("elementId")] public string ElementId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("elementKind")] public string ElementKind = "SingleLayer";
        [JsonProperty("memberComponentIds")] public List<string> MemberComponentIds = new List<string>();
        [JsonProperty("roles")] public List<string> Roles = new List<string>();
        [JsonProperty("family")] public string Family = string.Empty;
        [JsonProperty("theme")] public string Theme = string.Empty;
        [JsonProperty("shape")] public string Shape = string.Empty;
        [JsonProperty("anchor")] public string Anchor = string.Empty;
        [JsonProperty("lifecycle")] public string Lifecycle = "OneShot";
        [JsonProperty("timingPhase")] public string TimingPhase = "Peak";
        [JsonProperty("useCases")] public List<string> UseCases = new List<string>();
        [JsonProperty("motions")] public List<string> Motions = new List<string>();
        [JsonProperty("simulationSpaces")] public List<string> SimulationSpaces = new List<string>();
        [JsonProperty("rendererModes")] public List<string> RendererModes = new List<string>();
        [JsonProperty("budgetTier")] public string BudgetTier = string.Empty;
        [JsonProperty("granularity")] public string Granularity = string.Empty;
        [JsonProperty("qualityTier")] public string QualityTier = string.Empty;
        [JsonProperty("budgetMaxParticles")] public int BudgetMaxParticles;
        [JsonProperty("sourcePackageIds")] public List<string> SourcePackageIds = new List<string>();
        [JsonProperty("sourceEffectIds")] public List<string> SourceEffectIds = new List<string>();
        [JsonProperty("sourcePrefabGuids")] public List<string> SourcePrefabGuids = new List<string>();
        [JsonProperty("sourcePrefabContentHash", NullValueHandling = NullValueHandling.Ignore)] public string SourcePrefabContentHash = string.Empty;
        [JsonProperty("sourceDependencyHash", NullValueHandling = NullValueHandling.Ignore)] public string SourceDependencyHash = string.Empty;
        [JsonProperty("sourceContentHashes")] public List<string> SourceContentHashes = new List<string>();
        [JsonProperty("autoSemanticSuggestions", NullValueHandling = NullValueHandling.Ignore)]
        public List<VfxSourceSemanticSuggestionView> AutoSemanticSuggestions = new List<VfxSourceSemanticSuggestionView>();
        [JsonProperty("technicalFingerprint")] public string TechnicalFingerprint = string.Empty;
        [JsonProperty("curationStatus")] public string CurationStatus = "Curated";
        [JsonProperty("existingPrimitiveId")] public string ExistingPrimitiveId = string.Empty;
        [JsonProperty("equivalentComponentIds")] public List<string> EquivalentComponentIds = new List<string>();
        [JsonProperty("extractionStatus")] public string ExtractionStatus = "NotExtracted";
        [JsonProperty("consumerCount")] public int ConsumerCount;
        [JsonProperty("previewEvidencePath")] public string PreviewEvidencePath = string.Empty;
        [JsonProperty("stale")] public bool Stale;
    }

    [Serializable]
    internal sealed class VfxSourceSemanticSuggestionView
    {
        [JsonProperty("field")] public string Field = string.Empty;
        [JsonProperty("value")] public string Value = string.Empty;
    }

    /// <summary>Provides Simplified Chinese presentation labels without changing stable authoring values.</summary>
    /// <remarks>Editor-only, LowFreqAlloc. Unknown source/package values intentionally remain unchanged.</remarks>
    internal static class VfxSourceElementChineseLabels
    {
        public static string Format(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未分类";
            return value switch
            {
                "All" or "全部" => "全部",
                "Unknown" => "未知",

                "flash" => "瞬时闪光",
                "core" => "能量核心",
                "glow" => "光晕",
                "flame" => "火焰主体",
                "sparks" => "火星",
                "smoke" => "烟雾",
                "trail" => "尾迹",
                "decal" => "地面贴花",
                "shockwave" => "冲击波",
                "arc" => "电弧",
                "beam" => "光束",
                "rune" => "符文",
                "slash" => "斩击",
                "ring" => "能量环",
                "debris" => "碎片",
                "liquid" => "液体 / 水花",
                "distortion" => "画面扭曲",
                "ribbon" => "飘带",
                "embers" => "余烬",
                "detail" => "细节",

                "Fire" => "火焰",
                "Lightning" => "雷电",
                "Poison" => "毒素",
                "Ice" => "寒冰",
                "Holy" => "神圣",
                "Dark" => "暗影",
                "Blood" => "血液",
                "Mana" => "法力",
                "Water" => "水系",
                "Wind" => "风系",
                "Nature" => "自然",
                "Generic" => "通用",

                "General" => "通用效果",
                "Impact" => "命中 / 冲击",
                "Projectile" => "投射物",
                "Casting" => "施法 / 蓄力",
                "Aura" => "光环 / 附体",
                "Zone" => "区域效果",
                "Telegraph" => "预警提示",
                "Beam" => "光束 / 射线",
                "Melee" => "近战攻击",
                "Shield" => "护盾 / 屏障",
                "Status" => "状态效果",
                "Environment" => "环境效果",
                "Portal" => "传送门",
                "Healing" => "治疗 / 恢复",
                "Movement" => "移动 / 冲刺",
                "Project" => "项目正式资源",
                "Extracted" => "已提取资源",

                "Caster" => "施法者",
                "Ground" => "地面",
                "HitPoint" => "命中点",
                "Path" => "路径",
                "World" => "世界空间",

                "Billboard" => "始终朝向相机",
                "HorizontalBillboard" => "水平公告板",
                "VerticalBillboard" => "垂直公告板",
                "Stretch" => "拉伸公告板",
                "Mesh" => "网格",
                "MeshRenderer" => "网格渲染器",
                "None" => "无渲染器",
                "Box" => "盒体",
                "BoxEdge" => "盒体边缘",
                "BoxShell" => "盒体表面",
                "Circle" => "圆形",
                "Cone" => "圆锥",
                "ConeVolume" => "圆锥体积",
                "Donut" => "圆环体",
                "Hemisphere" => "半球",
                "MeshVolume" => "网格体积",
                "Rectangle" => "矩形",
                "SingleSidedEdge" => "单侧边缘",
                "Sphere" => "球体",

                "OneShot" => "单次播放",
                "Loop" => "循环播放",
                "Build" => "构建",
                "Rebuild" => "重建",
                "Complete" => "完成",
                "Dissipate" => "消散",
                "Continuous" => "持续循环",
                "Anticipation" => "出现 / 蓄势",
                "Peak" => "峰值",
                "Dissipation" => "消散阶段",

                "Directional" => "定向运动",
                "Expanding" => "向外扩张",
                "Orbiting" => "环绕运动",
                "Rising" => "向上升腾",
                "Falling" => "向下坠落",
                "Radial" => "径向运动",
                "Sustained" => "持续运动",
                "Stationary" => "原地表现",

                "Local" => "局部空间",
                "Normal" => "普通",
                "GameplayImportant" => "玩法重要",
                "Showcase" => "展示级",
                "SourceComplex" => "源素材复杂",
                "Low" => "低",
                "Medium" => "中",
                "High" => "高",

                "NotExtracted" => "尚未提取",
                "ReuseExisting" => "复用现有 Primitive",
                "PartiallyExtracted" => "部分已提取",
                "VariantRequired" => "需要派生变体",
                "Stale" => "来源已过期",
                "Conflict" => "提取冲突",

                "SingleLayer" or "单层组件" => "单层组件",
                "CompoundGroup" or "复合职责组" => "复合职责组",
                "RegisteredPrimitive" or "已提取 Primitive" => "已提取 Primitive",
                "ProductionPrimitive" or "Production Primitive" => "正式 Primitive",
                _ => value
            };
        }

        public static string FormatMany(IEnumerable<string> values)
        {
            if (values == null) return "未分类";
            string[] labels = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(Format).Distinct(StringComparer.Ordinal).ToArray();
            return labels.Length == 0 ? "未分类" : string.Join(" / ", labels);
        }
    }

    /// <summary>Enriches Source Elements with controlled, searchable art-production taxonomy.</summary>
    /// <remarks>Editor-only, AllocByDesign. Derived values are rebuildable and never affect Runtime.</remarks>
    internal static class VfxSourceElementTaxonomy
    {
        public static void Apply(VfxSourceElementView element, IReadOnlyDictionary<string, VfxSourceComponentView> components, IReadOnlyDictionary<string, VfxSourceEffectView> effects)
        {
            if (element == null) return;
            List<VfxSourceComponentView> members = element.MemberComponentIds.Select(id => components.TryGetValue(id, out VfxSourceComponentView value) ? value : null).Where(value => value != null).ToList();
            string text = string.Join(" ", members.Select(value => value.DisplayName + " " + value.ObjectPath + " " + value.PrefabPath)).ToLowerInvariant();
            if (text.Length == 0) text = ((element.DisplayName ?? string.Empty) + " " + element.Family + " " + element.Theme + " " + string.Join(" ", element.Roles)).ToLowerInvariant();

            AddRole(element.Roles, text, "beam", "beam", "laser", "ray");
            AddRole(element.Roles, text, "rune", "rune", "sigil", "glyph");
            AddRole(element.Roles, text, "slash", "slash", "stab", "lunge", "sword");
            AddRole(element.Roles, text, "ring", "ring", "circle", "halo");
            AddRole(element.Roles, text, "debris", "debris", "shard", "rock", "fragment");
            AddRole(element.Roles, text, "liquid", "water", "liquid", "drop", "splash");
            AddRole(element.Roles, text, "distortion", "distort", "heat", "refraction");
            AddRole(element.Roles, text, "ribbon", "ribbon", "streak");
            AddRole(element.Roles, text, "embers", "ember", "ash");
            element.Roles = element.Roles.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();

            string explicitTheme = InferTheme(text);
            if (!string.IsNullOrWhiteSpace(explicitTheme)) element.Theme = explicitTheme;

            var useCases = new HashSet<string>(element.UseCases ?? new List<string>(), StringComparer.Ordinal);
            AddCategory(useCases, text, "Impact", "hit", "impact", "explosion", "explotion", "burst", "slam", "strike", "nova");
            AddCategory(useCases, text, "Projectile", "projectile", "orb", "missile", "spear", "arrow", "bullet");
            AddCategory(useCases, text, "Casting", "cast", "casting", "charge", "summon", "gather", "channel");
            AddCategory(useCases, text, "Aura", "aura", "body_fx", "character_fx", "buff", "empower");
            AddCategory(useCases, text, "Zone", "aoe", "ground", "rune", "circle", "area", "zone", "field");
            AddCategory(useCases, text, "Telegraph", "telegraph", "warning", "indicator");
            AddCategory(useCases, text, "Beam", "beam", "laser", "ray");
            AddCategory(useCases, text, "Melee", "slash", "stab", "melee", "lunge", "sword", "weapon");
            AddCategory(useCases, text, "Shield", "shield", "barrier", "guard");
            AddCategory(useCases, text, "Status", "curse", "debuff", "poison", "burn", "frozen", "bleed");
            AddCategory(useCases, text, "Environment", "environment", "weather", "rain", "waterfall", "ambient");
            AddCategory(useCases, text, "Portal", "portal", "teleport", "gateway");
            AddCategory(useCases, text, "Healing", "heal", "recovery", "regeneration");
            AddCategory(useCases, text, "Movement", "dash", "trail", "speed", "blink");
            if (!string.IsNullOrWhiteSpace(element.Family) && element.Family != "General" && element.Family != "Extracted" && element.Family != "Project") useCases.Add(element.Family);
            if (useCases.Count == 0) useCases.Add("General");
            element.UseCases = useCases.OrderBy(value => value, StringComparer.Ordinal).ToList();
            if (string.IsNullOrWhiteSpace(element.Family) || element.Family == "General") element.Family = element.UseCases[0];

            var motions = new HashSet<string>(element.Motions ?? new List<string>(), StringComparer.Ordinal);
            AddCategory(motions, text, "Directional", "trail", "beam", "laser", "projectile", "forward", "lunge");
            AddCategory(motions, text, "Expanding", "shock", "wave", "nova", "burst", "explosion", "explotion");
            AddCategory(motions, text, "Orbiting", "orbit", "orbital", "around", "cluster");
            AddCategory(motions, text, "Rising", "rise", "rising", "up", "gather", "charge");
            AddCategory(motions, text, "Falling", "fall", "falling", "rain", "drop");
            if (element.Shape == "Circle" || element.Shape == "Sphere" || element.Shape == "Donut" || element.Roles.Contains("ring")) motions.Add("Radial");
            if (element.Shape == "Path" || element.Roles.Contains("trail") || element.Roles.Contains("beam")) motions.Add("Directional");
            if (motions.Count == 0) motions.Add(element.Lifecycle == "Loop" ? "Sustained" : "Stationary");
            element.Motions = motions.OrderBy(value => value, StringComparer.Ordinal).ToList();

            element.SimulationSpaces = members.Select(value => string.IsNullOrWhiteSpace(value.SimulationSpace) ? "Unknown" : value.SimulationSpace).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            element.RendererModes = members.Select(value => string.IsNullOrWhiteSpace(value.RenderMode) ? "Unknown" : value.RenderMode).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            if (element.SimulationSpaces.Count == 0) element.SimulationSpaces.Add("Unknown");
            if (element.RendererModes.Count == 0) element.RendererModes.Add("Unknown");
            element.BudgetTier = BudgetTier(element.BudgetMaxParticles);
            element.Granularity = element.ElementKind switch { "CompoundGroup" => "复合职责组", "RegisteredPrimitive" => "已提取 Primitive", "ProductionPrimitive" => "Production Primitive", _ => "单层组件" };
        }

        private static void AddRole(List<string> roles, string text, string role, params string[] tokens)
        { if (tokens.Any(text.Contains) && !roles.Contains(role)) roles.Add(role); }

        private static void AddCategory(HashSet<string> target, string text, string category, params string[] tokens)
        { if (tokens.Any(text.Contains)) target.Add(category); }

        private static string InferTheme(string text)
        {
            if (ContainsAny(text, "blood", "bloody", "crimson")) return "Blood";
            if (ContainsAny(text, "mana", "arcane")) return "Mana";
            if (ContainsAny(text, "water", "liquid", "splash")) return "Water";
            if (ContainsAny(text, "wind", "tornado", "air")) return "Wind";
            if (ContainsAny(text, "nature", "leaf", "plant")) return "Nature";
            if (ContainsAny(text, "fire", "flame", "burn")) return "Fire";
            if (ContainsAny(text, "lightning", "electric", "thunder")) return "Lightning";
            if (ContainsAny(text, "poison", "toxic", "venom")) return "Poison";
            if (ContainsAny(text, "ice", "frost", "snow")) return "Ice";
            if (ContainsAny(text, "holy", "angel", "divine")) return "Holy";
            if (ContainsAny(text, "dark", "demon", "shadow", "curse")) return "Dark";
            return string.Empty;
        }

        private static bool ContainsAny(string text, params string[] tokens) => tokens.Any(text.Contains);
        private static string BudgetTier(int particles) => particles <= 20 ? "微型 ≤20" : particles <= 100 ? "小型 21–100" : particles <= 500 ? "中型 101–500" : particles <= 2000 ? "大型 501–2000" : "超大型 >2000";
    }

    internal sealed class VfxSourceElementCatalog
    {
        public VfxSourceLibraryView Index;
        public VfxSourceCurationView Curation;
        public readonly List<VfxSourceElementView> Elements = new List<VfxSourceElementView>();
        public readonly Dictionary<string, VfxSourceComponentView> Components = new Dictionary<string, VfxSourceComponentView>(StringComparer.Ordinal);
        public readonly List<VfxSourceElementView> UniqueElements = new List<VfxSourceElementView>();
        private readonly Dictionary<string, VfxSourceElementView> _localElements = new Dictionary<string, VfxSourceElementView>(StringComparer.Ordinal);
        public IReadOnlyCollection<VfxSourceElementView> LocalElements => _localElements.Values;
        public int UniqueElementCount;
        public VfxSourceExtractionRegistryView Registry = new VfxSourceExtractionRegistryView();
        public string Message = string.Empty;
        public bool Available;
        public bool SourceIndexAvailable;

        public VfxSourceElementView Find(string elementId)
        {
            return _localElements.TryGetValue(elementId ?? string.Empty, out VfxSourceElementView local)
                ? local
                : Elements.FirstOrDefault(value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
        }

        /// <summary>Binds draft-private Elements without mutating the shared curated palette.</summary>
        /// <remarks>Editor-only, AllocByDesign. Local Elements are resolved before global Elements and cannot shadow global IDs.</remarks>
        public void SetLocalElements(IEnumerable<VfxSourceElementView> elements)
        {
            _localElements.Clear();
            if (elements != null)
            {
                var globalIds = new HashSet<string>(Elements.Select(value => value.ElementId), StringComparer.Ordinal);
                foreach (VfxSourceElementView element in elements)
                {
                    if (element == null || string.IsNullOrWhiteSpace(element.ElementId) || globalIds.Contains(element.ElementId)) continue;
                    _localElements[element.ElementId] = element;
                }
            }
            if (Curation?.DraftLocalOnly == true)
            {
                Available = SourceIndexAvailable && _localElements.Count > 0;
                Message = !SourceIndexAvailable && _localElements.Count > 0
                    ? _localElements.Count + " 个当前草稿素材可浏览；Source Index 未建立，成员预览、语义编辑、导入与烘焙已禁用。"
                    : Available
                        ? _localElements.Count + " 个当前草稿素材"
                        : "当前项目只显示草稿本地素材；请打开含有本地 Element 的草稿。";
            }
        }

        public IReadOnlyList<VfxSourceComponentView> Members(VfxSourceElementView element)
        {
            if (element == null) return Array.Empty<VfxSourceComponentView>();
            var result = new List<VfxSourceComponentView>(element.MemberComponentIds.Count);
            for (int i = 0; i < element.MemberComponentIds.Count; i++)
                if (Components.TryGetValue(element.MemberComponentIds[i], out VfxSourceComponentView component)) result.Add(component);
            return result;
        }
    }

    /// <summary>Creates and validates the curated Element palette over the rebuildable Source Library index.</summary>
    /// <remarks>Editor-only, AllocByDesign. It writes custom JSON and preview evidence, never source Unity assets.</remarks>
    internal sealed class VfxSourceElementCurationService
    {
        internal const int TargetElementCount = 200;
        internal const int TargetCompoundCount = 75;
        internal const string PreviewRoot = "Temp/MxFramework/VfxSourceLibrary/ElementPreviews";
        private static readonly string[] RequiredPrimaryRoles = { "flash", "core", "glow", "flame", "sparks", "smoke", "trail", "decal", "shockwave", "arc", "beam", "detail" };
        private static readonly string[] RequiredThemes = { "Fire", "Lightning", "Poison", "Ice", "Holy", "Dark", "Generic" };

        public VfxSourceElementCatalog Load()
        {
            VfxSourceLibraryEditorStatus source = new VfxSourceLibraryEditorService().Load();
            try
            {
                VfxSourceCurationView curation = File.Exists(VfxSourceLibraryEditorService.CurationPath)
                    ? JsonConvert.DeserializeObject<VfxSourceCurationView>(File.ReadAllText(VfxSourceLibraryEditorService.CurationPath))
                    : new VfxSourceCurationView();
                curation ??= new VfxSourceCurationView();
                VfxAuthoredElementLibraryView authored = null;
                string authoredMessage = string.Empty;
                if (curation.ExplicitAuthored)
                {
                    authored = new VfxAuthoredElementLibraryService().Load(out authoredMessage);
                }
                return BuildCatalog(source, curation, authored, authoredMessage);
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                return new VfxSourceElementCatalog
                {
                    Index = source.Index,
                    SourceIndexAvailable = source.Available && source.Index != null,
                    Available = false,
                    Message = "读取 Element 策展目录失败：" + exception.Message,
                };
            }
        }

        internal static VfxSourceElementCatalog BuildCatalog(
            VfxSourceLibraryEditorStatus source,
            VfxSourceCurationView curation,
            VfxAuthoredElementLibraryView authored,
            string authoredMessage)
        {
            source ??= new VfxSourceLibraryEditorStatus { Message = "Source Index 不可用。" };
            curation ??= new VfxSourceCurationView();
            var catalog = new VfxSourceElementCatalog
            {
                Index = source.Index,
                Curation = curation,
                SourceIndexAvailable = source.Available && source.Index != null,
                Available = false,
                Message = source.Message,
            };
            if (!ValidateCurationStableIds(curation, catalog)) return catalog;
            if (curation.ExplicitAuthored)
            {
                if (authored == null)
                {
                    catalog.Message = string.IsNullOrWhiteSpace(authoredMessage)
                        ? "全局派生素材库不可用。"
                        : authoredMessage;
                    return catalog;
                }
                catalog.Elements.AddRange(
                    (authored.Elements ?? new List<VfxSourceElementView>())
                    .Where(value => value != null)
                    .OrderBy(value => value.ElementId, StringComparer.Ordinal));
            }
            else if (curation.Elements != null)
            {
                catalog.Elements.AddRange(
                    curation.Elements
                    .Where(value => value != null)
                    .OrderBy(value => value.ElementId, StringComparer.Ordinal));
            }
            ApplyElementDisplayNameOverrides(catalog.Elements, curation);
            if (!catalog.SourceIndexAvailable)
            {
                catalog.Message = catalog.Elements.Count > 0
                    ? catalog.Elements.Count + " 个版本化素材可浏览；" + source.Message
                        + " 成员预览、语义编辑、导入与烘焙已禁用。"
                    : source.Message;
                return catalog;
            }

            for (int i = 0; i < source.Index.Components.Count; i++)
                catalog.Components[source.Index.Components[i].ComponentId] = source.Index.Components[i];
            RefreshElementSemantics(catalog.Elements, catalog.Components, curation);
            catalog.Registry = new VfxSourceExtractionRegistryService().Load(source.Index);
            ApplyExtractionStatus(catalog);
            catalog.UniqueElementCount = source.Index.Components
                .Where(value => !value.Deprecated
                    && value.MaterialGuids.Count > 0
                    && !string.IsNullOrWhiteSpace(value.TechnicalFingerprint))
                .Select(value => value.TechnicalFingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count();
            ValidateStale(catalog);
            catalog.Available = curation.DraftLocalOnly || curation.ExplicitAuthored || catalog.Elements.Count > 0;
            catalog.Message = curation.DraftLocalOnly
                ? "当前项目只显示草稿本地素材；请打开含有本地 Element 的草稿。"
                : curation.ExplicitAuthored
                    ? catalog.Elements.Count + " 个手工派生全局素材；自动精选已禁用。"
                    : catalog.Available
                        ? catalog.Elements.Count + " 个精选 Element · "
                            + catalog.Elements.Count(value => value.ElementKind == "CompoundGroup") + " 个复合职责组"
                        : "尚未生成精选 Element；请在原材料管理中执行自动策展。";
            return catalog;
        }

        internal static bool ValidateCurationStableIds(VfxSourceCurationView curation, VfxSourceElementCatalog catalog)
        {
            var componentIds = new HashSet<string>(StringComparer.Ordinal);
            List<VfxSourceCurationEntryView> entries = curation?.Entries ?? new List<VfxSourceCurationEntryView>();
            for (int i = 0; i < entries.Count; i++)
            {
                string componentId = entries[i]?.ComponentId;
                if (string.IsNullOrWhiteSpace(componentId)) continue;
                if (componentIds.Add(componentId)) continue;
                catalog.Available = false;
                catalog.Message = "Element 策展数据存在重复 componentId：" + componentId;
                return false;
            }

            var displayNameElementIds = new HashSet<string>(StringComparer.Ordinal);
            List<VfxSourceElementCurationOverrideView> overrides = curation?.ElementOverrides ?? new List<VfxSourceElementCurationOverrideView>();
            for (int i = 0; i < overrides.Count; i++)
            {
                VfxSourceElementCurationOverrideView entry = overrides[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ElementId) || string.IsNullOrWhiteSpace(entry.DisplayName)) continue;
                if (displayNameElementIds.Add(entry.ElementId)) continue;
                catalog.Available = false;
                catalog.Message = "Element 策展数据存在重复显示名覆盖 elementId：" + entry.ElementId;
                return false;
            }
            return true;
        }

        private static void ApplyElementDisplayNameOverrides(IEnumerable<VfxSourceElementView> elements, VfxSourceCurationView curation)
        {
            Dictionary<string, string> names = (curation?.ElementOverrides ?? new List<VfxSourceElementCurationOverrideView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ElementId) && !string.IsNullOrWhiteSpace(value.DisplayName))
                .ToDictionary(value => value.ElementId, value => value.DisplayName, StringComparer.Ordinal);
            foreach (VfxSourceElementView element in elements)
                if (names.TryGetValue(element.ElementId, out string displayName)) element.DisplayName = displayName;
        }

        private static void RefreshElementSemantics(IEnumerable<VfxSourceElementView> elements, IReadOnlyDictionary<string, VfxSourceComponentView> components, VfxSourceCurationView curation)
        {
            Dictionary<string, VfxSourceCurationEntryView> entries = (curation?.Entries ?? new List<VfxSourceCurationEntryView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ComponentId)).ToDictionary(value => value.ComponentId, StringComparer.Ordinal);
            foreach (VfxSourceElementView element in elements)
            {
                List<VfxSourceComponentView> members = element.MemberComponentIds
                    .Select(id => components.TryGetValue(id, out VfxSourceComponentView component) ? component : null)
                    .Where(value => value != null).ToList();
                if (members.Count == 0) continue;
                VfxSourceComponentView primary = members[0];
                List<VfxSourceCurationEntryView> memberEntries = element.MemberComponentIds
                    .Select(id => entries.TryGetValue(id, out VfxSourceCurationEntryView entry) ? entry : null).Where(value => value != null).ToList();
                element.Roles = members.SelectMany(value => value.Roles).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (element.ElementKind == "SingleLayer" && !string.IsNullOrWhiteSpace(primary.DisplayName)) element.DisplayName = primary.DisplayName;
                string curatedFamily = Majority(memberEntries.Select(value => value.Family), string.Empty);
                if (!string.IsNullOrWhiteSpace(curatedFamily)) element.Family = curatedFamily;
                element.Theme = Majority(members.Select(value => value.Theme), "Generic");
                element.Shape = primary.Shape;
                element.Anchor = primary.Anchor;
                element.Lifecycle = members.Any(value => value.Loop) ? "Loop" : "OneShot";
                element.QualityTier = Majority(members.Select(value => value.QualityTier), "Normal");
                element.BudgetMaxParticles = members.Sum(value => value.MaxParticles);
                string timing = Majority(memberEntries.Select(value => value.TimingPhase), string.Empty);
                if (!string.IsNullOrWhiteSpace(timing)) element.TimingPhase = timing;
                List<string> useCases = memberEntries.SelectMany(value => value.UseCases ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (useCases.Count > 0) element.UseCases = useCases;
                List<string> motions = memberEntries.SelectMany(value => value.Motions ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (motions.Count > 0) element.Motions = motions;
            }
        }

        internal static void ApplyManualElementOverrides(IEnumerable<VfxSourceElementView> elements, IReadOnlyDictionary<string, VfxSourceComponentView> components, VfxSourceCurationView curation)
        {
            Dictionary<string, VfxSourceCurationEntryView> entries = (curation?.Entries ?? new List<VfxSourceCurationEntryView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ComponentId)).ToDictionary(value => value.ComponentId, StringComparer.Ordinal);
            Dictionary<string, string> elementNames = (curation?.ElementOverrides ?? new List<VfxSourceElementCurationOverrideView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ElementId) && !string.IsNullOrWhiteSpace(value.DisplayName))
                .ToDictionary(value => value.ElementId, value => value.DisplayName, StringComparer.Ordinal);
            foreach (VfxSourceElementView element in elements)
            {
                List<VfxSourceComponentView> members = element.MemberComponentIds
                    .Select(id => components.TryGetValue(id, out VfxSourceComponentView component) ? component : null).Where(value => value != null).ToList();
                List<VfxSourceCurationEntryView> memberEntries = element.MemberComponentIds
                    .Select(id => entries.TryGetValue(id, out VfxSourceCurationEntryView entry) ? entry : null).Where(value => value != null).ToList();
                if (members.Count == 0 || memberEntries.Count == 0) continue;
                VfxSourceCurationEntryView primaryEntry = entries.TryGetValue(element.MemberComponentIds[0], out VfxSourceCurationEntryView primary) ? primary : null;
                if (elementNames.TryGetValue(element.ElementId, out string elementDisplayName)) element.DisplayName = elementDisplayName;
                else if (element.ElementKind == "SingleLayer" && !string.IsNullOrWhiteSpace(primaryEntry?.DisplayName)) element.DisplayName = primaryEntry.DisplayName;
                if (memberEntries.Any(value => value.Roles?.Count > 0)) element.Roles = members.SelectMany(value => value.Roles).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                string family = Majority(memberEntries.Select(value => value.Family), string.Empty);
                if (!string.IsNullOrWhiteSpace(family)) element.Family = family;
                if (memberEntries.Any(value => !string.IsNullOrWhiteSpace(value.Theme))) element.Theme = Majority(members.Select(value => value.Theme), element.Theme);
                string shape = Majority(memberEntries.Select(value => value.Shape), string.Empty);
                if (!string.IsNullOrWhiteSpace(shape)) element.Shape = shape;
                string anchor = Majority(memberEntries.Select(value => value.Anchor), string.Empty);
                if (!string.IsNullOrWhiteSpace(anchor)) element.Anchor = anchor;
                string timing = Majority(memberEntries.Select(value => value.TimingPhase), string.Empty);
                if (!string.IsNullOrWhiteSpace(timing)) element.TimingPhase = timing;
                List<string> useCases = memberEntries.SelectMany(value => value.UseCases ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (useCases.Count > 0) element.UseCases = useCases;
                List<string> motions = memberEntries.SelectMany(value => value.Motions ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (motions.Count > 0) element.Motions = motions;
                if (memberEntries.Any(value => !string.IsNullOrWhiteSpace(value.QualityTier))) element.QualityTier = Majority(members.Select(value => value.QualityTier), element.QualityTier);
            }
        }

        public bool Generate(out VfxSourceElementCatalog catalog, out string message)
        {
            VfxSourceLibraryEditorStatus source = new VfxSourceLibraryEditorService().Load();
            catalog = null;
            if (!source.Available) { message = source.Message; return false; }
            VfxSourceCurationView curation;
            try
            {
                curation = File.Exists(VfxSourceLibraryEditorService.CurationPath)
                    ? JsonConvert.DeserializeObject<VfxSourceCurationView>(File.ReadAllText(VfxSourceLibraryEditorService.CurationPath)) ?? new VfxSourceCurationView()
                    : new VfxSourceCurationView();
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "无法读取既有策展修正：" + exception.Message;
                return false;
            }
            List<VfxSourceElementView> previousElements = (curation.Elements ?? new List<VfxSourceElementView>()).ToList();
            if (!curation.AutomaticCurated)
            {
                catalog = Load();
                message = "当前编目模式为 " + curation.PaletteMode + "；自动策展已禁用，不会重新生成全局 Element。";
                return false;
            }

            Dictionary<string, VfxSourceEffectView> effects = source.Index.Effects.ToDictionary(value => value.SourceEffectId, StringComparer.Ordinal);
            List<VfxSourceComponentView> valid = UniqueRenderableComponents(source.Index.Components);
            var selectedSingles = new List<VfxSourceComponentView>();
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            // Themes are filled first because every component has one primary role, while a theme quota can
            // simultaneously satisfy several role quotas. This keeps the 125-single palette feasible and stable.
            FillQuota(selectedSingles, selectedIds, valid, component => component.Theme, RequiredThemes, 12);
            FillQuota(selectedSingles, selectedIds, valid, component => component.Roles.FirstOrDefault() ?? "detail", RequiredPrimaryRoles, 10);
            string[] families = source.Index.Effects.Select(value => value.Family).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            FillQuota(selectedSingles, selectedIds, valid, component => effects.TryGetValue(component.SourceEffectId, out VfxSourceEffectView effect) ? effect.Family : "General", families, 8);
            foreach (VfxSourceComponentView component in valid.OrderBy(ScoreComponent).ThenBy(value => value.ComponentId, StringComparer.Ordinal))
            {
                if (selectedSingles.Count >= TargetElementCount - TargetCompoundCount) break;
                if (selectedIds.Add(component.ComponentId)) selectedSingles.Add(component);
            }

            var elements = new List<VfxSourceElementView>(TargetElementCount);
            for (int i = 0; i < selectedSingles.Count; i++) elements.Add(CreateElement(new[] { selectedSingles[i] }, effects, "SingleLayer"));
            foreach (IGrouping<string, VfxSourceComponentView> group in valid.GroupBy(value => value.SourceEffectId, StringComparer.Ordinal).OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (elements.Count >= TargetElementCount) break;
                List<VfxSourceComponentView> members = BuildCompoundMembers(group);
                if (members.Count < 2) continue;
                VfxSourceElementView element = CreateElement(members, effects, "CompoundGroup");
                if (elements.Any(value => value.TechnicalFingerprint == element.TechnicalFingerprint)) continue;
                elements.Add(element);
            }
            foreach (VfxSourceComponentView component in valid.OrderBy(ScoreComponent).ThenBy(value => value.ComponentId, StringComparer.Ordinal))
            {
                if (elements.Count >= TargetElementCount) break;
                if (elements.Any(value => value.MemberComponentIds.Count == 1 && value.MemberComponentIds[0] == component.ComponentId)) continue;
                elements.Add(CreateElement(new[] { component }, effects, "SingleLayer"));
            }
            PreserveDraftReferencedElements(elements, previousElements, valid);

            curation.SchemaVersion = "mx.vfx.source-curation.v2";
            curation.SourceIndexHash = source.Index.IndexHash;
            curation.Elements = elements.OrderBy(value => value.ElementId, StringComparer.Ordinal).ToList();
            curation.CurationHash = string.Empty;
            curation.CurationHash = Hash(JsonConvert.SerializeObject(curation, Formatting.None));
            File.WriteAllText(VfxSourceLibraryEditorService.CurationPath, JsonConvert.SerializeObject(curation, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(VfxSourceLibraryEditorService.CurationPath, ImportAssetOptions.ForceSynchronousImport);
            catalog = Load();
            message = "已策展 " + catalog.Elements.Count + " 个 Element，其中 " + catalog.Elements.Count(value => value.ElementKind == "CompoundGroup") + " 个复合职责组。";
            return catalog.Available;
        }

        private static void PreserveDraftReferencedElements(
            List<VfxSourceElementView> elements,
            IReadOnlyCollection<VfxSourceElementView> previousElements,
            IReadOnlyCollection<VfxSourceComponentView> availableComponents)
        {
            HashSet<string> referenced = ReadDraftReferencedElementIds();
            if (referenced.Count == 0) return;
            var availableIds = new HashSet<string>(availableComponents.Select(value => value.ComponentId), StringComparer.Ordinal);
            var selectedIds = new HashSet<string>(elements.Select(value => value.ElementId), StringComparer.Ordinal);
            Dictionary<string, VfxSourceElementView> previousById = previousElements
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ElementId))
                .ToDictionary(value => value.ElementId, StringComparer.Ordinal);
            foreach (string elementId in referenced.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (selectedIds.Contains(elementId)
                    || !previousById.TryGetValue(elementId, out VfxSourceElementView previous)
                    || previous.MemberComponentIds.Count == 0
                    || previous.MemberComponentIds.Any(value => !availableIds.Contains(value))) continue;
                elements.Add(previous);
                selectedIds.Add(elementId);
            }

            while (elements.Count > TargetElementCount)
            {
                int removeIndex = -1;
                for (int i = elements.Count - 1; i >= 0; i--)
                {
                    VfxSourceElementView candidate = elements[i];
                    if (referenced.Contains(candidate.ElementId) || candidate.ElementKind != "SingleLayer") continue;
                    int requiredTheme = Math.Min(12, availableComponents.Count(value => value.Theme == candidate.Theme));
                    if (elements.Count(value => value.Theme == candidate.Theme) <= requiredTheme) continue;
                    removeIndex = i;
                    break;
                }
                if (removeIndex < 0) break;
                elements.RemoveAt(removeIndex);
            }
        }

        private static HashSet<string> ReadDraftReferencedElementIds()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(VfxCompositionDraftService.DraftRoot)) return result;
            foreach (string path in Directory.GetFiles(VfxCompositionDraftService.DraftRoot, "*.json", SearchOption.TopDirectoryOnly).OrderBy(value => value, StringComparer.Ordinal))
            {
                try
                {
                    JObject draft = JObject.Parse(File.ReadAllText(path));
                    foreach (JObject layer in (draft["layers"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                    {
                        string elementId = layer.Value<string>("elementId");
                        if (!string.IsNullOrWhiteSpace(elementId)) result.Add(elementId);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is JsonException)
                {
                    Debug.LogWarning("VFX 策展跳过无法读取的 Composition Draft：" + path + " · " + exception.Message);
                }
            }
            return result;
        }

        private static void ApplyExtractionStatus(VfxSourceElementCatalog catalog)
        {
            foreach (VfxSourceElementView element in catalog.Elements)
            {
                List<string> primitiveIds = new List<string>();
                int mapped = 0;
                foreach (string componentId in element.MemberComponentIds)
                {
                    if (!catalog.Components.TryGetValue(componentId, out VfxSourceComponentView component)) continue;
                    VfxSourceExtractionRegistryEntryView entry = VfxSourceExtractionRegistryService.Find(catalog.Registry, component);
                    string primitiveId = entry?.PrimitiveId ?? component.ExistingPrimitiveId;
                    if (string.IsNullOrWhiteSpace(primitiveId)) continue;
                    mapped++;
                    if (!primitiveIds.Contains(primitiveId)) primitiveIds.Add(primitiveId);
                }
                element.ExtractionStatus = mapped == 0 ? "NotExtracted" : mapped == element.MemberComponentIds.Count ? "ReuseExisting" : "PartiallyExtracted";
                if (primitiveIds.Count == 1 && mapped == element.MemberComponentIds.Count) element.ExistingPrimitiveId = primitiveIds[0];
            }
        }

        private static List<VfxSourceComponentView> UniqueRenderableComponents(IEnumerable<VfxSourceComponentView> source)
        {
            return source.Where(IsRenderable)
                .GroupBy(value => value.TechnicalFingerprint, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(value => value.Preferred).ThenBy(ScoreComponent).ThenBy(value => value.ComponentId, StringComparer.Ordinal).First())
                .OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
        }

        private static bool IsRenderable(VfxSourceComponentView component)
        {
            if (component == null || component.Deprecated || string.IsNullOrWhiteSpace(component.PrefabPath) || component.MaterialGuids.Count == 0) return false;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(component.PrefabPath);
            if (prefab == null) return false;
            ParticleSystem system = FindSourceSystem(prefab, component);
            if (system == null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(system.gameObject) > 0) return false;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || !renderer.enabled) return false;
            Material[] materials = renderer.sharedMaterials;
            return materials.Any(material => material != null && material.shader != null && material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static void FillQuota(List<VfxSourceComponentView> result, HashSet<string> ids, List<VfxSourceComponentView> candidates, Func<VfxSourceComponentView, string> selector, IEnumerable<string> values, int quota)
        {
            foreach (string value in values)
            {
                int count = result.Count(item => string.Equals(selector(item), value, StringComparison.Ordinal));
                foreach (VfxSourceComponentView component in candidates.Where(item => string.Equals(selector(item), value, StringComparison.Ordinal)).OrderBy(ScoreComponent).ThenBy(item => item.ComponentId, StringComparer.Ordinal))
                {
                    if (count >= quota || result.Count >= TargetElementCount - TargetCompoundCount) break;
                    if (!ids.Add(component.ComponentId)) continue;
                    result.Add(component); count++;
                }
            }
        }

        private static int ScoreComponent(VfxSourceComponentView value)
        {
            int score = value.Preferred ? -1000 : 0;
            score += value.MaxParticles;
            score += value.QualityTier == "SourceComplex" ? 800 : value.QualityTier == "Showcase" ? 300 : 0;
            score += value.Roles.Contains("detail") && value.Roles.Count == 1 ? 100 : 0;
            score += value.Loop ? 20 : 0;
            return score;
        }

        private static List<VfxSourceComponentView> BuildCompoundMembers(IEnumerable<VfxSourceComponentView> source)
        {
            List<VfxSourceComponentView> values = source.OrderBy(ScoreComponent).ThenBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            if (values.Count < 2) return new List<VfxSourceComponentView>();
            string anchor = values[0].Anchor;
            string space = values[0].SimulationSpace;
            var roles = new HashSet<string>(StringComparer.Ordinal);
            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<VfxSourceComponentView>();
            foreach (VfxSourceComponentView component in values)
            {
                if (result.Count >= 8 || component.Anchor != anchor || component.SimulationSpace != space || !fingerprints.Add(component.TechnicalFingerprint)) continue;
                string role = component.Roles.FirstOrDefault() ?? "detail";
                if (roles.Contains(role) && result.Count >= 4) continue;
                roles.Add(role); result.Add(component);
            }
            return result.Count >= 2 && roles.Count >= 2 ? result : new List<VfxSourceComponentView>();
        }

        private static VfxSourceElementView CreateElement(IEnumerable<VfxSourceComponentView> source, IReadOnlyDictionary<string, VfxSourceEffectView> effects, string kind)
        {
            List<VfxSourceComponentView> members = source.OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            VfxSourceComponentView primary = members[0];
            List<string> roles = members.SelectMany(value => value.Roles).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            string fingerprint = Hash(string.Join("|", members.Select(value => value.TechnicalFingerprint)));
            string token = fingerprint.Substring("sha256:".Length, 16);
            string family = effects.TryGetValue(primary.SourceEffectId, out VfxSourceEffectView effect) ? effect.Family : "General";
            if (family == "Beam" && !roles.Contains("beam")) { roles.Add("beam"); roles.Sort(StringComparer.Ordinal); }
            string display = kind == "SingleLayer" ? primary.DisplayName : family + " · " + string.Join(" + ", roles.Take(3));
            return new VfxSourceElementView
            {
                ElementId = "vfx.element.piloto." + (kind == "SingleLayer" ? "layer." : "group.") + token,
                DisplayName = display,
                ElementKind = kind,
                MemberComponentIds = members.Select(value => value.ComponentId).ToList(),
                Roles = roles,
                Family = family,
                Theme = Majority(members.Select(value => value.Theme), "Generic"),
                Shape = Majority(members.Select(value => value.Shape), "Billboard"),
                Anchor = primary.Anchor,
                Lifecycle = members.Any(value => value.Loop) ? "Loop" : "OneShot",
                TimingPhase = InferTimingPhase(roles),
                QualityTier = Majority(members.Select(value => value.QualityTier), "Normal"),
                BudgetMaxParticles = members.Sum(value => value.MaxParticles),
                SourcePackageIds = members.Select(value => value.SourcePackageId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                SourceEffectIds = members.Select(value => value.SourceEffectId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                SourcePrefabGuids = members.Select(value => value.PrefabGuid).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                SourceContentHashes = members.Select(value => value.SourceContentHash).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                TechnicalFingerprint = fingerprint,
                ExistingPrimitiveId = Majority(members.Select(value => value.ExistingPrimitiveId).Where(value => !string.IsNullOrWhiteSpace(value)), string.Empty),
                PreviewEvidencePath = PreviewRoot + "/" + token + "/manifest.json",
                CurationStatus = "Curated"
            };
        }

        private static string InferTimingPhase(IReadOnlyCollection<string> roles)
        {
            if (roles.Contains("glow") || roles.Contains("core")) return "Anticipation";
            if (roles.Contains("smoke") || roles.Contains("decal")) return "Dissipation";
            return "Peak";
        }

        private static string Majority(IEnumerable<string> values, string fallback)
        {
            return values.Where(value => !string.IsNullOrWhiteSpace(value)).GroupBy(value => value, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).Select(group => group.Key).FirstOrDefault() ?? fallback;
        }

        internal static void ValidateStale(VfxSourceElementCatalog catalog)
        {
            for (int i = 0; i < catalog.Elements.Count; i++)
            {
                VfxSourceElementView element = catalog.Elements[i];
                element.Stale = element.MemberComponentIds.Any(id => !catalog.Components.ContainsKey(id));
                if (!element.Stale)
                {
                    List<string> hashes = element.MemberComponentIds.Select(id => catalog.Components[id].SourceContentHash).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                    element.Stale = !hashes.SequenceEqual(element.SourceContentHashes);
                }
            }
        }

        internal static ParticleSystem FindSourceSystem(GameObject sourceRoot, VfxSourceComponentView evidence)
        {
            ParticleSystem[] systems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(systems[i], out _, out long localId) && localId == evidence.LocalFileId) return systems[i];
            Transform target = sourceRoot.transform.Find(evidence.ObjectPath);
            return target != null ? target.GetComponent<ParticleSystem>() : null;
        }

        internal static string Hash(string value)
        {
            using SHA256 sha = SHA256.Create();
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal static class VfxSourceElementMenus
    {
        [MenuItem("MxFramework/VFX/Source Library/Open Manual Composer")]
        private static void OpenManualComposer()
        {
            VfxAuthoringResourceLibraryWindow.OpenManual();
        }
    }

    public static class VfxSourceElementAutomation
    {
        /// <summary>Generates the curated palette without opening a modal dialog.</summary>
        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public static string GenerateCuratedElements()
        {
            bool success = new VfxSourceElementCurationService().Generate(out _, out string message);
            return (success ? "OK: " : "ERROR: ") + message;
        }

        /// <summary>Generates fixed-seed preview evidence without opening a modal dialog.</summary>
        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public static string GeneratePreviews()
        {
            bool success = VfxSourceElementPreviewCapture.GenerateAll(out string message);
            return (success ? "OK: " : "ERROR: ") + message;
        }

        /// <summary>Generates generic composition starter drafts without opening a modal dialog.</summary>
        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public static string GenerateStarterDrafts()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            bool success = VfxCompositionStarterDrafts.Generate(catalog, out string message);
            return (success ? "OK: " : "ERROR: ") + message;
        }

        /// <summary>Creates or stably updates one composition draft from an indexed source ParticleSystem Prefab.</summary>
        /// <remarks>Editor-only, AllocByDesign. Writes only custom Draft JSON; it never modifies Unity source assets.</remarks>
        public static string CreateDraftFromSourcePrefab(string prefabPath)
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var importer = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = importer.Analyze(prefabPath, catalog);
            if (!analysis.Valid) return "ERROR: " + string.Join("\n", analysis.Diagnostics);
            string draftId = "vfx.composition.source." + analysis.PrefabGuid;
            var draftService = new VfxCompositionDraftService();
            string loadedPath = string.Empty;
            VfxCompositionDraftView existing = null;
            IReadOnlyList<string> paths = draftService.ListDrafts();
            for (int i = 0; i < paths.Count; i++)
            {
                VfxCompositionDraftView candidate = draftService.Load(paths[i], out _);
                if (candidate == null || !string.Equals(candidate.DraftId, draftId, StringComparison.Ordinal)) continue;
                existing = candidate;
                loadedPath = paths[i];
                break;
            }
            if (!importer.CreateOrUpdate(analysis, catalog, existing, out VfxCompositionDraftView draft, out string message))
                return "ERROR: " + message;
            string expectedHash = VfxCompositionDraftService.HashFile(loadedPath);
            if (!draftService.SaveGuarded(draft, loadedPath, expectedHash, out string savedPath, out _, out string saveMessage))
                return "ERROR: " + saveMessage;
            return "OK: " + message + "\n" + savedPath;
        }
    }

    internal static class VfxSourceElementInstantiation
    {
        /// <summary>Builds a disposable preview/bake root by copying only selected ParticleSystem data.</summary>
        /// <remarks>Editor-only, AllocByDesign. The caller owns and must destroy the returned root.</remarks>
        public static GameObject Create(VfxSourceElementCatalog catalog, VfxSourceElementView element, string name = null)
        {
            if (catalog == null || element == null || element.Stale) return null;
            var root = new GameObject(name ?? element.DisplayName) { hideFlags = HideFlags.HideAndDontSave };
            IReadOnlyList<VfxSourceComponentView> members = catalog.Members(element);
            foreach (IGrouping<string, VfxSourceComponentView> group in members.GroupBy(value => value.PrefabPath, StringComparer.Ordinal))
            {
                GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                if (sourceRoot == null)
                {
                    Object.DestroyImmediate(root);
                    return null;
                }
                var particleSystemMap = new Dictionary<ParticleSystem, ParticleSystem>();
                foreach (VfxSourceComponentView evidence in group)
                {
                    ParticleSystem source = VfxSourceElementCurationService.FindSourceSystem(sourceRoot, evidence);
                    if (source == null)
                    {
                        Object.DestroyImmediate(root);
                        return null;
                    }
                    var layer = new GameObject(evidence.DisplayName) { hideFlags = HideFlags.HideAndDontSave };
                    layer.transform.SetParent(root.transform, false);
                    Matrix4x4 relative = sourceRoot.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    layer.transform.localPosition = relative.GetColumn(3);
                    layer.transform.localRotation = relative.rotation;
                    layer.transform.localScale = relative.lossyScale;
                    ParticleSystem target = layer.AddComponent<ParticleSystem>();
                    EditorUtility.CopySerialized(source, target);
                    ParticleSystemRenderer sourceRenderer = source.GetComponent<ParticleSystemRenderer>();
                    ParticleSystemRenderer targetRenderer = target.GetComponent<ParticleSystemRenderer>();
                    if (sourceRenderer != null && targetRenderer != null) EditorUtility.CopySerialized(sourceRenderer, targetRenderer);
                    particleSystemMap.Add(source, target);
                }
                if (!TryRemapSubEmitters(particleSystemMap))
                {
                    Object.DestroyImmediate(root);
                    return null;
                }
            }
            if (root.transform.childCount > 0) return root;
            Object.DestroyImmediate(root); return null;
        }

        private static bool TryRemapSubEmitters(IReadOnlyDictionary<ParticleSystem, ParticleSystem> particleSystemMap)
        {
            foreach (KeyValuePair<ParticleSystem, ParticleSystem> pair in particleSystemMap)
            {
                ParticleSystem.SubEmittersModule sourceModule = pair.Key.subEmitters;
                if (!TryClearCopiedSubEmitters(pair.Value)) return false;
                ParticleSystem.SubEmittersModule targetModule = pair.Value.subEmitters;
                int resolvedSubEmitterCount = CountResolvedSubEmitters(sourceModule);
                targetModule.enabled = sourceModule.enabled
                    && (sourceModule.subEmittersCount == 0 || resolvedSubEmitterCount > 0);
                if (!targetModule.enabled)
                {
                    if (!SubEmittersMatch(sourceModule, targetModule, particleSystemMap)) return false;
                    continue;
                }
                int remappedSubEmitterCount = 0;
                for (int i = 0; i < sourceModule.subEmittersCount; i++)
                {
                    ParticleSystem sourceChild = sourceModule.GetSubEmitterSystem(i);
                    if (sourceChild == null) continue;
                    if (!particleSystemMap.TryGetValue(sourceChild, out ParticleSystem targetChild))
                        return false;
                    targetModule.AddSubEmitter(
                        targetChild,
                        sourceModule.GetSubEmitterType(i),
                        sourceModule.GetSubEmitterProperties(i));
                    remappedSubEmitterCount++;
                }
                if (remappedSubEmitterCount != resolvedSubEmitterCount) return false;
                if (!SubEmittersMatch(sourceModule, targetModule, particleSystemMap)) return false;
            }
            return true;
        }

        private static int CountResolvedSubEmitters(ParticleSystem.SubEmittersModule module)
        {
            int count = 0;
            for (int i = 0; i < module.subEmittersCount; i++)
                if (module.GetSubEmitterSystem(i) != null) count++;
            return count;
        }

        private static bool TryClearCopiedSubEmitters(ParticleSystem target)
        {
            var serializedTarget = new SerializedObject(target);
            SerializedProperty copiedSubEmitters = serializedTarget.FindProperty("SubModule.subEmitters");
            if (copiedSubEmitters == null || !copiedSubEmitters.isArray) return false;
            copiedSubEmitters.arraySize = 0;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            return target.subEmitters.subEmittersCount == 0;
        }

        private static bool SubEmittersMatch(
            ParticleSystem.SubEmittersModule sourceModule,
            ParticleSystem.SubEmittersModule targetModule,
            IReadOnlyDictionary<ParticleSystem, ParticleSystem> particleSystemMap)
        {
            int expectedTargetCount = CountResolvedSubEmitters(sourceModule);
            bool expectedEnabled = sourceModule.enabled
                && (sourceModule.subEmittersCount == 0 || expectedTargetCount > 0);
            if (expectedEnabled != targetModule.enabled)
                return false;
            if (!expectedEnabled) return targetModule.subEmittersCount == 0;
            if (expectedTargetCount != targetModule.subEmittersCount) return false;
            int targetIndex = 0;
            for (int i = 0; i < sourceModule.subEmittersCount; i++)
            {
                ParticleSystem sourceChild = sourceModule.GetSubEmitterSystem(i);
                if (sourceChild == null) continue;
                if (!particleSystemMap.TryGetValue(sourceChild, out ParticleSystem expectedTarget)
                    || targetModule.GetSubEmitterSystem(targetIndex) != expectedTarget
                    || sourceModule.GetSubEmitterType(i) != targetModule.GetSubEmitterType(targetIndex)
                    || sourceModule.GetSubEmitterProperties(i) != targetModule.GetSubEmitterProperties(targetIndex))
                    return false;
                targetIndex++;
            }
            return true;
        }
    }

    internal static class VfxSourceElementPreviewCapture
    {
        private static readonly float[] Fractions = { 0.25f, 0.5f, 0.75f };

        public static bool GenerateAll(out string message)
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            if (!catalog.Available) { message = catalog.Message; return false; }
            int completed = 0;
            try
            {
                for (int i = 0; i < catalog.Elements.Count; i++)
                {
                    VfxSourceElementView element = catalog.Elements[i];
                    if (element.CurationStatus != "Curated" || element.Stale) continue;
                    if (EditorUtility.DisplayCancelableProgressBar("生成精选 VFX Element 预览", element.DisplayName, i / (float)Math.Max(1, catalog.Elements.Count)))
                    { message = "用户取消了预览生成；已完成 " + completed + " 个。"; return false; }
                    if (!CaptureElement(catalog, element, out message)) return false;
                    completed++;
                }
                message = "已为 " + completed + " 个精选 Element 生成固定 seed 17 的三时间点预览。";
                return completed > 0;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static bool CaptureElement(VfxSourceElementCatalog catalog, VfxSourceElementView element, out string message)
        {
            string root = Path.GetDirectoryName(element.PreviewEvidencePath)?.Replace('\\', '/') ?? VfxSourceElementCurationService.PreviewRoot;
            Directory.CreateDirectory(root);
            var captures = new JArray();
            for (int i = 0; i < Fractions.Length; i++)
            {
                float fraction = Fractions[i];
                byte[] png = Capture(catalog, element, fraction);
                string path = root + "/t" + Mathf.RoundToInt(fraction * 100f).ToString("000") + ".png";
                File.WriteAllBytes(path, png);
                captures.Add(new JObject { ["timeFraction"] = fraction, ["fixedSeed"] = 17, ["path"] = path, ["contentHash"] = Hash(png) });
            }
            var manifest = new JObject
            {
                ["schemaVersion"] = "mx.vfx.source-element-preview.v1", ["elementId"] = element.ElementId,
                ["sourceIndexHash"] = catalog.Index.IndexHash, ["curationHash"] = catalog.Curation.CurationHash,
                ["thumbnailPath"] = root + "/t050.png", ["captures"] = captures
            };
            File.WriteAllText(element.PreviewEvidencePath, manifest.ToString(Formatting.Indented), new UTF8Encoding(false));
            message = string.Empty; return true;
        }

        private static byte[] Capture(VfxSourceElementCatalog catalog, VfxSourceElementView element, float fraction)
        {
            GameObject root = VfxSourceElementInstantiation.Create(catalog, element, "Element Preview");
            if (root == null) throw new InvalidOperationException("无法实例化 Element：" + element.ElementId);
            var utility = new PreviewRenderUtility();
            Texture2D texture = null;
            try
            {
                utility.BeginStaticPreview(new Rect(0f, 0f, 320f, 320f));
                root.hideFlags = HideFlags.HideAndDontSave;
                utility.AddSingleGO(root);
                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                float duration = EstimateDuration(systems);
                for (int i = 0; i < systems.Length; i++)
                {
                    systems[i].useAutoRandomSeed = false; systems[i].randomSeed = unchecked(17u + (uint)i * 7919u);
                    systems[i].Simulate(duration * fraction, false, true, true);
                }
                Bounds bounds = CalculateBounds(root);
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                utility.camera.transform.position = bounds.center + new Vector3(0.65f, 0.35f, -1f).normalized * radius * 3.2f;
                utility.camera.transform.LookAt(bounds.center); utility.camera.nearClipPlane = 0.01f; utility.camera.farClipPlane = radius * 12f + 10f;
                utility.camera.clearFlags = CameraClearFlags.SolidColor; utility.camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f); utility.cameraFieldOfView = 32f;
                utility.ambientColor = new Color(0.25f, 0.28f, 0.32f); utility.Render(true, true); texture = utility.EndStaticPreview();
                if (texture == null) throw new InvalidOperationException("PreviewRenderUtility 未返回图像。");
                return texture.EncodeToPNG();
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
                utility.Cleanup();
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static float EstimateDuration(IEnumerable<ParticleSystem> systems)
        {
            float result = 0.5f;
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                result = Mathf.Max(result, main.loop ? 2f : main.startDelay.constantMax + Mathf.Min(main.duration, 1.5f) + main.startLifetime.constantMax);
            }
            return Mathf.Min(result, 4f);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 2f);
            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            if (result.extents.sqrMagnitude < 0.01f) result = new Bounds(Vector3.zero, Vector3.one * 2f);
            return result;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
