using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        private sealed class SceneMarkerResolver : ICombatAuthoringPreviewMarkerResolver
        {
            private readonly Dictionary<string, string> _markerPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<CombatMarkerBindingData> _markers = new List<CombatMarkerBindingData>(64);
            private readonly List<Transform> _transforms = new List<Transform>(64);

            public SceneMarkerResolver(CombatSceneBindingAsset binding)
            {
                if (binding?.Markers == null)
                {
                    return;
                }

                _markers.AddRange(binding.Markers);
                _markers.Sort(ComparePreviewMarkers);
                for (int i = 0; i < _markers.Count; i++)
                {
                    CombatMarkerBindingData marker = _markers[i];
                    if (string.IsNullOrEmpty(marker.MarkerId) || _markerPaths.ContainsKey(marker.MarkerId))
                    {
                        continue;
                    }

                    _markerPaths.Add(marker.MarkerId, marker.TargetPath ?? string.Empty);
                }
            }

            public bool TryResolveMarker(string markerId, out CombatAuthoringPreviewMarkerPose pose)
            {
                if (string.IsNullOrEmpty(markerId) || !_markerPaths.TryGetValue(markerId, out string targetPath))
                {
                    pose = default;
                    return false;
                }

                Transform transform = ResolveTransformPath(targetPath);
                if (transform == null)
                {
                    pose = default;
                    return false;
                }

                pose = new CombatAuthoringPreviewMarkerPose(transform.position, transform.rotation);
                return true;
            }

            private Transform ResolveTransformPath(string targetPath)
            {
                if (string.IsNullOrEmpty(targetPath))
                {
                    return null;
                }

                GameObject direct = GameObject.Find(targetPath);
                if (direct != null)
                {
                    return direct.transform;
                }

                _transforms.Clear();
                Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null
                        || EditorUtility.IsPersistent(candidate)
                        || (candidate.hideFlags & HideFlags.HideInHierarchy) != 0)
                    {
                        continue;
                    }

                    string candidatePath = GetHierarchyPath(candidate);
                    if (string.Equals(candidatePath, targetPath, StringComparison.Ordinal)
                        || string.Equals(candidate.name, targetPath, StringComparison.Ordinal))
                    {
                        _transforms.Add(candidate);
                    }
                }

                _transforms.Sort(ComparePreviewTransformsByPath);
                return _transforms.Count == 0 ? null : _transforms[0];
            }
        }

        private static int ComparePreviewMarkers(CombatMarkerBindingData left, CombatMarkerBindingData right)
        {
            int markerCompare = string.CompareOrdinal(left.MarkerId, right.MarkerId);
            return markerCompare != 0 ? markerCompare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int ComparePreviewTransformsByPath(Transform left, Transform right)
        {
            return string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right));
        }

        private sealed class TimelineBarData
        {
            public TimelineBarData(int rowIndex, Label rangeLabel, VisualElement leftHandle, VisualElement rightHandle, int totalFrames, float trackWidth)
            {
                RowIndex = rowIndex;
                RangeLabel = rangeLabel;
                LeftHandle = leftHandle;
                RightHandle = rightHandle;
                TotalFrames = totalFrames;
                TrackWidth = trackWidth;
            }

            public int RowIndex { get; }

            public Label RangeLabel { get; }

            public VisualElement LeftHandle { get; }

            public VisualElement RightHandle { get; }

            public int TotalFrames { get; }

            public float TrackWidth { get; }
        }

        private sealed class TimelineDragState
        {
            public bool IsDragging { get; private set; }

            public int RowIndex { get; private set; }

            public TimelineRow Row { get; private set; }

            public TimelineDragMode Mode { get; private set; }

            public int OriginalStartFrame { get; private set; }

            public int OriginalEndFrame { get; private set; }

            public int PointerStartFrame { get; private set; }

            public int PreviewStartFrame { get; private set; }

            public int PreviewEndFrame { get; private set; }

            public int TotalFrames { get; private set; }

            public float TrackWidth { get; private set; }

            public int PointerId { get; private set; }

            public VisualElement Bar { get; private set; }

            public Label RangeLabel { get; private set; }

            public bool HasPreviewChange => PreviewStartFrame != OriginalStartFrame || PreviewEndFrame != OriginalEndFrame;

            public void Begin(
                int rowIndex,
                TimelineRow row,
                TimelineDragMode mode,
                int originalStartFrame,
                int originalEndFrame,
                int pointerStartFrame,
                int totalFrames,
                float trackWidth,
                int pointerId,
                VisualElement bar,
                Label rangeLabel,
                VisualElement leftHandle,
                VisualElement rightHandle)
            {
                IsDragging = true;
                RowIndex = rowIndex;
                Row = row;
                Mode = mode;
                OriginalStartFrame = originalStartFrame;
                OriginalEndFrame = originalEndFrame;
                PointerStartFrame = pointerStartFrame;
                PreviewStartFrame = originalStartFrame;
                PreviewEndFrame = originalEndFrame;
                TotalFrames = totalFrames;
                TrackWidth = trackWidth;
                PointerId = pointerId;
                Bar = bar;
                RangeLabel = rangeLabel;
                SetTimelineHandleOpacity(leftHandle, rightHandle, 1f);
            }

            public void SetPreview(int startFrame, int endFrame)
            {
                PreviewStartFrame = startFrame;
                PreviewEndFrame = endFrame;
            }

            public void Reset()
            {
                IsDragging = false;
                RowIndex = -1;
                Row = default;
                Mode = TimelineDragMode.None;
                OriginalStartFrame = 0;
                OriginalEndFrame = 0;
                PointerStartFrame = 0;
                PreviewStartFrame = 0;
                PreviewEndFrame = 0;
                TotalFrames = 0;
                TrackWidth = 0f;
                PointerId = 0;
                Bar = null;
                RangeLabel = null;
            }
        }

        private enum TimelineDragMode
        {
            None,
            Move,
            ResizeStart,
            ResizeEnd,
        }

        private readonly struct ShapeSnapshot
        {
            private ShapeSnapshot(
                int shapeKindEnumIndex,
                int startFrame,
                int endFrame,
                string markerId,
                Vector3 localCenter,
                int radiusRaw,
                int heightRaw)
            {
                ShapeKindEnumIndex = shapeKindEnumIndex;
                StartFrame = startFrame;
                EndFrame = endFrame;
                MarkerId = markerId;
                LocalCenter = localCenter;
                RadiusRaw = radiusRaw;
                HeightRaw = heightRaw;
            }

            public int ShapeKindEnumIndex { get; }

            public int StartFrame { get; }

            public int EndFrame { get; }

            public string MarkerId { get; }

            public Vector3 LocalCenter { get; }

            public int RadiusRaw { get; }

            public int HeightRaw { get; }

            public static ShapeSnapshot Read(SerializedProperty source)
            {
                SerializedProperty frameRange = source.FindPropertyRelative("frameRange");
                return new ShapeSnapshot(
                    ReadEnum(source, "shapeKind"),
                    ReadInt(frameRange, "startFrame"),
                    ReadInt(frameRange, "endFrame"),
                    ReadString(source, "markerId"),
                    ReadVector3(source, "localCenter"),
                    ReadInt(source, "radiusRaw"),
                    ReadInt(source, "heightRaw"));
            }

            public static void Write(SerializedProperty target, ShapeSnapshot snapshot, int trackId, int sourceOrder)
            {
                WriteInt(target, "trackId", trackId);
                WriteEnum(target, "shapeKind", snapshot.ShapeKindEnumIndex);
                SerializedProperty frameRange = target.FindPropertyRelative("frameRange");
                WriteInt(frameRange, "startFrame", snapshot.StartFrame);
                WriteInt(frameRange, "endFrame", snapshot.EndFrame);
                WriteString(target, "markerId", snapshot.MarkerId);
                WriteVector3(target, "localCenter", snapshot.LocalCenter);
                WriteInt(target, "radiusRaw", snapshot.RadiusRaw);
                WriteInt(target, "heightRaw", snapshot.HeightRaw);
                WriteInt(target, "sourceOrder", sourceOrder);
            }

            private static int ReadInt(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? 0 : property.intValue;
            }

            private static int ReadEnum(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? 0 : property.enumValueIndex;
            }

            private static string ReadString(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? string.Empty : property.stringValue;
            }

            private static Vector3 ReadVector3(SerializedProperty parent, string name)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                return property == null ? Vector3.zero : property.vector3Value;
            }

            private static void WriteInt(SerializedProperty parent, string name, int value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.intValue = value;
                }
            }

            private static void WriteEnum(SerializedProperty parent, string name, int value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.enumValueIndex = value;
                }
            }

            private static void WriteString(SerializedProperty parent, string name, string value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.stringValue = value ?? string.Empty;
                }
            }

            private static void WriteVector3(SerializedProperty parent, string name, Vector3 value)
            {
                SerializedProperty property = parent?.FindPropertyRelative(name);
                if (property != null)
                {
                    property.vector3Value = value;
                }
            }
        }

        private readonly struct TimelineRow
        {
            public TimelineRow(
                string section,
                string label,
                int trackId,
                CombatAuthoringFrameRange frameRange,
                string propertyPath,
                TimelineRowKind kind)
            {
                Section = section;
                Label = label;
                TrackId = trackId;
                FrameRange = frameRange;
                PropertyPath = propertyPath;
                Kind = kind;
            }

            public string Section { get; }

            public string Label { get; }

            public int TrackId { get; }

            public CombatAuthoringFrameRange FrameRange { get; }

            public string PropertyPath { get; }

            public TimelineRowKind Kind { get; }
        }

        private enum TimelineRowKind
        {
            ActionProperty,
        }

        private readonly struct RawPreset
        {
            public RawPreset(string label, int rawValue)
            {
                Label = label;
                RawValue = rawValue;
            }

            public string Label { get; }

            public int RawValue { get; }
        }

        private readonly struct MarkerDropdownItem
        {
            public MarkerDropdownItem(string markerId, string label, bool isPlaceholder)
            {
                MarkerId = markerId;
                Label = label;
                IsPlaceholder = isPlaceholder;
            }

            public string MarkerId { get; }

            public string Label { get; }

            public bool IsPlaceholder { get; }
        }

        private struct SceneMarkerCandidate
        {
            public SceneMarkerCandidate(string transformName, string targetPath, string markerId, int score)
            {
                TransformName = transformName;
                TargetPath = targetPath;
                MarkerId = markerId;
                Score = score;
            }

            public string TransformName { get; }

            public string TargetPath { get; }

            public string MarkerId { get; set; }

            public int Score { get; }
        }
    }
}
