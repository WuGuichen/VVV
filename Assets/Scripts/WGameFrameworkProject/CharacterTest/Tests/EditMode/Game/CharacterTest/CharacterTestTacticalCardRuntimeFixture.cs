using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Exercises the production card/session, command queue, action gateway and combat simulation together.</summary>
    internal sealed class CharacterTestTacticalCardRuntimeFixture : IDisposable
    {
        private readonly string _directory;
        private bool _disposed;
        private readonly int _referenceSurfaceOffsetMillimeters;
        internal CharacterTestTacticalCardRuntimeFixture(
            CharacterTestTacticalCardApproach approach = CharacterTestTacticalCardApproach.OptimalRange,
            bool offsetDummy = false, int actionId = 0,
            int sourceCharacterId = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
            CharacterActionSpatialMeasurement measurement = null, CharacterTestCharacterActionRuntime actionRuntime = null,
            MxFramework.Gameplay.AbilityRuntimeModule abilityRuntime = null)
        {
            _directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Library/MxFramework/TacticalCardRuntimeTests", Guid.NewGuid().ToString("N")));
            Measurement = measurement ?? new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, offsetDummy ? -2000 : 0));
            Probe = actionId > 0
                ? new CharacterActionReferenceProbeSession(new CharacterActionId(actionId), Measurement.ReferenceDummy, sourceCharacterId, actionRuntime, abilityRuntime)
                : CharacterActionReferenceProbeSession.ForCurrentOpener(sourceCharacterId, Measurement.ReferenceDummy,
                    actionRuntime: actionRuntime, abilityRuntime: abilityRuntime);
            try
            {
                Store = new CharacterActionTacticalMetricsStore(_directory);
                var measured = Store.Refresh(Probe, Measurement);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
                Assert.IsTrue(CharacterTestTacticalCardInputTests.Prepare(Probe, Store, Measurement, out var inputs, out string issue), issue);
                Inputs = inputs;
                int warmDistance = Inputs.Metrics.SpatialSummary.VerifiedOptimalMillimeters.Value;
                Assert.IsTrue(Probe.TryEvaluate(warmDistance, out _, out issue), issue);
                _referenceSurfaceOffsetMillimeters = Mathf.RoundToInt(RootDistance * 1000f) - warmDistance;
                Assert.IsTrue(Runtime.TryGetLastCompletedRuntimeFrame(out var completed));
                NextFrame = checked((int)completed.Value + 1);
                // Measurement uses an inert actor. Runtime acceptance must explicitly give the
                // validation actor a formal command-driven owner, without enabling another AI loop.
                Assert.IsTrue(Runtime.TrySetControlOwner(Source.Id.Value, CharacterTestCharacterControlOwner.RemotePlayer));
                Commands = new CharacterTestRuntimeCommandModule(Runtime, NullRuntimeLogger.Instance);
                Binding = new CharacterTestTacticalCardBinding(new CharacterTestTacticalCardRuntimeInputs(
                    Runtime, Source.Id.Value, Store, Measurement));
                Definition = new CharacterTestTacticalCardDefinition("Runtime single attack", approach);
                Card = new CharacterTestTacticalCardSession(Runtime, Source.Id.Value, Binding, Commands.Enqueue);
                // Compile the same explicitly selected action that supplied the measured inputs.
                Assert.IsTrue(Card.TryCompile(Definition, Probe.Action.ActionId.Value), Card.LastIssue);
                RefreshFacts();
            }
            catch { Probe.Dispose(); DeleteCache(); throw; }
        }

        internal CharacterActionReferenceProbeSession Probe { get; }
        internal CharacterTestCharacterRuntimeModule Runtime => Probe.Runtime;
        internal CharacterTestCharacterEntity Source => Probe.Source;
        internal CharacterTestCharacterEntity Target => Probe.Target;
        internal CharacterTestTacticalCardDefinition Definition { get; }
        internal CharacterTestTacticalCardBinding Binding { get; }
        internal CharacterTestTacticalCardSession Card { get; }
        internal CharacterTestRuntimeAiPlannerFactState Facts { get; } = new CharacterTestRuntimeAiPlannerFactState();
        internal CharacterTestRuntimeCommandModule Commands { get; }
        internal CharacterActionSpatialMeasurement Measurement { get; }
        internal CharacterActionTacticalMetricsStore Store { get; }
        internal CharacterTestTacticalCardInputs Inputs { get; }
        internal int NextFrame { get; private set; }
        internal List<Sample> Samples { get; } = new List<Sample>();
        internal float RootDistance => Vector2.Distance(new Vector2(Source.Transform.X, Source.Transform.Z),
            new Vector2(Target.Transform.X, Target.Transform.Z));

        /// <summary>Positions the ground reference using its measured root-to-surface offset and seals the new geometry.</summary>
        internal void PlaceTargetAtSurfaceDistance(int surfaceDistanceMillimeters)
        {
            PlaceTarget(surfaceDistanceMillimeters + _referenceSurfaceOffsetMillimeters);
            AdvanceWorldOnly();
            RefreshFacts();
        }

        internal void PlaceTarget(int rootDistanceMm, int yMm = 0)
        {
            Target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                Source.Transform.XMillimeters, yMm, Source.Transform.ZMillimeters + rootDistanceMm, 0));
            Target.SetMotionState(0, Vector2.zero, isGrounded: true);
            RefreshFacts();
        }

        internal void RefreshFacts(bool includeHurtboxDistance = true)
        {
            Assert.IsTrue(CharacterTestDirectAiNavigationService.Instance.TryResolveMove(Source, Target,
                (float)Inputs.Metrics.SpatialSummary.MaxDistance.Value, out var navigation));
            CharacterTestLockedHurtboxDistanceSample distance = default;
            bool hasDistance = includeHurtboxDistance && Runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                new RuntimeFrame(NextFrame), Source, Target, out _, out distance) && distance.HasDistance;
            Facts.SetTarget(Target.Id.Value, includeHurtboxDistance && hasDistance ? distance.SurfaceGapMeters : 0f,
                targetDistanceAvailable: includeHurtboxDistance && hasDistance, targetRootDistance: RootDistance,
                navigation: navigation, selfActionRunning: Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, targetVisible: true,
                targetLineOfSightBlocked: false, targetLastSeenAgeFrames: 0, targetThreatScore: 1f,
                targetDead: !Target.CombatState.IsAlive);
        }

        internal bool Start()
        {
            RefreshFacts();
            return Card.TryStart(Definition, Facts, new RuntimeFrame(NextFrame));
        }

        /// <summary>The optional observer runs after the card sampled, before commands/world consume the frame.</summary>
        internal void Step(Action<RuntimeFrame> afterCardTick = null)
        {
            RefreshFacts();
            int before = Card.LocalFrame;
            Card.Tick(Facts, new RuntimeFrame(NextFrame));
            afterCardTick?.Invoke(new RuntimeFrame(NextFrame));
            ConsumeWorld(before);
        }

        /// <summary>Seals changed geometry without sampling the card; preserves monotonically increasing world frames.</summary>
        internal void AdvanceWorldOnly() => ConsumeWorld(Card.LocalFrame);

        private void ConsumeWorld(int cursorBefore)
        {
            int consumedFrame = NextFrame++;
            var tick = new RuntimeTickContext(consumedFrame, 1d / 60d, consumedFrame / 60d, RuntimeTickStage.Simulation);
            Commands.Tick(tick);
            Assert.AreEqual(0, Commands.LastCommandErrors.Count,
                Commands.LastCommandErrors.Count == 0 ? string.Empty : Commands.LastCommandErrors[0].ToString());
            var consumed = Source.Input;
            Vector2 consumedMove = consumed.HasWorldMove ? consumed.WorldMove : consumed.Move;
            var before = Source.Transform;
            Runtime.Tick(tick);
            Runtime.ActionRuntime.TryGetActiveActionPresentationState(Source.Id, out long instance, out var actionId,
                out int actionFrame, out _);
            Samples.Add(new Sample
            {
                WorldFrame = consumedFrame, CursorBefore = cursorBefore, CursorAfter = Card.LocalFrame,
                SourceX = Source.Transform.XMillimeters, SourceZ = Source.Transform.ZMillimeters,
                DeltaX = Source.Transform.XMillimeters - before.XMillimeters,
                DeltaZ = Source.Transform.ZMillimeters - before.ZMillimeters,
                Yaw = Source.Transform.YawMilliDegrees, MoveX = consumedMove.x, MoveZ = consumedMove.y,
                HasMove = consumed.HasActiveMove, FacingMode = consumed.BehaviorControl.FacingMode,
                MoveLimit = consumed.BehaviorControl.MaxMoveStepMillimeters,
                ActionInstance = instance, ActionId = actionId.Value, ActionFrame = actionFrame,
                HitCount = Source.LastCombatHitCount, TargetHealth = Target.CombatState.Health,
                RootDistance = RootDistance
            });
        }

        internal void StopAndDrain()
        {
            RefreshFacts();
            Card.Stop(Facts, new RuntimeFrame(NextFrame));
            AdvanceWorldOnly();
            for (int i = 0; i < 5 && Card.IsExecuting; i++) Step();
            Assert.IsFalse(Card.IsExecuting, Card.LastIssue);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (Card != null && Card.IsExecuting) StopAndDrain(); }
            catch (Exception error)
            {
                // Scenarios explicitly assert cleanup. Disposal must not replace the original
                // command/simulation assertion with a second error during exception unwinding.
                TestContext.WriteLine("Cleanup after scenario: " + error);
            }
            finally { Commands?.Dispose(); Probe?.Dispose(); DeleteCache(); }
        }

        private void DeleteCache()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Library/MxFramework/TacticalCardRuntimeTests")) + Path.DirectorySeparatorChar;
            if (_directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        internal sealed class Sample
        {
            public int WorldFrame, CursorBefore, CursorAfter, SourceX, SourceZ, DeltaX, DeltaZ, Yaw;
            public float MoveX, MoveZ, RootDistance;
            public bool HasMove;
            public int FacingMode, MoveLimit, ActionId, ActionFrame, HitCount, TargetHealth;
            public long ActionInstance;
        }
    }
}
