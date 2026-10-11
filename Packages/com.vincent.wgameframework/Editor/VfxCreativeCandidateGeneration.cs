using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Creates procedural support assets from a small, data-only whitelist.</summary>
    /// <remarks>Editor-only and AllocByDesign. Unity serialization is always performed through AssetDatabase.</remarks>
    internal sealed class VfxDefaultSupportAssetProvider : IVfxSupportAssetProvider
    {
        private static readonly HashSet<string> AllowedShaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Particles/Standard Unlit"
        };

        public bool TryCreateAssets(VfxCandidatePlan plan, string sessionRoot, out VfxSupportAssetSet assets, out string error)
        {
            assets = new VfxSupportAssetSet();
            error = string.Empty;
            try
            {
                string textureRoot = sessionRoot + "/Textures";
                string materialRoot = sessionRoot + "/Materials";
                var textureIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < plan.Textures.Count; i++)
                {
                    VfxProceduralTextureSpec spec = plan.Textures[i] ?? throw new InvalidOperationException("VFX texture spec cannot be null.");
                    RequireIdentifier(spec.TextureId, "textureId");
                    if (!textureIds.Add(spec.TextureId))
                        throw new InvalidOperationException("Duplicate VFX textureId: " + spec.TextureId);
                    string stem = RequireAssetStem(spec.AssetName, "texture assetName");
                    int width = Mathf.Clamp(spec.Width, 4, 512);
                    int height = Mathf.Clamp(spec.Height, 4, 512);
                    VfxCandidateSandbox.EnsureAssetFolder(textureRoot);
                    string path = textureRoot + "/" + stem + ".asset";
                    EnsureMissing(path);
                    Texture2D texture = CreateTexture(spec, width, height, plan.FixedSeed);
                    texture.name = stem;
                    AssetDatabase.CreateAsset(texture, path);
                    assets.TexturePaths.Add(spec.TextureId, path);
                    assets.CreatedAssetPaths.Add(path);
                }

                var materialIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < plan.Materials.Count; i++)
                {
                    VfxMaterialSpec spec = plan.Materials[i] ?? throw new InvalidOperationException("VFX material spec cannot be null.");
                    RequireIdentifier(spec.MaterialId, "materialId");
                    if (!materialIds.Add(spec.MaterialId))
                        throw new InvalidOperationException("Duplicate VFX materialId: " + spec.MaterialId);
                    if (!AllowedShaders.Contains(spec.ShaderName))
                        throw new InvalidOperationException("Shader is outside the VFX candidate whitelist: " + spec.ShaderName);
                    Shader shader = Shader.Find(spec.ShaderName);
                    if (shader == null)
                        throw new InvalidOperationException("Whitelisted VFX shader is unavailable: " + spec.ShaderName);
                    string stem = RequireAssetStem(spec.AssetName, "material assetName");
                    VfxCandidateSandbox.EnsureAssetFolder(materialRoot);
                    string path = materialRoot + "/" + stem + ".mat";
                    EnsureMissing(path);
                    var material = new Material(shader) { name = stem };
                    Color color = ToColor(spec.Color);
                    SetColorIfPresent(material, "_BaseColor", color);
                    SetColorIfPresent(material, "_Color", color);
                    if (!string.IsNullOrWhiteSpace(spec.TextureId))
                    {
                        if (!assets.TexturePaths.TryGetValue(spec.TextureId, out string texturePath))
                            throw new InvalidOperationException("VFX material references an unknown textureId: " + spec.TextureId);
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        SetTextureIfPresent(material, "_BaseMap", texture);
                        SetTextureIfPresent(material, "_MainTex", texture);
                    }
                    SetFloatIfPresent(material, "_Surface", 1f);
                    SetFloatIfPresent(material, "_ZWrite", 0f);
                    material.renderQueue = 3000;
                    AssetDatabase.CreateAsset(material, path);
                    assets.MaterialPaths.Add(spec.MaterialId, path);
                    assets.CreatedAssetPaths.Add(path);
                }

                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static Texture2D CreateTexture(VfxProceduralTextureSpec spec, int width, int height, uint seed)
        {
            int frameColumns = Mathf.Clamp(spec.FrameColumns, 1, 8);
            int frameRows = Mathf.Clamp(spec.FrameRows, 1, 8);
            int frameCount = Mathf.Clamp(spec.FrameCount, 1, frameColumns * frameRows);
            if (spec.Kind == VfxProceduralTextureKind.Flipbook
                && (spec.FrameColumns != frameColumns || spec.FrameRows != frameRows || spec.FrameCount != frameCount
                    || width % frameColumns != 0 || height % frameRows != 0))
                throw new InvalidOperationException("Flipbook texture requires 1-8 columns/rows, bounded frameCount, and evenly divisible dimensions.");
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, spec.Linear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color tint = ToColor(spec.Color);
            var pixels = new Color[width * height];
            var random = new System.Random(unchecked((int)(seed ^ ComputeStableTextureSalt(spec.TextureId ?? string.Empty))));
            for (int y = 0; y < height; y++)
            {
                float vertical = ((y + 0.5f) / height) * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float horizontal = ((x + 0.5f) / width) * 2f - 1f;
                    float radius = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
                    float alpha;
                    switch (spec.Kind)
                    {
                        case VfxProceduralTextureKind.Ring:
                            alpha = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.62f) / 0.12f);
                            break;
                        case VfxProceduralTextureKind.Noise:
                            alpha = (float)random.NextDouble();
                            break;
                        case VfxProceduralTextureKind.Sdf:
                            alpha = Mathf.Clamp01(0.5f - (radius - 0.55f) * 0.8f);
                            break;
                        case VfxProceduralTextureKind.Flow:
                            float inverseRadius = 1f / Mathf.Max(0.0001f, radius);
                            float flowX = -vertical * inverseRadius;
                            float flowY = horizontal * inverseRadius;
                            float jitter = ((float)random.NextDouble() - 0.5f) * 0.04f;
                            pixels[y * width + x] = new Color(
                                Mathf.Clamp01(flowX * 0.5f + 0.5f + jitter),
                                Mathf.Clamp01(flowY * 0.5f + 0.5f - jitter),
                                0.5f,
                                Mathf.Clamp01(1f - radius));
                            continue;
                        case VfxProceduralTextureKind.RadialMask:
                            float angle = Mathf.Atan2(vertical, horizontal);
                            float spokes = 0.5f + 0.5f * Mathf.Cos(angle * 8f);
                            alpha = Mathf.Clamp01(1f - radius) * spokes;
                            break;
                        case VfxProceduralTextureKind.Flipbook:
                            int cellWidth = width / frameColumns;
                            int cellHeight = height / frameRows;
                            int cellX = x / cellWidth;
                            int cellY = y / cellHeight;
                            int frame = cellY * frameColumns + cellX;
                            if (frame >= frameCount)
                            {
                                alpha = 0f;
                                break;
                            }
                            float localX = ((x % cellWidth) + 0.5f) / cellWidth * 2f - 1f;
                            float localY = ((y % cellHeight) + 0.5f) / cellHeight * 2f - 1f;
                            float phase = frameCount > 1 ? frame / (float)(frameCount - 1) : 0f;
                            localX -= Mathf.Sin(phase * Mathf.PI * 2f) * 0.18f;
                            localY -= Mathf.Cos(phase * Mathf.PI * 2f) * 0.12f;
                            float localRadius = Mathf.Sqrt(localX * localX + localY * localY);
                            alpha = Mathf.Clamp01(1f - localRadius) * (1f - phase * 0.45f);
                            break;
                        default:
                            alpha = Mathf.Clamp01(1f - radius);
                            alpha *= alpha;
                            break;
                    }
                    pixels[y * width + x] = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        internal static uint ComputeStableTextureSalt(string value)
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offsetBasis;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                hash = unchecked((hash ^ (byte)character) * prime);
                hash = unchecked((hash ^ (byte)(character >> 8)) * prime);
            }
            return hash;
        }

        private static void EnsureMissing(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                throw new InvalidOperationException("VFX candidate asset already exists: " + path);
        }

        private static string RequireAssetStem(string value, string field)
        {
            RequireIdentifier(value, field);
            return value;
        }

        private static void RequireIdentifier(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("VFX candidate " + field + " is required.");
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool valid = char.IsLetterOrDigit(character) || character == '-' || character == '_';
                if (!valid)
                    throw new InvalidOperationException("VFX candidate " + field + " contains an invalid character: " + value);
            }
        }

        private static Color ToColor(VfxColorSpec value)
        {
            value ??= new VfxColorSpec();
            return new Color(value.R, value.G, value.B, value.A);
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture value)
        {
            if (material.HasProperty(property))
                material.SetTexture(property, value);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }

    /// <summary>Generates deterministic ParticleSystem, LineRenderer, or certified-template VFX Graph candidates.</summary>
    /// <remarks>Editor-only and AllocByDesign. Generated assets remain Draft in an isolated candidate sandbox.</remarks>
    internal sealed class VfxEditorCandidateGenerator : IVfxCandidateGenerator
    {
        private readonly IVfxSupportAssetProvider _supportAssetProvider;

        public VfxEditorCandidateGenerator()
            : this(new VfxDefaultSupportAssetProvider())
        {
        }

        internal VfxEditorCandidateGenerator(IVfxSupportAssetProvider supportAssetProvider)
        {
            _supportAssetProvider = supportAssetProvider ?? throw new ArgumentNullException(nameof(supportAssetProvider));
        }

        public VfxCandidateGenerationResult Generate(VfxCandidatePlan plan)
        {
            string sessionRoot = string.Empty;
            string outputRoot = string.Empty;
            bool createdOutput = false;
            try
            {
                ValidatePlan(plan);
                if (!VfxCandidateSandbox.TryGetSessionRoot(plan.SessionId, out sessionRoot, out string pathError))
                    throw new InvalidOperationException(pathError);
                outputRoot = string.IsNullOrWhiteSpace(plan.CandidateOutputRoot)
                    ? sessionRoot
                    : VfxCandidateSandbox.NormalizeAssetPath(plan.CandidateOutputRoot).TrimEnd('/');
                bool outputAtSessionRoot = string.Equals(outputRoot, sessionRoot, StringComparison.Ordinal);
                if (!outputAtSessionRoot && !VfxCandidateSandbox.IsWithinSessionRoot(outputRoot + "/placeholder.asset", sessionRoot))
                    throw new InvalidOperationException("Core candidate outputRoot escaped its shared generation session: " + outputRoot);
                if (!outputAtSessionRoot && string.IsNullOrWhiteSpace(plan.CoreCandidateId))
                    throw new InvalidOperationException("A nested Core candidate outputRoot requires coreCandidateId binding.");
                if (AssetDatabase.IsValidFolder(outputRoot) || Directory.Exists(outputRoot))
                    throw new InvalidOperationException("VFX candidate output root already exists; generation is append-free: " + outputRoot);
                VfxCandidateSandbox.EnsureAssetFolder(outputRoot);
                createdOutput = true;

                if (!_supportAssetProvider.TryCreateAssets(plan, outputRoot, out VfxSupportAssetSet supportAssets, out string supportError))
                    throw new InvalidOperationException("Could not create VFX candidate support assets: " + supportError);

                string mainAssetPath = CreateMainAsset(plan, outputRoot, supportAssets);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(mainAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var receipt = new VfxCandidateReceipt
                {
                    PlanId = plan.PlanId,
                    PlanHash = VfxCandidateSandbox.ComputePlanHash(plan),
                    SessionId = plan.SessionId,
                    GeneratorId = plan.GeneratorId,
                    GeneratorVersion = plan.GeneratorVersion,
                    CandidateKind = plan.CandidateKind,
                    CoreCandidateId = plan.CoreCandidateId,
                    FixedSeed = plan.FixedSeed,
                    ResolvedAssetRoot = outputRoot,
                    TopologyPolicy = plan.CandidateKind == VfxCandidateKind.GraphTemplateClone
                        ? "CertifiedTemplateCloneOnly;VersionLocked;DraftOnly"
                        : "NoGraphTopologyMutation",
                    GraphParameters = plan.GraphTemplate?.Parameters ?? new List<VfxGraphParameterMetadata>(),
                    Provenance = new List<VfxCandidateProvenanceEntry>(plan.Provenance)
                };

                for (int i = 0; i < supportAssets.CreatedAssetPaths.Count; i++)
                {
                    string supportPath = supportAssets.CreatedAssetPaths[i];
                    string kind = AssetDatabase.LoadAssetAtPath<Material>(supportPath) != null ? "Material" : "Texture";
                    receipt.Assets.Add(VfxCandidateSandbox.CreateAssetReceipt(kind, supportPath, outputRoot));
                }
                string mainKind = plan.CandidateKind == VfxCandidateKind.GraphTemplateClone ? "VfxGraph" : "Prefab";
                receipt.Assets.Add(VfxCandidateSandbox.CreateAssetReceipt(mainKind, mainAssetPath, outputRoot));

                string receiptPath = outputRoot + "/editor_candidate_receipt.json";
                VfxCandidateSandbox.WriteJsonAsset(receiptPath, receipt);
                AssetDatabase.SaveAssets();
                return new VfxCandidateGenerationResult
                {
                    Success = true,
                    Message = "VFX candidate generated as Draft. Human artistic review is still required.",
                    SessionRoot = outputRoot,
                    ReceiptAssetPath = receiptPath,
                    Receipt = receipt
                };
            }
            catch (Exception exception)
            {
                if (createdOutput && VfxCandidateSandbox.IsCandidateRoot(outputRoot))
                    AssetDatabase.DeleteAsset(outputRoot);
                return new VfxCandidateGenerationResult
                {
                    Success = false,
                    Message = exception.Message,
                    SessionRoot = outputRoot
                };
            }
        }

        internal static bool TryValidateGraphTemplatePolicy(VfxGraphTemplateCloneSpec spec, out string error)
        {
            error = "VFX Graph candidate generation is unavailable until a certified generation-Catalog template, wrapper Prefab, exposed-property bindings, fallback, and preview capture are implemented together.";
            return false;
        }

        private string CreateMainAsset(VfxCandidatePlan plan, string sessionRoot, VfxSupportAssetSet supportAssets)
        {
            string assetStem = RequireAssetStem(plan.OutputAssetName, "outputAssetName");
            switch (plan.CandidateKind)
            {
                case VfxCandidateKind.ParticleSystemPrimitive:
                    return CreateParticlePrefab(plan, plan.ParticleSystem, supportAssets, sessionRoot + "/Prefabs/" + assetStem + ".prefab");
                case VfxCandidateKind.LineRendererPrimitive:
                    return CreateLinePrefab(plan, plan.LineRenderer, supportAssets, sessionRoot + "/Prefabs/" + assetStem + ".prefab");
                case VfxCandidateKind.GraphTemplateClone:
                    throw new NotSupportedException("VFX Graph candidate generation is fail-closed; no Graph asset or false preview evidence was created.");
                default:
                    throw new NotSupportedException("Unsupported VFX candidate kind: " + plan.CandidateKind);
            }
        }

        private static string CreateParticlePrefab(VfxCandidatePlan plan, ParticleSystemPrimitiveSpec spec, VfxSupportAssetSet assets, string path)
        {
            if (spec == null)
                throw new InvalidOperationException("ParticleSystem candidate requires particleSystem spec.");
            VfxCandidateSandbox.EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var root = new GameObject(plan.DisplayName);
            try
            {
                ParticleSystem particle = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particle.main;
                main.duration = Mathf.Max(0.05f, spec.DurationSeconds);
                main.loop = spec.Looping;
                main.playOnAwake = true;
                main.startLifetime = Mathf.Max(0.01f, spec.StartLifetimeSeconds);
                main.startSpeed = spec.StartSpeed;
                main.startSize = Mathf.Max(0f, spec.StartSize);
                main.startColor = ToColor(spec.StartColor);
                main.maxParticles = Mathf.Clamp(spec.MaxParticles, 1, 100000);
                main.gravityModifier = spec.GravityModifier;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.useUnscaledTime = false;
                particle.useAutoRandomSeed = false;
                particle.randomSeed = plan.FixedSeed == 0u ? 1u : plan.FixedSeed;

                ParticleSystem.EmissionModule emission = particle.emission;
                emission.enabled = spec.EmissionRatePerSecond > 0f || spec.Bursts.Count > 0;
                emission.rateOverTime = Mathf.Max(0f, spec.EmissionRatePerSecond);
                if (spec.Bursts.Count > 0)
                {
                    var bursts = new ParticleSystem.Burst[spec.Bursts.Count];
                    for (int i = 0; i < bursts.Length; i++)
                    {
                        VfxBurstSpec value = spec.Bursts[i] ?? throw new InvalidOperationException("ParticleSystem burst cannot be null.");
                        var burst = new ParticleSystem.Burst(Mathf.Max(0f, value.Time), (short)Mathf.Max(1, value.Count));
                        burst.cycleCount = Mathf.Max(1, value.Cycles);
                        burst.repeatInterval = Mathf.Max(0.01f, value.Interval);
                        bursts[i] = burst;
                    }
                    emission.SetBursts(bursts);
                }

                ParticleSystem.ShapeModule shape = particle.shape;
                shape.enabled = true;
                shape.shapeType = ParseShape(spec.Shape);
                shape.radius = Mathf.Max(0f, spec.ShapeRadius);
                if (shape.shapeType == ParticleSystemShapeType.Cone)
                    shape.angle = Mathf.Clamp(spec.ShapeAngleDegrees, 0f, 90f);

                ParticleSystem.NoiseModule noise = particle.noise;
                noise.enabled = spec.NoiseStrength > 0f;
                noise.strength = Mathf.Max(0f, spec.NoiseStrength);
                noise.frequency = Mathf.Max(0.01f, spec.NoiseFrequency);

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(ToColor(spec.StartColor), 0f), new GradientColorKey(ToColor(spec.EndColor), 1f) },
                    new[] { new GradientAlphaKey(ToColor(spec.StartColor).a, 0f), new GradientAlphaKey(ToColor(spec.EndColor).a, 1f) });
                colorOverLifetime.color = gradient;

                if (!string.IsNullOrWhiteSpace(spec.MaterialId))
                {
                    if (!assets.MaterialPaths.TryGetValue(spec.MaterialId, out string materialPath))
                        throw new InvalidOperationException("ParticleSystem references an unknown materialId: " + spec.MaterialId);
                    root.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                    throw new InvalidOperationException("Unity failed to save ParticleSystem candidate Prefab: " + path);
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string CreateLinePrefab(VfxCandidatePlan plan, LineRendererPrimitiveSpec spec, VfxSupportAssetSet assets, string path)
        {
            if (spec == null)
                throw new InvalidOperationException("LineRenderer candidate requires lineRenderer spec.");
            if (spec.Positions.Count < 2)
                throw new InvalidOperationException("LineRenderer candidate requires at least two positions.");
            VfxCandidateSandbox.EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var root = new GameObject(plan.DisplayName);
            try
            {
                LineRenderer line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = spec.Loop;
                line.startWidth = Mathf.Max(0f, spec.StartWidth);
                line.endWidth = Mathf.Max(0f, spec.EndWidth);
                line.startColor = ToColor(spec.StartColor);
                line.endColor = ToColor(spec.EndColor);
                line.numCornerVertices = Mathf.Clamp(spec.CornerVertices, 0, 16);
                line.numCapVertices = Mathf.Clamp(spec.CapVertices, 0, 16);
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.positionCount = spec.Positions.Count;
                var positions = new Vector3[spec.Positions.Count];
                for (int i = 0; i < positions.Length; i++)
                {
                    VfxVector3Spec value = spec.Positions[i] ?? throw new InvalidOperationException("LineRenderer position cannot be null.");
                    positions[i] = new Vector3(value.X, value.Y, value.Z);
                }
                line.SetPositions(positions);
                if (!string.IsNullOrWhiteSpace(spec.MaterialId))
                {
                    if (!assets.MaterialPaths.TryGetValue(spec.MaterialId, out string materialPath))
                        throw new InvalidOperationException("LineRenderer references an unknown materialId: " + spec.MaterialId);
                    line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                }
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                    throw new InvalidOperationException("Unity failed to save LineRenderer candidate Prefab: " + path);
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidatePlan(VfxCandidatePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!string.Equals(plan.SchemaVersion, VfxCandidateProtocol.PlanSchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported VFX candidate plan schema: " + plan.SchemaVersion);
            if (string.IsNullOrWhiteSpace(plan.PlanId))
                throw new InvalidOperationException("VFX candidate planId is required.");
            if (string.IsNullOrWhiteSpace(plan.GeneratorId) || string.IsNullOrWhiteSpace(plan.GeneratorVersion))
                throw new InvalidOperationException("VFX candidate generator identity and version are required.");
            if (string.IsNullOrWhiteSpace(plan.DisplayName))
                throw new InvalidOperationException("VFX candidate displayName is required.");
            RequireAssetStem(plan.OutputAssetName, "outputAssetName");
            if (plan.FixedSeed == 0u)
                throw new InvalidOperationException("VFX candidate fixedSeed must be non-zero.");
            if (plan.CandidateKind == VfxCandidateKind.GraphTemplateClone
                && !TryValidateGraphTemplatePolicy(plan.GraphTemplate, out string error))
                throw new InvalidOperationException(error);
        }

        private static ParticleSystemShapeType ParseShape(string shape)
        {
            switch (shape)
            {
                case "Sphere": return ParticleSystemShapeType.Sphere;
                case "Cone": return ParticleSystemShapeType.Cone;
                case "Circle": return ParticleSystemShapeType.Circle;
                default: throw new InvalidOperationException("ParticleSystem shape is outside the candidate whitelist: " + shape);
            }
        }

        private static string RequireAssetStem(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("VFX candidate " + field + " is required.");
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                    throw new InvalidOperationException("VFX candidate " + field + " contains an invalid character: " + value);
            }
            return value;
        }

        private static Color ToColor(VfxColorSpec value)
        {
            value ??= new VfxColorSpec();
            return new Color(value.R, value.G, value.B, value.A);
        }
    }
}
