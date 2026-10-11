using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace WGameFramework.UpmSampleSmoke
{
    public sealed class RuntimeHostSampleConsumerSmokeTests
    {
        [UnityTest]
        public IEnumerator ImportedSample_RunsAndCleansRuntimeHostLifecycle()
        {
            string[] sceneGuids = AssetDatabase.FindAssets(
                "RuntimeHostMinimalLifecycle t:Scene",
                new[] { "Assets/Samples" });
            Assert.That(sceneGuids, Has.Length.EqualTo(1), "The sample must be explicitly imported before this smoke test runs.");
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
            EditorSceneManager.OpenScene(scenePath);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
                yield return null;

            Type sampleType = Type.GetType(
                "MxFramework.Samples.RuntimeHostMinimalLifecycle.RuntimeHostMinimalLifecycleSample, "
                + "MxFramework.Samples.RuntimeHostMinimalLifecycle",
                throwOnError: true);
            UnityEngine.Object sample = UnityEngine.Object.FindAnyObjectByType(sampleType);
            Assert.That(sample, Is.Not.Null);
            FieldInfo status = sampleType.GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo host = sampleType.GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(status, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(status.GetValue(sample), Is.EqualTo("Passed"));
            Assert.That(host.GetValue(sample), Is.Null, "The sample must dispose and release its RuntimeHost after the lifecycle completes.");
            LogAssert.NoUnexpectedReceived();

            yield return new ExitPlayMode();
        }
    }
}
