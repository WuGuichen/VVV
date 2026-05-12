using System;
using MxFramework.Combat.Authoring;
using UnityEditor;

namespace MxFramework.Combat.Editor
{
    internal static class CombatAuthoringSceneState
    {
        public static event Action Changed;

        private static CombatActionAuthoringAsset _actionAsset;
        private static CombatSceneBindingAsset _sceneBindingAsset;
        private static int _frame;
        private static CombatAuthoringSelection _selection;
        private static int _dataRevision;

        public static CombatAuthoringVisibility Visibility { get; private set; } = CombatAuthoringVisibility.Default;

        public static CombatActionAuthoringAsset ActionAsset
        {
            get
            {
                if (_actionAsset != null)
                {
                    return _actionAsset;
                }

                return UnityEditor.Selection.activeObject as CombatActionAuthoringAsset;
            }
        }

        public static CombatSceneBindingAsset SceneBindingAsset
        {
            get
            {
                if (_sceneBindingAsset != null)
                {
                    return _sceneBindingAsset;
                }

                return UnityEditor.Selection.activeObject as CombatSceneBindingAsset;
            }
        }

        public static int Frame => ClampFrame(_frame, ActionAsset);

        public static CombatAuthoringSelection Selection => _selection;

        public static int DataRevision => _dataRevision;

        public static void SetContext(CombatActionAuthoringAsset actionAsset, CombatSceneBindingAsset sceneBindingAsset)
        {
            bool changed = _actionAsset != actionAsset || _sceneBindingAsset != sceneBindingAsset;
            _actionAsset = actionAsset;
            _sceneBindingAsset = sceneBindingAsset;

            int clampedFrame = ClampFrame(_frame, actionAsset);
            if (_frame != clampedFrame)
            {
                _frame = clampedFrame;
                changed = true;
            }

            NotifyIfChanged(changed);
        }

        public static void SetFrame(int frame)
        {
            int clampedFrame = ClampFrame(frame, ActionAsset);
            if (_frame == clampedFrame)
            {
                return;
            }

            _frame = clampedFrame;
            NotifyChanged();
        }

        public static void SetSelection(CombatAuthoringSelection selection)
        {
            if (_selection.Equals(selection))
            {
                return;
            }

            _selection = selection;
            NotifyChanged();
        }

        public static void SetVisibility(CombatAuthoringVisibility visibility)
        {
            if (Visibility.Equals(visibility))
            {
                return;
            }

            Visibility = visibility;
            NotifyChanged();
        }

        public static void NotifyDataChanged()
        {
            _dataRevision++;
            NotifyChanged();
        }

        private static int ClampFrame(int frame, CombatActionAuthoringAsset actionAsset)
        {
            int maxFrame = Math.Max(0, actionAsset == null ? 0 : actionAsset.TotalFrames - 1);
            return Math.Max(0, Math.Min(frame, maxFrame));
        }

        private static void NotifyIfChanged(bool changed)
        {
            if (changed)
            {
                NotifyChanged();
            }
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }
    }

    internal readonly struct CombatAuthoringSelection : IEquatable<CombatAuthoringSelection>
    {
        public CombatAuthoringSelection(string section, int trackId, string propertyPath)
        {
            Section = section ?? string.Empty;
            TrackId = trackId;
            PropertyPath = propertyPath ?? string.Empty;
        }

        public string Section { get; }

        public int TrackId { get; }

        public string PropertyPath { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Section) || string.IsNullOrEmpty(PropertyPath);

        public bool Matches(string section, int trackId)
        {
            return !IsEmpty
                && TrackId == trackId
                && string.Equals(Section, section, StringComparison.Ordinal);
        }

        public bool Equals(CombatAuthoringSelection other)
        {
            return TrackId == other.TrackId
                && string.Equals(Section, other.Section, StringComparison.Ordinal)
                && string.Equals(PropertyPath, other.PropertyPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatAuthoringSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = Section == null ? 0 : Section.GetHashCode();
            hash = (hash * 397) ^ TrackId;
            hash = (hash * 397) ^ (PropertyPath == null ? 0 : PropertyPath.GetHashCode());
            return hash;
        }
    }

    internal struct CombatAuthoringVisibility : IEquatable<CombatAuthoringVisibility>
    {
        public static CombatAuthoringVisibility Default => new CombatAuthoringVisibility
        {
            Actor = true,
            Body = true,
            Collider = true,
            Trace = true,
            Labels = true,
        };

        public bool Actor;
        public bool Body;
        public bool Collider;
        public bool Trace;
        public bool Labels;

        public bool Equals(CombatAuthoringVisibility other)
        {
            return Actor == other.Actor
                && Body == other.Body
                && Collider == other.Collider
                && Trace == other.Trace
                && Labels == other.Labels;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatAuthoringVisibility other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = Actor ? 1 : 0;
            hash = (hash * 397) ^ (Body ? 1 : 0);
            hash = (hash * 397) ^ (Collider ? 1 : 0);
            hash = (hash * 397) ^ (Trace ? 1 : 0);
            hash = (hash * 397) ^ (Labels ? 1 : 0);
            return hash;
        }
    }
}
