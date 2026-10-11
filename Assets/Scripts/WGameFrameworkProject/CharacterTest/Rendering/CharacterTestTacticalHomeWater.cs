using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Home or battle-edge water presentation on the existing framework renderer feature.</summary>
    internal sealed class CharacterTestTacticalHomeWater : IMxRenderPassProvider, IMxRenderPass, IMxFullscreenRenderPass, IDisposable
    {
        internal const string ShaderName="Hidden/WGame/UI/TacticalHomeWater";
        private static readonly int TimeId=Shader.PropertyToID("_WgUiWaterTime");
        private static readonly int RectsId=Shader.PropertyToID("_WgUiStableRects");
        private static readonly int BattleModeId=Shader.PropertyToID("_WgUiBattleMode");
        private static readonly int StrengthId=Shader.PropertyToID("_WgUiWaterStrength");
        private static readonly int StillnessId=Shader.PropertyToID("_WgUiBattleStillness");
        private static readonly int PointerId=Shader.PropertyToID("_WgUiPointer");
        private static readonly int MotionId=Shader.PropertyToID("_WgUiBattleMotion");
        private IMxRenderPipeline _pipeline;
        private readonly MxRenderingPipelineFeature _feature;
        private readonly UnityEngine.Camera _camera;
        private readonly Material _material;
        private readonly Vector4[] _rects=new Vector4[6];
        private static readonly string[] Controls={"logo","btnContinue","btnNew","btnSettings","btnExit","message"};
        private static readonly string[] BattleControls={"hand0","hand1","hand2","phaseSurface","motionSurface","parameterWindows"};
        private readonly bool _battle;
        private readonly string _debugName;
        private float _battleTime;
        private bool _disposed;
        internal bool Active {get;set;}
        internal int RenderCount {get;private set;}
        public string DebugName=>_debugName;
        public MxRenderPhase Phase=>MxRenderPhase.AfterRenderingPostProcessing;
        public int Order=>200;
        public bool IsEnabled=>Active&&!_disposed;
        public IReadOnlyList<SharedRenderTextureKey> Reads=>Array.Empty<SharedRenderTextureKey>();
        public IReadOnlyList<SharedRenderTextureKey> Writes=>Array.Empty<SharedRenderTextureKey>();
        public ScriptableRenderPassInput InputRequirements=>ScriptableRenderPassInput.Color;
        public bool FetchActiveColor=>true;
        public bool BindDepthStencilAttachment=>false;

        internal CharacterTestTacticalHomeWater(MxRenderingPipelineFeature feature,UnityEngine.Camera camera,bool battle=false,string debugName=null)
        {
            _debugName=debugName ?? (battle ? "CharacterTest.TacticalBattle.Water" : "CharacterTest.TacticalHome.Water");
            var shader=Shader.Find(ShaderName);
            if(shader==null)throw new InvalidOperationException("Tactical home water shader is unavailable.");
            _material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            _camera=camera;_feature=feature;_battle=battle;
            _material.SetFloat(BattleModeId,battle?1f:0f);
            EnsurePipelineRegistration();
        }

        internal void SetSurface(Fgui.GComponent surface)
        {
            EnsurePipelineRegistration();
            Rect viewport=_camera.pixelRect;
            float width=Mathf.Max(1,viewport.width),height=Mathf.Max(1,viewport.height);
            float top=Screen.height-viewport.yMax;
            for(int i=0;i<_rects.Length;i++)
            {
                var control=surface?.GetChild((_battle?BattleControls:Controls)[i]);
                if(control==null||!control.visible){_rects[i]=new Vector4(-2,-2,-1,-1);continue;}
                var a=control.LocalToGlobal(Vector2.zero);
                var b=control.LocalToGlobal(new Vector2(control.width,control.height));
                _rects[i]=new Vector4((a.x-viewport.xMin-3)/width,(a.y-top-3)/height,
                    (b.x-viewport.xMin+3)/width,(b.y-top+3)/height);
            }
        }

        internal void SetBattleSurface(Fgui.GComponent surface,float stillness,float flowTime,Vector4 motion=default)
        {
            SetSurface(surface);
            _battleTime=flowTime;
            _material.SetFloat(StillnessId,Mathf.Clamp01(stillness));
            _material.SetFloat(StrengthId,Mathf.Clamp01(stillness)*.65f);
            _material.SetVector(MotionId,motion);
        }

        internal static Vector4 ScreenPointerToWaterUv(Vector2 position,Rect viewport)
        {
            if(viewport.width<=0||viewport.height<=0||!viewport.Contains(position))
                return new Vector4(-2,-2,0,0);
            return new Vector4((position.x-viewport.xMin)/viewport.width,
                1f-(position.y-viewport.yMin)/viewport.height,0,0);
        }

        private void EnsurePipelineRegistration()
        {
            if (_disposed || _feature == null) return;
            IMxRenderPipeline current = _feature.Pipeline;
            if (ReferenceEquals(_pipeline, current)) return;
            // URP can dispose/recreate the feature's pipeline when its renderer is rebuilt.
            // The provider survives with the menu, so it must follow the new pipeline instance.
            _pipeline?.UnregisterProvider(DebugName);
            _pipeline = current;
            _pipeline.RegisterProvider(this);
        }

        public void CollectPasses(IMxRenderPassRegistry registry,in MxCameraRenderContextDescriptor context)
        {
            if(IsEnabled&&context.Camera==_camera&&context.CameraKind==MxCameraRenderKind.Game)registry.RegisterPass(this);
        }
        public void Configure(in MxRenderPassConfigureContext context) { }
        /// <remarks>NoAlloc. Reuses material and stable-rectangle storage; reads the Unity UI clock at this presentation boundary.</remarks>
        public void Execute(in MxRenderPassExecuteContext context)
        {
            if(_battle)
            {
                var mouse=UnityEngine.InputSystem.Mouse.current;
                Vector2 position=mouse!=null?mouse.position.ReadValue()
                    :new Vector2(Fgui.Stage.inst.touchPosition.x,Screen.height-Fgui.Stage.inst.touchPosition.y);
                _material.SetVector(PointerId,ScreenPointerToWaterUv(position,_camera.pixelRect));
            }
            _material.SetFloat(TimeId,_battle?_battleTime:Time.unscaledTime);
            _material.SetVectorArray(RectsId,_rects);RenderCount++;
        }
        public bool TryGetFullscreenMaterial(out Material material,out int passIndex)
        {material=_material;passIndex=0;return IsEnabled&&material!=null;}
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;Active=false;
            _pipeline?.UnregisterProvider(DebugName);
            if(Application.isPlaying)UnityEngine.Object.Destroy(_material);else UnityEngine.Object.DestroyImmediate(_material);
        }
    }
}
