using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Combat Physics Scene")]
    public sealed class CharacterTestCombatPhysicsSceneBridge : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Vector3 _characterHalfExtents = new Vector3(0.45f, 0.9f, 0.45f);
        [SerializeField] private float _skinWidth = 0.01f;
        [SerializeField] private float _maxStepHeight = 0.35f;
        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawDebugGizmos = true;
        [SerializeField] private bool _drawDebugGizmosOnlyWhenSelected;
        [SerializeField] private Color _debugAabbColor = new Color(0.2f, 0.8f, 1f, 0.75f);
        [SerializeField] private Color _debugSphereColor = new Color(0.2f, 1f, 0.35f, 0.75f);
        [SerializeField] private Color _debugCapsuleColor = new Color(1f, 0.75f, 0.2f, 0.75f);
        private readonly List<CharacterTestCombatPhysicsAabbMarker> _gizmoAabbMarkers = new();
        private readonly List<CharacterTestCombatPhysicsSphereMarker> _gizmoSphereMarkers = new();
        private readonly List<CharacterTestCombatPhysicsCapsuleMarker> _gizmoCapsuleMarkers = new();
        private readonly List<CharacterTestWorldObjectSceneSource> _gizmoSources = new();
        private readonly List<GameObject> _gizmoSceneRoots = new();

        public bool Enabled => _enabled;

        public CharacterTestCharacterPhysicsRuntime BuildRuntime()
        {
            if (!_enabled)
                return null;

            CharacterTestCombatLevelSceneAdapterResult levelResult =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(this);
            LogLevelIssues(levelResult.AdapterReport);
            LogLevelIssues(levelResult.DefinitionReport);
            if (levelResult.HasErrors)
                return null;

            CombatLevelDefinition level = levelResult.Definition;
            CombatPhysicsWorld world = CombatLevelWorldBuilder.BuildWorld(level);
            int mask = 0;
            for (int i = 0; i < level.AabbColliders.Count; i++)
            {
                mask |= 1 << level.AabbColliders[i].Layer;
            }
            for (int i = 0; i < level.ObbColliders.Count; i++)
            {
                mask |= 1 << level.ObbColliders[i].Layer;
            }

            int appendedShapeColliderCount = AppendSceneShapeColliders(world, ref mask);
            if (world.TotalColliderCount == 0)
                return null;

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                new CombatPhysicsLayerMask(mask),
                ToFixVector3(MaxVector3(_characterHalfExtents, new Vector3(0.01f, 0.01f, 0.01f))),
                ToFix64(Mathf.Max(0f, _skinWidth)),
                ToFix64(Mathf.Max(0f, _maxStepHeight)),
                world.TotalColliderCount,
                "level=" + level.LevelStableId
                + " hash=" + levelResult.ContentHash
                + " sceneShapeColliders=" + appendedShapeColliderCount);
        }

        public static CharacterTestCharacterPhysicsRuntime BuildRuntime(
            Transform root,
            CharacterTestWorldObjectSceneCache cache,
            CharacterTestScenePhysicsSettings settings)
        {
            if (root == null || cache == null || settings == null || !settings.Enabled)
                return null;

            CharacterTestCombatLevelSceneAdapterResult levelResult =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(root, cache);
            LogLevelIssues(levelResult.AdapterReport);
            LogLevelIssues(levelResult.DefinitionReport);
            if (levelResult.HasErrors)
                return null;

            CombatLevelDefinition level = levelResult.Definition;
            CombatPhysicsWorld world = CombatLevelWorldBuilder.BuildWorld(level);
            int mask = 0;
            for (int i = 0; i < level.AabbColliders.Count; i++)
                mask |= 1 << level.AabbColliders[i].Layer;
            for (int i = 0; i < level.ObbColliders.Count; i++)
                mask |= 1 << level.ObbColliders[i].Layer;

            CharacterTestWorldObjectStaticColliderSpec[] specs =
                cache.CollectStaticColliderSpecs(out _);
            int appendedShapeColliderCount =
                AppendSourceShapeColliders(root, world, specs, ref mask);
            if (world.TotalColliderCount == 0)
                return null;

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                new CombatPhysicsLayerMask(mask),
                ToFixVector3(settings.CharacterHalfExtents),
                ToFix64(settings.SkinWidth),
                ToFix64(settings.MaxStepHeight),
                world.TotalColliderCount,
                "level=" + level.LevelStableId
                + " hash=" + levelResult.ContentHash
                + " sceneShapeColliders=" + appendedShapeColliderCount,
                ToFix64(settings.GroundSnapDistance));
        }

        private static void LogLevelIssues(CombatLevelValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                CombatLevelValidationIssue issue = report.Issues[i];
                Debug.LogWarning(
                    "CharacterTest combat level issue: "
                    + issue.Severity
                    + " "
                    + issue.Code
                    + " "
                    + issue.Path
                    + " - "
                    + issue.Message);
            }
        }

        private static Vector3 MaxVector3(Vector3 value, Vector3 min)
        {
            return new Vector3(
                Mathf.Max(value.x, min.x),
                Mathf.Max(value.y, min.y),
                Mathf.Max(value.z, min.z));
        }

        private int AppendSceneShapeColliders(CombatPhysicsWorld world, ref int obstacleMask)
        {
            if (world == null)
                return 0;

            var reservedBodyIds = new HashSet<int>();
            var bodies = new List<CombatPhysicsBody>();
            world.CopyBodiesTo(bodies);
            for (int i = 0; i < bodies.Count; i++)
                reservedBodyIds.Add(bodies[i].BodyId.Value);

            int nextBodyId = CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart;
            int count = 0;
            CharacterTestWorldObjectSceneSource worldObjectSource =
                CharacterTestWorldObjectSceneSource.FindFor(transform);
            if (worldObjectSource != null && worldObjectSource.StaticColliderInstanceCount > 0)
            {
                CharacterTestWorldObjectStaticColliderSpec[] specs =
                    worldObjectSource.CollectStaticColliderSpecs(out _);
                count += AppendSourceShapeColliders(transform, world, specs, reservedBodyIds, ref nextBodyId, ref obstacleMask);
            }
            else
            {
                count += AppendSphereColliders(world, reservedBodyIds, ref nextBodyId, ref obstacleMask);
                count += AppendCapsuleColliders(world, reservedBodyIds, ref nextBodyId, ref obstacleMask);
            }
            return count;
        }

        private static int AppendSourceShapeColliders(
            Transform root,
            CombatPhysicsWorld world,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> specs,
            ref int obstacleMask)
        {
            if (world == null)
                return 0;

            var reservedBodyIds = new HashSet<int>();
            var bodies = new List<CombatPhysicsBody>();
            world.CopyBodiesTo(bodies);
            for (int i = 0; i < bodies.Count; i++)
                reservedBodyIds.Add(bodies[i].BodyId.Value);

            int nextBodyId = CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart;
            return AppendSourceShapeColliders(root, world, specs, reservedBodyIds, ref nextBodyId, ref obstacleMask);
        }

        private static int AppendSourceShapeColliders(
            Transform root,
            CombatPhysicsWorld world,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> specs,
            HashSet<int> reservedBodyIds,
            ref int nextBodyId,
            ref int obstacleMask)
        {
            if (specs == null)
                return 0;

            int count = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                if (spec.Transform == null)
                    continue;
                if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb)
                    continue;

                int bodyIdValue = ResolveBodyId(spec.BodyId, reservedBodyIds, ref nextBodyId);
                int colliderIdValue = spec.ColliderId > 0 ? spec.ColliderId : 1;
                var bodyId = new CombatBodyId(bodyIdValue);
                var colliderId = new CombatColliderId(colliderIdValue);
                if (HasAnyCollider(world, bodyId, colliderId))
                {
                    Debug.LogWarning(CreateDuplicateColliderMessage(
                        root,
                        spec.ShapeKind.ToString(),
                        spec.Transform,
                        bodyIdValue,
                        colliderIdValue));
                    continue;
                }

                FixVector3 markerPosition = ToFixVector3(spec.Position);
                FixVector3 bodyPosition = EnsureBodyAndGetPosition(world, bodyId, bodyIdValue, markerPosition);
                if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere)
                {
                    world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                        bodyId,
                        colliderId,
                        spec.Layer,
                        markerPosition - bodyPosition,
                        ToFix64(spec.SphereRadius),
                        spec.SurfaceMaterial));
                    obstacleMask |= 1 << spec.Layer;
                    count++;
                    continue;
                }

                if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule)
                {
                    world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                        bodyId,
                        colliderId,
                        spec.Layer,
                        ToFixVector3(spec.CapsuleWorldPointA) - bodyPosition,
                        ToFixVector3(spec.CapsuleWorldPointB) - bodyPosition,
                        ToFix64(spec.CapsuleRadius),
                        spec.SurfaceMaterial));
                    obstacleMask |= 1 << spec.Layer;
                    count++;
                }
            }

            return count;
        }

        private int AppendSphereColliders(
            CombatPhysicsWorld world,
            HashSet<int> reservedBodyIds,
            ref int nextBodyId,
            ref int obstacleMask)
        {
            var markers = new List<CharacterTestCombatPhysicsSphereMarker>();
            GetComponentsInChildren(includeInactive: true, markers);
            markers.Sort(CompareMarkerTransforms);

            int count = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestCombatPhysicsSphereMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled)
                    continue;

                int bodyIdValue = ResolveBodyId(marker.BodyId, reservedBodyIds, ref nextBodyId);
                int colliderIdValue = marker.ColliderId > 0 ? marker.ColliderId : 1;
                var bodyId = new CombatBodyId(bodyIdValue);
                var colliderId = new CombatColliderId(colliderIdValue);
                if (HasAnyCollider(world, bodyId, colliderId))
                {
                    Debug.LogWarning(CreateDuplicateColliderMessage(
                        transform,
                        "Sphere",
                        marker.transform,
                        bodyIdValue,
                        colliderIdValue));
                    continue;
                }

                int layer = Mathf.Clamp(marker.CombatLayer, 0, 31);
                FixVector3 markerPosition = ToFixVector3(marker.transform.position);
                FixVector3 bodyPosition = EnsureBodyAndGetPosition(world, bodyId, bodyIdValue, markerPosition);
                world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                    bodyId,
                    colliderId,
                    layer,
                    markerPosition - bodyPosition,
                    ToFix64(marker.Radius),
                    marker.SurfaceMaterial));
                obstacleMask |= 1 << layer;
                count++;
            }

            return count;
        }

        private int AppendCapsuleColliders(
            CombatPhysicsWorld world,
            HashSet<int> reservedBodyIds,
            ref int nextBodyId,
            ref int obstacleMask)
        {
            var markers = new List<CharacterTestCombatPhysicsCapsuleMarker>();
            GetComponentsInChildren(includeInactive: true, markers);
            markers.Sort(CompareMarkerTransforms);

            int count = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestCombatPhysicsCapsuleMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled)
                    continue;

                int bodyIdValue = ResolveBodyId(marker.BodyId, reservedBodyIds, ref nextBodyId);
                int colliderIdValue = marker.ColliderId > 0 ? marker.ColliderId : 1;
                var bodyId = new CombatBodyId(bodyIdValue);
                var colliderId = new CombatColliderId(colliderIdValue);
                if (HasAnyCollider(world, bodyId, colliderId))
                {
                    Debug.LogWarning(CreateDuplicateColliderMessage(
                        transform,
                        "Capsule",
                        marker.transform,
                        bodyIdValue,
                        colliderIdValue));
                    continue;
                }

                int layer = Mathf.Clamp(marker.CombatLayer, 0, 31);
                FixVector3 markerPosition = ToFixVector3(marker.transform.position);
                FixVector3 bodyPosition = EnsureBodyAndGetPosition(world, bodyId, bodyIdValue, markerPosition);
                world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                    bodyId,
                    colliderId,
                    layer,
                    ToFixVector3(marker.WorldPointA) - bodyPosition,
                    ToFixVector3(marker.WorldPointB) - bodyPosition,
                    ToFix64(marker.Radius),
                    marker.SurfaceMaterial));
                obstacleMask |= 1 << layer;
                count++;
            }

            return count;
        }

        private static int ResolveBodyId(
            int markerBodyId,
            HashSet<int> reservedBodyIds,
            ref int nextBodyId)
        {
            if (markerBodyId > 0)
            {
                reservedBodyIds.Add(markerBodyId);
                return markerBodyId;
            }

            while (reservedBodyIds.Contains(nextBodyId))
                nextBodyId++;

            int resolved = nextBodyId;
            reservedBodyIds.Add(resolved);
            nextBodyId++;
            return resolved;
        }

        private static bool HasAnyCollider(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            CombatColliderId colliderId)
        {
            return world.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider _)
                || world.TryGetSphereCollider(bodyId, colliderId, out CombatPhysicsSphereCollider _)
                || world.TryGetCapsuleCollider(bodyId, colliderId, out CombatPhysicsCapsuleCollider _)
                || world.TryGetObbCollider(bodyId, colliderId, out CombatPhysicsObbCollider _);
        }

        private static FixVector3 EnsureBodyAndGetPosition(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            int bodyIdValue,
            FixVector3 fallbackPosition)
        {
            if (world.TryGetBody(bodyId, out CombatPhysicsBody existingBody))
                return existingBody.Position;

            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyIdValue), bodyId, fallbackPosition));
            return fallbackPosition;
        }

        private static string CreateDuplicateColliderMessage(
            Transform root,
            string shapeName,
            Transform marker,
            int bodyId,
            int colliderId)
        {
            return "CharacterTest combat physics " + shapeName
                + " marker skipped because body/collider id is already registered: "
                + ResolveTransformPath(root, marker)
                + " bodyId=" + bodyId
                + " colliderId=" + colliderId;
        }

        private static int CompareMarkerTransforms<T>(T left, T right)
            where T : Component
        {
            return string.CompareOrdinal(
                ResolveTransformPath(null, left == null ? null : left.transform),
                ResolveTransformPath(null, right == null ? null : right.transform));
        }

        private static string ResolveTransformPath(Transform root, Transform target)
        {
            if (target == null)
                return "missing";

            var segments = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            if (segments.Count == 0)
                return target.name + "[" + target.GetSiblingIndex() + "]";

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        internal static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(ToFix64(value.x), ToFix64(value.y), ToFix64(value.z));
        }

        internal static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)System.Math.Round(value * Fix64.Scale));
        }

        private void OnDrawGizmos()
        {
            if (_drawDebugGizmosOnlyWhenSelected)
                return;

            DrawDebugGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawDebugGizmos();
        }

        private void DrawDebugGizmos()
        {
            if (!_drawDebugGizmos || !_enabled)
                return;

            CharacterTestWorldObjectSceneSource worldObjectSource =
                FindGizmoWorldObjectSource();
            if (worldObjectSource != null && worldObjectSource.StaticColliderInstanceCount > 0)
            {
                CharacterTestWorldObjectStaticColliderSpec[] specs =
                    worldObjectSource.CollectStaticColliderSpecs(out _);
                for (int i = 0; i < specs.Length; i++)
                {
                    CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                    Color color = spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere
                        ? _debugSphereColor
                        : spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule
                            ? _debugCapsuleColor
                            : _debugAabbColor;
                    CharacterTestCombatPhysicsGizmos.DrawStaticColliderSpec(spec, color);
                }

                return;
            }

            var aabbMarkers = _gizmoAabbMarkers;
            GetComponentsInChildren(includeInactive: true, aabbMarkers);
            for (int i = 0; i < aabbMarkers.Count; i++)
                CharacterTestCombatPhysicsGizmos.DrawAabbMarker(aabbMarkers[i], _debugAabbColor);

            var sphereMarkers = _gizmoSphereMarkers;
            GetComponentsInChildren(includeInactive: true, sphereMarkers);
            for (int i = 0; i < sphereMarkers.Count; i++)
                CharacterTestCombatPhysicsGizmos.DrawSphereMarker(sphereMarkers[i], _debugSphereColor);

            var capsuleMarkers = _gizmoCapsuleMarkers;
            GetComponentsInChildren(includeInactive: true, capsuleMarkers);
            for (int i = 0; i < capsuleMarkers.Count; i++)
                CharacterTestCombatPhysicsGizmos.DrawCapsuleMarker(capsuleMarkers[i], _debugCapsuleColor);
        }

        // Reuse query buffers while still discovering scene/authoring changes on every repaint.
        private CharacterTestWorldObjectSceneSource FindGizmoWorldObjectSource()
        {
            GetComponentsInChildren(includeInactive: true, _gizmoSources);
            if (_gizmoSources.Count > 0 && _gizmoSources[0].Enabled) return _gizmoSources[0];
            for (int sceneIndex = 0; sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount; sceneIndex++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                if (_gizmoSceneRoots.Capacity < scene.rootCount) _gizmoSceneRoots.Capacity = scene.rootCount;
                scene.GetRootGameObjects(_gizmoSceneRoots);
                for (int rootIndex = 0; rootIndex < _gizmoSceneRoots.Count; rootIndex++)
                {
                    _gizmoSceneRoots[rootIndex].GetComponentsInChildren(includeInactive: false, _gizmoSources);
                    for (int i = 0; i < _gizmoSources.Count; i++)
                        if (_gizmoSources[i].gameObject.activeInHierarchy && _gizmoSources[i].Enabled)
                            return _gizmoSources[i];
                }
            }
            return null;
        }
    }

    internal static class CharacterTestCombatPhysicsGizmos
    {
        internal sealed class RuntimeBuffers
        {
            internal readonly List<CombatPhysicsAabbCollider> Aabbs = new();
            internal readonly List<CombatPhysicsSphereCollider> Spheres = new();
            internal readonly List<CombatPhysicsCapsuleCollider> Capsules = new();
            internal readonly List<CombatPhysicsObbCollider> Obbs = new();
        }

        public static void DrawRuntimeWorld(
            CharacterTestCharacterPhysicsRuntime runtime,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            Color aabbColor,
            Color characterColor,
            RuntimeBuffers buffers)
        {
            if (runtime == null || runtime.World == null)
                return;

            var aabbs = buffers.Aabbs;
            aabbs.Clear();
            runtime.World.CopyAabbCollidersTo(aabbs);
            for (int i = 0; i < aabbs.Count; i++)
            {
                CombatPhysicsAabbCollider collider = aabbs[i];
                if (!runtime.World.TryGetBody(collider.BodyId, out CombatPhysicsBody body))
                    continue;

                Vector3 min = ToVector3(collider.GetWorldMin(body.Position));
                Vector3 max = ToVector3(collider.GetWorldMax(body.Position));
                DrawAabb(min, max, aabbColor);
            }

            var spheres = buffers.Spheres;
            spheres.Clear();
            runtime.World.CopySphereCollidersTo(spheres);
            for (int i = 0; i < spheres.Count; i++)
            {
                CombatPhysicsSphereCollider collider = spheres[i];
                if (!runtime.World.TryGetBody(collider.BodyId, out CombatPhysicsBody body))
                    continue;

                DrawSphere(ToVector3(collider.GetWorldCenter(body.Position)), ToFloat(collider.Radius), aabbColor);
            }

            var capsules = buffers.Capsules;
            capsules.Clear();
            runtime.World.CopyCapsuleCollidersTo(capsules);
            for (int i = 0; i < capsules.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = capsules[i];
                if (!runtime.World.TryGetBody(collider.BodyId, out CombatPhysicsBody body))
                    continue;

                DrawCapsule(
                    ToVector3(collider.GetWorldPointA(body.Position)),
                    ToVector3(collider.GetWorldPointB(body.Position)),
                    ToFloat(collider.Radius),
                    aabbColor);
            }

            var obbs = buffers.Obbs;
            obbs.Clear();
            runtime.World.CopyObbCollidersTo(obbs);
            for (int i = 0; i < obbs.Count; i++)
            {
                CombatPhysicsObbCollider collider = obbs[i];
                if (!runtime.World.TryGetBody(collider.BodyId, out CombatPhysicsBody body))
                    continue;

                DrawObb(
                    ToVector3(collider.GetWorldCenter(body.Position)),
                    ToVector3(collider.HalfExtents),
                    ToVector3(collider.AxisX),
                    ToVector3(collider.AxisY),
                    ToVector3(collider.AxisZ),
                    aabbColor);
            }

            if (entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !entity.PhysicsEnabled)
                    continue;

                Vector3 halfExtents = ToVector3(runtime.GetCharacterHalfExtents(entity));
                Vector3 center = new Vector3(
                    entity.Transform.X,
                    entity.Transform.Y + ToFloat(runtime.GetCharacterCenterOffsetY(entity)),
                    entity.Transform.Z);
                DrawCapsule(center, halfExtents, characterColor);
            }
        }

        public static void DrawAabbMarker(CharacterTestCombatPhysicsAabbMarker marker, Color color)
        {
            if (marker == null || !marker.IsEnabled)
                return;

            Vector3 halfExtents = marker.HalfExtents;
            DrawAabb(marker.transform.position - halfExtents, marker.transform.position + halfExtents, color);
        }

        public static void DrawSphereMarker(CharacterTestCombatPhysicsSphereMarker marker, Color color)
        {
            if (marker == null || !marker.IsEnabled)
                return;

            DrawSphere(marker.transform.position, marker.Radius, color);
        }

        public static void DrawCapsuleMarker(CharacterTestCombatPhysicsCapsuleMarker marker, Color color)
        {
            if (marker == null || !marker.IsEnabled)
                return;

            DrawCapsule(marker.WorldPointA, marker.WorldPointB, marker.Radius, color);
        }

        public static void DrawStaticColliderSpec(
            CharacterTestWorldObjectStaticColliderSpec spec,
            Color color)
        {
            if (spec.Transform == null)
                return;

            if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere)
            {
                DrawSphere(spec.Position, spec.SphereRadius, color);
                return;
            }

            if (spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule)
            {
                DrawCapsule(spec.CapsuleWorldPointA, spec.CapsuleWorldPointB, spec.CapsuleRadius, color);
                return;
            }

            DrawAabb(spec.Position - spec.HalfExtents, spec.Position + spec.HalfExtents, color);
        }

        public static void DrawBodyPartContactDebugSnapshot(
            CharacterTestBodyPartContactDebugSnapshot snapshot,
            Color inactiveColor,
            Color missColor,
            Color hitColor,
            Color filteredColor,
            Color audioQueuedColor)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                DrawBodyPartContactDebugEntry(
                    snapshot[i],
                    inactiveColor,
                    missColor,
                    hitColor,
                    filteredColor,
                    audioQueuedColor);
            }
        }

        private static void DrawBodyPartContactDebugEntry(
            CharacterTestBodyPartContactDebugEntry entry,
            Color inactiveColor,
            Color missColor,
            Color hitColor,
            Color filteredColor,
            Color audioQueuedColor)
        {
            Color color = ResolveBodyPartContactDebugColor(
                entry,
                inactiveColor,
                missColor,
                hitColor,
                filteredColor,
                audioQueuedColor);
            Vector3 center = ToVector3(entry.Center);
            float radius = Mathf.Max(0.01f, ToFloat(entry.Radius));

            Color previous = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
            DrawBodyPartContactCenterCross(center, radius);
            DrawBodyPartContactMotionMarker(entry, center, radius);

            if (entry.HasHit)
            {
                Vector3 point = ToVector3(entry.HitPoint);
                Vector3 normal = ToVector3(entry.HitNormal);
                if (normal.sqrMagnitude <= 0.0001f)
                    normal = Vector3.up;
                else
                    normal.Normalize();

                Gizmos.DrawLine(center, point);
                Gizmos.DrawSphere(point, Mathf.Max(0.025f, radius * 0.22f));
                Gizmos.DrawLine(point, point + normal * Mathf.Max(0.18f, radius * 2.2f));
            }

            Gizmos.color = previous;
        }

        private static void DrawBodyPartContactMotionMarker(
            CharacterTestBodyPartContactDebugEntry entry,
            Vector3 center,
            float radius)
        {
            if (entry.MotionMode == CharacterBodyPartContactMotionMode.Static)
                return;

            float markerSize = Mathf.Max(0.035f, radius * 0.35f);
            if (entry.MotionState == CharacterTestBodyPartContactMotionState.PresentationPose)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, center + Vector3.up * Mathf.Max(0.1f, radius));
                Gizmos.DrawSphere(center + Vector3.up * Mathf.Max(0.1f, radius), markerSize * 0.5f);
                return;
            }

            if (entry.MotionState == CharacterTestBodyPartContactMotionState.BakedSocket)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center, center + Vector3.up * Mathf.Max(0.08f, radius * 0.8f));
                return;
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(center, Vector3.one * markerSize);
        }

        private static Color ResolveBodyPartContactDebugColor(
            CharacterTestBodyPartContactDebugEntry entry,
            Color inactiveColor,
            Color missColor,
            Color hitColor,
            Color filteredColor,
            Color audioQueuedColor)
        {
            if (!entry.Active)
                return inactiveColor;

            if (entry.HasHit && !entry.ContactAllowed)
                return filteredColor;

            if (entry.AudioQueued)
                return audioQueuedColor;

            return entry.HasHit ? hitColor : missColor;
        }

        private static void DrawBodyPartContactCenterCross(Vector3 center, float radius)
        {
            float halfSize = Mathf.Max(0.025f, radius * 0.28f);
            Gizmos.DrawLine(center - Vector3.right * halfSize, center + Vector3.right * halfSize);
            Gizmos.DrawLine(center - Vector3.up * halfSize, center + Vector3.up * halfSize);
            Gizmos.DrawLine(center - Vector3.forward * halfSize, center + Vector3.forward * halfSize);
        }

        private static void DrawAabb(Vector3 min, Vector3 max, Color color)
        {
            Color previous = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
            Gizmos.color = previous;
        }

        private static void DrawObb(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ,
            Color color)
        {
            Vector3 x = axisX * halfExtents.x;
            Vector3 y = axisY * halfExtents.y;
            Vector3 z = axisZ * halfExtents.z;
            System.Span<Vector3> corners = stackalloc Vector3[8]
            {
                center - x - y - z,
                center + x - y - z,
                center + x + y - z,
                center - x + y - z,
                center - x - y + z,
                center + x - y + z,
                center + x + y + z,
                center - x + y + z,
            };

            Color previous = Gizmos.color;
            Gizmos.color = color;
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) & 3;
                Gizmos.DrawLine(corners[i], corners[next]);
                Gizmos.DrawLine(corners[i + 4], corners[next + 4]);
                Gizmos.DrawLine(corners[i], corners[i + 4]);
            }
            Gizmos.color = previous;
        }

        private static void DrawCapsule(Vector3 center, Vector3 halfExtents, Color color)
        {
            float radius = Mathf.Max(0.01f, Mathf.Min(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.z)));
            float halfHeight = Mathf.Max(radius, Mathf.Abs(halfExtents.y));
            float segmentHalfLength = Mathf.Max(0f, halfHeight - radius);
            Vector3 bottom = center + Vector3.down * segmentHalfLength;
            Vector3 top = center + Vector3.up * segmentHalfLength;

            DrawCapsule(bottom, top, radius, color);
        }

        private static void DrawSphere(Vector3 center, float radius, Color color)
        {
            Color previous = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, radius));
            Gizmos.color = previous;
        }

        private static void DrawCapsule(Vector3 bottom, Vector3 top, float radius, Color color)
        {
            radius = Mathf.Max(0.01f, radius);
            Vector3 axis = top - bottom;
            Vector3 right = Vector3.right;
            Vector3 forward = Vector3.forward;
            if (axis.sqrMagnitude > 0.0001f)
            {
                Vector3 normalizedAxis = axis.normalized;
                right = Vector3.Cross(normalizedAxis, Vector3.forward);
                if (right.sqrMagnitude < 0.0001f)
                    right = Vector3.Cross(normalizedAxis, Vector3.right);

                right.Normalize();
                forward = Vector3.Cross(right, normalizedAxis).normalized;
            }

            Color previous = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawLine(bottom + right * radius, top + right * radius);
            Gizmos.DrawLine(bottom - right * radius, top - right * radius);
            Gizmos.DrawLine(bottom + forward * radius, top + forward * radius);
            Gizmos.DrawLine(bottom - forward * radius, top - forward * radius);
            Gizmos.color = previous;
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }
    }
}
