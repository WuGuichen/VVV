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
    internal static class VfxParticleSystemProfileSuggestion
    {
        public static VfxComponentProfileView Create(VfxSourceComponentView source, VfxParticleSystemSnapshotView snapshot, ParticleSystem system)
        {
            int duration = Math.Max(1, Mathf.CeilToInt(snapshot.DurationSeconds * 60f / Mathf.Max(0.05f, snapshot.SimulationSpeed)));
            int residence = Math.Max(0, Mathf.CeilToInt(snapshot.StartLifetime.ConstantMax * 60f / Mathf.Max(0.05f, snapshot.SimulationSpeed)));
            int entry = Mathf.Clamp(Mathf.RoundToInt(duration * 0.15f), 0, 12);
            int exit = Mathf.Clamp(Mathf.RoundToInt(Math.Max(1, residence) * 0.35f), 0, 18);
            int body = Math.Max(1, duration - entry);
            string token = VfxCompositionDraftService.FileToken(source.ComponentId);
            var result = new VfxComponentProfileView
            {
                ProfileId = "vfx.component.suggested." + token,
                SourceComponentId = source.ComponentId,
                SourceSnapshotHash = snapshot.SnapshotHash,
                Status = nameof(VfxProfileReviewState.AutoSuggested),
                Timing = new VfxProfileTimingView
                {
                    EntryFrames = entry,
                    BodyFrames = body,
                    ExitFrames = exit,
                    CleanupFrames = residence,
                    MinimumBodyFrames = 1,
                    CycleFrames = snapshot.Loop ? Math.Max(1, duration) : 0,
                    RetimeMechanism = snapshot.Loop || system.emission.rateOverTime.constantMax > 0f ? "EmitterWindow" : "FixedEvent",
                    TailPolicy = snapshot.Loop ? "EndLocked" : "Unconfirmed"
                }
            };
            AddStandardBindings(result, system);
            AddBurstEvents(result, system, duration, entry, body);
            if (system.trails.enabled)
            {
                result.Timing.TrailContract = new VfxTrailAuthoringContractView
                {
                    RetimePolicy = "PreserveNormalizedLifetime",
                    PreserveWidth = true,
                    PreserveColor = true,
                    ExpectedDieWithParticles = system.trails.dieWithParticles,
                    SourceTrailHash = snapshot.Trails?.ContentHash ?? string.Empty
                };
            }
            if (system.subEmitters.enabled) result.ReadOnlyModules.Add("SubEmitters");
            if (system.collision.enabled) result.ReadOnlyModules.Add("Collision");
            if (system.limitVelocityOverLifetime.enabled) result.ReadOnlyModules.Add("LimitVelocityOverLifetime");
            result.ContentHash = VfxCanonicalJson.Hash(result, "contentHash");
            return result;
        }

        private static void AddBurstEvents(VfxComponentProfileView result, ParticleSystem system, int durationFrames, int entryFrames, int bodyFrames)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            int count = emission.burstCount;
            if (count <= 0) return;
            var bursts = new ParticleSystem.Burst[count];
            emission.GetBursts(bursts);
            int bodyEnd = entryFrames + bodyFrames;
            for (int i = 0; i < bursts.Length; i++)
            {
                int frame = Mathf.Clamp(Mathf.RoundToInt(bursts[i].time * 60f), 0, durationFrames);
                string anchor = frame <= entryFrames
                    ? nameof(VfxTimeEventAnchor.StartLocked)
                    : frame >= bodyEnd
                        ? nameof(VfxTimeEventAnchor.EndLocked)
                        : nameof(VfxTimeEventAnchor.BodyNormalized);
                result.Events.Add(new VfxTimeEventView
                {
                    EventId = "burst." + i.ToString(CultureInfo.InvariantCulture),
                    Frame = frame,
                    Anchor = anchor
                });
            }
        }

        private static void AddStandardBindings(VfxComponentProfileView result, ParticleSystem system)
        {
            result.Bindings.Add(Binding("appearance.opacity", "ParticleStartColor", "main.startColor.alpha", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static, primaryOpacity: true));
            result.Bindings.Add(Binding("appearance.opacityEnvelope", "ParticleColorOverLifetime", "colorOverLifetime.color.alpha", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("appearance.colorOverLifetime.exact", "ParticleColorOverLifetimeExact", "colorOverLifetime.color", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("appearance.palette.primary", "ParticleStartColor", "main.startColor.rgb", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("timing.startDelay.exact", "ParticleMainExact", "main.startDelay", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.LayerFrame));
            result.Bindings.Add(Binding("timing.startLifetime.exact", "ParticleMainExact", "main.startLifetime", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("emission.density", "ParticleEmission", "emission.rateOverTime+bursts", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.EmitterNormalized, min: 0.25f, max: 2f));
            result.Bindings.Add(Binding("emission.rate.time", "ParticleEmission", "emission.rateOverTime", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.EmitterNormalized));
            result.Bindings.Add(Binding("emission.rate.distance", "ParticleEmission", "emission.rateOverDistance", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.EmitterNormalized));
            result.Bindings.Add(Binding("emission.burst.scale", "ParticleEmission", "emission.bursts.count", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.LayerFrame));
            result.Bindings.Add(Binding("emission.window.startFrame", "ParticleEmissionWindow", "main.startDelay", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.LayerFrame));
            result.Bindings.Add(Binding("emission.window.endFrame", "ParticleEmissionWindow", "main.duration", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.LayerFrame));
            result.Bindings.Add(Binding("emission.capacity.maxParticles", "ParticleMain", "main.maxParticles", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("motion.speed", "ParticleMain", "main.startSpeed", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static, min: 0.25f, max: 3f));
            result.Bindings.Add(Binding("motion.startSpeed.exact", "ParticleMainExact", "main.startSpeed", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.startRotation.exact", "ParticleMainExact", "main.startRotation", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.startRotation.x.exact", "ParticleMainExact", "main.startRotationX", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.startRotation.y.exact", "ParticleMainExact", "main.startRotationY", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.startRotation.z.exact", "ParticleMainExact", "main.startRotationZ", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.travelRange", "ParticleMain", "main.startSpeed+startLifetime", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("motion.gravity.world", "ParticleMain", "main.gravityModifier", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("motion.gravity.local", "ParticleForce", "forceOverLifetime.xyz.local", VfxSemanticOperation.Add, VfxSemanticTimeDomain.EmitterNormalized));
            result.Bindings.Add(Binding("motion.force.vector", "ParticleForce", "forceOverLifetime.xyz.world", VfxSemanticOperation.Add, VfxSemanticTimeDomain.EmitterNormalized));
            result.Bindings.Add(Binding("motion.force.x.exact", "ParticleForceExact", "forceOverLifetime.x", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.force.y.exact", "ParticleForceExact", "forceOverLifetime.y", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.force.z.exact", "ParticleForceExact", "forceOverLifetime.z", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.drag", "ParticleLimitVelocity", "limitVelocityOverLifetime.drag", VfxSemanticOperation.Add, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.rotation.speed", "ParticleRotationOverLifetime", "rotationOverLifetime.xyz", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.rotation.x.exact", "ParticleRotationExact", "rotationOverLifetime.x", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.rotation.y.exact", "ParticleRotationExact", "rotationOverLifetime.y", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.rotation.z.exact", "ParticleRotationExact", "rotationOverLifetime.z", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("shape.particle.size", "ParticleMain", "main.startSize", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.ParticleNormalized, min: 0.25f, max: 4f));
            result.Bindings.Add(Binding("shape.startSize.exact", "ParticleMainExact", "main.startSize", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("shape.startSize.x.exact", "ParticleMainExact", "main.startSizeX", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("shape.startSize.y.exact", "ParticleMainExact", "main.startSizeY", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("shape.startSize.z.exact", "ParticleMainExact", "main.startSizeZ", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("shape.composition.scale", "Transform", "transform.localScale", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static, min: 0.25f, max: 4f));
            result.Bindings.Add(Binding("shape.emitter.scale", "ParticleShape", "shape.scale", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static, min: 0.25f, max: 4f));
            result.Bindings.Add(Binding("shape.radius", "ParticleShape", "shape.radius", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("shape.angle", "ParticleShape", "shape.angle", VfxSemanticOperation.Add, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("shape.arc", "ParticleShape", "shape.arc", VfxSemanticOperation.Add, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("shape.direction.spread", "ParticleShape", "shape.randomDirectionAmount", VfxSemanticOperation.Add, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.mode", "ParticleRenderer", "renderer.renderMode", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.alignment", "ParticleRenderer", "renderer.alignment", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.sortMode", "ParticleRenderer", "renderer.sortMode", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.pivot", "ParticleRenderer", "renderer.pivot", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.minParticleSize", "ParticleRenderer", "renderer.minParticleSize", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.maxParticleSize", "ParticleRenderer", "renderer.maxParticleSize", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.material", "ParticleRendererAsset", "renderer.sharedMaterial", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.renderer.mesh", "ParticleRendererAsset", "renderer.mesh", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.Static));
            result.Bindings.Add(Binding("render.sortingOrder.offset", "ParticleRenderer", "renderer.sortingOrder", VfxSemanticOperation.Add, VfxSemanticTimeDomain.Static));
            if (system.noise.enabled)
            {
                result.Bindings.Add(Binding("motion.noise.strength", "ParticleNoise", "noise.strength", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.ParticleNormalized));
                result.Bindings.Add(Binding("motion.noise.envelope", "ParticleNoise", "noise.strength", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
                result.Bindings.Add(Binding("motion.noise.frequency", "ParticleNoise", "noise.frequency", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.Static));
                result.Bindings.Add(Binding("motion.noise.scroll.exact", "ParticleNoiseExact", "noise.scrollSpeed", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            }
            result.Bindings.Add(Binding("shape.particle.sizeEnvelope", "ParticleSizeOverLifetime", "sizeOverLifetime.size", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            result.Bindings.Add(Binding("motion.velocity.envelope", "ParticleVelocityOverLifetime", "velocityOverLifetime.speedModifier", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            if (system.textureSheetAnimation.enabled)
            {
                result.Bindings.Add(Binding("render.flipbook.speed", "ParticleTextureSheet", "textureSheet.cycleCount", VfxSemanticOperation.Multiply, VfxSemanticTimeDomain.ParticleNormalized));
                result.Bindings.Add(Binding("render.flipbook.frame.exact", "ParticleTextureSheetExact", "textureSheet.frameOverTime", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
                result.Bindings.Add(Binding("render.flipbook.startFrame.exact", "ParticleTextureSheetExact", "textureSheet.startFrame", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
                result.Bindings.Add(Binding("render.flipbook.cycles", "ParticleTextureSheet", "textureSheet.cycleCount", VfxSemanticOperation.Replace, VfxSemanticTimeDomain.ParticleNormalized));
            }
        }

        private static VfxSemanticBindingView Binding(string propertyId, string adapter, string target, VfxSemanticOperation operation, VfxSemanticTimeDomain domain, bool primaryOpacity = false, float? min = null, float? max = null)
        {
            return new VfxSemanticBindingView
            {
                PropertyId = propertyId,
                Adapter = adapter,
                Target = target,
                Operation = operation.ToString(),
                TimeDomain = domain.ToString(),
                PrimaryOpacity = primaryOpacity,
                RecommendedMinimum = min,
                RecommendedMaximum = max
            };
        }
    }
}

