using MxFramework.Rendering;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Resolved cutout/dissolve inputs shared by offline baking and dynamic triangle coverage.</summary>
    internal static class CharacterTestDirectionalShadowPolicy
    {
        internal static System.Func<System.Collections.Generic.List<UnityEngine.Camera>,bool> CollectPreviewReceiverCameras;
        public static void Configure(MxSceneRenderingConfiguration configuration)
        {
            MxDirectionalShadowCoverage.Resolver=Read;
            MxDirectionalShadowCoverage.OverrideResolver=ApplyOverrides;
            if(configuration!=null)
            {
                configuration.DynamicShadowCasterFilter=IsCaster;
                configuration.CollectDirectionalShadowReceiverCameras=CollectPreviewReceiverCameras;
                if(configuration.StaticDirectionalShadowCastingStateValid==null)
                    configuration.StaticDirectionalShadowCastingStateValid=CaptureVegetationCastingState(configuration);
            }
        }
        private static System.Func<bool> CaptureVegetationCastingState(MxSceneRenderingConfiguration configuration)
        {
            var types=new System.Collections.Generic.List<CharacterTestVegetationType>();
            var states=new System.Collections.Generic.List<bool>();
            if(configuration.Presentations != null)
                foreach(var binding in configuration.Presentations)
                    if(binding?.Data is CharacterTestVegetationData data)
                        foreach(var type in data.Types){types.Add(type);states.Add(type.CastFixedDirectionalShadow);}
            return ()=>
            {
                for(int i=0;i<types.Count;i++)
                    if(types[i]==null || types[i].CastFixedDirectionalShadow!=states[i])return false;
                return true;
            };
        }
        private static bool IsCaster(Renderer renderer)
        {
            // Repository-created socket debug markers and procedural receiver/underlay
            // surfaces are not moving physical casters. Other rigid objects are admitted.
            if(renderer is SkinnedMeshRenderer)return true;
            if(renderer.name=="Marker")return false;
            var parent=renderer.transform.parent;
            return parent==null || !((renderer.name=="Ground" && parent.name=="Tactical Card Demo Scene Root") ||
                (renderer.name=="SurroundingEarth" && parent.name=="PerimeterRockCuts"));
        }
        private static readonly int FoliageColor = Shader.PropertyToID("_WgFoliageColor"), AlphaCutoff = Shader.PropertyToID("_WgAlphaCutoff"),
            ColorId = Shader.PropertyToID("_Color"), CutoffId = Shader.PropertyToID("_Cutoff"), Progress = Shader.PropertyToID("_WgDissolveProgress"),
            NoiseMode = Shader.PropertyToID("_WgDissolveNoiseMode"), MaskId = Shader.PropertyToID("_WgMaskTex"), BaseMap = Shader.PropertyToID("_BaseMap"),
            BaseST = Shader.PropertyToID("_BaseMap_ST"), NoiseId = Shader.PropertyToID("_WgDissolveNoiseTex");
        private sealed class CoverageSchema
        {
            internal bool Foliage, Lite, HasColor, HasCutoff, HasMask, HasNoise, HasProgress, HasNoiseMode, HasBaseMap;
            internal int Color, Cutoff, Mask;
            internal CoverageSchema(Material material)
            {
                Foliage=material.HasProperty(AlphaCutoff);Lite=material.shader.name=="WGame/CharacterStylizedLitLite";
                Color=Foliage?FoliageColor:ColorId;Cutoff=Foliage?AlphaCutoff:CutoffId;Mask=Foliage?MaskId:BaseMap;
                HasColor=material.HasProperty(Color);HasCutoff=material.HasProperty(Cutoff);HasMask=material.HasProperty(Mask);
                HasNoise=material.HasProperty(NoiseId);HasProgress=material.HasProperty(Progress);
                HasNoiseMode=material.HasProperty(NoiseMode);HasBaseMap=material.HasProperty(BaseMap);
            }
        }
        private static readonly System.Collections.Generic.Dictionary<Shader,CoverageSchema> Schemas=
            new System.Collections.Generic.Dictionary<Shader,CoverageSchema>();
        internal static void InvalidateCoverageSchemas()=>Schemas.Clear();
        private static CoverageSchema Schema(Material material)
        {
            var shader=material.shader;
            if(!Schemas.TryGetValue(shader,out var schema))
            {
                if(Schemas.Count>256)Schemas.Clear();
                schema=new CoverageSchema(material);Schemas.Add(shader,schema);
            }
            return schema;
        }
        public static MxDirectionalShadowCoverage Read(Material material, MaterialPropertyBlock objectBlock = null, MaterialPropertyBlock subBlock = null)
        {
            var schema=Schema(material);
            int mode = schema.Foliage ? 1 : material.IsKeywordEnabled("_ALPHATEST_ON") ? 2 : 0;
            var coverage = new MxDirectionalShadowCoverage
            {
                Mode = mode,
                Alpha = schema.HasColor?material.GetColor(schema.Color).a:1,
                Cutoff = schema.HasCutoff?material.GetFloat(schema.Cutoff):0,
                Mask = schema.HasMask?material.GetTexture(schema.Mask)??Texture2D.whiteTexture:Texture2D.whiteTexture,
                Noise = schema.HasNoise?material.GetTexture(NoiseId)??Texture2D.whiteTexture:Texture2D.whiteTexture,
                Dissolve = schema.HasProgress?material.GetFloat(Progress):0,
                // Traditional/prop variants support textured dissolve only.
                DissolveMode = schema.HasNoiseMode ? (int)material.GetFloat(NoiseMode) : 1,
                BaseMapST = new Vector4(1,1,0,0)
            };
            if (schema.HasBaseMap)
            {
                Vector2 scale = material.GetTextureScale("_BaseMap"), offset = material.GetTextureOffset("_BaseMap");
                coverage.BaseMapST = new Vector4(scale.x, scale.y, offset.x, offset.y);
            }
            return ApplyOverrides(material,coverage,objectBlock,subBlock);
        }
        private static MxDirectionalShadowCoverage ApplyOverrides(Material material,MxDirectionalShadowCoverage value,
            MaterialPropertyBlock a,MaterialPropertyBlock b)
        {
            var schema=Schema(material);
            // Unity selects the entire per-material block when present. Missing
            // properties fall back to the material, never to the renderer block.
            if(b!=null&&!b.isEmpty)a=null;
            value.Alpha=ColorAlpha(a,b,schema.Color,value.Alpha);
            value.Cutoff=Float(a,b,schema.Cutoff,value.Cutoff);
            value.Mask=Texture(a,b,schema.Mask,value.Mask);
            value.Noise=Texture(a,b,NoiseId,value.Noise);
            value.Dissolve=Float(a,b,Progress,value.Dissolve);
            if(schema.HasNoiseMode)value.DissolveMode=(int)Float(a,b,NoiseMode,value.DissolveMode);
            if(b!=null&&b.HasVector(BaseST))value.BaseMapST=b.GetVector(BaseST);
            else if(a!=null&&a.HasVector(BaseST))value.BaseMapST=a.GetVector(BaseST);
            if(schema.Lite&&!material.IsKeywordEnabled("_WG_LITE_DISSOLVE_ON"))value.Dissolve=0;
            return value;
        }
        private static float Float(MaterialPropertyBlock a,MaterialPropertyBlock b,int id,float value)=>
            b!=null&&b.HasFloat(id)?b.GetFloat(id):a!=null&&a.HasFloat(id)?a.GetFloat(id):value;
        private static float ColorAlpha(MaterialPropertyBlock a,MaterialPropertyBlock b,int id,float value)=>
            b!=null&&b.HasVector(id)?b.GetColor(id).a:a!=null&&a.HasVector(id)?a.GetColor(id).a:value;
        private static Texture Texture(MaterialPropertyBlock a,MaterialPropertyBlock b,int id,Texture value)=>
            (b!=null&&b.HasTexture(id)?b.GetTexture(id):a!=null&&a.HasTexture(id)?a.GetTexture(id):value)??Texture2D.whiteTexture;
    }
}
