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
            Assert.AreEqual(Shader.PropertyToID("_MxWetness"), MxRenderingShaderIds.MxWetness);
            Assert.AreEqual(Shader.PropertyToID("_MxRain"), MxRenderingShaderIds.MxRain);
            Assert.AreEqual(Shader.PropertyToID("_MxSnowCoverage"), MxRenderingShaderIds.MxSnowCoverage);
            Assert.AreEqual(Shader.PropertyToID("_MxPrimarySubjectWorldPos"), MxRenderingShaderIds.MxPrimarySubjectWorldPos);
            Assert.AreEqual(Shader.PropertyToID("_MxPrimarySubjectVelocity"), MxRenderingShaderIds.MxPrimarySubjectVelocity);
            Assert.AreEqual(Shader.PropertyToID("_MxLocalSubjectWorldPos"), MxRenderingShaderIds.MxLocalSubjectWorldPos);
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
        public void SetWeather_UpdatesSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();

            context.SetWeather(0.2f, 0.4f, 0.6f);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(0.2f, snapshot.Wetness);
            Assert.AreEqual(0.4f, snapshot.Rain);
            Assert.AreEqual(0.6f, snapshot.SnowCoverage);
            Assert.AreEqual(0.2f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxWetness));
            Assert.AreEqual(0.4f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxRain));
            Assert.AreEqual(0.6f, Shader.GetGlobalFloat(MxRenderingShaderIds.MxSnowCoverage));
        }

        [Test]
        public void SetPrimarySubjectPose_UpdatesSnapshotAndShaderGlobals()
        {
            var context = new GlobalFrameContext();
            var worldPosition = new Vector3(1f, 2f, 3f);
            var velocity = new Vector3(0.5f, -0.25f, 4f);

            context.SetPrimarySubjectPose(worldPosition, velocity);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(worldPosition, snapshot.PrimarySubjectWorldPos);
            Assert.AreEqual(velocity, snapshot.PrimarySubjectVelocity);
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectWorldPos));
            Assert.AreEqual(new Vector4(0.5f, -0.25f, 4f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxPrimarySubjectVelocity));
        }

        [Test]
        public void SetLocalSubjectPose_UpdatesSnapshotAndWorldPositionShaderGlobal()
        {
            var context = new GlobalFrameContext();
            var worldPosition = new Vector3(-2f, 5f, 8f);
            var velocity = new Vector3(3f, 0f, -1f);

            context.SetLocalSubjectPose(worldPosition, velocity);

            GlobalFrameSnapshot snapshot = context.Snapshot();
            Assert.AreEqual(worldPosition, snapshot.LocalSubjectWorldPos);
            Assert.AreEqual(velocity, snapshot.LocalSubjectVelocity);
            Assert.AreEqual(new Vector4(-2f, 5f, 8f, 0f), Shader.GetGlobalVector(MxRenderingShaderIds.MxLocalSubjectWorldPos));
        }

        [Test]
        public void MaterialCanConsumeGlobalWindDirection()
        {
            Shader shader = Shader.Find("Hidden/MxFramework/Tests/Rendering/WindDirectionGlobalConsumer");
            Assert.IsNotNull(shader, "Test shader must be imported before running Rendering material validation.");

            var context = new GlobalFrameContext();
            context.SetWind(new Vector3(1f, 0f, 0f), 1f, 0f);

            var previousActive = RenderTexture.active;
            var material = new Material(shader);
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                readback.Apply();

                Color pixel = readback.GetPixel(2, 2);
                Assert.Greater(pixel.r, 0.8f);
                Assert.Less(pixel.g, 0.1f);
                Assert.Less(pixel.b, 0.1f);
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(readback);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
            }
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

        [Test]
        public void NoEngineAsmdefs_DoNotReferenceRendering()
        {
            string[] noEngineAsmdefs =
            {
                "Assets/Scripts/MxFramework/Core/MxFramework.Core.asmdef",
                "Assets/Scripts/MxFramework/Config/MxFramework.Config.asmdef",
                "Assets/Scripts/MxFramework/Events/MxFramework.Events.asmdef",
                "Assets/Scripts/MxFramework/Attributes/MxFramework.Attributes.asmdef",
                "Assets/Scripts/MxFramework/Modifiers/MxFramework.Modifiers.asmdef",
                "Assets/Scripts/MxFramework/Buffs/MxFramework.Buffs.asmdef",
                "Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef",
                "Assets/Scripts/MxFramework/Combat/MxFramework.Combat.asmdef",
                "Assets/Scripts/MxFramework/Runtime/MxFramework.Runtime.asmdef",
                "Assets/Scripts/MxFramework/Resources/MxFramework.Resources.asmdef",
                "Assets/Scripts/MxFramework/AI/MxFramework.AI.asmdef"
            };

            foreach (string asmdefPath in noEngineAsmdefs)
            {
                string asmdef = File.ReadAllText(asmdefPath);
                Assert.IsFalse(asmdef.Contains("MxFramework.Rendering"), asmdefPath);
            }
        }
    }
}
