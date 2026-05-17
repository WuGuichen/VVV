using System;
using System.Collections.Generic;
using MxFramework.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class RuntimeVerticalSliceDefaultResourceBindingTests
    {
        [Test]
        public void RuntimeVerticalSliceRunner_DefaultResourceBinding_ExposesWarmupAndReleaseDiagnostics()
        {
            Func<string, UnityEngine.Object> previousLoader = RuntimeVerticalSliceSampleResourceTest.AssetPathLoader;
            RuntimeVerticalSliceSampleResourceTest.AssetPathLoader = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>;
            GameObject go = new GameObject("RuntimeVerticalSliceDefaultResourceBindingTest");

            try
            {
                RuntimeVerticalSliceRunner runner = go.AddComponent<RuntimeVerticalSliceRunner>();
                InvokePrivate(runner, "WarmupRuntimeResources");

                IReadOnlyList<string> lines = runner.ResourceBindingLogLines;
                string log = string.Join("\n", lines);

                StringAssert.Contains("Samples resources ok", runner.ResourceWarmupSummary);
                StringAssert.Contains("Player resources ok", runner.ResourceWarmupSummary);
                StringAssert.Contains("Samples warmup: package 34/34", log);
                StringAssert.Contains("Player resources warmup: warmup.demo.start_screen 1/1", log);
                StringAssert.Contains("Player resources direct: ui.start_screen.button.normal Texture2D", log);
                StringAssert.Contains("fullRelease loaded=0 refs=0 failed=0", log);
                StringAssert.Contains("bundles=0", log);

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
            System.Reflection.MethodInfo method = typeof(RuntimeVerticalSliceRunner).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(runner, Array.Empty<object>());
        }
    }
}
