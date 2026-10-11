using System;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAuthoringHistoryIdentityTests
    {
        private const string ProfileDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
        private const string ActionSetDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
        private const string CharacterLibraryDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test";
        private const string CommonLibraryDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";

        [Test]
        public void ProfileStableId_UndoRedoRestoresExactSlotSelectionAndDirtyBaseline()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            CharacterActionAuthoringWindow window = CreateWindow(session, "ProfileComposition");
            try
            {
                CharacterActionProfileJsonDto profile = session.CurrentProfile;
                string originalStableId = profile.StableId;
                string renamedStableId = originalStableId + ".history_test";

                ApplyFieldChange(window, () => profile.StableId = renamedStableId);

                Assert.AreSame(profile, session.CurrentProfile);
                Assert.AreEqual(renamedStableId, session.Selection.ProfileStableId);
                Assert.IsTrue(session.ProfileDocument.State.IsDirty);
                AssertAllNonProfileDocumentsClean(session);

                Invoke(window, "UndoEdit");

                Assert.AreEqual(originalStableId, session.Selection.ProfileStableId);
                Assert.AreEqual(originalStableId, session.CurrentProfile.StableId);
                Assert.IsFalse(session.ProfileDocument.State.IsDirty);
                AssertAllNonProfileDocumentsClean(session);

                Invoke(window, "RedoEdit");

                Assert.AreEqual(renamedStableId, session.Selection.ProfileStableId);
                Assert.AreEqual(renamedStableId, session.CurrentProfile.StableId);
                Assert.IsTrue(session.ProfileDocument.State.IsDirty);
                AssertAllNonProfileDocumentsClean(session);

                Invoke(window, "UndoEdit");
                Assert.IsFalse(session.ProfileDocument.State.IsDirty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ActionSetStableId_UndoRedoUsesDtoIdentityAfterOldIdDisappears()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            CharacterActionAuthoringWindow window = CreateWindow(session, "ActionSet");
            try
            {
                CharacterActionSetJsonDto actionSet = session.CurrentActionSet;
                CharacterActionAuthoringActionSetBundleDocument owner =
                    session.FindActionSetBundle(actionSet.StableId);
                string originalStableId = actionSet.StableId;
                string renamedStableId = originalStableId + ".history_test";

                ApplyFieldChange(window, () => actionSet.StableId = renamedStableId);

                Assert.AreSame(actionSet, session.CurrentActionSet);
                Assert.AreEqual(renamedStableId, session.Selection.ActionSetStableId);
                Assert.IsTrue(owner.State.IsDirty);
                Assert.IsFalse(session.ProfileDocument.State.IsDirty);

                Invoke(window, "UndoEdit");

                Assert.AreEqual(originalStableId, session.Selection.ActionSetStableId);
                Assert.AreEqual(originalStableId, session.CurrentActionSet.StableId);
                Assert.IsFalse(owner.State.IsDirty);
                Assert.IsFalse(session.ProfileDocument.State.IsDirty);

                Invoke(window, "RedoEdit");

                Assert.AreEqual(renamedStableId, session.Selection.ActionSetStableId);
                Assert.AreEqual(renamedStableId, session.CurrentActionSet.StableId);
                Assert.IsTrue(owner.State.IsDirty);
                Assert.IsFalse(session.ProfileDocument.State.IsDirty);

                Invoke(window, "UndoEdit");
                Assert.IsFalse(owner.State.IsDirty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ReactionId_UndoRedoRestoresOwnerSlotAndActionSetReference()
        {
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            CharacterActionAuthoringWindow window = CreateWindow(session, "Reactions");
            try
            {
                CharacterActionSetJsonDto actionSet = session.CurrentActionSet;
                CharacterReactionProfileJsonDto reaction = session.CurrentReactionProfile;
                CharacterActionAuthoringActionSetBundleDocument owner =
                    session.FindActionSetBundle(actionSet.StableId);
                string originalStableId = reaction.StableId;
                string renamedStableId = originalStableId + ".history_test";

                ApplyFieldChange(
                    window,
                    () =>
                    {
                        reaction.StableId = renamedStableId;
                        actionSet.ReactionProfileId = renamedStableId;
                    });

                Assert.AreSame(reaction, session.CurrentReactionProfile);
                Assert.AreEqual(renamedStableId, actionSet.ReactionProfileId);
                Assert.IsTrue(owner.State.IsDirty);

                Invoke(window, "UndoEdit");

                Assert.AreEqual(originalStableId, session.CurrentActionSet.ReactionProfileId);
                Assert.AreEqual(originalStableId, session.CurrentReactionProfile.StableId);
                Assert.IsFalse(owner.State.IsDirty);

                Invoke(window, "RedoEdit");

                Assert.AreEqual(renamedStableId, session.CurrentActionSet.ReactionProfileId);
                Assert.AreEqual(renamedStableId, session.CurrentReactionProfile.StableId);
                Assert.IsTrue(owner.State.IsDirty);

                Invoke(window, "UndoEdit");
                Assert.IsFalse(owner.State.IsDirty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DocumentFieldUndo_DoesNotRewriteSharedResourceFiles()
        {
            string sourcePath = CharacterActionProjectileAuthoringStore.DefaultAbilityAuthoringPath;
            byte[] before = File.ReadAllBytes(sourcePath);
            byte[] afterWindowDispose = null;
            CharacterActionAuthoringDocumentSession session = LoadCodexSession();
            CharacterActionAuthoringWindow window = CreateWindow(session, "ProfileComposition");
            try
            {
                CharacterActionProfileJsonDto profile = session.CurrentProfile;
                string originalDisplayName = profile.DisplayName;
                ApplyFieldChange(window, () => profile.DisplayName = originalDisplayName + " history test");
                Invoke(window, "UndoEdit");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                afterWindowDispose = File.ReadAllBytes(sourcePath);
                if (!ByteArraysEqual(before, afterWindowDispose))
                    File.WriteAllBytes(sourcePath, before);
            }

            CollectionAssert.AreEqual(
                before,
                afterWindowDispose,
                "A document-only Undo must not restore or rewrite shared resource files.");
        }

        private static CharacterActionAuthoringDocumentSession LoadCodexSession()
        {
            return CharacterActionAuthoringDocumentSession.Load(
                new CharacterActionAuthoringSourceEntry(ProfileDirectory, "codex_flow_test"),
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(ActionSetDirectory, "codex_flow_test"),
                },
                new[]
                {
                    new CharacterActionAuthoringSourceEntry(CharacterLibraryDirectory, "codex_flow_test"),
                    new CharacterActionAuthoringSourceEntry(CommonLibraryDirectory, "Common"),
                });
        }

        private static CharacterActionAuthoringWindow CreateWindow(
            CharacterActionAuthoringDocumentSession session,
            string mappingSection)
        {
            var window = ScriptableObject.CreateInstance<CharacterActionAuthoringWindow>();
            window.position = new Rect(0f, 0f, 1180f, 700f);
            Invoke(window, "AdoptDocumentSession", session);
            SetEnumField(window, "_workspace", "Mappings");
            SetEnumField(window, "_mappingSection", mappingSection);
            return window;
        }

        private static void ApplyFieldChange(CharacterActionAuthoringWindow window, Action mutate)
        {
            Invoke(window, "ApplyFieldChange", mutate, true);
        }

        private static void AssertAllNonProfileDocumentsClean(
            CharacterActionAuthoringDocumentSession session)
        {
            for (int i = 0; i < session.DocumentStates.Count; i++)
            {
                if (ReferenceEquals(session.DocumentStates[i], session.ProfileDocument.State))
                    continue;
                Assert.IsFalse(
                    session.DocumentStates[i].IsDirty,
                    session.DocumentStates[i].Key.ToString());
            }
        }

        private static void SetEnumField(object instance, string fieldName, string enumValue)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, Enum.Parse(field.FieldType, enumValue));
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            return method.Invoke(instance, arguments);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }
    }
}
