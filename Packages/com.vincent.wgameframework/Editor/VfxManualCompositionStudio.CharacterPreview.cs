using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using MxFramework.Editor.CharacterAction;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal sealed partial class VfxManualCompositionStudioController
    {
        private void OnCharacterPreviewModeChanged(ChangeEvent<string> evt)
        {
            if (_binding || _session?.Draft == null) return;
            string phase = BeginCharacterPreviewContextEdit();
            string mode = evt.newValue == "角色挂载"
                ? VfxCompositionPreviewContextView.CharacterAttachedMode
                : VfxCompositionPreviewContextView.VfxOnlyMode;
            SetCharacterContextMode(phase, mode);
            VfxCompositionPreviewContextView context = ResolveCharacterPreviewContext(phase);
            if (mode == VfxCompositionPreviewContextView.CharacterAttachedMode)
            {
                SetCharacterPreviewDrawerExpanded(true);
                LoadCharacterChoices();
                VfxCharacterPreviewDescriptor selected = FindCharacter(context.CharacterStableId, context.PackageId) ?? _characterChoices.FirstOrDefault();
                if (selected != null)
                {
                    SetCharacterIdentity(phase, selected);
                    string animationKey = ResolvePoseAnimationKey(selected, context.PosePresetId);
                    SetCharacterContextString(phase, CharacterContextField.AnimationResourceKey, animationKey);
                }
            }
            RefreshCharacterPreviewControls();
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void OnCharacterSelectionChanged(ChangeEvent<string> evt)
        {
            if (_binding || _session?.Draft == null || !_charactersByLabel.TryGetValue(evt.newValue ?? string.Empty, out VfxCharacterPreviewDescriptor character)) return;
            string phase = BeginCharacterPreviewContextEdit();
            SetCharacterIdentity(phase, character);
            SetCharacterContextString(phase, CharacterContextField.SocketId, string.Empty);
            VfxCompositionPreviewContextView context = ResolveCharacterPreviewContext(phase);
            SetCharacterContextString(phase, CharacterContextField.AnimationResourceKey, ResolvePoseAnimationKey(character, context.PosePresetId));
            RefreshCharacterPreviewControls();
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void OnCharacterPoseChanged(ChangeEvent<string> evt)
        {
            if (_binding || _session?.Draft == null) return;
            string presetId = PosePresetId(evt.newValue);
            string phase = BeginCharacterPreviewContextEdit();
            SetCharacterContextString(phase, CharacterContextField.PosePresetId, presetId);
            if (presetId != "Custom")
                SetCharacterContextString(phase, CharacterContextField.AnimationResourceKey, ResolvePoseAnimationKey(_selectedCharacter, presetId));
            RefreshCharacterPreviewControls();
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void OnCharacterAnimationChanged(ChangeEvent<string> evt)
        {
            if (_binding || _session?.Draft == null || !_animationsByLabel.TryGetValue(evt.newValue ?? string.Empty, out CharacterActionAnimationClipChoice animation)) return;
            string phase = BeginCharacterPreviewContextEdit();
            SetCharacterContextString(phase, CharacterContextField.PosePresetId, "Custom");
            SetCharacterContextString(phase, CharacterContextField.AnimationResourceKey, animation.ResourceKey);
            RefreshCharacterPreviewControls();
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void OnCharacterSocketChanged(ChangeEvent<string> evt)
        {
            if (_binding || _session?.Draft == null || !_socketsByLabel.TryGetValue(evt.newValue ?? string.Empty, out VfxCharacterPreviewSocket socket)) return;
            string phase = BeginCharacterPreviewContextEdit();
            SetCharacterContextString(phase, CharacterContextField.SocketId, socket.SocketId);
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void UpdateCharacterPreviewContext(Action<VfxCompositionPreviewContextView> change)
        {
            if (_binding || _session?.Draft == null) return;
            string phase = BeginCharacterPreviewContextEdit();
            VfxCompositionPreviewContextView before = ResolveCharacterPreviewContext(phase);
            var edited = new VfxCompositionPreviewContextView
            {
                Mode = before.Mode, CharacterStableId = before.CharacterStableId, PackageId = before.PackageId,
                CharacterEvidenceHash = before.CharacterEvidenceHash, PosePresetId = before.PosePresetId,
                AnimationResourceKey = before.AnimationResourceKey, AnimationLoop = before.AnimationLoop,
                AnimationSpeed = before.AnimationSpeed, SocketId = before.SocketId
            };
            change(edited);
            if (edited.AnimationLoop != before.AnimationLoop) SetCharacterContextBool(phase, CharacterContextField.AnimationLoop, edited.AnimationLoop);
            if (!Mathf.Approximately(edited.AnimationSpeed, before.AnimationSpeed)) SetCharacterContextFloat(phase, CharacterContextField.AnimationSpeed, edited.AnimationSpeed);
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private VfxCompositionPreviewContextView EnsurePreviewContext()
        {
            VfxCompositionDraftService.NormalizePreviewContext(_session.Draft);
            return _session.Draft.PreviewContext;
        }

        private enum CharacterContextField { PosePresetId, AnimationResourceKey, AnimationLoop, AnimationSpeed, SocketId }

        private string BeginCharacterPreviewContextEdit()
        {
            string phase = ResolveCharacterPreviewEditPhase();
            if (_preview.State == VfxLibraryPlaybackState.Playing && !IsSpecificLifecyclePhase(_previewStage.value))
            {
                _preview.Pause();
                if (_abToggle.value) _comparisonPreview.Pause();
                _lockedCharacterContextPhase = phase ?? string.Empty;
                RefreshPlaybackUi();
            }
            _session.RecordChange();
            return phase;
        }

        private string ResolveCharacterPreviewEditPhase()
        {
            if (IsSpecificLifecyclePhase(_previewStage.value)) return _previewStage.value;
            if (_characterPreviewEditTarget.value == "共享默认") return null;
            if (IsSpecificLifecyclePhase(_lockedCharacterContextPhase)) return _lockedCharacterContextPhase;
            string current = _preview.CurrentLifecyclePhase.ToString();
            return IsSpecificLifecyclePhase(current) ? current : "Build";
        }

        private void ResumeCharacterContextFollowing()
        {
            if (string.IsNullOrWhiteSpace(_lockedCharacterContextPhase)) return;
            _lockedCharacterContextPhase = string.Empty;
            RefreshCharacterPreviewControls();
        }

        private VfxCompositionPreviewContextView ResolveCharacterPreviewContext(string phase)
        {
            VfxCompositionPreviewContextView shared = EnsurePreviewContext();
            return string.IsNullOrWhiteSpace(phase)
                ? VfxCompositionDraftService.ResolvePreviewContext(shared, null)
                : VfxCompositionDraftService.ResolvePreviewContext(shared, phase);
        }

        private void SetCharacterContextMode(string phase, string value)
        {
            if (string.IsNullOrWhiteSpace(phase)) EnsurePreviewContext().Mode = value;
            else VfxCompositionDraftService.EnsurePreviewContextOverride(EnsurePreviewContext(), phase).Mode = value;
        }

        private void SetCharacterIdentity(string phase, VfxCharacterPreviewDescriptor character)
        {
            if (character == null) return;
            if (string.IsNullOrWhiteSpace(phase))
            {
                VfxCompositionPreviewContextView shared = EnsurePreviewContext();
                shared.CharacterStableId = character.CharacterStableId;
                shared.PackageId = character.PackageId;
                shared.CharacterEvidenceHash = character.EvidenceHash;
            }
            else
            {
                VfxCompositionPreviewContextOverrideView value = VfxCompositionDraftService.EnsurePreviewContextOverride(EnsurePreviewContext(), phase);
                value.CharacterStableId = character.CharacterStableId;
                value.PackageId = character.PackageId;
                value.CharacterEvidenceHash = character.EvidenceHash;
            }
        }

        private void SetCharacterContextString(string phase, CharacterContextField field, string value)
        {
            value ??= string.Empty;
            if (string.IsNullOrWhiteSpace(phase))
            {
                VfxCompositionPreviewContextView shared = EnsurePreviewContext();
                switch (field)
                {
                    case CharacterContextField.PosePresetId: shared.PosePresetId = value; break;
                    case CharacterContextField.AnimationResourceKey: shared.AnimationResourceKey = value; break;
                    case CharacterContextField.SocketId: shared.SocketId = value; break;
                }
                return;
            }
            VfxCompositionPreviewContextOverrideView target = VfxCompositionDraftService.EnsurePreviewContextOverride(EnsurePreviewContext(), phase);
            switch (field)
            {
                case CharacterContextField.PosePresetId: target.PosePresetId = value; break;
                case CharacterContextField.AnimationResourceKey: target.AnimationResourceKey = value; break;
                case CharacterContextField.SocketId: target.SocketId = value; break;
            }
        }

        private void SetCharacterContextBool(string phase, CharacterContextField field, bool value)
        {
            if (field != CharacterContextField.AnimationLoop) return;
            if (string.IsNullOrWhiteSpace(phase)) EnsurePreviewContext().AnimationLoop = value;
            else VfxCompositionDraftService.EnsurePreviewContextOverride(EnsurePreviewContext(), phase).AnimationLoop = value;
        }

        private void SetCharacterContextFloat(string phase, CharacterContextField field, float value)
        {
            if (field != CharacterContextField.AnimationSpeed) return;
            if (string.IsNullOrWhiteSpace(phase)) EnsurePreviewContext().AnimationSpeed = value;
            else VfxCompositionDraftService.EnsurePreviewContextOverride(EnsurePreviewContext(), phase).AnimationSpeed = value;
        }

        private string ResolvePoseAnimationKey(VfxCharacterPreviewDescriptor character, string posePresetId)
        {
            if (character == null || posePresetId == "Custom") return string.Empty;
            return VfxCharacterPreviewCatalog.TryResolvePose(character, posePresetId, _session.Draft.TargetEffectId, out CharacterActionAnimationClipChoice choice)
                ? choice.ResourceKey
                : string.Empty;
        }

        private void ResetCharacterPreviewPhase()
        {
            if (_session?.Draft == null) return;
            string phase = ResolveCharacterPreviewEditPhase();
            if (!IsSpecificLifecyclePhase(phase)) return;
            _session.RecordChange();
            VfxCompositionDraftService.ClearPreviewContextOverride(EnsurePreviewContext(), phase);
            RefreshCharacterPreviewControls();
            ApplyCharacterPreviewContext(true);
            ValidateDraft();
        }

        private void LoadCharacterChoices()
        {
            if (_characterChoices.Count > 0) return;
            _characterChoices = _characterCatalogService.Discover();
            _charactersByLabel.Clear();
            var labels = new List<string>(_characterChoices.Count);
            for (int i = 0; i < _characterChoices.Count; i++)
            {
                VfxCharacterPreviewDescriptor character = _characterChoices[i];
                string label = character.DisplayName;
                labels.Add(label);
                _charactersByLabel[label] = character;
            }
            _characterPreviewCharacter.choices = labels;
        }

        private void RefreshCharacterPreviewControls()
        {
            if (_session?.Draft == null) return;
            string phase = ResolveCharacterPreviewEditPhase();
            VfxCompositionPreviewContextView context = ResolveCharacterPreviewContext(phase);
            bool attached = context.Mode == VfxCompositionPreviewContextView.CharacterAttachedMode;
            if (attached) LoadCharacterChoices();
            _selectedCharacter = attached ? FindCharacter(context.CharacterStableId, context.PackageId) : null;
            if (attached && _selectedCharacter == null) _selectedCharacter = _characterChoices.FirstOrDefault();

            _binding = true;
            try
            {
                bool specificStage = IsSpecificLifecyclePhase(_previewStage.value);
                if (specificStage)
                {
                    _characterPreviewEditTarget.choices = new List<string> { _previewStage.value };
                    _characterPreviewEditTarget.SetValueWithoutNotify(_previewStage.value);
                    _characterPreviewEditTarget.SetEnabled(false);
                }
                else
                {
                    _characterPreviewEditTarget.choices = new List<string> { "跟随播放", "共享默认" };
                    string target = string.IsNullOrWhiteSpace(phase) ? "共享默认" : "跟随播放";
                    _characterPreviewEditTarget.SetValueWithoutNotify(target);
                    _characterPreviewEditTarget.SetEnabled(true);
                }
                string active = string.IsNullOrWhiteSpace(phase) ? "共享默认" : phase;
                _characterPreviewActivePhase.text = "当前生效：" + active;
                VfxCompositionPreviewContextOverrideView phaseOverride = IsSpecificLifecyclePhase(phase) ? EnsurePreviewContext().PhaseOverrides?.Get(phase) : null;
                int overrideCount = phaseOverride?.FieldCount ?? 0;
                _characterPreviewOverrideSummary.text = string.IsNullOrWhiteSpace(phase)
                    ? "编辑五阶段继承的基础值"
                    : overrideCount == 0 ? "全部字段继承共享默认" : "本阶段覆盖 " + overrideCount + " 组字段 · 其余继承";
                _characterPreviewOverrideSummary.EnableInClassList("has-overrides", overrideCount > 0);
                _characterPreviewResetPhase.SetEnabled(IsSpecificLifecyclePhase(phase) && overrideCount > 0);
                bool phaseEditing = IsSpecificLifecyclePhase(phase);
                _characterPreviewMode.label = InheritedLabel("预览模式", phaseEditing && phaseOverride?.Mode == null);
                _characterPreviewCharacter.label = InheritedLabel("角色", phaseEditing && phaseOverride?.CharacterStableId == null);
                _characterPreviewPose.label = InheritedLabel("快速姿势", phaseEditing && phaseOverride?.PosePresetId == null);
                _characterPreviewAnimation.label = InheritedLabel("具体动画", phaseEditing && phaseOverride?.AnimationResourceKey == null);
                _characterPreviewSocket.label = InheritedLabel("挂载 Socket", phaseEditing && phaseOverride?.SocketId == null);
                _characterAnimationLoop.label = InheritedLabel("动画循环", phaseEditing && phaseOverride?.AnimationLoop.HasValue != true);
                _characterAnimationSpeed.label = InheritedLabel("动画速度", phaseEditing && phaseOverride?.AnimationSpeed.HasValue != true);
                _characterPreviewMode.SetValueWithoutNotify(attached ? "角色挂载" : "纯 VFX");
                RefreshCharacterPreviewContextToggle(attached);
                _characterPreviewCharacter.SetEnabled(attached && _characterChoices.Count > 0);
                _characterPreviewPose.SetEnabled(attached && _selectedCharacter != null);
                _characterPreviewAnimation.SetEnabled(attached && _selectedCharacter != null);
                _characterPreviewSocket.SetEnabled(attached && _selectedCharacter != null);
                _characterAnimationLoop.SetEnabled(attached && _selectedCharacter != null);
                _characterAnimationSpeed.SetEnabled(attached && _selectedCharacter != null);
                _characterVisible.SetEnabled(attached && _selectedCharacter != null);
                _characterSocketAxes.SetEnabled(attached && _selectedCharacter != null);
                if (_selectedCharacter != null)
                {
                    string characterLabel = _charactersByLabel.FirstOrDefault(value => ReferenceEquals(value.Value, _selectedCharacter)).Key;
                    _characterPreviewCharacter.SetValueWithoutNotify(characterLabel ?? string.Empty);
                    PopulateAnimationChoices(_selectedCharacter, context.AnimationResourceKey);
                    PopulateSocketChoices(_selectedCharacter, context.SocketId);
                }
                else
                {
                    _animationsByLabel.Clear();
                    _socketsByLabel.Clear();
                    _characterPreviewAnimation.choices = new List<string>();
                    _characterPreviewSocket.choices = new List<string>();
                    _characterPreviewCharacter.SetValueWithoutNotify(string.Empty);
                }
                _characterPreviewPose.SetValueWithoutNotify(PosePresetLabel(context.PosePresetId));
                _characterAnimationLoop.SetValueWithoutNotify(context.AnimationLoop);
                _characterAnimationSpeed.SetValueWithoutNotify(context.AnimationSpeed);
            }
            finally { _binding = false; }
        }

        private static string InheritedLabel(string label, bool inherited)
        {
            return inherited ? label + "（继承）" : label;
        }

        private void PopulateAnimationChoices(VfxCharacterPreviewDescriptor character, string selectedResourceKey)
        {
            _animationsByLabel.Clear();
            var labels = new List<string>(character.Animations.Length);
            string selectedLabel = string.Empty;
            for (int i = 0; i < character.Animations.Length; i++)
            {
                CharacterActionAnimationClipChoice animation = character.Animations[i];
                string label = animation.Label;
                if (_animationsByLabel.ContainsKey(label)) label += " · " + animation.ResourceKey;
                _animationsByLabel[label] = animation;
                labels.Add(label);
                if (string.Equals(animation.ResourceKey, selectedResourceKey, StringComparison.Ordinal)) selectedLabel = label;
            }
            _characterPreviewAnimation.choices = labels;
            _characterPreviewAnimation.SetValueWithoutNotify(selectedLabel);
        }

        private void PopulateSocketChoices(VfxCharacterPreviewDescriptor character, string selectedSocketId)
        {
            _socketsByLabel.Clear();
            var labels = new List<string>(character.Sockets.Count);
            string selectedLabel = string.Empty;
            for (int i = 0; i < character.Sockets.Count; i++)
            {
                VfxCharacterPreviewSocket socket = character.Sockets[i];
                labels.Add(socket.DisplayName);
                _socketsByLabel[socket.DisplayName] = socket;
                if (string.Equals(socket.SocketId, selectedSocketId, StringComparison.Ordinal)) selectedLabel = socket.DisplayName;
            }
            if (string.IsNullOrWhiteSpace(selectedLabel) && labels.Count > 0) selectedLabel = labels[0];
            _characterPreviewSocket.choices = labels;
            _characterPreviewSocket.SetValueWithoutNotify(selectedLabel);
        }

        private void ApplyCharacterPreviewContext(bool reportStatus)
        {
            if (_session?.Draft == null) return;
            string phase = ResolveAppliedCharacterPreviewPhase();
            VfxCompositionPreviewContextView context = ResolveCharacterPreviewContext(phase);
            _lastAppliedCharacterContextPhase = phase ?? string.Empty;
            if (context.Mode != VfxCompositionPreviewContextView.CharacterAttachedMode)
            {
                _preview.ClearCharacterContext();
                _comparisonPreview.ClearCharacterContext();
                if (reportStatus) SetCharacterPreviewStatus("纯 VFX 模式 · 角色上下文不会进入 Candidate Prefab", false);
                MarkPreviewDirty();
                return;
            }

            LoadCharacterChoices();
            VfxCharacterPreviewDescriptor character = FindCharacter(context.CharacterStableId, context.PackageId);
            if (character == null)
            {
                SetCharacterPreviewStatus("角色包已失效或不再 Ready；Layer 编辑仍可继续，但角色挂载预览不可用。", true);
                _preview.ClearCharacterContext();
                _comparisonPreview.ClearCharacterContext();
                return;
            }
            AnimationClip clip = null;
            if (!string.IsNullOrWhiteSpace(context.AnimationResourceKey)
                && CharacterActionPreviewBindingDiscovery.TryFindAnimationClipChoice(context.AnimationResourceKey, character.CharacterStableId, out CharacterActionAnimationClipChoice choice))
                CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(choice, out clip);

            bool primaryReady = _preview.SetCharacterContext(character.PrefabPath, character.Package, context.SocketId, clip, context.AnimationLoop, context.AnimationSpeed, _characterVisible.value, _characterSocketAxes.value, out string primaryMessage);
            bool comparisonReady = true;
            string comparisonMessage = string.Empty;
            if (_abToggle.value && _comparisonDraft != null)
                comparisonReady = _comparisonPreview.SetCharacterContext(character.PrefabPath, character.Package, context.SocketId, clip, context.AnimationLoop, context.AnimationSpeed, _characterVisible.value, _characterSocketAxes.value, out comparisonMessage);
            else _comparisonPreview.ClearCharacterContext();

            bool animationMissing = clip == null;
            bool staleEvidence = !string.IsNullOrWhiteSpace(context.CharacterEvidenceHash)
                && !string.Equals(context.CharacterEvidenceHash, character.EvidenceHash, StringComparison.Ordinal);
            string message = !primaryReady ? primaryMessage : !comparisonReady ? comparisonMessage : animationMissing
                ? "角色与 Socket 已加载，但当前姿势没有可用动画；请选择“自选动画”中的已注册 Clip。"
                : staleEvidence
                    ? "角色包证据已变化；请重新选择角色以更新草稿预览上下文。"
                    : primaryMessage + " 动画与 VFX 共用 60 FPS 时间轴。";
            SetCharacterPreviewStatus(message, !primaryReady || !comparisonReady || animationMissing || staleEvidence);
            MarkPreviewDirty();
        }

        private string ResolveAppliedCharacterPreviewPhase()
        {
            if (IsSpecificLifecyclePhase(_previewStage.value)) return _previewStage.value;
            string current = _preview.CurrentLifecyclePhase.ToString();
            return IsSpecificLifecyclePhase(current) ? current : "Build";
        }

        private void SyncCharacterContextToPreviewPhase()
        {
            ApplyCharacterPreviewContext(false);
            if (!IsSpecificLifecyclePhase(_previewStage.value)
                && _characterPreviewEditTarget.value != "共享默认"
                && string.IsNullOrWhiteSpace(_lockedCharacterContextPhase))
                RefreshCharacterPreviewControls();
        }

        private void SetCharacterPreviewStatus(string message, bool warning)
        {
            _characterPreviewStatus.text = message;
            _characterPreviewStatus.EnableInClassList("status-warning", warning);
        }

        private void ToggleCharacterPreviewContextDrawer()
        {
            SetCharacterPreviewDrawerExpanded(!_characterPreviewContextExpanded);
        }

        private void SetCharacterPreviewDrawerExpanded(bool expanded)
        {
            _characterPreviewContextExpanded = expanded;
            _characterPreviewContext.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshCharacterPreviewContextToggle(IsCharacterPreviewAttached());
        }

        private void RefreshCharacterPreviewContextToggle(bool attached)
        {
            _characterPreviewContextToggle.text = attached ? "角色挂载：已启用" : "角色挂载：关闭";
            _characterPreviewContextToggle.EnableInClassList("character-attached", attached);
            _characterPreviewContextToggle.EnableInClassList("drawer-open", _characterPreviewContextExpanded);
        }

        private bool IsCharacterPreviewAttached()
        {
            if (_session?.Draft == null) return false;
            string phase = ResolveCharacterPreviewEditPhase();
            return ResolveCharacterPreviewContext(phase).Mode == VfxCompositionPreviewContextView.CharacterAttachedMode;
        }

        private VfxCharacterPreviewDescriptor FindCharacter(string stableId, string packageId)
        {
            return _characterChoices.FirstOrDefault(value => string.Equals(value.CharacterStableId, stableId, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(packageId) || string.Equals(value.PackageId, packageId, StringComparison.Ordinal)));
        }

        private static string PosePresetId(string label)
        {
            for (int i = 0; i < VfxCharacterPreviewCatalog.PosePresets.Length; i++)
                if (string.Equals(VfxCharacterPreviewCatalog.PosePresets[i].DisplayName, label, StringComparison.Ordinal)) return VfxCharacterPreviewCatalog.PosePresets[i].Id;
            return "Idle";
        }

        private static string PosePresetLabel(string id)
        {
            for (int i = 0; i < VfxCharacterPreviewCatalog.PosePresets.Length; i++)
                if (string.Equals(VfxCharacterPreviewCatalog.PosePresets[i].Id, id, StringComparison.Ordinal)) return VfxCharacterPreviewCatalog.PosePresets[i].DisplayName;
            return VfxCharacterPreviewCatalog.PosePresets[0].DisplayName;
        }
    }
}
