using System;
using System.Linq;
using MxFramework.Rendering;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestSceneRenderingMigration
    {
        internal static void Migrate(GameManager owner, CharacterTestSceneRenderingLevelEditorModule module)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var scene = owner.gameObject.scene;
            var lights = UnityEngine.Object.FindObjectsByType<MxSceneLightingScene>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene).ToArray();
            var fogs = UnityEngine.Object.FindObjectsByType<MxHeightFogScene>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene).ToArray();
            if (lights.Length == 0 && fogs.Length == 0 && !CharacterTestSceneRenderingLevelEditorModule.IsUnconfigured(owner.SceneRendering)) return;
            if (!CharacterTestSceneRenderingLevelEditorModule.IsUnconfigured(owner.SceneRendering) || lights.Length != 1 || fogs.Length != 1)
                throw new InvalidOperationException("迁移需要当前场景唯一的旧光照与高度雾入口，且 GameManager 尚未配置渲染。");
            var light = lights[0]; var fog = fogs[0];
            if (light.TargetCamera != fog.TargetCamera || light.Feature != fog.Feature)
                throw new InvalidOperationException("原光照与雾绑定了不同相机或渲染入口，请先统一后再迁移。");
            bool lightEnabled = light.isActiveAndEnabled, fogEnabled = fog.isActiveAndEnabled;
            var oldLayout = light.SourceLayout;
            var layout = new MxSceneLightingSourceLayout
            {
                BaseColorProperty = oldLayout.BaseColorProperty,
                SurfaceDataProperty = oldLayout.SurfaceDataProperty,
                EmissionIntensityProperty = oldLayout.EmissionIntensityProperty,
                SolidColorProperty = oldLayout.SolidColorProperty,
                SecondaryColorProperty = oldLayout.SecondaryColorProperty,
                SecondaryBlendProperty = oldLayout.SecondaryBlendProperty,
                DiffuseFractions = (float[])oldLayout.DiffuseFractions.Clone()
            };
            var c = new MxSceneRenderingConfiguration
            {
                IndirectLightingEnabled = lightEnabled,
                HeightFogEnabled = fogEnabled,
                StaticRoot = light.StaticRoot, Sun = light.Sun, TargetCamera = light.TargetCamera,
                Feature = light.Feature, Data = light.Data, Look = light.Look, SourceLayout = layout,
                Compute = light.Compute, CaptureShader = light.CaptureShader, ResolveShader = light.ResolveShader,
                FogCompositeShader = fog.CompositeShader,
                PreviewIndirectInSceneView = light.PreviewInSceneView,
                PreviewFogInSceneView = fog.PreviewInSceneView,
                RequireMotionVectors = light.RequireMotionVectors,
                FogColor = fog.FogColor, FogDensity = fog.Density, FogBaseHeight = fog.BaseHeight,
                FogHeightFalloff = fog.HeightFalloff, FogMaxDistance = fog.MaxDistance,
                FogScatteringIntensity = fog.ScatteringIntensity,
                FogFocusClearRadius = fog.FocusClearRadius,
                FogFocusClearSoftness = fog.FocusClearSoftness
            };
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("整合场景渲染到 GameManager");
            try
            {
                Undo.RecordObject(light, "停用旧光照入口"); light.enabled = false;
                Undo.RecordObject(fog, "停用旧高度雾入口"); fog.enabled = false;
                Undo.RecordObject(owner, "迁移场景渲染配置"); owner.SetSceneRendering(c);
                RemoveLegacyRoot(light.gameObject, light);
                RemoveLegacyRoot(fog.gameObject, fog);
                Undo.CollapseUndoOperations(group);
                CharacterTestSceneRenderingPreview.RefreshNow();
                module?.OnMigrated();
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                CharacterTestSceneRenderingPreview.RefreshNow();
                throw;
            }
        }

        private static void RemoveLegacyRoot(GameObject go, Component component)
        {
            if (go == null || component == null) return;
            if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2)
                Undo.DestroyObjectImmediate(go);
            else Undo.DestroyObjectImmediate(component);
        }
    }
}
