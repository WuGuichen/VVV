using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Demo
{
    internal sealed class RuntimeCombatShowcaseMotionAdapter
    {
        private static readonly CombatEntityId GroundEntityId = new CombatEntityId(100);
        private static readonly CombatBodyId GroundBodyId = new CombatBodyId(100);
        private static readonly CombatEntityId WallEntityId = new CombatEntityId(101);
        private static readonly CombatBodyId WallBodyId = new CombatBodyId(101);
        private static readonly CombatEntityId CeilingEntityId = new CombatEntityId(102);
        private static readonly CombatBodyId CeilingBodyId = new CombatBodyId(102);
        private static readonly CombatColliderId ObstacleColliderId = new CombatColliderId(1);

        private readonly CombatKinematicMotor _motor;
        private readonly int _obstacleLayer;
        private CombatMotionState _state;
        private CombatMotionStepResult _lastStep;
        private Transform _visualRoot;
        private Material _groundMaterial;
        private Material _wallMaterial;
        private Material _ceilingMaterial;

        public RuntimeCombatShowcaseMotionAdapter(int obstacleLayer)
        {
            _obstacleLayer = obstacleLayer;
            Config = new CombatMotionConfig(
                CombatStepConfig.Default,
                new FixVector3(Fix64.FromRatio(45, 100), Fix64.FromRatio(9, 10), Fix64.FromRatio(45, 100)),
                moveSpeed: Fix64.FromRatio(9, 2),
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.FromInt(10),
                maxFallSpeed: Fix64.FromInt(45),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: CombatPhysicsLayerMask.FromLayer(obstacleLayer).Value,
                maxSlideIterations: 3);
            _motor = new CombatKinematicMotor(Config);
            Reset(FixVector3.Zero);
        }

        public CombatMotionConfig Config { get; }

        public bool IsInitialized { get; private set; }

        public CombatMotionState State => _state;

        public CombatMotionStepResult LastStep => _lastStep;

        public void Reset(FixVector3 startPosition)
        {
            FixVector3 normalized = NormalizeStartPosition(startPosition);
            _state = new CombatMotionState(
                CombatFrame.Zero,
                normalized,
                FixVector3.Zero,
                grounded: true,
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                CombatMotionCollisionFlags.Grounded);
            _lastStep = new CombatMotionStepResult(
                _state,
                FixVector3.Zero,
                FixVector3.Zero,
                jumpStarted: false,
                _state.CollisionFlags,
                System.Array.Empty<CombatMotionCollision>());
            IsInitialized = true;
        }

        public void Warp(CombatFrame frame, FixVector3 position)
        {
            FixVector3 normalized = NormalizeStartPosition(position);
            bool grounded = normalized.Y <= Config.CharacterHalfExtents.Y + Config.SkinWidth;
            CombatMotionCollisionFlags flags = grounded
                ? CombatMotionCollisionFlags.Grounded
                : CombatMotionCollisionFlags.None;
            _state = new CombatMotionState(
                frame,
                normalized,
                FixVector3.Zero,
                grounded,
                grounded ? new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero) : FixVector3.Zero,
                flags);
            _lastStep = new CombatMotionStepResult(
                _state,
                FixVector3.Zero,
                FixVector3.Zero,
                jumpStarted: false,
                flags,
                System.Array.Empty<CombatMotionCollision>());
        }

        public CombatMotionStepResult Step(
            CombatFrame targetFrame,
            CombatPhysicsWorld physicsWorld,
            CombatBodyId playerBodyId,
            CombatMotionInput input)
        {
            if (!IsInitialized)
                Reset(FixVector3.Zero);

            CombatFrame previousFrame = targetFrame.Value > 0
                ? new CombatFrame(targetFrame.Value - 1)
                : CombatFrame.Zero;
            _state = _state.WithFrame(previousFrame);
            _lastStep = _motor.Step(physicsWorld, playerBodyId, _state, input);
            _state = _lastStep.State;
            return _lastStep;
        }

        public void RegisterStaticObstacles(CombatPhysicsWorld world)
        {
            if (world == null)
                return;

            RegisterObstacle(
                world,
                GroundEntityId,
                GroundBodyId,
                new FixVector3(-Fix64.FromInt(12), -Fix64.One, -Fix64.FromInt(6)),
                new FixVector3(Fix64.FromInt(12), Fix64.Zero, Fix64.FromInt(6)));
            RegisterObstacle(
                world,
                WallEntityId,
                WallBodyId,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, -Fix64.FromInt(4)),
                new FixVector3(Fix64.FromRatio(9, 2), Fix64.FromInt(3), Fix64.FromInt(4)));
            RegisterObstacle(
                world,
                CeilingEntityId,
                CeilingBodyId,
                new FixVector3(-Fix64.FromRatio(5, 2), Fix64.FromInt(3), -Fix64.FromRatio(5, 2)),
                new FixVector3(Fix64.FromRatio(5, 2), Fix64.FromRatio(7, 2), Fix64.FromRatio(5, 2)));
        }

        public void EnsureObstacleVisuals(Transform parent)
        {
            if (parent == null)
                return;

            if (_visualRoot == null)
            {
                _visualRoot = parent.Find("Combat_Motion_Obstacles");
                if (_visualRoot == null)
                {
                    GameObject root = new GameObject("Combat_Motion_Obstacles");
                    root.transform.SetParent(parent, worldPositionStays: false);
                    _visualRoot = root.transform;
                }
            }

            _groundMaterial = _groundMaterial ?? CreateMaterial("CombatMotionGround", new Color(0.24f, 0.3f, 0.32f, 0.72f));
            _wallMaterial = _wallMaterial ?? CreateMaterial("CombatMotionWall", new Color(0.75f, 0.28f, 0.18f, 0.82f));
            _ceilingMaterial = _ceilingMaterial ?? CreateMaterial("CombatMotionCeiling", new Color(0.18f, 0.44f, 0.78f, 0.78f));

            ConfigureVisual("Motion_Ground", new Vector3(0f, -0.5f, 0f), new Vector3(24f, 1f, 12f), _groundMaterial);
            ConfigureVisual("Motion_Wall_X", new Vector3(4.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 8f), _wallMaterial);
            ConfigureVisual("Motion_Ceiling", new Vector3(0f, 3.25f, 0f), new Vector3(5f, 0.5f, 5f), _ceilingMaterial);
        }

        public string BuildSummary(int worldRevision, FixVector3 bodyPosition)
        {
            CombatMotionCapsuleProxy capsule = Config.CapsuleProxy;
            return $"Motion f={_state.Frame.Value} capsule(r={Format(capsule.Radius)} h={Format(capsule.Height)} skin={Format(capsule.SkinWidth)}) pos={Format(_state.Position)} vel={Format(_state.Velocity)} grounded={_state.Grounded} flags={_state.CollisionFlags} body={Format(bodyPosition)} hit={BuildCollisionSummary()} rev={worldRevision}";
        }

        public string BuildCollisionSummary()
        {
            if (_lastStep.CollisionCount == 0)
                return "Motion collision: none";

            CombatMotionCollision collision = _lastStep.LastCollision;
            return $"Motion collision: normal={Format(collision.Normal)} distance={collision.Distance} flags={collision.Flags}";
        }

        private FixVector3 NormalizeStartPosition(FixVector3 position)
        {
            Fix64 y = position.Y < Config.CharacterHalfExtents.Y
                ? Config.CharacterHalfExtents.Y
                : position.Y;
            return new FixVector3(position.X, y, position.Z);
        }

        private void RegisterObstacle(
            CombatPhysicsWorld world,
            CombatEntityId entityId,
            CombatBodyId bodyId,
            FixVector3 min,
            FixVector3 max)
        {
            world.UpsertBody(new CombatPhysicsBody(entityId, bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                ObstacleColliderId,
                _obstacleLayer,
                min,
                max));
        }

        private void ConfigureVisual(string name, Vector3 position, Vector3 scale, Material material)
        {
            Transform child = _visualRoot.Find(name);
            if (child == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                cube.transform.SetParent(_visualRoot, worldPositionStays: false);
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null)
                    DestroyComponent(collider);

                child = cube.transform;
            }

            child.localPosition = position;
            child.localRotation = Quaternion.identity;
            child.localScale = scale;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            return material;
        }

        private static void DestroyComponent(Object component)
        {
            if (Application.isPlaying)
                Object.Destroy(component);
            else
                Object.DestroyImmediate(component);
        }

        private static string Format(FixVector3 value)
        {
            return $"({Format(value.X)},{Format(value.Y)},{Format(value.Z)})";
        }

        private static string Format(Fix64 value)
        {
            return (((float)value.RawValue) / Fix64.Scale).ToString("0.00");
        }
    }
}
