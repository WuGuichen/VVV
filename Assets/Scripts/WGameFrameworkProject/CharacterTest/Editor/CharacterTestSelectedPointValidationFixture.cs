using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit transient fixture. Formal validation uses original card settlement; no player services, persistence or scene saves.</summary>
    public static partial class CharacterTestSelectedPointValidationFixture
    {
        private static CharacterActionReferenceProbeSession _probe;
        private static CharacterTestRuntimeCommandModule _commands;
        private static CharacterTestTacticalPointNavigation _navigation;
        private static TacticalPointSelectionPreview _preview;
        private static GameObject _root, _actorView;
        private static Material _material;
        private static int _frame;
        private static readonly List<object> Results = new List<object>();
        private static readonly CharacterTestRuntimeAiPlannerFactState Facts = new CharacterTestRuntimeAiPlannerFactState();
        private static TacticalWorldPoint Origin => new TacticalWorldPoint(_probe.Source.Transform.XMillimeters,
            _probe.Source.Transform.YMillimeters, _probe.Source.Transform.ZMillimeters);
        private static void Check(string name, bool pass, object actual)
        { Results.Add(new { name, pass, actual }); }
        internal static bool IsSafeContext(bool playing, int sceneCount, string scenePath,
            bool hasGameManager, bool hasPlayerServices, bool hasUserRoots) =>
            playing && sceneCount == 1 && string.IsNullOrEmpty(scenePath)
                && !hasGameManager && !hasPlayerServices && !hasUserRoots;

        private static void RequireSafeContext()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool userRoots = scene.GetRootGameObjects().Any(obj => obj != _root);
            if (!IsSafeContext(Application.isPlaying, UnityEngine.SceneManagement.SceneManager.sceneCount, scene.path,
                UnityEngine.Object.FindAnyObjectByType<GameManager>() != null, CharacterTacticalServices.Player != null, userRoots)
                || (_root != null && _root.scene != scene))
                throw new InvalidOperationException("Selected point fixture requires one empty unsaved Play scene without player services or user objects.");
        }

        public static string Build()
        {
            RequireSafeContext();
            CleanupFormal(); Cleanup(); Results.Clear(); _frame = 0;
            _probe = new CharacterActionReferenceProbeSession(new CharacterActionId(301), new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            _probe.Runtime.TrySetControlOwner(_probe.Source.Id.Value, CharacterTestCharacterControlOwner.RemotePlayer);
            _probe.Target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 20000, 0));
            _commands = new CharacterTestRuntimeCommandModule(_probe.Runtime, NullRuntimeLogger.Instance);
            Step();
            _navigation = new CharacterTestTacticalPointNavigation(_probe.Runtime, _probe.Source, null);
            _root = new GameObject("SelectedPointValidationTransient") { hideFlags = HideFlags.DontSave };
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
            _material.SetColor("_BaseColor", new Color(0.17f, 0.2f, 0.25f));
            Visual("Ground", new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 14), PrimitiveType.Cube);
            _actorView = Visual("ActorAuthorityMirror", new Vector3(0, 0.9f, 0), new Vector3(0.6f, 0.9f, 0.6f), PrimitiveType.Capsule);
            var actorColor = new MaterialPropertyBlock(); actorColor.SetColor("_BaseColor", new Color(1f, 0.7f, 0.2f));
            _actorView.GetComponent<Renderer>().SetPropertyBlock(actorColor);
            var cameraObject = new GameObject("SelectedPointValidationCamera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(_root.transform); var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 14, -9);
            camera.transform.LookAt(new Vector3(0, 0, 1)); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f); camera.fieldOfView = 55; camera.depth=0;
            // Own the overlay before the HUD asks FairyGUI to create a scene root.
            var overlay = FairyGUI.StageCamera.CreateCamera(FairyGUI.StageCamera.Name,
                1 << LayerMask.NameToLayer(FairyGUI.StageCamera.LayerName));
            overlay.gameObject.hideFlags = HideFlags.DontSave;
            overlay.transform.SetParent(_root.transform, true);
            // Fixture meshes and markers are unlit; a scene-wide light would also illuminate formal characters.
            _preview = new TacticalPointSelectionPreview(_root.transform);
            var good = new TacticalWorldPoint(0, 0, 2500);
            var picked = _navigation.TryPickGround(new TacticalWorldPoint(0, 5000, 2500), new TacticalWorldPoint(0, -1000, 0), out var point);
            Check("ground.ray.real-query", picked == TacticalInputStatus.Valid && point.Equals(good), new { status=picked.ToString(), point.X, point.Y, point.Z });
            Check("ground.valid", _navigation.ValidateGround(_probe.Source.GameplayEntityId, good) == TacticalInputStatus.Valid, good);
            var elevated = _navigation.ValidateGround(_probe.Source.GameplayEntityId, new TacticalWorldPoint(0, 3000, 2500));
            Check("ground.reject-air", elevated == TacticalInputStatus.InvalidGround, elevated.ToString());
            var missing = _navigation.TryPickGround(new TacticalWorldPoint(110000, 5000, 0), new TacticalWorldPoint(0,-1000,0), out _);
            Check("ground.reject-missing", missing == TacticalInputStatus.InvalidGround, missing.ToString());
            var direct = _navigation.ValidatePath(_probe.Source.GameplayEntityId, Origin, good);
            Check("path.direct", direct == TacticalInputStatus.Valid, direct.ToString());
            AddWall(900301, new Vector3(2, 1.5f, 0), new Vector3(0.5f, 3, 3));
            var wallPick=_navigation.TryPickGround(new TacticalWorldPoint(0,1500,0),new TacticalWorldPoint(1000,0,0),out _);
            Check("ground.reject-wall-normal",wallPick==TacticalInputStatus.InvalidGround,wallPick.ToString());
            var beyond = new TacticalWorldPoint(4000, 0, 0);
            var blocked = _navigation.ValidatePath(_probe.Source.GameplayEntityId, Origin, beyond);
            Check("path.no-navigation-fails-closed", blocked == TacticalInputStatus.PathUnavailable, blocked.ToString());
            var input = new CharacterTestNavigationSessionBuildInput(1,-6000,6000,-6000,6000,0,1000,true,1024,8192,1000,1024,256);
            var session = CharacterTestNavigationSessionBuilder.Build(input, _probe.Runtime.PhysicsRuntime,
                supportedAgentHalfExtents:_probe.Runtime.PhysicsRuntime.GetCharacterHalfExtents(_probe.Source));
            _navigation = new CharacterTestTacticalPointNavigation(_probe.Runtime, _probe.Source, session);
            var routed = _navigation.ValidatePath(_probe.Source.GameplayEntityId, Origin, beyond);
            Check("path.bounded-astar-detour", session.IsAvailable && routed == TacticalInputStatus.Valid,
                new { graph=session.BuildResult.Status.ToString(), status=routed.ToString(), revision=session.Graph?.Revision, hash=session.Graph?.ContentHash });
            ShowCandidate(true);
            return Report();
        }
        public static string ShowCandidate(bool valid)
        {
            RequireSafeContext();
            var point = valid ? new TacticalWorldPoint(0,0,2500) : new TacticalWorldPoint(6000,0,0);
            var binding = new TacticalExecutionInputBinding(1,1,1,1,_probe.Source.GameplayEntityId);
            var rules = new TacticalPointInputRules(5000,TacticalPointRangeCenter.Execution,true);
            var input = new TacticalExecutionInput(binding,TacticalExecutionInputSource.Manual,point,Origin);
            var status = TacticalExecutionInputValidation.ValidatePoint(input,binding,rules,Origin,Origin,true,_navigation);
            _preview.Show(Origin,5000,true,point,200,status==TacticalInputStatus.Valid);
            var lines = UnityEngine.Resources.FindObjectsOfTypeAll<LineRenderer>();
            var range=lines.First(l=>l.name=="Range" && l.gameObject.activeInHierarchy);
            var destination=lines.First(l=>l.name=="Destination" && l.gameObject.activeInHierarchy);
            var p=range.GetPosition(0); var color=destination.sharedMaterial.GetColor("_BaseColor");
            Check(valid?"visual.valid-candidate":"visual.invalid-candidate", _preview.IsAvailable && range.positionCount==64
                && Mathf.Abs(p.x-Origin.X/1000f-5)<0.001f && (valid?color.g>color.r:color.r>color.g)
                && status==(valid?TacticalInputStatus.Valid:TacticalInputStatus.OutOfRange),
                new { status=status.ToString(),range.positionCount,color=color.ToString(),rangeMeters=5,arrivalMeters=0.2f });
            return Report();
        }
        public static string MoveAndRelease()
        {
            RequireSafeContext();
            var start=Origin; var point=new TacticalWorldPoint(0,0,2500);
            var host=CreateHost(point,240); _navigation.BindDestination(point);
            Check("move.host-start", host.Start(_probe.Source,Facts,new RuntimeFrame(_frame)), host.LastFailureReason);
            long owner=0; bool sawOwnedMove=false; int ticks=0;
            for(int i=0;i<300;i++)
            {
                _navigation.BeginFrame(_frame+1); host.Tick(_probe.Source,Facts,new RuntimeFrame(_frame+1)); Step(); ticks++;
                if(_probe.Source.Input.HasActiveMove && _probe.Source.Input.BehaviorControl.IsPresent)
                { owner=_probe.Source.Input.BehaviorControl.OwnerToken; sawOwnedMove=true; }
                Mirror();
                if(host.State!=CharacterTestRuntimeAiLabLogicRuntimeState.Running && host.State!=CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning) break;
            }
            var finish=Origin; double distance=Math.Sqrt((finish.X-point.X)*(double)(finish.X-point.X)+(finish.Z-point.Z)*(double)(finish.Z-point.Z));
            Check("move.actual-worldposition-arrival", sawOwnedMove && distance<=200 && finish.Z>start.Z,
                new {start,finish,distanceMillimeters=distance,ticks,hostState=host.State.ToString(),owner,navStatus=_navigation.LastStatus.ToString()});
            Check("move.arrival-releases-owner", Released,
                new { _probe.Source.Input.HasActiveMove, control=_probe.Source.Input.BehaviorControl.IsPresent, _probe.Source.Input.BehaviorControl.IsRelease });
            var interrupted=CreateHost(new TacticalWorldPoint(0,0,5000),240); _navigation.BindDestination(new TacticalWorldPoint(0,0,5000));
            interrupted.Start(_probe.Source,Facts,new RuntimeFrame(_frame));
            _navigation.BeginFrame(_frame+1); interrupted.Tick(_probe.Source,Facts,new RuntimeFrame(_frame+1)); Step();
            interrupted.Stop(_probe.Source,Facts,new RuntimeFrame(_frame+1)); Step();
            Check("move.stop-releases-owner", Released, new {state=interrupted.State.ToString(),_probe.Source.Input.BehaviorControl.IsRelease});
            var timeout=CreateHost(new TacticalWorldPoint(0,0,5000),2); _navigation.BindDestination(new TacticalWorldPoint(0,0,5000));
            timeout.Start(_probe.Source,Facts,new RuntimeFrame(_frame));
            for(int i=0;i<5;i++) { _navigation.BeginFrame(_frame+1); timeout.Tick(_probe.Source,Facts,new RuntimeFrame(_frame+1)); Step(); }
            Check("move.timeout-releases-owner", timeout.State==CharacterTestRuntimeAiLabLogicRuntimeState.Failed
                && Released,new {state=timeout.State.ToString(),_probe.Source.Input.BehaviorControl.IsRelease});
            SubmitControl(222,false,Vector2.right); SubmitControl(111,true,Vector2.zero); Step();
            Check("owner.stale-release-preserves-replacement",_probe.Source.Input.BehaviorControl.OwnerToken==222 && _probe.Source.Input.HasActiveMove,
                new { _probe.Source.Input.BehaviorControl.OwnerToken,_probe.Source.Input.HasActiveMove });
            SubmitControl(222,true,Vector2.zero); Step();
            Check("owner.matching-release-clears",!_probe.Source.Input.HasActiveMove && !_probe.Source.Input.BehaviorControl.IsPresent,
                new { _probe.Source.Input.HasActiveMove });
            _navigation.ClearDestination(); Mirror();
            _preview.Show(Origin,5000,true,point,200,true);
            return Report();
        }
        private static CharacterTestBehaviorExecutionHost CreateHost(TacticalWorldPoint point,int maximumFrames)
        {
            var source=CharacterTestTacticalAuthoringTemplates.CreateBlank("temporary-world-point-probe");
            source.Schema=CharacterTestTacticalAuthoringSource.TimingSchema; source.Version=4; source.Slots.Clear();
            var block=new CharacterTestTacticalEventBlock { BlockId="point-move",Start=new CharacterTestTacticalFrameReference{Kind="Absolute",Frame=0},
                Completion="Arrived",CompletionEventId="move",Timing=new CharacterTestTacticalBlockTiming{MinimumFrames=1,DurationFrames=maximumFrames}};
            var movement=new CharacterTestTacticalAuthoringEvent { EventId="move",Start=CharacterTestTacticalBlockEdits.Local(0),
                End=CharacterTestTacticalAuthoringTemplates.VisualEnd(block.BlockId),Operation=new CharacterTestTacticalAuthoringOperation{Kind="Movement",Speed=1,
                    Movement=new CharacterTestTacticalMovement{Target=CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition,
                        Strategy=CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,X=point.X/1000f,Z=point.Z/1000f,Minimum=0.2f,Maximum=0.2f}}};
            block.EventIds.Add("move"); source.Timelines[0].Events.Add(movement);source.Timelines[0].EventBlocks.Add(block);
            if(!CharacterTestTacticalAuthoringCompiler.TryCompile(source,CharacterTestTacticalCardInputs.Unbound(_probe.Source.Identity.CharacterId),out var compiled,out var diagnostics))
                throw new InvalidOperationException(JsonConvert.SerializeObject(diagnostics));
            return new CharacterTestBehaviorExecutionHost(compiled.Program,_probe.Runtime,_probe.Runtime.ActionRuntime,null,_commands.Enqueue,_navigation);
        }
        private static void SubmitControl(long owner,bool release,Vector2 movement)
        {
            var result=_commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(_frame+1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,_probe.Source.Id.Value,movement,1,
                new CharacterTestBehaviorControlInput(2,1000,1000,ownerToken:owner,isRelease:release,isOwnerOnlyRelease:release)));
            if(!result.Success) throw new InvalidOperationException(result.Error.Message);
        }
        private static bool Released => !_probe.Source.Input.HasActiveMove
            && (!_probe.Source.Input.BehaviorControl.IsPresent || _probe.Source.Input.BehaviorControl.IsRelease)
            && _commands.PendingCommandCount==0 && _commands.LastCommandErrors.Count==0;
        private static void Step()
        {
            _frame++; var tick=new RuntimeTickContext(_frame,1d/60d,_frame/60d,RuntimeTickStage.Simulation);
            _commands.Tick(tick); if(_commands.LastCommandErrors.Count>0) throw new InvalidOperationException(_commands.LastCommandErrors[0].ToString());
            _probe.Runtime.Tick(tick);
        }
        private static void Mirror() { _actorView.transform.position=new Vector3(Origin.X/1000f,Origin.Y/1000f+0.9f,Origin.Z/1000f); }
        private static void AddWall(int id,Vector3 center,Vector3 size)
        {
            var body=new CombatBodyId(id); var c=new FixVector3(Fixed(center.x),Fixed(center.y),Fixed(center.z));
            var half=new FixVector3(Fixed(size.x/2),Fixed(size.y/2),Fixed(size.z/2));
            _probe.Runtime.PhysicsRuntime.World.UpsertBody(new CombatPhysicsBody(new CombatEntityId(id),body,FixVector3.Zero));
            _probe.Runtime.PhysicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(body,new CombatColliderId(id),1,c-half,c+half));
            Visual("Obstacle",center,size,PrimitiveType.Cube);
        }
        private static GameObject Visual(string name,Vector3 position,Vector3 scale,PrimitiveType kind)
        {
            var obj=GameObject.CreatePrimitive(kind);obj.name=name;obj.hideFlags=HideFlags.DontSave;obj.transform.SetParent(_root.transform);
            obj.transform.position=position;obj.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial=_material;return obj;
        }
        public static string Capture(string name)
        {
            RequireSafeContext();
            if (string.IsNullOrWhiteSpace(name) || System.IO.Path.GetFileName(name) != name || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A local capture filename is required.", nameof(name));
            var camera=_root.GetComponentInChildren<Camera>();
            var texture=new RenderTexture(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../.agent/SelectedPointLive",name+".png"));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());return path;
            }
            finally
            {camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(texture);}
        }
        public static string HidePreview()
        {
            RequireSafeContext();
            _preview.Hide();
            bool hidden=UnityEngine.Resources.FindObjectsOfTypeAll<LineRenderer>().Where(l=>l.name=="Range"||l.name=="Destination").All(l=>!l.gameObject.activeInHierarchy);
            Check("visual.hide",hidden,hidden);return Report();
        }
        private static Fix64 Fixed(float value) => Fix64.FromRatio(CharacterTestDeterministicUnits.MetersToMillimeters(value),1000);
        public static string Report() => JsonConvert.SerializeObject(new { playing=Application.isPlaying, frame=_frame,
            results=Results,scope="real isolated CombatPhysics and Runtime command/movement authority; mirrored visuals; no card settlement"},Formatting.Indented);
        public static string Cleanup()
        {
            _preview?.Dispose();_preview=null;_commands?.Dispose();_commands=null;_probe?.Dispose();_probe=null;
            if(_root!=null) UnityEngine.Object.DestroyImmediate(_root);if(_material!=null) UnityEngine.Object.DestroyImmediate(_material);
            _root=null;_material=null;_navigation=null;return "temporary fixture disposed";
        }
    }
}
