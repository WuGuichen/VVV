using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using MxFramework.Rendering.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    public static class CharacterTestDirectionalShadowAuthoring
    {
        static CharacterTestDirectionalShadowAuthoring()
        {
            CharacterTestDirectionalShadowPolicy.CollectPreviewReceiverCameras=CollectPreviewReceiverCameras;
            EditorApplication.projectChanged+=CharacterTestDirectionalShadowPolicy.InvalidateCoverageSchemas;
            CharacterTestDirectionalShadowPolicy.Configure(null);
        }
        private static bool CollectPreviewReceiverCameras(List<UnityEngine.Camera> cameras)
        {
            foreach(SceneView view in SceneView.sceneViews)
                if(view!=null&&view.camera!=null&&!cameras.Contains(view.camera))cameras.Add(view.camera);
            return true;
        }
        public const string ComputePath = "Packages/com.vincent.wgameframework/Rendering/DirectionalTriangleShadow.compute";
        [MenuItem("Tools/MxFramework/Rendering/Bake Fixed Directional Shadows")]
        public static void BakeActiveScene()
        {
            foreach (var owner in UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None))
                if(owner.gameObject.scene==SceneManager.GetActiveScene()){Bake(owner);return;}
            throw new InvalidOperationException("Active scene has no GameManager rendering owner.");
        }
        public static MxDirectionalShadowData Bake(GameManager owner, int resolution=4096)=>Bake(owner,resolution,false);
        public static MxDirectionalShadowData Bake(GameManager owner,int resolution,bool centerOnly)
        {
            var data=owner.SceneRendering?.DirectionalShadowData;
            var plane=data!=null&&data.HasPlaneCoverage?data.PlaneCoverage:Vector4.zero;
            if(plane.x==1)plane.w=Mathf.Max(0,owner.SceneRendering.DirectionalShadowBias);
            return BakeInternal(owner,resolution,centerOnly,plane);
        }
        /// <summary>Explicit opt-in for a known fixed horizontal ground. First activation rebuilds both regions.</summary>
        public static MxDirectionalShadowData BakeWithPlaneCoverage(GameManager owner,float planeY,float planeTolerance=.001f,
            int resolution=4096,bool centerOnly=false)
        {
            var c=owner.SceneRendering;
            if(c==null)throw new InvalidOperationException("Missing scene rendering configuration.");
            return BakeInternal(owner,resolution,centerOnly,new Vector4(1,planeY,planeTolerance,Mathf.Max(0,c.DirectionalShadowBias)));
        }
        private static MxDirectionalShadowData BakeInternal(GameManager owner,int resolution,bool centerOnly,Vector4 planeCoverage)
        {
            var c=owner.SceneRendering;
            if(EditorApplication.isPlaying || c==null || c.StaticRoot==null || c.Sun==null || string.IsNullOrEmpty(owner.gameObject.scene.path))
                throw new InvalidOperationException("Use a saved Edit-mode scene with a static root and fixed sun.");
            Bounds bounds=ReceiverBounds(c);
            var sources=Sources(owner);
            string folder="Assets/Config/MxFramework/Rendering/DirectionalShadows/"+owner.gameObject.scene.name;
            if(centerOnly && (c.DirectionalShadowData==null || !c.DirectionalShadowData.IsValid))
                throw new InvalidOperationException("Center-only baking requires an existing valid peripheral field.");
            var data=MxDirectionalShadowBaker.Bake(sources,bounds,CenterReceiverBounds(c),-c.Sun.transform.forward,folder,resolution,
                c.DirectionalShadowCenterResolution,centerOnly?c.DirectionalShadowData:null,planeCoverage);
            Undo.RecordObject(owner,"烘焙真实轮廓固定阴影");
            c.DirectionalShadowData=data;c.DirectionalShadowCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            c.DirectionalShadowsEnabled=true;
            c.StaticDirectionalShadowCastingStateValid=null;
            CharacterTestDirectionalShadowPolicy.Configure(c);
            EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            CharacterTestSceneRenderingPreview.Session?.SetDirectionalShadowSourceValidation(true,string.Empty);
            CharacterTestSceneRenderingPreview.Session?.RebindDirectionalShadows();
            SceneView.RepaintAll();return data;
        }
        public static Bounds ReceiverBounds(MxSceneRenderingConfiguration c)=>c.Data!=null?c.Data.Bounds:
            c.UseProbeCoverageBounds?c.ProbeCoverageBounds:new Bounds(c.StaticRoot.position,new Vector3(160,24,160));
        public static Bounds CenterReceiverBounds(MxSceneRenderingConfiguration c)
        {
            var full=ReceiverBounds(c);
            float minX=Mathf.Max(full.min.x,-TacticalCard.TacticalBattlefieldLayout.HalfWidth);
            float maxX=Mathf.Min(full.max.x,TacticalCard.TacticalBattlefieldLayout.HalfWidth);
            float minZ=Mathf.Max(full.min.z,-TacticalCard.TacticalBattlefieldLayout.HalfDepth);
            float maxZ=Mathf.Min(full.max.z,TacticalCard.TacticalBattlefieldLayout.HalfDepth);
            if(minX>=maxX||minZ>=maxZ)throw new InvalidOperationException("Receiver coverage does not include the fixed battlefield.");
            float scale=c.DirectionalShadowCenterScale>0?Mathf.Clamp(c.DirectionalShadowCenterScale,.1f,1f):1f;
            return new Bounds(new Vector3((minX+maxX)*.5f,full.center.y,(minZ+maxZ)*.5f),
                new Vector3((maxX-minX)*scale,full.size.y,(maxZ-minZ)*scale));
        }
        private static System.Collections.Generic.IEnumerable<CharacterTestVegetationSource> VegetationSources(UnityEngine.SceneManagement.Scene scene)
        {
            var source = CharacterTestVegetationAssetAuthoring.Find(scene);
            if (source != null) yield return source;
        }
        public static List<MxShadowBakeSource> Sources(GameManager owner)
        {
            CharacterTestDirectionalShadowPolicy.Configure(owner.SceneRendering);
            var sources=new List<MxShadowBakeSource>();
            var objectProperties=new MaterialPropertyBlock();var subProperties=new MaterialPropertyBlock();
            foreach(var renderer in owner.SceneRendering.StaticRoot.GetComponentsInChildren<MeshRenderer>(false))
            {
                if(!renderer.enabled || renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off)continue;
                Mesh mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
                Material[] materials=renderer.sharedMaterials;renderer.GetPropertyBlock(objectProperties);
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    Material material=sub<materials.Length?materials[sub]:null;
                    if(!MxDirectionalShadowCoverage.CanCast(material))continue;
                    renderer.GetPropertyBlock(subProperties,sub);
                    sources.Add(new MxShadowBakeSource(mesh,material,renderer.localToWorldMatrix,sub,objectProperties,subProperties,renderer.shadowCastingMode));
                }
            }
            // Scene-owned instances have no per-clump MeshRenderer. Include every reference pose, independently of camera visibility.
            foreach(var layout in VegetationSources(owner.gameObject.scene))
            {
                if(layout.gameObject.scene!=owner.gameObject.scene || !layout.isActiveAndEnabled)continue;
                var types=new Dictionary<int,CharacterTestVegetationType>();foreach(var type in layout.Types)types.Add(type.Id,type);
                foreach(var instance in layout.Instances)
                {
                    if(!types.TryGetValue(instance.TypeId,out var type) || type.Prefab==null)throw new InvalidOperationException("Invalid vegetation source type.");
                    if(!type.CastFixedDirectionalShadow)continue;
                    Mesh mesh=type.Prefab.GetComponent<MeshFilter>()?.sharedMesh;Material material=type.Prefab.GetComponent<MeshRenderer>()?.sharedMaterial;
                    if(mesh==null || material==null)throw new InvalidOperationException("Missing vegetation mesh or alpha material.");
                    // This exact shader has a reference red-channel/vertex-alpha
                    // coverage contract and intentionally has no mesh ShadowCaster pass.
                    bool explicitCoverage=material.shader!=null && material.shader.name=="WGame/FoliageMossLit";
                    if(!MxDirectionalShadowCoverage.CanCast(material,explicitCoverage))continue;
                    Matrix4x4 matrix=layout.transform.localToWorldMatrix*Matrix4x4.TRS(instance.Position,instance.Rotation.normalized,instance.Scale);
                    sources.Add(new MxShadowBakeSource(mesh,material,matrix,0,null,null,UnityEngine.Rendering.ShadowCastingMode.On,explicitCoverage));
                }
            }
            return sources;
        }
        public static bool Validate(GameManager owner,out string issue)
        {
            var c=owner.SceneRendering;if(c==null || !c.DirectionalShadowsEnabled){issue=string.Empty;return true;}
            if(c.DirectionalShadowData==null || !c.DirectionalShadowData.IsValid || c.StaticRoot==null || c.Sun==null)
            {issue="Directional shadow data or bindings are missing.";return false;}
            try
            {
                CharacterTestDirectionalShadowPolicy.Configure(c);
                if(c.DynamicDirectionalShadows)
                    foreach(var root in owner.gameObject.scene.GetRootGameObjects())
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if(renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off ||
                                (renderer is MeshRenderer && renderer.transform.IsChildOf(c.StaticRoot)) ||
                                (c.DynamicShadowCasterFilter!=null&&!c.DynamicShadowCasterFilter(renderer)))continue;
                            var skinned=renderer as SkinnedMeshRenderer;
                            Mesh mesh=skinned!=null?skinned.sharedMesh:(renderer as MeshRenderer)?.GetComponent<MeshFilter>()?.sharedMesh;
                            if(mesh==null||mesh.isReadable)continue;
                            bool casts=false;
                            foreach(var material in renderer.sharedMaterials)
                                if(MxDirectionalShadowCoverage.CanCast(material)){casts=true;break;}
                            if(!casts)continue;
                            issue="Dynamic shadow mesh requires Read/Write Enabled: "+renderer.name+" ("+AssetDatabase.GetAssetPath(mesh)+").";
                            return false;
                        }
                var data=c.DirectionalShadowData;
                var plane=data.PlaneCoverage;
                if(plane.x==1 && Mathf.Abs(plane.w-Mathf.Max(0,c.DirectionalShadowBias))>1e-6f)
                { issue="The static plane-coverage bias changed. Rebuild both directional-shadow regions.";return false; }
                string fingerprint=MxDirectionalShadowBaker.Fingerprint(Sources(owner),ReceiverBounds(c),CenterReceiverBounds(c),
                    -c.Sun.transform.forward,data.TextureBudgetResolution,c.DirectionalShadowCenterResolution,plane);
                if(fingerprint!=data.SourceFingerprint){issue="Static geometry, alpha, vegetation layout, coverage or fixed sun changed. Rebuild directional shadows.";return false;}
                issue=string.Empty;return true;
            }
            catch(Exception exception){issue=exception.Message;return false;}
        }
    }
    internal sealed class CharacterTestDirectionalShadowBuildValidation:IProcessSceneWithReport
    {
        public int callbackOrder=>100;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null)return;
            foreach(var root in scene.GetRootGameObjects())foreach(var owner in root.GetComponentsInChildren<GameManager>(true))
                if(!CharacterTestDirectionalShadowAuthoring.Validate(owner,out string issue))throw new BuildFailedException(issue);
        }
    }
}
