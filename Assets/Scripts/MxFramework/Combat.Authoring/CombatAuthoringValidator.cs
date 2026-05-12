using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Authoring
{
    public static class CombatAuthoringValidator
    {
        public static CombatAuthoringReport Validate(
            CombatActionAuthoringAsset action,
            CombatSceneBindingAsset sceneBinding)
        {
            var issues = new List<CombatAuthoringIssue>();
            var markers = CollectMarkers(sceneBinding);

            ValidateAction(action, markers, issues);
            ValidateSceneBinding(sceneBinding, markers, issues);

            return new CombatAuthoringReport(issues);
        }

        private static void ValidateAction(
            CombatActionAuthoringAsset action,
            HashSet<string> markers,
            List<CombatAuthoringIssue> issues)
        {
            if (action == null)
            {
                issues.Add(new CombatAuthoringIssue(
                    CombatAuthoringSeverity.Error,
                    string.Empty,
                    "Action",
                    0,
                    CombatAuthoringFrameRange.Empty,
                    "action",
                    "Combat action authoring asset is missing.",
                    "Assign a CombatActionAuthoringAsset.",
                    CombatAuthoringQuickActionKind.SelectAsset));
                return;
            }

            string source = action.name;
            if (action.ActionId <= 0)
            {
                AddActionIssue(issues, source, "Action", 0, CombatAuthoringFrameRange.Empty, "actionId", "Action id must be positive.", "Assign a positive action id.");
            }

            if (action.TotalFrames <= 0)
            {
                AddActionIssue(issues, source, "Action", 0, CombatAuthoringFrameRange.Empty, "totalFrames", "Total frames must be positive.", "Set totalFrames to at least 1.");
            }

            ValidateRange(issues, source, "Startup", 0, action.Startup, action.TotalFrames, "startup");
            ValidateRange(issues, source, "Active", 0, action.Active, action.TotalFrames, "active");
            ValidateRange(issues, source, "Recovery", 0, action.Recovery, action.TotalFrames, "recovery");
            ValidateShapes(issues, source, "Hitbox", action.Hitboxes, action.TotalFrames, markers);
            ValidateShapes(issues, source, "Hurtbox", action.Hurtboxes, action.TotalFrames, markers);
            ValidateWeaponTraces(issues, source, action.WeaponTraces, action.TotalFrames, markers);
        }

        private static void ValidateSceneBinding(
            CombatSceneBindingAsset sceneBinding,
            HashSet<string> markers,
            List<CombatAuthoringIssue> issues)
        {
            if (sceneBinding == null)
            {
                return;
            }

            string source = sceneBinding.name;
            CombatActorBindingData[] actors = CloneAndSort(sceneBinding.Actors, CompareActorBinding);
            var entityIds = new HashSet<int>();
            var bodyIds = new HashSet<int>();
            var colliderIds = new HashSet<int>();

            for (int i = 0; i < actors.Length; i++)
            {
                CombatActorBindingData actor = actors[i];
                if (!entityIds.Add(actor.EntityId))
                {
                    AddBindingIssue(issues, source, "Actor", actor.EntityId, "entityId", "Duplicate entity id in scene binding.", "Assign a unique entity id.", i);
                }

                if (!bodyIds.Add(actor.BodyId))
                {
                    AddBindingIssue(issues, source, "Actor", actor.EntityId, "bodyId", "Duplicate body id in scene binding.", "Assign a unique body id.", i);
                }

                if (!MarkerExists(markers, actor.MarkerId))
                {
                    AddBindingIssue(issues, source, "Actor", actor.EntityId, "markerId", "Actor marker is missing.", "Create Preview Marker or relink the actor marker.", i);
                }

                CombatColliderBindingData[] colliders = CloneAndSort(actor.Colliders, CompareColliderBinding);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    CombatColliderBindingData collider = colliders[colliderIndex];
                    if (!colliderIds.Add(collider.ColliderId))
                    {
                        AddBindingIssue(issues, source, "Collider", collider.ColliderId, "colliderId", "Duplicate collider id in scene binding.", "Assign a unique collider id.", collider.SourceOrder);
                    }

                    if (!MarkerExists(markers, collider.MarkerId))
                    {
                        AddBindingIssue(issues, source, "Collider", collider.ColliderId, "markerId", "Collider marker is missing.", "Create Preview Marker or relink the collider marker.", collider.SourceOrder);
                    }
                }
            }
        }

        private static void ValidateShapes(
            List<CombatAuthoringIssue> issues,
            string source,
            string section,
            CombatShapeAuthoringData[] shapes,
            int totalFrames,
            HashSet<string> markers)
        {
            CombatShapeAuthoringData[] sorted = CloneAndSort(shapes, CompareShape);
            var trackIds = new HashSet<int>();
            for (int i = 0; i < sorted.Length; i++)
            {
                CombatShapeAuthoringData shape = sorted[i];
                if (!trackIds.Add(shape.TrackId))
                {
                    AddShapeIssue(issues, source, section, shape.TrackId, shape.FrameRange, "trackId", "Duplicate track id.", "Assign a unique track id.", shape.SourceOrder);
                }

                ValidateRange(issues, source, section, shape.TrackId, shape.FrameRange, totalFrames, "frameRange", shape.SourceOrder);
                if (shape.RadiusRaw <= 0)
                {
                    AddShapeIssue(issues, source, section, shape.TrackId, shape.FrameRange, "radiusRaw", "Shape radius must be positive.", "Set radiusRaw to a positive fixed raw value.", shape.SourceOrder);
                }

                if (shape.ShapeKind == CombatAuthoringShapeKind.Capsule
                    && shape.RadiusRaw > 0
                    && shape.HeightRaw > 0
                    && (long)shape.HeightRaw < (long)shape.RadiusRaw * 2L)
                {
                    AddShapeIssue(
                        issues,
                        source,
                        section,
                        shape.TrackId,
                        shape.FrameRange,
                        "heightRaw",
                        "Capsule height is smaller than diameter.",
                        "Set heightRaw to at least radiusRaw * 2, or leave heightRaw at 0 for legacy default preview height.",
                        shape.SourceOrder,
                        CombatAuthoringSeverity.Warning);
                }

                if (!MarkerExists(markers, shape.MarkerId))
                {
                    AddShapeIssue(issues, source, section, shape.TrackId, shape.FrameRange, "markerId", "Shape marker is missing.", "Create Preview Marker, relink the marker, or use an asset default.", shape.SourceOrder);
                }
            }
        }

        private static void ValidateWeaponTraces(
            List<CombatAuthoringIssue> issues,
            string source,
            CombatWeaponTraceAuthoringData[] traces,
            int totalFrames,
            HashSet<string> markers)
        {
            CombatWeaponTraceAuthoringData[] sorted = CloneAndSort(traces, CompareWeaponTrace);
            var traceIds = new HashSet<int>();
            for (int i = 0; i < sorted.Length; i++)
            {
                CombatWeaponTraceAuthoringData trace = sorted[i];
                if (!traceIds.Add(trace.TraceId))
                {
                    AddShapeIssue(issues, source, "WeaponTrace", trace.TraceId, trace.FrameRange, "traceId", "Duplicate trace id.", "Assign a unique trace id.", trace.SourceOrder);
                }

                ValidateRange(issues, source, "WeaponTrace", trace.TraceId, trace.FrameRange, totalFrames, "frameRange", trace.SourceOrder);
                if (trace.RadiusRaw < 0)
                {
                    AddShapeIssue(issues, source, "WeaponTrace", trace.TraceId, trace.FrameRange, "radiusRaw", "Weapon trace radius cannot be negative.", "Set radiusRaw to 0 or a positive fixed raw value.", trace.SourceOrder);
                }

                if (!MarkerExists(markers, trace.RootMarkerId))
                {
                    AddShapeIssue(issues, source, "WeaponTrace", trace.TraceId, trace.FrameRange, "rootMarkerId", "Weapon trace root marker is missing.", "Create Preview Marker or relink the trace root marker.", trace.SourceOrder);
                }

                if (!MarkerExists(markers, trace.TipMarkerId))
                {
                    AddShapeIssue(issues, source, "WeaponTrace", trace.TraceId, trace.FrameRange, "tipMarkerId", "Weapon trace tip marker is missing.", "Create Preview Marker or relink the trace tip marker.", trace.SourceOrder);
                }
            }
        }

        private static void ValidateRange(
            List<CombatAuthoringIssue> issues,
            string source,
            string section,
            int trackId,
            CombatAuthoringFrameRange range,
            int totalFrames,
            string field,
            int sourceOrder = 0)
        {
            if (range.StartFrame < 0 || (!range.IsEmpty && range.EndFrame < range.StartFrame))
            {
                AddShapeIssue(issues, source, section, trackId, range, field, "Frame range is invalid.", "Clamp Frame Range.", sourceOrder);
                return;
            }

            if (!range.IsEmpty && totalFrames > 0 && range.EndFrame >= totalFrames)
            {
                AddShapeIssue(issues, source, section, trackId, range, field, "Frame range must be inside totalFrames.", "Clamp Frame Range.", sourceOrder);
            }
        }

        private static HashSet<string> CollectMarkers(CombatSceneBindingAsset sceneBinding)
        {
            var markers = new HashSet<string>(StringComparer.Ordinal);
            if (sceneBinding == null)
            {
                return markers;
            }

            CombatMarkerBindingData[] sorted = CloneAndSort(sceneBinding.Markers, CompareMarkerBinding);
            for (int i = 0; i < sorted.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(sorted[i].MarkerId))
                {
                    markers.Add(sorted[i].MarkerId);
                }
            }

            return markers;
        }

        private static bool MarkerExists(HashSet<string> markers, string markerId)
        {
            return !string.IsNullOrWhiteSpace(markerId) && markers.Contains(markerId);
        }

        private static void AddActionIssue(
            List<CombatAuthoringIssue> issues,
            string source,
            string section,
            int trackId,
            CombatAuthoringFrameRange range,
            string field,
            string message,
            string suggestedFix)
        {
            issues.Add(new CombatAuthoringIssue(
                CombatAuthoringSeverity.Error,
                source,
                section,
                trackId,
                range,
                field,
                message,
                suggestedFix,
                CombatAuthoringQuickActionKind.ClampFrameRange));
        }

        private static void AddShapeIssue(
            List<CombatAuthoringIssue> issues,
            string source,
            string section,
            int trackId,
            CombatAuthoringFrameRange range,
            string field,
            string message,
            string suggestedFix,
            int sourceOrder,
            CombatAuthoringSeverity severity = CombatAuthoringSeverity.Error)
        {
            CombatAuthoringQuickActionKind quickAction = field.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0
                ? CombatAuthoringQuickActionKind.CreatePreviewMarker
                : CombatAuthoringQuickActionKind.ClampFrameRange;

            issues.Add(new CombatAuthoringIssue(
                severity,
                source,
                section,
                trackId,
                range,
                field,
                message,
                suggestedFix,
                quickAction,
                sourceOrder));
        }

        private static void AddBindingIssue(
            List<CombatAuthoringIssue> issues,
            string source,
            string section,
            int trackId,
            string field,
            string message,
            string suggestedFix,
            int sourceOrder)
        {
            CombatAuthoringQuickActionKind quickAction = field == "markerId"
                ? CombatAuthoringQuickActionKind.CreatePreviewMarker
                : CombatAuthoringQuickActionKind.SelectAsset;

            issues.Add(new CombatAuthoringIssue(
                CombatAuthoringSeverity.Error,
                source,
                section,
                trackId,
                CombatAuthoringFrameRange.Empty,
                field,
                message,
                suggestedFix,
                quickAction,
                sourceOrder));
        }

        private static T[] CloneAndSort<T>(T[] values, Comparison<T> comparison)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<T>();
            }

            T[] sorted = (T[])values.Clone();
            Array.Sort(sorted, comparison);
            return sorted;
        }

        private static int CompareShape(CombatShapeAuthoringData left, CombatShapeAuthoringData right)
        {
            int compare = left.TrackId.CompareTo(right.TrackId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareWeaponTrace(CombatWeaponTraceAuthoringData left, CombatWeaponTraceAuthoringData right)
        {
            int compare = left.TraceId.CompareTo(right.TraceId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareActorBinding(CombatActorBindingData left, CombatActorBindingData right)
        {
            int compare = left.EntityId.CompareTo(right.EntityId);
            return compare != 0 ? compare : left.BodyId.CompareTo(right.BodyId);
        }

        private static int CompareColliderBinding(CombatColliderBindingData left, CombatColliderBindingData right)
        {
            int compare = left.ColliderId.CompareTo(right.ColliderId);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareMarkerBinding(CombatMarkerBindingData left, CombatMarkerBindingData right)
        {
            int compare = string.CompareOrdinal(left.MarkerId, right.MarkerId);
            return compare != 0 ? compare : left.SourceOrder.CompareTo(right.SourceOrder);
        }
    }
}
