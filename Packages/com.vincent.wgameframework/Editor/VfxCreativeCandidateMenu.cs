using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Manual smoke entries for the Editor-only VFX candidate workflow.</summary>
    internal static class VfxCreativeCandidateMenu
    {
        private static readonly string[] StageIds = { "charge", "flight", "impact" };
        private static readonly string[] StageNames = { "Charge", "Flight", "Impact" };
        private static readonly string[] DirectionIds = { "reuse", "signature", "budget", "strong", "theme", "explore" };
        private static readonly string[] DirectionNames = { "Conservative Reuse", "High Recognition", "Low Budget", "Strong Impact", "Theme Variant", "Primitive Exploration" };

        [MenuItem("MxFramework/VFX/Candidates/Clean Candidate Sandbox")]
        private static void CleanCandidateSandbox()
        {
            if (!AssetDatabase.IsValidFolder(VfxCandidateProtocol.ResolvedSandboxRoot))
            {
                Debug.Log("VFX candidate sandbox is already empty.");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "Clean VFX Candidate Sandbox",
                    "Delete every Draft candidate under " + VfxCandidateProtocol.ResolvedSandboxRoot + "? Promoted assets are not affected.",
                    "Delete Drafts",
                    "Cancel"))
                return;
            if (!AssetDatabase.DeleteAsset(VfxCandidateProtocol.ResolvedSandboxRoot))
            {
                Debug.LogError("Unity failed to clean the VFX candidate sandbox.");
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("VFX candidate sandbox cleaned. No Catalog or published VFX asset was modified.");
        }

        internal static List<VfxCandidatePlan> CreateFireballRoundZeroPlans(string sessionPrefix)
        {
            var plans = new List<VfxCandidatePlan>(StageIds.Length * DirectionIds.Length);
            for (int stageIndex = 0; stageIndex < StageIds.Length; stageIndex++)
            {
                for (int directionIndex = 0; directionIndex < DirectionIds.Length; directionIndex++)
                {
                    string sessionId = sessionPrefix + "_" + StageIds[stageIndex] + "_" + DirectionIds[directionIndex];
                    plans.Add(CreateRoundZeroPlan(sessionId, stageIndex, directionIndex));
                }
            }
            return plans;
        }

        internal static VfxCandidatePlan CreateFireballPlan(string sessionId)
        {
            return CreateRoundZeroPlan(sessionId, 2, 0);
        }

        private static VfxCandidatePlan CreateRoundZeroPlan(string sessionId, int stageIndex, int directionIndex)
        {
            VfxCandidatePlan plan = CreateBaseParticlePlan(sessionId);
            string stageId = StageIds[stageIndex];
            string directionId = DirectionIds[directionIndex];
            plan.PlanId = "round0.fireball." + stageId + "." + directionId;
            plan.DisplayName = "Fireball " + StageNames[stageIndex] + " - " + DirectionNames[directionIndex];
            plan.OutputAssetName = "Fireball" + StageNames[stageIndex] + DirectionToAssetName(directionIndex) + "Candidate";
            plan.FixedSeed = 0x51F10001u + (uint)(stageIndex * 16 + directionIndex);
            ApplyStage(plan.ParticleSystem, stageIndex);
            ApplyDirection(plan, directionIndex);
            if (stageIndex == 1 && directionIndex == 5)
                ConvertFlightExplorationToLine(plan);
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "round", Value = "Round0" });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "stage", Value = stageId });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "direction", Value = directionId });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "authority", Value = "Editor-only generated Draft; human artistic approval required" });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "licenseId", Value = "project-owned" });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "sourceUri", Value = "mx://vfx/candidate-plan/" + plan.PlanId });
            plan.Provenance.Add(new VfxCandidateProvenanceEntry { Key = "seed", Value = "0x" + plan.FixedSeed.ToString("X8") });
            return plan;
        }

        private static VfxCandidatePlan CreateBaseParticlePlan(string sessionId)
        {
            return new VfxCandidatePlan
            {
                SessionId = sessionId,
                GeneratorId = "mx.vfx.candidate.sample-fireball-round0",
                GeneratorVersion = "1.0.0",
                CandidateKind = VfxCandidateKind.ParticleSystemPrimitive,
                Textures =
                {
                    new VfxProceduralTextureSpec
                    {
                        TextureId = "fireball_texture",
                        AssetName = "FireballTexture",
                        Kind = VfxProceduralTextureKind.SoftCircle,
                        Width = 64,
                        Height = 64,
                        Color = new VfxColorSpec { R = 1f, G = 0.32f, B = 0.04f, A = 1f }
                    }
                },
                Materials =
                {
                    new VfxMaterialSpec
                    {
                        MaterialId = "fireball_material",
                        AssetName = "FireballMaterial",
                        ShaderName = "Universal Render Pipeline/Particles/Unlit",
                        TextureId = "fireball_texture",
                        Color = new VfxColorSpec { R = 1f, G = 0.42f, B = 0.06f, A = 1f }
                    }
                },
                ParticleSystem = new ParticleSystemPrimitiveSpec
                {
                    MaterialId = "fireball_material",
                    StartColor = new VfxColorSpec { R = 1f, G = 0.72f, B = 0.12f, A = 1f },
                    EndColor = new VfxColorSpec { R = 1f, G = 0.05f, B = 0.01f, A = 0f },
                    NoiseFrequency = 0.75f
                }
            };
        }

        private static void ApplyStage(ParticleSystemPrimitiveSpec particle, int stageIndex)
        {
            switch (stageIndex)
            {
                case 0:
                    particle.DurationSeconds = 1.2f;
                    particle.Looping = true;
                    particle.StartLifetimeSeconds = 0.7f;
                    particle.StartSpeed = 0.35f;
                    particle.StartSize = 0.18f;
                    particle.MaxParticles = 64;
                    particle.EmissionRatePerSecond = 24f;
                    particle.Shape = "Sphere";
                    particle.ShapeRadius = 0.34f;
                    particle.NoiseStrength = 0.22f;
                    break;
                case 1:
                    particle.DurationSeconds = 0.8f;
                    particle.Looping = true;
                    particle.StartLifetimeSeconds = 0.34f;
                    particle.StartSpeed = 2.4f;
                    particle.StartSize = 0.15f;
                    particle.MaxParticles = 48;
                    particle.EmissionRatePerSecond = 28f;
                    particle.Shape = "Cone";
                    particle.ShapeRadius = 0.07f;
                    particle.ShapeAngleDegrees = 11f;
                    particle.NoiseStrength = 0.12f;
                    break;
                default:
                    particle.DurationSeconds = 0.8f;
                    particle.Looping = false;
                    particle.StartLifetimeSeconds = 0.65f;
                    particle.StartSpeed = 3.2f;
                    particle.StartSize = 0.32f;
                    particle.MaxParticles = 96;
                    particle.EmissionRatePerSecond = 0f;
                    particle.Shape = "Sphere";
                    particle.ShapeRadius = 0.12f;
                    particle.NoiseStrength = 0.2f;
                    particle.Bursts.Add(new VfxBurstSpec { Time = 0f, Count = 42, Cycles = 1, Interval = 0.01f });
                    break;
            }
        }

        private static void ApplyDirection(VfxCandidatePlan plan, int directionIndex)
        {
            ParticleSystemPrimitiveSpec particle = plan.ParticleSystem;
            switch (directionIndex)
            {
                case 1:
                    particle.StartSize *= 1.25f;
                    particle.ShapeRadius *= 1.15f;
                    particle.StartColor = new VfxColorSpec { R = 1f, G = 0.88f, B = 0.2f, A = 1f };
                    plan.Textures[0].Kind = VfxProceduralTextureKind.Ring;
                    break;
                case 2:
                    particle.MaxParticles = Mathf.Max(8, particle.MaxParticles / 2);
                    particle.EmissionRatePerSecond *= 0.5f;
                    for (int i = 0; i < particle.Bursts.Count; i++)
                        particle.Bursts[i].Count = (short)Mathf.Max(4, particle.Bursts[i].Count / 2);
                    plan.Textures[0].Width = 32;
                    plan.Textures[0].Height = 32;
                    break;
                case 3:
                    particle.StartSpeed *= 1.45f;
                    particle.StartSize *= 1.4f;
                    particle.MaxParticles = Mathf.RoundToInt(particle.MaxParticles * 1.5f);
                    for (int i = 0; i < particle.Bursts.Count; i++)
                        particle.Bursts[i].Count = (short)Mathf.Min(short.MaxValue, Mathf.RoundToInt(particle.Bursts[i].Count * 1.6f));
                    break;
                case 4:
                    particle.StartColor = new VfxColorSpec { R = 0.5f, G = 0.22f, B = 1f, A = 1f };
                    particle.EndColor = new VfxColorSpec { R = 0.05f, G = 0.75f, B = 1f, A = 0f };
                    plan.Textures[0].Color = new VfxColorSpec { R = 0.42f, G = 0.1f, B = 1f, A = 1f };
                    plan.Materials[0].Color = new VfxColorSpec { R = 0.55f, G = 0.2f, B = 1f, A = 1f };
                    break;
                case 5:
                    particle.NoiseStrength = Mathf.Max(0.55f, particle.NoiseStrength * 2f);
                    particle.Shape = "Circle";
                    plan.Textures[0].Kind = VfxProceduralTextureKind.Noise;
                    plan.Textures[0].Color = new VfxColorSpec { R = 0.1f, G = 0.9f, B = 0.75f, A = 1f };
                    plan.Materials[0].Color = new VfxColorSpec { R = 0.08f, G = 1f, B = 0.78f, A = 1f };
                    break;
            }
        }

        private static void ConvertFlightExplorationToLine(VfxCandidatePlan plan)
        {
            plan.CandidateKind = VfxCandidateKind.LineRendererPrimitive;
            plan.LineRenderer = new LineRendererPrimitiveSpec
            {
                MaterialId = "fireball_material",
                StartWidth = 0.16f,
                EndWidth = 0.015f,
                StartColor = new VfxColorSpec { R = 0.08f, G = 1f, B = 0.78f, A = 1f },
                EndColor = new VfxColorSpec { R = 0.05f, G = 0.25f, B = 1f, A = 0f },
                CornerVertices = 4,
                CapVertices = 4,
                Positions =
                {
                    new VfxVector3Spec { X = -0.9f, Y = 0f, Z = 0f },
                    new VfxVector3Spec { X = -0.35f, Y = 0.08f, Z = 0f },
                    new VfxVector3Spec { X = 0.2f, Y = -0.04f, Z = 0f },
                    new VfxVector3Spec { X = 0.9f, Y = 0f, Z = 0f }
                }
            };
            plan.ParticleSystem = null;
        }

        private static string FindAssetPath(VfxCandidateReceipt receipt, string kind)
        {
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                if (string.Equals(receipt.Assets[i].Kind, kind, StringComparison.Ordinal))
                    return receipt.Assets[i].AssetPath;
            }
            return string.Empty;
        }

        private static float GetSampleTime(VfxCandidatePlan plan)
        {
            if (plan.PlanId.Contains(".charge."))
                return 0.7f;
            if (plan.PlanId.Contains(".flight."))
                return 0.3f;
            return 0.35f;
        }

        private static string DirectionToAssetName(int directionIndex)
        {
            switch (directionIndex)
            {
                case 0: return "Reuse";
                case 1: return "Signature";
                case 2: return "Budget";
                case 3: return "Strong";
                case 4: return "Theme";
                default: return "Explore";
            }
        }
    }
}
