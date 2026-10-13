using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerEnvironmentSample
    {
        public int SurfaceKind;
        public bool OnHardSlick;
        public bool NearMovableObject;
        public bool NearObstacle;
        public bool NearHazard;
        public bool NearUsefulObject;

        public void Clear()
        {
            SurfaceKind = 0;
            OnHardSlick = false;
            NearMovableObject = false;
            NearObstacle = false;
            NearHazard = false;
            NearUsefulObject = false;
        }
    }

    internal interface ICharacterTestRuntimeAiPlannerPerceptionSource
    {
        void SampleEnvironment(
            CharacterTestCharacterEntity self,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            CharacterTestRuntimeAiPlannerEnvironmentSample sample);

        bool TryResolveLineOfSightBlocked(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            out bool blocked);
    }

    internal sealed class CharacterTestRuntimeAiPlannerWorldPerceptionSource
        : ICharacterTestRuntimeAiPlannerPerceptionSource
    {
        public static readonly CharacterTestRuntimeAiPlannerWorldPerceptionSource Empty =
            new CharacterTestRuntimeAiPlannerWorldPerceptionSource(null);

        private const int EnvironmentQueryId = 910200;
        private const int LineOfSightQueryId = 910210;
        private const float DirectionEpsilon = 0.0001f;

        private readonly CharacterTestCharacterRuntimeModule _runtimeModule;
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>(8);

        public CharacterTestRuntimeAiPlannerWorldPerceptionSource(
            CharacterTestCharacterRuntimeModule runtimeModule)
        {
            _runtimeModule = runtimeModule;
        }

        public void SampleEnvironment(
            CharacterTestCharacterEntity self,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            CharacterTestRuntimeAiPlannerEnvironmentSample sample)
        {
            if (sample == null)
                return;

            sample.Clear();
            if (self == null)
                return;

            sample.SurfaceKind = (int)self.SurfaceMaterialKind;
            sample.OnHardSlick = self.SurfaceMaterialKind == CombatSurfaceMaterialKind.HardSlick;

            CharacterTestRuntimeAiPlannerPerceptionSettings safeSettings =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
            float radius = safeSettings.EnvironmentSampleRadius;
            if (radius <= 0f)
                return;

            SampleMovableObjects(self, radius, sample);
            SampleStaticObstacles(self, safeSettings, sample);
        }

        public bool TryResolveLineOfSightBlocked(
            CharacterTestCharacterEntity self,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            out bool blocked)
        {
            blocked = false;
            CharacterTestCharacterPhysicsRuntime physicsRuntime = _runtimeModule?.PhysicsRuntime;
            if (self == null || target == null || physicsRuntime == null || !physicsRuntime.IsReady)
                return false;

            Vector2 toTarget = CharacterTestDirectAiNavigationService.ToTarget(self, target);
            float distance = toTarget.magnitude;
            if (distance <= DirectionEpsilon)
                return true;

            CombatPhysicsLayerMask layerMask = ResolveLineOfSightLayerMask(physicsRuntime, settings);
            if (layerMask.Equals(CombatPhysicsLayerMask.None))
                return false;

            FixVector3 origin = new FixVector3(
                ToFix64(self.Transform.X),
                ToFix64(self.Transform.Y) + physicsRuntime.CharacterHalfExtents.Y,
                ToFix64(self.Transform.Z));
            Vector2 direction = toTarget / distance;
            FixVector3 rayDirection = new FixVector3(
                ToFix64(direction.x),
                Fix64.Zero,
                ToFix64(direction.y));
            var header = new CombatQueryHeader(
                LineOfSightQueryId,
                CombatQueryKind.Ray,
                physicsRuntime.GetEntityId(self),
                LineOfSightQueryId,
                actionId: 0,
                sourceOrder: 0,
                layerMask);

            _queryResults.Clear();
            physicsRuntime.World.QueryRay(
                new CombatRayQuery(header, origin, rayDirection, ToFix64(distance)),
                _queryResults);
            blocked = _queryResults.Count > 0;
            return true;
        }

        private void SampleMovableObjects(
            CharacterTestCharacterEntity self,
            float radius,
            CharacterTestRuntimeAiPlannerEnvironmentSample sample)
        {
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects =
                _runtimeModule?.MovableNaturalObjects;
            if (objects == null || objects.Count == 0)
                return;

            float radiusSquared = radius * radius;
            float selfX = self.Transform.X;
            float selfZ = self.Transform.Z;
            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState obj = objects[i];
                if (obj == null)
                    continue;

                float dx = obj.X - selfX;
                float dz = obj.Z - selfZ;
                if (dx * dx + dz * dz > radiusSquared)
                    continue;

                sample.NearMovableObject = true;
                sample.NearUsefulObject = true;
                if (HasHazardPotency(obj))
                {
                    sample.NearHazard = true;
                    return;
                }
            }
        }

        private void SampleStaticObstacles(
            CharacterTestCharacterEntity self,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            CharacterTestRuntimeAiPlannerEnvironmentSample sample)
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime = _runtimeModule?.PhysicsRuntime;
            if (physicsRuntime == null || !physicsRuntime.IsReady)
                return;

            CombatPhysicsLayerMask layerMask = ResolveEnvironmentObstacleLayerMask(physicsRuntime, settings);
            if (layerMask.Equals(CombatPhysicsLayerMask.None))
                return;

            var header = new CombatQueryHeader(
                EnvironmentQueryId,
                CombatQueryKind.Sphere,
                physicsRuntime.GetEntityId(self),
                EnvironmentQueryId,
                actionId: 0,
                sourceOrder: 0,
                layerMask);
            FixVector3 center = new FixVector3(
                ToFix64(self.Transform.X),
                ToFix64(self.Transform.Y) + physicsRuntime.CharacterHalfExtents.Y,
                ToFix64(self.Transform.Z));

            _queryResults.Clear();
            physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, center, ToFix64(settings.EnvironmentSampleRadius)),
                _queryResults);
            sample.NearObstacle = _queryResults.Count > 0;
        }

        private static CombatPhysicsLayerMask ResolveEnvironmentObstacleLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings)
        {
            if (settings != null && settings.LineOfSightObstacleLayerMask > 0)
                return new CombatPhysicsLayerMask(settings.LineOfSightObstacleLayerMask);

            return physicsRuntime.StaticObstacleLayerMask;
        }

        private static CombatPhysicsLayerMask ResolveLineOfSightLayerMask(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings)
        {
            if (settings != null && settings.LineOfSightObstacleLayerMask > 0)
                return new CombatPhysicsLayerMask(settings.LineOfSightObstacleLayerMask);

            return physicsRuntime.ObstacleLayerMask;
        }

        private static bool HasHazardPotency(CharacterTestMovableNaturalObjectState obj)
        {
            CharacterTestGenesisPotencyTriplet potency = obj.CurrentPotency;
            return potency.Motion != 0 || potency.Cohesion != 0 || potency.Scatter != 0;
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}
