using MxFramework.CharacterApplication;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        private readonly CharacterTestShallowWaterSplashPolicy _splashPolicy = new CharacterTestShallowWaterSplashPolicy();
        private CharacterTestShallowWaterSplashPool _splashPool;
        private GameObject _splashPrefab;
        private int _splashCapacity;
        internal string SplashIssue { get; private set; } = string.Empty;
        internal int ContactSplashes { get; private set; }
        internal int LandingSplashes { get; private set; }
        internal int ActiveSplashBursts => _splashPool?.ActiveCount ?? 0;
        internal int ActiveSplashParticles => _splashPool?.ParticleCount ?? 0;
        internal int SplashRejected => _splashPolicy.Rejected;
        internal CharacterTestSplashRejection SplashLastRejection => _splashPolicy.LastRejection;
        internal bool SplashResourceLoaded => _splashPool?.Ready == true;
        internal int SplashPoolCapacity => _splashPool?.Capacity ?? 0;
        internal int SplashReuses => _splashPool?.ReusedBursts ?? 0;
        internal Vector3 LastSplashContact { get; private set; }
        internal Vector3 LastSplashOrigin { get; private set; }

        private void TickSplashes()
        {
            if (!Application.isPlaying || !IsActive || !SurfaceEnabled || !Configuration.SplashesEnabled || Settings == null)
            { ReleaseSplashes(); return; }
            // OnEnable can precede Scene.isLoaded; defer acquisition until the owner's scene is ready.
            if (!Owner.gameObject.scene.IsValid() || !Owner.gameObject.scene.isLoaded) return;
            int capacity = Mathf.Clamp(Settings.SplashMaximumActive, 1, CharacterTestShallowWaterSplashPolicy.HardMaximumBursts);
            if (_splashPool == null || _splashPrefab != Settings.SplashPrefab || _splashCapacity != capacity)
            {
                ReleaseSplashes(); _splashPrefab = Settings.SplashPrefab; _splashCapacity = capacity;
                _splashPool = new CharacterTestShallowWaterSplashPool(_splashPrefab, Owner.gameObject.scene, capacity);
                SplashIssue = _splashPool.Issue;
            }
            _splashPool.Tick(Clock);
            SplashIssue = _splashPool.Issue;
        }
        private void ClearSplashParticles()
        { _splashPool?.Clear(); _splashPolicy.Reset(); }
        private void ReleaseSplashes()
        {
            _splashPool?.Dispose(); _splashPool = null; _splashPrefab = null; _splashCapacity = 0;
            _splashPolicy.Reset(); SplashIssue = string.Empty;
        }
        private void ObserveSplashMotion(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterMotionSystem motion, RuntimeFrame frame)
        {
            if (!Application.isPlaying || !Configuration.SplashesEnabled || !SurfaceEnabled || Settings == null) return;
            if (_splashPolicy.BeginMotion(frame.Value, Clock)) _splashPool?.Clear();
            RuntimeFrame completedFrame = default;
            bool hasCompletedFrame = _runtime != null && _runtime.TryGetLastCompletedRuntimeFrame(out completedFrame);
            for (int i = 0; i < world.Entities.Count; i++)
            {
                var entity = world.Entities[i]; var t = entity.Transform;
                float vx = entity.HorizontalVelocityXMillimetersPerSecond * .001f;
                float vz = entity.HorizontalVelocityYMillimetersPerSecond * .001f;
                bool recalled = entity.PositionRecall.Result == CharacterTestPositionRecallResult.Returned
                    && hasCompletedFrame && entity.PositionRecall.CompletedTick == completedFrame.Value;
                _splashPolicy.ObserveSubject(entity.Id.Value, frame.Value, Clock,
                    t.XMillimeters * .001f, t.YMillimeters * .001f, t.ZMillimeters * .001f, entity.IsGrounded,
                    Mathf.Sqrt(vx * vx + vz * vz), _runtime?.SimulationTickRate ?? 60, recalled);
            }
            _splashPolicy.EndMotion(frame.Value);
            for (int i = 0; i < motion.LandingEventCount; i++)
            {
                var landing = motion.GetLandingEvent(i);
                _splashPolicy.ObserveLanding(landing.Entity.Id.Value, frame.Value,
                    landing.DownwardSpeedMillimetersPerSecond * .001f);
            }
        }
        private void EmitContactSplash(in CombatPhysicalContactEvent contact, Vector3 contactPoint)
        {
            if (!Application.isPlaying || !IsActive || !SurfaceEnabled || !Configuration.SplashesEnabled
                || Settings == null || _splashPool?.Ready != true || !string.IsNullOrEmpty(Issue) || _runtime == null) return;
            if (!_runtime.TryResolvePhysicalContactPresentation(contact, out var entity, out var kind)) return;
            int id = entity.Id.Value;
            bool isFoot = kind == CharacterBodyPartKind.Foot;
            var local = LocalToWorld.inverse.MultiplyPoint3x4(contactPoint);
            var t = entity.Transform;
            float vx = entity.HorizontalVelocityXMillimetersPerSecond * .001f;
            float vz = entity.HorizontalVelocityYMillimetersPerSecond * .001f;
            var input = new CharacterTestShallowWaterSplashPolicy.Contact {
                EntityId = id, ContactPointId = contact.ContactPointId, BodyPartId = contact.BodyPartId,
                Frame = contact.Frame.Value, Sequence = contact.Sequence, IsFoot = isFoot,
                IsSubmerged = CharacterTestShallowWaterSplashPolicy.IsSubmerged(local.x, local.y, local.z,
                    Settings.Size.x, Settings.Size.y, Settings.SplashMinimumDepth, Settings.MaximumWaterDepth),
                IsVisible = HasSplashReceiver(contactPoint), RootX = t.XMillimeters * .001f,
                RootY = t.YMillimeters * .001f, RootZ = t.ZMillimeters * .001f,
                HorizontalSpeed = Mathf.Sqrt(vx * vx + vz * vz), NormalSpeed = contact.NormalSpeedMillimetersPerSecond * .001f
            };
            var budget = new CharacterTestShallowWaterSplashPolicy.Budget(Settings.SplashMaximumActive,
                Settings.SplashBurstsPerSecond, Settings.SplashCooldown, Settings.SplashMinimumSpeed, Settings.SplashLandingMultiplier);
            if (!_splashPolicy.TryAdmit(input, Clock, _splashPool.ActiveCount, budget, out bool landing, out float strength)) return;
            // The floor contact is below water. Keep its real X/Z and emit from the known liquid plane, without another query.
            var origin = contactPoint; origin.y = Position.y + .003f;
            if (!_splashPool.Emit(origin, strength, Settings.SplashScale, Settings.SplashStepParticles, Clock)) return;
            LastSplashContact = contactPoint; LastSplashOrigin = origin;
            if (landing) LandingSplashes++; else ContactSplashes++;
        }
        private bool HasSplashReceiver(Vector3 point)
        {
            float distance = Mathf.Clamp(Settings.SplashDistance, 5, 60);
            if (SplashReceiverContains(_source, point, distance * distance)) return true;
            foreach (var preview in _scenePreviews.Values)
                if (SplashReceiverContains(preview.Source, point, distance * distance)) return true;
            return false;
        }
        private static bool SplashReceiverContains(UnityEngine.Camera camera, Vector3 point, float distanceSquared)
        {
            if (camera == null || (!camera.isActiveAndEnabled && camera.cameraType != CameraType.SceneView)
                || (camera.transform.position - point).sqrMagnitude > distanceSquared) return false;
            var viewport = camera.WorldToViewportPoint(point + Vector3.up * .15f);
            return viewport.z > 0 && viewport.x > -.1f && viewport.x < 1.1f && viewport.y > -.1f && viewport.y < 1.1f;
        }
    }
}
