using System;
using System.Reflection;
using MxFramework.Combat.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Combat.Authoring
{
    public sealed class CombatAuthoringWindowMarkerTests
    {
        private static readonly Type WindowType = Type.GetType("MxFramework.Combat.Editor.CombatAuthoringWindow, MxFramework.Combat.Editor", true);

        [Test]
        public void GetDefaultMarkerId_IgnoresActorMarkerMissingFromMarkers()
        {
            using (var scope = new WindowScope())
            {
                CombatSceneBindingAsset binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
                binding.Actors = new[]
                {
                    new CombatActorBindingData
                    {
                        EntityId = 1,
                        MarkerId = "missing_actor_marker",
                        BodyId = 1,
                    },
                };
                binding.Markers = new[]
                {
                    new CombatMarkerBindingData
                    {
                        MarkerId = "valid_marker",
                        TargetPath = "Root/Valid",
                    },
                };

                scope.SetSceneBinding(binding);

                Assert.AreEqual("valid_marker", scope.GetDefaultMarkerId());
            }
        }

        [Test]
        public void GetDefaultMarkerId_UsesActorMarkerWhenItExistsInMarkers()
        {
            using (var scope = new WindowScope())
            {
                CombatSceneBindingAsset binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
                binding.Actors = new[]
                {
                    new CombatActorBindingData
                    {
                        EntityId = 1,
                        MarkerId = "actor_marker",
                        BodyId = 1,
                    },
                };
                binding.Markers = new[]
                {
                    new CombatMarkerBindingData
                    {
                        MarkerId = "fallback_marker",
                        TargetPath = "Root/Fallback",
                    },
                    new CombatMarkerBindingData
                    {
                        MarkerId = "actor_marker",
                        TargetPath = "Root/Actor",
                    },
                };

                scope.SetSceneBinding(binding);

                Assert.AreEqual("actor_marker", scope.GetDefaultMarkerId());
            }
        }

        [Test]
        public void GetDefaultMarkerId_ReturnsFirstNonEmptyMarker()
        {
            using (var scope = new WindowScope())
            {
                CombatSceneBindingAsset binding = ScriptableObject.CreateInstance<CombatSceneBindingAsset>();
                binding.Markers = new[]
                {
                    new CombatMarkerBindingData
                    {
                        MarkerId = string.Empty,
                        TargetPath = "Root/Empty",
                    },
                    new CombatMarkerBindingData
                    {
                        MarkerId = "first_valid",
                        TargetPath = "Root/Valid",
                    },
                };

                scope.SetSceneBinding(binding);

                Assert.AreEqual("first_valid", scope.GetDefaultMarkerId());
            }
        }

        [TestCase(15f, 180f, "ResizeStart")]
        [TestCase(30f, 180f, "Move")]
        [TestCase(165f, 180f, "ResizeEnd")]
        [TestCase(9f, 20f, "ResizeStart")]
        [TestCase(11f, 20f, "ResizeEnd")]
        public void GetTimelineDragMode_UsesFixedEdgeZone(float localX, float barWidth, string expected)
        {
            MethodInfo method = WindowType.GetMethod("GetTimelineDragMode", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(float), typeof(float) }, null);

            object mode = method.Invoke(null, new object[] { localX, barWidth });

            Assert.AreEqual(expected, mode.ToString());
        }

        private sealed class WindowScope : IDisposable
        {
            private static readonly FieldInfo SceneBindingField = WindowType.GetField("_sceneBindingAsset", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly MethodInfo GetDefaultMarkerIdMethod = WindowType.GetMethod("GetDefaultMarkerId", BindingFlags.Instance | BindingFlags.NonPublic);

            private readonly EditorWindow _window;

            public WindowScope()
            {
                _window = ScriptableObject.CreateInstance(WindowType) as EditorWindow;
            }

            public void SetSceneBinding(CombatSceneBindingAsset binding)
            {
                SceneBindingField.SetValue(_window, binding);
            }

            public string GetDefaultMarkerId()
            {
                return (string)GetDefaultMarkerIdMethod.Invoke(_window, Array.Empty<object>());
            }

            public void Dispose()
            {
                if (_window != null)
                {
                    UnityEngine.Object.DestroyImmediate(_window);
                }
            }
        }
    }
}
