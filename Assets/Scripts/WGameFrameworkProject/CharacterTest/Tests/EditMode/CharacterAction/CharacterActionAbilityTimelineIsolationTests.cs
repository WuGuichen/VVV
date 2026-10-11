using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Gameplay;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAbilityTimelineIsolationTests
    {
        [Test]
        public void AbilityTimeline_FieldEditAndGestureParticipateInUndoRedo()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                var timeline = new AbilityEffectTimelineDraft
                {
                    TimelineId = "test.undo.timeline",
                    DurationFrames = 8,
                };
                SetField(window, "_abilityAuthoringRoot", new JObject { ["schemaVersion"] = "1" });
                SetField(window, "_abilityEffectTimelines", new List<AbilityEffectTimelineDraft> { timeline });
                SetField(window, "_selectedAbilityEffectTimelineId", timeline.TimelineId);
                SetField(window, "_abilityEffectTimelineDirty", false);

                Invoke(
                    window,
                    "ApplyAbilityEffectTimelineFieldChange",
                    new Action(() => timeline.DurationFrames = 24));

                Assert.AreEqual(24, GetAbilityTimeline(window).DurationFrames);
                Assert.IsTrue(GetField<bool>(window, "_abilityEffectTimelineDirty"));

                Invoke(window, "UndoEdit");

                Assert.AreEqual(8, GetAbilityTimeline(window).DurationFrames);
                Assert.IsFalse(GetField<bool>(window, "_abilityEffectTimelineDirty"));

                Invoke(window, "RedoEdit");

                Assert.AreEqual(24, GetAbilityTimeline(window).DurationFrames);
                Assert.IsTrue(GetField<bool>(window, "_abilityEffectTimelineDirty"));

                var hostParent = new VisualElement();
                Invoke(window, "AddAbilityEffectTimelineTimelineView", hostParent, GetAbilityTimeline(window));
                CharacterActionTimelineView abilityView = GetField<CharacterActionTimelineView>(
                    window,
                    "_abilityEffectTimelineView");
                CharacterActionTimelineHost host = GetField<CharacterActionTimelineHost>(abilityView, "_host");
                host.BeginGesture();
                GetAbilityTimeline(window).DurationFrames = 36;
                host.EndGesture();

                Invoke(window, "UndoEdit");

                Assert.AreEqual(24, GetAbilityTimeline(window).DurationFrames);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AbilityTimeline_SourceSwitchStagesFirstAndCancelPreservesDraft()
        {
            string root = Path.Combine("Temp", "CharacterActionAbilitySourceSwitch", Guid.NewGuid().ToString("N"));
            string currentPath = Path.Combine(root, "current.json");
            string targetPath = Path.Combine(root, "target.json");
            Directory.CreateDirectory(root);
            File.WriteAllText(currentPath, AbilityDocumentJson("current.timeline", 8));
            File.WriteAllText(targetPath, AbilityDocumentJson("target.timeline", 16));
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.CreateGUI();
                SetField(window, "_abilityAuthoringJsonPath", currentPath);
                SetField(window, "_abilityAuthoringRoot", JObject.Parse(File.ReadAllText(currentPath)));
                SetField(
                    window,
                    "_abilityEffectTimelines",
                    new List<AbilityEffectTimelineDraft>
                    {
                        new AbilityEffectTimelineDraft
                        {
                            TimelineId = "current.timeline",
                            DurationFrames = 99,
                        },
                    });
                SetField(window, "_selectedAbilityEffectTimelineId", "current.timeline");
                SetField(window, "_abilityEffectTimelineDirty", true);
                int confirmationCount = 0;
                int errorCount = 0;
                SetField(
                    window,
                    "_documentGraphSwitchConfirmationOverride",
                    new Func<bool>(() =>
                    {
                        confirmationCount++;
                        return false;
                    }));
                SetField(
                    window,
                    "_documentGraphSwitchErrorOverride",
                    new Action<string, string>((_, __) => errorCount++));

                bool switched = (bool)Invoke(
                    window,
                    "TrySwitchAbilityEffectTimelineSource",
                    targetPath,
                    true,
                    false);

                Assert.IsFalse(switched);
                Assert.AreEqual(1, confirmationCount);
                Assert.AreEqual(currentPath, GetField<string>(window, "_abilityAuthoringJsonPath"));
                Assert.AreEqual(99, GetAbilityTimeline(window).DurationFrames);
                Assert.IsTrue(GetField<bool>(window, "_abilityEffectTimelineDirty"));

                File.WriteAllText(targetPath, "{ invalid json");
                switched = (bool)Invoke(
                    window,
                    "TrySwitchAbilityEffectTimelineSource",
                    targetPath,
                    true,
                    false);

                Assert.IsFalse(switched);
                Assert.AreEqual(1, confirmationCount, "Invalid target must fail before confirmation.");
                Assert.AreEqual(1, errorCount);
                Assert.AreEqual(currentPath, GetField<string>(window, "_abilityAuthoringJsonPath"));
                Assert.AreEqual(99, GetAbilityTimeline(window).DurationFrames);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void AbilityTimeline_PreviewProjectionReusesCharacterActionTracks()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                var timeline = new AbilityEffectTimelineDraft
                {
                    TimelineId = "test.fireball",
                    DurationFrames = 30,
                    Events = new List<AbilityEffectTimelineEventDraft>
                    {
                        new AbilityEffectTimelineEventDraft
                        {
                            EventId = "test.fireball.projectile",
                            TrackKind = AbilityEffectTimelineTrackKind.Gameplay.ToString(),
                            EventKind = AbilityEffectTimelineEventKind.SpawnProjectile.ToString(),
                            Frame = 8,
                            RequestId = "projectile.test.fireball",
                        },
                        new AbilityEffectTimelineEventDraft
                        {
                            EventId = "test.fireball.vfx",
                            TrackKind = AbilityEffectTimelineTrackKind.Presentation.ToString(),
                            EventKind = AbilityEffectTimelineEventKind.SpawnVfx.ToString(),
                            Frame = 9,
                            PresentationProfileId = "vfx.test.fireball",
                        },
                    },
                };

                var preview = (CharacterActionAuthoringModel)Invoke(
                    window,
                    "BuildAbilityPreviewModel",
                    timeline);

                Assert.NotNull(preview);
                Assert.AreEqual(30, preview.SelectedAction.DurationFrames);
                Assert.AreEqual(1, preview.SelectedAction.GameplayTrack.Events.Length);
                Assert.AreEqual(
                    CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                    preview.SelectedAction.GameplayTrack.Events[0].Kind);
                Assert.AreEqual(
                    CharacterActionAuthoringIds.Resolve("projectile.test.fireball", 408),
                    preview.SelectedAction.GameplayTrack.Events[0].RequestId);
                Assert.AreEqual(1, preview.SelectedAction.PresentationTrack.Events.Length);
                Assert.AreEqual(
                    CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                    preview.SelectedAction.PresentationTrack.Events[0].Kind);
                Assert.AreEqual(
                    "vfx.test.fireball",
                    preview.SelectedAction.PresentationTrack.Events[0].ResourceKey);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AbilityTimeline_UsesIndependentViewAndUnifiedSaveHost()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                Invoke(window, "OpenActionCompositionWorkspace");

                CharacterActionTimelineView actionView = GetField<CharacterActionTimelineView>(
                    window,
                    "_timelineView");
                Assert.NotNull(actionView);
                Assert.NotNull(actionView.PlaybackController);
                VisualElement actionParent = actionView.parent;

                var abilityParent = new VisualElement();
                Invoke(
                    window,
                    "AddAbilityEffectTimelineTimelineView",
                    abilityParent,
                    new AbilityEffectTimelineDraft
                    {
                        TimelineId = "test.ability.timeline",
                        DurationFrames = 12,
                    });

                CharacterActionTimelineView abilityView = GetField<CharacterActionTimelineView>(
                    window,
                    "_abilityEffectTimelineView");
                Assert.NotNull(abilityView);
                Assert.AreNotSame(actionView, abilityView);
                Assert.AreSame(actionParent, actionView.parent);
                Assert.AreSame(abilityParent, abilityView.parent);
                Assert.IsNull(actionView.PlaybackController);
                Assert.NotNull(abilityView.PlaybackController);

                CharacterActionTimelineHost abilityHost = GetField<CharacterActionTimelineHost>(
                    abilityView,
                    "_host");
                Assert.NotNull(abilityHost);
                Assert.NotNull(abilityHost.SaveRequested);
                Assert.AreEqual(
                    "SaveAllDirtyCharacterActionSources",
                    abilityHost.SaveRequested.Method.Name);

                Invoke(window, "RebuildTracks");

                Assert.NotNull(actionView.PlaybackController);
                Assert.IsNull(abilityView.PlaybackController);
                Assert.AreSame(actionParent, actionView.parent);
                Assert.AreSame(abilityParent, abilityView.parent);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RefreshWorkbenchAll_ActionWorkspaceRestoresPlaybackToVisibleTimeline()
        {
            CharacterActionAuthoringWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
                window.position = new Rect(0f, 0f, 1180f, 700f);
                window.CreateGUI();
                Invoke(window, "OpenActionCompositionWorkspace");

                Invoke(window, "RefreshWorkbenchAll");

                CharacterActionTimelineView actionView = GetField<CharacterActionTimelineView>(
                    window,
                    "_timelineView");
                CharacterActionTimelineView abilityView = GetField<CharacterActionTimelineView>(
                    window,
                    "_abilityEffectTimelineView");
                Assert.NotNull(actionView);
                Assert.NotNull(actionView.PlaybackController);
                if (abilityView != null)
                    Assert.IsNull(abilityView.PlaybackController);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private static AbilityEffectTimelineDraft GetAbilityTimeline(
            CharacterActionAuthoringWindow window)
        {
            return GetField<List<AbilityEffectTimelineDraft>>(window, "_abilityEffectTimelines")[0];
        }

        private static string AbilityDocumentJson(string id, int duration)
        {
            return new JObject
            {
                ["schemaVersion"] = "1",
                ["effectTimelines"] = new JArray
                {
                    new JObject
                    {
                        ["timelineId"] = id,
                        ["durationFrames"] = duration,
                        ["events"] = new JArray(),
                    },
                },
            }.ToString();
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            return method.Invoke(instance, arguments);
        }
    }
}
