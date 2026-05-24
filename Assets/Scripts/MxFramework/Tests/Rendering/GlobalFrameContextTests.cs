using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public class GlobalFrameContextTests
    {
        [Test]
        public void ShaderIds_RegisterExpectedMxGlobals()
        {
            Assert.AreEqual(Shader.PropertyToID("_MxTime"), MxRenderingShaderIds.MxTime);
            Assert.AreEqual(Shader.PropertyToID("_MxWindDirection"), MxRenderingShaderIds.MxWindDirection);
        }

        [Test]
        public void GlobalAndCameraShaderOwnership_DoNotOverlap()
        {
            var globalIds = new HashSet<int>(MxRenderingShaderIds.GlobalFramePropertyIds);

            foreach (int cameraId in MxRenderingShaderIds.CameraFramePropertyIds)
                Assert.IsFalse(globalIds.Contains(cameraId), "Camera property id must not be owned by GlobalFrameContext.");
        }

        [Test]
        public void SetTimeAndWind_UpdateSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();

            context.SetTime(1.25f, 9.5f, 0.016f);
            context.SetWind(new Vector3(0.25f, 0f, 0.75f), 2f, 0.5f);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(1.25f, snapshot.Time);
            Assert.AreEqual(9.5f, snapshot.GameTime);
            Assert.AreEqual(0.016f, snapshot.DeltaTime);
            Assert.AreEqual(new Vector3(0.25f, 0f, 0.75f), snapshot.WindDirection);
            Assert.AreEqual(2f, snapshot.WindStrength);
            Assert.AreEqual(0.5f, snapshot.WindTurbulence);
            Assert.AreEqual(new Vector4(1.25f, 9.5f, 0.016f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxTime));
            Assert.AreEqual(new Vector4(0.25f, 0f, 0.75f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxWindDirection));
        }

        [Test]
        public void GlobalFrameDebugSource_ExposesGlobalsSection()
        {
            var context = new GlobalFrameContext();
            context.SetTime(3f, 4f, 0.02f);
            context.SetWind(Vector3.right, 1.5f, 0.25f);
            var source = new GlobalFrameContextDebugSource(context);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Rendering", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.AreEqual(1, snapshot.Sections.Count);
            Assert.AreEqual(RenderingDebugSectionNames.Globals, snapshot.Sections[0].Title);
            StringAssert.Contains("time: 3", snapshot.Sections[0].Body);
            StringAssert.Contains("windStrength: 1.5", snapshot.Sections[0].Body);
        }

        [Test]
        public void RenderingAsmdef_HasOnlyAllowedRuntimeReferences()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");

            StringAssert.Contains("\"MxFramework.Core\"", asmdef);
            StringAssert.Contains("\"MxFramework.Diagnostics\"", asmdef);
            StringAssert.Contains("\"Unity.RenderPipelines.Universal.Runtime\"", asmdef);
            Assert.IsFalse(asmdef.Contains("MxFramework.DebugUI"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Gameplay"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Combat"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Buffs"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Character"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Animation"));
        }
    }
}
