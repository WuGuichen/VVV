using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Diagnostics;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn.DebugUI.Unity
{
    public sealed class CharacterRuntimeAnimationDebugSource : IFrameworkDebugSource
    {
        private readonly CharacterRuntimeResourceBootstrap _bootstrap;
        private readonly Func<CharacterRuntimeLocomotionBlendController> _locomotionResolver;

        public CharacterRuntimeAnimationDebugSource(
            CharacterRuntimeResourceBootstrap bootstrap,
            string name = "CharacterAnimation",
            Func<CharacterRuntimeLocomotionBlendController> locomotionResolver = null)
        {
            _bootstrap = bootstrap;
            _locomotionResolver = locomotionResolver;
            Name = string.IsNullOrWhiteSpace(name) ? "CharacterAnimation" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _bootstrap != null || _locomotionResolver != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            var sections = new List<FrameworkDebugSection>
            {
                new FrameworkDebugSection("Status", CreateStatusSection())
            };

            CharacterRuntimeLocomotionBlendController locomotion = ResolveLocomotion();
            sections.Add(new FrameworkDebugSection("Warmup", CreateWarmupSection(_bootstrap != null ? _bootstrap.AnimationWarmupResult : null)));
            sections.Add(new FrameworkDebugSection("Playback", CreatePlaybackSection(locomotion)));
            sections.Add(new FrameworkDebugSection("Locomotion Blend Probe", CreateLocomotionBlendProbeSection(locomotion)));

            MxAnimationDiagnosticSnapshot animation = locomotion != null ? locomotion.CreateAnimationSnapshot() : null;
            sections.Add(new FrameworkDebugSection("Backend", CreateBackendSection(animation)));
            sections.Add(new FrameworkDebugSection("Layers", CreateLayersSection(animation)));
            sections.Add(new FrameworkDebugSection("Blend Weights", CreateBlendWeightsSection(animation)));
            sections.Add(new FrameworkDebugSection("Recent Requests", CreateRecentRequestsSection(animation)));
            sections.Add(new FrameworkDebugSection("Resource Errors", CreateResourceErrorsSection(animation)));

            return new FrameworkDebugSnapshot(Name, Mode, sections);
        }

        private CharacterRuntimeLocomotionBlendController ResolveLocomotion()
        {
            if (_locomotionResolver != null)
                return _locomotionResolver();

            if (_bootstrap == null || _bootstrap.CharacterInstance == null)
                return null;

            return _bootstrap.CharacterInstance.GetComponentInChildren<CharacterRuntimeLocomotionBlendController>(includeInactive: true);
        }

        private string CreateStatusSection()
        {
            var builder = new StringBuilder();
            builder.Append("available: ").Append(FormatBool(IsAvailable)).Append('\n');
            builder.Append("bootstrap: ").Append(_bootstrap != null ? "present" : "missing").Append('\n');
            if (_bootstrap == null)
                return builder.ToString();

            builder.Append("authority: ").Append(DescribeAnimationAuthority(_bootstrap)).Append('\n');
            builder.Append("animationArtifacts: ").Append(_bootstrap.HasAnimationArtifacts ? "present" : "missing").Append('\n');
            builder.Append("animationSetDefinitionJsonPath: ").Append(EmptyAsDash(_bootstrap.AnimationSetDefinitionJsonPath)).Append('\n');
            builder.Append("animationClipRegistryPath: ").Append(EmptyAsDash(_bootstrap.AnimationClipRegistryPath)).Append('\n');
            builder.Append("animationResourcePlanPath: ").Append(EmptyAsDash(_bootstrap.AnimationResourcePlanPath)).Append('\n');
            builder.Append("animationSetDefinitionContentHash: ").Append(EmptyAsDash(_bootstrap.AnimationSetDefinitionContentHash)).Append('\n');
            builder.Append("animationClipRegistryContentHash: ").Append(EmptyAsDash(_bootstrap.AnimationClipRegistryContentHash)).Append('\n');
            builder.Append("animationResourcePlanContentHash: ").Append(EmptyAsDash(_bootstrap.AnimationResourcePlanContentHash)).Append('\n');
            builder.Append("importReportPath: ").Append(EmptyAsDash(_bootstrap.ImportReportPath)).Append('\n');
            builder.Append("sourcePackageHash: ").Append(EmptyAsDash(_bootstrap.SourcePackageHash)).Append('\n');
            builder.Append("generatedConfigHash: ").Append(EmptyAsDash(_bootstrap.GeneratedConfigHash)).Append('\n');
            builder.Append("geometryBindingHash: ").Append(EmptyAsDash(_bootstrap.GeometryBindingHash)).Append('\n');
            builder.Append("resourceMappingHash: ").Append(EmptyAsDash(_bootstrap.ResourceMappingHash)).Append('\n');
            builder.Append("packageId: ").Append(EmptyAsDash(_bootstrap.PackageId)).Append('\n');
            builder.Append("characterResourceId: ").Append(EmptyAsDash(_bootstrap.CharacterResourceId)).Append('\n');
            builder.Append("characterVariant: ").Append(EmptyAsDash(_bootstrap.CharacterResourceVariant)).Append('\n');
            builder.Append("animationSetId: ").Append(EmptyAsDash(_bootstrap.AnimationSetId)).Append('\n');
            builder.Append("resourceManager: ").Append(_bootstrap.ResourceManager != null ? "ready" : "missing").Append('\n');
            builder.Append("characterInstance: ").Append(_bootstrap.CharacterInstance != null ? _bootstrap.CharacterInstance.name : "not loaded");
            return builder.ToString();
        }

        private static string DescribeAnimationAuthority(CharacterRuntimeResourceBootstrap bootstrap)
        {
            if (bootstrap == null || !bootstrap.HasAnimationArtifacts)
                return "non-authoritative";
            return "authoritative";
        }

        private static string CreateWarmupSection(MxAnimationWarmupResult warmup)
        {
            if (warmup == null)
                return "result: none";

            var builder = new StringBuilder();
            builder.Append("success: ").Append(FormatBool(warmup.Success)).Append('\n');
            builder.Append("groupId: ").Append(EmptyAsDash(warmup.GroupId)).Append('\n');
            builder.Append("requiredKeys: ").Append(warmup.RequiredKeys.Count).Append('\n');
            builder.Append("labels: ").Append(JoinStrings(warmup.Labels)).Append('\n');
            builder.Append("preloadRequested: ").Append(warmup.PreloadResult != null ? warmup.PreloadResult.RequestedCount.ToString(CultureInfo.InvariantCulture) : "0").Append('\n');
            builder.Append("preloadLoaded: ").Append(warmup.PreloadResult != null ? warmup.PreloadResult.LoadedCount.ToString(CultureInfo.InvariantCulture) : "0").Append('\n');
            builder.Append("preloadFailed: ").Append(warmup.PreloadResult != null ? warmup.PreloadResult.FailedCount.ToString(CultureInfo.InvariantCulture) : "0").Append('\n');
            builder.Append("issues: ").Append(warmup.IssueCount);

            for (int i = 0; i < warmup.Issues.Count; i++)
            {
                MxAnimationWarmupIssue issue = warmup.Issues[i];
                builder.Append('\n')
                    .Append("- ").Append(issue.Code)
                    .Append(" key=").Append(FormatKey(issue.Key))
                    .Append(" field=").Append(EmptyAsDash(issue.Field))
                    .Append(" expected=").Append(EmptyAsDash(issue.Expected))
                    .Append(" actual=").Append(EmptyAsDash(issue.Actual))
                    .Append(" message=").Append(EmptyAsDash(issue.Message));
            }

            return builder.ToString();
        }

        private static string CreatePlaybackSection(CharacterRuntimeLocomotionBlendController locomotion)
        {
            if (locomotion == null)
                return "locomotionController: missing";

            MxAnimationBackendResult result = locomotion.LastAnimationResult;
            var builder = new StringBuilder();
            builder.Append("locomotionController: present\n");
            builder.Append("blend: ").Append(FormatFloat(locomotion.Blend.x)).Append(", ").Append(FormatFloat(locomotion.Blend.y)).Append('\n');
            builder.Append("speed01: ").Append(FormatFloat(locomotion.Speed01)).Append('\n');
            builder.Append("activeBlend2DId: ").Append(EmptyAsDash(locomotion.ActiveBlend2DId)).Append('\n');
            builder.Append("quantized: ").Append(locomotion.LastQuantizedBlendX).Append(", ").Append(locomotion.LastQuantizedBlendY).Append('\n');
            builder.Append("hasBackend: ").Append(FormatBool(locomotion.HasAnimationBackend)).Append('\n');
            builder.Append("usingFallback: ").Append(FormatBool(locomotion.UsingFallback)).Append('\n');
            builder.Append("hasAnimationClips: ").Append(FormatBool(locomotion.HasAnimationClips)).Append('\n');
            builder.Append("lastResult: ").Append(result.Code).Append(" success=").Append(FormatBool(result.Success)).Append('\n');
            builder.Append("lastResultClip: ").Append(FormatKey(result.ClipKey)).Append('\n');
            builder.Append("lastResultMessage: ").Append(EmptyAsDash(result.Message));
            return builder.ToString();
        }

        private static string CreateLocomotionBlendProbeSection(CharacterRuntimeLocomotionBlendController locomotion)
        {
            if (locomotion == null)
                return "locomotionController: missing";

            MxAnimationLocomotionBlendProbeSnapshot probe = locomotion.CreateLocomotionBlendProbeSnapshot();
            if (probe == null)
                return "probe: unavailable";

            var builder = new StringBuilder();
            builder.Append("blendId: ").Append(EmptyAsDash(probe.BlendId)).Append('\n');
            builder.Append("domain: x=[").Append(probe.Domain.MinX).Append(',').Append(probe.Domain.MaxX)
                .Append("] y=[").Append(probe.Domain.MinY).Append(',').Append(probe.Domain.MaxY).Append("]\n");
            builder.Append("sample: ").Append(probe.SampleX).Append(", ").Append(probe.SampleY).Append('\n');
            builder.Append("weightsFromBackend: ").Append(FormatBool(probe.WeightsFromBackend)).Append('\n');
            builder.Append("dominant: ").Append(probe.HasDominantClip ? FormatKey(probe.DominantClipKey) : "-")
                .Append(" weight=").Append(FormatFloat(probe.DominantWeight)).Append('\n');

            MxAnimationBlendReachabilityReport reachability = probe.ReachabilityReport;
            if (reachability == null)
            {
                builder.Append("reachability: unavailable");
            }
            else
            {
                builder.Append("reachablePoints: ").Append(reachability.ReachablePoints.Count).Append('\n');
                builder.Append("unreachablePoints: ").Append(reachability.UnreachablePoints.Count);
                for (int i = 0; i < reachability.Issues.Count; i++)
                {
                    MxAnimationBlendReachabilityIssue issue = reachability.Issues[i];
                    builder.Append('\n')
                        .Append("- ").Append(issue.Code)
                        .Append(" key=").Append(FormatKey(issue.ClipKey))
                        .Append(" point=(").Append(issue.X).Append(',').Append(issue.Y).Append(')')
                        .Append(" message=").Append(EmptyAsDash(issue.Message));
                }
            }

            builder.Append('\n').Append("weights:");
            if (probe.Weights.Count == 0)
            {
                builder.Append(" none");
            }
            else
            {
                for (int i = 0; i < probe.Weights.Count; i++)
                {
                    MxAnimationBlend2DWeight weight = probe.Weights[i];
                    builder.Append('\n')
                        .Append("- ").Append(FormatKey(weight.ClipKey))
                        .Append(" point=(").Append(weight.X).Append(',').Append(weight.Y).Append(')')
                        .Append(" weight=").Append(FormatFloat(weight.Weight));
                }
            }

            return builder.ToString();
        }

        private static string CreateBackendSection(MxAnimationDiagnosticSnapshot animation)
        {
            if (animation == null)
                return "backend: missing";

            var builder = new StringBuilder();
            builder.Append("backend: ").Append(EmptyAsDash(animation.BackendName)).Append('\n');
            builder.Append("actorId: ").Append(EmptyAsDash(animation.ActorId)).Append('\n');
            builder.Append("setId: ").Append(EmptyAsDash(animation.SetId)).Append('\n');
            builder.Append("graphIsValid: ").Append(FormatBool(animation.GraphIsValid)).Append('\n');
            builder.Append("isReleased: ").Append(FormatBool(animation.IsReleased)).Append('\n');
            builder.Append("actorCount: ").Append(animation.ActorCount).Append('\n');
            builder.Append("cacheHits: ").Append(animation.Cache.CacheHitCount).Append('\n');
            builder.Append("cacheMisses: ").Append(animation.Cache.CacheMissCount).Append('\n');
            builder.Append("residentClips: ").Append(animation.Cache.ResidentClipCount).Append('\n');
            builder.Append("activePlayables: ").Append(animation.Cache.ActivePlayableCount).Append('\n');
            builder.Append("resourceRefs: ").Append(animation.Cache.ResourceRefCount);
            return builder.ToString();
        }

        private static string CreateLayersSection(MxAnimationDiagnosticSnapshot animation)
        {
            if (animation == null || animation.LayerStates.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < animation.LayerStates.Count; i++)
            {
                MxAnimationLayerDiagnostic layer = animation.LayerStates[i];
                if (i > 0)
                    builder.Append('\n');

                builder.Append(layer.LayerId)
                    .Append(" status=").Append(layer.Status)
                    .Append(" weight=").Append(FormatFloat(layer.CurrentWeight))
                    .Append(" layerWeight=").Append(FormatFloat(layer.LayerWeight))
                    .Append(" target=").Append(FormatFloat(layer.TargetLayerWeight))
                    .Append(" active=").Append(layer.ActivePlayableCount)
                    .Append('\n');
                builder.Append("  current=").Append(FormatKey(layer.CurrentClipKey))
                    .Append(" next=").Append(FormatKey(layer.NextClipKey))
                    .Append(" fallback=").Append(FormatBool(layer.CurrentClipIsFallback))
                    .Append('\n');
                builder.Append("  blendKind=").Append(layer.BlendKind)
                    .Append(" blend1D=").Append(EmptyAsDash(layer.Blend1DId))
                    .Append(" blend2D=").Append(EmptyAsDash(layer.Blend2DId))
                    .Append('\n');
                builder.Append("  paramX=").Append(FormatParameter(layer.Blend2DParameterX))
                    .Append(" paramY=").Append(FormatParameter(layer.Blend2DParameterY));

                if (!layer.LastError.IsNone)
                    builder.Append('\n').Append("  error=").Append(FormatError(layer.LastError));
            }

            return builder.ToString();
        }

        private static string CreateBlendWeightsSection(MxAnimationDiagnosticSnapshot animation)
        {
            if (animation == null)
                return "none";

            var builder = new StringBuilder();
            bool any = false;
            for (int i = 0; i < animation.LayerStates.Count; i++)
            {
                MxAnimationLayerDiagnostic layer = animation.LayerStates[i];
                for (int j = 0; j < layer.Blend2DWeights.Count; j++)
                {
                    MxAnimationBlend2DWeight weight = layer.Blend2DWeights[j];
                    if (any)
                        builder.Append('\n');

                    any = true;
                    builder.Append(layer.LayerId)
                        .Append(" key=").Append(FormatKey(weight.ClipKey))
                        .Append(" x=").Append(weight.X)
                        .Append(" y=").Append(weight.Y)
                        .Append(" weight=").Append(FormatFloat(weight.Weight))
                        .Append(" speed=").Append(FormatFloat(weight.PlaybackSpeed))
                        .Append(" loop=").Append(FormatBool(weight.Loop));
                }
            }

            return any ? builder.ToString() : "none";
        }

        private static string CreateRecentRequestsSection(MxAnimationDiagnosticSnapshot animation)
        {
            if (animation == null || animation.RecentRequests.Count == 0)
                return "none";

            var builder = new StringBuilder();
            int start = Math.Max(0, animation.RecentRequests.Count - 12);
            for (int i = start; i < animation.RecentRequests.Count; i++)
            {
                MxAnimationRequestDiagnostic request = animation.RecentRequests[i];
                if (i > start)
                    builder.Append('\n');

                builder.Append(request.Kind)
                    .Append(" layer=").Append(request.LayerId)
                    .Append(" result=").Append(request.ResultCode)
                    .Append(" requested=").Append(FormatKey(request.RequestedClipKey))
                    .Append(" resolved=").Append(FormatKey(request.ResolvedClipKey))
                    .Append(" fallback=").Append(FormatBool(request.UsedFallback))
                    .Append(" corr=").Append(EmptyAsDash(request.CorrelationId))
                    .Append(" msg=").Append(EmptyAsDash(request.Message));
            }

            return builder.ToString();
        }

        private static string CreateResourceErrorsSection(MxAnimationDiagnosticSnapshot animation)
        {
            if (animation == null || animation.RecentResourceErrors.Count == 0)
                return "none";

            var builder = new StringBuilder();
            int start = Math.Max(0, animation.RecentResourceErrors.Count - 12);
            for (int i = start; i < animation.RecentResourceErrors.Count; i++)
            {
                if (i > start)
                    builder.Append('\n');
                builder.Append(FormatError(animation.RecentResourceErrors[i]));
            }

            return builder.ToString();
        }

        private static string FormatParameter(MxAnimationQuantizedParameter parameter)
        {
            return EmptyAsDash(parameter.ParameterId)
                + "=" + parameter.Value.ToString(CultureInfo.InvariantCulture)
                + "/" + parameter.Scale.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatError(ResourceError error)
        {
            if (error.IsNone)
                return "none";

            return error.Code
                + " key=" + FormatKey(error.Key)
                + " provider=" + EmptyAsDash(error.ProviderId)
                + " address=" + EmptyAsDash(error.Address)
                + " message=" + EmptyAsDash(error.Message);
        }

        private static string FormatKey(ResourceKey key)
        {
            return key.IsValid ? key.ToString() : "-";
        }

        private static string JoinStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return "-";

            return string.Join(", ", values);
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
