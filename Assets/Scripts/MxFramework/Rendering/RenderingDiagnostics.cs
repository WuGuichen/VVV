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
}
