using System;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Rendering
{
    public interface IRenderingDebugSource : IFrameworkDebugSource
    {
    }

    public static class RenderingDebugSectionNames
    {
        public const string Globals = "globals";
        public const string CameraGlobals = "cameraGlobals";
        public const string PipelineTopology = "pipelineTopology";
        public const string SharedRTHealth = "sharedRTHealth";
        public const string MaterialBindings = "materialBindings";
        public const string VolumeBlender = "volumeBlender";
        public const string PublisherCounts = "publisherCounts";
    }

    public sealed class GlobalFrameContextDebugSource : IRenderingDebugSource
    {
        private readonly IGlobalFrameContext _context;

        public GlobalFrameContextDebugSource(IGlobalFrameContext context, string name = "Rendering")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection(RenderingDebugSectionNames.Globals, FormatGlobals(_context.Snapshot()))
                });
        }

        private static string FormatGlobals(GlobalFrameSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("time: ").Append(snapshot.Time).Append('\n');
            builder.Append("gameTime: ").Append(snapshot.GameTime).Append('\n');
            builder.Append("deltaTime: ").Append(snapshot.DeltaTime).Append('\n');
            builder.Append("windDirection: ").Append(snapshot.WindDirection).Append('\n');
            builder.Append("windStrength: ").Append(snapshot.WindStrength).Append('\n');
            builder.Append("windTurbulence: ").Append(snapshot.WindTurbulence).Append('\n');
            builder.Append("wetness: ").Append(snapshot.Wetness).Append('\n');
            builder.Append("rain: ").Append(snapshot.Rain).Append('\n');
            builder.Append("snowCoverage: ").Append(snapshot.SnowCoverage).Append('\n');
            builder.Append("primarySubjectWorldPos: ").Append(snapshot.PrimarySubjectWorldPos).Append('\n');
            builder.Append("primarySubjectVelocity: ").Append(snapshot.PrimarySubjectVelocity).Append('\n');
            builder.Append("localSubjectWorldPos: ").Append(snapshot.LocalSubjectWorldPos).Append('\n');
            builder.Append("localSubjectVelocity: ").Append(snapshot.LocalSubjectVelocity);
            return builder.ToString();
        }
    }

    public sealed class CameraRenderContextDebugSource : IRenderingDebugSource
    {
        private readonly ICameraRenderContext _context;

        public CameraRenderContextDebugSource(ICameraRenderContext context, string name = "Rendering")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection(RenderingDebugSectionNames.CameraGlobals, FormatCameraGlobals(_context.Snapshot()))
                });
        }

        private static string FormatCameraGlobals(CameraRenderSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("cameraKind: ").Append(snapshot.CameraKind).Append('\n');
            builder.Append("viewFocusWorldPos: ").Append(snapshot.ViewFocusWorldPosition).Append('\n');
            builder.Append("overrides: ").Append(snapshot.Overrides.Count);
            return builder.ToString();
        }
    }

    public sealed class RenderPipelineTopologyDebugSource : IRenderingDebugSource
    {
        private readonly IMxRenderPipeline _pipeline;

        public RenderPipelineTopologyDebugSource(IMxRenderPipeline pipeline, string name = "Rendering")
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection(RenderingDebugSectionNames.PipelineTopology, FormatTopology(_pipeline.CaptureTopology()))
                });
        }

        private static string FormatTopology(MxRenderPipelineTopologySnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("cameraKind: ").Append(snapshot.CameraKind).Append('\n');
            builder.Append("passes: ").Append(snapshot.Passes.Count).Append('\n');
            builder.Append("diagnostics: ").Append(snapshot.Diagnostics.Count);

            for (int i = 0; i < snapshot.Passes.Count; i++)
            {
                MxRenderPipelinePassTopology pass = snapshot.Passes[i];
                builder.Append('\n')
                    .Append(pass.Phase)
                    .Append('/')
                    .Append(pass.Order)
                    .Append(' ')
                    .Append(pass.DebugName)
                    .Append(" reads=")
                    .Append(pass.ReadCount)
                    .Append(" writes=")
                    .Append(pass.WriteCount);
            }

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                MxRenderPipelineDiagnostic diagnostic = snapshot.Diagnostics[i];
                builder.Append('\n')
                    .Append(diagnostic.Severity)
                    .Append(' ')
                    .Append(diagnostic.Code)
                    .Append(": ")
                    .Append(diagnostic.Message);
            }

            return builder.ToString();
        }
    }

    public sealed class VolumeBlenderDebugSource : IRenderingDebugSource
    {
        private readonly IVolumeBlender _volumeBlender;

        public VolumeBlenderDebugSource(IVolumeBlender volumeBlender, string name = "Rendering")
        {
            _volumeBlender = volumeBlender ?? throw new ArgumentNullException(nameof(volumeBlender));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection(RenderingDebugSectionNames.VolumeBlender, FormatVolumeBlender(_volumeBlender.CaptureDiagnostics()))
                });
        }

        private static string FormatVolumeBlender(MxVolumeDiagnosticsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("activeRequests: ").Append(snapshot.ActiveRequests.Count).Append('\n');
            builder.Append("expiredRequests: ").Append(snapshot.ExpiredRequests.Count).Append('\n');
            builder.Append("recentBlendStates: ").Append(snapshot.RecentBlendStates.Count);

            for (int i = 0; i < snapshot.ActiveRequests.Count; i++)
                AppendRequest(builder, "active", snapshot.ActiveRequests[i]);

            for (int i = 0; i < snapshot.ExpiredRequests.Count; i++)
                AppendRequest(builder, "expired", snapshot.ExpiredRequests[i]);

            for (int i = 0; i < snapshot.RecentBlendStates.Count; i++)
            {
                MxVolumeBlendStateSnapshot blendState = snapshot.RecentBlendStates[i];
                builder.Append('\n')
                    .Append("blendState cameraKind=")
                    .Append(blendState.Context.CameraKind)
                    .Append(" cameraToken=")
                    .Append(blendState.Context.CameraToken)
                    .Append(" time=")
                    .Append(blendState.Context.PresentationTimeSeconds)
                    .Append(" applied=")
                    .Append(blendState.AppliedProfiles.Count)
                    .Append(" suppressed=")
                    .Append(blendState.SuppressedRequests.Count);

                for (int appliedIndex = 0; appliedIndex < blendState.AppliedProfiles.Count; appliedIndex++)
                {
                    MxVolumeAppliedProfileSnapshot applied = blendState.AppliedProfiles[appliedIndex];
                    builder.Append('\n')
                        .Append("applied id=")
                        .Append(applied.SourceRequestId)
                        .Append(" profile=")
                        .Append(applied.Profile)
                        .Append(" priority=")
                        .Append(applied.Priority)
                        .Append(" weight=")
                        .Append(applied.Weight);
                }
            }

            return builder.ToString();
        }

        private static void AppendRequest(StringBuilder builder, string label, MxVolumeRequestSnapshot request)
        {
            builder.Append('\n')
                .Append(label)
                .Append(" id=")
                .Append(request.RequestId)
                .Append(" profile=")
                .Append(request.Profile)
                .Append(" scope=")
                .Append(request.Scope.Kind)
                .Append(" cameraKind=")
                .Append(request.Scope.CameraKind)
                .Append(" cameraToken=")
                .Append(request.Scope.CameraToken)
                .Append(" priority=")
                .Append(request.Priority)
                .Append(" phase=")
                .Append(request.Phase)
                .Append(" weight=")
                .Append(request.Weight)
                .Append(" sequence=")
                .Append(request.CreationSequence)
                .Append(" cleanup=")
                .Append(request.CleanupReason);

            if (!string.IsNullOrWhiteSpace(request.DebugName))
                builder.Append(" debugName=").Append(request.DebugName);
        }
    }
}
