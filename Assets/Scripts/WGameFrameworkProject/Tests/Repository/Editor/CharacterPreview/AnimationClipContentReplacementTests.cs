using System;
using System.IO;
using MxFramework.Project.Editor.AnimationDesigner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Editor.CharacterPreview
{
    public sealed class AnimationClipContentReplacementTests
    {
        [Test]
        public void CopyKeepsGuidFileIdNameAndReferencesWhileReplacingCurvesAndEvents()
        {
            string folder = "Assets/AnimationClipContentTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                var target = new AnimationClip { name = "Original" };
                var source = new AnimationClip { name = "Modified", frameRate = 48 };
                var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x");
                AnimationUtility.SetEditorCurve(target, binding, AnimationCurve.Linear(0, 0, 1, 1));
                AnimationUtility.SetEditorCurve(source, binding, AnimationCurve.Linear(0, 2, 2, 4));
                AnimationUtility.SetAnimationEvents(source, new[] { new AnimationEvent { time = 0.5f, functionName = "Example" } });
                AssetDatabase.CreateAsset(target, folder + "/old.anim");
                AssetDatabase.CreateAsset(source, folder + "/new.anim");
                AssetDatabase.SaveAssetIfDirty(target); AssetDatabase.SaveAssetIfDirty(source);
                string targetName = target.name;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long localId);
                byte[] meta = File.ReadAllBytes(folder + "/old.anim.meta");
                var reference = target;
                AnimationClipReplacementService.CopyClipContent(source, target);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string actualGuid, out long actualId);
                Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(actualId, Is.EqualTo(localId));
                Assert.That(target, Is.SameAs(reference)); Assert.That(target.name, Is.EqualTo(targetName));
                Assert.That(File.ReadAllBytes(folder + "/old.anim.meta"), Is.EqualTo(meta));
                Assert.That(AnimationUtility.GetEditorCurve(target, binding).Evaluate(2), Is.EqualTo(4));
                Assert.That(AnimationUtility.GetAnimationEvents(target)[0].functionName, Is.EqualTo("Example"));
                Assert.That(target.frameRate, Is.EqualTo(48));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }
    }
}
