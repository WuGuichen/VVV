using System;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class TacticalDirectPlayTests
    {
        [Test]
        public void BattleProjectionKeepsActorFeetAboveHandAndHeadsOnScreen()
        {
            var owner=new GameObject("Battle projection fixture");
            try
            {
                var camera=owner.AddComponent<UnityEngine.Camera>(); camera.fieldOfView=60;
                camera.transform.position=new Vector3(0,6,-10);camera.transform.LookAt(Vector3.up);
                var feet=new[]{new Vector3(-2,0,-1),new Vector3(2,0,3)};
                float min=1,max=0;
                foreach(var foot in feet){min=Mathf.Min(min,camera.WorldToViewportPoint(foot).y);max=Mathf.Max(max,camera.WorldToViewportPoint(foot+Vector3.up*2.2f).y);}
                camera.projectionMatrix=CharacterTestFairyGuiTacticalBattleView.FitBattleProjection(camera.projectionMatrix,min,max);
                foreach(var foot in feet)
                {Assert.GreaterOrEqual(camera.WorldToViewportPoint(foot).y,.459f);Assert.LessOrEqual(camera.WorldToViewportPoint(foot+Vector3.up*2.2f).y,.921f);
                 var screen=camera.WorldToScreenPoint(foot); var ray=camera.ScreenPointToRay(screen);
                 Assert.IsTrue(new Plane(Vector3.up,0).Raycast(ray,out var distance));
                 Assert.Less(Vector3.Distance(foot,ray.GetPoint(distance)),.01f);}
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }

        [Test]
        public void OrdinaryPlayWaitsForSelectionThenCommitsExactlyOnce()
        {
            using var f=new Fixture();
            Assert.IsTrue(f.Hud.TryDirectPlayHandSlot(0));
            Assert.IsFalse(f.Hud.TryDirectPlayHandSlot(1));
            f.Hud.TickDirectPlay(); Assert.AreEqual(1,f.Pump.PendingCount); Assert.AreEqual(0,f.Ledger.Reserved);
            f.Pump.Drain(); Assert.AreEqual(10,f.Ledger.Reserved);
            f.Hud.TickDirectPlay(); f.Hud.TickDirectPlay(); Assert.AreEqual(1,f.Pump.PendingCount);
            f.Pump.Drain(); f.Hud.TickDirectPlay();
            Assert.AreEqual(1,f.Port.Admissions); Assert.IsFalse(f.Hud.DirectPlayActive);
            Assert.IsFalse(f.Hud.TryDirectPlayHandSlot(1));
        }
        [Test]
        public void CancelBeforeSelectionAcknowledgementReleasesReservationWithoutExecuting()
        {
            using var f=new Fixture();
            Assert.IsTrue(f.Hud.TryDirectPlayHandSlot(0)); f.Hud.CancelDirectPlay();
            f.Pump.Drain(); f.Hud.TickDirectPlay(); f.Pump.Drain(); f.Hud.TickDirectPlay();
            Assert.AreEqual(0,f.Port.Admissions); Assert.AreEqual(0,f.Ledger.Reserved);
            Assert.AreEqual(50,f.Ledger.Current); Assert.IsFalse(f.Hud.DirectPlayActive);
        }
        [Test]
        public void PointClickWaitsForValidPointThenExecutesWithoutConfirmationGesture()
        {
            using var f=new Fixture(point:true);
            Assert.IsTrue(f.Hud.TryDirectPlayHandSlot(0));f.Pump.Drain();f.Hud.TickDirectPlay();
            Assert.AreEqual(0,f.Pump.PendingCount);Assert.IsTrue(f.Hud.DirectPlayNeedsPoint);
            Assert.IsTrue(f.Hud.TryCompleteDirectPoint(new TacticalWorldPoint(1000,0,0)));
            f.Pump.Drain();f.Hud.TickDirectPlay();f.Pump.Drain();f.Hud.TickDirectPlay();
            Assert.AreEqual(1,f.Port.Admissions);Assert.AreEqual(1000,f.Port.Input.Point.X);
        }
        [Test]
        public void InvalidDragPointRollsBackReservedCost()
        {
            using var f=new Fixture(point:true);
            Assert.IsTrue(f.Hud.TryDirectPlayHandSlot(0,new TacticalWorldPoint(10000,0,0)));
            f.Pump.Drain();f.Hud.TickDirectPlay();f.Hud.TickDirectPlay();f.Pump.Drain();f.Hud.TickDirectPlay();
            Assert.AreEqual(0,f.Port.Admissions);Assert.AreEqual(0,f.Ledger.Reserved);Assert.AreEqual(50,f.Ledger.Current);
        }
        [Test]
        public void UnaffordableCardDoesNotSubmitOrReserve()
        {
            using var f=new Fixture(cost:80);
            Assert.IsFalse(f.Hud.TryDirectPlayHandSlot(0));Assert.AreEqual(0,f.Pump.PendingCount);
            Assert.AreEqual(0,f.Ledger.Reserved);Assert.AreEqual(50,f.Ledger.Current);
        }
        [Test]
        public void ExecutionRejectionFeedbackSurvivesAutomaticReservationRollback()
        {
            using var f=new Fixture();f.Port.PrepareIssue="ActionSummary:Unavailable:Stale:MeasurementInputsChanged;LoadedProviderMismatch";
            Assert.IsTrue(f.Hud.TryDirectPlayHandSlot(0));f.Pump.Drain();f.Hud.TickDirectPlay();f.Pump.Drain();
            Assert.AreEqual(TacticalStageRejection.ExecutionRejected,f.Stage.LastRejection);
            f.Hud.TickDirectPlay();f.Hud.TickDirectPlay();f.Pump.Drain();f.Hud.TickDirectPlay();f.Hud.RefreshIfChanged();
            Assert.AreEqual(CardZone.Hand,f.Hud.GetHandSlot(0).Zone);Assert.AreEqual(0,f.Ledger.Reserved);
            Assert.AreEqual(50,f.Ledger.Current);Assert.AreEqual(0,f.Port.Admissions);
            Assert.That(f.Hud.DirectPlayFeedback,Does.Contain("动作数据校验失败"));
            Assert.That(f.Hud.DirectPlayFeedback,Does.Not.Contain("LoadedProviderMismatch"));
        }
        internal sealed class Fixture:IDisposable
        {
            internal readonly TacticalCardHud Hud;
            internal readonly TacticalCommandPump Pump;
            internal readonly TacticalStageFlowCoordinator Stage;
            internal readonly MomentumLedger Ledger=new(50);
            internal readonly Port Port;
            private readonly GameObject _owner;
            private readonly GameObject _previewCamera;
            internal Fixture(bool point=false,int cost=10,int copies=1,ITacticalTimeAdapter adapter=null)
            {
                if(UnityEngine.Camera.main==null)
                {
                    _previewCamera=new GameObject("Direct drop camera fixture");_previewCamera.tag="MainCamera";
                    _previewCamera.AddComponent<UnityEngine.Camera>();
                }
                Port=new Port(point);var deck=new TacticalDeckManager();
                var time=new TacticalTimeCoordinator(adapter??new Clock(),new SettleDetector(new SettleDetectorConfig(requiredStableTicks:3)));
                var driver=new TacticalSingleCardDriver(Port,time,deck,Ledger);
                TacticalCardDefinition card=point ? TacticalActionCardDefinition.CreatePointMove("point","选点","",cost,
                    new TacticalPointMoveRules(5000,200,60,1000,true,TacticalPointMoveStartPolicy.OwnedMoveAccepted),0f,CardDestination.Discard)
                    : new TacticalActionCardDefinition("card","动作","",cost,"card",1,"hash");
                var cards=new TacticalCardDefinition[copies];for(int i=0;i<copies;i++)cards[i]=card;
                var stage=new TacticalStageFlowCoordinator(1,time,deck,Ledger,driver,cards,17,
                    _=>true,new TacticalStoryStepBinding(11,21,31),initialMomentum:50);
                Stage=stage;
                Pump=new TacticalCommandPump(stage);
                _owner=new GameObject("Direct play fixture");_owner.SetActive(false);Hud=_owner.AddComponent<TacticalCardHud>();Hud.Bind(stage,Pump);
                var token=new TacticalStoryStepToken(1,11,1,21,31);stage.OnStoryStepStarted(in token);
                stage.OnSpawnBound(1,0,new GameplayEntityId(1,1),0);stage.OnSpawnBound(1,1,new GameplayEntityId(2,1),0);
                for(long frame=1;frame<=3;frame++)
                {var sample=new SettleSample(frame,false,true,true,false,false,false,true,true,true,false,0,0,noActionStarted:true);stage.OnCompletedSimulationTick(frame,in sample,in sample);}
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_owner);
                if(_previewCamera!=null)UnityEngine.Object.DestroyImmediate(_previewCamera);
            }
        }
        private sealed class Clock:ITacticalTimeAdapter
        {public bool IsSimulationPaused{get;private set;}public void SetSimulationPaused(bool paused)=>IsSimulationPaused=paused;}
        internal sealed class Port:ITacticalExecutionInputPort,ITacticalPointSelectionPort
        {
            private readonly bool _point;internal int Admissions;internal TacticalExecutionInput Input;
            internal TacticalWorldPoint GroundPoint=new(1000,0,0);
            internal bool GroundAvailable=true;
            internal int GroundPickCount;
            internal string PrepareIssue="";
            internal Port(bool point)=>_point=point;
            public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card)=>_point?TacticalExecutionInputKind.WorldPoint:TacticalExecutionInputKind.None;
            public TacticalInputStatus ValidateInput(TacticalCardInstance card,in TacticalExecutionInput input)=>input.Kind!=TacticalExecutionInputKind.WorldPoint?TacticalInputStatus.MissingInput:input.Point.X>5000?TacticalInputStatus.OutOfRange:TacticalInputStatus.Valid;
            public bool TryGetPointOrigin(out TacticalWorldPoint origin){origin=default;return true;}
            public TacticalInputStatus TryPickGround(TacticalWorldPoint origin,TacticalWorldPoint direction,out TacticalWorldPoint point)
            {GroundPickCount++;point=GroundPoint;return GroundAvailable?TacticalInputStatus.Valid:TacticalInputStatus.GroundUnavailable;}
            public bool TryPrepare(TacticalCardInstance card,out string issue){issue=PrepareIssue;return string.IsNullOrEmpty(issue);}
            public bool TryAdmit(TacticalCardInstance card,out ulong id,out string issue){Admissions++;id=1;issue="";return true;}
            public bool TryPrepare(TacticalCardInstance card,in TacticalExecutionInput input,out string issue)=>TryPrepare(card,out issue);
            public bool TryAdmit(TacticalCardInstance card,in TacticalExecutionInput input,out ulong id,out string issue){Input=input;return TryAdmit(card,out id,out issue);}
            public ExecutionObservation Observe(ulong id)=>new(id,false,false,false,true,true);
            public bool TryAbortPreAction(ulong id,string reason)=>true;
        }
    }
}
