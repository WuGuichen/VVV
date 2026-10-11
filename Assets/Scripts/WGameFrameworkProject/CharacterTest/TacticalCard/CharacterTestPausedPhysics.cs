using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Narrow candidate capability: one immutable default-material floor, with character-derived colliders.
    // No physics save provider is registered. All values come from the actual deterministic world.
    internal static class CharacterTestPausedPhysics
    {
        internal const string HeadlessCapability = "headless-mixed.two-actors.ready-paused.v3";
        internal const string FloorCapability = "fixed-floor-mixed.two-actors.ready-paused.v3";

        private static JArray Vector(FixVector3 value) => new JArray(value.X.RawValue, value.Y.RawValue, value.Z.RawValue);
        private static JObject Body(CombatPhysicsBody value) => new JObject {
            ["body"] = value.BodyId.Value, ["entity"] = value.EntityId.Value, ["position"] = Vector(value.Position) };
        private static JObject Material(CombatSurfaceMaterial value) => new JObject {
            ["kind"] = (int)value.Kind, ["friction"] = value.Friction.RawValue,
            ["elasticity"] = value.Elasticity.RawValue, ["bounceHeight"] = value.FixedBounceHeight.RawValue };
        private static JObject Shape(int body, int collider, int layer, int priority, CombatSurfaceMaterial material, string kind)
            => new JObject { ["body"] = body, ["collider"] = collider, ["layer"] = layer, ["priority"] = priority,
                ["material"] = Material(material), ["kind"] = kind };

        private static JArray Shapes(CombatPhysicsWorld world)
        {
            var values = new List<JObject>();
            var aabbs = new List<CombatPhysicsAabbCollider>(); world.CopyAabbCollidersTo(aabbs);
            foreach (var value in aabbs) {
                var shape = Shape(value.BodyId.Value, value.ColliderId.Value, value.Layer, value.Priority, value.SurfaceMaterial, "aabb");
                shape["min"] = Vector(value.LocalMin); shape["max"] = Vector(value.LocalMax); values.Add(shape);
            }
            var spheres = new List<CombatPhysicsSphereCollider>(); world.CopySphereCollidersTo(spheres);
            foreach (var value in spheres) {
                var shape = Shape(value.BodyId.Value, value.ColliderId.Value, value.Layer, value.Priority, value.SurfaceMaterial, "sphere");
                shape["center"] = Vector(value.LocalCenter); shape["radius"] = value.Radius.RawValue; values.Add(shape);
            }
            var capsules = new List<CombatPhysicsCapsuleCollider>(); world.CopyCapsuleCollidersTo(capsules);
            foreach (var value in capsules) {
                var shape = Shape(value.BodyId.Value, value.ColliderId.Value, value.Layer, value.Priority, value.SurfaceMaterial, "capsule");
                shape["a"] = Vector(value.LocalPointA); shape["b"] = Vector(value.LocalPointB); shape["radius"] = value.Radius.RawValue; values.Add(shape);
            }
            var obbs = new List<CombatPhysicsObbCollider>(); world.CopyObbCollidersTo(obbs);
            foreach (var value in obbs) {
                var shape = Shape(value.BodyId.Value, value.ColliderId.Value, value.Layer, value.Priority, value.SurfaceMaterial, "obb");
                shape["center"] = Vector(value.LocalCenter); shape["half"] = Vector(value.HalfExtents);
                shape["x"] = Vector(value.AxisX); shape["y"] = Vector(value.AxisY); shape["z"] = Vector(value.AxisZ); values.Add(shape);
            }
            SessionSnapshotCheck.Require(values.Count == world.TotalColliderCount &&
                values.Select(v => (int)v["body"] + ":" + (int)v["collider"]).Distinct().Count() == values.Count,
                "actual-physics-collider-coverage");
            return new JArray(values.OrderBy(v => (int)v["body"]).ThenBy(v => (int)v["collider"]).Select(v => (JToken)v));
        }

        internal static string CaptureStatic(CharacterTestCharacterPhysicsRuntime physics)
        {
            if (physics == null) return null;
            SessionSnapshotCheck.Require(physics.IsReady && physics.World != null && physics.StaticColliderCount == 1 &&
                physics.World.BroadphaseConfig.Equals(CombatPhysicsBroadphaseConfig.Default) && !physics.NavigationBuildInput.HasValue,
                "actual-physics-fixed-floor-required");
            var bodies = new List<CombatPhysicsBody>(); physics.World.CopyBodiesTo(bodies);
            var statics = bodies.Where(v => v.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart).ToArray();
            var shapes = Shapes(physics.World);
            var staticShapes = shapes.Children<JObject>().Where(v => (int)v["body"] < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart).ToArray();
            SessionSnapshotCheck.Require(statics.Length == 1 && statics[0].BodyId.Value == CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart &&
                statics[0].EntityId.Value == statics[0].BodyId.Value && statics[0].Position.Equals(FixVector3.Zero) &&
                staticShapes.Length == 1 && (string)staticShapes[0]["kind"] == "aabb" &&
                (int)staticShapes[0]["body"] == statics[0].BodyId.Value && (int)staticShapes[0]["collider"] == 1 &&
                (int)staticShapes[0]["layer"] == 1 && (int)staticShapes[0]["priority"] == 0 &&
                JToken.DeepEquals(staticShapes[0]["material"], Material(CombatSurfaceMaterial.Default)) &&
                physics.ObstacleLayerMask.Equals(CombatPhysicsLayerMask.FromLayer(1)), "actual-physics-static-shape-required");
            // The permitted floor has its top at zero. Geometry is frozen by the full raw-value signature below.
            var min = (JArray)staticShapes[0]["min"]; var max = (JArray)staticShapes[0]["max"];
            SessionSnapshotCheck.Require((long)min[0] < 0 && (long)min[1] < 0 && (long)min[2] < 0 &&
                (long)max[0] > 0 && (long)max[1] == 0 && (long)max[2] > 0, "actual-physics-floor-geometry");
            return new JObject {
                ["capability"] = FloorCapability, ["staticBody"] = Body(statics[0]), ["staticCollider"] = staticShapes[0],
                ["mask"] = physics.ObstacleLayerMask.Value, ["half"] = Vector(physics.CharacterHalfExtents),
                ["skin"] = physics.SkinWidth.RawValue, ["step"] = physics.MaxStepHeight.RawValue,
                ["snap"] = physics.GroundSnapDistance.RawValue, ["cell"] = physics.World.BroadphaseConfig.CellSize.RawValue
            }.ToString(Formatting.None);
        }

        internal static void ValidateFresh(CharacterTestCharacterPhysicsRuntime fresh, CharacterTestCharacterPhysicsRuntime previous, string expected)
        {
            SessionSnapshotCheck.Require(expected == null ? fresh == null : fresh != null && !ReferenceEquals(fresh, previous) &&
                !ReferenceEquals(fresh.World, previous?.World), "actual-fresh-physics-alias");
            SessionSnapshotCheck.Require(CaptureStatic(fresh) == expected, "actual-fresh-physics-static-input");
            if (fresh == null) return;
            SessionSnapshotCheck.Require(fresh.World.BodyCount == 1 && fresh.World.TotalColliderCount == 1 &&
                fresh.World.CompoundCount == 0, "actual-fresh-physics-dynamic-state");
        }

        internal static string CaptureWorld(GameSlice slice)
        {
            var physics = slice.PhysicsRuntime;
            if (physics == null) return null;
            CaptureStatic(physics);
            var bodies = new List<CombatPhysicsBody>(); physics.World.CopyBodiesTo(bodies);
            var expected = new Dictionary<int, int> { [CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart] = CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart };
            foreach (var actor in slice.CharacterEntities) {
                expected.Add(physics.GetBodyId(actor).Value, physics.GetEntityId(actor).Value);
                expected.Add(physics.GetHurtboxBodyId(actor).Value, physics.GetEntityId(actor).Value);
                SessionSnapshotCheck.Require(physics.TryGetBodyPresenceBodyId(actor.Id.Value, out var presence), "actual-physics-body-presence-id");
                expected.Add(presence.Value, physics.GetEntityId(actor).Value);
                SessionSnapshotCheck.Require(physics.World.TryGetBody(physics.GetBodyId(actor), out _), "actual-physics-character-body-required");
            }
            SessionSnapshotCheck.Require(bodies.All(v => expected.TryGetValue(v.BodyId.Value, out int entity) && entity == v.EntityId.Value),
                "actual-physics-unknown-body-owner");
            var shapes = Shapes(physics.World);
            SessionSnapshotCheck.Require(shapes.Children<JObject>().All(v => expected.ContainsKey((int)v["body"])), "actual-physics-unknown-collider-owner");
            return new JObject { ["bodies"] = new JArray(bodies.Select(v => (JToken)Body(v))), ["shapes"] = shapes,
                ["compoundCount"] = physics.World.CompoundCount, ["compoundLeaves"] = physics.World.CompoundLeafCount }.ToString(Formatting.None);
        }
    }
}
