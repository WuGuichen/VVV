using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Repeatable vector-mask authoring for the isolated card material study.</summary>
    internal static class CardShellStudyAssets
    {
        internal const string Root = "Assets/MxFrameworkGenerated/UI/CardShellStudy";
        internal const int Width = 756, Height = 1056;
        private static readonly Vector2[] Body = Points(16,23, 32,14, 548,14, 566,32, 566,401, 522,445, 61,445, 16,401);
        // The optical inset is a stepped opening, not a plain parallelogram.
        private static readonly Vector2[] Window = Points(189,157, 464,157, 436,205, 436,337, 383,422, 96,422, 137,353, 137,249, 122,249);
        private static readonly Vector2[] Title = Points(132,14, 500,14, 408,123, 136,123, 126,112, 126,38);
        private static readonly Vector2[] Corner = Points(479,21, 548,21, 565,38, 565,102, 540,132, 474,132, 455,116, 415,116);
        private static readonly Vector2[] Description = Points(60,458, 523,458, 565,501, 565,733, 521,791, 171,791, 153,776, 76,776, 23,731, 23,498);
        private static readonly Vector2[][] AccentCuts = {
            Points(56,141, 70,141, 59,153, 45,153),
            Points(76,141, 90,141, 79,153, 65,153),
            Points(97,141, 123,141, 112,153, 86,153),
            Points(474,774, 486,774, 475,788, 463,788),
            Points(493,774, 505,774, 494,788, 482,788),
            Points(512,774, 524,774, 513,788, 501,788)
        };
        // Linework belongs to the card shell, independent of quality or animation.
        private static readonly Vector2[][] Engravings = {
            Points(26,122, 51,149), Points(24,36, 42,20),
            Points(508,19, 548,19, 562,34),
            Points(27,738, 78,785, 152,785, 168,800, 510,800, 560,747),
            Points(21,752, 68,797), Points(22,770, 50,798),
            Points(142,217, 170,181), Points(448,321, 438,342),
            Points(448,344, 397,425), Points(99,391, 115,365)
        };

        internal static void EnsureCreated()
        {
            if (!File.Exists(Root + "/PanelMask.png") || !File.Exists(Root + "/ShellDetailMask.png")) Generate();
        }

        [MenuItem("MxFramework/CharacterTest/Card Shell/Generate Material Study Assets")]
        internal static void Generate()
        {
            Directory.CreateDirectory(Root);
            var quality = new Color32[Width * Height];
            var combined = new Color32[Width * Height];
            var details = new Color32[Width * Height];
            var layerPixels = new Color32[5][];
            for (int i = 0; i < layerPixels.Length; i++) layerPixels[i] = new Color32[Width * Height];
            var distances = new float[5];
            var shifted = new float[5];
            var lightShifted = new float[5];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                var p = new Vector2((x + .5f) * 580 / Width, (Height-y-.5f) * 810 / Height);
                float outer = RoundedBox(p, new Vector2(290,405), new Vector2(288,403), 14);
                float alpha = Coverage(outer);
                float depth=-outer;
                float rim=alpha*(Ridge(depth-1.2f,1.1f)+.56f*Ridge(depth-11.5f,1.3f));
                float rimDark=alpha*(.65f*Ridge(depth-6.5f,2.4f)+.45f*Ridge(depth-14,1.1f));
                float hole = Coverage(PolygonDistance(p, Window));
                var costDirection=p-new Vector2(76,77);
                float radius=costDirection.magnitude;
                float ringLight=Ridge(radius-48,1.1f)+.72f*Ridge(radius-42,1.1f);
                float ringShade=Ridge(radius-45,3)*Mathf.Clamp01(.45f+(costDirection.y+costDirection.x)/130);
                quality[index] = new Color(Mathf.Clamp01(rim+ringLight),hole,Mathf.Clamp01(rimDark+ringShade),alpha);
                Distances(p, distances);
                Distances(p-new Vector2(0,3), shifted);
                Distances(p+new Vector2(1.1f,1.1f), lightShifted);
                float coverage=0, light=0, shadow=0, dark=0;
                for(int layer=0;layer<distances.Length;layer++)
                {
                    float d=distances[layer];
                    float c=Coverage(d);
                    float edge=c*(1-Mathf.SmoothStep(0,1,Mathf.Max(0,-d)/2.1f));
                    float direction=(d-lightShifted[layer])*.46f;
                    float edgeLight=edge*Mathf.Clamp01(.45f+direction);
                    float edgeDark=edge*Mathf.Clamp01(.45f-direction);
                    float shade=1-Mathf.SmoothStep(0,1,Mathf.Max(0,shifted[layer])/4.4f);
                    shadow=Mathf.Max(shadow,shade)*(1-c);
                    light=Mathf.Lerp(light,edgeLight,c);
                    dark=Mathf.Lerp(dark,edgeDark,c);
                    coverage=c+coverage*(1-c);
                    layerPixels[layer][index]=new Color(c,edgeLight,shade*(1-c),edgeDark);
                }
                float cut=0,line=0;
                foreach(var accent in AccentCuts)cut=Mathf.Max(cut,Coverage(PolygonDistance(p,accent)));
                foreach(var engraving in Engravings)line=Mathf.Max(line,Coverage(PolylineDistance(p,engraving)-.65f));
                // A second thin outline follows the lower plate without flattening its stepped corner.
                float descDistance=PolygonDistance(p,Description);
                float insetLine=Ridge(descDistance+6, .75f)*Coverage(descDistance);
                line=Mathf.Max(line,insetLine*.5f);
                details[index]=new Color(cut,line,0,1);
                coverage*=1-cut;
                combined[index]=new Color(coverage,light,shadow,dark);
            }
            SaveMask("QualityMask",quality);
            SaveMask("PanelMask",combined);
            SaveMask("ShellDetailMask",details);
            string[] names={"BodyPanel","CornerUnderlay","TitlePanel","DescriptionPanel","CostDisc"};
            for(int i=0;i<names.Length;i++) SaveMask(names[i],layerPixels[i]);
            var flow=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/MxFramework/UI/CardQualityFlow.shader");
            var frost=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/MxFramework/UI/CardFrostedPanel.shader");
            if(flow==null || frost==null) throw new InvalidOperationException("Card shell shaders have not imported.");
            for(int i=0;i<3;i++)
            {
                var mat=LoadOrCreateMaterial(new[]{"Silver","Gold","Holographic"}[i],flow);
                mat.SetFloat("_Quality",i);
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/QualityMask.png");
                mat.SetTexture("_ShellDetailTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ShellDetailMask.png"));
                EditorUtility.SetDirty(mat);
            }
            var panel=LoadOrCreateMaterial("FrostedPanels",frost);
            panel.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/PanelMask.png");
            panel.SetTexture("_ShellDetailTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ShellDetailMask.png"));
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
        }

        private static Material LoadOrCreateMaterial(string name,Shader shader)
        {
            string path=Root+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            return material;
        }

        private static void SaveMask(string name,Color32[] pixels)
        {
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false,true);
            try
            {
                texture.SetPixels32(pixels);texture.Apply();
                string path=Root+"/"+name+".png";
                File.WriteAllBytes(path,texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.alphaIsTransparency=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }

        private static void Distances(Vector2 p,float[] values)
        {
            float circle=Vector2.Distance(p,new Vector2(76,77));
            values[0]=Mathf.Max(PolygonDistance(p,Body),-PolygonDistance(p,Window));
            values[0]=Mathf.Max(values[0],51-circle);
            values[1]=PolygonDistance(p,Corner);
            values[2]=PolygonDistance(p,Title);
            values[3]=PolygonDistance(p,Description);
            values[4]=circle-39;
        }

        private static float Coverage(float distance) => 1-Mathf.SmoothStep(0,1,(distance+.55f)/1.1f);

        private static float Ridge(float distance,float width) => Mathf.Exp(-distance*distance/(width*width));

        private static float PolylineDistance(Vector2 p,Vector2[] vertices)
        {
            float closest=float.MaxValue;
            for(int i=1;i<vertices.Length;i++)
            {
                Vector2 edge=vertices[i]-vertices[i-1],v=p-vertices[i-1];
                float t=Mathf.Clamp01(Vector2.Dot(v,edge)/edge.sqrMagnitude);
                closest=Mathf.Min(closest,(v-edge*t).sqrMagnitude);
            }
            return Mathf.Sqrt(closest);
        }

        private static float RoundedBox(Vector2 p,Vector2 center,Vector2 half,float radius)
        {
            var q=new Vector2(Mathf.Abs(p.x-center.x),Mathf.Abs(p.y-center.y))-half+Vector2.one*radius;
            return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
        }

        private static float PolygonDistance(Vector2 p,Vector2[] vertices)
        {
            float closest=float.MaxValue;bool inside=false;
            for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
            {
                Vector2 a=vertices[j],b=vertices[i],e=b-a,v=p-a;
                float t=Mathf.Clamp01(Vector2.Dot(v,e)/e.sqrMagnitude);
                closest=Mathf.Min(closest,(v-e*t).sqrMagnitude);
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return Mathf.Sqrt(closest)*(inside?-1:1);
        }

        private static Vector2[] Points(params float[] xy)
        {
            var result=new Vector2[xy.Length/2];
            for(int i=0;i<result.Length;i++)result[i]=new Vector2(xy[i*2],xy[i*2+1]);
            return result;
        }
    }
}
