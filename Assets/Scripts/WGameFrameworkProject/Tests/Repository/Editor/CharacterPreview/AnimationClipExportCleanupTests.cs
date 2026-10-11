using System;
using System.IO;
using MxFramework.Project.Editor.AnimationDesigner;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MxFramework.Tests.Editor.CharacterPreview
{
    public sealed class AnimationClipExportCleanupTests
    {
        private string _folder;
        private AnimationClip _target;
        private AnimationClip _source;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/AnimationClipCleanupTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(_folder));
            _target = new AnimationClip(); _source = new AnimationClip();
            AssetDatabase.CreateAsset(_target, _folder + "/old.anim");
            AssetDatabase.CreateAsset(_source, _folder + "/new.anim");
        }

        [TearDown]
        public void TearDown() { if (_folder != null) AssetDatabase.DeleteAsset(_folder); }

        [Test]
        public void DirtyExportIsSavedCopiedAndConsumedWhileOriginalIdentityRemains()
        {
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(_source, binding, AnimationCurve.Linear(0, 2, 1, 4));
            EditorUtility.SetDirty(_source);
            Assert.That(EditorUtility.IsDirty(_source), Is.True);
            AnimationClipReplacementService.SaveReplacementSource(_source);
            Assert.That(EditorUtility.IsDirty(_source), Is.False);
            string guid = AssetDatabase.AssetPathToGUID(_folder + "/old.anim");
            byte[] meta = File.ReadAllBytes(_folder + "/old.anim.meta");
            var cleanup = AnimationClipExportCleanup.Capture(_source);
            AnimationClipReplacementService.CopyClipContent(_source, _target);
            cleanup.DeleteAfterReplacement();
            Assert.That(File.Exists(_folder + "/new.anim"), Is.False);
            Assert.That(File.Exists(_folder + "/new.anim.meta"), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(_folder + "/old.anim"), Is.EqualTo(guid));
            Assert.That(File.ReadAllBytes(_folder + "/old.anim.meta"), Is.EqualTo(meta));
            Assert.That(AnimationUtility.GetEditorCurve(_target, binding).Evaluate(1), Is.EqualTo(4));
        }

        [Test]
        public void ExportModifiedAfterCaptureIsKept()
        {
            var cleanup = AnimationClipExportCleanup.Capture(_source);
            _source.frameRate = 48;
            EditorUtility.SetDirty(_source);
            AssetDatabase.SaveAssetIfDirty(_source);
            Assert.Throws<InvalidOperationException>(() => cleanup.DeleteAfterReplacement());
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(_folder + "/new.anim"), Is.Not.Null);
        }

        [Test]
        public void ExportReferencedByAnotherAssetCannotBeConsumed()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(_folder + "/uses-export.controller");
            var state = controller.layers[0].stateMachine.AddState("Uses export");
            state.motion = _source;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            Assert.Throws<InvalidOperationException>(() => AnimationClipExportCleanup.Capture(_source));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(_folder + "/new.anim"), Is.Not.Null);
        }
    }
}
