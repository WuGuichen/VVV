using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI.Adapters
{
    public readonly struct UiCameraRigDebugSnapshot
    {
        public UiCameraRigDebugSnapshot(
            string rigId,
            string rigKind,
            bool available,
            bool stackBound,
            bool layerPolicyValid,
            string uiLayerName,
            bool targetTextureAssigned,
            int targetTextureWidth,
            int targetTextureHeight,
            string code = "",
            string message = "")
        {
            RigId = string.IsNullOrWhiteSpace(rigId) ? "ui.camera" : rigId;
            RigKind = string.IsNullOrWhiteSpace(rigKind) ? "Unknown" : rigKind;
            Available = available;
            StackBound = stackBound;
            LayerPolicyValid = layerPolicyValid;
            UiLayerName = uiLayerName ?? string.Empty;
            TargetTextureAssigned = targetTextureAssigned;
            TargetTextureWidth = Math.Max(0, targetTextureWidth);
            TargetTextureHeight = Math.Max(0, targetTextureHeight);
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string RigId { get; }
        public string RigKind { get; }
        public bool Available { get; }
        public bool StackBound { get; }
        public bool LayerPolicyValid { get; }
        public string UiLayerName { get; }
        public bool TargetTextureAssigned { get; }
        public int TargetTextureWidth { get; }
        public int TargetTextureHeight { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public readonly struct UiCameraDiagnostic
    {
        public UiCameraDiagnostic(string code, string rigId, string message)
        {
            Code = code ?? string.Empty;
            RigId = rigId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string RigId { get; }
        public string Message { get; }
    }

    public sealed class UiCameraDebugSnapshot
    {
        private readonly List<UiCameraRigDebugSnapshot> _rigs;
        private readonly List<UiCameraDiagnostic> _diagnostics;

        public UiCameraDebugSnapshot(
            IReadOnlyList<UiCameraRigDebugSnapshot> rigs,
            IReadOnlyList<UiCameraDiagnostic> diagnostics = null)
        {
            _rigs = rigs != null
                ? new List<UiCameraRigDebugSnapshot>(rigs)
                : new List<UiCameraRigDebugSnapshot>();
            _diagnostics = diagnostics != null
                ? new List<UiCameraDiagnostic>(diagnostics)
                : new List<UiCameraDiagnostic>();
            _rigs.Sort(CompareRigs);
            _diagnostics.Sort(CompareDiagnostics);
        }

        public IReadOnlyList<UiCameraRigDebugSnapshot> Rigs => _rigs;
        public IReadOnlyList<UiCameraDiagnostic> Diagnostics => _diagnostics;
        public bool IsAvailable => _rigs.Count > 0;

        private static int CompareRigs(UiCameraRigDebugSnapshot left, UiCameraRigDebugSnapshot right)
        {
            return string.Compare(left.RigId, right.RigId, StringComparison.Ordinal);
        }

        private static int CompareDiagnostics(UiCameraDiagnostic left, UiCameraDiagnostic right)
        {
            int rig = string.Compare(left.RigId, right.RigId, StringComparison.Ordinal);
            if (rig != 0)
                return rig;

            return string.Compare(left.Code, right.Code, StringComparison.Ordinal);
        }
    }

    public sealed class UiCameraDebugSource : IFrameworkDebugSource
    {
        private readonly Func<UiCameraDebugSnapshot> _snapshotFactory;

        public UiCameraDebugSource(Func<UiCameraDebugSnapshot> snapshotFactory, string name = "UICamera")
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "UICamera" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotFactory != null && (_snapshotFactory()?.IsAvailable ?? false);

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            UiCameraDebugSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
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
                    new FrameworkDebugSection("Rigs", CreateRigs(snapshot)),
                    new FrameworkDebugSection("Diagnostics", CreateDiagnostics(snapshot))
                });
        }

        private static string CreateSummary(UiCameraDebugSnapshot snapshot)
        {
            int available = 0;
            int stackBound = 0;
            int textureAssigned = 0;
            for (int i = 0; i < snapshot.Rigs.Count; i++)
            {
                UiCameraRigDebugSnapshot rig = snapshot.Rigs[i];
                if (rig.Available)
                    available++;
                if (rig.StackBound)
                    stackBound++;
                if (rig.TargetTextureAssigned)
                    textureAssigned++;
            }

            return "rigs: " + snapshot.Rigs.Count
                + "\navailable: " + available
                + "\nstackBound: " + stackBound
                + "\ntargetTextures: " + textureAssigned
                + "\ndiagnostics: " + snapshot.Diagnostics.Count;
        }

        private static string CreateRigs(UiCameraDebugSnapshot snapshot)
        {
            if (snapshot.Rigs.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Rigs.Count; i++)
            {
                UiCameraRigDebugSnapshot rig = snapshot.Rigs[i];
                builder.Append("rig=")
                    .Append(rig.RigId)
                    .Append(" kind=")
                    .Append(rig.RigKind)
                    .Append(" available=")
                    .Append(rig.Available ? "true" : "false")
                    .Append(" stackBound=")
                    .Append(rig.StackBound ? "true" : "false")
                    .Append(" layerPolicy=")
                    .Append(rig.LayerPolicyValid ? "valid" : "invalid")
                    .Append(" layer=")
                    .Append(string.IsNullOrEmpty(rig.UiLayerName) ? "-" : rig.UiLayerName)
                    .Append(" texture=")
                    .Append(rig.TargetTextureAssigned ? "assigned" : "missing")
                    .Append(" textureSize=")
                    .Append(rig.TargetTextureWidth)
                    .Append('x')
                    .Append(rig.TargetTextureHeight);

                if (!string.IsNullOrEmpty(rig.Code))
                    builder.Append(" code=").Append(rig.Code);
                if (!string.IsNullOrEmpty(rig.Message))
                    builder.Append(" message=").Append(rig.Message);
                if (i + 1 < snapshot.Rigs.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateDiagnostics(UiCameraDebugSnapshot snapshot)
        {
            if (snapshot.Diagnostics.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                UiCameraDiagnostic diagnostic = snapshot.Diagnostics[i];
                builder.Append(diagnostic.Code)
                    .Append(" rig=")
                    .Append(diagnostic.RigId)
                    .Append(" message=")
                    .Append(diagnostic.Message);
                if (i + 1 < snapshot.Diagnostics.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
