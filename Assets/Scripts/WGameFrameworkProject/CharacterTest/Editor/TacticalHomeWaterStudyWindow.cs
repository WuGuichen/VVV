using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Motion approval over the approved artwork; not the live menu implementation.</summary>
    internal sealed class TacticalHomeWaterStudyWindow : EditorWindow
    {
        private const string SourcePath="Assets/MxFrameworkGenerated/UI/TacticalHomeWaterStudy/HomeReference.png";
        private static readonly int TimeId=Shader.PropertyToID("_WaterTime");
        private static readonly int StrengthId=Shader.PropertyToID("_WaterStrength");
        private static readonly Vector4[] StableRects={
            new(.304f,.085f,.702f,.255f),
            new(.208f,.680f,.497f,.813f), new(.505f,.680f,.791f,.813f),
            new(.704f,.867f,.815f,.950f),new(.822f,.867f,.971f,.950f),
            new(.226f,.632f,.339f,.666f)
        };
        private Material _material;
        private Texture2D _source;
        private RenderTexture _target;
        private Image _image;
        private bool _playing=true;
        private float _time,_speed=1,_strength=1;
        private double _lastTick;

        [MenuItem("MxFramework/CharacterTest/Home UI/Open Water Motion Study")]
        internal static void Open()
        {
            var window=GetWindow<TacticalHomeWaterStudyWindow>();
            window.titleContent=new GUIContent("主界面水影预览");
            window.minSize=new Vector2(1000,680);window.Show();window.Focus();
        }

        private void OnEnable(){_lastTick=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
        private void OnDisable(){EditorApplication.update-=Tick;Release();}

        private void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();
            root.style.backgroundColor=new Color(.07f,.085f,.10f);
            root.Add(new Label("主界面 · 动态水影") {style={fontSize=21,marginLeft=16,marginTop=12,marginBottom=8}});
            root.Add(new Label("水影缓慢流动，徽标、文字与按钮保持稳定。此窗口用于确认动效。") {style={marginLeft=16,marginBottom=10}});
            var row=new VisualElement{style={flexDirection=FlexDirection.Row,marginLeft=12}};
            var pause=new Button();pause.text=_playing?"暂停":"继续";
            pause.clicked+=()=>{_playing=!_playing;pause.text=_playing?"暂停":"继续";};row.Add(pause);
            row.Add(new Button(()=>{_time=0;Draw();}){text="回到起点"});
            row.Add(new Button(()=>{
                var path=EditorUtility.SaveFilePanel("导出当前帧","","home-water.png","png");
                if(!string.IsNullOrEmpty(path)){Draw();Save(path);}
            }){text="导出当前帧"});root.Add(row);
            _image=new Image{scaleMode=ScaleMode.ScaleToFit,style={flexGrow=1,minHeight=400,marginTop=8,marginBottom=8}};root.Add(_image);
            var speed=new Slider("流动速度",.2f,1.5f){value=_speed,showInputField=true};
            speed.RegisterValueChangedCallback(e=>_speed=e.newValue);root.Add(speed);
            var strength=new Slider("水影强度",0,1.5f){value=_strength,showInputField=true};
            strength.RegisterValueChangedCallback(e=>{_strength=e.newValue;Draw();});root.Add(strength);
            Release();Initialize();_image.image=_target;Draw();
        }

        private void Initialize()
        {
            _source=AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/TacticalHomeWaterStudy.shader");
            if(_source==null||shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("主界面参考图或水影 Shader 未就绪。");
            _material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            _material.SetVectorArray("_StableRects",StableRects);
            _target=new RenderTexture(1536,864,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {hideFlags=HideFlags.HideAndDontSave,name="Home water motion",filterMode=FilterMode.Bilinear};
            _target.Create();
        }

        private void Tick()
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-_lastTick<1.0/30)return;
            float dt=(float)Math.Min(now-_lastTick,.1);_lastTick=now;
            if(!_playing||_target==null||rootVisualElement.panel==null)return;
            _time+=dt*_speed;Draw();
        }

        private void Draw()
        {
            if(_target==null)return;
            var previous=RenderTexture.active;
            try
            {
                _material.SetFloat(TimeId,_time);_material.SetFloat(StrengthId,_strength);
                Graphics.Blit(_source,_target,_material);_image?.MarkDirtyRepaint();
            }
            finally{RenderTexture.active=previous;}
        }

        private void Save(string path)
        {
            var texture=CardShellStudyRenderer.Readback(_target);
            try{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{DestroyImmediate(texture);}
        }

        private void Release()
        {
            if(_material!=null)DestroyImmediate(_material);_material=null;
            if(_target!=null){if(RenderTexture.active==_target)RenderTexture.active=null;_target.Release();DestroyImmediate(_target);}
            _target=null;
        }

        internal static void ExportFrames(string folder,int start,int count)
        {
            var study=CreateInstance<TacticalHomeWaterStudyWindow>();
            try
            {
                study.Initialize();study._playing=false;
                for(int i=start;i<start+count;i++)
                {
                    study._time=i/15f;study.Draw();study.Save(Path.Combine(folder,"frame-"+i.ToString("D3")+".png"));
                }
            }
            finally{study.Release();DestroyImmediate(study);}
        }
    }
}
