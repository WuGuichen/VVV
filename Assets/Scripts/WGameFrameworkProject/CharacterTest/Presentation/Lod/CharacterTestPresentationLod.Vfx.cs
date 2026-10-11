using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestPresentationLod
    {
        private long _vfxTraversal;
        internal long VfxObservations { get; private set; }
        internal long VfxUpdates { get; private set; }
        internal long VfxHolds { get; private set; }
        internal bool ShouldUpdateVfx(int entityId) => ShouldUpdateVfx(entityId, Now, _vfxTraversal);

        /// <summary>NoAlloc after observer warmup. Owner-level decisions are stable across anchor/backend requests.</summary>
        internal bool ShouldUpdateVfx(int entityId, double now, long traversal)
        {
            if (!_subjects.TryGetValue(entityId, out var subject) || !_coordinator.IsRegistered(subject.Handle)) return true;
            if (subject.VfxTraversal == traversal) return subject.VfxDecision;
            if (!subject.PoseRegistered)
            { subject.PoseRegistered = true; _coordinator.InvalidateObservation(subject.Handle); }
            bool protect = subject.OwnerImportant || subject.PadImportant || subject.FeedbackUntil > now || subject.GeometryDirty;
            long cameras = CameraCaptures * 397 + _coordinator.Counters.Cameras;
            if (protect) subject.Vfx.Invalidate();
            if (subject.Vfx.NeedsObservation(now, cameras) || subject.View.PublishedPoseVersion != subject.VfxObservedPoseVersion
                || !subject.Transform.Equals(subject.VfxObservedTransform))
            {
                bool known = true, anyCamera = false, visible = false;
                float height = 0;
                foreach (var pair in _cameras)
                {
                    if (!pair.Value.Captured) continue;
                    anyCamera = true;
                    if (!_coordinator.TryObserve(subject.Handle, pair.Key, out var value)) { known = false; continue; }
                    known &= value.Known; visible |= value.Visible; height = Mathf.Max(height, value.ScreenHeight);
                }
                // This first consumer only reduces offscreen decoration. Visible attack direction and feedback stay current.
                subject.Vfx.Observe(now, cameras, known && anyCamera, height, protect || visible);
                subject.VfxObservedPoseVersion = subject.View.PublishedPoseVersion;
                subject.VfxObservedTransform = subject.Transform; VfxObservations++;
            }
            subject.VfxDecision = subject.Vfx.Evaluate(now); subject.VfxTraversal = traversal;
            if (subject.VfxDecision) VfxUpdates++; else VfxHolds++;
            return subject.VfxDecision;
        }
    }
}
