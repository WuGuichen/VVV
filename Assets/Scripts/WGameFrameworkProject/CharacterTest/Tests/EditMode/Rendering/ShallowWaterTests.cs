using MxFramework.Game.CharacterTest;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;
using MxFramework.Settings;

namespace MxFramework.Tests.Rendering
{
    public sealed class ShallowWaterTests
    {
        [Test] public void PlayerWaterDraftApplySaveCancelAndReloadPreserveAuthoredAsset()
        {
            var authored = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
            authored.QualityTier = CharacterTestWaterQuality.Performance;
            var configuration = new CharacterTestShallowWaterConfiguration { Settings = authored };
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "water-settings-" + System.Guid.NewGuid().ToString("N") + ".json");
            var service = new MxSettingsService(new MxSettingsJsonFileStore(path));
            var runtime = new CharacterTestSettingsRuntime(path, service, null, null);
            try
            {
                runtime.AttachWaterSettings(() => configuration);
                runtime.SettingsUi.SetWaterQuality(MxGraphicsFeatureQuality.High);
                runtime.SettingsUi.SetRenderScale(.8f);
                runtime.SettingsUi.SetDirectionalShadowQuality(MxGraphicsFeatureQuality.Low);
                Assert.AreEqual(MxGraphicsFeatureQuality.High, runtime.CurrentProfile.Graphics.WaterQuality);
                Assert.IsNull(configuration.RuntimeSettingsOverride, "Draft changes must not affect rendering.");
                Assert.IsTrue(runtime.ApplyCurrentProfile().Success);
                Assert.IsTrue(runtime.SaveCurrentProfile().Success);
                Assert.AreEqual(CharacterTestWaterQuality.Quality, configuration.RuntimeSettingsOverride.QualityTier);
                Assert.AreNotSame(authored, configuration.RuntimeSettingsOverride);
                Assert.AreEqual(CharacterTestWaterQuality.Performance, authored.QualityTier);
                runtime.SettingsUi.SetWaterQuality(MxGraphicsFeatureQuality.Low);
                Assert.IsTrue(runtime.ReloadProfile().Success);
                Assert.AreEqual(MxGraphicsFeatureQuality.High, runtime.CurrentProfile.Graphics.WaterQuality);
                Assert.AreEqual(CharacterTestWaterQuality.Quality, configuration.RuntimeSettingsOverride.QualityTier);
                var reloaded = MxSettingsProfileJson.LoadFromJson(System.IO.File.ReadAllText(path));
                Assert.AreEqual(MxGraphicsFeatureQuality.High, reloaded.Profile.Graphics.WaterQuality);
                runtime.SettingsUi.SetWaterQuality(MxGraphicsFeatureQuality.Default);
                Assert.IsTrue(runtime.ApplyCurrentProfile().Success);
                Assert.IsNull(configuration.RuntimeSettingsOverride);
            }
            finally
            {
                runtime.Dispose(); Object.DestroyImmediate(authored);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test] public void OldSettingsJsonDefaultsWaterAndDisposalRemovesRuntimeCopy()
        {
            var loaded = MxSettingsProfileJson.LoadFromJson("{\"format\":\"mx.settings.v1\",\"profileId\":\"legacy-water\",\"graphics\":{\"renderScale\":0.75}}");
            Assert.IsTrue(loaded.Success); Assert.AreEqual(MxGraphicsFeatureQuality.Default, loaded.Profile.Graphics.WaterQuality);
            var authored = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
            var configuration = new CharacterTestShallowWaterConfiguration { Settings = authored };
            var applier = new CharacterTestWaterSettingsApplier(() => configuration);
            try
            {
                applier.Apply(loaded.Profile.WithGraphics(new MxGraphicsSettings(waterQuality: MxGraphicsFeatureQuality.Medium)));
                Assert.AreEqual(CharacterTestWaterQuality.Balanced, configuration.RuntimeSettingsOverride.QualityTier);
                applier.Dispose(); Assert.IsNull(configuration.RuntimeSettingsOverride);
            }
            finally { applier.Dispose(); Object.DestroyImmediate(authored); }
        }
        [Test] public void QualitySwitchPreservesCustomValuesAndBoundsWork()
        {
            var settings = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
            try
            {
                settings.ReflectionScale = .7f; settings.ReflectionMaximumEdge = 768;
                settings.ReflectionFramesPerSecond = 24; settings.MaximumRipples = 5;
                settings.QualityTier = CharacterTestWaterQuality.Performance;
                Assert.IsFalse(settings.UsePlanarReflection); Assert.IsFalse(settings.UseRefraction);
                Assert.IsTrue(settings.UseSspr); Assert.AreEqual(256, settings.EffectiveSsprEdge);
                Assert.AreEqual(5, settings.EffectiveRippleLimit);
                settings.QualityTier = CharacterTestWaterQuality.Balanced;
                Assert.IsFalse(settings.UsePlanarReflection); Assert.IsTrue(settings.UseSspr);
                Assert.AreEqual(512, settings.EffectiveSsprEdge); Assert.AreEqual(0, settings.EffectiveReflectionRate);
                Assert.IsFalse(settings.UseReflectionShadows); Assert.IsFalse(settings.UseRefraction);
                settings.QualityTier = CharacterTestWaterQuality.Quality;
                Assert.IsTrue(settings.UsePlanarReflection); Assert.IsFalse(settings.UseSspr);
                Assert.IsTrue(settings.UseRefraction); Assert.AreEqual(0, settings.EffectiveReflectionRate);
                settings.QualityTier = CharacterTestWaterQuality.Custom;
                Assert.AreEqual(.7f, settings.EffectiveReflectionScale); Assert.AreEqual(768, settings.EffectiveReflectionEdge);
                Assert.AreEqual(24, settings.EffectiveReflectionRate); Assert.AreEqual(5, settings.EffectiveRippleLimit);
            }
            finally { Object.DestroyImmediate(settings); }
        }
        [Test] public void RippleQualityIsIndependentAndDraftCopiesPreserveBothPreferences()
        {
            var authored = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
            authored.QualityTier = CharacterTestWaterQuality.Performance;
            var config = new CharacterTestShallowWaterConfiguration { Settings = authored };
            var service = new MxSettingsService(null);
            using var runtime = new CharacterTestSettingsRuntime("", service, null, null);
            try
            {
                runtime.AttachWaterSettings(() => config);
                runtime.SettingsUi.SetWaterQuality(MxGraphicsFeatureQuality.High);
                runtime.SettingsUi.SetRippleQuality(MxGraphicsFeatureQuality.Disabled);
                runtime.SettingsUi.SetRenderScale(.75f);
                runtime.SettingsUi.SetWaterQuality(MxGraphicsFeatureQuality.Low);
                Assert.AreEqual(MxGraphicsFeatureQuality.Disabled, runtime.CurrentProfile.Graphics.RippleQuality);
                Assert.IsNull(config.RuntimeSettingsOverride);
                runtime.ApplyCurrentProfile();
                Assert.IsTrue(config.RuntimeSettingsOverride.UseSspr);
                Assert.AreEqual(0, config.RuntimeSettingsOverride.EffectiveRippleLimit);
                runtime.SettingsUi.SetRippleQuality(MxGraphicsFeatureQuality.High); runtime.ApplyCurrentProfile();
                Assert.IsTrue(config.RuntimeSettingsOverride.UseSspr);
                Assert.AreEqual(1024, config.RuntimeSettingsOverride.EffectiveRippleResolution);
                Assert.AreEqual(32, config.RuntimeSettingsOverride.EffectiveRippleLimit);
                var loaded = MxSettingsProfileJson.LoadFromJson(MxSettingsProfileJson.SaveToJson(runtime.CurrentProfile));
                Assert.AreEqual(MxGraphicsFeatureQuality.High, loaded.Profile.Graphics.RippleQuality);
                Assert.AreEqual(CharacterTestRippleQuality.Low, authored.RippleQuality);
            }
            finally { runtime.Dispose(); Object.DestroyImmediate(authored); }
        }
        [Test] public void RippleMergingAndOverflowProtectFootstepAndLandingFeedback()
        {
            var waves = new CharacterTestShallowWaterRipples();
            waves.Add(Vector2.zero, 0, .02f, priority: 1, merge: true);
            waves.Add(new Vector2(.1f, 0), .04f, .04f, priority: 1, merge: true);
            Assert.AreEqual(1, waves.Count); Assert.AreEqual(1, waves.Merged); Assert.AreEqual(.04f, waves.Sources[0].w);
            for (int i = 1; i < 32; i++) waves.Add(new Vector2(i, 0), 0, .02f, priority: 1);
            waves.Add(new Vector2(99, 0), 1, .02f, priority: 0);
            Assert.AreEqual(0, waves.Replaced, "Movement must not evict contact feedback.");
            waves.Add(new Vector2(100, 0), 1, .06f, priority: 2);
            Assert.AreEqual(1, waves.Replaced);
            Assert.IsTrue(System.Array.Exists(waves.Sources, x => x.x == 100));
        }
        [Test] public void RippleFieldCadenceHandlesEventsPauseAndRewind()
        {
            Assert.IsFalse(CharacterTestShallowWaterSession.RippleFieldDue(true, 1.01f, 1, 30, false));
            Assert.IsTrue(CharacterTestShallowWaterSession.RippleFieldDue(true, 1.04f, 1, 30, false));
            Assert.IsTrue(CharacterTestShallowWaterSession.RippleFieldDue(true, 1.01f, 1, 30, true));
            Assert.IsFalse(CharacterTestShallowWaterSession.RippleFieldDue(true, 1, 1, 30, false));
            Assert.IsTrue(CharacterTestShallowWaterSession.RippleFieldDue(true, .9f, 1, 30, false));
            var center = CharacterTestShallowWaterSession.SnapRippleCenter(new Vector2(1.001f, -2.003f), 32, 512);
            Assert.AreEqual(new Vector2(1, -2), center);
        }
        [Test] public void RippleFieldRejectsDistantPacketsButKeepsIncomingAnnuli()
        {
            var waves = new CharacterTestShallowWaterRipples();
            waves.Add(new Vector2(20, 0), 0, .03f, .25f);
            Assert.IsFalse(waves.IntersectsField(Vector2.zero, 24, 0, 4));
            Assert.IsTrue(waves.IntersectsField(Vector2.zero, 24, 2, 4));
            waves.Clear(); Assert.IsFalse(waves.IntersectsField(Vector2.zero, 24, 0, 4));
        }
        [Test] public void RippleFieldGpuMatchesAnalyticWavesAndClearsExpiredPixels()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback
                || !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore))
                Assert.Ignore("GPU field test requires compute, half-float UAV and readback.");
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterRipples.compute");
            Assert.IsNotNull(shader);
            var waves = new CharacterTestShallowWaterRipples();
            waves.Add(new Vector2(-.4f, .2f), 0, .027f, .35f);
            waves.Add(new Vector2(.6f, -.3f), .2f, .045f, .25f);
            var rt = new RenderTexture(new RenderTextureDescriptor(64, 64)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, msaaSamples = 1 });
            try
            {
                Assert.IsTrue(rt.Create()); int kernel = shader.FindKernel("BuildRippleField");
                shader.SetInt("_RippleResolution", 64); shader.SetVector("_WgWaterRippleRect", new Vector4(-2, -2, .25f, 16));
                shader.SetTexture(kernel, "_RippleOutput", rt);
                for (int frame = 0; frame < 2; frame++)
                {
                    if (frame == 1) waves.Expire(3, 2);
                    waves.BuildDrawData(.5f, 2, 1.6f, Matrix4x4.identity, new Vector2(4, 4));
                    shader.SetVectorArray("_WgWaterRipples", waves.DrawSources); shader.SetVectorArray("_WgWaterRippleShape", waves.DrawShapes);
                    shader.SetVectorArray("_WgWaterRippleSheen", waves.DrawSheen); shader.SetVectorArray("_WgWaterRippleTiles", waves.TileMasks);
                    shader.Dispatch(kernel, 8, 8, 1);
                    var read = UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBAFloat); read.WaitForCompletion();
                    Assert.IsFalse(read.hasError); var pixels = read.GetData<Color>();
                    for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        var point = new Vector2(-2 + (x + .5f) / 16, -2 + (y + .5f) / 16);
                        Vector2 slope = Vector2.zero; float crest = 0;
                        for (int n = 0; n < waves.Count; n++)
                        {
                            var r = waves.Sources[n]; var shape = waves.Shapes[n];
                            float age = .5f - r.z, distance = (point - new Vector2(r.x, r.y)).magnitude;
                            float wave = distance - age * 1.6f;
                            if (Mathf.Abs(wave) > 4 * shape.x) continue;
                            float envelope = Mathf.Exp(-wave * wave / (shape.x * shape.x)) * Mathf.Pow(1 - age / 2, 2);
                            float derivative = -Mathf.Sin(wave * shape.y) * shape.y - 2 * wave / (shape.x * shape.x) * Mathf.Cos(wave * shape.y);
                            slope += (point - new Vector2(r.x, r.y)) / Mathf.Max(.05f, distance) * derivative * r.w * envelope;
                            crest += Mathf.Cos(wave * shape.y) * envelope * Mathf.Clamp01(r.w / .02f);
                        }
                        Color pixel = pixels[y * 64 + x];
                        Assert.AreEqual(slope.x, pixel.r, .001f); Assert.AreEqual(slope.y, pixel.g, .001f); Assert.AreEqual(crest, pixel.b, .001f);
                    }
                }
            }
            finally { Object.DestroyImmediate(rt); }
        }
        [Test] public void SsprInputAndOutputStayInsidePackedIndexBudget()
        {
            Assert.AreEqual(new Vector2Int(256, 144), CharacterTestShallowWaterSession.SsprSize(1920, 1080, .25f, 256));
            Assert.AreEqual(new Vector2Int(512, 288), CharacterTestShallowWaterSession.SsprSize(3840, 2160, .5f, 512));
            Assert.AreEqual(new Vector2Int(1024, 1024), CharacterTestShallowWaterSession.SsprSize(8192, 8192, 1, 2048));
            Assert.AreEqual(18, CharacterTestShallowWaterSession.SsprSourceIndexBits(512, 512));
            Assert.AreEqual(20, CharacterTestShallowWaterSession.SsprSourceIndexBits(1024, 576));
            Assert.AreEqual(Vector2Int.one, CharacterTestShallowWaterSession.SsprSize(0, 0, .25f, 256));
        }
        [Test] public void SsprProjectionWinnerIsNearestWithStableTieAndReservedEmptyKey()
        {
            uint near = CharacterTestShallowWaterSession.SsprProjectionKey(.25f, 262143);
            uint far = CharacterTestShallowWaterSession.SsprProjectionKey(.5f, 0);
            Assert.Less(near, far);
            Assert.Less(CharacterTestShallowWaterSession.SsprProjectionKey(.5f, 1), CharacterTestShallowWaterSession.SsprProjectionKey(.5f, 2));
            uint last = CharacterTestShallowWaterSession.SsprProjectionKey(1, 262143);
            Assert.Less(last, uint.MaxValue); Assert.AreEqual(262143u, last & 0x3ffffu);
            uint highLast = CharacterTestShallowWaterSession.SsprProjectionKey(1, 1048575, 20);
            Assert.Less(highLast, uint.MaxValue); Assert.AreEqual(1048575u, highLast & 0xfffffu);
            Assert.AreEqual(4094u, highLast >> 20);
        }
        [Test] public void HighSsprPreferenceRoundTripsWithoutChangingLegacyMirrorPreference()
        {
            var settings = ScriptableObject.CreateInstance<CharacterTestShallowWaterSettings>();
            var config = new CharacterTestShallowWaterConfiguration { Settings = settings };
            using var applier = new CharacterTestWaterSettingsApplier(() => config);
            try
            {
                var old = MxSettingsProfileJson.LoadFromJson("{\"format\":\"mx.settings.v1\",\"profileId\":\"legacy-water\",\"graphics\":{\"waterQuality\":4}}");
                Assert.IsFalse(old.HasIssues);
                applier.Apply(old.Profile); Assert.IsTrue(config.RuntimeSettingsOverride.UsePlanarReflection);
                var high = new MxSettingsProfile(graphics: new MxGraphicsSettings(waterQuality: MxGraphicsFeatureQuality.Ultra));
                var loaded = MxSettingsProfileJson.LoadFromJson(MxSettingsProfileJson.SaveToJson(high));
                Assert.AreEqual(MxGraphicsFeatureQuality.Ultra, loaded.Profile.Graphics.WaterQuality);
                applier.Apply(loaded.Profile); Assert.IsTrue(config.RuntimeSettingsOverride.UseSspr);
                Assert.IsFalse(config.RuntimeSettingsOverride.UsePlanarReflection);
                Assert.AreEqual(1024, config.RuntimeSettingsOverride.EffectiveSsprEdge);
                Assert.AreEqual(.3f, config.RuntimeSettingsOverride.EffectiveSsprFilterStrength);
                Assert.AreEqual(MxGraphicsFeatureQuality.Default, new MxGraphicsSettings(rippleQuality: MxGraphicsFeatureQuality.Ultra).RippleQuality);
                settings.QualityTier = CharacterTestWaterQuality.Balanced;
                Assert.AreEqual(.55f, settings.EffectiveSsprFilterStrength);
            }
            finally { applier.Dispose(); Object.DestroyImmediate(settings); }
        }
        [Test] public void SsprSoftResolveFiltersCoverageAndColorWithoutCrossingDepthLayers()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback || !SystemInfo.usesReversedZBuffer
                || !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore)
                || !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore))
                Assert.Ignore("GPU fixture requires compute, readback, reversed depth and UAV formats.");
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterSSPR.compute");
            var depth = new Texture2D(8, 8, TextureFormat.RFloat, false, true);
            var color = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
            var keys = new RenderTexture(new RenderTextureDescriptor(8, 8)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite = true, msaaSamples = 1 });
            var result = new RenderTexture(new RenderTextureDescriptor(8, 8)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, msaaSamples = 1 });
            try
            {
                Assert.IsTrue(keys.Create()); Assert.IsTrue(result.Create());
                var projection = Matrix4x4.identity; projection.m22 = -1;
                shader.SetVector("_SsprSize", new Vector4(8, 8, .125f, .125f));
                shader.SetInt("_SsprIndexBits", 18); shader.SetFloat("_SsprFilterStrength", 1);
                shader.SetVector("_SsprWater", new Vector4(0, 10, 2, 1));
                shader.SetVector("_SsprBounds", new Vector4(2, 2, 0, 0));
                shader.SetMatrix("_SsprView", Matrix4x4.identity); shader.SetMatrix("_SsprVP", projection);
                shader.SetMatrix("_SsprInvVP", projection); shader.SetMatrix("_SsprWorldToWater", Matrix4x4.identity);
                int clear = shader.FindKernel("Clear"), project = shader.FindKernel("Project"), resolve = shader.FindKernel("ResolveSoft");
                shader.SetTexture(clear, "_SsprKeyWrite", keys); shader.SetTexture(project, "_SsprKeyWrite", keys);
                shader.SetTexture(project, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprKeyRead", keys);
                shader.SetTexture(resolve, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprColor", color);
                shader.SetTexture(resolve, "_SsprOutput", result);
                var depths = new float[64]; var colors = new Color[64];
                int row = SystemInfo.graphicsUVStartsAtTop ? 5 : 2;
                for (int scenario = 0; scenario < 4; scenario++)
                {
                    for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    {
                        bool source = SystemInfo.graphicsUVStartsAtTop ? y < 4 : y >= 4;
                        depths[y * 8 + x] = scenario == 1 && source ? (x < 4 ? .25f : .9f) : .5f;
                        if (source && (scenario == 3 || (scenario == 2 && x >= 4))) depths[y * 8 + x] = 0;
                        colors[y * 8 + x] = scenario == 0 ? (x % 2 == 0 ? Color.black : Color.white) : x < 4 ? Color.red : Color.blue;
                    }
                    depth.SetPixelData(depths, 0); depth.Apply(); color.SetPixels(colors); color.Apply();
                    shader.Dispatch(clear, 1, 1, 1); shader.Dispatch(project, 1, 1, 1); shader.Dispatch(resolve, 1, 1, 1);
                    var read = UnityEngine.Rendering.AsyncGPUReadback.Request(result, 0, TextureFormat.RGBAFloat); read.WaitForCompletion();
                    Assert.IsFalse(read.hasError); var pixels = read.GetData<Color>(); Color edge = pixels[row * 8 + 3];
                    if (scenario == 0)
                    {
                        Assert.AreEqual(.5f, edge.r, .001f); Assert.AreEqual(1, edge.a, .001f);
                        foreach (int radius in new[] { 1, 2 }) foreach (float strength in new[] { .55f, .3f })
                        {
                            shader.SetVector("_SsprWater", new Vector4(0, 10, 2, radius));
                            shader.SetFloat("_SsprFilterStrength", strength); shader.Dispatch(resolve, 1, 1, 1);
                            var detailRead = UnityEngine.Rendering.AsyncGPUReadback.Request(result, 0, TextureFormat.RGBAFloat); detailRead.WaitForCompletion();
                            Assert.IsFalse(detailRead.hasError); var detail = detailRead.GetData<Color>()[row * 8 + 3];
                            float coverage = radius == 1 ? 1 : 8f / 9;
                            float filteredLoss = radius == 1 ? .5f : 4f / 9;
                            Assert.AreEqual((1 - filteredLoss * strength) * coverage, detail.r, .001f);
                            Assert.AreEqual(coverage, detail.a, .001f);
                        }
                        shader.SetFloat("_SsprFilterStrength", 1); shader.SetVector("_SsprWater", new Vector4(0, 10, 2, 1));
                    }
                    if (scenario == 1) { Assert.AreEqual(1, edge.r, .001f); Assert.AreEqual(0, edge.b, .001f); Assert.AreEqual(1, edge.a, .001f); }
                    if (scenario == 2)
                    {
                        Assert.AreEqual(.75f, edge.a, .001f); Assert.AreEqual(edge.a, edge.r, .001f);
                        Color hole = pixels[row * 8 + 4]; Assert.AreEqual(.25f, hole.a, .001f); Assert.AreEqual(hole.a, hole.r, .001f);
                        Assert.AreEqual(Color.clear, pixels[row * 8 + 5]);
                    }
                    if (scenario == 3) foreach (Color pixel in pixels) Assert.AreEqual(Color.clear, pixel);
                }
            }
            finally { Object.DestroyImmediate(keys); Object.DestroyImmediate(result); Object.DestroyImmediate(depth); Object.DestroyImmediate(color); }
        }
        [Test] public void SsprHighGpuAddressesSourcePixelsBeyondTheOldEighteenBitLimit()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback || !SystemInfo.usesReversedZBuffer
                || !SystemInfo.graphicsUVStartsAtTop) Assert.Ignore("High-resolution fixture requires the tested reversed-Z GPU convention.");
            const int size = 1024, x = 512, sourceY = 400, targetY = size - 1 - sourceY;
            int sourceIndex = sourceY * size + x, targetIndex = targetY * size + x;
            var depth = new Texture2D(size, size, TextureFormat.RFloat, false, true);
            var color = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var keys = new RenderTexture(new RenderTextureDescriptor(size, size)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite = true, msaaSamples = 1 });
            var result = new RenderTexture(new RenderTextureDescriptor(size, size)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, msaaSamples = 1 });
            try
            {
                Assert.IsTrue(keys.Create()); Assert.IsTrue(result.Create());
                var depths = new float[size * size]; depths[sourceIndex] = depths[targetIndex] = .5f;
                var colors = new Color32[size * size]; colors[sourceIndex] = new Color32(0, 255, 0, 255);
                depth.SetPixelData(depths, 0); depth.Apply(); color.SetPixelData(colors, 0); color.Apply();
                var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterSSPR.compute");
                var projection = Matrix4x4.identity; projection.m22 = -1;
                shader.SetVector("_SsprSize", new Vector4(size, size, 1f / size, 1f / size)); shader.SetInt("_SsprIndexBits", 20);
                shader.SetVector("_SsprWater", new Vector4(0, 10, 2, 0)); shader.SetVector("_SsprBounds", new Vector4(2, 2, 0, 0));
                shader.SetMatrix("_SsprView", Matrix4x4.identity); shader.SetMatrix("_SsprVP", projection);
                shader.SetMatrix("_SsprInvVP", projection); shader.SetMatrix("_SsprWorldToWater", Matrix4x4.identity);
                int clear = shader.FindKernel("Clear"), project = shader.FindKernel("Project"), resolve = shader.FindKernel("Resolve");
                shader.SetTexture(clear, "_SsprKeyWrite", keys); shader.SetTexture(project, "_SsprKeyWrite", keys);
                shader.SetTexture(project, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprKeyRead", keys);
                shader.SetTexture(resolve, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprColor", color); shader.SetTexture(resolve, "_SsprOutput", result);
                shader.Dispatch(clear, 128, 128, 1); shader.Dispatch(project, 128, 128, 1); shader.Dispatch(resolve, 128, 128, 1);
                var keyRead = UnityEngine.Rendering.AsyncGPUReadback.Request(keys, 0); keyRead.WaitForCompletion();
                var colorRead = UnityEngine.Rendering.AsyncGPUReadback.Request(result, 0, TextureFormat.RGBAFloat); colorRead.WaitForCompletion();
                Assert.IsFalse(keyRead.hasError); Assert.IsFalse(colorRead.hasError);
                Assert.Greater(sourceIndex, 262143);
                Assert.AreEqual((uint)sourceIndex, keyRead.GetData<uint>()[targetIndex] & 0xfffffu);
                Assert.AreEqual(Color.green, colorRead.GetData<Color>()[targetIndex]);
            }
            finally { Object.DestroyImmediate(keys); Object.DestroyImmediate(result); Object.DestroyImmediate(depth); Object.DestroyImmediate(color); }
        }
        [TestCase(18)] [TestCase(20)] public void SsprGpuProjectsKnownPixelsAndClearsPreviousFrame(int indexBits)
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback || !SystemInfo.usesReversedZBuffer
                || !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore)
                || !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.LoadStore))
                Assert.Ignore("GPU fixture requires compute, readback, reversed depth and supported UAV formats.");
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Art/WGame/Shaders/Environment/ShallowWaterSSPR.compute");
            Assert.IsNotNull(shader);
            var depth = new Texture2D(8, 8, TextureFormat.RFloat, false, true);
            var color = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
            var keys = new RenderTexture(new RenderTextureDescriptor(8, 8)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite = true, msaaSamples = 1 });
            var result = new RenderTexture(new RenderTextureDescriptor(8, 8)
            { graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, msaaSamples = 1 });
            try
            {
                Assert.IsTrue(keys.Create()); Assert.IsTrue(result.Create());
                var depths = new float[64]; var colors = new Color[64];
                for (int i = 0; i < 64; i++) { depths[i] = .5f; colors[i] = new Color((i % 8) / 8f, (i / 8) / 8f, 1, 1); }
                depth.SetPixelData(depths, 0); depth.Apply(); color.SetPixels(colors); color.Apply();
                var projection = Matrix4x4.identity; projection.m22 = -1;
                shader.SetVector("_SsprSize", new Vector4(8, 8, .125f, .125f));
                shader.SetInt("_SsprIndexBits", indexBits);
                shader.SetVector("_SsprWater", new Vector4(0, 10, 2, 0));
                shader.SetVector("_SsprBounds", new Vector4(2, 2, 0, 0));
                shader.SetMatrix("_SsprView", Matrix4x4.identity); shader.SetMatrix("_SsprVP", projection);
                shader.SetMatrix("_SsprInvVP", projection); shader.SetMatrix("_SsprWorldToWater", Matrix4x4.identity);
                int clear = shader.FindKernel("Clear"), project = shader.FindKernel("Project"), resolve = shader.FindKernel("Resolve");
                shader.SetTexture(clear, "_SsprKeyWrite", keys); shader.SetTexture(project, "_SsprKeyWrite", keys);
                shader.SetTexture(project, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprKeyRead", keys);
                shader.SetTexture(resolve, "_SsprDepth", depth); shader.SetTexture(resolve, "_SsprColor", color);
                shader.SetTexture(resolve, "_SsprOutput", result);
                for (int frame = 0; frame < 2; frame++)
                {
                    if (frame == 1) { System.Array.Clear(depths, 0, depths.Length); depth.SetPixelData(depths, 0); depth.Apply(); }
                    shader.Dispatch(clear, 1, 1, 1); shader.Dispatch(project, 1, 1, 1); shader.Dispatch(resolve, 1, 1, 1);
                    var keyRead = UnityEngine.Rendering.AsyncGPUReadback.Request(keys, 0); keyRead.WaitForCompletion();
                    var colorRead = UnityEngine.Rendering.AsyncGPUReadback.Request(result, 0, TextureFormat.RGBAFloat); colorRead.WaitForCompletion();
                    Assert.IsFalse(keyRead.hasError); Assert.IsFalse(colorRead.hasError);
                    var packed = keyRead.GetData<uint>(); var pixels = colorRead.GetData<Color>();
                    for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    {
                        int i = y * 8 + x;
                        bool receiver = frame == 0 && (SystemInfo.graphicsUVStartsAtTop ? y >= 4 : y < 4);
                        if (receiver)
                        {
                            uint source = (uint)((7 - y) * 8 + x);
                            Assert.AreEqual(CharacterTestShallowWaterSession.SsprProjectionKey(.05f, source, indexBits), packed[i]);
                            Assert.AreEqual(1, pixels[i].a, .001f);
                            Assert.AreEqual(colors[source].r, pixels[i].r, .001f); Assert.AreEqual(colors[source].g, pixels[i].g, .001f);
                        }
                        else { Assert.AreEqual(uint.MaxValue, packed[i]); Assert.AreEqual(0, pixels[i].a); }
                    }
                }
            }
            finally { Object.DestroyImmediate(keys); Object.DestroyImmediate(result); Object.DestroyImmediate(depth); Object.DestroyImmediate(color); }
        }
        [Test] public void ReflectionCadenceUsesCompletedCaptureAndInvalidatesOnCameraCut()
        {
            var go = new GameObject("Water cadence camera");
            try
            {
                var camera = go.AddComponent<UnityEngine.Camera>(); var cadence = new WaterReflectionSchedule();
                Assert.IsTrue(cadence.IsDue(camera, 1, false, 15));
                cadence.Record(camera, 1);
                Assert.IsFalse(cadence.IsDue(camera, 1.03, true, 15));
                Assert.IsFalse(cadence.IsDue(camera, 1.03, true, 15));
                Assert.IsTrue(cadence.IsDue(camera, 1.07, true, 15));
                Assert.IsTrue(cadence.IsDue(camera, 1.03, true, 0));
                camera.transform.position = Vector3.right * 3;
                Assert.IsTrue(cadence.IsDue(camera, 1.03, true, 15));
                cadence.Record(camera, 2); camera.fieldOfView = 35;
                Assert.IsTrue(cadence.IsDue(camera, 2.01, true, 15));
                Assert.IsTrue(cadence.IsDue(camera, .5, true, 15));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void LowerRippleBudgetKeepsNewestAndZeroBudgetRejectsNewWaves()
        {
            var waves = new CharacterTestShallowWaterRipples();
            for (int i = 0; i < 32; i++) waves.Add(new Vector2(i, 0), i, .02f);
            waves.SetLimit(8); Assert.AreEqual(8, waves.Count);
            for (int i = 0; i < waves.Count; i++) Assert.GreaterOrEqual(waves.Sources[i].z, 24);
            waves.SetLimit(0); waves.Add(Vector2.zero, 33, .02f); Assert.AreEqual(0, waves.Count);
        }
        [Test] public void RippleTilesConservativelyCoverRotatedWavesIncludingBit31()
        {
            var waves = new CharacterTestShallowWaterRipples();
            var localToWorld = Matrix4x4.TRS(new Vector3(3, .04f, -2), Quaternion.Euler(0, 37, 0), Vector3.one);
            var size = new Vector2(20, 16);
            for (int n = 0; n < 32; n++)
            {
                Vector3 center = localToWorld.MultiplyPoint3x4(new Vector3(n % 8 - 4, 0, n / 8 - 2));
                waves.Add(new Vector2(center.x, center.z), 0, .03f, .25f);
            }
            waves.BuildDrawData(1, 2, 1.6f, localToWorld.inverse, size);
            for (int n = 0; n < 32; n++) for (int a = 0; a < 72; a++)
            {
                float angle = a * Mathf.PI / 36;
                for (int ring = 0; ring < 3; ring++)
                {
                    float radius = .601f + ring * .999f;
                    Vector4 s = waves.Sources[n];
                    Vector3 local = localToWorld.inverse.MultiplyPoint3x4(new Vector3(s.x + Mathf.Cos(angle) * radius, .04f, s.y + Mathf.Sin(angle) * radius));
                    int x = Mathf.Clamp(Mathf.FloorToInt((local.x / size.x + .5f) * 16), 0, 15);
                    int y = Mathf.Clamp(Mathf.FloorToInt((local.z / size.y + .5f) * 16), 0, 15);
                    Assert.AreNotEqual(0, (int)waves.TileMasks[y * 16 + x][n / 8] & (1 << (n % 8)), "Missing wave " + n);
                }
            }
            waves.Clear(); waves.BuildDrawData(1, 2, 1.6f, localToWorld.inverse, size);
            foreach (var tile in waves.TileMasks) Assert.AreEqual(Vector4.zero, tile);
        }
        [Test] public void SparseRipplesSkipMostTilesAndPrecomputeOriginalFade()
        {
            var waves = new CharacterTestShallowWaterRipples(); waves.Add(Vector2.zero, 0, .04f, .25f);
            waves.BuildDrawData(1, 2, 1, Matrix4x4.identity, new Vector2(72, 50));
            int admitted = 0; foreach (var tile in waves.TileMasks) if (tile != Vector4.zero) admitted++;
            Assert.Less(admitted, 16); Assert.Greater(admitted, 0);
            Assert.AreEqual(.01f, waves.DrawSources[0].w, 1e-6f);
            Assert.AreEqual(.25f, waves.DrawSheen[0].x, 1e-6f);
        }
        [Test] public void ReflectionPreservesPlaneAndRoundTripsPoints()
        {
            var mirror=CharacterTestShallowWaterSample.ReflectionMatrix(2.5f);
            var point=new Vector3(7,8,-4);
            Assert.AreEqual(new Vector3(7,-3,-4),mirror.MultiplyPoint(point));
            Assert.AreEqual(point,mirror.MultiplyPoint(mirror.MultiplyPoint(point)));
            Assert.AreEqual(new Vector3(1,2.5f,3),mirror.MultiplyPoint(new Vector3(1,2.5f,3)));
        }
        [Test] public void RotatedSurfaceBoundsContainEveryWaterCorner()
        {
            var position = new Vector3(3,.025f,-7); var rotation = Quaternion.Euler(0,37,0);
            var size = new Vector2(36,28);
            var bounds = CharacterTestShallowWaterSample.SurfaceBounds(position,rotation,size);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 corner = position + rotation * new Vector3(x*size.x*.5f,0,z*size.y*.5f);
                Assert.That(bounds.SqrDistance(corner),Is.LessThan(1e-8f));
            }
        }
        [Test] public void RippleOverflowKeepsBoundAndReplacesOldest()
        {
            var waves=new CharacterTestShallowWaterRipples();
            for(int i=0;i<100;i++)waves.Add(new Vector2(i,0),i,1);
            Assert.AreEqual(32,waves.Count);Assert.AreEqual(68,waves.Replaced);
            for(int i=0;i<waves.Count;i++){Assert.GreaterOrEqual(waves.Sources[i].z,68);Assert.LessOrEqual(waves.Sources[i].w,.08f);}
        }
        [Test] public void RippleExpiryAndClockRestoreRemoveStalePackets()
        {
            var waves=new CharacterTestShallowWaterRipples();waves.Add(Vector2.zero,1,.02f);waves.Add(Vector2.one,2,.02f);
            waves.Expire(2.5f,1);Assert.AreEqual(1,waves.Count);
            waves.Expire(.5f,1);Assert.AreEqual(0,waves.Count);
        }
        [Test] public void InvalidWaveInputIsRejected()
        {
            var waves=new CharacterTestShallowWaterRipples();waves.Add(new Vector2(float.NaN,0),0,.02f);
            waves.Add(Vector2.zero,float.PositiveInfinity,.02f);waves.Add(Vector2.zero,0,-1);
            Assert.AreEqual(0,waves.Count);
        }
        [Test] public void AuxiliaryAdmissionEndsWhenDisabledOrDestroyed()
        {
            var source=new GameObject("Water test source");var secondary=new GameObject("Water test secondary");
            try
            {
                var camera=source.AddComponent<UnityEngine.Camera>();var auxiliaryCamera=secondary.AddComponent<UnityEngine.Camera>();
                var marker=secondary.AddComponent<MxSceneRenderingAuxiliaryCamera>();marker.SourceCamera=camera;
                Assert.IsTrue(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                marker.enabled=false;Assert.IsFalse(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                marker.enabled=true;Assert.IsTrue(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                Object.DestroyImmediate(marker);Assert.IsFalse(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
            }
            finally{Object.DestroyImmediate(secondary);Object.DestroyImmediate(source);}
        }
    }
}
