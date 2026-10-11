using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class PlayerGuidancePresentationPlayModeTests
    {
        private const string EvidenceDirectory = "Temp/PlayerGuidanceEvidence";

        [UnityTest]
        public IEnumerator PublishedPackage_RendersGuidanceVariantsAndCapturesEvidence()
        {
            RemovePackageIfLoaded();
            Fgui.FontManager.Clear();
            Texture2D atlas = null;
            IMxFairyGuiComponentHandle cardHandle = null;
            IMxFairyGuiComponentHandle noticeHandle = null;
            PreviewScene previewScene = null;
            try
            {
                previewScene = PreviewScene.Create();
                Fgui.UIPackage package = LoadPublishedPackage(out atlas);
                Assert.That(package, Is.Not.Null);

                Fgui.GComponent card = Fgui.UIPackage.CreateObject(
                    MxPlayerGuidanceFairyGuiIds.PackageId,
                    MxPlayerGuidanceFairyGuiIds.CardComponentName)?.asCom;
                Fgui.GComponent notices = Fgui.UIPackage.CreateObject(
                    MxPlayerGuidanceFairyGuiIds.PackageId,
                    MxPlayerGuidanceFairyGuiIds.NoticeComponentName)?.asCom;
                Assert.That(card, Is.Not.Null);
                Assert.That(notices, Is.Not.Null);

                cardHandle = new MxFairyGuiComponentHandle(
                    MxPlayerGuidanceFairyGuiIds.PackageId,
                    MxPlayerGuidanceFairyGuiIds.CardComponentName,
                    card);
                noticeHandle = new MxFairyGuiComponentHandle(
                    MxPlayerGuidanceFairyGuiIds.PackageId,
                    MxPlayerGuidanceFairyGuiIds.NoticeComponentName,
                    notices);
                card.SetSize(1920f, 1080f);
                notices.SetSize(1920f, 1080f);
                Fgui.GRoot.inst.AddChild(card);
                Fgui.GRoot.inst.AddChild(notices);

                var cardBinder = new MxPlayerGuidanceCardBinder(null);
                var noticeBinder = new MxPlayerGuidanceNoticeStackBinder(null);
                var cardViewModel = CreateDevelopmentPreviewCard();
                var noticeViewModel = CreateDevelopmentPreviewNotices();
                cardBinder.Bind(cardHandle, cardViewModel);
                noticeBinder.Bind(noticeHandle, noticeViewModel);

                yield return null;
                yield return null;

                AssertPublishedVisuals(card, notices);
                yield return CaptureEvidence("player_guidance_full_preview.png");

                cardViewModel.Visible = false;
                cardBinder.Bind(cardHandle, cardViewModel);
                BindSingleNotice(
                    noticeViewModel,
                    "目标已锁定",
                    MxUiGuidanceGlyph.Success,
                    MxUiGuidanceTone.Success);
                noticeBinder.Bind(noticeHandle, noticeViewModel);
                yield return CaptureEvidence("player_guidance_target_locked.png");

                BindSingleNotice(
                    noticeViewModel,
                    "当前无法锁定目标",
                    MxUiGuidanceGlyph.Warning,
                    MxUiGuidanceTone.Warning);
                noticeBinder.Bind(noticeHandle, noticeViewModel);
                yield return CaptureEvidence("player_guidance_target_rejected.png");

                BindSingleNotice(
                    noticeViewModel,
                    "操作失败，请稍后重试",
                    MxUiGuidanceGlyph.Error,
                    MxUiGuidanceTone.Error);
                noticeBinder.Bind(noticeHandle, noticeViewModel);
                yield return CaptureEvidence("player_guidance_error.png");
            }
            finally
            {
                RemoveAndDispose(cardHandle);
                RemoveAndDispose(noticeHandle);
                RemovePackageIfLoaded();
                if (atlas != null)
                    UnityEngine.Object.Destroy(atlas);
                previewScene?.Dispose();
            }
        }

        private static MxFairyGuiGuidanceCardViewModel CreateDevelopmentPreviewCard()
        {
            return new MxFairyGuiGuidanceCardViewModel
            {
                Visible = true,
                Title = "开发预览：边界接近引导",
                HeaderGlyph = MxUiGuidanceGlyph.Context,
                ActionLabel = "步骤：",
                ActionBody = "从 ApproachStart 接近边界",
                ActionGlyph = MxUiGuidanceGlyph.Move,
                ActionTone = MxUiGuidanceTone.Info,
                FocusLabel = "观察：",
                FocusBody = "接触、停步、滑动与离地反馈",
                FocusGlyph = MxUiGuidanceGlyph.Focus,
                FocusTone = MxUiGuidanceTone.Info,
                StatusLabel = "状态：",
                StatusBody = "开发预览，等待正式观察结果",
                StatusGlyph = MxUiGuidanceGlyph.Progress,
                StatusTone = MxUiGuidanceTone.Neutral,
                LayoutPreset = MxFairyGuiGuidanceLayoutPreset.Default
            };
        }

        private static MxFairyGuiGuidanceNoticeStackViewModel CreateDevelopmentPreviewNotices()
        {
            return new MxFairyGuiGuidanceNoticeStackViewModel
            {
                Notice1Visible = true,
                Notice1Text = "效果预览：Typed 读数已采集",
                Notice1Glyph = MxUiGuidanceGlyph.Success,
                Notice1Tone = MxUiGuidanceTone.Success,
                Notice1Sequence = 2L,
                Notice1Revision = 1L,
                Notice2Visible = true,
                Notice2Text = "效果预览：尚未进入观察区",
                Notice2Glyph = MxUiGuidanceGlyph.Warning,
                Notice2Tone = MxUiGuidanceTone.Warning,
                Notice2Sequence = 1L,
                Notice2Revision = 1L,
                LayoutPreset = MxFairyGuiGuidanceLayoutPreset.Default
            };
        }

        private static void BindSingleNotice(
            MxFairyGuiGuidanceNoticeStackViewModel viewModel,
            string text,
            MxUiGuidanceGlyph glyph,
            MxUiGuidanceTone tone)
        {
            viewModel.Notice1Visible = true;
            viewModel.Notice1Text = text;
            viewModel.Notice1Glyph = glyph;
            viewModel.Notice1Tone = tone;
            viewModel.Notice1Sequence++;
            viewModel.Notice1Revision++;
            viewModel.Notice2Visible = false;
            viewModel.Notice2Text = string.Empty;
            viewModel.Notice2Sequence = 0L;
            viewModel.Notice2Revision = 0L;
        }

        private static void AssertPublishedVisuals(
            Fgui.GComponent card,
            Fgui.GComponent notices)
        {
            Assert.That(
                card.GetChild(MxPlayerGuidanceFairyGuiIds.Title)?.asTextField?.text,
                Is.EqualTo("开发预览：边界接近引导"));
            Assert.That(
                notices.GetChild(MxPlayerGuidanceFairyGuiIds.Notice1Text)?.asTextField?.text,
                Is.EqualTo("效果预览：Typed 读数已采集"));
            Assert.That(
                notices.GetChild(MxPlayerGuidanceFairyGuiIds.Notice2Text)?.asTextField?.text,
                Is.EqualTo("效果预览：尚未进入观察区"));
            Assert.That(
                card.GetChild(MxPlayerGuidanceFairyGuiIds.HeaderIcon)?.asImage?.texture,
                Is.Not.Null);
            Assert.That(
                notices.GetChild(MxPlayerGuidanceFairyGuiIds.Notice1Icon)?.asImage?.texture,
                Is.Not.Null);
            Assert.That(card.touchable, Is.False);
            Assert.That(notices.touchable, Is.False);
        }

        private static IEnumerator CaptureEvidence(string fileName)
        {
            yield return new WaitForEndOfFrame();

            int width = Math.Max(1, Screen.width);
            int height = Math.Max(1, Screen.height);
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                renderTexture.Create();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
                RenderTexture.active = renderTexture;
                screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                screenshot.Apply(false, false);
                FlipVertically(screenshot, width, height);

                string directory = Path.GetFullPath(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    EvidenceDirectory));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0L), path);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(screenshot);
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }

        private static void FlipVertically(Texture2D texture, int width, int height)
        {
            Color32[] pixels = texture.GetPixels32();
            int halfHeight = height / 2;
            for (int y = 0; y < halfHeight; y++)
            {
                int oppositeY = height - 1 - y;
                int rowStart = y * width;
                int oppositeRowStart = oppositeY * width;
                for (int x = 0; x < width; x++)
                {
                    int index = rowStart + x;
                    int oppositeIndex = oppositeRowStart + x;
                    Color32 value = pixels[index];
                    pixels[index] = pixels[oppositeIndex];
                    pixels[oppositeIndex] = value;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static Fgui.UIPackage LoadPublishedPackage(out Texture2D atlas)
        {
            string packageId = MxPlayerGuidanceFairyGuiIds.PackageId;
            string packageDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI",
                packageId);
            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                packageDirectory,
                packageId + "_fui.bytes"));
            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(atlas.LoadImage(File.ReadAllBytes(Path.Combine(
                packageDirectory,
                packageId + "_atlas0.png"))), Is.True);
            Texture2D packageAtlas = atlas;
            return Fgui.UIPackage.AddPackage(
                packageBytes,
                packageId,
                (string name,
                    string extension,
                    Type type,
                    out Fgui.DestroyMethod destroyMethod) =>
                {
                    destroyMethod = Fgui.DestroyMethod.None;
                    return type != null && type.IsInstanceOfType(packageAtlas)
                        ? packageAtlas
                        : null;
                });
        }

        private static void RemoveAndDispose(IMxFairyGuiComponentHandle handle)
        {
            if (handle is MxFairyGuiComponentHandle concrete
                && concrete.Component.parent != null)
            {
                concrete.Component.RemoveFromParent();
            }
            handle?.Dispose();
        }

        private static void RemovePackageIfLoaded()
        {
            Fgui.UIPackage package = Fgui.UIPackage.GetById(
                    MxPlayerGuidanceFairyGuiIds.PackageId)
                ?? Fgui.UIPackage.GetByName(MxPlayerGuidanceFairyGuiIds.PackageId);
            if (package != null)
                Fgui.UIPackage.RemovePackage(package.id);
        }

        private sealed class PreviewScene : IDisposable
        {
            private readonly GameObject _root;
            private readonly List<Material> _materials;

            private PreviewScene(GameObject root, List<Material> materials)
            {
                _root = root;
                _materials = materials;
            }

            public static PreviewScene Create()
            {
                var root = new GameObject("Player Guidance Visual Evidence Scene");
                var materials = new List<Material>(4);

                var cameraObject = new GameObject("Preview Camera");
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(123, 165, 198, 255);
                camera.fieldOfView = 48f;
                camera.transform.position = new Vector3(0f, 6.5f, -14f);
                camera.transform.LookAt(new Vector3(0f, 2f, 3f));

                var lightObject = new GameObject("Preview Directional Light");
                lightObject.transform.SetParent(root.transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.color = new Color32(220, 235, 255, 255);
                lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

                AddPrimitive(
                    root.transform,
                    materials,
                    PrimitiveType.Cube,
                    "Ground",
                    new Vector3(0f, -0.4f, 4f),
                    new Vector3(24f, 0.8f, 24f),
                    new Color32(92, 104, 113, 255));
                AddPrimitive(
                    root.transform,
                    materials,
                    PrimitiveType.Cube,
                    "Boundary Wall",
                    new Vector3(4.6f, 2.2f, 7.5f),
                    new Vector3(9f, 5.2f, 0.8f),
                    new Color32(110, 121, 130, 255));
                AddPrimitive(
                    root.transform,
                    materials,
                    PrimitiveType.Cube,
                    "Course Block",
                    new Vector3(-5.4f, 1.25f, 5.2f),
                    new Vector3(5.5f, 3.3f, 4.2f),
                    new Color32(73, 84, 94, 255));
                AddPrimitive(
                    root.transform,
                    materials,
                    PrimitiveType.Capsule,
                    "Character Silhouette",
                    new Vector3(0.7f, 1.1f, 1.2f),
                    new Vector3(0.8f, 1.1f, 0.8f),
                    new Color32(35, 40, 48, 255));

                return new PreviewScene(root, materials);
            }

            public void Dispose()
            {
                if (_root != null)
                    UnityEngine.Object.Destroy(_root);
                for (int i = 0; i < _materials.Count; i++)
                {
                    if (_materials[i] != null)
                        UnityEngine.Object.Destroy(_materials[i]);
                }
            }

            private static void AddPrimitive(
                Transform parent,
                List<Material> materials,
                PrimitiveType primitiveType,
                string name,
                Vector3 position,
                Vector3 scale,
                Color color)
            {
                GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
                gameObject.name = name;
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.position = position;
                gameObject.transform.localScale = scale;
                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                    return;

            Shader shader = Shader.Find("WGame/PropStylizedLit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", color);
            material.SetFloat("_WgAlbedoStrength", 1f);
            material.SetFloat("_WgBronzeTrimAmount", 0f);
            material.SetFloat("_WgInteractableGlowAmount", 0f);
            materials.Add(material);
                renderer.sharedMaterial = material;
            }
        }
    }
}
