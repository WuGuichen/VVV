using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class RuntimeVerticalSliceSampleResourceTestTests
    {
        [Test]
        public void Run_CompletesSamplesResourceChainAndReleasesAllResources()
        {
            System.Func<string, UnityEngine.Object> previousLoader = RuntimeVerticalSliceSampleResourceTest.AssetPathLoader;
            RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>;
            var resourceTest = new RuntimeVerticalSliceSampleResourceTest();

            try
            {
                RuntimeVerticalSliceSampleResourceTestResult result = resourceTest.Run();

                Assert.IsTrue(result.Success, result.FailureMessage + "\n" + string.Join("\n", result.LogLines));
                Assert.AreEqual(5, result.DirectPrefabCount);
                Assert.AreEqual(7, result.DirectTextureCount);
                Assert.AreEqual(4, result.DirectAudioClipCount);
                Assert.AreEqual(5, result.InstantiatedPrefabCount);
                Assert.AreEqual(5, result.DestroyedPrefabCount);

                Assert.NotNull(result.AfterWarmupSnapshot);
                Assert.AreEqual(34, result.AfterWarmupSnapshot.LoadedCount);
                Assert.AreEqual(76, result.AfterWarmupSnapshot.TotalRefCount);

                Assert.NotNull(result.AfterDirectLoadSnapshot);
                Assert.AreEqual(34, result.AfterDirectLoadSnapshot.LoadedCount);
                Assert.AreEqual(92, result.AfterDirectLoadSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterDirectLoadSnapshot.FailedCount);

                Assert.NotNull(result.AfterDirectReleaseSnapshot);
                Assert.AreEqual(34, result.AfterDirectReleaseSnapshot.LoadedCount);
                Assert.AreEqual(76, result.AfterDirectReleaseSnapshot.TotalRefCount);

                Assert.NotNull(result.AfterFullReleaseSnapshot);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.LoadedCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.TotalRefCount);
                Assert.AreEqual(0, result.AfterFullReleaseSnapshot.FailedCount);

                Assert.AreEqual(34, result.ProviderLoadCount);
                Assert.AreEqual(34, result.ProviderReleaseCount);
                StringAssert.Contains("Samples warmup: package 34/34", string.Join("\n", result.LogLines));
                StringAssert.Contains("MxAnimation 18/18", string.Join("\n", result.LogLines));
                StringAssert.Contains("Samples direct: Katana=1, StatusAura prefabs=4", string.Join("\n", result.LogLines));
                StringAssert.Contains("fullRelease loaded=0 refs=0 failed=0", string.Join("\n", result.LogLines));
            }
            finally
            {
                resourceTest.Dispose();
                RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = previousLoader;
            }
        }

        [Test]
        public void RuntimeVerticalSliceRunner_WarmupRuntimeResources_RunsSamplesChainWithoutManualWiring()
        {
            System.Func<string, UnityEngine.Object> previousLoader = RuntimeVerticalSliceSampleResourceTest.AssetPathLoader;
            RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>;
            GameObject go = new GameObject("RuntimeVerticalSliceRunnerResourceTest");

            try
            {
                RuntimeVerticalSliceRunner runner = go.AddComponent<RuntimeVerticalSliceRunner>();

                InvokePrivate(runner, "WarmupRuntimeResources");

                string summary = (string)GetPrivateField(runner, "_resourceWarmupSummary");
                IReadOnlyList<string> lines = (IReadOnlyList<string>)GetPrivateField(runner, "_resourceTestLines");
                string log = string.Join("\n", lines);

                StringAssert.Contains("Samples resources ok", summary);
                StringAssert.Contains("Samples warmup: package 34/34", log);
                StringAssert.Contains("MxAnimation 18/18", log);
                StringAssert.Contains("StartScreen 7/7", log);
                StringAssert.Contains("Combat 9/9", log);
                StringAssert.Contains("StatusEffects 4/4", log);
                StringAssert.Contains("MagicEffects 4/4", log);
                StringAssert.Contains("Samples direct: Katana=1, StatusAura prefabs=4", log);
                StringAssert.Contains("fullRelease loaded=0 refs=0 failed=0", log);

                InvokePrivate(runner, "ReleaseRuntimeResources");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = previousLoader;
            }
        }

        private static void InvokePrivate(RuntimeVerticalSliceRunner runner, string methodName)
        {
            MethodInfo method = typeof(RuntimeVerticalSliceRunner).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(runner, Array.Empty<object>());
        }

        private static object GetPrivateField(RuntimeVerticalSliceRunner runner, string fieldName)
        {
            FieldInfo field = typeof(RuntimeVerticalSliceRunner).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(runner);
        }
    }
}
