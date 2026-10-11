using System;
using MxFramework.Audio;
using MxFramework.Audio.Physical;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestAudioCueCatalog
    {
        /// <summary>Adapts the generated global audio catalog to the CharacterTest composition contract.</summary>
        /// <remarks>LowFreqAlloc. Called once while constructing the runtime session.</remarks>
        public static CharacterTestAudioCueCatalog FromRuntimeCatalog(
            AudioDefinitionCatalog runtimeCatalog,
            IPhysicalAudioDefinitionProvider physicalAudioDefinitions)
        {
            if (!TryFromRuntimeCatalog(runtimeCatalog, physicalAudioDefinitions, out CharacterTestAudioCueCatalog catalog, out string error))
                throw new ArgumentException(error);
            return catalog;
        }

        /// <summary>Links and validates generated physical definitions against the matching global audio catalog.</summary>
        /// <remarks>LowFreqAlloc. Called once during runtime composition; failure must select the complete legacy fallback.</remarks>
        public static bool TryFromRuntimeCatalog(
            AudioDefinitionCatalog runtimeCatalog,
            IPhysicalAudioDefinitionProvider physicalAudioDefinitions,
            out CharacterTestAudioCueCatalog catalog,
            out string error)
        {
            catalog = null;
            error = string.Empty;
            if (runtimeCatalog == null)
            {
                error = "Generated global audio catalog is missing.";
                return false;
            }
            if (physicalAudioDefinitions == null)
            {
                error = "Generated physical audio catalog is missing.";
                return false;
            }
            if (!TryBuildPhysicalAudioBindings(runtimeCatalog, physicalAudioDefinitions, out CharacterTestPhysicalAudioPlaybackBinding[] bindings, out error))
                return false;

            catalog = new CharacterTestAudioCueCatalog(runtimeCatalog, physicalAudioDefinitions, bindings);
            return true;
        }

        private static bool TryBuildPhysicalAudioBindings(
            AudioDefinitionCatalog runtimeCatalog,
            IPhysicalAudioDefinitionProvider physicalAudioDefinitions,
            out CharacterTestPhysicalAudioPlaybackBinding[] bindings,
            out string error)
        {
            if (physicalAudioDefinitions.MaxResponseParameterCount
                > CharacterTestAudioIntent.MaxParameterCount)
            {
                bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                error = "Generated physical audio catalog requires "
                    + physicalAudioDefinitions.MaxResponseParameterCount
                    + " response parameters, exceeding CharacterTest audio intent capacity "
                    + CharacterTestAudioIntent.MaxParameterCount
                    + "; the generated catalog pair must fall back as a whole.";
                return false;
            }

            bindings = new CharacterTestPhysicalAudioPlaybackBinding[physicalAudioDefinitions.BasisCount];
            error = string.Empty;
            for (int i = 0; i < bindings.Length; i++)
            {
                PhysicalAudioBasisDefinition basis = physicalAudioDefinitions.GetBasisAt(i);
                if (!runtimeCatalog.TryGetCue(basis.CueId, out AudioCueDefinition cue))
                {
                    error = "Physical audio Basis " + basis.Id.Value + " references missing runtime Cue " + basis.CueId + ".";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }
                if (cue.PlayMode != AudioCuePlayMode.OneShot)
                {
                    error = "Physical audio Basis " + basis.Id.Value + " must reference a OneShot Cue.";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }
                if (!runtimeCatalog.TryGetEvent(cue.EventId, out AudioEventDefinition audioEvent)
                    || audioEvent.Kind != AudioEventKind.Event
                    || audioEvent.IsLoop)
                {
                    error = "Physical audio Cue " + cue.Id + " must reference a non-loop FMOD Event.";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }
                if (basis.MaterialPairId.IsValid
                    && !physicalAudioDefinitions.TryGetMaterialPair(basis.MaterialPairId, out _))
                {
                    error = "Physical audio Basis " + basis.Id.Value + " references missing material Pair " + basis.MaterialPairId.Value + ".";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }
                bool matchedFamily = false;
                for (int familyIndex = 0; familyIndex < physicalAudioDefinitions.FamilyCount; familyIndex++)
                {
                    PhysicalAudioFamilyDefinition family = physicalAudioDefinitions.GetFamilyAt(familyIndex);
                    bool selectsBasis = basis.MaterialPairId.IsValid
                        ? family.VariantKind == basis.VariantKind
                        : family.Id == basis.FamilyId;
                    if (!selectsBasis)
                        continue;
                    matchedFamily = true;
                    if (family.ResponseProfileId.IsValid)
                    {
                        if (!physicalAudioDefinitions.TryGetResponseProfile(
                                family.ResponseProfileId,
                                out PhysicalAudioResponseProfileDefinition responseProfile))
                        {
                            error = "Physical audio family " + family.Id.Value + " references missing response profile " + family.ResponseProfileId.Value + ".";
                            bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                            return false;
                        }
                        for (int curveIndex = 0; curveIndex < responseProfile.CurveCount; curveIndex++)
                        {
                            int parameterId = responseProfile.GetCurve(curveIndex).ParameterId;
                            if (!IsBackendNativePhysicalParameter(parameterId)
                                && !runtimeCatalog.TryGetParameter(audioEvent.Id, parameterId, out _))
                            {
                                error = "Physical audio Event " + audioEvent.Id + " is missing required FMOD parameter " + parameterId + ".";
                                bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                                return false;
                            }
                        }
                    }
                }
                if (!matchedFamily)
                {
                    error = "Physical audio Basis " + basis.Id.Value + " has no Family selecting variant " + basis.VariantKind + ".";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }

                bindings[i] = new CharacterTestPhysicalAudioPlaybackBinding(
                    cue.Id,
                    audioEvent.Id,
                    audioEvent.Is3D,
                    cue.Name);
            }

            Array.Sort(bindings, ComparePhysicalAudioBindings);
            for (int i = 1; i < bindings.Length; i++)
            {
                if (bindings[i - 1].CueId == bindings[i].CueId)
                {
                    error = "Physical audio Basis Sounds must reference unique runtime Cues; duplicated Cue " + bindings[i].CueId + ".";
                    bindings = Array.Empty<CharacterTestPhysicalAudioPlaybackBinding>();
                    return false;
                }
            }
            return true;
        }

        private static bool IsBackendNativePhysicalParameter(int parameterId)
        {
            return parameterId == PhysicalAudioVolumeParameterId
                || parameterId == PhysicalAudioPitchParameterId;
        }

        private static int ComparePhysicalAudioBindings(
            CharacterTestPhysicalAudioPlaybackBinding left,
            CharacterTestPhysicalAudioPlaybackBinding right)
        {
            return left.CueId.CompareTo(right.CueId);
        }
    }
}
