using System;
using System.Collections.Generic;
using MxFramework.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    /// <summary>Reusable UI Toolkit editor for <see cref="MxAttachmentTransform"/> values.</summary>
    /// <remarks>AllocByDesign editor-only control.</remarks>
    public sealed class MxAttachmentTransformField : VisualElement
    {
        private const string StylePackageRelativePath =
            "Editor/MxAttachmentTransformField.uss";

        private static readonly string[] DefaultSocketIds =
        {
            "mainHand",
            "offHand",
            "headVfx",
            "back",
            "foot.L",
            "foot.R",
        };

        private readonly Action<MxAttachmentTransform> _valueChanged;
        private readonly Func<string, string> _resolveSocketStatus;
        private readonly Action<string> _locateSocket;
        private readonly IReadOnlyList<string> _socketIds;
        private readonly Button[] _modeButtons = new Button[3];
        private readonly Button[] _spaceButtons = new Button[2];
        private readonly FloatField[] _positionFields = new FloatField[3];
        private readonly FloatField[] _rotationFields = new FloatField[3];
        private readonly FloatField[] _scaleFields = new FloatField[3];
        private readonly VisualElement _socketRow;
        private readonly TextField _socketField;
        private readonly Label _helperLabel;
        private readonly Label _socketStatusLabel;
        private readonly Label _previewStatusLabel;
        private readonly Button _locateButton;
        private readonly Button _uniformScaleButton;
        private bool _uniformScale;
        private bool _refreshing;
        private MxAttachmentTransform _value;

        public MxAttachmentTransformField(
            in MxAttachmentTransform value,
            Action<MxAttachmentTransform> valueChanged,
            IReadOnlyList<string> socketIds = null,
            Func<string, string> resolveSocketStatus = null,
            Action<string> locateSocket = null)
        {
            _value = MxAttachmentTransform.Normalize(value);
            _valueChanged = valueChanged;
            _socketIds = socketIds ?? Array.Empty<string>();
            _resolveSocketStatus = resolveSocketStatus;
            _locateSocket = locateSocket;
            _uniformScale = AreScalesUniform(_value.Transform);

            name = "mx-attachment-transform-field";
            AddToClassList("mx-attachment");
            StyleSheet sheet = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                StylePackageRelativePath,
                out string stylePath)
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath)
                : null;
            if (sheet != null)
                styleSheets.Add(sheet);

            var foldout = new Foldout
            {
                text = "挂点与变换",
                value = true,
                name = "mx-attachment-foldout",
            };
            Add(foldout);

            var reset = new Button(() => SetValue(MxAttachmentTransform.Default))
            {
                text = "↻  重置",
                name = "mx-attachment-reset",
                tooltip = "恢复为跟随角色、局部偏移和单位缩放。",
            };
            reset.AddToClassList("mx-attachment__reset");
            Add(reset);

            var card = new VisualElement { name = "mx-attachment-card" };
            card.AddToClassList("mx-attachment__card");
            foldout.Add(card);

            card.Add(CreateSectionLabel("锚定方式"));
            var modeSegments = CreateSegmentRow("mx-attachment-mode");
            _modeButtons[0] = AddSegment(modeSegments, "跟随挂点", () => SetMode(MxAttachmentMode.FollowSocket));
            _modeButtons[1] = AddSegment(modeSegments, "跟随角色", () => SetMode(MxAttachmentMode.FollowSubject));
            _modeButtons[2] = AddSegment(modeSegments, "固定世界", () => SetMode(MxAttachmentMode.FixedWorld));
            _modeButtons[0].name = "mx-attachment-mode-socket";
            _modeButtons[1].name = "mx-attachment-mode-subject";
            _modeButtons[2].name = "mx-attachment-mode-world";
            card.Add(modeSegments);

            _helperLabel = new Label { name = "mx-attachment-helper" };
            _helperLabel.AddToClassList("mx-attachment__helper");
            card.Add(_helperLabel);

            card.Add(CreateDivider());
            _socketRow = CreateLabeledRow("挂点");
            _socketField = new TextField
            {
                name = "mx-attachment-socket",
                isDelayed = true,
            };
            _socketField.AddToClassList("mx-attachment__socket-field");
            _socketField.RegisterValueChangedCallback(evt =>
            {
                if (!_refreshing)
                    SetValue(_value.WithSocketId(evt.newValue));
            });
            _socketRow.Add(_socketField);
            _socketRow.Add(new Button(ShowSocketMenu)
            {
                text = "拾取",
                name = "mx-attachment-pick-socket",
                tooltip = "从公共挂点列表中选择。",
            });
            _socketRow.Add(new Button(LocateCurrentSocket)
            {
                text = "◎",
                name = "mx-attachment-locate-socket-icon",
                tooltip = "在当前预览角色中定位挂点。",
            });
            card.Add(_socketRow);

            _socketStatusLabel = new Label { name = "mx-attachment-socket-status" };
            _socketStatusLabel.AddToClassList("mx-attachment__validation");
            card.Add(_socketStatusLabel);

            var spaceRow = CreateLabeledRow("偏移基准");
            var spaceSegments = CreateSegmentRow("mx-attachment-space");
            spaceSegments.AddToClassList("mx-attachment__segments--inline");
            _spaceButtons[0] = AddSegment(spaceSegments, "局部", () => SetSpace(MxTransformSpace.Local));
            _spaceButtons[1] = AddSegment(spaceSegments, "世界", () => SetSpace(MxTransformSpace.World));
            _spaceButtons[0].name = "mx-attachment-space-local";
            _spaceButtons[1].name = "mx-attachment-space-world";
            spaceRow.Add(spaceSegments);
            card.Add(spaceRow);

            card.Add(CreateDivider());
            var transformHeader = new VisualElement();
            transformHeader.AddToClassList("mx-attachment__transform-header");
            transformHeader.Add(CreateSectionLabel("变换偏移"));
            var transformHeaderSpacer = new VisualElement();
            transformHeaderSpacer.style.flexGrow = 1f;
            transformHeader.Add(transformHeaderSpacer);
            _uniformScaleButton = new Button(ToggleUniformScale)
            {
                text = "🔗",
                name = "mx-attachment-uniform-scale",
                tooltip = "等比缩放",
            };
            transformHeader.Add(_uniformScaleButton);
            transformHeader.Add(new Button(ResetTransform)
            {
                text = "归零",
                name = "mx-attachment-reset-transform",
                tooltip = "位置和旋转归零，缩放恢复为 1。",
            });
            card.Add(transformHeader);

            card.Add(CreateVectorRow("位置", "m", _positionFields, SetPositionComponent));
            card.Add(CreateVectorRow("旋转", "°", _rotationFields, SetRotationComponent));
            card.Add(CreateVectorRow("缩放", string.Empty, _scaleFields, SetScaleComponent));

            var previewStrip = new VisualElement();
            previewStrip.AddToClassList("mx-attachment__preview");
            var previewIcon = new Label("●") { tooltip = "当前锚定预览" };
            previewIcon.AddToClassList("mx-attachment__preview-icon");
            previewStrip.Add(previewIcon);
            _previewStatusLabel = new Label { name = "mx-attachment-preview-status" };
            _previewStatusLabel.AddToClassList("mx-attachment__preview-text");
            previewStrip.Add(_previewStatusLabel);
            _locateButton = new Button(LocateCurrentSocket)
            {
                text = "在预览中定位",
                name = "mx-attachment-locate-preview",
            };
            previewStrip.Add(_locateButton);
            card.Add(previewStrip);

            RefreshVisualState();
        }

        public MxAttachmentTransform Value => _value;

        /// <summary>Updates the displayed value without invoking the caller callback.</summary>
        /// <remarks>NoAlloc after control construction.</remarks>
        public void SetValueWithoutNotify(in MxAttachmentTransform value)
        {
            _value = MxAttachmentTransform.Normalize(value);
            _uniformScale = AreScalesUniform(_value.Transform);
            RefreshVisualState();
        }

        private static Label CreateSectionLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("mx-attachment__section-title");
            return label;
        }

        private static VisualElement CreateSegmentRow(string name)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("mx-attachment__segments");
            return row;
        }

        private static Button AddSegment(VisualElement parent, string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("mx-attachment__segment");
            parent.Add(button);
            return button;
        }

        private static VisualElement CreateLabeledRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("mx-attachment__row");
            var rowLabel = new Label(label);
            rowLabel.AddToClassList("mx-attachment__row-label");
            row.Add(rowLabel);
            return row;
        }

        private static VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("mx-attachment__divider");
            return divider;
        }

        private VisualElement CreateVectorRow(
            string label,
            string unit,
            FloatField[] fields,
            Action<int, float> setter)
        {
            var row = CreateLabeledRow(label);
            string[] axes = { "X", "Y", "Z" };
            string[] classes = { "x", "y", "z" };
            for (int i = 0; i < fields.Length; i++)
            {
                int index = i;
                var field = new FloatField(axes[i])
                {
                    name = "mx-attachment-" + label + "-" + axes[i],
                };
                field.AddToClassList("mx-attachment__axis");
                field.AddToClassList("mx-attachment__axis--" + classes[i]);
                field.RegisterValueChangedCallback(evt =>
                {
                    if (!_refreshing)
                        setter(index, evt.newValue);
                });
                fields[i] = field;
                row.Add(field);
            }

            if (!string.IsNullOrEmpty(unit))
            {
                var suffix = new Label(unit);
                suffix.AddToClassList("mx-attachment__unit");
                row.Add(suffix);
            }

            return row;
        }

        private void SetMode(MxAttachmentMode mode) => SetValue(_value.WithMode(mode));

        private void SetSpace(MxTransformSpace space) => SetValue(_value.WithOffsetSpace(space));

        private void SetPositionComponent(int index, float value)
        {
            MxTransformOffset current = _value.Transform;
            SetTransform(new MxTransformOffset(
                index == 0 ? value : current.PositionX,
                index == 1 ? value : current.PositionY,
                index == 2 ? value : current.PositionZ,
                current.RotationEulerX,
                current.RotationEulerY,
                current.RotationEulerZ,
                current.ScaleX,
                current.ScaleY,
                current.ScaleZ));
        }

        private void SetRotationComponent(int index, float value)
        {
            MxTransformOffset current = _value.Transform;
            SetTransform(new MxTransformOffset(
                current.PositionX,
                current.PositionY,
                current.PositionZ,
                index == 0 ? value : current.RotationEulerX,
                index == 1 ? value : current.RotationEulerY,
                index == 2 ? value : current.RotationEulerZ,
                current.ScaleX,
                current.ScaleY,
                current.ScaleZ));
        }

        private void SetScaleComponent(int index, float value)
        {
            value = value <= 0f ? 1f : value;
            MxTransformOffset current = _value.Transform;
            float x = _uniformScale || index == 0 ? value : current.ScaleX;
            float y = _uniformScale || index == 1 ? value : current.ScaleY;
            float z = _uniformScale || index == 2 ? value : current.ScaleZ;
            SetTransform(new MxTransformOffset(
                current.PositionX,
                current.PositionY,
                current.PositionZ,
                current.RotationEulerX,
                current.RotationEulerY,
                current.RotationEulerZ,
                x,
                y,
                z));
        }

        private void SetTransform(MxTransformOffset transform)
        {
            SetValue(_value.WithTransform(transform));
        }

        private void ResetTransform()
        {
            _uniformScale = true;
            SetTransform(MxTransformOffset.Identity);
        }

        private void ToggleUniformScale()
        {
            _uniformScale = !_uniformScale;
            RefreshVisualState();
        }

        private void ShowSocketMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("角色根节点"),
                _value.Mode == MxAttachmentMode.FollowSubject,
                () => SetValue(new MxAttachmentTransform(
                    MxAttachmentMode.FollowSubject,
                    string.Empty,
                    _value.OffsetSpace,
                    _value.Transform)));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddSocketMenuItem(menu, _value.SocketId, seen);
            for (int i = 0; i < _socketIds.Count; i++)
                AddSocketMenuItem(menu, _socketIds[i], seen);
            for (int i = 0; i < DefaultSocketIds.Length; i++)
                AddSocketMenuItem(menu, DefaultSocketIds[i], seen);
            menu.ShowAsContext();
        }

        private void AddSocketMenuItem(GenericMenu menu, string socketId, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(socketId) || !seen.Add(socketId))
                return;

            string captured = socketId;
            menu.AddItem(
                new GUIContent(captured),
                _value.Mode == MxAttachmentMode.FollowSocket
                    && string.Equals(_value.SocketId, captured, StringComparison.Ordinal),
                () => SetValue(new MxAttachmentTransform(
                    MxAttachmentMode.FollowSocket,
                    captured,
                    _value.OffsetSpace,
                    _value.Transform)));
        }

        private void LocateCurrentSocket()
        {
            if (_locateSocket == null || _value.Mode == MxAttachmentMode.FixedWorld)
                return;
            _locateSocket(_value.Mode == MxAttachmentMode.FollowSocket ? _value.SocketId : string.Empty);
        }

        private void SetValue(in MxAttachmentTransform value)
        {
            _value = MxAttachmentTransform.Normalize(value);
            RefreshVisualState();
            _valueChanged?.Invoke(_value);
        }

        private void RefreshVisualState()
        {
            _refreshing = true;
            try
            {
                SetSelected(_modeButtons[0], _value.Mode == MxAttachmentMode.FollowSocket);
                SetSelected(_modeButtons[1], _value.Mode == MxAttachmentMode.FollowSubject);
                SetSelected(_modeButtons[2], _value.Mode == MxAttachmentMode.FixedWorld);
                SetSelected(_spaceButtons[0], _value.OffsetSpace == MxTransformSpace.Local);
                SetSelected(_spaceButtons[1], _value.OffsetSpace == MxTransformSpace.World);
                SetSelected(_uniformScaleButton, _uniformScale);

                _socketRow.SetEnabled(_value.Mode == MxAttachmentMode.FollowSocket);
                _socketField.SetValueWithoutNotify(_value.SocketId ?? string.Empty);
                MxTransformOffset transform = _value.Transform;
                SetVectorWithoutNotify(
                    _positionFields,
                    transform.PositionX,
                    transform.PositionY,
                    transform.PositionZ);
                SetVectorWithoutNotify(
                    _rotationFields,
                    transform.RotationEulerX,
                    transform.RotationEulerY,
                    transform.RotationEulerZ);
                SetVectorWithoutNotify(_scaleFields, transform.ScaleX, transform.ScaleY, transform.ScaleZ);

                _helperLabel.text = BuildHelperText(_value);
                _socketStatusLabel.text = BuildSocketStatus(_value);
                bool valid = !string.IsNullOrWhiteSpace(_socketStatusLabel.text)
                    && !_socketStatusLabel.text.StartsWith("未", StringComparison.Ordinal)
                    && !_socketStatusLabel.text.StartsWith("请", StringComparison.Ordinal);
                _socketStatusLabel.EnableInClassList("mx-attachment__validation--valid", valid);
                _socketStatusLabel.EnableInClassList("mx-attachment__validation--warning", !valid);
                _previewStatusLabel.text = BuildPreviewText(_value);
                _locateButton.SetEnabled(
                    _locateSocket != null
                    && _value.Mode != MxAttachmentMode.FixedWorld
                    && (_value.Mode != MxAttachmentMode.FollowSocket
                        || !string.IsNullOrWhiteSpace(_value.SocketId)));
            }
            finally
            {
                _refreshing = false;
            }
        }

        private string BuildSocketStatus(in MxAttachmentTransform value)
        {
            if (value.Mode == MxAttachmentMode.FixedWorld)
                return "固定世界坐标，不使用角色挂点";
            if (value.Mode == MxAttachmentMode.FollowSubject)
                return "已匹配  角色根节点";
            if (string.IsNullOrWhiteSpace(value.SocketId))
                return "请选择角色挂点";

            string resolved = _resolveSocketStatus?.Invoke(value.SocketId);
            return string.IsNullOrWhiteSpace(resolved)
                ? "未在当前预览角色中验证  " + value.SocketId
                : "已匹配  " + resolved;
        }

        private static string BuildHelperText(in MxAttachmentTransform value)
        {
            switch (value.Mode)
            {
                case MxAttachmentMode.FollowSocket:
                    return value.OffsetSpace == MxTransformSpace.Local
                        ? "特效将随所选骨骼移动，偏移按局部坐标计算"
                        : "特效将随所选骨骼移动，偏移保持世界坐标方向";
                case MxAttachmentMode.FixedWorld:
                    return "特效在触发时解析一次位置，之后保持固定世界坐标";
                default:
                    return value.OffsetSpace == MxTransformSpace.Local
                        ? "特效将随角色根节点移动，偏移按角色局部坐标计算"
                        : "特效将随角色根节点移动，偏移保持世界坐标方向";
            }
        }

        private static string BuildPreviewText(in MxAttachmentTransform value)
        {
            string anchor = value.Mode == MxAttachmentMode.FollowSocket
                ? (string.IsNullOrWhiteSpace(value.SocketId) ? "未选择挂点" : value.SocketId)
                : value.Mode == MxAttachmentMode.FollowSubject
                    ? "角色根节点"
                    : "固定世界";
            string space = value.OffsetSpace == MxTransformSpace.Local ? "局部偏移" : "世界偏移";
            string follow = value.Mode == MxAttachmentMode.FixedWorld ? "不跟随" : "跟随";
            return "预览：" + anchor + " · " + space + " · " + follow;
        }

        private static void SetVectorWithoutNotify(FloatField[] fields, float x, float y, float z)
        {
            fields[0].SetValueWithoutNotify(x);
            fields[1].SetValueWithoutNotify(y);
            fields[2].SetValueWithoutNotify(z);
        }

        private static void SetSelected(VisualElement element, bool selected)
        {
            element?.EnableInClassList("is-selected", selected);
        }

        private static bool AreScalesUniform(in MxTransformOffset transform)
        {
            return Mathf.Approximately(transform.ScaleX, transform.ScaleY)
                && Mathf.Approximately(transform.ScaleY, transform.ScaleZ);
        }
    }
}
