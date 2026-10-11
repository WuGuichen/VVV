using System;
using System.Text;
using MxFramework.Camera;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class CameraDebugSource : IFrameworkDebugSource
    {
        private readonly Func<MxCameraDebugSnapshot> _snapshotFactory;

        public CameraDebugSource(Func<MxCameraDebugSnapshot> snapshotFactory, string name = "Camera")
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "Camera" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotFactory != null && (_snapshotFactory()?.IsAvailable ?? false);

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            MxCameraDebugSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
            if (snapshot == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "snapshot unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(snapshot)),
                    new FrameworkDebugSection("Target Group", CreateTargetGroup(snapshot)),
                    new FrameworkDebugSection("Framing", CreateFraming(snapshot)),
                    new FrameworkDebugSection("Diagnostics", CreateDiagnostics(snapshot))
                });
        }

        private static string CreateSummary(MxCameraDebugSnapshot snapshot)
        {
            return "available: " + (snapshot.IsAvailable ? "true" : "false")
                + "\nrig: " + snapshot.RigId
                + "\nbackend: " + snapshot.BackendId
                + "\nprofile: " + snapshot.ActiveProfileId
                + "\nmode: " + snapshot.Mode
                + "\nstateSource: " + snapshot.State.Source
                + "\nshakeQueue: " + snapshot.ShakeRequestCount;
        }

        private static string CreateTargetGroup(MxCameraDebugSnapshot snapshot)
        {
            MxCameraTargetGroupState group = snapshot.TargetGroupState;
            return "group: " + group.GroupId
                + "\nvalidTargets: " + group.ValidTargetCount
                + "\nprimary: " + group.PrimaryTarget
                + "\ncenter: " + group.Center
                + "\nradius: " + group.Radius
                + "\nboundsExceeded: " + (group.BoundsExceeded ? "true" : "false");
        }

        private static string CreateFraming(MxCameraDebugSnapshot snapshot)
        {
            MxCameraState state = snapshot.State;
            return "position: " + state.Position
                + "\nfocus: " + state.FocusCenter
                + "\nprojection: " + state.ProjectionKind
                + "\nfov: " + state.FieldOfView
                + "\northographicSize: " + state.OrthographicSize
                + "\nframingUtilization: " + state.FramingUtilization
                + "\nshakeOffset: " + state.ShakeOffset;
        }

        private static string CreateDiagnostics(MxCameraDebugSnapshot snapshot)
        {
            if (snapshot.RecentDiagnostics.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.RecentDiagnostics.Count; i++)
            {
                MxCameraDiagnostic diagnostic = snapshot.RecentDiagnostics[i];
                builder.Append(diagnostic.Code)
                    .Append(" field=")
                    .Append(diagnostic.Field)
                    .Append(" request=")
                    .Append(diagnostic.RequestId)
                    .Append(" message=")
                    .Append(diagnostic.Message);
                if (i + 1 < snapshot.RecentDiagnostics.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
