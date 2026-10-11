using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    internal enum VfxLibraryItemKind
    {
        Effect,
        Recipe,
        Primitive
    }

    internal abstract class VfxLibraryItem
    {
        public VfxLibraryItemKind Kind;
        public string StableId = string.Empty;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public string Backend = string.Empty;
        public string VisualMaturity = "LegacyPlaceholder";
        public string ReviewStatus = string.Empty;
        public string AssetPath = string.Empty;
        public string CostTier = "-";
        public bool IsExemplar;
        public string[] Roles = Array.Empty<string>();
        public string[] Themes = Array.Empty<string>();
        public string[] Tags = Array.Empty<string>();
        public Texture2D Thumbnail;

        public string PrimaryRole => Roles.Length > 0 ? Roles[0] : "Other";
        public string PrimaryTheme => Themes.Length > 0 ? Themes[0] : "generic";
        public string KindLabel => Kind == VfxLibraryItemKind.Effect ? "发布效果" : Kind == VfxLibraryItemKind.Recipe ? "配方" : "基础单元";
    }

    internal sealed class VfxLibraryEffect : VfxLibraryItem
    {
        // Raw source is retained only for the guarded Catalog draft session.
        public JObject Source;
        public string RecipeId = string.Empty;
        public string FallbackPolicy = "None";
        public int MaxConcurrent;
        public int DefinitionId;
        public string ResourceKey = string.Empty;
        public string AnchorSummary = "-";
        public string LifecycleSummary = "-";

        public string EffectId
        {
            get => StableId;
            set => StableId = value ?? string.Empty;
        }
    }

    internal sealed class VfxLibraryRecipeLayer
    {
        public string LayerId = string.Empty;
        public string PrimitiveId = string.Empty;
        public int Order;
        public string BindingSummary = "无";
        public string OverrideSummary = "无";
    }

    internal sealed class VfxLibraryParameter
    {
        public string ParameterId = string.Empty;
        public string ValueType = string.Empty;
        public string Unit = string.Empty;
        public string Transport = string.Empty;
        public string DefaultValue = "-";
        public string Range = "-";
        public string BackendProperty = string.Empty;
    }

    internal sealed class VfxLibraryRecipe : VfxLibraryItem
    {
        public string[] IncludedRecipeIds = Array.Empty<string>();
        public readonly List<VfxLibraryRecipeLayer> Layers = new List<VfxLibraryRecipeLayer>();
        public readonly List<VfxLibraryParameter> Parameters = new List<VfxLibraryParameter>();
        public readonly List<string> ConsumerRecipeIds = new List<string>();
        public readonly List<string> ConsumerEffectIds = new List<string>();

        public int ConsumerCount => ConsumerRecipeIds.Count + ConsumerEffectIds.Count;

        public string RecipeId
        {
            get => StableId;
            set => StableId = value ?? string.Empty;
        }
    }

    internal sealed class VfxLibraryPrimitive : VfxLibraryItem
    {
        public string[] CompatibleAnchors = Array.Empty<string>();
        public readonly List<VfxLibraryParameter> Parameters = new List<VfxLibraryParameter>();
        public readonly List<string> ConsumerRecipeIds = new List<string>();
        public readonly List<string> ConsumerEffectIds = new List<string>();

        public int ConsumerCount => ConsumerRecipeIds.Count + ConsumerEffectIds.Count;

        public string PrimitiveId
        {
            get => StableId;
            set => StableId = value ?? string.Empty;
        }
    }

    internal sealed class VfxLibraryReuseRecommendation
    {
        public VfxLibraryItem Item;
        public int Score;
        public string Reason = string.Empty;
    }

    internal sealed class VfxLibraryReuseGap
    {
        public string Role = string.Empty;
        public int EffectCount;
        public int ReadyReusableCount;
        public int ReviewCandidateCount;
        public string Reason = string.Empty;
    }

    internal sealed class VfxLibraryWorkspace : IDisposable
    {
        private const string ResourceCatalogPath = "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";

        public readonly List<VfxLibraryEffect> Effects = new List<VfxLibraryEffect>();
        public readonly List<VfxLibraryRecipe> RecipeItems = new List<VfxLibraryRecipe>();
        public readonly List<VfxLibraryPrimitive> PrimitiveItems = new List<VfxLibraryPrimitive>();
        public readonly List<VfxLibraryItem> Items = new List<VfxLibraryItem>();
        public readonly SortedDictionary<string, int> RoleCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> Roles = new List<string> { "All" };
        public readonly List<string> Themes = new List<string> { "All" };
        public readonly List<string> Backends = new List<string> { "All" };
        public readonly List<string> Reviews = new List<string> { "All" };
        public readonly Dictionary<string, VfxLibraryRecipe> Recipes = new Dictionary<string, VfxLibraryRecipe>(StringComparer.Ordinal);
        public readonly Dictionary<string, VfxLibraryPrimitive> Primitives = new Dictionary<string, VfxLibraryPrimitive>(StringComparer.Ordinal);
        public readonly List<VfxLibraryReuseGap> ReuseGaps = new List<VfxLibraryReuseGap>();

        public VfxLibraryDraftSession Draft { get; private set; }
        public VfxLibraryItem SelectedItem { get; private set; }
        public VfxLibraryEffect Selected => SelectedItem as VfxLibraryEffect;
        public bool StrictCoverage;
        public int DiagnosticCount;
        public int FavoriteCount;
        public int NeedsReviewCount;
        public int HighBudgetCount;
        public int ThumbnailCount;
        public int ExemplarCount;
        public int LegacyPlaceholderCount;
        public int CandidateCount;
        public int ProductionCount;
        public int ProductionEffectCount;
        public string CatalogHash { get; private set; } = string.Empty;
        public string SchemaVersion { get; private set; } = string.Empty;
        public int RecipeCount => RecipeItems.Count;
        public int PrimitiveCount => PrimitiveItems.Count;

        private JObject _catalog;

        /// <remarks>Editor-only, LowFreqAlloc.</remarks>
        public static VfxLibraryWorkspace CreateUnavailable()
        {
            return new VfxLibraryWorkspace();
        }

        /// <remarks>Editor-only, LowFreqAlloc.</remarks>
        public static VfxLibraryWorkspace Load(string catalogPath, string thumbnailRoot)
        {
            var workspace = new VfxLibraryWorkspace();
            byte[] catalogBytes = File.ReadAllBytes(catalogPath);
            workspace.CatalogHash = Hash(catalogBytes);
            workspace._catalog = JObject.Parse(Encoding.UTF8.GetString(catalogBytes));
            workspace.SchemaVersion = workspace._catalog.Value<string>("schemaVersion") ?? string.Empty;
            workspace.StrictCoverage = workspace._catalog.Value<bool?>("strictCoverage") ?? false;

            Dictionary<string, string> resourcePaths = LoadResourcePaths();
            workspace.LoadPrimitives();
            workspace.LoadRecipes();
            workspace.ResolveRecipeSemantics();
            workspace.LoadEffects(resourcePaths, thumbnailRoot);
            workspace.ResolveConsumers();
            workspace.BuildIndexes();
            workspace.Draft = new VfxLibraryDraftSession(workspace._catalog, catalogPath);
            return workspace;
        }

        public void Select(VfxLibraryItem item)
        {
            SelectedItem = item;
        }

        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public List<VfxLibraryItem> Filter(
            VfxLibraryItemKind kind,
            string search,
            string role,
            string theme,
            string backend,
            string review,
            bool highBudgetOnly)
        {
            var result = new List<VfxLibraryItem>();
            for (int i = 0; i < Items.Count; i++)
            {
                VfxLibraryItem item = Items[i];
                if (item.Kind != kind || !MatchesFilters(item, search, role, theme, backend, review) ||
                    (highBudgetOnly && item.CostTier != "High"))
                    continue;
                result.Add(item);
            }
            return result;
        }

        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public List<VfxLibraryItem> FilterSpecial(
            string quickFilter,
            string search,
            string role,
            string theme,
            string backend,
            string review)
        {
            var result = new List<VfxLibraryItem>();
            for (int i = 0; i < Items.Count; i++)
            {
                VfxLibraryItem item = Items[i];
                bool include = quickFilter == "exemplars"
                    ? item.IsExemplar
                    : quickFilter == "gaps" && IsReuseGapItem(item);
                if (include && MatchesFilters(item, search, role, theme, backend, review))
                    result.Add(item);
            }
            return result;
        }

        /// <remarks>Editor-only, AllocByDesign.</remarks>
        public List<VfxLibraryReuseRecommendation> FindReuseRecommendations(string query)
        {
            string[] tokens = (query ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '\t', '\r', '\n', ',', '，', '/', '→' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<VfxLibraryReuseRecommendation>();
            for (int i = 0; i < Items.Count; i++)
            {
                VfxLibraryItem item = Items[i];
                string haystack = (item.StableId + " " + item.DisplayName + " " + string.Join(" ", item.Roles) + " " + string.Join(" ", item.Themes) + " " + string.Join(" ", item.Tags)).ToLowerInvariant();
                int matches = tokens.Count(token => token.Length > 1 && haystack.Contains(token));
                bool reusable = item.ReviewStatus == "Ready" && item.VisualMaturity == "Production";
                if (matches == 0 && !reusable) continue;
                int score = (reusable ? 1000 : 0) + matches * 100 + (item.IsExemplar && reusable ? 150 : 0) + (item.Kind == VfxLibraryItemKind.Effect ? 20 : item.Kind == VfxLibraryItemKind.Recipe ? 15 : 10);
                string readiness = reusable ? "Production 已就绪样板" : VfxLibraryLabels.Display(item.VisualMaturity) + "合同/评审证据";
                result.Add(new VfxLibraryReuseRecommendation { Item = item, Score = score, Reason = readiness + " · " + VfxLibraryLabels.Display(item.PrimaryRole) + " · " + VfxLibraryLabels.Display(item.PrimaryTheme) });
            }
            result.Sort((a, b) => { int score = b.Score.CompareTo(a.Score); return score != 0 ? score : string.Compare(a.Item.StableId, b.Item.StableId, StringComparison.Ordinal); });
            if (result.Count > 12) result.RemoveRange(12, result.Count - 12);
            return result;
        }

        public int Count(VfxLibraryItemKind kind)
        {
            return kind == VfxLibraryItemKind.Effect ? Effects.Count : kind == VfxLibraryItemKind.Recipe ? RecipeItems.Count : PrimitiveItems.Count;
        }

        public bool IsReuseGapItem(VfxLibraryItem item)
        {
            for (int roleIndex = 0; roleIndex < item.Roles.Length; roleIndex++)
                for (int gapIndex = 0; gapIndex < ReuseGaps.Count; gapIndex++)
                    if (string.Equals(item.Roles[roleIndex], ReuseGaps[gapIndex].Role, StringComparison.Ordinal))
                        return true;
            return false;
        }

        public void Dispose()
        {
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i].Thumbnail != null)
                    UnityEngine.Object.DestroyImmediate(Effects[i].Thumbnail);
        }

        private void LoadPrimitives()
        {
            foreach (JObject source in (_catalog["primitives"] as JArray ?? new JArray()).Children<JObject>())
            {
                JObject assetRef = source["assetRef"] as JObject;
                var primitive = new VfxLibraryPrimitive
                {
                    Kind = VfxLibraryItemKind.Primitive,
                    PrimitiveId = source.Value<string>("primitiveId"),
                    DisplayName = source.Value<string>("displayName") ?? string.Empty,
                    Description = source.Value<string>("description") ?? string.Empty,
                    Backend = source.Value<string>("backend") ?? string.Empty,
                    VisualMaturity = source.Value<string>("visualMaturity") ?? "LegacyPlaceholder",
                    ReviewStatus = source.Value<string>("reviewStatus") ?? string.Empty,
                    IsExemplar = source.Value<bool?>("exemplar") ?? false,
                    AssetPath = assetRef?.Value<string>("path") ?? string.Empty,
                    Roles = Values(source["roles"]),
                    Themes = ThemesFrom(source["tags"]),
                    Tags = Values(source["tags"]),
                    CompatibleAnchors = Values(source["compatibleAnchors"])
                };
                LoadParameters(source["parameters"], primitive.Parameters);
                PrimitiveItems.Add(primitive);
                Primitives[primitive.PrimitiveId] = primitive;
            }
        }

        private void LoadRecipes()
        {
            foreach (JObject source in (_catalog["recipes"] as JArray ?? new JArray()).Children<JObject>())
            {
                var recipe = new VfxLibraryRecipe
                {
                    Kind = VfxLibraryItemKind.Recipe,
                    RecipeId = source.Value<string>("recipeId"),
                    DisplayName = source.Value<string>("displayName") ?? string.Empty,
                    Description = source.Value<string>("description") ?? string.Empty,
                    VisualMaturity = source.Value<string>("visualMaturity") ?? "LegacyPlaceholder",
                    ReviewStatus = source.Value<string>("reviewStatus") ?? string.Empty,
                    IsExemplar = source.Value<bool?>("exemplar") ?? false,
                    Tags = Values(source["tags"]),
                    Themes = ThemesFrom(source["tags"]),
                    IncludedRecipeIds = Values(source["includedRecipeIds"])
                };
                LoadParameters(source["parameters"], recipe.Parameters);
                foreach (JObject layerSource in (source["layers"] as JArray ?? new JArray()).Children<JObject>())
                {
                    recipe.Layers.Add(new VfxLibraryRecipeLayer
                    {
                        LayerId = layerSource.Value<string>("layerId") ?? string.Empty,
                        PrimitiveId = layerSource.Value<string>("primitiveId") ?? string.Empty,
                        Order = layerSource.Value<int?>("order") ?? 0,
                        BindingSummary = ObjectSummary(layerSource["bindings"] as JObject),
                        OverrideSummary = ObjectSummary(layerSource["overrides"] as JObject)
                    });
                }
                recipe.Layers.Sort((a, b) => a.Order.CompareTo(b.Order));
                RecipeItems.Add(recipe);
                Recipes[recipe.RecipeId] = recipe;
            }
        }

        private void ResolveRecipeSemantics()
        {
            for (int i = 0; i < RecipeItems.Count; i++)
            {
                VfxLibraryRecipe recipe = RecipeItems[i];
                var roles = new List<string>();
                var backends = new List<string>();
                for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
                {
                    if (!Primitives.TryGetValue(recipe.Layers[layerIndex].PrimitiveId, out VfxLibraryPrimitive primitive))
                        continue;
                    AddUnique(roles, primitive.Roles);
                    AddUnique(backends, primitive.Backend);
                    if (string.IsNullOrEmpty(recipe.AssetPath))
                        recipe.AssetPath = primitive.AssetPath;
                }
                recipe.Roles = roles.Count == 0 ? new[] { "Other" } : roles.ToArray();
                recipe.Backend = backends.Count == 0 ? "Unknown" : backends.Count == 1 ? backends[0] : "Mixed";
            }
        }

        private void LoadEffects(Dictionary<string, string> resourcePaths, string thumbnailRoot)
        {
            foreach (JObject source in (_catalog["effects"] as JArray ?? new JArray()).Children<JObject>())
            {
                JObject key = source["resourceKey"] as JObject;
                JObject quality = source["quality"] as JObject;
                JObject budget = source["budget"] as JObject;
                string resourceKey = (key?.Value<string>("packageId") ?? string.Empty) + "|" + (key?.Value<string>("id") ?? string.Empty);
                var effect = new VfxLibraryEffect
                {
                    Kind = VfxLibraryItemKind.Effect,
                    Source = source,
                    EffectId = source.Value<string>("effectId"),
                    DisplayName = source.Value<string>("displayName") ?? string.Empty,
                    Description = source.Value<string>("description") ?? string.Empty,
                    Backend = source.Value<string>("backend") ?? string.Empty,
                    VisualMaturity = source.Value<string>("visualMaturity") ?? "LegacyPlaceholder",
                    ReviewStatus = source.Value<string>("reviewStatus") ?? string.Empty,
                    IsExemplar = source.Value<bool?>("exemplar") ?? false,
                    RecipeId = source.Value<string>("recipeId") ?? string.Empty,
                    Roles = Values(source["roles"]),
                    Themes = Values(source["themeIds"]),
                    Tags = Values(source["tags"]),
                    CostTier = quality?.Value<string>("costTier") ?? "-",
                    FallbackPolicy = quality?.Value<string>("fallbackPolicy") ?? "None",
                    MaxConcurrent = budget?.Value<int?>("recommendedMaxConcurrent") ?? 0,
                    DefinitionId = source.Value<int?>("definitionId") ?? 0,
                    ResourceKey = resourceKey,
                    AnchorSummary = ObjectSummary(source["anchorContract"] as JObject),
                    LifecycleSummary = ObjectSummary(source["lifecycle"] as JObject)
                };
                resourcePaths.TryGetValue(resourceKey, out effect.AssetPath);
                TryLoadThumbnail(effect, thumbnailRoot);
                Effects.Add(effect);
            }
        }

        private void ResolveConsumers()
        {
            for (int recipeIndex = 0; recipeIndex < RecipeItems.Count; recipeIndex++)
            {
                VfxLibraryRecipe recipe = RecipeItems[recipeIndex];
                for (int includeIndex = 0; includeIndex < recipe.IncludedRecipeIds.Length; includeIndex++)
                    if (Recipes.TryGetValue(recipe.IncludedRecipeIds[includeIndex], out VfxLibraryRecipe included))
                        AddUnique(included.ConsumerRecipeIds, recipe.RecipeId);

                var primitiveIds = new List<string>();
                CollectPrimitiveIds(recipe, primitiveIds, new HashSet<string>(StringComparer.Ordinal));
                for (int primitiveIndex = 0; primitiveIndex < primitiveIds.Count; primitiveIndex++)
                    if (Primitives.TryGetValue(primitiveIds[primitiveIndex], out VfxLibraryPrimitive primitive))
                        AddUnique(primitive.ConsumerRecipeIds, recipe.RecipeId);
            }

            for (int effectIndex = 0; effectIndex < Effects.Count; effectIndex++)
            {
                VfxLibraryEffect effect = Effects[effectIndex];
                if (string.IsNullOrEmpty(effect.RecipeId) || !Recipes.TryGetValue(effect.RecipeId, out VfxLibraryRecipe rootRecipe))
                    continue;
                var recipeIds = new List<string>();
                CollectRecipeIds(rootRecipe, recipeIds, new HashSet<string>(StringComparer.Ordinal));
                for (int recipeIndex = 0; recipeIndex < recipeIds.Count; recipeIndex++)
                    if (Recipes.TryGetValue(recipeIds[recipeIndex], out VfxLibraryRecipe recipe))
                        AddUnique(recipe.ConsumerEffectIds, effect.EffectId);
                var primitiveIds = new List<string>();
                CollectPrimitiveIds(rootRecipe, primitiveIds, new HashSet<string>(StringComparer.Ordinal));
                for (int primitiveIndex = 0; primitiveIndex < primitiveIds.Count; primitiveIndex++)
                    if (Primitives.TryGetValue(primitiveIds[primitiveIndex], out VfxLibraryPrimitive primitive))
                        AddUnique(primitive.ConsumerEffectIds, effect.EffectId);
            }

            for (int i = 0; i < RecipeItems.Count; i++)
            {
                RecipeItems[i].ConsumerRecipeIds.Sort(StringComparer.Ordinal);
                RecipeItems[i].ConsumerEffectIds.Sort(StringComparer.Ordinal);
            }
            for (int i = 0; i < PrimitiveItems.Count; i++)
            {
                PrimitiveItems[i].ConsumerRecipeIds.Sort(StringComparer.Ordinal);
                PrimitiveItems[i].ConsumerEffectIds.Sort(StringComparer.Ordinal);
            }
        }

        private void TryLoadThumbnail(VfxLibraryEffect effect, string thumbnailRoot)
        {
            string path = thumbnailRoot + "/" + effect.EffectId.Replace('.', '_') + ".png";
            if (!File.Exists(path))
                return;
            effect.Thumbnail = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "VFX Thumbnail - " + effect.EffectId,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (effect.Thumbnail.LoadImage(File.ReadAllBytes(path), true))
            {
                ThumbnailCount++;
                return;
            }
            UnityEngine.Object.DestroyImmediate(effect.Thumbnail);
            effect.Thumbnail = null;
        }

        private void BuildIndexes()
        {
            PrimitiveItems.Sort((a, b) => string.Compare(a.PrimitiveId, b.PrimitiveId, StringComparison.Ordinal));
            RecipeItems.Sort((a, b) => string.Compare(a.RecipeId, b.RecipeId, StringComparison.Ordinal));
            Effects.Sort((a, b) => string.Compare(a.EffectId, b.EffectId, StringComparison.Ordinal));
            Items.AddRange(Effects);
            Items.AddRange(RecipeItems);
            Items.AddRange(PrimitiveItems);

            for (int i = 0; i < Items.Count; i++)
            {
                VfxLibraryItem item = Items[i];
                AddUnique(Roles, item.Roles);
                AddUnique(Themes, item.Themes);
                AddUnique(Backends, item.Backend);
                AddUnique(Reviews, item.ReviewStatus);
                if (item.ReviewStatus == "ReviewNeeded") NeedsReviewCount++;
                if (item.CostTier == "High") HighBudgetCount++;
                if (item.IsExemplar) ExemplarCount++;
                if (item.VisualMaturity == "Production") ProductionCount++;
                else if (item.VisualMaturity == "Candidate") CandidateCount++;
                else LegacyPlaceholderCount++;
            }
            for (int i = 0; i < Effects.Count; i++) if (Effects[i].VisualMaturity == "Production") ProductionEffectCount++;
            for (int i = 0; i < Effects.Count; i++)
            {
                string role = Effects[i].PrimaryRole;
                RoleCounts.TryGetValue(role, out int count);
                RoleCounts[role] = count + 1;
            }
            SortChoices(Roles);
            SortChoices(Themes);
            SortChoices(Backends);
            SortChoices(Reviews);
            BuildReuseGaps();
        }

        private void BuildReuseGaps()
        {
            foreach (KeyValuePair<string, int> pair in RoleCounts)
            {
                int ready = 0;
                int review = 0;
                for (int itemIndex = 0; itemIndex < Items.Count; itemIndex++)
                {
                    VfxLibraryItem item = Items[itemIndex];
                    if (!Contains(item.Roles, pair.Key))
                        continue;
                    if (item.ReviewStatus == "Ready" && item.VisualMaturity == "Production") ready++;
                    else review++;
                }
                if (ready > 0)
                    continue;
                ReuseGaps.Add(new VfxLibraryReuseGap
                {
                    Role = pair.Key,
                    EffectCount = pair.Value,
                    ReadyReusableCount = 0,
                    ReviewCandidateCount = review,
                    Reason = pair.Value + " 个发布效果尚无已就绪复用入口"
                });
            }
            ReuseGaps.Sort((left, right) =>
            {
                int count = right.EffectCount.CompareTo(left.EffectCount);
                return count != 0 ? count : string.Compare(left.Role, right.Role, StringComparison.Ordinal);
            });
        }

        private void CollectRecipeIds(VfxLibraryRecipe recipe, List<string> output, HashSet<string> visited)
        {
            if (recipe == null || !visited.Add(recipe.RecipeId))
                return;
            AddUnique(output, recipe.RecipeId);
            for (int i = 0; i < recipe.IncludedRecipeIds.Length; i++)
                if (Recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxLibraryRecipe included))
                    CollectRecipeIds(included, output, visited);
        }

        private void CollectPrimitiveIds(VfxLibraryRecipe recipe, List<string> output, HashSet<string> visited)
        {
            if (recipe == null || !visited.Add(recipe.RecipeId))
                return;
            for (int i = 0; i < recipe.Layers.Count; i++)
                AddUnique(output, recipe.Layers[i].PrimitiveId);
            for (int i = 0; i < recipe.IncludedRecipeIds.Length; i++)
                if (Recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxLibraryRecipe included))
                    CollectPrimitiveIds(included, output, visited);
        }

        private static Dictionary<string, string> LoadResourcePaths()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(ResourceCatalogPath))
                return result;
            JObject resources = JObject.Parse(File.ReadAllText(ResourceCatalogPath, Encoding.UTF8));
            string package = resources.Value<string>("packageId") ?? string.Empty;
            foreach (JObject entry in (resources["entries"] as JArray ?? new JArray()).Children<JObject>())
            {
                string key = (entry.Value<string>("packageId") ?? package) + "|" + (entry.Value<string>("id") ?? string.Empty);
                result[key] = (entry["providerData"] as JObject)?.Value<string>("assetPath") ?? string.Empty;
            }
            return result;
        }

        private static void LoadParameters(JToken token, List<VfxLibraryParameter> destination)
        {
            foreach (JObject source in (token as JArray ?? new JArray()).Children<JObject>())
            {
                string min = Scalar(source["minValue"]);
                string max = Scalar(source["maxValue"]);
                destination.Add(new VfxLibraryParameter
                {
                    ParameterId = source.Value<string>("parameterId") ?? string.Empty,
                    ValueType = source.Value<string>("valueType") ?? string.Empty,
                    Unit = source.Value<string>("unit") ?? string.Empty,
                    Transport = source.Value<string>("transport") ?? string.Empty,
                    DefaultValue = Scalar(source["defaultValue"]),
                    Range = min == "-" && max == "-" ? "-" : min + " … " + max,
                    BackendProperty = source.Value<string>("backendProperty") ?? string.Empty
                });
            }
        }

        private static string[] ThemesFrom(JToken token)
        {
            string[] tags = Values(token);
            var result = new List<string>();
            string[] known = { "generic", "fire", "poison", "wind", "arcane", "lightning", "physical" };
            for (int i = 0; i < tags.Length; i++)
                if (Contains(known, tags[i]))
                    AddUnique(result, tags[i]);
            if (result.Count == 0)
                result.Add("generic");
            return result.ToArray();
        }

        private static bool MatchesSearch(VfxLibraryItem item, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            if (ContainsText(item.StableId, search) || ContainsText(item.DisplayName, search) || ContainsText(item.Description, search))
                return true;
            for (int i = 0; i < item.Tags.Length; i++)
                if (ContainsText(item.Tags[i], search))
                    return true;
            return false;
        }

        private static bool MatchesFilters(VfxLibraryItem item, string search, string role, string theme, string backend, string review)
        {
            return MatchesSearch(item, search) && Match(role, item.Roles) && Match(theme, item.Themes) &&
                   Match(backend, item.Backend) && Match(review, item.ReviewStatus);
        }

        private static bool ContainsText(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Match(string filter, string value)
        {
            return string.IsNullOrEmpty(filter) || filter == "All" || filter == value;
        }

        private static bool Match(string filter, string[] values)
        {
            return string.IsNullOrEmpty(filter) || filter == "All" || Contains(values, filter);
        }

        private static bool Contains(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string[] Values(JToken token)
        {
            return token is JArray array ? array.Values<string>().Where(value => value != null).ToArray() : Array.Empty<string>();
        }

        private static string ObjectSummary(JObject value)
        {
            return value == null || !value.Properties().Any() ? "无" : value.ToString(Formatting.None);
        }

        private static string Scalar(JToken value)
        {
            return value == null || value.Type == JTokenType.Null ? "-" : value.ToString(Formatting.None);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
                values.Add(value);
        }

        private static void AddUnique(List<string> values, string[] source)
        {
            for (int i = 0; i < source.Length; i++)
                AddUnique(values, source[i]);
        }

        private static void SortChoices(List<string> values)
        {
            if (values.Count > 1)
                values.Sort(1, values.Count - 1, StringComparer.Ordinal);
        }

        private static string Hash(byte[] data)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class VfxLibraryDraftSession
    {
        private readonly JObject _catalog;
        private readonly string _originalHash;
        private JObject _draftEffect;
        private string _effectId;
        private byte[] _preparedCatalog;
        private readonly Func<string, VfxCatalogAssetPublishTransaction> _publishTransactionFactory;

        public bool HasChanges => _draftEffect != null;
        public string DiffText { get; private set; } = "没有草稿变更。";

        public VfxLibraryDraftSession(JObject catalog, string path)
            : this(catalog, path, null)
        {
        }

        internal VfxLibraryDraftSession(
            JObject catalog,
            string path,
            Func<string, VfxCatalogAssetPublishTransaction> publishTransactionFactory)
        {
            _catalog = catalog;
            _originalHash = Hash(File.ReadAllBytes(path));
            _publishTransactionFactory = publishTransactionFactory
                ?? (catalogPath => new VfxCatalogAssetPublishTransaction(catalogPath));
        }

        public void Capture(VfxLibraryEffect effect)
        {
            _effectId = effect.EffectId;
            _draftEffect = (JObject)effect.Source.DeepClone();
            _draftEffect["reviewStatus"] = effect.ReviewStatus;
            _draftEffect["description"] = effect.Description;
            _preparedCatalog = null;
            DiffText = BuildDiff(effect.Source, _draftEffect, effect.EffectId);
            if (!string.Equals(DiffText, "没有草稿变更。", StringComparison.Ordinal))
                return;
            _draftEffect = null;
            _effectId = string.Empty;
        }

        /// <remarks>Editor-only, AllocByDesign. Starts the authoritative CLI dry-run.</remarks>
        public bool TryPrepareApply(string path, out string planSummary, out string message)
        {
            planSummary = string.Empty;
            _preparedCatalog = null;
            if (_draftEffect == null)
            {
                message = "没有可应用的草稿。";
                return false;
            }
            if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != _originalHash)
            {
                message = "工作台加载后语义目录已发生变化，请刷新后重试。";
                return false;
            }

            JObject candidate = CreateCandidate();
            string serialized = candidate.ToString(Formatting.Indented) + Environment.NewLine;
            string temp = Path.Combine("Temp", "MxFramework", "VfxAuthoring", "workspace_candidate.json");
            Directory.CreateDirectory(Path.GetDirectoryName(temp));
            File.WriteAllText(temp, serialized, new UTF8Encoding(false));
            string arguments = "run -c Release --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- vfx plan-save --project-root . --proposed \"" + temp.Replace('\\', '/') + "\" --expected-hash " + _originalHash;
            var start = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process process = Process.Start(start);
            if (process == null)
            {
                message = "无法启动权威 VFX 语义目录试运行。";
                return false;
            }
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                message = "权威试运行阻止了本次变更。\n" + output + error;
                return false;
            }

            JObject plan;
            try
            {
                plan = JObject.Parse(output);
            }
            catch (JsonException exception)
            {
                message = "权威试运行返回了无效结果。\n" + exception.Message + "\n" + output + error;
                return false;
            }
            if (plan.Value<bool?>("blocked") == true || plan.Value<bool?>("wouldModifyCatalog") != true ||
                plan.Value<bool?>("wouldModifyUnityAssets") == true || plan.Value<bool?>("wouldModifyPublishedOutput") == true ||
                plan.Value<bool?>("requiresUnityAuthoring") == true)
            {
                message = "权威试运行未产生可提交的仅语义目录计划。\n" + output;
                return false;
            }
            if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != _originalHash)
            {
                message = "语义目录在试运行后发生变化，请刷新后重试。";
                return false;
            }

            _preparedCatalog = new UTF8Encoding(false).GetBytes(serialized);
            planSummary = DiffText + "\n\n试运行：仅修改 VFX 创作语义目录\n预期哈希：" + _originalHash + "\n实际哈希：" + (plan.Value<string>("actualCatalogHash") ?? string.Empty);
            message = "权威试运行、校验和预期哈希检查已通过。";
            return true;
        }

        public bool CommitPrepared(string path, out string message)
        {
            if (_preparedCatalog == null)
            {
                message = "没有已验证的语义目录变更计划。";
                return false;
            }
            if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != _originalHash)
            {
                message = "语义目录在确认前发生变化，未写入任何内容。";
                _preparedCatalog = null;
                return false;
            }
            try
            {
                string preparedText = new UTF8Encoding(false, true).GetString(_preparedCatalog);
                JObject preparedCatalog = JObject.Parse(preparedText);
                using VfxCatalogAssetPublishTransaction transaction = _publishTransactionFactory(path);
                if (!transaction.Prepare(preparedCatalog, out message)
                    || !transaction.Commit(out message))
                    return false;
            }
            catch (Exception exception)
            {
                message = "VFX Catalog publish failed without changing the prepared plan: " + exception.Message;
                return false;
            }
            _preparedCatalog = null;
            message = "已应用经过试运行、校验和哈希防护的仅语义目录变更。";
            return true;
        }

        internal JObject CreateCandidate()
        {
            JObject candidate = (JObject)_catalog.DeepClone();
            JArray effects = candidate["effects"] as JArray ?? new JArray();
            for (int i = 0; i < effects.Count; i++)
                if (effects[i]?.Value<string>("effectId") == _effectId)
                    effects[i] = _draftEffect.DeepClone();
            return candidate;
        }

        private static string BuildDiff(JObject original, JObject draft, string effectId)
        {
            var lines = new List<string>();
            AddDiff(lines, "reviewStatus", original["reviewStatus"], draft["reviewStatus"]);
            AddDiff(lines, "description", original["description"], draft["description"]);
            return lines.Count == 0 ? "没有草稿变更。" : "发布效果 " + effectId + "\n" + string.Join("\n", lines);
        }

        private static void AddDiff(List<string> lines, string field, JToken original, JToken draft)
        {
            if (!JToken.DeepEquals(original, draft))
                lines.Add(field + ": " + OneLine(original) + " → " + OneLine(draft));
        }

        private static string OneLine(JToken value)
        {
            string text = value?.Type == JTokenType.String ? value.Value<string>() : value?.ToString(Formatting.None) ?? "null";
            return text.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Hash(byte[] data)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class VfxLibraryCardRow
    {
        public readonly List<VfxLibraryItem> Items = new List<VfxLibraryItem>();

        public VfxLibraryCardRow(List<VfxLibraryItem> source, int start, int count)
        {
            for (int i = start; i < source.Count && i < start + count; i++)
                Items.Add(source[i]);
        }
    }

    internal sealed class VfxLibraryEffectCard : VisualElement
    {
        private readonly Action<VfxLibraryItem> _select;
        private readonly Action<VfxLibraryItem> _open;
        private VfxLibraryItem _item;
        private readonly VisualElement _image;
        private readonly Label _thumbnailPlaceholder;
        private readonly Label _kind;
        private readonly Label _title;
        private readonly Label _id;
        private readonly Label _chips;
        private readonly Label _meta;

        public VfxLibraryEffectCard(Action<VfxLibraryItem> select, Action<VfxLibraryItem> open)
        {
            _select = select;
            _open = open;
            AddToClassList("effect-card");
            _image = new VisualElement();
            _image.AddToClassList("card-image");
            _kind = new Label();
            _kind.AddToClassList("card-kind");
            _thumbnailPlaceholder = new Label("缩略图未生成");
            _thumbnailPlaceholder.AddToClassList("thumbnail-placeholder");
            _image.Add(_kind);
            _image.Add(_thumbnailPlaceholder);
            Add(_image);
            _title = new Label();
            _title.AddToClassList("card-title");
            Add(_title);
            _id = new Label();
            _id.AddToClassList("card-id");
            Add(_id);
            _chips = new Label();
            _chips.AddToClassList("card-chips");
            Add(_chips);
            _meta = new Label();
            _meta.AddToClassList("card-meta");
            Add(_meta);
            focusable = true;
            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Bind(VfxLibraryItem item, VfxLibraryItem selected)
        {
            _item = item;
            style.display = item == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (item == null)
                return;
            EnableInClassList("selected", item == selected);
            EnableInClassList("recipe-card", item.Kind == VfxLibraryItemKind.Recipe);
            EnableInClassList("primitive-card", item.Kind == VfxLibraryItemKind.Primitive);
            EnableInClassList("exemplar-card", item.IsExemplar);
            EnableInClassList("placeholder-card", item.VisualMaturity == "LegacyPlaceholder");
            EnableInClassList("candidate-card", item.VisualMaturity == "Candidate");
            EnableInClassList("production-card", item.VisualMaturity == "Production");
            bool hasThumbnail = item.Thumbnail != null;
            _image.style.backgroundImage = hasThumbnail ? new StyleBackground(item.Thumbnail) : StyleKeyword.None;
            _thumbnailPlaceholder.style.display = hasThumbnail ? DisplayStyle.None : DisplayStyle.Flex;
            _thumbnailPlaceholder.text = item.Kind == VfxLibraryItemKind.Effect ? "缩略图未生成" : item.Kind == VfxLibraryItemKind.Recipe ? "有序组合配方" : "可复用基础单元";
            string maturity = item.VisualMaturity == "Production" ? "生产级" : item.VisualMaturity == "Candidate" ? "候选" : "占位待替换";
            _kind.text = item.KindLabel + " · " + maturity + (item.IsExemplar ? " · 样板" : string.Empty);
            _title.text = string.IsNullOrEmpty(item.DisplayName) ? item.StableId : item.DisplayName;
            _id.text = item.StableId;
            _chips.text = VfxLibraryLabels.Display(item.PrimaryRole) + "   " + VfxLibraryLabels.Display(item.PrimaryTheme);
            int consumers = item is VfxLibraryRecipe recipe ? recipe.ConsumerCount : item is VfxLibraryPrimitive primitive ? primitive.ConsumerCount : 0;
            _meta.text = VfxLibraryLabels.Display(item.Backend) + "     " + maturity + "     " + VfxLibraryLabels.Display(item.ReviewStatus) +
                         (item.Kind == VfxLibraryItemKind.Effect ? "     " + VfxLibraryLabels.Display(item.CostTier) : "     消费者 " + consumers);
            tooltip = item.Description;
        }

        private void OnClick(ClickEvent evt)
        {
            if (_item == null)
                return;
            if (evt.clickCount == 2)
                _open(_item);
            else
                _select(_item);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_item == null)
                return;
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.Space)
            {
                _select(_item);
                evt.StopPropagation();
            }
        }
    }

    internal static class VfxLibraryDetailBuilder
    {
        public static void Build(VisualElement host, string tab, VfxLibraryItem item, VfxLibraryWorkspace workspace)
        {
            host.Clear();
            if (item is VfxLibraryEffect effect)
                BuildEffect(host, tab, effect, workspace);
            else if (item is VfxLibraryRecipe recipe)
                BuildRecipe(host, tab, recipe, workspace);
            else if (item is VfxLibraryPrimitive primitive)
                BuildPrimitive(host, tab, primitive);
        }

        private static void BuildEffect(VisualElement host, string tab, VfxLibraryEffect effect, VfxLibraryWorkspace workspace)
        {
            if (tab == "组合")
            {
                Add(host, "发布效果", effect.DisplayName + " · " + VfxLibraryLabels.Display(effect.Backend));
                if (workspace.Recipes.TryGetValue(effect.RecipeId, out VfxLibraryRecipe recipe))
                {
                    Add(host, "↳ 配方", recipe.DisplayName + " · " + recipe.RecipeId);
                    AddRecipeLayers(host, recipe, workspace);
                }
                else
                    Add(host, "配方", "直接发布效果");
            }
            else if (tab == "依赖")
            {
                Add(host, "预制体", Empty(effect.AssetPath));
                Add(host, "ResourceKey", Empty(effect.ResourceKey));
                Add(host, "运行时定义", effect.DefinitionId == 0 ? "未绑定" : effect.DefinitionId.ToString());
                Add(host, "降级策略", VfxLibraryLabels.Display(effect.FallbackPolicy));
            }
            else if (tab == "参数")
            {
                if (workspace.Recipes.TryGetValue(effect.RecipeId, out VfxLibraryRecipe recipe))
                    AddParameters(host, recipe.Parameters);
                else
                    Add(host, "运行时参数", "没有配方参数合同");
                Add(host, "预览控制", "速度、循环和降级模式只影响当前编辑器会话");
            }
            else if (tab == "评审")
            {
                var choices = new List<string> { "草稿", "待评审", "已就绪", "已弃用" };
                var status = new DropdownField("评审状态", choices, VfxLibraryLabels.Display(effect.ReviewStatus));
                status.AddToClassList("review-field");
                status.RegisterValueChangedCallback(change => effect.ReviewStatus = VfxLibraryLabels.Raw(change.newValue));
                host.Add(status);
                var description = new TextField("评审说明") { value = effect.Description, multiline = true };
                description.AddToClassList("review-field");
                description.AddToClassList("review-description");
                description.RegisterValueChangedCallback(change => effect.Description = change.newValue);
                host.Add(description);
                Add(host, "治理流程", "保存草稿 → 权威校验 → 哈希防护 → 人工确认");
                Add(host, "已就绪边界", "自动生成、评分或迁移不能把资源提升为已就绪");
                Add(host, "样板标记", effect.IsExemplar ? "是；仅人工已就绪资源可进入正向样板索引" : "否");
                Add(host, "草稿差异", workspace.Draft.DiffText);
            }
            else
            {
                Add(host, "类型", "发布效果（运行时只加载完整预制体）");
                Add(host, "角色 / 主题", VfxLibraryLabels.Display(effect.PrimaryRole) + " / " + VfxLibraryLabels.Display(effect.PrimaryTheme));
                Add(host, "锚点", effect.AnchorSummary);
                Add(host, "生命周期", effect.LifecycleSummary);
                Add(host, "质量", VfxLibraryLabels.Display(effect.CostTier) + " · " + VfxLibraryLabels.Display(effect.FallbackPolicy));
                Add(host, "预算", "建议最大并发 " + effect.MaxConcurrent);
                Add(host, "样板", effect.IsExemplar ? "已标记" : "未标记");
                Add(host, "说明", Empty(effect.Description));
            }
        }

        private static void BuildRecipe(VisualElement host, string tab, VfxLibraryRecipe recipe, VfxLibraryWorkspace workspace)
        {
            if (tab == "组合")
            {
                Add(host, "配方", recipe.DisplayName + " · " + recipe.RecipeId);
                if (recipe.IncludedRecipeIds.Length > 0)
                    Add(host, "包含配方", string.Join(", ", recipe.IncludedRecipeIds));
                AddRecipeLayers(host, recipe, workspace);
            }
            else if (tab == "参数")
                AddParameters(host, recipe.Parameters);
            else if (tab == "依赖")
            {
                Add(host, "基础单元", recipe.Layers.Count == 0 ? "无" : string.Join(", ", recipe.Layers.Select(layer => layer.PrimitiveId)));
                Add(host, "包含它的配方", recipe.ConsumerRecipeIds.Count == 0 ? "无" : string.Join(", ", recipe.ConsumerRecipeIds));
                Add(host, "消费效果", recipe.ConsumerEffectIds.Count == 0 ? "无" : string.Join(", ", recipe.ConsumerEffectIds));
                Add(host, "预览资产", Empty(recipe.AssetPath));
            }
            else if (tab == "评审")
            {
                Add(host, "评审状态", VfxLibraryLabels.Display(recipe.ReviewStatus));
                Add(host, "传递门禁", "已就绪配方要求全部基础单元与包含配方均为已就绪");
                Add(host, "样板标记", recipe.IsExemplar ? "是；仍需满足已就绪闭包" : "否");
                Add(host, "编辑边界", "当前资源库对配方只读；修改需走受控语义目录计划");
            }
            else
            {
                Add(host, "类型", "配方（创作阶段有序组合，运行时不动态拼装）");
                Add(host, "角色 / 主题", VfxLibraryLabels.Display(recipe.PrimaryRole) + " / " + VfxLibraryLabels.Display(recipe.PrimaryTheme));
                Add(host, "后端", VfxLibraryLabels.Display(recipe.Backend));
                Add(host, "层数", recipe.Layers.Count.ToString());
                Add(host, "反向消费者", recipe.ConsumerCount + " 个");
                Add(host, "样板", recipe.IsExemplar ? "已标记" : "未标记");
                Add(host, "说明", Empty(recipe.Description));
            }
        }

        private static void BuildPrimitive(VisualElement host, string tab, VfxLibraryPrimitive primitive)
        {
            if (tab == "组合")
            {
                Add(host, "基础单元", primitive.DisplayName + " · " + primitive.PrimitiveId);
                Add(host, "技术后端", VfxLibraryLabels.Display(primitive.Backend));
                Add(host, "兼容锚点", primitive.CompatibleAnchors.Length == 0 ? "未声明" : string.Join(", ", primitive.CompatibleAnchors.Select(VfxLibraryLabels.Display)));
            }
            else if (tab == "参数")
                AddParameters(host, primitive.Parameters);
            else if (tab == "依赖")
            {
                Add(host, "技术资产", Empty(primitive.AssetPath));
                Add(host, "参数后端", primitive.Parameters.Count == 0 ? "无" : string.Join(", ", primitive.Parameters.Select(parameter => parameter.BackendProperty)));
                Add(host, "消费配方", primitive.ConsumerRecipeIds.Count == 0 ? "无" : string.Join(", ", primitive.ConsumerRecipeIds));
                Add(host, "消费效果", primitive.ConsumerEffectIds.Count == 0 ? "无" : string.Join(", ", primitive.ConsumerEffectIds));
            }
            else if (tab == "评审")
            {
                Add(host, "评审状态", VfxLibraryLabels.Display(primitive.ReviewStatus));
                Add(host, "晋升门禁", "单一视觉职责、参数最小值/默认值/最大值扫测，并进入至少两个配方候选短名单");
                Add(host, "样板标记", primitive.IsExemplar ? "是；仍需人工已就绪审批" : "否");
                Add(host, "编辑边界", "当前资源库对基础单元只读；生成候选必须先进入隔离沙箱");
            }
            else
            {
                Add(host, "类型", "基础单元（可复用基础视觉职责）");
                Add(host, "角色 / 主题", VfxLibraryLabels.Display(primitive.PrimaryRole) + " / " + VfxLibraryLabels.Display(primitive.PrimaryTheme));
                Add(host, "后端", VfxLibraryLabels.Display(primitive.Backend));
                Add(host, "参数合同", primitive.Parameters.Count + " 项");
                Add(host, "反向消费者", primitive.ConsumerCount + " 个");
                Add(host, "样板", primitive.IsExemplar ? "已标记" : "未标记");
                Add(host, "说明", Empty(primitive.Description));
            }
        }

        private static void AddRecipeLayers(VisualElement host, VfxLibraryRecipe recipe, VfxLibraryWorkspace workspace)
        {
            if (recipe.Layers.Count == 0)
            {
                Add(host, "组合层", "无");
                return;
            }
            for (int i = 0; i < recipe.Layers.Count; i++)
            {
                VfxLibraryRecipeLayer layer = recipe.Layers[i];
                string primitiveName = workspace.Primitives.TryGetValue(layer.PrimitiveId, out VfxLibraryPrimitive primitive)
                    ? primitive.DisplayName
                    : layer.PrimitiveId;
                Add(host, "↳ " + layer.Order + " · " + layer.LayerId, primitiveName + "\n覆盖：" + layer.OverrideSummary + "\n绑定：" + layer.BindingSummary);
            }
        }

        private static void AddParameters(VisualElement host, List<VfxLibraryParameter> parameters)
        {
            if (parameters.Count == 0)
            {
                Add(host, "参数合同", "无公开参数");
                return;
            }
            for (int i = 0; i < parameters.Count; i++)
            {
                VfxLibraryParameter parameter = parameters[i];
                Add(host, parameter.ParameterId, parameter.ValueType + " · " + VfxLibraryLabels.Display(parameter.Transport) + " · 默认 " + parameter.DefaultValue + " · 范围 " + parameter.Range + "\n" + Empty(parameter.BackendProperty));
            }
        }

        private static void Add(VisualElement host, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("detail-row");
            var key = new Label(label);
            key.AddToClassList("detail-key");
            var text = new Label(WrapLongDetail(value)) { tooltip = value };
            text.AddToClassList("detail-value");
            row.Add(key);
            row.Add(text);
            host.Add(row);
        }

        private static string Empty(string value)
        {
            return string.IsNullOrEmpty(value) ? "未绑定" : value;
        }

        private static string WrapLongDetail(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value
                .Replace("/", "/\u200B")
                .Replace("\\", "\\\u200B")
                .Replace("|", "|\u200B")
                .Replace(".", ".\u200B");
        }
    }

    internal static class VfxLibraryLabels
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "All", "全部" }, { "Aura", "光环" }, { "Beam", "光束" }, { "Charge", "蓄力" }, { "Cast", "施放" },
            { "Flight", "飞行" }, { "Impact", "命中" }, { "Telegraph", "预警" }, { "Status", "状态" }, { "Zone", "区域" },
            { "Environment", "环境" }, { "Transition", "过渡" }, { "Other", "其他" }, { "ParticleSystem", "粒子系统" },
            { "LineRenderer", "线渲染器" }, { "CustomView", "自定义表现" }, { "VfxGraph", "VFX Graph" }, { "Mixed", "混合后端" },
            { "Unknown", "未知" }, { "Draft", "草稿" }, { "ReviewNeeded", "待评审" }, { "Ready", "已就绪" },
            { "LegacyPlaceholder", "旧占位" }, { "Candidate", "创作候选" }, { "Production", "Production" },
            { "Deprecated", "已弃用" }, { "Low", "低" }, { "Medium", "中" }, { "High", "高" }, { "None", "无" },
            { "UseFallbackEffect", "使用降级效果" }, { "UseResourceVariant", "使用资源变体" }, { "generic", "通用" },
            { "fire", "火焰" }, { "poison", "毒素" }, { "wind", "风" }, { "arcane", "奥术" }, { "lightning", "闪电" },
            { "physical", "物理" }, { "RuntimeSlot", "运行时插槽" }, { "StaticBake", "静态烘焙" }, { "PreviewOnly", "仅预览" },
            { "Path", "路径" }, { "World", "世界" }, { "Character", "角色" }, { "Target", "目标" }
        };

        public static string Display(string value)
        {
            return value != null && Values.TryGetValue(value, out string text) ? text : value ?? string.Empty;
        }

        public static string Raw(string value)
        {
            foreach (KeyValuePair<string, string> pair in Values)
                if (pair.Value == value)
                    return pair.Key;
            return value ?? string.Empty;
        }
    }
}
