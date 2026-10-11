using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class VfxParticleSystemSnapshotCapture
    {
        public static VfxParticleSystemSnapshotView Capture(VfxSourceComponentView source, ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.ShapeModule shape = system.shape;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            ParticleSystem.NoiseModule noise = system.noise;
            ParticleSystem.TextureSheetAnimationModule texture = system.textureSheetAnimation;
            ParticleSystem.TrailModule trails = system.trails;
            ParticleSystem.CustomDataModule customData = system.customData;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            var result = new VfxParticleSystemSnapshotView
            {
                ComponentId = source?.ComponentId ?? system.name,
                SourcePrefabGuid = source?.PrefabGuid ?? string.Empty,
                SourceContentHash = source?.SourceContentHash ?? string.Empty,
                LocalFileId = source?.LocalFileId ?? 0,
                TransformPath = source?.ObjectPath ?? system.name,
                LocalPosition = Vector(system.transform.localPosition),
                LocalEulerAngles = Vector(system.transform.localEulerAngles),
                LocalScale = Vector(system.transform.localScale),
                DurationSeconds = main.duration,
                Loop = main.loop,
                Prewarm = main.prewarm,
                SimulationSpeed = main.simulationSpeed,
                SimulationSpace = (int)main.simulationSpace,
                ScalingMode = (int)main.scalingMode,
                MaxParticles = main.maxParticles,
                StartDelay = Curve(main.startDelay),
                StartLifetime = Curve(main.startLifetime),
                StartSpeed = Curve(main.startSpeed),
                StartSize3D = main.startSize3D,
                StartSize = Curve(main.startSize),
                StartSizeX = Curve(main.startSizeX),
                StartSizeY = Curve(main.startSizeY),
                StartSizeZ = Curve(main.startSizeZ),
                StartRotation3D = main.startRotation3D,
                StartRotation = Curve(main.startRotation),
                StartRotationX = Curve(main.startRotationX),
                StartRotationY = Curve(main.startRotationY),
                StartRotationZ = Curve(main.startRotationZ),
                StartColor = Gradient(main.startColor),
                GravityModifier = Curve(main.gravityModifier),
                RateOverTime = Curve(emission.rateOverTime),
                RateOverDistance = Curve(emission.rateOverDistance),
                ShapeType = (int)shape.shapeType,
                ShapeScale = Vector(shape.scale),
                ShapeRadius = shape.radius,
                ShapeAngle = shape.angle,
                ShapeArc = shape.arc,
                ShapeRandomDirection = shape.randomDirectionAmount,
                ColorOverLifetime = Gradient(color.color),
                SizeOverLifetimeX = Curve(size.x),
                SizeOverLifetimeY = Curve(size.y),
                SizeOverLifetimeZ = Curve(size.z),
                VelocityOverLifetimeX = Curve(velocity.x),
                VelocityOverLifetimeY = Curve(velocity.y),
                VelocityOverLifetimeZ = Curve(velocity.z),
                VelocitySpeedModifier = Curve(velocity.speedModifier),
                VelocitySpace = (int)velocity.space,
                ForceOverLifetimeX = Curve(force.x),
                ForceOverLifetimeY = Curve(force.y),
                ForceOverLifetimeZ = Curve(force.z),
                ForceSpace = (int)force.space,
                RotationOverLifetimeX = Curve(rotation.x),
                RotationOverLifetimeY = Curve(rotation.y),
                RotationOverLifetimeZ = Curve(rotation.z),
                LimitVelocity = Curve(limit.limit),
                Drag = Curve(limit.drag),
                NoiseStrength = Curve(noise.strength),
                NoiseFrequency = noise.frequency,
                NoiseScrollSpeed = Curve(noise.scrollSpeed),
                TextureFrameOverTime = Curve(texture.frameOverTime),
                TextureStartFrame = Curve(texture.startFrame),
                TextureCycleCount = texture.cycleCount,
                TextureTiles = new List<int> { texture.numTilesX, texture.numTilesY },
                Trails = CaptureTrails(trails),
                RendererMode = renderer != null ? (int)renderer.renderMode : 0,
                RendererAlignment = renderer != null ? (int)renderer.alignment : 0,
                RendererSortMode = renderer != null ? (int)renderer.sortMode : 0,
                SortingOrder = renderer != null ? renderer.sortingOrder : 0,
                RendererPivot = renderer != null ? Vector(renderer.pivot) : Vector(Vector3.zero),
                RendererMinParticleSize = renderer != null ? renderer.minParticleSize : 0f,
                RendererMaxParticleSize = renderer != null ? renderer.maxParticleSize : 0f,
                MeshGuid = renderer != null && renderer.mesh != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.mesh)) : string.Empty,
                RawEditorJson = EditorJsonUtility.ToJson(system, false)
            };
            if (emission.enabled) result.ModuleFlags.Add("Emission");
            if (system.shape.enabled) result.ModuleFlags.Add("Shape");
            if (system.colorOverLifetime.enabled) result.ModuleFlags.Add("ColorOverLifetime");
            if (system.colorBySpeed.enabled) result.ModuleFlags.Add("ColorBySpeed");
            if (system.sizeOverLifetime.enabled) result.ModuleFlags.Add("SizeOverLifetime");
            if (system.sizeBySpeed.enabled) result.ModuleFlags.Add("SizeBySpeed");
            if (system.velocityOverLifetime.enabled) result.ModuleFlags.Add("VelocityOverLifetime");
            if (system.rotationOverLifetime.enabled) result.ModuleFlags.Add("RotationOverLifetime");
            if (system.noise.enabled) result.ModuleFlags.Add("Noise");
            if (system.textureSheetAnimation.enabled) result.ModuleFlags.Add("TextureSheetAnimation");
            if (system.customData.enabled) result.ModuleFlags.Add("CustomData");
            if (system.trails.enabled) result.ModuleFlags.Add("Trails");
            if (system.subEmitters.enabled) result.ModuleFlags.Add("SubEmitters");
            if (system.collision.enabled) result.ModuleFlags.Add("Collision");
            if (system.limitVelocityOverLifetime.enabled) result.ModuleFlags.Add("LimitVelocityOverLifetime");
            if (renderer != null)
            {
                var streams = new List<ParticleSystemVertexStream>();
                renderer.GetActiveVertexStreams(streams);
                result.VertexStreams.AddRange(streams.Select(value => (int)value));
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(materials[i]));
                    if (!string.IsNullOrWhiteSpace(guid)) result.MaterialGuids.Add(guid);
                }
            }
            if (customData.enabled)
            {
                result.CustomData.Add(CustomData(customData, ParticleSystemCustomData.Custom1));
                result.CustomData.Add(CustomData(customData, ParticleSystemCustomData.Custom2));
            }
            result.ModuleFlags.Sort(StringComparer.Ordinal);
            result.MaterialGuids = result.MaterialGuids.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            result.SnapshotHash = VfxCanonicalJson.Hash(result, "snapshotHash");
            return result;
        }

        private static VfxParticleTrailSnapshotView CaptureTrails(ParticleSystem.TrailModule trails)
        {
            var result = new VfxParticleTrailSnapshotView
            {
                Enabled = trails.enabled,
                Ratio = trails.ratio,
                Lifetime = Curve(trails.lifetime),
                MinimumVertexDistance = trails.minVertexDistance,
                TextureMode = (int)trails.textureMode,
                WorldSpace = trails.worldSpace,
                DieWithParticles = trails.dieWithParticles,
                SizeAffectsWidth = trails.sizeAffectsWidth,
                SizeAffectsLifetime = trails.sizeAffectsLifetime,
                InheritParticleColor = trails.inheritParticleColor,
                ColorOverLifetime = Gradient(trails.colorOverLifetime),
                WidthOverTrail = Curve(trails.widthOverTrail),
                GenerateLightingData = trails.generateLightingData,
                RibbonCount = trails.ribbonCount,
                ShadowBias = trails.shadowBias,
                SplitSubEmitterRibbons = trails.splitSubEmitterRibbons,
                AttachRibbonsToTransform = trails.attachRibbonsToTransform
            };
            result.ContentHash = VfxCanonicalJson.Hash(result, "contentHash");
            return result;
        }

        private static List<float> Vector(Vector3 value) => new List<float> { value.x, value.y, value.z };

        private static List<float> ColorSnapshot(Color value) => new List<float> { value.r, value.g, value.b, value.a };

        private static VfxCustomDataStreamSnapshotView CustomData(ParticleSystem.CustomDataModule module, ParticleSystemCustomData stream)
        {
            ParticleSystemCustomDataMode mode = module.GetMode(stream);
            var result = new VfxCustomDataStreamSnapshotView { Stream = (int)stream, Mode = (int)mode };
            if (mode == ParticleSystemCustomDataMode.Color) result.Color = Gradient(module.GetColor(stream));
            else if (mode == ParticleSystemCustomDataMode.Vector)
            {
                for (int i = 0; i < 4; i++) result.Vector.Add(Curve(module.GetVector(stream, i)));
            }
            return result;
        }

        private static VfxMinMaxCurveSnapshotView Curve(ParticleSystem.MinMaxCurve value)
        {
            return new VfxMinMaxCurveSnapshotView
            {
                Mode = (int)value.mode,
                CurveMultiplier = value.curveMultiplier,
                ConstantMin = value.constantMin,
                ConstantMax = value.constantMax,
                CurveMin = Curve(value.curveMin),
                CurveMax = Curve(value.curveMax)
            };
        }

        private static VfxCurveSnapshotView Curve(AnimationCurve value)
        {
            if (value == null) return null;
            var result = new VfxCurveSnapshotView { PreWrapMode = (int)value.preWrapMode, PostWrapMode = (int)value.postWrapMode };
            Keyframe[] keys = value.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                result.Keys.Add(VfxCurveKeySnapshotView.Capture(keys[i], "particleSystem.curve.keys[" + i + "]"));
            }
            return result;
        }

        private static VfxMinMaxGradientSnapshotView Gradient(ParticleSystem.MinMaxGradient value)
        {
            return new VfxMinMaxGradientSnapshotView
            {
                Mode = (int)value.mode,
                ColorMin = ColorSnapshot(value.colorMin),
                ColorMax = ColorSnapshot(value.colorMax),
                GradientMin = Gradient(value.gradientMin),
                GradientMax = Gradient(value.gradientMax)
            };
        }

        private static VfxGradientSnapshotView Gradient(Gradient value)
        {
            if (value == null) return null;
            var result = new VfxGradientSnapshotView { Mode = (int)value.mode };
            GradientColorKey[] colors = value.colorKeys;
            for (int i = 0; i < colors.Length; i++)
                result.ColorKeys.Add(new VfxGradientColorKeySnapshotView { Time = colors[i].time, Color = ColorSnapshot(colors[i].color) });
            GradientAlphaKey[] alphas = value.alphaKeys;
            for (int i = 0; i < alphas.Length; i++)
                result.AlphaKeys.Add(new VfxGradientAlphaKeySnapshotView { Time = alphas[i].time, Alpha = alphas[i].alpha });
            return result;
        }
    }
}
