using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Imports the approved-detail art and bakes independent optical-region masks.</summary>
    internal static class CardShellAuthoredAssets
    {
        private const string Root=CardShellStudyAssets.Root;
        // Inner edge of the widened authored opening. Its top/bottom remain at
        // the same height; the small right-hand notches must not be filled over.
        private static readonly Vector2[] Aperture=Units(
            274,290, 918,290, 853,391, 867,395, 868,414, 880,404,
            890,422, 889,439, 901,447, 901,614, 793,759, 108,759,
            163,622, 163,454, 130,454, 217,336, 237,336);
        private static readonly Vector2[] UpperPlate=Units(39,39, 998,39, 1033,79, 1033,716, 939,806, 864,806, 858,809, 122,809, 37,720);
        private static readonly Vector2[] LowerPlate=Units(120,828, 936,828, 1030,905, 1030,1285, 927,1416, 270,1416, 244,1391, 125,1391, 48,1310, 48,897);
        private static readonly Vector2[] TitleSeam=Units(258,229, 743,229, 912,40);
        private static readonly Vector2[] CornerSeam=Units(764,213, 832,213, 861,243, 978,243, 1017,208);

        internal static void EnsureCreated()
        {
            if(!File.Exists(Root+"/AuthoredRegionMask.png") || !File.Exists(Root+"/AuthoredHolographic.mat")) Generate();
        }

        internal static bool IsInInset(Vector2 uv) => PolygonDistance(new Vector2(uv.x,1-uv.y),Aperture)<-.002f;

        [MenuItem("MxFramework/CharacterTest/Card Shell/Generate Authored Material Regions")]
        internal static void Generate()
        {
            string source=Root+"/ShellArtwork.png";
            AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
            Configure(source,true,true);
            var art=AssetDatabase.LoadAssetAtPath<Texture2D>(source);
            var pixels=art.GetPixels32();var mask=new Color32[pixels.Length];
            for(int y=0;y<art.height;y++)for(int x=0;x<art.width;x++)
            {
                int index=y*art.width+x;Color color=pixels[index];
                float max=Mathf.Max(color.r,Mathf.Max(color.g,color.b));
                float min=Mathf.Min(color.r,Mathf.Min(color.g,color.b));
                float saturation=(max-min)/Mathf.Max(max,.001f);
                var p=new Vector2((x+.5f)/art.width,1-(y+.5f)/art.height);
                float distance=PolygonDistance(p,Aperture);
                // One-pixel antialiasing, not a ten-pixel inset. Refraction safety
                // is handled separately in the shader so visible bands fill the opening.
                float aperture=Ramp(-distance,-.0005f,.0005f);
                float discDistance=Vector2.Distance(new Vector2(p.x,p.y*art.height/art.width),new Vector2(.139f,.102f*art.height/art.width));
                float disc=1-Ramp(discDistance,.077f,.088f);
                float alpha=Ramp(max,.045f,.17f);
                float quality=Ramp(saturation,.14f,.27f)*Ramp(max,.16f,.32f);
                // Chroma alone would select the cool-blue frost and its diagonal shading.
                // Restrict quality response to structural gaps, seams and authored accents.
                float plateDistance=Mathf.Min(PolygonDistance(p,UpperPlate),PolygonDistance(p,LowerPlate));
                float structure=Ramp(plateDistance,-.004f,.001f);
                float seams=1-Ramp(Mathf.Min(LineDistance(p,TitleSeam),LineDistance(p,CornerSeam)),.002f,.005f);
                bool accent=(p.x>.07f&&p.x<.232f&&p.y>.170f&&p.y<.195f)
                    ||(p.x>.78f&&p.x<.88f&&p.y>.934f&&p.y<.957f)
                    ||(p.x>.10f&&p.x<.25f&&p.y>.22f&&p.y<.35f)
                    ||(p.x>.07f&&p.x<.17f&&p.y>.43f&&p.y<.522f)
                    ||(p.x>.81f&&p.x<.90f&&p.y>.205f&&p.y<.333f)
                    ||(p.x>.79f&&p.x<.90f&&p.y>.423f&&p.y<.522f);
                // Structural regions may include white gem highlights. Accent search boxes
                // require strong chroma so their rectangular bounds cannot tint the frost.
                quality=Mathf.Max(Mathf.Max(structure,seams),accent?Ramp(saturation,.40f,.60f):0)*Ramp(max,.16f,.32f);
                mask[index]=new Color(quality*(1-aperture)*(1-disc),aperture,disc,alpha);
            }
            var texture=new Texture2D(art.width,art.height,TextureFormat.RGBA32,false,true);
            try
            {
                texture.SetPixels32(mask);texture.Apply();
                File.WriteAllBytes(Root+"/AuthoredRegionMask.png",texture.EncodeToPNG());
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(Root+"/AuthoredRegionMask.png",ImportAssetOptions.ForceSynchronousImport);
            Configure(Root+"/AuthoredRegionMask.png",false,false);
            Configure(source,true,false);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/MxFramework/UI/CardAuthoredShell.shader");
            if(shader==null)throw new InvalidOperationException("Authored card shader is not imported.");
            string[] names={"AuthoredSilver","AuthoredGold","AuthoredHolographic"};
            for(int i=0;i<names.Length;i++)
            {
                string path=Root+"/"+names[i]+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                material.shader=shader;material.mainTexture=art;
                material.SetTexture("_RegionTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/AuthoredRegionMask.png"));
                material.SetFloat("_Quality",i);EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Configure(string path,bool srgb,bool readable)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=srgb;importer.isReadable=readable;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.alphaIsTransparency=false;
            importer.SaveAndReimport();
        }

        private static float Ramp(float value,float low,float high)=>Mathf.SmoothStep(0,1,(value-low)/(high-low));

        private static Vector2[] Units(params float[] xy)
        {
            var result=new Vector2[xy.Length/2];
            for(int i=0;i<result.Length;i++)result[i]=new Vector2(xy[i*2]/1068f,xy[i*2+1]/1473f);
            return result;
        }

        private static float LineDistance(Vector2 p,Vector2[] v)
        {
            float closest=float.MaxValue;
            for(int i=1;i<v.Length;i++)
            {
                var edge=v[i]-v[i-1];var q=p-v[i-1];
                float t=Mathf.Clamp01(Vector2.Dot(q,edge)/edge.sqrMagnitude);
                closest=Mathf.Min(closest,(q-edge*t).sqrMagnitude);
            }
            return Mathf.Sqrt(closest);
        }

        private static float PolygonDistance(Vector2 p,Vector2[] v)
        {
            float closest=float.MaxValue;bool inside=false;
            for(int i=0,j=v.Length-1;i<v.Length;j=i++)
            {
                var a=v[j];var b=v[i];var e=b-a;var q=p-a;
                float t=Mathf.Clamp01(Vector2.Dot(q,e)/e.sqrMagnitude);
                closest=Mathf.Min(closest,(q-e*t).sqrMagnitude);
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return Mathf.Sqrt(closest)*(inside?-1:1);
        }
    }
}
