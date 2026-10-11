using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Authoring
{
    /// <summary>Editable Authoring-only description sent to an SFX provider.</summary>
    /// <remarks>LowFreqAlloc. Derived and serialized only in the Audio Workbench.</remarks>
    public sealed class AudioGenerationBriefV1
    {
        public string Format { get; set; } = "mx.audioGenerationBrief.v1";
        public string AudioCueId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string Category { get; set; } = "Foley";
        public string Material { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Weight { get; set; } = "medium";
        public string Intensity { get; set; } = "medium";
        public string Perspective { get; set; } = "close";
        public string Environment { get; set; } = "dry studio";
        public float TargetDurationSeconds { get; set; } = 0.8f;
        public string DurationMode { get; set; } = "LegacyFixed";
        public string DurationCharacter { get; set; } = "Auto";
        public string DecayProfile { get; set; } = "Natural";
        public float MaxDurationSeconds { get; set; }
        public List<AudioGenerationLayerRequest> Layers { get; set; } = new List<AudioGenerationLayerRequest>();
        public int VariationCount { get; set; } = 8;
        public int AdoptionCountPerLayer { get; set; } = 4;
        public string GenerationProfile { get; set; } = "Standard";
        public string LayerTemplate { get; set; } = "BodyOnly";
        public string PositivePrompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = "music, speech, voice, ambience, background noise, clipping, distortion, rhythmic repetition, baked large-room reverb, room impulse response, hall reverb, chamber reverb, echo, slapback, diffuse tail, wind bed";
        public string SoundDirection { get; set; } = "realistic foundation with restrained fantasy enhancement";
        public AudioCollisionProfileV1 CollisionProfile { get; set; }
    }

    public sealed class AudioGenerationLayerRequest
    {
        public string Name { get; set; } = "body";
        public bool Enabled { get; set; } = true;
        public float GainDb { get; set; }
        public int DelayMs { get; set; }
        public int Takes { get; set; } = 1;
        public int AdoptionTakes { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public float MaxDurationSeconds { get; set; }
    }

    /// <summary>Persisted Authoring-only SFX generation recipe for one gameplay Cue.</summary>
    /// <remarks>LowFreqAlloc. Serialized only by Audio Workbench and never enters runtime catalogs.</remarks>
    public sealed class AudioGenerationRecipeV2
    {
        public string Format { get; set; } = "mx.audioGenerationRecipe.v2";
        public string AudioCueId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string BindingHash { get; set; } = string.Empty;
        public string IntentZh { get; set; } = string.Empty;
        public string Category { get; set; } = "Foley";
        public string Material { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Weight { get; set; } = "medium";
        public string Intensity { get; set; } = "medium";
        public string Perspective { get; set; } = "close";
        public string Environment { get; set; } = "dry studio";
        public float TargetDurationSeconds { get; set; } = 0.8f;
        public string DurationMode { get; set; } = "LegacyFixed";
        public string DurationCharacter { get; set; } = "Auto";
        public string DecayProfile { get; set; } = "Natural";
        public float MaxDurationSeconds { get; set; }
        public int VariationCount { get; set; } = 8;
        public int AdoptionCountPerLayer { get; set; } = 4;
        public string GenerationProfile { get; set; } = "Standard";
        public string LayerTemplate { get; set; } = string.Empty;
        public string SoundDirection { get; set; } = "realistic foundation with restrained fantasy enhancement";
        public AudioCollisionProfileV1 CollisionProfile { get; set; }
        public string PositivePrompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public List<AudioGenerationLayerPromptV2> Layers { get; set; } = new List<AudioGenerationLayerPromptV2>();
        public string DerivationProvider { get; set; } = "RuleDerived";
        public string DerivationModel { get; set; } = string.Empty;
        public bool PromptModified { get; set; }
        public int Revision { get; set; } = 1;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public List<AudioGenerationRecipeRevisionV2> History { get; set; } = new List<AudioGenerationRecipeRevisionV2>();
    }

    /// <summary>Structured Authoring-only description of two bodies and their contact event.</summary>
    /// <remarks>LowFreqAlloc. Serialized in generation recipes and provenance; never enters runtime catalogs.</remarks>
    public sealed class AudioCollisionProfileV1
    {
        public string Kind { get; set; } = "ObjectCollision";
        public AudioCollisionBodyV1 Impactor { get; set; } = new AudioCollisionBodyV1();
        public AudioCollisionBodyV1 Receiver { get; set; } = new AudioCollisionBodyV1();
        public AudioCollisionInteractionV1 Interaction { get; set; } = new AudioCollisionInteractionV1();
    }

    public sealed class AudioCollisionBodyV1
    {
        public string MaterialId { get; set; } = "auto";
        public string CustomMaterial { get; set; } = string.Empty;
        public string MassClass { get; set; } = "medium";
        public string ContactArea { get; set; } = "medium";
        public string Surface { get; set; } = "normal";
        public string Construction { get; set; } = "solid";
    }

    public sealed class AudioCollisionInteractionV1
    {
        public string Intensity { get; set; } = "medium";
        public string Speed { get; set; } = "medium";
        public string Angle { get; set; } = "direct";
        public string Mode { get; set; } = "impact";
    }

    public sealed class AudioGenerationLayerPromptV2
    {
        public string Name { get; set; } = "body";
        public bool Enabled { get; set; } = true;
        public float GainDb { get; set; }
        public int DelayMs { get; set; }
        public int Takes { get; set; } = 1;
        public int AdoptionTakes { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public float MaxDurationSeconds { get; set; }
    }

    public sealed class AudioGenerationRecipeRevisionV2
    {
        public int Revision { get; set; }
        public string SavedAtUtc { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string GenerationProfile { get; set; } = "Standard";
        public string LayerTemplate { get; set; } = "BodyOnly";
        public string IntentZh { get; set; } = string.Empty;
        public string PositivePrompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public string DurationMode { get; set; } = "LegacyFixed";
        public string DurationCharacter { get; set; } = "Auto";
        public string DecayProfile { get; set; } = "Natural";
        public float MaxDurationSeconds { get; set; }
        public AudioCollisionProfileV1 CollisionProfile { get; set; }
        public List<AudioGenerationLayerPromptV2> Layers { get; set; } = new List<AudioGenerationLayerPromptV2>();
    }

    public sealed class AudioGenerationCandidate
    {
        public string CandidateId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public int Take { get; set; }
        public float DurationSeconds { get; set; }
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public float PeakDbfs { get; set; }
        public float RmsDbfs { get; set; }
        public float RawClippingRatio { get; set; }
        public float CrestFactorDb { get; set; }
        public float SpectralFlatness { get; set; }
        public Dictionary<string, float> FrequencyBands { get; set; } = new Dictionary<string, float>();
        public List<float> WaveformPeaks { get; set; } = new List<float>();
        public string RawSha256 { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public float SourceDurationSeconds { get; set; }
        public float MaxDurationSeconds { get; set; }
        public float TrimmedDurationSeconds { get; set; }
        public float DecayTimeMs { get; set; }
        public float LateTailRelativeDb { get; set; }
        public bool DecayReached { get; set; }
    }

    /// <summary>Authoring-only non-destructive candidate trim and fade settings.</summary>
    /// <remarks>LowFreqAlloc: serialized only while editing or adopting generated audio.</remarks>
    public sealed class AudioCandidateEditV1
    {
        public string JobId { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public float InMs { get; set; }
        public float OutMs { get; set; }
        public float FadeInMs { get; set; } = 3f;
        public float FadeOutMs { get; set; } = 3f;
        public string SourceHash { get; set; } = string.Empty;
        public int Revision { get; set; } = 1;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public List<AudioCandidateEditRevisionV1> History { get; set; } = new List<AudioCandidateEditRevisionV1>();
    }

    public sealed class AudioCandidateEditRevisionV1
    {
        public int Revision { get; set; }
        public float InMs { get; set; }
        public float OutMs { get; set; }
        public float FadeInMs { get; set; }
        public float FadeOutMs { get; set; }
        public string SavedAtUtc { get; set; } = string.Empty;
    }

    public sealed class AudioGenerationJob
    {
        public string JobId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public bool Fake { get; set; }
        public string Status { get; set; } = string.Empty;
        public float Progress { get; set; }
        public int Seed { get; set; }
        public string AudioCueId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public int RecipeRevision { get; set; }
        public string RecipeHash { get; set; } = string.Empty;
        public List<AudioGenerationBatchV1> Batches { get; set; } = new List<AudioGenerationBatchV1>();
        public List<AudioGenerationCandidate> Candidates { get; set; } = new List<AudioGenerationCandidate>();
        public string FailureReason { get; set; } = string.Empty;
        public string GpuState { get; set; } = string.Empty;
        public string ComputeDevice { get; set; } = string.Empty;
        public float GpuPeakMemoryMiB { get; set; }
        public string VoiceRecoveryStatus { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
        public string UpdatedUtc { get; set; } = string.Empty;
    }

    public sealed class AudioGenerationBatchV1
    {
        public string BatchId { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
        public int Seed { get; set; }
        public Dictionary<string, int> LayerCounts { get; set; } = new Dictionary<string, int>();
    }

    public sealed class AudioGenerationProvenance
    {
        public string IntentZh { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public List<AudioGenerationLayerPromptV2> LayerPrompts { get; set; } = new List<AudioGenerationLayerPromptV2>();
        public int RecipeRevision { get; set; }
        public string DurationMode { get; set; } = "LegacyFixed";
        public string DurationCharacter { get; set; } = "Auto";
        public string DecayProfile { get; set; } = "Natural";
        public float MaxDurationSeconds { get; set; }
        public string DerivationProvider { get; set; } = string.Empty;
        public int GeneratedCandidateCount { get; set; }
        public int AdoptionCandidateCount { get; set; }
        public int Seed { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ModelRevision { get; set; } = string.Empty;
        public string License { get; set; } = "Stable Audio Open Model License";
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public string OriginalHash { get; set; } = string.Empty;
        public List<string> ProcessingSteps { get; set; } = new List<string>();
        public string AdoptedBy { get; set; } = string.Empty;
        public string FinalFileHash { get; set; } = string.Empty;
        public AudioCandidateEditV1 CandidateEdit { get; set; }
        public AudioCollisionProfileV1 CollisionProfile { get; set; }
    }

    public sealed class FmodAudioAdoptionRequestV1
    {
        public string Format { get; set; } = "mx.fmodAudioAdoptionRequest.v1";
        public string RequestId { get; set; } = string.Empty;
        public string SetId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string TargetEventPath { get; set; } = string.Empty;
        public string TargetEventGuid { get; set; } = string.Empty;
        public string ExpectedBindingHash { get; set; } = string.Empty;
        public string Layout { get; set; } = "MultiInstrument";
        public List<FmodAudioAdoptionSource> Sources { get; set; } = new List<FmodAudioAdoptionSource>();
        public List<FmodAudioAdoptionRandomization> Randomization { get; set; } = new List<FmodAudioAdoptionRandomization>();
        public List<FmodAudioAdoptionLayerMix> LayerMix { get; set; } = new List<FmodAudioAdoptionLayerMix>();
        public string RequestHash { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
    }

    public sealed class FmodAudioAdoptionSource
    {
        public string Layer { get; set; } = string.Empty;
        public int Take { get; set; }
        public string ProjectRelativePath { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class FmodAudioAdoptionRandomization
    {
        public string Layer { get; set; } = string.Empty;
        public float PitchSemitones { get; set; }
        public float VolumeDb { get; set; }
    }

    public sealed class FmodAudioAdoptionLayerMix
    {
        public string Layer { get; set; } = string.Empty;
        public float GainDb { get; set; }
        public int DelayMs { get; set; }
    }

    /// <summary>Immutable Authoring-only source set that can be reapplied to an existing FMOD event.</summary>
    /// <remarks>LowFreqAlloc. Serialized in the generation manifest and never loaded by gameplay runtime.</remarks>
    public sealed class AudioAdoptionSetV3
    {
        public string SetId { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string BindingHash { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Generated";
        public string Status { get; set; } = "Saved";
        public string Layout { get; set; } = "MultiInstrument";
        public List<FmodAudioAdoptionSource> Sources { get; set; } = new List<FmodAudioAdoptionSource>();
        public List<FmodAudioAdoptionRandomization> Randomization { get; set; } = new List<FmodAudioAdoptionRandomization>();
        public List<FmodAudioAdoptionLayerMix> LayerMix { get; set; } = new List<FmodAudioAdoptionLayerMix>();
        public List<AudioGenerationProvenance> Provenance { get; set; } = new List<AudioGenerationProvenance>();
        public string SetHash { get; set; } = string.Empty;
        public string BankHash { get; set; } = string.Empty;
        public string SnapshotHash { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string AppliedAtUtc { get; set; } = string.Empty;
        public string PublishedAtUtc { get; set; } = string.Empty;
        public string FailureCode { get; set; } = string.Empty;
        public string FailureMessage { get; set; } = string.Empty;
    }

    public sealed class AudioAdoptionEventStateV3
    {
        public string EventDefinitionId { get; set; } = string.Empty;
        public string ActiveSetId { get; set; } = string.Empty;
        public string PublishedSetId { get; set; } = string.Empty;
    }

    public static class AudioGenerationBriefBuilder
    {
        /// <summary>Builds a conservative English SFX brief from Cue semantics and Event facts.</summary>
        /// <remarks>LowFreqAlloc. Allocates strings and collections for Authoring UI only.</remarks>
        public static AudioGenerationBriefV1 Build(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent)
        {
            cue ??= new AudioCueAuthoringDefinition();
            audioEvent ??= new AudioEventAuthoringDefinition();
            string searchable = (cue.AudioCueId + " " + cue.DisplayName + " " + cue.Purpose + " " + cue.Context + " " + string.Join(" ", cue.Tags ?? new List<string>())).ToLowerInvariant();
            bool airDisplacement = IsAirDisplacement(searchable);
            string collisionKind = airDisplacement ? string.Empty : DetectCollisionKind(searchable);
            string category = airDisplacement ? "AirDisplacement"
                : !string.IsNullOrEmpty(collisionKind) ? collisionKind
                : ContainsAny(searchable, "fireball", "explosion") ? "Explosion"
                : "Foley";
            bool layered = category == "Landing" || category == "WeaponHit" || category == "Explosion";
            int variations = category == "Footstep" ? 12 : category == "ObjectCollision" ? 8 : 6;
            float duration = layered ? 1.2f : category == "Footstep" ? 0.45f : 0.7f;
            if (category == "Explosion") duration = 1.5f;
            var brief = new AudioGenerationBriefV1
            {
                AudioCueId = cue.AudioCueId,
                EventDefinitionId = audioEvent.Id,
                Category = category,
                Action = FirstNonEmpty(cue.DisplayName, cue.AudioCueId, "gameplay sound"),
                TargetDurationSeconds = duration,
                DurationMode = "NaturalDecay",
                DurationCharacter = "Auto",
                DecayProfile = category == "AirDisplacement" ? "Sweep" : "Natural",
                MaxDurationSeconds = category == "Footstep" ? 0.4f
                    : category == "ObjectCollision" ? 0.3f
                    : category == "Landing" ? 0.55f
                    : category == "WeaponHit" ? 0.45f
                    : category == "AirDisplacement" ? 0.45f
                    : duration,
                VariationCount = variations,
                AdoptionCountPerLayer = category == "Footstep" ? 6 : category == "ObjectCollision" || layered ? 4 : 1,
                LayerTemplate = layered ? "TransientBodyTail" : "BodyOnly",
            };
            if (!string.IsNullOrEmpty(collisionKind))
            {
                brief.CollisionProfile = BuildCollisionProfile(collisionKind, searchable, string.Empty, brief.Weight, brief.Intensity);
                ProjectLegacyCollisionFields(brief, brief.CollisionProfile);
            }
            else
                brief.Material = FindMaterial(searchable);
            if (layered)
            {
                brief.Layers.Add(new AudioGenerationLayerRequest { Name = "transient", Takes = variations, AdoptionTakes = brief.AdoptionCountPerLayer, MaxDurationSeconds = category == "WeaponHit" ? 0.15f : 0.18f });
                brief.Layers.Add(new AudioGenerationLayerRequest { Name = "body", Takes = variations, AdoptionTakes = brief.AdoptionCountPerLayer, MaxDurationSeconds = brief.MaxDurationSeconds });
                brief.Layers.Add(new AudioGenerationLayerRequest { Name = "tail", Takes = variations, AdoptionTakes = brief.AdoptionCountPerLayer, MaxDurationSeconds = category == "WeaponHit" ? 0.65f : brief.MaxDurationSeconds });
            }
            else
            {
                brief.Layers.Add(new AudioGenerationLayerRequest { Name = "body", Takes = variations, AdoptionTakes = brief.AdoptionCountPerLayer, MaxDurationSeconds = brief.MaxDurationSeconds });
            }
            brief.PositivePrompt = brief.CollisionProfile != null
                ? BuildCollisionPrompt(brief.Action, brief.CollisionProfile, brief.Perspective, brief.Environment, brief.SoundDirection)
                : BuildGenericPrompt(brief, cue);
            return brief;
        }

        public static AudioGenerationRecipeV2 BuildRecipe(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent, string bindingHash)
        {
            AudioGenerationBriefV1 brief = Build(cue, audioEvent);
            var recipe = new AudioGenerationRecipeV2
            {
                AudioCueId = brief.AudioCueId,
                EventDefinitionId = brief.EventDefinitionId,
                BindingHash = bindingHash ?? string.Empty,
                IntentZh = FirstNonEmpty(cue?.Purpose, cue?.Context, cue?.DisplayName),
                Category = brief.Category,
                Material = brief.Material,
                Action = brief.Action,
                Weight = brief.Weight,
                Intensity = brief.Intensity,
                Perspective = brief.Perspective,
                Environment = brief.Environment,
                TargetDurationSeconds = brief.TargetDurationSeconds,
                DurationMode = brief.DurationMode,
                DurationCharacter = brief.DurationCharacter,
                DecayProfile = brief.DecayProfile,
                MaxDurationSeconds = brief.MaxDurationSeconds,
                VariationCount = brief.VariationCount,
                AdoptionCountPerLayer = brief.AdoptionCountPerLayer,
                GenerationProfile = brief.GenerationProfile,
                LayerTemplate = brief.LayerTemplate,
                SoundDirection = brief.SoundDirection,
                CollisionProfile = CloneCollisionProfile(brief.CollisionProfile),
                PositivePrompt = brief.PositivePrompt,
                NegativePrompt = "music, speech, voice, ambience, background noise, clipping, distortion, rhythmic repetition, baked large-room reverb, room impulse response, hall reverb, chamber reverb, echo, slapback, diffuse tail, wind bed",
                DerivationProvider = "RuleDerived",
                UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < brief.Layers.Count; i++)
            {
                AudioGenerationLayerRequest layer = brief.Layers[i];
                recipe.Layers.Add(new AudioGenerationLayerPromptV2
                {
                    Name = layer.Name,
                    Enabled = layer.Enabled,
                    GainDb = layer.GainDb,
                    DelayMs = layer.DelayMs,
                    Takes = layer.Takes,
                    AdoptionTakes = layer.AdoptionTakes > 0 ? layer.AdoptionTakes : brief.AdoptionCountPerLayer,
                    Prompt = BuildLayerPrompt(recipe, layer.Name),
                    MaxDurationSeconds = layer.MaxDurationSeconds
                });
            }
            return recipe;
        }

        public static string BuildLayerPrompt(AudioGenerationRecipeV2 recipe, string layer)
        {
            if (recipe.CollisionProfile != null)
            {
                string collisionBehavior = string.Equals(layer, "transient", StringComparison.OrdinalIgnoreCase)
                    ? "speed, angle, surface hardness and contact-area focused initial transient"
                    : string.Equals(layer, "tail", StringComparison.OrdinalIgnoreCase)
                        ? "receiver resonance, surface debris and damping focused short decay without baked spatial reverb"
                        : "both bodies' mass, material and construction focused impact body with controlled low-mid energy";
                return string.Concat(BuildCollisionPrompt(recipe.Action, recipe.CollisionProfile, recipe.Perspective, recipe.Environment, recipe.SoundDirection), " Layer focus: ", collisionBehavior, ".");
            }
            string genericBehavior = string.Equals(layer, "transient", StringComparison.OrdinalIgnoreCase)
                ? "clean fast onset with a defined initial transient"
                : string.Equals(layer, "tail", StringComparison.OrdinalIgnoreCase)
                    ? "controlled characterful decay without baked spatial reverb"
                    : "clear primary sound body with controlled spectral energy";
            return string.Concat(recipe.Action, ", ", recipe.Material, ", ", recipe.Weight, " weight, ", recipe.Intensity,
                " intensity, ", genericBehavior, ", ", recipe.Perspective, " perspective, ", recipe.Environment,
                ", ", recipe.SoundDirection, ", dry, close-mic, isolated, game-ready, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance");
        }

        public static void RebuildRecipePrompts(AudioGenerationRecipeV2 recipe)
        {
            if (recipe == null)
                return;
            NormalizeCollisionProfile(recipe);
            recipe.DurationMode = "NaturalDecay";
            recipe.DurationCharacter = "Auto";
            if (recipe.MaxDurationSeconds <= 0) recipe.MaxDurationSeconds = recipe.TargetDurationSeconds;
            recipe.PositivePrompt = recipe.CollisionProfile != null
                ? BuildCollisionPrompt(recipe.Action, recipe.CollisionProfile, recipe.Perspective, recipe.Environment, recipe.SoundDirection)
                : string.Concat(recipe.Action, ", ", recipe.Material, ", ", recipe.Weight,
                    " weight, ", recipe.Intensity, " intensity, ", recipe.Perspective, " perspective, ", recipe.Environment,
                    ", ", recipe.SoundDirection, ", dry, close-mic, isolated, game-ready, clear transient, controlled decay, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance");
            recipe.NegativePrompt = "music, speech, voice, ambience, background noise, clipping, distortion, rhythmic repetition, baked large-room reverb, room impulse response, hall reverb, chamber reverb, echo, slapback, diffuse tail, wind bed";
            for (int i = 0; i < recipe.Layers.Count; i++)
                recipe.Layers[i].Prompt = BuildLayerPrompt(recipe, recipe.Layers[i].Name);
            recipe.DerivationProvider = "RuleFallback";
            recipe.PromptModified = false;
            recipe.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        /// <summary>Migrates and normalizes the optional contact-sound profile, then projects legacy fields.</summary>
        /// <remarks>LowFreqAlloc. Authoring startup/save migration only; may allocate defaults and rebuilt prompt strings.</remarks>
        public static void NormalizeCollisionProfile(AudioGenerationRecipeV2 recipe)
        {
            if (recipe == null)
                return;
            recipe.Layers ??= new List<AudioGenerationLayerPromptV2>();
            string searchable = string.Concat(recipe.AudioCueId, " ", recipe.Category, " ", recipe.Action, " ", recipe.IntentZh).ToLowerInvariant();
            if (string.Equals(recipe.Category, "AirDisplacement", StringComparison.OrdinalIgnoreCase) || IsAirDisplacement(searchable))
            {
                recipe.Category = "AirDisplacement";
                recipe.CollisionProfile = null;
                return;
            }
            string kind = recipe.CollisionProfile?.Kind;
            if (string.IsNullOrWhiteSpace(kind)) kind = DetectCollisionKind(searchable);
            if (string.IsNullOrWhiteSpace(kind)) return;
            bool created = recipe.CollisionProfile == null;
            recipe.CollisionProfile ??= BuildCollisionProfile(kind, searchable, recipe.Material, recipe.Weight, recipe.Intensity);
            NormalizeCollisionProfile(recipe.CollisionProfile);
            ProjectLegacyCollisionFields(recipe, recipe.CollisionProfile);
            if (created && !recipe.PromptModified)
            {
                recipe.PositivePrompt = BuildCollisionPrompt(recipe.Action, recipe.CollisionProfile, recipe.Perspective, recipe.Environment, recipe.SoundDirection);
                for (int i = 0; i < recipe.Layers.Count; i++) recipe.Layers[i].Prompt = BuildLayerPrompt(recipe, recipe.Layers[i].Name);
                recipe.DerivationProvider = "RuleDerived";
            }
        }

        /// <summary>Creates an independent Authoring snapshot of a contact-sound profile.</summary>
        /// <remarks>LowFreqAlloc. Allocates DTO objects for history and immutable provenance snapshots.</remarks>
        public static AudioCollisionProfileV1 CloneCollisionProfile(AudioCollisionProfileV1 value)
        {
            if (value == null) return null;
            return new AudioCollisionProfileV1
            {
                Kind = value.Kind,
                Impactor = CloneBody(value.Impactor),
                Receiver = CloneBody(value.Receiver),
                Interaction = new AudioCollisionInteractionV1
                {
                    Intensity = value.Interaction?.Intensity ?? "medium", Speed = value.Interaction?.Speed ?? "medium",
                    Angle = value.Interaction?.Angle ?? "direct", Mode = value.Interaction?.Mode ?? "impact"
                }
            };
        }

        /// <summary>Validates stable contact-sound option identifiers before save, generation, or adoption.</summary>
        /// <remarks>LowFreqAlloc. Authoring request validation only.</remarks>
        public static bool IsValidCollisionProfile(AudioCollisionProfileV1 profile)
        {
            if (profile == null) return true;
            return IsChoice(profile.Kind, new[] { "Footstep", "Landing", "ObjectCollision", "WeaponHit" })
                && IsValidBody(profile.Impactor) && IsValidBody(profile.Receiver) && profile.Interaction != null
                && IsChoice(profile.Interaction.Intensity, new[] { "very_light", "light", "medium", "strong", "extreme" })
                && IsChoice(profile.Interaction.Speed, new[] { "slow", "medium", "fast", "very_fast" })
                && IsChoice(profile.Interaction.Angle, new[] { "glancing", "oblique", "direct" })
                && IsChoice(profile.Interaction.Mode, new[] { "impact", "step", "land", "weapon_hit", "block", "scrape", "crush" });
        }

        private static string BuildGenericPrompt(AudioGenerationBriefV1 brief, AudioCueAuthoringDefinition cue)
        {
            var prompt = new StringBuilder();
            prompt.Append("A high-quality ").Append(brief.Category.ToLowerInvariant()).Append(" game sound: ").Append(brief.Action).Append(". ");
            if (!string.IsNullOrWhiteSpace(brief.Material)) prompt.Append("Material: ").Append(brief.Material).Append(". ");
            prompt.Append("Context: ").Append(FirstNonEmpty(cue.Context, cue.Purpose, "third-person gameplay")).Append(". ")
                .Append("Realistic foundation with restrained fantasy enhancement, dry, close-mic, isolated, game-ready, clean transient and controlled tail, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance.");
            return prompt.ToString();
        }

        private static string BuildCollisionPrompt(string action, AudioCollisionProfileV1 profile, string perspective, string environment, string direction)
        {
            return string.Concat(ActionPrompt(profile.Interaction.Mode), ": ", FirstNonEmpty(action, profile.Kind, "contact sound"), ". Impactor: ",
                BodyPrompt(profile.Impactor), ". Receiver: ", BodyPrompt(profile.Receiver), ". Contact: ",
                ChoicePrompt(profile.Interaction.Angle), " angle, ", ChoicePrompt(profile.Interaction.Speed), " speed, ",
                ChoicePrompt(profile.Interaction.Intensity), " intensity. ", FirstNonEmpty(perspective, "close"), " perspective, ",
                FirstNonEmpty(environment, "dry studio"), ", ", FirstNonEmpty(direction, "realistic foundation with restrained fantasy enhancement"),
                ", dry, close-mic, isolated, game-ready, clear transient, controlled decay, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance.");
        }

        private static string BodyPrompt(AudioCollisionBodyV1 body)
        {
            body ??= new AudioCollisionBodyV1();
            return string.Concat(MaterialPrompt(body), ", ", ChoicePrompt(body.MassClass), " mass, ", ChoicePrompt(body.Construction),
                " construction, ", ChoicePrompt(body.ContactArea), " contact area, ", ChoicePrompt(body.Surface), " surface");
        }

        private static string MaterialPrompt(AudioCollisionBodyV1 body)
        {
            if (body.MaterialId == "custom" && !string.IsNullOrWhiteSpace(body.CustomMaterial)) return body.CustomMaterial.Trim();
            return body.MaterialId switch
            {
                "generic_solid" => "generic solid material", "wood" => "wood", "metal" => "metal", "stone_concrete" => "stone or concrete",
                "glass_ceramic" => "glass or ceramic", "hard_plastic" => "hard plastic", "rubber" => "rubber", "cloth_leather" => "cloth or leather",
                "flesh" => "flesh", "soil_sand" => "soil or gravel", "ice" => "ice crystal", "magic_energy" => "magical energy", _ => "material inferred from the contact context"
            };
        }

        private static string ChoicePrompt(string value) => string.IsNullOrWhiteSpace(value) ? "medium" : value.Replace('_', ' ');
        private static string ActionPrompt(string mode) => mode switch
        {
            "step" => "Footwear steps onto a receiving surface", "land" => "A character lands onto a receiving surface",
            "weapon_hit" => "A weapon strikes a target", "block" => "A weapon impact is firmly blocked", "scrape" => "Two surfaces scrape",
            "crush" => "One body compresses another", _ => "One body impacts another"
        };

        private static AudioCollisionProfileV1 BuildCollisionProfile(string kind, string searchable, string legacyMaterial, string legacyWeight, string legacyIntensity)
        {
            var profile = new AudioCollisionProfileV1 { Kind = kind };
            profile.Impactor.MaterialId = kind == "Footstep" ? "cloth_leather" : kind == "WeaponHit" ? "metal" : "auto";
            profile.Impactor.MassClass = NormalizeMass(legacyWeight);
            profile.Impactor.ContactArea = kind == "Footstep" || kind == "WeaponHit" ? "small" : "medium";
            profile.Impactor.Construction = kind == "Footstep" || kind == "Landing" ? "soft_filled" : "solid";
            profile.Receiver.MaterialId = MaterialIdFromText(FirstNonEmpty(legacyMaterial, searchable));
            profile.Receiver.MassClass = kind == "Footstep" || kind == "Landing" ? "massive" : "medium";
            profile.Receiver.ContactArea = kind == "Footstep" ? "small" : "medium";
            profile.Receiver.Surface = ContainsAny(searchable, "hard-slick", "hard slick") ? "smooth"
                : ContainsAny(searchable, "rough", "粗糙") ? "rough" : "normal";
            profile.Receiver.Construction = ContainsAny(searchable, "elastic", "弹性") ? "soft_filled" : "solid";
            profile.Interaction.Intensity = NormalizeIntensity(legacyIntensity);
            profile.Interaction.Mode = ContainsAny(searchable, "blocked", "block", "格挡") ? "block"
                : kind == "Footstep" ? "step" : kind == "Landing" ? "land" : kind == "WeaponHit" ? "weapon_hit" : "impact";
            return profile;
        }

        private static void NormalizeCollisionProfile(AudioCollisionProfileV1 profile)
        {
            profile.Kind = NormalizeChoice(profile.Kind, new[] { "Footstep", "Landing", "ObjectCollision", "WeaponHit" }, "ObjectCollision");
            profile.Impactor ??= new AudioCollisionBodyV1(); profile.Receiver ??= new AudioCollisionBodyV1(); profile.Interaction ??= new AudioCollisionInteractionV1();
            NormalizeBody(profile.Impactor); NormalizeBody(profile.Receiver);
            profile.Interaction.Intensity = NormalizeChoice(profile.Interaction.Intensity, new[] { "very_light", "light", "medium", "strong", "extreme" }, "medium");
            profile.Interaction.Speed = NormalizeChoice(profile.Interaction.Speed, new[] { "slow", "medium", "fast", "very_fast" }, "medium");
            profile.Interaction.Angle = NormalizeChoice(profile.Interaction.Angle, new[] { "glancing", "oblique", "direct" }, "direct");
            profile.Interaction.Mode = NormalizeChoice(profile.Interaction.Mode, new[] { "impact", "step", "land", "weapon_hit", "block", "scrape", "crush" }, "impact");
        }

        private static void NormalizeBody(AudioCollisionBodyV1 body)
        {
            body.MaterialId = NormalizeChoice(body.MaterialId, new[] { "auto", "generic_solid", "wood", "metal", "stone_concrete", "glass_ceramic", "hard_plastic", "rubber", "cloth_leather", "flesh", "soil_sand", "ice", "magic_energy", "custom" }, "auto");
            body.MassClass = NormalizeChoice(body.MassClass, new[] { "very_light", "light", "medium", "heavy", "massive" }, "medium");
            body.ContactArea = NormalizeChoice(body.ContactArea, new[] { "point", "small", "medium", "large", "broad" }, "medium");
            body.Surface = NormalizeChoice(body.Surface, new[] { "polished", "smooth", "normal", "rough", "granular" }, "normal");
            body.Construction = NormalizeChoice(body.Construction, new[] { "solid", "hollow", "thin_shell", "soft_filled" }, "solid");
        }

        private static bool IsValidBody(AudioCollisionBodyV1 body) => body != null
            && IsChoice(body.MaterialId, new[] { "auto", "generic_solid", "wood", "metal", "stone_concrete", "glass_ceramic", "hard_plastic", "rubber", "cloth_leather", "flesh", "soil_sand", "ice", "magic_energy", "custom" })
            && (body.MaterialId != "custom" || !string.IsNullOrWhiteSpace(body.CustomMaterial))
            && IsChoice(body.MassClass, new[] { "very_light", "light", "medium", "heavy", "massive" })
            && IsChoice(body.ContactArea, new[] { "point", "small", "medium", "large", "broad" })
            && IsChoice(body.Surface, new[] { "polished", "smooth", "normal", "rough", "granular" })
            && IsChoice(body.Construction, new[] { "solid", "hollow", "thin_shell", "soft_filled" });
        private static bool IsChoice(string value, string[] values)
        {
            for (int i = 0; i < values.Length; i++) if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static AudioCollisionBodyV1 CloneBody(AudioCollisionBodyV1 value) => value == null ? new AudioCollisionBodyV1() : new AudioCollisionBodyV1
        {
            MaterialId = value.MaterialId, CustomMaterial = value.CustomMaterial, MassClass = value.MassClass,
            ContactArea = value.ContactArea, Surface = value.Surface, Construction = value.Construction
        };

        private static void ProjectLegacyCollisionFields(AudioGenerationBriefV1 brief, AudioCollisionProfileV1 profile)
        {
            brief.Material = profile.Receiver.MaterialId; brief.Weight = profile.Impactor.MassClass; brief.Intensity = profile.Interaction.Intensity;
        }
        private static void ProjectLegacyCollisionFields(AudioGenerationRecipeV2 recipe, AudioCollisionProfileV1 profile)
        {
            recipe.Category = profile.Kind; recipe.Material = profile.Receiver.MaterialId; recipe.Weight = profile.Impactor.MassClass; recipe.Intensity = profile.Interaction.Intensity;
        }

        private static string DetectCollisionKind(string value)
        {
            if (ContainsAny(value, "fireball", "explosion")) return string.Empty;
            if (ContainsAny(value, "foot", "sole", "step")) return "Footstep";
            if (ContainsAny(value, "landing", "落地")) return "Landing";
            if (ContainsAny(value, "collision", "impact object")) return "ObjectCollision";
            if (ContainsAny(value, "weapon", "attack", "blocked", "hit")) return "WeaponHit";
            return string.Empty;
        }

        private static bool IsAirDisplacement(string value)
        {
            return ContainsAny(value, "air_displacement", "air displacement", "sweep", "whoosh", "破空");
        }

        private static string MaterialIdFromText(string value)
        {
            value = (value ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(value, "elastic", "rubber", "弹性")) return "rubber";
            if (ContainsAny(value, "hard-slick", "hard slick")) return "generic_solid";
            if (ContainsAny(value, "stone", "concrete")) return "stone_concrete";
            if (value.Contains("wood")) return "wood"; if (value.Contains("metal")) return "metal";
            if (ContainsAny(value, "glass", "ceramic")) return "glass_ceramic"; if (value.Contains("plastic")) return "hard_plastic";
            if (ContainsAny(value, "cloth", "leather")) return "cloth_leather"; if (ContainsAny(value, "flesh", "body")) return "flesh";
            if (ContainsAny(value, "dirt", "grass", "sand", "gravel")) return "soil_sand"; if (value.Contains("ice")) return "ice";
            return "auto";
        }

        private static string NormalizeMass(string value) => NormalizeChoice(value, new[] { "very_light", "light", "medium", "heavy", "massive" }, "medium");
        private static string NormalizeIntensity(string value) => NormalizeChoice(value, new[] { "very_light", "light", "medium", "strong", "extreme" }, "medium");
        private static string NormalizeChoice(string value, string[] values, string fallback)
        {
            for (int i = 0; i < values.Length; i++) if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return values[i];
            return fallback;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (value.Contains(needles[i]))
                    return true;
            return false;
        }

        private static string FindMaterial(string value)
        {
            string[] values = { "stone", "wood", "metal", "dirt", "grass", "sand", "cloth", "leather", "flesh", "fire", "ice" };
            for (int i = 0; i < values.Length; i++)
                if (value.Contains(values[i]))
                    return values[i];
            return string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            return string.Empty;
        }
    }
}
