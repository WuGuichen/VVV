using System;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Resolved surface coverage. The scene composition supplies shader-specific policy.</summary>
    public struct MxDirectionalShadowCoverage
    {
        /// <summary>Shared caster admission. Explicit coverage is reserved for authored sources without a mesh pass.</summary>
        public static bool CanCast(Material material, bool hasExplicitCoverage = false) =>
            material != null && material.renderQueue < 3000 && material.GetShaderPassEnabled("ShadowCaster") &&
            (hasExplicitCoverage || material.FindPass("ShadowCaster") >= 0);

        public int Mode, DissolveMode;
        public float Alpha, Cutoff, Dissolve;
        public Vector4 BaseMapST;
        public Texture Mask, Noise;
        public static Func<Material,MaterialPropertyBlock,MaterialPropertyBlock,MxDirectionalShadowCoverage> Resolver;
        public static Func<Material,MxDirectionalShadowCoverage,MaterialPropertyBlock,MaterialPropertyBlock,MxDirectionalShadowCoverage> OverrideResolver;
        public static MxDirectionalShadowCoverage ApplyOverrides(Material material,MxDirectionalShadowCoverage baseline,
            MaterialPropertyBlock objectBlock,MaterialPropertyBlock subBlock)
        {
            if((objectBlock==null||objectBlock.isEmpty)&&(subBlock==null||subBlock.isEmpty))return baseline;
            return OverrideResolver!=null?OverrideResolver(material,baseline,objectBlock,subBlock):Read(material,objectBlock,subBlock);
        }
        public static MxDirectionalShadowCoverage Read(Material material,MaterialPropertyBlock objectBlock=null,MaterialPropertyBlock subBlock=null)
        {
            if(Resolver!=null)return Resolver(material,objectBlock,subBlock);
            string color=material.HasProperty("_BaseColor")?"_BaseColor":"_Color";
            var value=new MxDirectionalShadowCoverage{
                Mode=material.IsKeywordEnabled("_ALPHATEST_ON")?2:0,
                Alpha=material.HasProperty(color)?material.GetColor(color).a:1,
                Cutoff=material.HasProperty("_Cutoff")?material.GetFloat("_Cutoff"):0,
                Mask=material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap")??Texture2D.whiteTexture:Texture2D.whiteTexture,
                Noise=Texture2D.whiteTexture,BaseMapST=new Vector4(1,1,0,0)
            };
            if(material.HasProperty("_BaseMap"))
            {
                var scale=material.GetTextureScale("_BaseMap");var offset=material.GetTextureOffset("_BaseMap");
                value.BaseMapST=new Vector4(scale.x,scale.y,offset.x,offset.y);
            }
            var block=subBlock!=null&&!subBlock.isEmpty?subBlock:objectBlock;
            if(block!=null)
            {
                if(block.HasVector(color))value.Alpha=block.GetColor(color).a;
                if(block.HasFloat("_Cutoff"))value.Cutoff=block.GetFloat("_Cutoff");
                if(block.HasTexture("_BaseMap"))value.Mask=block.GetTexture("_BaseMap")??Texture2D.whiteTexture;
                if(block.HasVector("_BaseMap_ST"))value.BaseMapST=block.GetVector("_BaseMap_ST");
            }
            return value;
        }
    }
}
