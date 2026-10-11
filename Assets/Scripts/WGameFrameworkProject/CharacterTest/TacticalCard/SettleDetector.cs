using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Authority facts captured after a complete simulation tick; speeds are support-relative magnitudes.</summary>
    public readonly struct SettleSample
    {
        public readonly long SimulationTick;
        public readonly bool ActionTimelineCompleted;
        public readonly bool BehaviorTerminal;
        public readonly bool NeutralPoseReached;
        public readonly bool HasReactionOrRecovery;
        public readonly bool HasPendingLanding;
        public readonly bool HasPendingMotionOrImpulse;
        public readonly bool OwnedIntentsReleased;
        public readonly bool CommandsAcknowledged;
        public readonly bool HasStableSupport;
        public readonly bool HasNewDestabilizingContact;
        public readonly float RelativeLinearSpeed;
        public readonly float RelativeAngularSpeed;
        /// <summary>Explicit terminal fact for an admitted attempt that never started an action.</summary>
        public readonly bool NoActionStarted;
        /// <summary>Authoritative player action/movement permission, independent of physical rest.</summary>
        public readonly bool PlayerCanActFreely;

        public SettleSample(long simulationTick, bool actionTimelineCompleted, bool behaviorTerminal,
            bool neutralPoseReached, bool hasReactionOrRecovery, bool hasPendingLanding,
            bool hasPendingMotionOrImpulse, bool ownedIntentsReleased, bool commandsAcknowledged,
            bool hasStableSupport, bool hasNewDestabilizingContact, float relativeLinearSpeed,
            float relativeAngularSpeed, bool noActionStarted = false, bool playerCanActFreely = false)
        {
            SimulationTick = simulationTick;
            ActionTimelineCompleted = actionTimelineCompleted;
            BehaviorTerminal = behaviorTerminal;
            NeutralPoseReached = neutralPoseReached;
            HasReactionOrRecovery = hasReactionOrRecovery;
            HasPendingLanding = hasPendingLanding;
            HasPendingMotionOrImpulse = hasPendingMotionOrImpulse;
            OwnedIntentsReleased = ownedIntentsReleased;
            CommandsAcknowledged = commandsAcknowledged;
            HasStableSupport = hasStableSupport;
            HasNewDestabilizingContact = hasNewDestabilizingContact;
            RelativeLinearSpeed = relativeLinearSpeed;
            RelativeAngularSpeed = relativeAngularSpeed;
            NoActionStarted = noActionStarted;
            PlayerCanActFreely = playerCanActFreely;
        }
    }

    /// <summary>Immutable thresholds. Construction validates finite, nonnegative hysteresis bands.</summary>
    public sealed class SettleDetectorConfig
    {
        public int RequiredStableTicks { get; }
        public float EnterLinearSpeed { get; }
        public float ExitLinearSpeed { get; }
        public float EnterAngularSpeed { get; }
        public float ExitAngularSpeed { get; }

        public SettleDetectorConfig(int requiredStableTicks = 8, float enterLinearSpeed = 0.08f,
            float exitLinearSpeed = 0.15f, float enterAngularSpeed = 0.15f, float exitAngularSpeed = 0.30f)
        {
            if (requiredStableTicks <= 0) throw new ArgumentOutOfRangeException(nameof(requiredStableTicks));
            if (!ValidSpeed(enterLinearSpeed)) throw new ArgumentOutOfRangeException(nameof(enterLinearSpeed));
            if (!ValidSpeed(exitLinearSpeed) || exitLinearSpeed < enterLinearSpeed)
                throw new ArgumentOutOfRangeException(nameof(exitLinearSpeed));
            if (!ValidSpeed(enterAngularSpeed)) throw new ArgumentOutOfRangeException(nameof(enterAngularSpeed));
            if (!ValidSpeed(exitAngularSpeed) || exitAngularSpeed < enterAngularSpeed)
                throw new ArgumentOutOfRangeException(nameof(exitAngularSpeed));
            RequiredStableTicks = requiredStableTicks;
            EnterLinearSpeed = enterLinearSpeed;
            ExitLinearSpeed = exitLinearSpeed;
            EnterAngularSpeed = enterAngularSpeed;
            ExitAngularSpeed = exitAngularSpeed;
        }

        internal static bool ValidSpeed(float speed) => speed >= 0 && !float.IsInfinity(speed);
    }

    /// <summary>Single-threaded, allocation-free complete-tick stability detector.</summary>
    public interface ISettleDetector
    {
        /// <remarks>NoAlloc. Duplicate/older ticks cannot advance stability.</remarks>
        bool Sample(in SettleSample sample);
        /// <remarks>NoAlloc. Starts a new observation sequence.</remarks>
        void Reset();
    }

    /// <summary>Normal turns pause as soon as the behavior ends and the player can act freely.</summary>
    internal sealed class TacticalTurnPauseDetector : ISettleDetector
    {
        /// <remarks>NoAlloc. Physical support, motion and consecutive stable ticks do not gate a turn.</remarks>
        public bool Sample(in SettleSample sample) => sample.BehaviorTerminal && sample.PlayerCanActFreely;

        public void Reset() { }
    }

    /// <summary>Hysteresis starts on the first stable tick, with a saturating consecutive-tick counter.</summary>
    public sealed partial class SettleDetector : ISettleDetector
    {
        private readonly SettleDetectorConfig _config;
        private long _lastTick;
        private bool _hasTick;
        private int _stableTicks;

        public SettleDetector(SettleDetectorConfig config = null)
        {
            _config = config ?? new SettleDetectorConfig();
        }

        /// <remarks>NoAlloc. Missing ticks restart the window; duplicate/older samples fail closed.</remarks>
        public bool Sample(in SettleSample sample)
        {
            if (_hasTick && sample.SimulationTick <= _lastTick)
            {
                _stableTicks = 0;
                return false;
            }
            if (_hasTick && (_lastTick == long.MaxValue || sample.SimulationTick != _lastTick + 1))
                _stableTicks = 0;
            _hasTick = true;
            _lastTick = sample.SimulationTick;

            bool ready = (sample.ActionTimelineCompleted || sample.NoActionStarted) && sample.BehaviorTerminal &&
                sample.NeutralPoseReached && !sample.HasReactionOrRecovery && !sample.HasPendingLanding &&
                !sample.HasPendingMotionOrImpulse && sample.OwnedIntentsReleased && sample.CommandsAcknowledged &&
                sample.HasStableSupport && !sample.HasNewDestabilizingContact;
            float linear = sample.RelativeLinearSpeed;
            float angular = sample.RelativeAngularSpeed;
            bool inside = ready && SettleDetectorConfig.ValidSpeed(linear) && SettleDetectorConfig.ValidSpeed(angular) &&
                linear <= (_stableTicks > 0 ? _config.ExitLinearSpeed : _config.EnterLinearSpeed) &&
                angular <= (_stableTicks > 0 ? _config.ExitAngularSpeed : _config.EnterAngularSpeed);
            if (!inside) _stableTicks = 0;
            else if (_stableTicks < _config.RequiredStableTicks) _stableTicks++;
            return _stableTicks >= _config.RequiredStableTicks;
        }

        public void Reset()
        {
            _hasTick = false;
            _lastTick = 0;
            _stableTicks = 0;
        }
    }
}
