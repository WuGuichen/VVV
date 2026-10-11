using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Isolated material approval surface, not a replacement player-facing UI.</summary>
    internal sealed class CardShellMaterialStudyWindow : EditorWindow
    {
        private CardShellStudyRenderer _renderer;
        private Image _preview;
        private Label _status;
        private Slider _position;
        private Button _pause;
        private Button _referenceButton;
        private Texture2D _reference;
        private double _lastTime;
        private float _clock,_speed=.65f,_strength=1f,_ripple=1f,_opacity=.84f;
        private bool _playing=true,_neutral,_baseOnly;
        private bool _showReference,_single;
        private int _capturedPointer=-1;
        private float _interactionClock,_lastDragTime=-100;
        private Vector2 _lastDragPosition;

        [MenuItem("MxFramework/CharacterTest/Card Shell/Open Material Study")]
        internal static void Open()
        {
            var window=GetWindow<CardShellMaterialStudyWindow>();
            window.titleContent=new GUIContent("卡壳材质预览");
            window.minSize=new Vector2(980,680);window.Show();window.Focus();
        }

        private void OnEnable(){_lastTime=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
        private void OnDisable(){EditorApplication.update-=Tick;_renderer?.Dispose();_renderer=null;}

        private void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();
            root.style.backgroundColor=new Color(.075f,.09f,.11f);
            var heading=new Label("卡壳材质验证  ·  银色 / 金色 / 全息") {style={fontSize=20,marginLeft=18,marginTop=14,marginBottom=8,color=new Color(.88f,.94f,1)}};
            root.Add(heading);
            root.Add(new Label("外框宝石折光 · 中央虹彩波纹 · 磨砂叠层保持稳定。无文字、无图标。") {style={marginLeft=18,marginBottom=10,color=new Color(.65f,.74f,.82f)}});
            var controls=new VisualElement {style={flexDirection=FlexDirection.Row,marginLeft=14,marginRight=14}};
            _pause=new Button(()=>{_playing=!_playing;_pause.text=_playing?"暂停流光":"继续流光";}){text="暂停流光"};
            controls.Add(_pause);
            controls.Add(new Button(()=>{_clock=0;Draw();}){text="回到起点"});
            controls.Add(new Button(()=>{_neutral=!_neutral;Draw();}){text="切换背景"});
            controls.Add(new Button(()=>{_baseOnly=!_baseOnly;Draw();}){text="查看 / 隐藏磨砂叠层"});
            _referenceButton=new Button(ToggleReference){text="对照参考图"};controls.Add(_referenceButton);
            controls.Add(new Button(()=>{_single=!_single;_showReference=false;_referenceButton.text="对照参考图";_preview.image=_single?_renderer.Detail:_renderer.Board;}){text="单卡细节 / 三档对比"});
            controls.Add(new Button(ExportCurrent){text="导出当前帧"});root.Add(controls);
            _preview=new Image {name="cardShellInteractivePreview",scaleMode=ScaleMode.ScaleToFit,style={flexGrow=1,minHeight=360,marginTop=8,marginBottom=8}};root.Add(_preview);
            _capturedPointer=-1;
            _preview.RegisterCallback<PointerDownEvent>(OnPreviewDown);
            _preview.RegisterCallback<PointerMoveEvent>(OnPreviewMove);
            _preview.RegisterCallback<PointerUpEvent>(OnPreviewUp);
            _preview.RegisterCallback<PointerCaptureOutEvent>(_=>_capturedPointer=-1);
            var settings=new VisualElement {style={marginLeft=18,marginRight=18,marginBottom=10}};
            settings.Add(Control("流动速度",.1f,1.5f,_speed,v=>_speed=v));
            settings.Add(Control("外框流光",0,2,_strength,v=>{_strength=v;Draw();}));
            settings.Add(Control("中央波纹",0,2,_ripple,v=>{_ripple=v;Draw();}));
            settings.Add(Control("磨砂覆盖",.35f,1,_opacity,v=>{_opacity=v;Draw();}));
            _position=Control("时间位置",0,40,0,v=>{_clock=v;_playing=false;_pause.text="继续流光";Draw();});settings.Add(_position);
            _status=new Label("准备材质…"){style={color=new Color(.68f,.77f,.85f),marginTop=5}};settings.Add(_status);root.Add(settings);
            try
            {
                _renderer?.Dispose();_renderer=new CardShellStudyRenderer();_preview.image=_renderer.Board;
                _reference=AssetDatabase.LoadAssetAtPath<Texture2D>(CardShellStudyAssets.Root+"/SingleCardReference.png");
                _showReference=false;
                _status.text="点击中央纹理区产生涟漪；按住拖动可连续扰动。";Draw();
            }
            catch(Exception error){_status.text=error.Message;Debug.LogException(error);}
        }

        private static Slider Control(string title,float min,float max,float value,Action<float> changed)
        {
            var slider=new Slider(title,min,max){value=value,showInputField=true};
            slider.RegisterValueChangedCallback(e=>changed(e.newValue));return slider;
        }

        private void Tick()
        {
            double now=EditorApplication.timeSinceStartup;
            if(now-_lastTime<1.0/30)return;
            float delta=(float)Math.Min(now-_lastTime,.1);_lastTime=now;
            if(_renderer==null || rootVisualElement.panel==null)return;
            _interactionClock+=delta;_renderer.InteractionTime=_interactionClock;
            if(_playing)
            {
                _clock+=delta*_speed;
                if(_position!=null){_position.highValue=Math.Max(40,_clock);_position.SetValueWithoutNotify(_clock);}
            }
            if(_playing||_renderer.HasInteraction)Draw();
        }

        private void Draw()
        {
            if(_renderer==null)return;
            _renderer.InteractionTime=_interactionClock;
            _renderer.Render(_clock,_strength,_opacity,_neutral,_baseOnly,_ripple);_preview?.MarkDirtyRepaint();
        }

        private void ExportCurrent()
        {
            string path=EditorUtility.SaveFilePanel("导出卡壳效果图","","card-shell.png","png");
            if(string.IsNullOrEmpty(path))return;
            Draw();_renderer.SaveFrame(path);_status.text="已导出："+path;
        }

        private void ToggleReference()
        {
            if(_reference==null){_status.text="参考图资源尚未导入。";return;}
            _showReference=!_showReference;
            _preview.image=_showReference?(Texture)_reference:(_single?_renderer.Detail:_renderer.Board);
            _referenceButton.text=_showReference?"返回实时卡壳":"对照参考图";
            _status.text=_showReference?"参考图含文字与图标；当前样机只验证卡壳结构和材质。":"Unity Shader 实时渲染 · 背景为场景参考图 · 右上角无类型标识";
        }

        private void OnPreviewDown(PointerDownEvent e)
        {
            if(e.button!=0||!TriggerPreview((Vector2)e.localPosition,1))return;
            _capturedPointer=e.pointerId;_preview.CapturePointer(e.pointerId);
            _lastDragPosition=(Vector2)e.localPosition;_lastDragTime=_interactionClock;
            e.StopPropagation();
        }

        private void OnPreviewMove(PointerMoveEvent e)
        {
            if(e.pointerId!=_capturedPointer||!_preview.HasPointerCapture(e.pointerId))return;
            var p=(Vector2)e.localPosition;
            if(_interactionClock-_lastDragTime<.09f||(p-_lastDragPosition).sqrMagnitude<25)return;
            if(TriggerPreview(p,.42f)){_lastDragPosition=p;_lastDragTime=_interactionClock;}
        }

        private void OnPreviewUp(PointerUpEvent e)
        {
            if(e.pointerId!=_capturedPointer)return;
            _preview.ReleasePointer(e.pointerId);_capturedPointer=-1;e.StopPropagation();
        }

        private bool TriggerPreview(Vector2 localPosition,float amplitude)
        {
            if(_showReference||_renderer==null||_preview.image==null)return false;
            var box=_preview.contentRect;var texture=_preview.image;
            float scale=Mathf.Min(box.width/texture.width,box.height/texture.height);
            if(scale<=0)return false;
            var size=new Vector2(texture.width*scale,texture.height*scale);
            var imageRect=new Rect(box.center-size*.5f,size);
            if(!imageRect.Contains(localPosition))return false;
            var uv=new Vector2((localPosition.x-imageRect.x)/imageRect.width,1-(localPosition.y-imageRect.y)/imageRect.height);
            _renderer.InteractionTime=_interactionClock;
            if(!_renderer.Interact(uv,_single,amplitude))return false;
            _status.text=amplitude>.8f?"点击涟漪 · 从落点扩散并逐渐消退":"拖动扰动 · 多个波源叠加";
            Draw();return true;
        }

        // Called by the validation tool through reflection; no gameplay state is touched.
        internal static string ExportEvidence(string folder,int frameCount=0)
        {
            Directory.CreateDirectory(folder);
            using(var renderer=new CardShellStudyRenderer())
            {
                renderer.Render(0,1,.84f,false);renderer.SaveFrame(Path.Combine(folder,"shell-t0.png"));
                var first=CardShellStudyRenderer.Readback(renderer.Board);
                renderer.Render(4,1,.84f,false);renderer.SaveFrame(Path.Combine(folder,"shell-t4.png"));
                var second=CardShellStudyRenderer.Readback(renderer.Board);
                renderer.Render(4,1,.84f,false);
                var repeated=CardShellStudyRenderer.Readback(renderer.Board);
                int changed=0,unstable=0;
                try
                {
                    var a=first.GetPixels32();var b=second.GetPixels32();var c=repeated.GetPixels32();
                    for(int i=0;i<a.Length;i++){if(!a[i].Equals(b[i]))changed++;if(!b[i].Equals(c[i]))unstable++;}
                }
                finally{DestroyImmediate(first);DestroyImmediate(second);DestroyImmediate(repeated);}
                renderer.Render(4,1,.84f,true,true);renderer.SaveFrame(Path.Combine(folder,"quality-base-only.png"));
                for(int i=0;i<Mathf.Clamp(frameCount,0,120);i++)
                {
                    renderer.Render(i*.16f,1,.84f,false);
                    renderer.SaveFrame(Path.Combine(folder,"frame-"+i.ToString("D3")+".png"));
                }
                string report="{\"changedPixelsAt4Seconds\":"+changed+",\"sameTimeChangedPixels\":"+unstable+",\"frameCount\":"+frameCount+",\"width\":1536,\"height\":864}";
                File.WriteAllText(Path.Combine(folder,"validation.json"),report);return report;
            }
        }
    }
}
