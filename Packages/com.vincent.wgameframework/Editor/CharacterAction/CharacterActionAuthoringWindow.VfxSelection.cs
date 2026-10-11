using System;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddTimelineVfxResourceCard(
            VisualElement parent,
            string serializedResourceKey,
            int eventFrame,
            Action<string> setter,
            bool requestFullTracksRefresh)
        {
            AddVfxResourceCard(
                parent,
                serializedResourceKey,
                eventFrame,
                value => ApplyTimelineFieldChange(() => setter(value), requestFullTracksRefresh));
        }

        private void AddVfxResourceCard(
            VisualElement parent,
            string serializedResourceKey,
            int eventFrame,
            Action<string> applyValue)
        {
            CharacterActionVfxCardSnapshot snapshot = CharacterActionVfxCardSnapshot.Resolve(serializedResourceKey);
            var card = new VisualElement();
            card.AddToClassList("ca-vfx-resource-card");

            var header = new VisualElement();
            header.AddToClassList("ca-vfx-resource-card__header");
            var thumbnail = new Image
            {
                image = snapshot.Thumbnail,
                scaleMode = ScaleMode.ScaleAndCrop
            };
            thumbnail.AddToClassList("ca-vfx-resource-card__thumbnail");
            header.Add(thumbnail);

            var copy = new VisualElement();
            copy.AddToClassList("ca-vfx-resource-card__copy");
            var title = new Label(snapshot.DisplayName);
            title.AddToClassList("ca-vfx-resource-card__title");
            copy.Add(title);
            var key = new Label(snapshot.ResourceKeyLabel);
            key.AddToClassList("ca-vfx-resource-card__key");
            copy.Add(key);
            var semantic = new Label(snapshot.SemanticLabel);
            semantic.AddToClassList("ca-vfx-resource-card__semantic");
            copy.Add(semantic);
            var actual = new Label(snapshot.ActualPrefabLabel);
            actual.AddToClassList("ca-vfx-resource-card__path");
            copy.Add(actual);
            header.Add(copy);

            var badge = new Label(snapshot.StatusLabel);
            badge.AddToClassList(snapshot.IsCandidate
                ? "ca-vfx-resource-card__badge--candidate"
                : "ca-vfx-resource-card__badge");
            header.Add(badge);
            card.Add(header);

            if (!string.IsNullOrWhiteSpace(snapshot.Diagnostic))
            {
                var diagnostic = new Label(snapshot.Diagnostic);
                diagnostic.AddToClassList("ca-vfx-resource-card__diagnostic");
                card.Add(diagnostic);
            }

            var actions = new VisualElement();
            actions.AddToClassList("ca-vfx-resource-card__actions");
            Button choose = null;
            choose = new Button(() =>
            {
                Rect world = choose.worldBound;
                Rect screen = new Rect(position.x + world.x, position.y + world.y, world.width, world.height);
                CharacterActionVfxPickerWindow.ShowPicker(
                    screen,
                    serializedResourceKey,
                    snapshot.ExpectedRole,
                    option =>
                    {
                        applyValue(option.ResourceKey.ToString());
                        PreviewSelectedVfxAtFrame(eventFrame);
                    });
            })
            {
                text = "选择 VFX…"
            };
            choose.AddToClassList("ca-vfx-resource-card__primary");
            actions.Add(choose);

            var workbench = new Button(VfxAuthoringResourceLibraryWindow.Open) { text = "打开 VFX 工作台" };
            actions.Add(workbench);
            var ping = new Button(() =>
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(snapshot.ActualPrefabPath);
                if (asset == null)
                    return;
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            })
            {
                text = "定位 Prefab"
            };
            ping.SetEnabled(!string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath));
            actions.Add(ping);
            card.Add(actions);

            var advanced = new Foldout { text = "高级：ResourceKey", value = false };
            var raw = new TextField { value = serializedResourceKey ?? string.Empty, isDelayed = true };
            raw.RegisterValueChangedCallback(evt =>
            {
                applyValue(evt.newValue ?? string.Empty);
                PreviewSelectedVfxAtFrame(eventFrame);
            });
            advanced.Add(raw);
            card.Add(advanced);
            parent.Add(card);
        }

        private void PreviewSelectedVfxAtFrame(int eventFrame)
        {
            if (_model?.SelectedAction == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureTimelinePreviewSession();
            _timelinePlayback?.Pause();
            _timelinePreviewSession?.InvalidatePresentationBindings();
            int targetFrame = Math.Max(0, eventFrame);
            if (_timelinePlayback != null && _timelinePlayback.CurrentFrame != targetFrame)
                _timelinePlayback.Seek(targetFrame);
            else
                UpdateTimelineRuntimePreview(targetFrame);
            _timelinePlayback?.Play();
            RefreshWorkbenchTransportState();
        }
    }

    internal sealed class CharacterActionVfxCardSnapshot
    {
        internal string DisplayName = "未选择 VFX";
        internal string ResourceKeyLabel = "ResourceKey：—";
        internal string SemanticLabel = "尚未绑定特效资源";
        internal string ActualPrefabPath = string.Empty;
        internal string ActualPrefabLabel = "实际 Prefab：—";
        internal string StatusLabel = "未绑定";
        internal string Diagnostic = string.Empty;
        internal string ExpectedRole = string.Empty;
        internal Texture Thumbnail;
        internal bool IsCandidate;
        internal string Role = string.Empty;
        internal string Anchor = string.Empty;
        internal string Lifecycle = string.Empty;
        internal string VisualMaturity = string.Empty;
        internal string Review = string.Empty;
        internal bool IsStale;
        internal bool IsSelectable = true;

        internal static CharacterActionVfxCardSnapshot Resolve(string serializedResourceKey)
        {
            var snapshot = new CharacterActionVfxCardSnapshot();
            if (!CharacterActionVfxSelectionCatalog.TryParseResourceKey(serializedResourceKey, out ResourceKey key))
            {
                snapshot.Diagnostic = string.IsNullOrWhiteSpace(serializedResourceKey)
                    ? "点击“选择 VFX”从资源库中选择。"
                    : "当前 ResourceKey 格式无效，请重新选择。";
                return snapshot;
            }

            snapshot.ResourceKeyLabel = key.ToString();
            snapshot.ExpectedRole = InferRole(key.Id);
            snapshot.DisplayName = FriendlyName(key.Id, snapshot.ExpectedRole);
            snapshot.SemanticLabel = string.IsNullOrWhiteSpace(snapshot.ExpectedRole)
                ? "VFX 资源"
                : "技能职责 · " + VfxLibraryLabels.Display(snapshot.ExpectedRole);

            if (VfxPublishedBindingEditorService.TryResolveAuthoringPreviewBinding(
                    key,
                    out VfxUnityPrefabBinding binding,
                    out _))
            {
                snapshot.ActualPrefabPath = AssetDatabase.GetAssetPath(binding.Prefab);
                snapshot.Thumbnail = AssetPreview.GetAssetPreview(binding.Prefab)
                    ?? AssetPreview.GetMiniThumbnail(binding.Prefab);
            }

            snapshot.IsCandidate = false;
            using (CharacterActionVfxSelectionCatalog catalog = CharacterActionVfxSelectionCatalog.Load())
            {
                CharacterActionVfxSelectionOption option = catalog.FindCurrent(serializedResourceKey);
                if (option != null)
                {
                    snapshot.Role = option.Role;
                    snapshot.Anchor = option.Anchor;
                    snapshot.Lifecycle = option.Lifecycle;
                    snapshot.VisualMaturity = option.Maturity;
                    snapshot.Review = option.Review;
                    snapshot.IsSelectable = option.Selectable;
                    snapshot.IsStale = option.IsCandidate && !option.Selectable;
                    snapshot.Thumbnail = option.Thumbnail ?? snapshot.Thumbnail;
                }
            }
            snapshot.StatusLabel = snapshot.IsCandidate ? "本机 Candidate" : "正式绑定";
            snapshot.ActualPrefabLabel = string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath)
                ? "实际 Prefab：当前投影未解析"
                : "实际 Prefab：" + snapshot.ActualPrefabPath;
            if (string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath))
                snapshot.Diagnostic = "当前 ResourceKey 没有可解析的 Unity Prefab，请刷新 VFX 发布投影。";
            return snapshot;
        }

        private static string InferRole(string id)
        {
            if (id.EndsWith(".charge", StringComparison.OrdinalIgnoreCase)) return "Charge";
            if (id.EndsWith(".flight", StringComparison.OrdinalIgnoreCase)) return "Flight";
            if (id.EndsWith(".impact", StringComparison.OrdinalIgnoreCase)) return "Impact";
            return string.Empty;
        }

        private static string FriendlyName(string id, string role)
        {
            string core = id ?? string.Empty;
            int marker = core.IndexOf("vfx.", StringComparison.OrdinalIgnoreCase);
            if (marker == 0)
                core = core.Substring(4);
            core = core.Replace('.', ' ').Replace('_', ' ');
            return string.IsNullOrWhiteSpace(role)
                ? core
                : core + " · " + VfxLibraryLabels.Display(role);
        }
    }
}
