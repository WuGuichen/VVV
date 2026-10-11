using System;
using System.IO;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public sealed partial class MagicaCharacterClothAuthoringWindow
    {
        private ClothSerializeData _runtimeParameterData;

        private void DrawClothParameterOverrides(MagicaCharacterClothAuthoringEntry cloth)
        {
            _runtimeParameterData = null;
            if (!MagicaCharacterClothParameterOverrideUtility.TryLoadPreset(
                    cloth,
                    out ClothSerializeData preset,
                    out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }

            MagicaCharacterClothMotionPreviewController.TryGetActiveClothData(
                cloth.id,
                out _runtimeParameterData);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Cloth 运动调校", _sectionTitleStyle);
                GUILayout.Label(
                    "Preset: " + PresetDisplayName(cloth.presetAssetPath)
                    + "  ·  Schema " + _session.document.schemaVersion,
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(cloth.parameterOverrides == null))
                    if (GUILayout.Button("全部恢复 Preset", GUILayout.Width(112f)))
                        MutateParameter(cloth, "Reset Cloth Parameter Overrides", _ => cloth.parameterOverrides = null);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Preset 基线", _valueBadgeStyle, GUILayout.Width(92f));
                using (BackgroundColor(cloth.parameterOverrides != null ? new Color(0.16f, 0.55f, 0.72f) : GUI.backgroundColor))
                    GUILayout.Label("Draft Override", _valueBadgeStyle, GUILayout.Width(108f));
                if (_runtimeParameterData != null)
                {
                    using (BackgroundColor(new Color(0.22f, 0.58f, 0.32f)))
                        GUILayout.Label(
                            "Run r" + MagicaCharacterClothMotionPreviewController.HotApplyRevision,
                            _valueBadgeStyle,
                            GUILayout.Width(86f));
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "勾选 Override 后覆盖 Preset；Run 为当前运行组件回读值。",
                    EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                ParameterTab("基础", 0);
                ParameterTab("惯性", 1);
                ParameterTab("形状保持", 2);
                ParameterTab("碰撞", 3);
                ParameterTab("风", 4);
                GUILayout.FlexibleSpace();
            }

            _detailScroll = EditorGUILayout.BeginScrollView(
                _detailScroll,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.ExpandHeight(true));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (_clothParameterCategory)
                {
                    case 0:
                    DrawFloatOverride(cloth, "Gravity", preset.gravity, 0f, 20f,
                        value => value.gravity, (value, next) => value.gravity = next);
                    DrawVectorOverride(
                        cloth,
                        "Gravity Direction",
                        new Vector3(preset.gravityDirection.x, preset.gravityDirection.y, preset.gravityDirection.z),
                        value => value.gravityDirection,
                        (value, next) => value.gravityDirection = next);
                    DrawFloatOverride(cloth, "Damping", preset.damping.value, 0f, 1f,
                        value => value.damping, (value, next) => value.damping = next);
                    DrawFloatOverride(cloth, "Blend Weight", preset.blendWeight, 0f, 1f,
                        value => value.blendWeight, (value, next) => value.blendWeight = next);
                        break;
                    case 1:
                    InertiaConstraint.SerializeData inertia = preset.inertiaConstraint;
                    DrawFloatOverride(cloth, "World Inertia", inertia.worldInertia, 0f, 1f,
                        value => value.worldInertia, (value, next) => value.worldInertia = next);
                    DrawFloatOverride(cloth, "Local Inertia", inertia.localInertia, 0f, 1f,
                        value => value.localInertia, (value, next) => value.localInertia = next);
                    DrawFloatOverride(cloth, "World Inertia Smoothing", inertia.movementInertiaSmoothing, 0f, 1f,
                        value => value.movementInertiaSmoothing, (value, next) => value.movementInertiaSmoothing = next);
                    DrawLimitOverride(cloth, "Movement Speed Limit", inertia.movementSpeedLimit.use, inertia.movementSpeedLimit.value, 0f, 10f,
                        value => value.movementSpeedLimit, (value, next) => value.movementSpeedLimit = next);
                    DrawLimitOverride(cloth, "Rotation Speed Limit", inertia.rotationSpeedLimit.use, inertia.rotationSpeedLimit.value, 0f, 1440f,
                        value => value.rotationSpeedLimit, (value, next) => value.rotationSpeedLimit = next);
                    DrawLimitOverride(cloth, "Particle Speed Limit", inertia.particleSpeedLimit.use, inertia.particleSpeedLimit.value, 0f, 10f,
                        value => value.particleSpeedLimit, (value, next) => value.particleSpeedLimit = next);
                    DrawFloatOverride(cloth, "Teleport Distance", inertia.teleportDistance, 0f, 10f,
                        value => value.teleportDistance, (value, next) => value.teleportDistance = next);
                    DrawFloatOverride(cloth, "Teleport Rotation", inertia.teleportRotation, 0f, 360f,
                        value => value.teleportRotation, (value, next) => value.teleportRotation = next);
                        break;
                    case 2:
                    DrawFloatOverride(cloth, "Distance Stiffness", preset.distanceConstraint.stiffness.value, 0f, 1f,
                        value => value.distanceStiffness, (value, next) => value.distanceStiffness = next);
                    DrawLimitOverride(
                        cloth,
                        "Angle Restoration Stiffness",
                        preset.angleRestorationConstraint.useAngleRestoration,
                        preset.angleRestorationConstraint.stiffness.value,
                        0f,
                        1f,
                        value => value.angleRestorationStiffness,
                        (value, next) => value.angleRestorationStiffness = next);
                    DrawFloatOverride(cloth, "Velocity Attenuation", preset.angleRestorationConstraint.velocityAttenuation, 0f, 1f,
                        value => value.velocityAttenuation, (value, next) => value.velocityAttenuation = next);
                    DrawLimitOverride(
                        cloth,
                        "Angle Limit",
                        preset.angleLimitConstraint.useAngleLimit,
                        preset.angleLimitConstraint.limitAngle.value,
                        0f,
                        180f,
                        value => value.angleLimit,
                        (value, next) => value.angleLimit = next);
                    DrawLimitOverride(
                        cloth,
                        "Max Distance",
                        preset.motionConstraint.useMaxDistance,
                        preset.motionConstraint.maxDistance.value,
                        0f,
                        5f,
                        value => value.maxDistance,
                        (value, next) => value.maxDistance = next);
                        bool boneSpringShape = string.Equals(cloth.clothType, "BoneSpring", StringComparison.Ordinal);
                        using (new EditorGUI.DisabledScope(!boneSpringShape))
                        {
                            DrawLimitOverride(cloth, "Spring Power", preset.springConstraint.useSpring, preset.springConstraint.springPower, 0.001f, 1f,
                                value => value.springPower, (value, next) => value.springPower = next);
                            DrawFloatOverride(cloth, "Spring Limit Distance", preset.springConstraint.limitDistance, 0f, 5f,
                                value => value.springLimitDistance, (value, next) => value.springLimitDistance = next);
                        }
                        if (!boneSpringShape)
                            EditorGUILayout.HelpBox("Spring 参数仅 BoneSpring 生效。", MessageType.None);
                        break;
                    case 3:
                    DrawFloatOverride(cloth, "Collision Friction", preset.colliderCollisionConstraint.friction, 0f, 0.5f,
                        value => value.collisionFriction, (value, next) => value.collisionFriction = next);
                    bool boneSpring = string.Equals(cloth.clothType, "BoneSpring", StringComparison.Ordinal);
                    using (new EditorGUI.DisabledScope(!boneSpring))
                        DrawFloatOverride(cloth, "Collision Limit Distance", preset.colliderCollisionConstraint.limitDistance.value, 0f, 1f,
                            value => value.collisionLimitDistance, (value, next) => value.collisionLimitDistance = next);
                    if (!boneSpring)
                        EditorGUILayout.HelpBox("Collision Limit Distance 仅 BoneSpring 求解路径使用；当前 BoneCloth 不生效。", MessageType.None);
                    bool selfCollision = preset.selfCollisionConstraint.selfMode
                        != SelfCollisionConstraint.SelfCollisionMode.None;
                    using (new EditorGUI.DisabledScope(!selfCollision))
                        DrawFloatOverride(cloth, "Surface Thickness", preset.selfCollisionConstraint.surfaceThickness.value, 0.001f, 0.05f,
                            value => value.surfaceThickness, (value, next) => value.surfaceThickness = next);
                    if (!selfCollision)
                        EditorGUILayout.HelpBox("当前 Preset 未启用 Self Collision，Surface Thickness 不参与求解。", MessageType.None);
                        break;
                    case 4:
                    if (!MagicaCharacterClothMotionPreviewController.HasActivePreviewWind)
                        EditorGUILayout.HelpBox("当前没有启用预览风源；参数会热应用，但需要在运动预览中打开“预览风源”才能看到风响应。", MessageType.Warning);
                    DrawFloatOverride(cloth, "Wind Influence", preset.wind.influence, 0f, 2f,
                        value => value.windInfluence, (value, next) => value.windInfluence = next);
                    DrawFloatOverride(cloth, "Wind Frequency", preset.wind.frequency, 0f, 2f,
                        value => value.windFrequency, (value, next) => value.windFrequency = next);
                    DrawFloatOverride(cloth, "Wind Turbulence", preset.wind.turbulence, 0f, 2f,
                        value => value.windTurbulence, (value, next) => value.windTurbulence = next);
                        break;
                }
            }
            EditorGUILayout.EndScrollView();
            _runtimeParameterData = null;
        }

        private void DrawFloatOverride(
            MagicaCharacterClothAuthoringEntry cloth,
            string label,
            float presetValue,
            float min,
            float max,
            Func<MagicaCharacterClothParameterOverrides, MagicaCharacterClothFloatOverride> getter,
            Action<MagicaCharacterClothParameterOverrides, MagicaCharacterClothFloatOverride> setter)
        {
            MagicaCharacterClothFloatOverride current =
                cloth.parameterOverrides != null ? getter(cloth.parameterOverrides) : null;
            bool enabled = current != null;
            using (new EditorGUILayout.HorizontalScope(enabled ? EditorStyles.helpBox : GUIStyle.none))
            {
                bool nextEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18f));
                GUILayout.Label(label, GUILayout.Width(124f));
                if (nextEnabled != enabled)
                {
                    MutateParameter(cloth, "Toggle " + label + " Override", value =>
                        setter(value, nextEnabled ? new MagicaCharacterClothFloatOverride(presetValue) : null));
                    return;
                }

                float effective = enabled ? current.value : presetValue;
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    float next = EditorGUILayout.Slider(effective, min, max, GUILayout.MinWidth(118f));
                    if (enabled && !Mathf.Approximately(next, current.value))
                    {
                        MutateParameter(cloth, "Edit " + label + " Override", value =>
                            setter(value, new MagicaCharacterClothFloatOverride(next)));
                    }
                }
                string runtime = TryGetRuntimeFloat(label, out float runtimeValue)
                    ? runtimeValue.ToString("0.###")
                    : "—";
                GUILayout.Label("Preset " + presetValue.ToString("0.###"), _valueBadgeStyle, GUILayout.Width(78f));
                GUILayout.Label("Draft " + effective.ToString("0.###"), _valueBadgeStyle, GUILayout.Width(78f));
                GUILayout.Label("Run " + runtime, _valueBadgeStyle, GUILayout.Width(78f));
                DrawRuntimeStatus(_runtimeParameterData != null, _runtimeParameterData != null && Mathf.Approximately(runtimeValue, effective));
            }
        }

        private void DrawLimitOverride(
            MagicaCharacterClothAuthoringEntry cloth,
            string label,
            bool presetUse,
            float presetValue,
            float min,
            float max,
            Func<MagicaCharacterClothParameterOverrides, MagicaCharacterClothLimitOverride> getter,
            Action<MagicaCharacterClothParameterOverrides, MagicaCharacterClothLimitOverride> setter)
        {
            MagicaCharacterClothLimitOverride current =
                cloth.parameterOverrides != null ? getter(cloth.parameterOverrides) : null;
            bool enabled = current != null;
            using (new EditorGUILayout.HorizontalScope(enabled ? EditorStyles.helpBox : GUIStyle.none))
            {
                bool nextEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18f));
                GUILayout.Label(label, GUILayout.Width(124f));
                if (nextEnabled != enabled)
                {
                    MutateParameter(cloth, "Toggle " + label + " Override", value =>
                        setter(value, nextEnabled ? new MagicaCharacterClothLimitOverride(presetUse, presetValue) : null));
                    return;
                }

                bool use = enabled ? current.use : presetUse;
                float effective = enabled ? current.value : presetValue;
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    bool nextUse = GUILayout.Toggle(use, "启用", GUILayout.Width(48f));
                    float next = EditorGUILayout.Slider(effective, min, max, GUILayout.MinWidth(92f));
                    if (enabled && (nextUse != current.use || !Mathf.Approximately(next, current.value)))
                    {
                        MutateParameter(cloth, "Edit " + label + " Override", value =>
                            setter(value, new MagicaCharacterClothLimitOverride(nextUse, next)));
                    }
                }
                string runtime = TryGetRuntimeLimit(label, out bool runtimeUse, out float runtimeValue)
                    ? (runtimeUse ? "On " : "Off ") + runtimeValue.ToString("0.###")
                    : "—";
                GUILayout.Label("Preset " + (presetUse ? "On " : "Off ") + presetValue.ToString("0.###"), _valueBadgeStyle, GUILayout.Width(92f));
                GUILayout.Label("Draft " + (use ? "On " : "Off ") + effective.ToString("0.###"), _valueBadgeStyle, GUILayout.Width(92f));
                GUILayout.Label("Run " + runtime, _valueBadgeStyle, GUILayout.Width(92f));
                bool applied = _runtimeParameterData != null && runtimeUse == use && Mathf.Approximately(runtimeValue, effective);
                DrawRuntimeStatus(_runtimeParameterData != null, applied);
            }
        }

        private void DrawVectorOverride(
            MagicaCharacterClothAuthoringEntry cloth,
            string label,
            Vector3 presetValue,
            Func<MagicaCharacterClothParameterOverrides, MagicaCharacterClothAuthoringVector> getter,
            Action<MagicaCharacterClothParameterOverrides, MagicaCharacterClothAuthoringVector> setter)
        {
            MagicaCharacterClothAuthoringVector current =
                cloth.parameterOverrides != null ? getter(cloth.parameterOverrides) : null;
            bool enabled = current != null;
            using (new EditorGUILayout.HorizontalScope(enabled ? EditorStyles.helpBox : GUIStyle.none))
            {
                bool nextEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18f));
                if (nextEnabled != enabled)
                {
                    MutateParameter(cloth, "Toggle " + label + " Override", value =>
                        setter(value, nextEnabled
                            ? new MagicaCharacterClothAuthoringVector(presetValue.x, presetValue.y, presetValue.z)
                            : null));
                    return;
                }

                Vector3 effective = enabled
                    ? new Vector3(current.x, current.y, current.z)
                    : presetValue;
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 next = EditorGUILayout.Vector3Field(label, effective);
                    bool changed = EditorGUI.EndChangeCheck();
                    if (enabled && changed)
                    {
                        MutateParameter(cloth, "Edit " + label + " Override", value =>
                            setter(value, new MagicaCharacterClothAuthoringVector(next.x, next.y, next.z)));
                    }
                }
                string runtime = _runtimeParameterData != null
                    ? $"({_runtimeParameterData.gravityDirection.x:0.##},{_runtimeParameterData.gravityDirection.y:0.##},{_runtimeParameterData.gravityDirection.z:0.##})"
                    : "—";
                GUILayout.Label($"Preset ({presetValue.x:0.##},{presetValue.y:0.##},{presetValue.z:0.##})", _valueBadgeStyle, GUILayout.Width(108f));
                GUILayout.Label($"Draft ({effective.x:0.##},{effective.y:0.##},{effective.z:0.##})", _valueBadgeStyle, GUILayout.Width(108f));
                GUILayout.Label("Run " + runtime, _valueBadgeStyle, GUILayout.Width(108f));
                bool applied = _runtimeParameterData != null
                    && Vector3.Distance(
                        new Vector3(_runtimeParameterData.gravityDirection.x, _runtimeParameterData.gravityDirection.y, _runtimeParameterData.gravityDirection.z),
                        effective.normalized) < 0.001f;
                DrawRuntimeStatus(_runtimeParameterData != null, applied);
            }
        }

        private void MutateParameter(
            MagicaCharacterClothAuthoringEntry cloth,
            string label,
            Action<MagicaCharacterClothParameterOverrides> mutation)
        {
            Mutate(label, () =>
            {
                _session.document.schemaVersion = "1.1";
                MagicaCharacterClothParameterOverrides value =
                    cloth.parameterOverrides ??= new MagicaCharacterClothParameterOverrides();
                mutation(value);
                if (cloth.parameterOverrides?.IsEmpty() == true)
                    cloth.parameterOverrides = null;
            });
        }

        private bool TryGetRuntimeFloat(string label, out float value)
        {
            value = 0f;
            ClothSerializeData data = _runtimeParameterData;
            if (data == null) return false;
            switch (label)
            {
                case "Gravity": value = data.gravity; return true;
                case "Damping": value = data.damping.value; return true;
                case "Blend Weight": value = data.blendWeight; return true;
                case "World Inertia": value = data.inertiaConstraint.worldInertia; return true;
                case "Local Inertia": value = data.inertiaConstraint.localInertia; return true;
                case "World Inertia Smoothing": value = data.inertiaConstraint.movementInertiaSmoothing; return true;
                case "Teleport Distance": value = data.inertiaConstraint.teleportDistance; return true;
                case "Teleport Rotation": value = data.inertiaConstraint.teleportRotation; return true;
                case "Distance Stiffness": value = data.distanceConstraint.stiffness.value; return true;
                case "Velocity Attenuation": value = data.angleRestorationConstraint.velocityAttenuation; return true;
                case "Collision Friction": value = data.colliderCollisionConstraint.friction; return true;
                case "Collision Limit Distance": value = data.colliderCollisionConstraint.limitDistance.value; return true;
                case "Surface Thickness": value = data.selfCollisionConstraint.surfaceThickness.value; return true;
                case "Wind Influence": value = data.wind.influence; return true;
                case "Wind Frequency": value = data.wind.frequency; return true;
                case "Wind Turbulence": value = data.wind.turbulence; return true;
                case "Spring Limit Distance": value = data.springConstraint.limitDistance; return true;
                default: return false;
            }
        }

        private void DrawRuntimeStatus(bool hasRuntime, bool applied)
        {
            if (!hasRuntime)
            {
                using (ContentColor(new Color(0.60f, 0.60f, 0.60f)))
                    GUILayout.Label("○ 未预览", _appliedBadgeStyle, GUILayout.Width(68f));
                return;
            }
            if (applied)
            {
                using (ContentColor(new Color(0.32f, 0.85f, 0.42f)))
                    GUILayout.Label("✓ 已应用", _appliedBadgeStyle, GUILayout.Width(68f));
            }
            else
            {
                using (ContentColor(new Color(0.95f, 0.68f, 0.24f)))
                    GUILayout.Label("… 等待", _appliedBadgeStyle, GUILayout.Width(68f));
            }
        }

        private bool TryGetRuntimeLimit(string label, out bool use, out float value)
        {
            use = false;
            value = 0f;
            ClothSerializeData data = _runtimeParameterData;
            if (data == null) return false;
            switch (label)
            {
                case "Movement Speed Limit":
                    use = data.inertiaConstraint.movementSpeedLimit.use;
                    value = data.inertiaConstraint.movementSpeedLimit.value;
                    return true;
                case "Rotation Speed Limit":
                    use = data.inertiaConstraint.rotationSpeedLimit.use;
                    value = data.inertiaConstraint.rotationSpeedLimit.value;
                    return true;
                case "Particle Speed Limit":
                    use = data.inertiaConstraint.particleSpeedLimit.use;
                    value = data.inertiaConstraint.particleSpeedLimit.value;
                    return true;
                case "Angle Restoration Stiffness":
                    use = data.angleRestorationConstraint.useAngleRestoration;
                    value = data.angleRestorationConstraint.stiffness.value;
                    return true;
                case "Angle Limit":
                    use = data.angleLimitConstraint.useAngleLimit;
                    value = data.angleLimitConstraint.limitAngle.value;
                    return true;
                case "Max Distance":
                    use = data.motionConstraint.useMaxDistance;
                    value = data.motionConstraint.maxDistance.value;
                    return true;
                case "Spring Power":
                    use = data.springConstraint.useSpring;
                    value = data.springConstraint.springPower;
                    return true;
                default:
                    return false;
            }
        }
    }
}
