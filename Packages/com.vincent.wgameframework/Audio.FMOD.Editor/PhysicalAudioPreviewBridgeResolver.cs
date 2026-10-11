using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;

namespace MxFramework.Audio.FMOD.Editor
{
    /// <summary>Low-frequency Unity Editor adapter that resolves a physical preview request with runtime code.</summary>
    /// <remarks>LowFreqAlloc. Used only by the Audio Workbench Preview Bridge.</remarks>
    internal static class PhysicalAudioPreviewBridgeResolver
    {
        internal const int PhysicalVolumeParameterId = 500251;
        internal const int PhysicalPitchParameterId = 500252;
        private const string CatalogDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_preview_catalogs/";

        internal static bool TryResolve(
            string projectRoot,
            PhysicalAudioPreviewRequest request,
            out PhysicalAudioPreviewResolution resolution,
            out string error)
        {
            resolution = null;
            error = string.Empty;
            if (request == null)
            {
                error = "Physical preview payload is missing.";
                return false;
            }

            if (!TryResolveCatalogPath(projectRoot, request.runtimeCatalogPath, out string catalogPath, out error))
                return false;
            if (!File.Exists(catalogPath))
            {
                error = "Physical preview runtime catalog does not exist.";
                return false;
            }

            string catalogJson = File.ReadAllText(catalogPath);
            string catalogHash = ComputeTextSha256(catalogJson);
            if (!string.Equals(catalogHash, request.runtimeCatalogHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Physical preview runtime catalog hash does not match the request.";
                return false;
            }
            if (!PhysicalAudioRuntimeCatalogJson.TryLoad(catalogJson, out PhysicalAudioDefinitionCatalog catalog, out error))
                return false;
            if (!string.Equals(catalog.PhysicalManifestHash, request.physicalManifestHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(catalog.AudioCatalogHash, request.audioCatalogHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Physical preview catalog authority hashes do not match the request.";
                return false;
            }

            if (!TryBuildFact(request.fact, out PhysicalAudioInteractionFact fact, out error))
                return false;
            var parameters = new AudioParameterValue[Math.Max(1, catalog.MaxResponseParameterCount)];
            var resolver = new PhysicalAudioResolver(catalog);
            if (!resolver.TryResolve(fact, parameters.AsSpan(), out PhysicalAudioResolvedPlayback playback))
            {
                error = "Runtime PhysicalAudioResolver failed: " + playback.FailureReason + ".";
                return false;
            }
            if (playback.BasisId.Value != request.expectedRuntimeBasisId
                || playback.CueId != request.expectedRuntimeCueId)
            {
                error = "Unity runtime resolution differs from the Authoring expectation. expected Basis/Cue="
                    + request.expectedRuntimeBasisId + "/" + request.expectedRuntimeCueId
                    + ", actual=" + playback.BasisId.Value + "/" + playback.CueId + ".";
                return false;
            }

            var namesById = new Dictionary<int, string>();
            for (int i = 0; request.parameterBindings != null && i < request.parameterBindings.Count; i++)
            {
                PhysicalAudioPreviewParameterBinding binding = request.parameterBindings[i];
                if (binding == null || binding.parameterId <= 0 || string.IsNullOrWhiteSpace(binding.name))
                    continue;
                namesById[binding.parameterId] = binding.name;
            }

            var eventParameters = new Dictionary<string, float>(StringComparer.Ordinal);
            var resolvedParameters = new List<PhysicalAudioPreviewResolvedParameter>(playback.ParameterCount);
            float volume = 1f;
            float pitch = 1f;
            for (int i = 0; i < playback.ParameterCount; i++)
            {
                AudioParameterValue parameter = parameters[i];
                namesById.TryGetValue(parameter.ParameterId, out string name);
                if (parameter.ParameterId == PhysicalVolumeParameterId)
                {
                    volume = Clamp(parameter.Value, 0f, 2f);
                    name = string.IsNullOrWhiteSpace(name) ? "MxPhysicalVolume" : name;
                }
                else if (parameter.ParameterId == PhysicalPitchParameterId)
                {
                    pitch = Clamp(parameter.Value, 0.01f, 4f);
                    name = string.IsNullOrWhiteSpace(name) ? "MxPhysicalPitch" : name;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        error = "Physical preview parameter has no FMOD binding: " + parameter.ParameterId + ".";
                        return false;
                    }
                    eventParameters[name] = parameter.Value;
                }
                resolvedParameters.Add(new PhysicalAudioPreviewResolvedParameter
                {
                    parameterId = parameter.ParameterId,
                    name = name ?? string.Empty,
                    value = parameter.Value
                });
            }

            resolution = new PhysicalAudioPreviewResolution
            {
                basisId = request.expectedBasisId ?? string.Empty,
                runtimeBasisId = playback.BasisId.Value,
                audioCueId = request.expectedAudioCueId ?? string.Empty,
                runtimeCueId = playback.CueId,
                usedFallback = playback.UsedFallback,
                matchDistance = playback.MatchDistance,
                volume = volume,
                pitch = pitch,
                contactNormal = new PhysicalAudioPreviewDirection
                {
                    x = fact.ContactNormal.X,
                    y = fact.ContactNormal.Y,
                    z = fact.ContactNormal.Z
                },
                eventParameters = eventParameters,
                parameters = resolvedParameters
            };
            return true;
        }

        private static bool TryResolveCatalogPath(string projectRoot, string relativePath, out string catalogPath, out string error)
        {
            catalogPath = string.Empty;
            error = string.Empty;
            string normalized = (relativePath ?? string.Empty).Replace('\\', '/');
            if (Path.IsPathRooted(normalized)
                || !normalized.StartsWith(CatalogDirectoryRelativePath, StringComparison.Ordinal)
                || normalized.Contains("..", StringComparison.Ordinal))
            {
                error = "Physical preview runtime catalog path is outside the Temp shelf.";
                return false;
            }
            string root = Path.GetFullPath(projectRoot ?? string.Empty);
            string allowedDirectory = Path.GetFullPath(Path.Combine(root, CatalogDirectoryRelativePath));
            string candidate = Path.GetFullPath(Path.Combine(root, normalized));
            if (!candidate.StartsWith(allowedDirectory, StringComparison.Ordinal))
            {
                error = "Physical preview runtime catalog path escaped the Temp shelf.";
                return false;
            }
            catalogPath = candidate;
            return true;
        }

        private static bool TryBuildFact(
            PhysicalAudioPreviewFact source,
            out PhysicalAudioInteractionFact fact,
            out string error)
        {
            fact = default;
            error = string.Empty;
            source ??= new PhysicalAudioPreviewFact();
            if (!TryParseEnum(source.interactionKind, out PhysicalAudioInteractionKind kind)
                || !TryParseEnum(source.interactionMode, out PhysicalAudioInteractionMode mode)
                || !TryParseEnum(source.phase, out PhysicalAudioInteractionPhase phase)
                || !TryBuildBody(source.impactor, out AcousticBodyProperties impactor)
                || !TryBuildBody(source.receiver, out AcousticBodyProperties receiver))
            {
                error = "Physical preview fact contains an invalid enum or acoustic body.";
                return false;
            }
            PhysicalAudioPreviewPosition position = source.position ?? new PhysicalAudioPreviewPosition();
            PhysicalAudioPreviewDirection contactNormal = source.contactNormal ?? new PhysicalAudioPreviewDirection();
            var transform = new AudioTransform(position.x, position.y, position.z);
            fact = new PhysicalAudioInteractionFact(
                kind,
                mode,
                phase,
                impactor,
                receiver,
                source.relativeSpeed,
                source.normalSpeed,
                source.tangentialSpeed,
                source.pressure,
                source.impulse,
                source.angleDegrees,
                source.intensity,
                new PhysicalAudioDirection(contactNormal.x, contactNormal.y, contactNormal.z),
                transform,
                source.emitterId,
                source.frame,
                source.sequence);
            return true;
        }

        private static bool TryBuildBody(PhysicalAudioPreviewBody source, out AcousticBodyProperties body)
        {
            body = default;
            if (source == null
                || source.runtimeMaterialId <= 0
                || !TryParseEnum(source.surface, out AcousticSurfaceKind surface)
                || !TryParseEnum(source.construction, out AcousticConstructionKind construction))
                return false;
            body = new AcousticBodyProperties(
                new AcousticMaterialId(source.runtimeMaterialId),
                source.mass,
                source.contactArea,
                surface,
                construction);
            return true;
        }

        private static bool TryParseEnum<T>(string value, out T result) where T : struct
        {
            result = default;
            return !string.IsNullOrWhiteSpace(value)
                && Enum.TryParse(value, true, out result)
                && Enum.IsDefined(typeof(T), result);
        }

        private static string ComputeTextSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || value <= min)
                return min;
            return value >= max ? max : value;
        }
    }

    internal sealed class PhysicalAudioPreviewRequest
    {
        public string runtimeCatalogPath = string.Empty;
        public string runtimeCatalogHash = string.Empty;
        public string physicalManifestHash = string.Empty;
        public string audioCatalogHash = string.Empty;
        public PhysicalAudioPreviewFact fact = new PhysicalAudioPreviewFact();
        public string expectedBasisId = string.Empty;
        public int expectedRuntimeBasisId;
        public string expectedAudioCueId = string.Empty;
        public int expectedRuntimeCueId;
        public List<PhysicalAudioPreviewParameterBinding> parameterBindings = new List<PhysicalAudioPreviewParameterBinding>();
    }

    internal sealed class PhysicalAudioPreviewFact
    {
        public string interactionKind = string.Empty;
        public string interactionMode = string.Empty;
        public string phase = string.Empty;
        public string dynamicsSource = string.Empty;
        public PhysicalAudioPreviewBody impactor = new PhysicalAudioPreviewBody();
        public PhysicalAudioPreviewBody receiver = new PhysicalAudioPreviewBody();
        public float relativeSpeed;
        public float normalSpeed;
        public float tangentialSpeed;
        public float pressure;
        public float impulse;
        public float angleDegrees;
        public float intensity;
        public float rawNormalImpulseNewtonSeconds;
        public float rawAveragePressurePascals;
        public PhysicalAudioPreviewDirection contactNormal = new PhysicalAudioPreviewDirection();
        public PhysicalAudioPreviewPosition position = new PhysicalAudioPreviewPosition();
        public int emitterId;
        public long frame;
        public long sequence;
    }

    internal sealed class PhysicalAudioPreviewBody
    {
        public int runtimeMaterialId;
        public float mass;
        public float contactArea;
        public string surface = string.Empty;
        public string construction = string.Empty;
    }

    internal sealed class PhysicalAudioPreviewPosition
    {
        public float x;
        public float y;
        public float z;
    }

    internal sealed class PhysicalAudioPreviewDirection
    {
        public float x;
        public float y;
        public float z;
    }

    internal sealed class PhysicalAudioPreviewParameterBinding
    {
        public int parameterId;
        public string name = string.Empty;
    }

    internal sealed class PhysicalAudioPreviewResolution
    {
        public string basisId = string.Empty;
        public int runtimeBasisId;
        public string audioCueId = string.Empty;
        public int runtimeCueId;
        public bool usedFallback;
        public float matchDistance;
        public float volume = 1f;
        public float pitch = 1f;
        public PhysicalAudioPreviewDirection contactNormal = new PhysicalAudioPreviewDirection();
        public Dictionary<string, float> eventParameters = new Dictionary<string, float>(StringComparer.Ordinal);
        public List<PhysicalAudioPreviewResolvedParameter> parameters = new List<PhysicalAudioPreviewResolvedParameter>();
    }

    internal sealed class PhysicalAudioPreviewResolvedParameter
    {
        public int parameterId;
        public string name = string.Empty;
        public float value;
    }
}
