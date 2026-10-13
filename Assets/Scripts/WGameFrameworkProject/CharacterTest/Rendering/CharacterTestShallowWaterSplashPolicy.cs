using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestSplashRejection
    {
        None, Invalid, NotFoot, NotSubmerged, OutOfView, NoMotionBaseline, Teleport,
        Stationary, Duplicate, Cooldown, Budget, PoolFull
    }

    /// <summary>Presentation-only admission. No authority, physics query, Unity random or serialized state.</summary>
    internal sealed class CharacterTestShallowWaterSplashPolicy
    {
        internal const int MaximumSubjects = 128;
        internal const int MaximumContacts = 256;
        internal const int HardMaximumBursts = 16;
        internal const int HardMaximumParticlesPerBurst = 24;
        internal const int HardMaximumBurstsPerFrame = 4;
        private readonly Subject[] _subjects = new Subject[MaximumSubjects];
        private readonly Foot[] _feet = new Foot[MaximumContacts];
        private long _observedFrame = -1, _budgetFrame = -1;
        private double _lastClock = -1, _windowStart;
        private int _frameBursts, _windowBursts;
        internal int Accepted { get; private set; }
        internal int Rejected { get; private set; }
        internal CharacterTestSplashRejection LastRejection { get; private set; }

        internal struct Contact
        {
            internal int EntityId, ContactPointId, BodyPartId, Sequence;
            internal long Frame;
            internal bool IsFoot, IsSubmerged, IsVisible;
            internal float RootX, RootY, RootZ, HorizontalSpeed, NormalSpeed;
        }

        internal readonly struct Budget
        {
            internal readonly int MaximumActive, BurstsPerSecond;
            internal readonly float Cooldown, MinimumSpeed, LandingMultiplier;
            internal Budget(int maximumActive, int burstsPerSecond, float cooldown, float minimumSpeed, float landingMultiplier)
            {
                MaximumActive = Math.Max(1, Math.Min(HardMaximumBursts, maximumActive));
                BurstsPerSecond = Math.Max(1, Math.Min(32, burstsPerSecond));
                Cooldown = Math.Max(.08f, Math.Min(1, cooldown));
                MinimumSpeed = Math.Max(.1f, Math.Min(2, minimumSpeed));
                LandingMultiplier = Math.Max(1, Math.Min(2, landingMultiplier));
            }
        }

        private struct Subject
        {
            internal int Id;
            internal long Frame, LandingFrame, LandedBurstFrame;
            internal double SuppressUntil;
            internal float X, Y, Z, LandingSpeed, HorizontalSpeed;
            internal bool Grounded, HasSecondObservation;
        }
        private struct Foot
        {
            internal int EntityId, PointId, PartId, Sequence;
            internal long Frame;
            internal double LastEmission;
        }

        /// <remarks>NoAlloc. Rectangular horizontal patch; depth is positive below the surface.</remarks>
        internal static bool IsSubmerged(float localX, float localY, float localZ, float width, float length,
            float minimumDepth, float maximumDepth)
        {
            if (!Finite(localX) || !Finite(localY) || !Finite(localZ) || !Finite(width) || !Finite(length)
                || !Finite(minimumDepth) || !Finite(maximumDepth) || width <= 0 || length <= 0) return false;
            float depth = -localY;
            return Math.Abs(localX) <= width * .5f && Math.Abs(localZ) <= length * .5f
                && depth >= Math.Max(.001f, minimumDepth) && depth <= maximumDepth;
        }

        /// <remarks>NoAlloc. Receives the existing motion observation; never samples collisions.</remarks>
        internal bool BeginMotion(long frame, double clock)
        {
            if (frame < 0 || double.IsNaN(clock) || double.IsInfinity(clock)) return false;
            bool restored = frame < _observedFrame || clock < _lastClock;
            if (restored) Reset();
            _observedFrame = frame; _lastClock = clock;
            return restored;
        }

        internal void ObserveSubject(int entityId, long frame, double clock, float x, float y, float z, bool grounded,
            float horizontalSpeed = 0, int tickRate = 60, bool explicitTeleport = false)
        {
            if (entityId <= 0 || frame < 0 || !Finite(x) || !Finite(y) || !Finite(z)) return;
            int index = FindSubject(entityId), free = -1;
            if (index < 0)
            {
                for (int i = 0; i < _subjects.Length; i++)
                    if (_subjects[i].Id == 0 || frame - _subjects[i].Frame > 120) { free = i; break; }
                if (free < 0) return;
                _subjects[free] = new Subject { Id = entityId, Frame = frame,
                    SuppressUntil = clock + .1, X = x, Y = y, Z = z, Grounded = grounded,
                    HorizontalSpeed = Math.Abs(horizontalSpeed),
                    LandingFrame = -1, LandedBurstFrame = -1 };
                return;
            }
            ref Subject subject = ref _subjects[index];
            if (frame <= subject.Frame) return;
            float dx = x - subject.X, dy = y - subject.Y, dz = z - subject.Z;
            float allowedTravel = .2f + Math.Max(Math.Abs(horizontalSpeed), subject.HorizontalSpeed)
                * Math.Max(1, frame - subject.Frame) / Math.Max(1, tickRate);
            if (explicitTeleport || dx * dx + dz * dz > allowedTravel * allowedTravel || dy * dy > 4)
            {
                subject.SuppressUntil = clock + .18;
                subject.HasSecondObservation = false;
                subject.LandingFrame = -1;
                ClearFeet(entityId);
            }
            else subject.HasSecondObservation = true;
            subject.Frame = frame; subject.X = x; subject.Y = y; subject.Z = z; subject.Grounded = grounded;
            subject.HorizontalSpeed = Math.Abs(horizontalSpeed);
        }

        internal void ObserveLanding(int entityId, long frame, float downwardSpeed)
        {
            int index = FindSubject(entityId);
            if (index < 0 || _subjects[index].Frame != frame || !Finite(downwardSpeed) || downwardSpeed <= 0) return;
            _subjects[index].LandingFrame = frame;
            _subjects[index].LandingSpeed = downwardSpeed;
        }

        internal void EndMotion(long frame)
        {
            for (int i = 0; i < _subjects.Length; i++)
            {
                if (_subjects[i].Id == 0 || _subjects[i].Frame == frame) continue;
                ClearFeet(_subjects[i].Id); _subjects[i] = default;
            }
        }

        /// <remarks>NoAlloc. Rejects stale/duplicate/cold/teleported contacts before consuming a bounded emission slot.</remarks>
        internal bool TryAdmit(in Contact contact, double clock, int activeCount, in Budget budget,
            out bool landing, out float strength)
        {
            landing = false; strength = 0;
            if (contact.EntityId <= 0 || contact.ContactPointId <= 0 || contact.BodyPartId <= 0 || contact.Frame < 0
                || !Finite(contact.RootX) || !Finite(contact.RootY) || !Finite(contact.RootZ)
                || !Finite(contact.HorizontalSpeed) || !Finite(contact.NormalSpeed)
                || double.IsNaN(clock) || double.IsInfinity(clock)) return Reject(CharacterTestSplashRejection.Invalid);
            if (!contact.IsFoot) return Reject(CharacterTestSplashRejection.NotFoot);
            if (!contact.IsSubmerged) return Reject(CharacterTestSplashRejection.NotSubmerged);
            if (!contact.IsVisible) return Reject(CharacterTestSplashRejection.OutOfView);
            int index = FindSubject(contact.EntityId);
            if (index < 0) return Reject(CharacterTestSplashRejection.NoMotionBaseline);
            ref Subject subject = ref _subjects[index];
            if (subject.Frame != contact.Frame || !subject.HasSecondObservation || !subject.Grounded)
                return Reject(CharacterTestSplashRejection.NoMotionBaseline);
            float dx = contact.RootX - subject.X, dy = contact.RootY - subject.Y, dz = contact.RootZ - subject.Z;
            if (clock < subject.SuppressUntil || dx * dx + dy * dy + dz * dz > .25f)
                return Reject(CharacterTestSplashRejection.Teleport);
            landing = subject.LandingFrame == contact.Frame && subject.LandingSpeed >= .7f;
            if (!landing && Math.Abs(contact.HorizontalSpeed) < budget.MinimumSpeed
                && Math.Abs(contact.NormalSpeed) < .7f) return Reject(CharacterTestSplashRejection.Stationary);
            if (landing && subject.LandedBurstFrame == contact.Frame) return Reject(CharacterTestSplashRejection.Duplicate);
            int foot = -1, free = -1;
            for (int i = 0; i < _feet.Length; i++)
            {
                if (_feet[i].EntityId == contact.EntityId && _feet[i].PointId == contact.ContactPointId
                    && _feet[i].PartId == contact.BodyPartId) { foot = i; break; }
                if (free < 0 && (_feet[i].EntityId == 0 || clock - _feet[i].LastEmission > 2)) free = i;
            }
            if (foot >= 0)
            {
                if (contact.Frame <= _feet[foot].Frame) return Reject(CharacterTestSplashRejection.Duplicate);
                if (clock - _feet[foot].LastEmission < budget.Cooldown) return Reject(CharacterTestSplashRejection.Cooldown);
            }
            else if (free < 0) return Reject(CharacterTestSplashRejection.Budget);
            if (activeCount >= budget.MaximumActive) return Reject(CharacterTestSplashRejection.PoolFull);
            if (clock < _windowStart || clock - _windowStart >= 1) { _windowStart = clock; _windowBursts = 0; }
            if (_budgetFrame != contact.Frame) { _budgetFrame = contact.Frame; _frameBursts = 0; }
            if (_frameBursts >= HardMaximumBurstsPerFrame || _windowBursts >= budget.BurstsPerSecond)
                return Reject(CharacterTestSplashRejection.Budget);
            foot = foot >= 0 ? foot : free;
            _feet[foot] = new Foot { EntityId = contact.EntityId, PointId = contact.ContactPointId, PartId = contact.BodyPartId,
                Frame = contact.Frame, Sequence = contact.Sequence, LastEmission = clock };
            if (landing) subject.LandedBurstFrame = contact.Frame;
            strength = landing ? budget.LandingMultiplier * Math.Max(1, Math.Min(1.35f, subject.LandingSpeed / 3))
                : Math.Max(.6f, Math.Min(1.35f, Math.Abs(contact.HorizontalSpeed) / 1.6f));
            _frameBursts++; _windowBursts++; Accepted++; LastRejection = CharacterTestSplashRejection.None;
            return true;
        }

        internal void Reset()
        {
            Array.Clear(_subjects, 0, _subjects.Length); Array.Clear(_feet, 0, _feet.Length);
            _observedFrame = _budgetFrame = -1; _lastClock = -1; _windowStart = 0;
            _frameBursts = _windowBursts = 0; LastRejection = CharacterTestSplashRejection.None;
        }
        private void ClearFeet(int entityId)
        { for (int i = 0; i < _feet.Length; i++) if (_feet[i].EntityId == entityId) _feet[i] = default; }
        private int FindSubject(int id)
        { for (int i = 0; i < _subjects.Length; i++) if (_subjects[i].Id == id) return i; return -1; }
        private bool Reject(CharacterTestSplashRejection reason)
        { LastRejection = reason; Rejected++; return false; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
