using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;
namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static AudioGenerationManifestDocument UpgradeGenerationManifest(AudioGenerationManifestDocument document)
    {
        document ??= new AudioGenerationManifestDocument();
        document.Format = "mx.audioGenerationManifest.v3";
        document.SchemaVersion = "3.0";
        document.Recipes ??= new List<AudioGenerationRecipeV2>();
        document.Entries ??= new List<AudioGenerationManifestEntry>();
        document.AdoptionSets ??= new List<AudioAdoptionSetV3>();
        document.EventStates ??= new List<AudioAdoptionEventStateV3>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        document.Recipes = document.Recipes.Where(item => item != null && !string.IsNullOrWhiteSpace(item.AudioCueId) && seen.Add(item.AudioCueId)).ToList();
        for (int i = 0; i < document.Recipes.Count; i++)
        {
            AudioGenerationRecipeV2 recipe = document.Recipes[i];
            AudioGenerationBriefBuilder.NormalizeCollisionProfile(recipe);
            if (!string.Equals(recipe.DurationMode, "NaturalDecay", StringComparison.Ordinal) && !string.Equals(recipe.DurationMode, "LegacyFixed", StringComparison.Ordinal))
                recipe.DurationMode = "LegacyFixed";
            if (string.IsNullOrWhiteSpace(recipe.DurationCharacter)) recipe.DurationCharacter = "Auto";
            if (string.IsNullOrWhiteSpace(recipe.DecayProfile)) recipe.DecayProfile = "Natural";
            if (recipe.MaxDurationSeconds <= 0) recipe.MaxDurationSeconds = recipe.TargetDurationSeconds;
            recipe.MaxDurationSeconds = Math.Clamp(recipe.MaxDurationSeconds, 0.03f, 30f);
            recipe.History ??= new List<AudioGenerationRecipeRevisionV2>();
            recipe.Layers ??= new List<AudioGenerationLayerPromptV2>();
            if (recipe.Layers.Count > 0 && recipe.Layers.All(item => !item.Enabled))
                recipe.Layers.ForEach(item => item.Enabled = true);
            if (string.IsNullOrWhiteSpace(recipe.LayerTemplate))
                recipe.LayerTemplate = recipe.Layers.Any(item => string.Equals(item.Name, "tail", StringComparison.OrdinalIgnoreCase)) ? "TransientBodyTail"
                    : recipe.Layers.Any(item => string.Equals(item.Name, "transient", StringComparison.OrdinalIgnoreCase)) ? "TransientBody" : "BodyOnly";
            var enabledLayerNames = string.Equals(recipe.LayerTemplate, "BodyOnly", StringComparison.Ordinal)
                ? new HashSet<string>(new[] { "body" }, StringComparer.OrdinalIgnoreCase)
                : string.Equals(recipe.LayerTemplate, "TransientBody", StringComparison.Ordinal)
                    ? new HashSet<string>(new[] { "transient", "body" }, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(new[] { "transient", "body", "tail" }, StringComparer.OrdinalIgnoreCase);
            for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
            {
                AudioGenerationLayerPromptV2 layer = recipe.Layers[layerIndex];
                layer.Enabled = enabledLayerNames.Contains(layer.Name);
                layer.GainDb = Math.Clamp(layer.GainDb, -24f, 6f);
                layer.DelayMs = Math.Clamp(layer.DelayMs, 0, 250);
                if (layer.MaxDurationSeconds <= 0) layer.MaxDurationSeconds = recipe.MaxDurationSeconds;
                layer.MaxDurationSeconds = Math.Clamp(layer.MaxDurationSeconds, 0.03f, 30f);
                if (layer.AdoptionTakes <= 0)
                    layer.AdoptionTakes = Math.Min(layer.Takes, Math.Max(1, recipe.AdoptionCountPerLayer));
            }
            recipe.VariationCount = recipe.Layers.Where(item => item.Enabled).Sum(item => item.Takes);
            if (recipe.History.Count > 10)
                recipe.History.RemoveRange(0, recipe.History.Count - 10);
        }
        if (document.AdoptionSets.Count == 0 && document.Entries.Count > 0)
        {
            foreach (IGrouping<string, AudioGenerationManifestEntry> group in document.Entries
                .Where(item => item != null && string.Equals(item.Status, "Adopted", StringComparison.Ordinal))
                .GroupBy(item => item.EventDefinitionId, StringComparer.Ordinal))
            {
                string setId = "migrated-" + ComputeTextSha256(string.Join("|", group.Select(item => item.ProjectRelativePath + ":" + item.Provenance?.FinalFileHash))).Substring(7, 12);
                var set = new AudioAdoptionSetV3
                {
                    SetId = setId, AudioCueId = group.First().AudioCueId, EventDefinitionId = group.Key,
                    SourceType = "MigratedV2", Status = "Saved", CreatedAtUtc = document.UpdatedAtUtc,
                    Sources = group.Select(item => new FmodAudioAdoptionSource { Layer = item.Layer, Take = item.Take, ProjectRelativePath = item.ProjectRelativePath, Sha256 = item.Provenance?.FinalFileHash ?? string.Empty }).ToList(),
                    Provenance = group.Where(item => item.Provenance != null).Select(item => item.Provenance).ToList()
                };
                set.SetHash = ComputeTextSha256(JsonSerializer.Serialize(set));
                document.AdoptionSets.Add(set);
                document.EventStates.Add(new AudioAdoptionEventStateV3 { EventDefinitionId = group.Key, ActiveSetId = setId });
            }
        }
        return document;
    }

    private static string ComputeTextSha256(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

    private static bool IsRawAudioSha256(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length == 64
            && value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F'));
    }

    private static void RecordRecipeHistory(AudioGenerationManifestDocument previous, AudioGenerationManifestDocument current)
    {
        previous = UpgradeGenerationManifest(previous);
        current = UpgradeGenerationManifest(current);
        for (int i = 0; i < current.Recipes.Count; i++)
        {
            AudioGenerationRecipeV2 recipe = current.Recipes[i];
            AudioGenerationRecipeV2 old = previous.Recipes.Find(item => string.Equals(item.AudioCueId, recipe.AudioCueId, StringComparison.Ordinal));
            bool changed = old != null && (!string.Equals(old.IntentZh, recipe.IntentZh, StringComparison.Ordinal)
                || !string.Equals(old.PositivePrompt, recipe.PositivePrompt, StringComparison.Ordinal)
                || !string.Equals(old.NegativePrompt, recipe.NegativePrompt, StringComparison.Ordinal)
                || !string.Equals(old.LayerTemplate, recipe.LayerTemplate, StringComparison.Ordinal)
                || !string.Equals(JsonSerializer.Serialize(old.CollisionProfile), JsonSerializer.Serialize(recipe.CollisionProfile), StringComparison.Ordinal)
                || old.Layers.Count != recipe.Layers.Count
                || old.Layers.Where((item, layerIndex) => !string.Equals(item.Name, recipe.Layers[layerIndex].Name, StringComparison.Ordinal)
                    || !string.Equals(item.Prompt, recipe.Layers[layerIndex].Prompt, StringComparison.Ordinal)
                    || item.Takes != recipe.Layers[layerIndex].Takes
                    || item.Enabled != recipe.Layers[layerIndex].Enabled
                    || item.GainDb != recipe.Layers[layerIndex].GainDb
                    || item.DelayMs != recipe.Layers[layerIndex].DelayMs
                    || item.AdoptionTakes != recipe.Layers[layerIndex].AdoptionTakes).Any());
            recipe.History = old?.History?.Select(item => item).ToList() ?? recipe.History ?? new List<AudioGenerationRecipeRevisionV2>();
            if (changed)
            {
                recipe.History.Add(new AudioGenerationRecipeRevisionV2
                {
                    Revision = old.Revision,
                    SavedAtUtc = old.UpdatedAtUtc,
                    Source = old.DerivationProvider,
                    GenerationProfile = old.GenerationProfile,
                    LayerTemplate = old.LayerTemplate,
                    IntentZh = old.IntentZh,
                    PositivePrompt = old.PositivePrompt,
                    NegativePrompt = old.NegativePrompt,
                    DurationMode = old.DurationMode,
                    DurationCharacter = old.DurationCharacter,
                    DecayProfile = old.DecayProfile,
                    MaxDurationSeconds = old.MaxDurationSeconds,
                    CollisionProfile = AudioGenerationBriefBuilder.CloneCollisionProfile(old.CollisionProfile),
                    Layers = old.Layers?.Select(item => new AudioGenerationLayerPromptV2 { Name = item.Name, Enabled = item.Enabled, GainDb = item.GainDb, DelayMs = item.DelayMs, Takes = item.Takes, AdoptionTakes = item.AdoptionTakes, Prompt = item.Prompt, MaxDurationSeconds = item.MaxDurationSeconds }).ToList() ?? new List<AudioGenerationLayerPromptV2>()
                });
                recipe.Revision = Math.Max(1, old.Revision + 1);
            }
            else if (old != null)
                recipe.Revision = old.Revision;
            if (recipe.History.Count > 10)
                recipe.History.RemoveRange(0, recipe.History.Count - 10);
            recipe.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }
    internal sealed class AudioGenerationManifestEntry
    {
        public string AudioCueId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public int Take { get; set; }
        public string ProjectRelativePath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public AudioGenerationProvenance Provenance { get; set; }
    }
    internal sealed class FmodAudioAdoptionStatus
    {
        public string requestId { get; set; } = string.Empty;
        public string requestHash { get; set; } = string.Empty;
        public string expectedBindingHash { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string appliedAtUtc { get; set; } = string.Empty;
        public string eventDefinitionId { get; set; } = string.Empty;
        public string eventPath { get; set; } = string.Empty;
        public string eventGuid { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public bool buildCompleted { get; set; }
        public bool buildSucceeded { get; set; }
    }
}
