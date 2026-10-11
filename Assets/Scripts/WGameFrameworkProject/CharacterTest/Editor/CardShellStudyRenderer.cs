using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Owned GPU targets for material study; does not render or mutate a game scene.</summary>
    internal sealed class CardShellStudyRenderer : IDisposable
    {
        private static readonly int FlowTime=Shader.PropertyToID("_FlowTime");
        private static readonly int Strength=Shader.PropertyToID("_FlowStrength");
        private static readonly int RippleStrength=Shader.PropertyToID("_RippleStrength");
        private static readonly int Opacity=Shader.PropertyToID("_PanelOpacity");
        private static readonly int Backdrop=Shader.PropertyToID("_BackdropTex");
        private static readonly int BackdropRect=Shader.PropertyToID("_BackdropRect");
        private static readonly int BackdropMode=Shader.PropertyToID("_BackdropMode");
        private static readonly int TouchClock=Shader.PropertyToID("_TouchClock");
        private static readonly int TouchEvents=Shader.PropertyToID("_TouchEvents");
        private readonly Material[] _flows=new Material[3];
        private readonly RenderTexture[] _cards=new RenderTexture[3];
        private readonly Vector4[] _rects=new Vector4[3];
        private readonly Vector4[][] _touches={new Vector4[12],new Vector4[12],new Vector4[12]};
        private readonly int[] _touchCursor=new int[3];
        private float _lastTouchTime=-100;
        private readonly Material _composite;
        private readonly Texture2D _artwork,_background;
        private readonly RenderTexture _blurred;
        private bool _disposed;
        internal RenderTexture Board {get;private set;}
        internal RenderTexture Detail => _cards[2];
        internal float InteractionTime {get;set;}
        internal bool HasInteraction => InteractionTime-_lastTouchTime<2.5f;

        internal bool Interact(Vector2 displayUv,bool single,float amplitude)
        {
            if(_disposed)return false;
            for(int i=0;i<3;i++)
            {
                if(single&&i!=2)continue;
                Vector4 r=single?new Vector4(0,0,1,1):_rects[i];
                var uv=new Vector2((displayUv.x-r.x)/r.z,(displayUv.y-r.y)/r.w);
                if(uv.x<0||uv.x>1||uv.y<0||uv.y>1||!CardShellAuthoredAssets.IsInInset(uv))continue;
                _touches[i][_touchCursor[i]]=new Vector4(uv.x,uv.y,InteractionTime,Mathf.Clamp(amplitude,0,1));
                _touchCursor[i]=(_touchCursor[i]+1)%12;
                _lastTouchTime=InteractionTime;return true;
            }
            return false;
        }

        internal CardShellStudyRenderer()
        {
            try
            {
                CardShellAuthoredAssets.EnsureCreated();
                string root=CardShellStudyAssets.Root;
                _artwork=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/ShellArtwork.png");
                _background=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/StudyBackdrop.png");
                if(_background==null) throw new InvalidOperationException("Material study backdrop is missing.");
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CardShellStudyComposite.shader");
                if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Card shell preview shader is unavailable.");
                _composite=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                Board=Target(1536,864,"Card shell study");
                _blurred=Target(384,216,"Shared study backdrop blur");
                var previousTarget=RenderTexture.active;
                var temporary=RenderTexture.GetTemporary(384,216,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
                try
                {
                    Graphics.Blit(_background,temporary,_composite,0);
                    Graphics.Blit(temporary,_blurred,_composite,0);
                }
                finally{RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(temporary);}
                string[] names={"AuthoredSilver","AuthoredGold","AuthoredHolographic"};
                for(int i=0;i<3;i++)
                {
                    _flows[i]=CloneMaterial(root+"/"+names[i]+".mat");
                    _cards[i]=Target(756,1056,names[i]+" shell");
                    _rects[i]=new Vector4((174+i*402)/1536f,60/864f,384/1536f,(384*88f/63)/864f);
                    _flows[i].SetTexture(Backdrop,_blurred);
                    _flows[i].SetVector(BackdropRect,_rects[i]);
                    _composite.SetTexture("_Card"+i,_cards[i]);
                    _composite.SetVector("_Rect"+i,_rects[i]);
                }
            }
            catch{Dispose();throw;}
        }

        private static Material CloneMaterial(string path)
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(source==null || ShaderUtil.ShaderHasError(source.shader))throw new InvalidOperationException("Invalid card shell material: "+path);
            return new Material(source){hideFlags=HideFlags.HideAndDontSave};
        }

        private static RenderTexture Target(int width,int height,string name)
        {
            var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            target.Create();return target;
        }

        internal void Render(float time,float strength,float opacity,bool neutral,bool baseOnly=false,float rippleStrength=1)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(CardShellStudyRenderer));
            var previous=RenderTexture.active;
            try
            {
                for(int i=0;i<3;i++)
                {
                    RenderTexture.active=_cards[i];GL.Clear(true,true,Color.clear);
                    _flows[i].SetFloat(FlowTime,time);_flows[i].SetFloat(Strength,strength);
                    _flows[i].SetFloat(RippleStrength,rippleStrength);
                    _flows[i].SetFloat(TouchClock,InteractionTime);
                    _flows[i].SetVectorArray(TouchEvents,_touches[i]);
                    _flows[i].SetFloat(Opacity,opacity);
                    _flows[i].SetFloat("_BaseOnly",baseOnly?1:0);
                    Graphics.Blit(_artwork,_cards[i],_flows[i]);
                }
                _composite.SetFloat(BackdropMode,neutral?1:0);
                Graphics.Blit(_background,Board,_composite,1);
            }
            finally{RenderTexture.active=previous;}
        }

        internal static Texture2D Readback(RenderTexture target)
        {
            var previous=RenderTexture.active;
            var linear=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
            Texture2D texture=null;
            try
            {
                RenderTexture.active=target;
                linear.ReadPixels(new Rect(0,0,target.width,target.height),0,0);linear.Apply();
                // Convert before 8-bit quantization: quantizing linear light first creates bands.
                var pixels=linear.GetPixels();
                for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
                texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false,false);
                texture.SetPixels(pixels);texture.Apply();return texture;
            }
            catch{UnityEngine.Object.DestroyImmediate(texture);throw;}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(linear);}
        }

        internal void SaveFrame(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture=Readback(Board);
            try{File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }

        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            foreach(var material in _flows)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            if(_composite!=null)UnityEngine.Object.DestroyImmediate(_composite);
            foreach(var target in _cards)Release(target);
            Release(_blurred);Release(Board);Board=null;
        }

        private static void Release(RenderTexture target)
        {
            if(target==null)return;
            if(RenderTexture.active==target)RenderTexture.active=null;
            target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
