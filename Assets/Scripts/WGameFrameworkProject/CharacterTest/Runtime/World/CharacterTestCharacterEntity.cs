using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestDeterministicUnits
    {
        public const int PositionScale = 1000;
        public const int WireCoordinateScale = 100;
        public const int InputAxisScale = 1000;
        public const int AngleScale = 1000;
        public const int ScaleFactorScale = 1000;
        public const int MassScale = 1000;
        public const int SecondsScale = 1000;
        public const int DefaultTickRate = 60;

        public const short MinInputAxisMilli = -InputAxisScale;
        public const short MaxInputAxisMilli = InputAxisScale;

        public static int MetersToMillimeters(float value)
        {
            return RoundToIntSaturating(Sanitize(value) * PositionScale);
        }

        public static int MetersToMillimeters(double value)
        {
            return RoundToIntSaturating(Sanitize(value) * PositionScale);
        }

        public static float MillimetersToMeters(int millimeters)
        {
            return millimeters / (float)PositionScale;
        }

        public static int VelocityToMillimetersPerSecond(float metersPerSecond)
        {
            return MetersToMillimeters(metersPerSecond);
        }

        public static float MillimetersPerSecondToMetersPerSecond(int millimetersPerSecond)
        {
            return MillimetersToMeters(millimetersPerSecond);
        }

        public static int AccelerationToMillimetersPerSecondSquared(float metersPerSecondSquared)
        {
            return MetersToMillimeters(metersPerSecondSquared);
        }

        public static float MillimetersPerSecondSquaredToMetersPerSecondSquared(int millimetersPerSecondSquared)
        {
            return MillimetersToMeters(millimetersPerSecondSquared);
        }

        public static short AxisToMilli(float value)
        {
            float sanitized = Sanitize(value);
            if (sanitized < -1f)
                sanitized = -1f;
            else if (sanitized > 1f)
                sanitized = 1f;

            int milli = RoundToIntSaturating(sanitized * InputAxisScale);
            if (milli < MinInputAxisMilli)
                return MinInputAxisMilli;
            if (milli > MaxInputAxisMilli)
                return MaxInputAxisMilli;
            return (short)milli;
        }

        public static float MilliToAxis(short milli)
        {
            return milli / (float)InputAxisScale;
        }

        public static int DegreesToMilliDegrees(float value)
        {
            float sanitized = Sanitize(value);
            double normalized = sanitized % 360d;
            if (normalized < 0d)
                normalized += 360d;

            int milli = RoundToIntSaturating(normalized * AngleScale);
            int fullTurn = 360 * AngleScale;
            if (milli >= fullTurn)
                milli -= fullTurn;
            return milli;
        }

        public static int DegreesToMilliDegreesUnnormalized(float value)
        {
            return RoundToIntSaturating(Sanitize(value) * AngleScale);
        }

        public static float MilliDegreesToDegrees(int milliDegrees)
        {
            return NormalizeMilliDegrees(milliDegrees) / (float)AngleScale;
        }

        public static int NormalizeMilliDegrees(int milliDegrees)
        {
            int fullTurn = 360 * AngleScale;
            int normalized = milliDegrees % fullTurn;
            if (normalized < 0)
                normalized += fullTurn;
            return normalized;
        }

        public static int ScaleToMilli(float value)
        {
            float sanitized = Sanitize(value);
            if (sanitized < 0f)
                sanitized = 0f;
            return RoundToIntSaturating(sanitized * ScaleFactorScale);
        }

        public static float MilliToScale(int milli)
        {
            return milli <= 0 ? 0f : milli / (float)ScaleFactorScale;
        }

        public static int KilogramsToGrams(float value)
        {
            float sanitized = Sanitize(value);
            if (sanitized < 0f)
                sanitized = 0f;
            return RoundToIntSaturating(sanitized * MassScale);
        }

        public static float GramsToKilograms(int grams)
        {
            return grams <= 0 ? 0f : grams / (float)MassScale;
        }

        public static int SecondsToMilliseconds(float value)
        {
            float sanitized = Sanitize(value);
            if (sanitized < 0f)
                sanitized = 0f;
            return RoundToIntSaturating(sanitized * SecondsScale);
        }

        public static float MillisecondsToSeconds(int milliseconds)
        {
            return milliseconds <= 0 ? 0f : milliseconds / (float)SecondsScale;
        }

        public static int SecondsToFrames(float seconds, int tickRate = DefaultTickRate)
        {
            float sanitized = Sanitize(seconds);
            if (sanitized <= 0f || tickRate <= 0)
                return 0;

            double frames = sanitized * tickRate;
            if (frames >= int.MaxValue)
                return int.MaxValue;
            double nearestFrame = System.Math.Round(frames);
            if (System.Math.Abs(frames - nearestFrame) <= 0.0001d)
                return System.Math.Max(1, (int)nearestFrame);
            return (int)System.Math.Ceiling(frames);
        }

        public static float FramesToSeconds(int frames, int tickRate = DefaultTickRate)
        {
            return frames <= 0 || tickRate <= 0 ? 0f : frames / (float)tickRate;
        }

        public static Fix64 MillimetersToFix64Meters(int millimeters)
        {
            return Fix64.FromRaw(((long)millimeters * Fix64.Scale) / PositionScale);
        }

        public static Fix64 MillimetersPerSecondToFix64MetersPerSecond(int millimetersPerSecond)
        {
            return MillimetersToFix64Meters(millimetersPerSecond);
        }

        public static int Fix64MetersToMillimeters(Fix64 meters)
        {
            return CharacterTestDeterministicMath.DivideRoundToInt(
                meters.RawValue * PositionScale,
                Fix64.Scale);
        }

        public static int Fix64MetersPerSecondToMillimetersPerSecond(Fix64 metersPerSecond)
        {
            return Fix64MetersToMillimeters(metersPerSecond);
        }

        public static int MillimetersToWireCentimeters(int millimeters)
        {
            int centimeters = RoundToIntSaturating(millimeters / 10d);
            if (centimeters < short.MinValue)
                return short.MinValue;
            if (centimeters > short.MaxValue)
                return short.MaxValue;
            return centimeters;
        }

        public static int WireCentimetersToMillimeters(short centimeters)
        {
            return centimeters * 10;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static double Sanitize(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
        }

        private static int RoundToIntSaturating(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
        }
    }

    public static class CharacterTestDeterministicMath
    {
        public const int TrigScale = 1000000;

        private static readonly int[] CordicAtanMilliDegrees =
        {
            45000,
            26565,
            14036,
            7125,
            3576,
            1790,
            895,
            448,
            224,
            112,
            56,
            28,
            14,
            7,
            3,
            2,
            1
        };

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        public static int ClampControlScaleMilli(int value) => Clamp(value, 0, 10000);

        public static int ClampScaleMilli(int value)
        {
            return Clamp(value, 0, CharacterTestDeterministicUnits.ScaleFactorScale);
        }

        public static int DivideRoundToInt(long numerator, long denominator)
        {
            if (denominator == 0)
                return 0;

            bool negative = (numerator < 0) ^ (denominator < 0);
            long absNumerator = numerator < 0 ? -numerator : numerator;
            long absDenominator = denominator < 0 ? -denominator : denominator;
            long value = (absNumerator + absDenominator / 2) / absDenominator;
            if (negative)
                value = -value;
            if (value <= int.MinValue)
                return int.MinValue;
            if (value >= int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

        public static int MulDivRoundToInt(long value, long multiplier, long denominator)
        {
            return DivideRoundToInt(value * multiplier, denominator);
        }

        public static int Magnitude(int x, int y)
        {
            return IntegerSqrt((long)x * x + (long)y * y);
        }

        public static int SqrMagnitude(int x, int y)
        {
            long sqr = (long)x * x + (long)y * y;
            if (sqr >= int.MaxValue)
                return int.MaxValue;
            return (int)sqr;
        }

        public static void NormalizeToScale(int x, int y, int scale, out int normalizedX, out int normalizedY)
        {
            int magnitude = Magnitude(x, y);
            if (magnitude <= 0 || scale <= 0)
            {
                normalizedX = 0;
                normalizedY = 0;
                return;
            }

            normalizedX = DivideRoundToInt((long)x * scale, magnitude);
            normalizedY = DivideRoundToInt((long)y * scale, magnitude);
        }

        public static void MoveTowards2D(
            int currentX,
            int currentY,
            int targetX,
            int targetY,
            int maxDelta,
            out int resultX,
            out int resultY)
        {
            if (maxDelta <= 0)
            {
                resultX = targetX;
                resultY = targetY;
                return;
            }

            int deltaX = targetX - currentX;
            int deltaY = targetY - currentY;
            int distance = Magnitude(deltaX, deltaY);
            if (distance <= maxDelta || distance == 0)
            {
                resultX = targetX;
                resultY = targetY;
                return;
            }

            resultX = currentX + DivideRoundToInt((long)deltaX * maxDelta, distance);
            resultY = currentY + DivideRoundToInt((long)deltaY * maxDelta, distance);
        }

        public static int MoveTowards(int current, int target, int maxDelta)
        {
            if (maxDelta <= 0)
                return target;
            int delta = target - current;
            if (delta > maxDelta)
                return current + maxDelta;
            if (delta < -maxDelta)
                return current - maxDelta;
            return target;
        }

        public static int MoveTowardsAngle(int currentMilliDegrees, int targetMilliDegrees, int maxDeltaMilliDegrees)
        {
            if (maxDeltaMilliDegrees <= 0)
                return CharacterTestDeterministicUnits.NormalizeMilliDegrees(targetMilliDegrees);

            int current = CharacterTestDeterministicUnits.NormalizeMilliDegrees(currentMilliDegrees);
            int delta = ShortestAngleDelta(current, targetMilliDegrees);
            if (delta > maxDeltaMilliDegrees)
                delta = maxDeltaMilliDegrees;
            else if (delta < -maxDeltaMilliDegrees)
                delta = -maxDeltaMilliDegrees;

            return CharacterTestDeterministicUnits.NormalizeMilliDegrees(current + delta);
        }

        public static int ShortestAngleDelta(int currentMilliDegrees, int targetMilliDegrees)
        {
            int current = CharacterTestDeterministicUnits.NormalizeMilliDegrees(currentMilliDegrees);
            int target = CharacterTestDeterministicUnits.NormalizeMilliDegrees(targetMilliDegrees);
            int fullTurn = 360 * CharacterTestDeterministicUnits.AngleScale;
            int delta = (target - current) % fullTurn;
            if (delta > fullTurn / 2)
                delta -= fullTurn;
            else if (delta < -fullTurn / 2)
                delta += fullTurn;
            return delta;
        }

        public static int Atan2MilliDegrees(int y, int x)
        {
            if (x == 0 && y == 0)
                return 0;

            long absX = x < 0 ? -(long)x : x;
            long absY = y < 0 ? -(long)y : y;
            int angle;
            if (absX >= absY)
                angle = AtanRatioMilliDegrees(absY, absX);
            else
                angle = 90000 - AtanRatioMilliDegrees(absX, absY);

            if (x < 0)
                angle = 180000 - angle;
            if (y < 0)
                angle = 360000 - angle;
            return CharacterTestDeterministicUnits.NormalizeMilliDegrees(angle);
        }

        public static void SinCosMilliDegrees(int milliDegrees, out int sinScaled, out int cosScaled)
        {
            int angle = CharacterTestDeterministicUnits.NormalizeMilliDegrees(milliDegrees);
            if (angle > 180000)
                angle -= 360000;

            int sign = 1;
            if (angle > 90000)
            {
                angle -= 180000;
                sign = -1;
            }
            else if (angle < -90000)
            {
                angle += 180000;
                sign = -1;
            }

            long x = 607253;
            long y = 0;
            int z = angle;
            for (int i = 0; i < CordicAtanMilliDegrees.Length; i++)
            {
                long nextX;
                long nextY;
                if (z >= 0)
                {
                    nextX = x - (y >> i);
                    nextY = y + (x >> i);
                    z -= CordicAtanMilliDegrees[i];
                }
                else
                {
                    nextX = x + (y >> i);
                    nextY = y - (x >> i);
                    z += CordicAtanMilliDegrees[i];
                }

                x = nextX;
                y = nextY;
            }

            sinScaled = (int)(y * sign);
            cosScaled = (int)(x * sign);
        }

        public static Fix64 CosFix64FromMilliDegrees(int milliDegrees)
        {
            SinCosMilliDegrees(milliDegrees, out _, out int cosScaled);
            return Fix64.FromRaw(((long)cosScaled * Fix64.Scale) / TrigScale);
        }

        private static int AtanRatioMilliDegrees(long numerator, long denominator)
        {
            if (denominator <= 0 || numerator <= 0)
                return 0;

            long ratio = (numerator * TrigScale + denominator / 2) / denominator;
            if (ratio > TrigScale)
                ratio = TrigScale;

            long correction = 15642L * (TrigScale - ratio) / TrigScale;
            return DivideRoundToInt(ratio * (45000 + correction), TrigScale);
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0)
                return 0;

            ulong n = (ulong)value;
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > n)
                bit >>= 2;

            while (bit != 0)
            {
                if (n >= result + bit)
                {
                    n -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }

                bit >>= 2;
            }

            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }

    internal enum CharacterTestGameplayIntentKind
    {
        None = 0,
        AbilitySlotCast = 1,
        AttackSecondary = 2,
        SkillSlot = 3,
        AttackPrimary = 4,
        Jump = 5,
        Dodge = 6
    }

    internal readonly struct CharacterTestGameplayIntent
    {
        public CharacterTestGameplayIntent(
            CharacterTestGameplayIntentKind kind,
            int targetEntityId,
            long createdFrame,
            long expiresFrame,
            long sequence,
            int skillSlotPressedMask = 0,
            int abilitySlotPressed = 0,
            int abilityIdPressed = 0)
        {
            Kind = kind;
            TargetEntityId = targetEntityId;
            CreatedFrame = createdFrame;
            ExpiresFrame = expiresFrame;
            Sequence = sequence;
            SkillSlotPressedMask = skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits;
            AbilitySlotPressed = CharacterTestAbilityHotbarRuntime.IsValidSlot(abilitySlotPressed)
                ? abilitySlotPressed
                : 0;
            AbilityIdPressed = AbilitySlotPressed > 0 && abilityIdPressed > 0 ? abilityIdPressed : 0;
        }

        public CharacterTestGameplayIntentKind Kind { get; }
        public int TargetEntityId { get; }
        public long CreatedFrame { get; }
        public long ExpiresFrame { get; }
        public long Sequence { get; }
        public int SkillSlotPressedMask { get; }
        public int AbilitySlotPressed { get; }
        public int AbilityIdPressed { get; }
        public bool IsValid => Kind != CharacterTestGameplayIntentKind.None;

        public int RemainingFrames(long frame)
        {
            if (!IsValid || frame >= ExpiresFrame)
                return 0;

            long remaining = ExpiresFrame - frame;
            return remaining >= int.MaxValue ? int.MaxValue : (int)remaining;
        }
    }

    internal sealed class CharacterTestGameplayIntentBuffer
    {
        public const int DefaultBufferFrames = 12;
        private const int Capacity = 6;

        private readonly CharacterTestGameplayIntent[] _entries =
            new CharacterTestGameplayIntent[Capacity];
        private long _nextSequence;

        public int PendingCount { get; private set; }

        public void Clear()
        {
            for (int i = 0; i < _entries.Length; i++)
                _entries[i] = default;

            PendingCount = 0;
        }

        public void ClearExpired(long frame)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (!IsExpired(_entries[i], frame))
                    continue;

                if (_entries[i].IsValid)
                    PendingCount--;
                _entries[i] = default;
            }
        }

        public void AddOrRefresh(
            CharacterTestGameplayIntentKind kind,
            int targetEntityId,
            long frame,
            int bufferFrames = DefaultBufferFrames,
            int skillSlotPressedMask = 0,
            int abilitySlotPressed = 0,
            int abilityIdPressed = 0)
        {
            int index = ToIndex(kind);
            if (index < 0)
                return;

            int safeBufferFrames = bufferFrames > 0 ? bufferFrames : DefaultBufferFrames;
            CharacterTestGameplayIntent existing = _entries[index];
            if (!existing.IsValid)
                PendingCount++;

            _entries[index] = new CharacterTestGameplayIntent(
                kind,
                targetEntityId,
                frame,
                frame + safeBufferFrames,
                _nextSequence++,
                skillSlotPressedMask,
                abilitySlotPressed,
                abilityIdPressed);
        }

        public bool TryPeekHighestPriority(long frame, out CharacterTestGameplayIntent intent)
        {
            ClearExpired(frame);
            if (TryGet(CharacterTestGameplayIntentKind.AbilitySlotCast, frame, out intent))
                return true;
            if (TryGet(CharacterTestGameplayIntentKind.AttackSecondary, frame, out intent))
                return true;
            if (TryGet(CharacterTestGameplayIntentKind.SkillSlot, frame, out intent))
                return true;
            if (TryGet(CharacterTestGameplayIntentKind.Dodge, frame, out intent))
                return true;
            if (TryGet(CharacterTestGameplayIntentKind.AttackPrimary, frame, out intent))
                return true;
            return TryGet(CharacterTestGameplayIntentKind.Jump, frame, out intent);
        }

        public bool TryGet(
            CharacterTestGameplayIntentKind kind,
            long frame,
            out CharacterTestGameplayIntent intent)
        {
            int index = ToIndex(kind);
            if (index < 0)
            {
                intent = default;
                return false;
            }

            CharacterTestGameplayIntent entry = _entries[index];
            if (IsExpired(entry, frame))
            {
                intent = default;
                return false;
            }

            intent = entry;
            return true;
        }

        public int GetRemainingFrames(CharacterTestGameplayIntentKind kind, long frame)
        {
            return TryGet(kind, frame, out CharacterTestGameplayIntent intent)
                ? intent.RemainingFrames(frame)
                : 0;
        }

        public void Consume(CharacterTestGameplayIntentKind kind)
        {
            int index = ToIndex(kind);
            if (index < 0 || !_entries[index].IsValid)
                return;

            _entries[index] = default;
            PendingCount--;
        }

        private static bool IsExpired(CharacterTestGameplayIntent intent, long frame)
        {
            return !intent.IsValid || frame >= intent.ExpiresFrame;
        }

        private static int ToIndex(CharacterTestGameplayIntentKind kind)
        {
            switch (kind)
            {
                case CharacterTestGameplayIntentKind.AbilitySlotCast:
                    return 0;
                case CharacterTestGameplayIntentKind.AttackSecondary:
                    return 1;
                case CharacterTestGameplayIntentKind.SkillSlot:
                    return 2;
                case CharacterTestGameplayIntentKind.AttackPrimary:
                    return 3;
                case CharacterTestGameplayIntentKind.Jump:
                    return 4;
                case CharacterTestGameplayIntentKind.Dodge:
                    return 5;
                default:
                    return -1;
            }
        }
    }

    public sealed partial class CharacterTestCharacterEntity
    {
        private readonly CharacterTestComponentWorld _componentWorld;
        private readonly List<CharacterAuthoritySecondaryPoseSample> _authorityPoseSamplesBeforeTick;
        private readonly List<CharacterAuthoritySecondaryPoseSample> _authorityPoseSamplesAfterTick;

        public CharacterTestCharacterEntity(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterIdentityComponent identity,
            CharacterTestCharacterTeamComponent team,
            CharacterTestCharacterTransformComponent transform,
            CharacterTestCharacterEquipmentComponent equipment,
            CharacterTestCharacterControlComponent control,
            GameplayEntityId gameplayEntityId)
            : this(
                id,
                identity,
                team,
                transform,
                equipment,
                control,
                gameplayEntityId,
                CharacterTestCharacterPhysicsComponent.Default)
        {
        }

        public CharacterTestCharacterEntity(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterIdentityComponent identity,
            CharacterTestCharacterTeamComponent team,
            CharacterTestCharacterTransformComponent transform,
            CharacterTestCharacterEquipmentComponent equipment,
            CharacterTestCharacterControlComponent control,
            GameplayEntityId gameplayEntityId,
            CharacterTestCharacterPhysicsComponent physics)
            : this(
                new CharacterTestComponentWorld(),
                id,
                identity,
                team,
                transform,
                equipment,
                control,
                gameplayEntityId,
                physics)
        {
        }

        internal CharacterTestCharacterEntity(
            CharacterTestComponentWorld componentWorld,
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterIdentityComponent identity,
            CharacterTestCharacterTeamComponent team,
            CharacterTestCharacterTransformComponent transform,
            CharacterTestCharacterEquipmentComponent equipment,
            CharacterTestCharacterControlComponent control,
            GameplayEntityId gameplayEntityId,
            CharacterTestCharacterPhysicsComponent physics)
        {
            _componentWorld = componentWorld ?? new CharacterTestComponentWorld();
            Id = id;
            _componentWorld.RegisterCharacter(
                id,
                identity,
                team,
                transform,
                equipment,
                control,
                gameplayEntityId,
                physics);
            int authorityPoseSampleCapacity = _componentWorld.GetAuthorityPose(id)?.Definition?.SecondaryBindings?.Count ?? 0;
            _authorityPoseSamplesBeforeTick = new List<CharacterAuthoritySecondaryPoseSample>(authorityPoseSampleCapacity);
            _authorityPoseSamplesAfterTick = new List<CharacterAuthoritySecondaryPoseSample>(authorityPoseSampleCapacity);
        }

        public CharacterTestCharacterEntityId Id { get; }
        public CharacterTestCharacterIdentityComponent Identity => _componentWorld.GetIdentity(Id);
        public CharacterTestCharacterTeamComponent Team => _componentWorld.GetTeam(Id);
        public CharacterTestCharacterTransformComponent Transform => _componentWorld.GetTransform(Id);
        public CharacterTestCharacterEquipmentComponent Equipment => _componentWorld.GetEquipment(Id);
        public CharacterTestCharacterControlComponent Control => _componentWorld.GetControl(Id);
        public GameplayEntityId GameplayEntityId => _componentWorld.GetGameplayEntityId(Id);
        public CharacterTestCharacterPhysicsComponent Physics => _componentWorld.GetPhysics(Id);
        public CharacterTestColliderPostureComponent ColliderPosture => _componentWorld.GetColliderPosture(Id);
        public CharacterTestCharacterCombatStateComponent CombatState => _componentWorld.GetCombatState(Id);
        internal CharacterTestBodyPartStateComponent BodyPartState =>
            _componentWorld.GetBodyPartState(Id);
        internal CharacterTestEntityPresenceComponent Presence => _componentWorld.GetPresence(Id);
        internal CharacterTestBattleStateComponent BattleState => _componentWorld.GetBattleState(Id);
        internal CharacterTestSpecialHitStateComponent SpecialHitState =>
            _componentWorld.GetSpecialHitState(Id);
        internal CharacterTestSpecialHitSusceptibilityComponent SpecialHitSusceptibility =>
            _componentWorld.GetSpecialHitSusceptibility(Id);
        public CharacterTestCharacterInputComponent Input => _componentWorld.GetInput(Id);
        public CharacterTestCharacterAnimationComponent Animation => _componentWorld.GetAnimation(Id);
        public CharacterTestCharacterActionComponent Action => _componentWorld.GetAction(Id);
        public CharacterAuthorityPoseStateComponent AuthorityPose => _componentWorld.GetAuthorityPose(Id);
        internal CharacterTestTerrainIkStateComponent TerrainIkState =>
            _componentWorld.GetTerrainIk(Id);
        public CharacterTestResolvedAuthorityPoseFrame ResolvedAuthorityPoseFrame =>
            TerrainIkState?.ResolvedFrame;
        /// <summary>
        /// Monotonic write revision for root/source pose geometry. It is
        /// independent from the simulation frame and does not change when
        /// read.
        /// </summary>
        public int SourcePoseRevision => _componentWorld.GetSourceRevisions(Id).PoseRevision;
        /// <summary>
        /// Monotonic write revision for source facing (root yaw). It is
        /// independent from the simulation frame and does not change when
        /// read.
        /// </summary>
        public int SourceFacingRevision => _componentWorld.GetSourceRevisions(Id).FacingRevision;
        internal CharacterTestLivingNaturalAttributesRuntimeState LivingNaturalAttributes =>
            _componentWorld.GetLivingNaturalAttributes(Id);
        public int VerticalVelocityMillimetersPerSecond =>
            MovementState.VerticalVelocityMillimetersPerSecond;
        public int HorizontalVelocityXMillimetersPerSecond =>
            MovementState.HorizontalVelocityXMillimetersPerSecond;
        public int HorizontalVelocityYMillimetersPerSecond =>
            MovementState.HorizontalVelocityYMillimetersPerSecond;
        public int DesiredVelocityXMillimetersPerSecond =>
            MovementState.DesiredVelocityXMillimetersPerSecond;
        public int DesiredVelocityYMillimetersPerSecond =>
            MovementState.DesiredVelocityYMillimetersPerSecond;
        public short LastMoveInputXMilli => MovementState.LastMoveInputXMilli;
        public short LastMoveInputYMilli => MovementState.LastMoveInputYMilli;
        public bool IsGrounded => MovementState.IsGrounded;
        public int JumpBufferFramesRemaining => MovementState.JumpBufferFramesRemaining;
        public int CoyoteTimeFramesRemaining => MovementState.CoyoteTimeFramesRemaining;
        public CharacterMovementMode MovementMode => MovementState.MovementMode;
        public bool MovementLocked => MovementState.MovementLocked;
        public int MovementControlScaleMilli => MovementState.MovementControlScaleMilli;
        public int RotationControlScaleMilli => MovementState.RotationControlScaleMilli;
        public int MovementProfileId => MovementState.MovementProfileId;
        public bool MovementProfileFallback => MovementState.MovementProfileFallback;
        public bool PhysicsEnabled => MovementState.PhysicsEnabled;
        public CombatMotionCollisionFlags CollisionFlags => MovementState.CollisionFlags;
        public int SurfaceFrictionMilli => MovementState.SurfaceFrictionMilli;
        public int SurfaceBounceSpeedCapYMillimetersPerSecond =>
            MovementState.SurfaceBounceSpeedCapYMillimetersPerSecond;
        public CombatSurfaceMaterial SurfaceMaterial => MovementState.SurfaceMaterial;
        public CombatSurfaceMaterialKind SurfaceMaterialKind => SurfaceMaterial.Kind;
        public string SurfaceMaterialStableId => SurfaceMaterial.StableId;
        public float SurfaceElasticity => ToFloat(SurfaceMaterial.Elasticity);
        public float SurfaceFixedBounceHeight => ToFloat(SurfaceMaterial.FixedBounceHeight);
        public float VerticalVelocity =>
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(VerticalVelocityMillimetersPerSecond);
        public Vector2 HorizontalVelocity => new Vector2(
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(HorizontalVelocityXMillimetersPerSecond),
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(HorizontalVelocityYMillimetersPerSecond));
        public Vector2 DesiredVelocity => new Vector2(
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(DesiredVelocityXMillimetersPerSecond),
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(DesiredVelocityYMillimetersPerSecond));
        public Vector2 LastMoveInput => new Vector2(
            CharacterTestDeterministicUnits.MilliToAxis(LastMoveInputXMilli),
            CharacterTestDeterministicUnits.MilliToAxis(LastMoveInputYMilli));
        public float JumpBufferSecondsRemaining =>
            CharacterTestDeterministicUnits.FramesToSeconds(JumpBufferFramesRemaining);
        public float CoyoteTimeSecondsRemaining =>
            CharacterTestDeterministicUnits.FramesToSeconds(CoyoteTimeFramesRemaining);
        public float MovementControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(MovementControlScaleMilli);
        public float RotationControlScale =>
            CharacterTestDeterministicUnits.MilliToScale(RotationControlScaleMilli);
        public float SurfaceFriction =>
            CharacterTestDeterministicUnits.MilliToScale(SurfaceFrictionMilli);
        public float SurfaceBounceSpeedCapY =>
            CharacterTestDeterministicUnits.MillimetersPerSecondToMetersPerSecond(
                SurfaceBounceSpeedCapYMillimetersPerSecond);
        public IReadOnlyList<CharacterTestCharacterMotionRequest> MotionRequests =>
            RequestBuffers.MotionRequests;
        public IReadOnlyList<CharacterActionCombatRequest> CombatRequests =>
            RequestBuffers.CombatRequests;
        public IReadOnlyList<CharacterActionGameplayRequest> GameplayRequests =>
            RequestBuffers.GameplayRequests;
        public IReadOnlyList<CharacterActionAudioCueRequest> AudioCueRequests =>
            RequestBuffers.AudioCueRequests;
        public IReadOnlyList<CharacterActionVfxRequest> VfxRequests => RequestBuffers.VfxRequests;
        public IReadOnlyList<HitResolveResult> LastCombatHitResults =>
            RequestBuffers.LastCombatHitResults;
        internal CharacterTestGameplayIntentBuffer GameplayIntentBuffer =>
            RequestBuffers.GameplayIntentBuffer;
        public int PendingMotionRequestCount => RequestBuffers.MotionRequests.Count;
        public int PendingCombatRequestCount => RequestBuffers.CombatRequests.Count;
        public int PendingGameplayRequestCount => RequestBuffers.GameplayRequests.Count;
        public int PendingAudioCueRequestCount => RequestBuffers.AudioCueRequests.Count;
        public int PendingVfxRequestCount => RequestBuffers.VfxRequests.Count;
        public int LastCombatHitCount => RequestBuffers.LastCombatHitResults.Count;
        public int PendingGameplayIntentCount => RequestBuffers.GameplayIntentBuffer.PendingCount;

        internal CharacterTestComponentWorld ComponentWorld => _componentWorld;
        private CharacterTestCharacterMovementStateComponent MovementState =>
            _componentWorld.GetMovement(Id);
        private CharacterTestCharacterRequestBufferComponent RequestBuffers =>
            _componentWorld.GetMutableRequestBuffer(Id);

        public void SetTransform(CharacterTestCharacterTransformComponent transform)
        {
            _componentWorld.SetTransform(Id, transform);
        }

        public void SetTeam(CharacterTestCharacterTeamComponent team)
        {
            _componentWorld.SetTeam(Id, team);
        }

        public void SetInput(CharacterTestCharacterInputComponent input)
        {
            _componentWorld.SetInput(Id, input);
        }

        public void SetControl(CharacterTestCharacterControlComponent control)
        {
            _componentWorld.SetControl(Id, control);
        }

        public void SetPhysics(CharacterTestCharacterPhysicsComponent physics)
        {
            _componentWorld.SetPhysics(Id, physics);
        }

        /// <summary>Updates the equipment state used to select this entity's Character ActionSet.</summary>
        /// <remarks>LowFreqAlloc boundary. Call when an equipment transaction commits, not from Tick.</remarks>
        public void SetEquipment(CharacterTestCharacterEquipmentComponent equipment)
        {
            _componentWorld.SetComponent(Id, equipment);
        }

        internal CharacterTestBodyPartStateInitializationStatus ReinitializeBodyPartState(
            CharacterImportedGeometryBinding geometryBinding)
        {
            return BodyPartState.Reinitialize(geometryBinding);
        }

        internal void SetColliderPosture(CharacterTestColliderPostureComponent posture)
        {
            _componentWorld.SetColliderPosture(Id, posture);
        }

        public void SetMotionState(
            float verticalVelocity,
            Vector2 lastMoveInput,
            bool isGrounded,
            Vector2 horizontalVelocity = default,
            Vector2 desiredVelocity = default,
            float jumpBufferSecondsRemaining = 0f,
            float coyoteTimeSecondsRemaining = 0f,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            bool movementLocked = false,
            float movementControlScale = 1f,
            float rotationControlScale = 1f,
            int movementProfileId = 0,
            bool movementProfileFallback = false,
            bool physicsEnabled = false,
            CombatMotionCollisionFlags collisionFlags = CombatMotionCollisionFlags.None,
            float surfaceFriction = 0f,
            CombatSurfaceMaterial? surfaceMaterial = null,
            float surfaceBounceSpeedCapY = 0f)
        {
            SetMotionStateDeterministic(
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(verticalVelocity),
                CharacterTestDeterministicUnits.AxisToMilli(lastMoveInput.x),
                CharacterTestDeterministicUnits.AxisToMilli(lastMoveInput.y),
                isGrounded,
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(horizontalVelocity.x),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(horizontalVelocity.y),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(desiredVelocity.x),
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(desiredVelocity.y),
                CharacterTestDeterministicUnits.SecondsToFrames(jumpBufferSecondsRemaining),
                CharacterTestDeterministicUnits.SecondsToFrames(coyoteTimeSecondsRemaining),
                movementMode,
                movementLocked,
                CharacterTestDeterministicUnits.ScaleToMilli(movementControlScale),
                CharacterTestDeterministicUnits.ScaleToMilli(rotationControlScale),
                movementProfileId,
                movementProfileFallback,
                physicsEnabled,
                collisionFlags,
                CharacterTestDeterministicUnits.ScaleToMilli(surfaceFriction),
                surfaceMaterial,
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(surfaceBounceSpeedCapY));
        }

        public void SetMotionStateDeterministic(
            int verticalVelocityMillimetersPerSecond,
            short lastMoveInputXMilli,
            short lastMoveInputYMilli,
            bool isGrounded,
            int horizontalVelocityXMillimetersPerSecond = 0,
            int horizontalVelocityYMillimetersPerSecond = 0,
            int desiredVelocityXMillimetersPerSecond = 0,
            int desiredVelocityYMillimetersPerSecond = 0,
            int jumpBufferFramesRemaining = 0,
            int coyoteTimeFramesRemaining = 0,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            bool movementLocked = false,
            int movementControlScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int rotationControlScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale,
            int movementProfileId = 0,
            bool movementProfileFallback = false,
            bool physicsEnabled = false,
            CombatMotionCollisionFlags collisionFlags = CombatMotionCollisionFlags.None,
            int surfaceFrictionMilli = 0,
            CombatSurfaceMaterial? surfaceMaterial = null,
            int surfaceBounceSpeedCapYMillimetersPerSecond = 0)
        {
            CombatSurfaceMaterial resolvedSurfaceMaterial = surfaceMaterial ?? CombatSurfaceMaterial.FromFriction(
                ToFix64(CharacterTestDeterministicUnits.MilliToScale(surfaceFrictionMilli)));
            int resolvedSurfaceFrictionMilli = CharacterTestDeterministicUnits.ScaleToMilli(
                ToFloat(resolvedSurfaceMaterial.Friction));
            _componentWorld.SetMovement(
                Id,
                new CharacterTestCharacterMovementStateComponent(
                    verticalVelocityMillimetersPerSecond,
                    horizontalVelocityXMillimetersPerSecond,
                    horizontalVelocityYMillimetersPerSecond,
                    desiredVelocityXMillimetersPerSecond,
                    desiredVelocityYMillimetersPerSecond,
                    lastMoveInputXMilli,
                    lastMoveInputYMilli,
                    isGrounded,
                    jumpBufferFramesRemaining,
                    coyoteTimeFramesRemaining,
                    movementMode,
                    movementLocked,
                    movementControlScaleMilli,
                    rotationControlScaleMilli,
                    movementProfileId,
                    movementProfileFallback,
                    physicsEnabled,
                    collisionFlags,
                    resolvedSurfaceFrictionMilli,
                    resolvedSurfaceMaterial,
                    surfaceBounceSpeedCapYMillimetersPerSecond));
        }

        public void ConsumeJumpBuffer()
        {
            _componentWorld.SetMovement(Id, MovementState.WithoutJumpBuffer());
        }

        internal void BufferJumpFrames(int frames)
        {
            _componentWorld.SetMovement(Id, MovementState.WithJumpBufferFrames(frames));
        }

        internal TacticalCard.TacticalInteractionEffectAuthority InteractionEffects => _componentWorld.GetInteractionEffects(Id);
        internal CharacterTestPositionRecallState PositionRecall => _componentWorld.GetPositionRecall(Id);
        internal void SetPositionRecall(in CharacterTestPositionRecallState state) => _componentWorld.SetPositionRecall(Id, state);
        internal CharacterTestGroundPointTractionState GroundPointTraction => _componentWorld.GetGroundPointTraction(Id);
        internal void SetGroundPointTraction(in CharacterTestGroundPointTractionState state) => _componentWorld.SetGroundPointTraction(Id, state);

        public void EnqueueMotionRequest(CharacterTestCharacterMotionRequest request)
        {
            if (request.HasValue)
                RequestBuffers.MotionRequests.Add(request);
        }

        public void ClearMotionRequests()
        {
            RequestBuffers.MotionRequests.Clear();
        }

        public void EnqueueCombatRequest(CharacterActionCombatRequest request)
        {
            if (request.EventKind != CharacterActionTrackEventKind.None)
                RequestBuffers.CombatRequests.Add(request);
        }

        public void ClearCombatRequests()
        {
            RequestBuffers.CombatRequests.Clear();
        }

        public void EnqueueGameplayRequest(CharacterActionGameplayRequest request)
        {
            if (request.EventKind != CharacterActionTrackEventKind.None)
                RequestBuffers.GameplayRequests.Add(request);
        }

        public void ClearGameplayRequests()
        {
            RequestBuffers.GameplayRequests.Clear();
        }

        public void EnqueueAudioCueRequest(CharacterActionAudioCueRequest request)
        {
            if (request.CueId > 0)
            {
                RequestBuffers.AudioCueRequests.Add(request);
                RequestBuffers.AudioCuePresentationKinds.Add(
                    CharacterTestAudioCueRequestClassifier.Classify(request));
            }
        }

        public void ClearAudioCueRequests()
        {
            RequestBuffers.AudioCueRequests.Clear();
            RequestBuffers.AudioCuePresentationKinds.Clear();
        }

        /// <remarks>NoAlloc. Reads the low-frequency classification stored when the request entered the buffer.</remarks>
        internal CharacterTestAudioPresentationKind GetAudioCuePresentationKindAt(int index)
        {
            if (index < 0 || index >= RequestBuffers.AudioCuePresentationKinds.Count)
                return CharacterTestAudioPresentationKind.None;
            return RequestBuffers.AudioCuePresentationKinds[index];
        }

        /// <remarks>NoAlloc. Used by presentation bridges that replace one authored Cue with a resolved numeric Event.</remarks>
        internal void RemoveAudioCueRequestAt(int index)
        {
            if (index < 0 || index >= RequestBuffers.AudioCueRequests.Count)
                return;
            RequestBuffers.AudioCueRequests.RemoveAt(index);
            if (index < RequestBuffers.AudioCuePresentationKinds.Count)
                RequestBuffers.AudioCuePresentationKinds.RemoveAt(index);
        }

        public void EnqueueVfxRequest(CharacterActionVfxRequest request)
        {
            if (!string.IsNullOrEmpty(request.ResourceKey))
                RequestBuffers.VfxRequests.Add(request);
        }

        public void ClearVfxRequests()
        {
            RequestBuffers.VfxRequests.Clear();
        }

        public void SetLastCombatHitResults(IReadOnlyList<HitResolveResult> results)
        {
            RequestBuffers.LastCombatHitResults.Clear();
            if (results == null)
                return;

            for (int i = 0; i < results.Count; i++)
                RequestBuffers.LastCombatHitResults.Add(results[i]);
        }

        public void ApplyCombatHit(HitResolveResult result, long frame, int sourceEntityId)
        {
            if (!result.IsAcceptedDamage)
                return;

            SetCombatState(CombatState.ApplyHit(
                result.Damage,
                result.StaggerFrames,
                frame,
                sourceEntityId,
                result.TraceId));
        }

        public void ApplyCombatHitReaction(
            HitResolveResult result,
            int appliedDamage,
            int reactionFrames,
            long frame,
            int sourceEntityId,
            int currentHealth,
            int maxHealth)
        {
            if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked)
                return;

            SetCombatState(CombatState.ApplyHitReaction(
                appliedDamage,
                reactionFrames,
                frame,
                sourceEntityId,
                result.TraceId,
                maxHealth,
                currentHealth));
        }

        public void SyncCombatHealth(int maxHealth, int health)
        {
            SetCombatState(CombatState.WithHealth(maxHealth, health));
        }

        public void SyncCombatScatterPressure(GameplayScatterPressureComponent scatterPressure)
        {
            SetCombatState(CombatState.WithScatterPressure(
                scatterPressure.CurrentScatterPressure,
                scatterPressure.CurrentCohesionLimit,
                scatterPressure.CurrentBand,
                scatterPressure.BrokenRecoveryDelayFrames,
                scatterPressure.LastCohesionBreakFrame));
        }

        public void ApplyCohesionBreak(long frame)
        {
            SetCombatState(CombatState.ApplyCohesionBreak(
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames,
                frame));
        }

        public void BeginDeath(long frame, int sourceEntityId, int traceId)
        {
            SetCombatState(CombatState.BeginDeath(frame, sourceEntityId, traceId));
            SetSpecialHitState(CharacterTestSpecialHitStateComponent.None);
            ClearPendingPresentationRequests();
            RequestBuffers.GameplayIntentBuffer.Clear();
            ConsumeJumpBuffer();
            SetInput(Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
        }

        public void CompleteDeath(long frame)
        {
            SetCombatState(CombatState.CompleteDeath(frame));
            SetSpecialHitState(CharacterTestSpecialHitStateComponent.None);
            ClearPendingPresentationRequests();
            RequestBuffers.GameplayIntentBuffer.Clear();
            ConsumeJumpBuffer();
            SetInput(Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
        }

        public void BeginRevive(long frame, int health)
        {
            SetCombatState(CombatState.BeginRevive(frame, health));
            SetSpecialHitState(CharacterTestSpecialHitStateComponent.None);
            ClearPendingPresentationRequests();
            RequestBuffers.GameplayIntentBuffer.Clear();
            ConsumeJumpBuffer();
            SetInput(Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
        }

        public void CompleteRevive(long frame)
        {
            SetCombatState(CombatState.CompleteRevive(frame));
            SetSpecialHitState(CharacterTestSpecialHitStateComponent.None);
            ClearPendingPresentationRequests();
            RequestBuffers.GameplayIntentBuffer.Clear();
            ConsumeJumpBuffer();
            SetInput(Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
        }

        public void SetCombatState(CharacterTestCharacterCombatStateComponent combatState)
        {
            _componentWorld.SetCombatState(Id, combatState);
        }

        internal void SetPresence(CharacterTestEntityPresenceComponent presence)
        {
            _componentWorld.SetPresence(Id, presence);
        }

        internal void SetSpecialHitState(CharacterTestSpecialHitStateComponent state)
        {
            _componentWorld.SetSpecialHitState(Id, state);
        }

        internal void SetSpecialHitSusceptibility(
            CharacterTestSpecialHitSusceptibilityComponent susceptibility)
        {
            _componentWorld.SetSpecialHitSusceptibility(Id, susceptibility);
        }

        public void TickCombatState()
        {
            SetCombatState(CombatState.Tick());
        }

        private void ClearPendingPresentationRequests()
        {
            RequestBuffers.ClearPresentationRequests();
        }

        public void SetAnimation(CharacterTestCharacterAnimationComponent animation)
        {
            _componentWorld.SetAnimation(Id, animation);
        }

        public void SetAction(CharacterTestCharacterActionComponent action)
        {
            _componentWorld.SetAction(Id, action);
        }

        internal void SetLivingNaturalAttributes(CharacterTestLivingNaturalAttributesRuntimeState state)
        {
            _componentWorld.SetLivingNaturalAttributes(Id, state);
        }

        public void SetAuthorityPose(CharacterAuthorityPoseStateComponent authorityPose)
        {
            CharacterAuthorityPoseStateComponent previous = AuthorityPose;
            CharacterAuthorityPoseStateComponent replacement = authorityPose
                ?? new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition());
            bool changed = !ReferenceEquals(previous, replacement)
                && (previous == null
                    || !AreAuthorityPoseSnapshotsEqual(
                        previous.CreateSnapshot(),
                        replacement.CreateSnapshot())
                    || (previous != null
                        && !AreAuthorityPoseDefinitionsEqual(
                            previous.Definition,
                            replacement.Definition)));
            _componentWorld.SetAuthorityPose(Id, replacement);
            EnsureAuthorityPoseSampleCapacity(AuthorityPose);
            if (changed)
                _componentWorld.TouchAuthorityPoseRevision(Id);
        }

        internal void SetTerrainIkState(CharacterTestTerrainIkStateComponent terrainIk)
        {
            _componentWorld.SetTerrainIk(Id, terrainIk);
        }

        internal bool ApplyAuthorityPoseImpulse(
            CharacterAuthorityPoseImpulseRequest request,
            RuntimeFrame startFrame,
            out string issue)
        {
            issue = string.Empty;
            CharacterAuthorityPoseStateComponent pose = AuthorityPose;
            if (pose == null)
            {
                issue = "authority pose state is not available";
                return false;
            }

            bool accepted = pose.ApplySecondaryImpulse(request, startFrame, out issue);
            if (accepted)
                _componentWorld.TouchAuthorityPoseRevision(Id);
            return accepted;
        }

        internal void RestoreAuthorityPoseSnapshot(CharacterAuthorityPoseSnapshot snapshot)
        {
            CharacterAuthorityPoseStateComponent pose = AuthorityPose;
            if (pose == null)
            {
                SetAuthorityPose(new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));
                pose = AuthorityPose;
            }

            CharacterAuthorityPoseSnapshot previous = pose.CreateSnapshot();
            pose.RestoreSnapshot(snapshot);
            if (!AreAuthorityPoseSnapshotsEqual(previous, pose.CreateSnapshot()))
                _componentWorld.TouchAuthorityPoseRevision(Id);
        }

        /// <summary>
        /// Restores the source identity frontier after the enclosing network or
        /// replay snapshot has restored root/pose state. The caller owns the
        /// authoritative snapshot values; this method never derives a token
        /// from the current frame or a hash.
        /// </summary>
        internal void RestoreSourceRevisions(int poseRevision, int facingRevision)
        {
            _componentWorld.RestoreSourceRevisions(Id, poseRevision, facingRevision);
        }

        public void TickAuthorityPose(RuntimeFrame frame)
        {
            CharacterAuthorityPoseStateComponent pose = AuthorityPose;
            if (pose == null)
                return;

            if (!pose.HasActiveWork)
                return;

            EnsureAuthorityPoseSampleCapacity(pose);
            pose.CopySecondaryPoseSamplesTo(_authorityPoseSamplesBeforeTick);
            pose.Tick(frame);
            pose.CopySecondaryPoseSamplesTo(_authorityPoseSamplesAfterTick);
            if (!AreAuthorityPoseSamplesEqual(
                    _authorityPoseSamplesBeforeTick,
                    _authorityPoseSamplesAfterTick))
                _componentWorld.TouchAuthorityPoseRevision(Id);
        }

        private void EnsureAuthorityPoseSampleCapacity(CharacterAuthorityPoseStateComponent pose)
        {
            int required = pose?.Definition?.SecondaryBindings?.Count ?? 0;
            if (_authorityPoseSamplesBeforeTick.Capacity < required)
                _authorityPoseSamplesBeforeTick.Capacity = required;
            if (_authorityPoseSamplesAfterTick.Capacity < required)
                _authorityPoseSamplesAfterTick.Capacity = required;
        }

        private static bool AreAuthorityPoseSamplesEqual(
            List<CharacterAuthoritySecondaryPoseSample> left,
            List<CharacterAuthoritySecondaryPoseSample> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                CharacterAuthoritySecondaryPoseSample a = left[i];
                CharacterAuthoritySecondaryPoseSample b = right[i];
                if (a.ChannelId != b.ChannelId
                    || a.SocketId != b.SocketId
                    || a.OffsetXRaw != b.OffsetXRaw
                    || a.OffsetYRaw != b.OffsetYRaw
                    || a.OffsetZRaw != b.OffsetZRaw)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreAuthorityPoseSnapshotsEqual(
            CharacterAuthorityPoseSnapshot left,
            CharacterAuthorityPoseSnapshot right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null
                || left.SchemaVersion != right.SchemaVersion
                || left.ActiveSecondaryCount != right.ActiveSecondaryCount
                || left.KnownRegionCount != right.KnownRegionCount
                || left.LastResolvedRegionId != right.LastResolvedRegionId
                || left.LastMatchSource != right.LastMatchSource
                || left.LastIssue != right.LastIssue)
            {
                return false;
            }

            CharacterAuthoritySecondarySnapshot[] leftEntries =
                left.SecondaryBindings ?? Array.Empty<CharacterAuthoritySecondarySnapshot>();
            CharacterAuthoritySecondarySnapshot[] rightEntries =
                right.SecondaryBindings ?? Array.Empty<CharacterAuthoritySecondarySnapshot>();
            if (leftEntries.Length != rightEntries.Length)
                return false;

            for (int i = 0; i < leftEntries.Length; i++)
            {
                CharacterAuthoritySecondarySnapshot a = leftEntries[i];
                CharacterAuthoritySecondarySnapshot b = rightEntries[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.BindingId != b.BindingId
                    || a.RegionId != b.RegionId
                    || a.ChannelId != b.ChannelId
                    || a.SocketId != b.SocketId
                    || a.OffsetXRaw != b.OffsetXRaw
                    || a.OffsetYRaw != b.OffsetYRaw
                    || a.OffsetZRaw != b.OffsetZRaw
                    || a.VelocityXRaw != b.VelocityXRaw
                    || a.VelocityYRaw != b.VelocityYRaw
                    || a.VelocityZRaw != b.VelocityZRaw
                    || a.PendingForceXRaw != b.PendingForceXRaw
                    || a.PendingForceYRaw != b.PendingForceYRaw
                    || a.PendingForceZRaw != b.PendingForceZRaw
                    || a.RemainingFrames != b.RemainingFrames
                    || a.StartFrame != b.StartFrame
                    || a.Active != b.Active)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreAuthorityPoseDefinitionsEqual(
            CharacterAuthorityPoseRigDefinition left,
            CharacterAuthorityPoseRigDefinition right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null
                || left.KnownSecondaryRegionCount != right.KnownSecondaryRegionCount)
            {
                return false;
            }

            IReadOnlyList<CharacterAuthoritySecondaryBinding> leftSecondary =
                left.SecondaryBindings;
            IReadOnlyList<CharacterAuthoritySecondaryBinding> rightSecondary =
                right.SecondaryBindings;
            if (leftSecondary.Count != rightSecondary.Count)
                return false;

            for (int i = 0; i < leftSecondary.Count; i++)
            {
                CharacterAuthoritySecondaryBinding a = leftSecondary[i];
                CharacterAuthoritySecondaryBinding b = rightSecondary[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.BindingId != b.BindingId
                    || a.RegionId != b.RegionId
                    || a.ChannelId != b.ChannelId
                    || a.SocketId != b.SocketId
                    || a.Stiffness != b.Stiffness
                    || a.DampingPerFrame != b.DampingPerFrame
                    || a.MaxOffset != b.MaxOffset
                    || a.StrengthScale != b.StrengthScale)
                {
                    return false;
                }
            }

            IReadOnlyList<CharacterAuthorityTwoBoneIkBinding> leftIk =
                left.TwoBoneIkBindings;
            IReadOnlyList<CharacterAuthorityTwoBoneIkBinding> rightIk =
                right.TwoBoneIkBindings;
            if (leftIk.Count != rightIk.Count)
                return false;

            for (int i = 0; i < leftIk.Count; i++)
            {
                CharacterAuthorityTwoBoneIkBinding a = leftIk[i];
                CharacterAuthorityTwoBoneIkBinding b = rightIk[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                        return false;
                    continue;
                }

                if (a.BindingId != b.BindingId
                    || a.RootChannelId != b.RootChannelId
                    || a.MidChannelId != b.MidChannelId
                    || a.TipChannelId != b.TipChannelId
                    || a.RootToMidLength != b.RootToMidLength
                    || a.MidToTipLength != b.MidToTipLength
                    || a.PoleDirection != b.PoleDirection
                    || a.ReachClamp != b.ReachClamp)
                {
                    return false;
                }
            }

            return true;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)System.Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }

    public readonly struct CharacterTestCharacterPhysicsComponent
    {
        public const float DefaultCapsuleRadiusMeters = 0.34f;
        public const float DefaultCapsuleHeightMeters = 1.86f;
        public const float DefaultMassKg = 80f;
        public const float MinimumRadiusMeters = 0.01f;
        public const float MinimumHeightMeters = 0.02f;
        public const float MinimumMassKg = 0.001f;
        public const int DefaultCapsuleRadiusMillimeters = 340;
        public const int DefaultCapsuleHeightMillimeters = 1860;
        public const int DefaultMassGrams = 80000;
        public const int MinimumRadiusMillimeters = 10;
        public const int MinimumHeightMillimeters = 20;
        public const int MinimumMassGrams = 1;

        public static readonly CharacterTestCharacterPhysicsComponent Default =
            FromDeterministic(
                DefaultCapsuleRadiusMillimeters,
                DefaultCapsuleHeightMillimeters,
                DefaultCapsuleHeightMillimeters / 2,
                DefaultMassGrams,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                blocksCharacters: true,
                canBePushed: true);

        public CharacterTestCharacterPhysicsComponent(
            float capsuleRadiusMeters,
            float capsuleHeightMeters,
            float capsuleCenterY,
            float massKg,
            float massScale = 1f,
            float radiusScale = 1f,
            float pushScale = 1f,
            bool blocksCharacters = true,
            bool canBePushed = true,
            Vector2 contactVelocity = default,
            Vector2 contactDisplacement = default,
            int contactCount = 0)
        {
            int capsuleRadiusMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(capsuleRadiusMeters);
            int capsuleHeightMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(capsuleHeightMeters);
            int capsuleCenterYMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(capsuleCenterY);
            int massGrams = CharacterTestDeterministicUnits.KilogramsToGrams(massKg);
            CapsuleRadiusMillimeters = capsuleRadiusMillimeters > 0
                ? capsuleRadiusMillimeters
                : DefaultCapsuleRadiusMillimeters;
            CapsuleHeightMillimeters = capsuleHeightMillimeters > 0
                ? capsuleHeightMillimeters
                : DefaultCapsuleHeightMillimeters;
            CapsuleCenterYMillimeters = capsuleCenterYMillimeters > 0
                ? capsuleCenterYMillimeters
                : CapsuleHeightMillimeters / 2;
            MassGrams = massGrams > 0 ? massGrams : DefaultMassGrams;
            MassScaleMilli = CharacterTestDeterministicUnits.ScaleToMilli(massScale);
            if (MassScaleMilli <= 0)
                MassScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale;
            RadiusScaleMilli = CharacterTestDeterministicUnits.ScaleToMilli(radiusScale);
            if (RadiusScaleMilli <= 0)
                RadiusScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale;
            PushScaleMilli = CharacterTestDeterministicUnits.ScaleToMilli(pushScale);
            BlocksCharacters = blocksCharacters;
            CanBePushed = canBePushed;
            ContactVelocityXMillimetersPerSecond =
                CharacterTestDeterministicUnits.MetersToMillimeters(contactVelocity.x);
            ContactVelocityYMillimetersPerSecond =
                CharacterTestDeterministicUnits.MetersToMillimeters(contactVelocity.y);
            ContactDisplacementXMillimeters =
                CharacterTestDeterministicUnits.MetersToMillimeters(contactDisplacement.x);
            ContactDisplacementYMillimeters =
                CharacterTestDeterministicUnits.MetersToMillimeters(contactDisplacement.y);
            ContactCount = contactCount < 0 ? 0 : contactCount;
        }

        private CharacterTestCharacterPhysicsComponent(
            int capsuleRadiusMillimeters,
            int capsuleHeightMillimeters,
            int capsuleCenterYMillimeters,
            int massGrams,
            int massScaleMilli,
            int radiusScaleMilli,
            int pushScaleMilli,
            bool blocksCharacters,
            bool canBePushed,
            int contactVelocityXMillimetersPerSecond,
            int contactVelocityYMillimetersPerSecond,
            int contactDisplacementXMillimeters,
            int contactDisplacementYMillimeters,
            int contactCount)
        {
            CapsuleRadiusMillimeters = capsuleRadiusMillimeters > 0
                ? capsuleRadiusMillimeters
                : DefaultCapsuleRadiusMillimeters;
            CapsuleHeightMillimeters = capsuleHeightMillimeters > 0
                ? capsuleHeightMillimeters
                : DefaultCapsuleHeightMillimeters;
            CapsuleCenterYMillimeters = capsuleCenterYMillimeters > 0
                ? capsuleCenterYMillimeters
                : CapsuleHeightMillimeters / 2;
            MassGrams = massGrams > 0 ? massGrams : DefaultMassGrams;
            MassScaleMilli = massScaleMilli > 0
                ? massScaleMilli
                : CharacterTestDeterministicUnits.ScaleFactorScale;
            RadiusScaleMilli = radiusScaleMilli > 0
                ? radiusScaleMilli
                : CharacterTestDeterministicUnits.ScaleFactorScale;
            PushScaleMilli = pushScaleMilli < 0 ? 0 : pushScaleMilli;
            BlocksCharacters = blocksCharacters;
            CanBePushed = canBePushed;
            ContactVelocityXMillimetersPerSecond = contactVelocityXMillimetersPerSecond;
            ContactVelocityYMillimetersPerSecond = contactVelocityYMillimetersPerSecond;
            ContactDisplacementXMillimeters = contactDisplacementXMillimeters;
            ContactDisplacementYMillimeters = contactDisplacementYMillimeters;
            ContactCount = contactCount < 0 ? 0 : contactCount;
        }

        public int CapsuleRadiusMillimeters { get; }
        public int CapsuleHeightMillimeters { get; }
        public int CapsuleCenterYMillimeters { get; }
        public int MassGrams { get; }
        public int MassScaleMilli { get; }
        public int RadiusScaleMilli { get; }
        public int PushScaleMilli { get; }
        public bool BlocksCharacters { get; }
        public bool CanBePushed { get; }
        public int ContactVelocityXMillimetersPerSecond { get; }
        public int ContactVelocityYMillimetersPerSecond { get; }
        public int ContactDisplacementXMillimeters { get; }
        public int ContactDisplacementYMillimeters { get; }
        public int ContactCount { get; }

        public float CapsuleRadiusMeters =>
            CharacterTestDeterministicUnits.MillimetersToMeters(CapsuleRadiusMillimeters);

        public float CapsuleHeightMeters =>
            CharacterTestDeterministicUnits.MillimetersToMeters(CapsuleHeightMillimeters);

        public float CapsuleCenterY =>
            CharacterTestDeterministicUnits.MillimetersToMeters(CapsuleCenterYMillimeters);

        public float MassKg =>
            CharacterTestDeterministicUnits.GramsToKilograms(MassGrams);

        public float MassScale =>
            CharacterTestDeterministicUnits.MilliToScale(MassScaleMilli);

        public float RadiusScale =>
            CharacterTestDeterministicUnits.MilliToScale(RadiusScaleMilli);

        public float PushScale =>
            CharacterTestDeterministicUnits.MilliToScale(PushScaleMilli);

        public Vector2 ContactVelocity => new Vector2(
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactVelocityXMillimetersPerSecond),
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactVelocityYMillimetersPerSecond));

        public Vector2 ContactDisplacement => new Vector2(
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactDisplacementXMillimeters),
            CharacterTestDeterministicUnits.MillimetersToMeters(ContactDisplacementYMillimeters));

        public bool IsConfigured => CapsuleRadiusMillimeters > 0
            && CapsuleHeightMillimeters > 0
            && MassGrams > 0;

        public float EffectiveRadiusMeters =>
            CharacterTestDeterministicUnits.MillimetersToMeters(EffectiveRadiusMillimeters);

        public float EffectiveHeightMeters =>
            CharacterTestDeterministicUnits.MillimetersToMeters(EffectiveHeightMillimeters);

        public float EffectiveHalfHeightMeters => EffectiveHeightMeters * 0.5f;

        public float EffectiveCenterY =>
            CapsuleCenterY > 0f ? CapsuleCenterY : EffectiveHalfHeightMeters;

        public float EffectiveMassKg =>
            CharacterTestDeterministicUnits.GramsToKilograms(EffectiveMassGrams);

        public float EffectivePushScale => PushScale < 0f ? 0f : PushScale;

        public int EffectiveRadiusMillimeters =>
            System.Math.Max(
                MinimumRadiusMillimeters,
                (int)System.Math.Round(
                    CapsuleRadiusMillimeters * (double)RadiusScaleMilli
                    / CharacterTestDeterministicUnits.ScaleFactorScale,
                    System.MidpointRounding.AwayFromZero));

        public int EffectiveHeightMillimeters =>
            System.Math.Max(MinimumHeightMillimeters, CapsuleHeightMillimeters);

        public int EffectiveMassGrams =>
            System.Math.Max(
                MinimumMassGrams,
                (int)System.Math.Round(
                    MassGrams * (double)MassScaleMilli
                    / CharacterTestDeterministicUnits.ScaleFactorScale,
                    System.MidpointRounding.AwayFromZero));

        public int EffectivePushScaleMilli => PushScaleMilli < 0 ? 0 : PushScaleMilli;

        public static CharacterTestCharacterPhysicsComponent FromDeterministic(
            int capsuleRadiusMillimeters,
            int capsuleHeightMillimeters,
            int capsuleCenterYMillimeters,
            int massGrams,
            int massScaleMilli,
            int radiusScaleMilli,
            int pushScaleMilli,
            bool blocksCharacters,
            bool canBePushed,
            int contactVelocityXMillimetersPerSecond = 0,
            int contactVelocityYMillimetersPerSecond = 0,
            int contactDisplacementXMillimeters = 0,
            int contactDisplacementYMillimeters = 0,
            int contactCount = 0)
        {
            return new CharacterTestCharacterPhysicsComponent(
                capsuleRadiusMillimeters,
                capsuleHeightMillimeters,
                capsuleCenterYMillimeters,
                massGrams,
                massScaleMilli,
                radiusScaleMilli,
                pushScaleMilli,
                blocksCharacters,
                canBePushed,
                contactVelocityXMillimetersPerSecond,
                contactVelocityYMillimetersPerSecond,
                contactDisplacementXMillimeters,
                contactDisplacementYMillimeters,
                contactCount);
        }

        public CharacterTestCharacterPhysicsComponent WithRuntimeModifiers(
            float massScale,
            float radiusScale,
            float pushScale,
            bool? blocksCharacters = null,
            bool? canBePushed = null)
        {
            return new CharacterTestCharacterPhysicsComponent(
                CapsuleRadiusMillimeters,
                CapsuleHeightMillimeters,
                CapsuleCenterYMillimeters,
                MassGrams,
                CharacterTestDeterministicUnits.ScaleToMilli(massScale),
                CharacterTestDeterministicUnits.ScaleToMilli(radiusScale),
                CharacterTestDeterministicUnits.ScaleToMilli(pushScale),
                blocksCharacters ?? BlocksCharacters,
                canBePushed ?? CanBePushed,
                ContactVelocityXMillimetersPerSecond,
                ContactVelocityYMillimetersPerSecond,
                ContactDisplacementXMillimeters,
                ContactDisplacementYMillimeters,
                ContactCount);
        }

        public CharacterTestCharacterPhysicsComponent WithContactState(
            Vector2 contactVelocity,
            Vector2 contactDisplacement,
            int contactCount)
        {
            return new CharacterTestCharacterPhysicsComponent(
                CapsuleRadiusMillimeters,
                CapsuleHeightMillimeters,
                CapsuleCenterYMillimeters,
                MassGrams,
                MassScaleMilli,
                RadiusScaleMilli,
                PushScaleMilli,
                BlocksCharacters,
                CanBePushed,
                CharacterTestDeterministicUnits.MetersToMillimeters(contactVelocity.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(contactVelocity.y),
                CharacterTestDeterministicUnits.MetersToMillimeters(contactDisplacement.x),
                CharacterTestDeterministicUnits.MetersToMillimeters(contactDisplacement.y),
                contactCount);
        }
    }

    internal enum CharacterTestColliderPostureSource
    {
        Standing = 0,
        ActionTimeline = 1,
        Lifecycle = 2,
        Presence = 3,
        ClearanceBlocked = 4
    }

    public readonly struct CharacterTestColliderPostureComponent
    {
        public const int StandingProfileId = CharacterTestAuthoredContentIds.D402_Id2;
        public const int CrouchProfileId = CharacterTestAuthoredContentIds.D402_Id3;
        public const int LowProfileId = CharacterTestAuthoredContentIds.D402_Id4;
        public const int CorpseProfileId = CharacterTestAuthoredContentIds.D402_Id5;
        public const int ReviveLowProfileId = CharacterTestAuthoredContentIds.D402_Id6;

        internal CharacterTestColliderPostureComponent(
            int profileId,
            int heightMillimeters,
            int radiusMillimeters,
            int centerYMillimeters,
            bool blocksCharacters,
            bool canBePushed,
            CharacterTestColliderPostureSource source,
            int priority,
            long sinceFrame,
            int actionProfileId = 0,
            CharacterActionCollisionProfileRestorePolicy actionRestorePolicy =
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
            int actionTransitionFrames = 0,
            CharacterActionId actionSourceActionId = default,
            int actionStableEventId = 0,
            bool isValid = true)
        {
            source = NormalizeSource(source);
            actionRestorePolicy = NormalizeRestorePolicy(actionRestorePolicy);

            ProfileId = NormalizeProfileId(profileId);
            HeightMillimeters = System.Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumHeightMillimeters,
                heightMillimeters);
            RadiusMillimeters = System.Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters,
                radiusMillimeters);
            CenterYMillimeters = centerYMillimeters > 0
                ? centerYMillimeters
                : HeightMillimeters / 2;
            BlocksCharacters = blocksCharacters;
            CanBePushed = canBePushed;
            Source = source;
            Priority = priority;
            SinceFrame = sinceFrame >= 0L ? sinceFrame : 0L;
            ActionProfileId = actionProfileId;
            ActionRestorePolicy = actionRestorePolicy;
            ActionTransitionFrames = actionTransitionFrames < 0 ? 0 : actionTransitionFrames;
            ActionSourceActionId = actionSourceActionId;
            ActionStableEventId = actionStableEventId;
            IsValid = isValid;
        }

        public int ProfileId { get; }
        public int HeightMillimeters { get; }
        public int RadiusMillimeters { get; }
        public int CenterYMillimeters { get; }
        public bool BlocksCharacters { get; }
        public bool CanBePushed { get; }
        internal CharacterTestColliderPostureSource Source { get; }
        public int Priority { get; }
        public long SinceFrame { get; }
        public int ActionProfileId { get; }
        public CharacterActionCollisionProfileRestorePolicy ActionRestorePolicy { get; }
        public int ActionTransitionFrames { get; }
        public CharacterActionId ActionSourceActionId { get; }
        public int ActionStableEventId { get; }
        public bool IsValid { get; }
        public bool HasActionOverride => ActionProfileId > 0;

        private static CharacterTestColliderPostureSource NormalizeSource(
            CharacterTestColliderPostureSource source)
        {
            switch (source)
            {
                case CharacterTestColliderPostureSource.Standing:
                case CharacterTestColliderPostureSource.ActionTimeline:
                case CharacterTestColliderPostureSource.Lifecycle:
                case CharacterTestColliderPostureSource.Presence:
                case CharacterTestColliderPostureSource.ClearanceBlocked:
                    return source;
                default:
                    return CharacterTestColliderPostureSource.Standing;
            }
        }

        private static CharacterActionCollisionProfileRestorePolicy NormalizeRestorePolicy(
            CharacterActionCollisionProfileRestorePolicy policy)
        {
            switch (policy)
            {
                case CharacterActionCollisionProfileRestorePolicy.None:
                case CharacterActionCollisionProfileRestorePolicy.OnActionEnd:
                    return policy;
                default:
                    return CharacterActionCollisionProfileRestorePolicy.OnActionEnd;
            }
        }

        public static CharacterTestColliderPostureComponent FromPhysics(
            CharacterTestCharacterPhysicsComponent physics,
            long frame)
        {
            physics = physics.IsConfigured ? physics : CharacterTestCharacterPhysicsComponent.Default;
            return new CharacterTestColliderPostureComponent(
                StandingProfileId,
                physics.EffectiveHeightMillimeters,
                physics.EffectiveRadiusMillimeters,
                physics.CapsuleCenterYMillimeters > 0
                    ? physics.CapsuleCenterYMillimeters
                    : physics.EffectiveHeightMillimeters / 2,
                physics.BlocksCharacters,
                physics.CanBePushed,
                CharacterTestColliderPostureSource.Standing,
                priority: 0,
                sinceFrame: frame);
        }

        internal CharacterTestColliderPostureComponent WithActionOverride(
            int profileId,
            CharacterActionCollisionProfileRestorePolicy restorePolicy,
            int transitionFrames,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestColliderPostureComponent(
                ProfileId,
                HeightMillimeters,
                RadiusMillimeters,
                CenterYMillimeters,
                BlocksCharacters,
                CanBePushed,
                Source,
                Priority,
                SinceFrame,
                profileId == 0 ? 0 : NormalizeProfileId(profileId),
                restorePolicy,
                transitionFrames,
                sourceActionId,
                stableEventId,
                IsValid);
        }

        internal CharacterTestColliderPostureComponent WithoutActionOverride()
        {
            return new CharacterTestColliderPostureComponent(
                ProfileId,
                HeightMillimeters,
                RadiusMillimeters,
                CenterYMillimeters,
                BlocksCharacters,
                CanBePushed,
                Source,
                Priority,
                SinceFrame,
                0,
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
                0,
                CharacterActionId.None,
                0,
                IsValid);
        }

        internal CharacterTestColliderPostureComponent WithResolved(
            int profileId,
            int heightMillimeters,
            int radiusMillimeters,
            int centerYMillimeters,
            bool blocksCharacters,
            bool canBePushed,
            CharacterTestColliderPostureSource source,
            int priority,
            long frame)
        {
            long sinceFrame = ProfileId == NormalizeProfileId(profileId)
                && HeightMillimeters == heightMillimeters
                && RadiusMillimeters == radiusMillimeters
                && CenterYMillimeters == centerYMillimeters
                && BlocksCharacters == blocksCharacters
                && CanBePushed == canBePushed
                && Source == source
                && Priority == priority
                    ? SinceFrame
                    : frame;
            return new CharacterTestColliderPostureComponent(
                profileId,
                heightMillimeters,
                radiusMillimeters,
                centerYMillimeters,
                blocksCharacters,
                canBePushed,
                source,
                priority,
                sinceFrame,
                ActionProfileId,
                ActionRestorePolicy,
                ActionTransitionFrames,
                ActionSourceActionId,
                ActionStableEventId,
                isValid: true);
        }

        private static int NormalizeProfileId(int profileId)
        {
            if (profileId < 0) throw new System.ArgumentOutOfRangeException(nameof(profileId));
            return profileId == 0 ? StandingProfileId : profileId;
        }
    }

    public enum CharacterTestCharacterLifeState
    {
        Alive = 0,
        Dying = 1,
        Dead = 2,
        Reviving = 3
    }

    public readonly struct CharacterTestCharacterCombatStateComponent
    {
        public const int DefaultMaxHealth = 100;

        public CharacterTestCharacterCombatStateComponent(
            int maxHealth,
            int health,
            int hitStunFramesRemaining,
            int lastDamage,
            long lastHitFrame,
            int lastHitSourceEntityId,
            int lastHitTraceId,
            int scatterPressure = 0,
            int maxScatterPressure = CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
            PressureBand scatterPressureBand = PressureBand.Stable,
            long lastCohesionBreakFrame = -1L,
            int brokenRecoveryDelayFrames = 0,
            CharacterTestCharacterLifeState lifeState = CharacterTestCharacterLifeState.Alive,
            long deathFrame = -1L,
            long reviveFrame = -1L,
            int lastDeathSourceEntityId = 0,
            int lastDeathTraceId = 0)
        {
            MaxHealth = NormalizeMaxHealth(maxHealth);
            Health = Clamp(health, 0, MaxHealth);
            HitStunFramesRemaining = hitStunFramesRemaining < 0 ? 0 : hitStunFramesRemaining;
            LastDamage = lastDamage < 0 ? 0 : lastDamage;
            LastHitFrame = lastHitFrame;
            LastHitSourceEntityId = lastHitSourceEntityId < 0 ? 0 : lastHitSourceEntityId;
            LastHitTraceId = lastHitTraceId < 0 ? 0 : lastHitTraceId;
            MaxScatterPressure = NormalizeMaxScatterPressure(maxScatterPressure);
            ScatterPressure = NormalizeScatterPressure(scatterPressure);
            ScatterPressureBand = NormalizePressureBand(scatterPressureBand);
            LastCohesionBreakFrame = NormalizeCohesionBreakFrame(lastCohesionBreakFrame);
            BrokenRecoveryDelayFrames = NormalizeBrokenRecoveryDelayFrames(brokenRecoveryDelayFrames);
            LifeState = NormalizeLifeState(lifeState, Health);
            DeathFrame = NormalizeLifecycleFrame(deathFrame);
            ReviveFrame = NormalizeLifecycleFrame(reviveFrame);
            LastDeathSourceEntityId = lastDeathSourceEntityId < 0 ? 0 : lastDeathSourceEntityId;
            LastDeathTraceId = lastDeathTraceId < 0 ? 0 : lastDeathTraceId;
        }

        public int MaxHealth { get; }
        public int Health { get; }
        public int HitStunFramesRemaining { get; }
        public int LastDamage { get; }
        public long LastHitFrame { get; }
        public int LastHitSourceEntityId { get; }
        public int LastHitTraceId { get; }
        public int ScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public PressureBand ScatterPressureBand { get; }
        public long LastCohesionBreakFrame { get; }
        public int BrokenRecoveryDelayFrames { get; }
        public CharacterTestCharacterLifeState LifeState { get; }
        public long DeathFrame { get; }
        public long ReviveFrame { get; }
        public int LastDeathSourceEntityId { get; }
        public int LastDeathTraceId { get; }
        public bool IsAlive => LifeState == CharacterTestCharacterLifeState.Alive && Health > 0;
        public bool IsDying => LifeState == CharacterTestCharacterLifeState.Dying;
        public bool IsDead => LifeState == CharacterTestCharacterLifeState.Dead;
        public bool IsReviving => LifeState == CharacterTestCharacterLifeState.Reviving;
        public bool CanReceiveCombatHit => IsAlive;
        public bool CanAcceptControlInput => IsAlive;
        public bool IsReacting => HitStunFramesRemaining > 0;
        public bool IsCohesionBroken => IsAlive && ScatterPressureBand == PressureBand.Broken;
        public bool IsCohesionBreakReacting => IsCohesionBroken && HitStunFramesRemaining > 0;

        public static CharacterTestCharacterCombatStateComponent CreateDefault()
        {
            return new CharacterTestCharacterCombatStateComponent(
                DefaultMaxHealth,
                DefaultMaxHealth,
                hitStunFramesRemaining: 0,
                lastDamage: 0,
                lastHitFrame: -1L,
                lastHitSourceEntityId: 0,
                lastHitTraceId: 0);
        }

        public CharacterTestCharacterCombatStateComponent ApplyHit(
            int damage,
            int staggerFrames,
            long frame,
            int sourceEntityId,
            int traceId)
        {
            int safeDamage = damage < 0 ? 0 : damage;
            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health - safeDamage,
                staggerFrames,
                safeDamage,
                frame,
                sourceEntityId,
                traceId,
                ScatterPressure,
                MaxScatterPressure,
                ScatterPressureBand,
                LastCohesionBreakFrame,
                BrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent ApplyHitReaction(
            int damage,
            int staggerFrames,
            long frame,
            int sourceEntityId,
            int traceId,
            int maxHealth,
            int health)
        {
            int safeDamage = damage < 0 ? 0 : damage;
            return new CharacterTestCharacterCombatStateComponent(
                maxHealth,
                health,
                System.Math.Max(HitStunFramesRemaining, staggerFrames),
                safeDamage,
                frame,
                sourceEntityId,
                traceId,
                ScatterPressure,
                MaxScatterPressure,
                ScatterPressureBand,
                LastCohesionBreakFrame,
                BrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent WithHealth(int maxHealth, int health)
        {
            int safeMaxHealth = NormalizeMaxHealth(maxHealth);
            int safeHealth = Clamp(health, 0, safeMaxHealth);
            if (MaxHealth == safeMaxHealth && Health == safeHealth)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                safeMaxHealth,
                safeHealth,
                HitStunFramesRemaining,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                ScatterPressure,
                MaxScatterPressure,
                ScatterPressureBand,
                LastCohesionBreakFrame,
                BrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        internal CharacterTestCharacterCombatStateComponent WithLifecycle(
            CharacterTestCharacterLifecycleComponent lifecycle)
        {
            if (LifeState == lifecycle.LifeState
                && DeathFrame == lifecycle.DeathFrame
                && ReviveFrame == lifecycle.ReviveFrame
                && LastDeathSourceEntityId == lifecycle.LastDeathSourceEntityId
                && LastDeathTraceId == lifecycle.LastDeathTraceId)
            {
                return this;
            }

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health,
                HitStunFramesRemaining,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                ScatterPressure,
                MaxScatterPressure,
                ScatterPressureBand,
                LastCohesionBreakFrame,
                BrokenRecoveryDelayFrames,
                lifecycle.LifeState,
                lifecycle.DeathFrame,
                lifecycle.ReviveFrame,
                lifecycle.LastDeathSourceEntityId,
                lifecycle.LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent WithScatterPressure(
            int scatterPressure,
            int maxScatterPressure,
            PressureBand scatterPressureBand)
        {
            return WithScatterPressure(
                scatterPressure,
                maxScatterPressure,
                scatterPressureBand,
                BrokenRecoveryDelayFrames,
                LastCohesionBreakFrame);
        }

        public CharacterTestCharacterCombatStateComponent WithScatterPressure(
            int scatterPressure,
            int maxScatterPressure,
            PressureBand scatterPressureBand,
            int brokenRecoveryDelayFrames,
            long lastCohesionBreakFrame)
        {
            int safeScatterPressure = NormalizeScatterPressure(scatterPressure);
            int safeMaxScatterPressure = NormalizeMaxScatterPressure(maxScatterPressure);
            PressureBand safeScatterPressureBand = NormalizePressureBand(scatterPressureBand);
            int safeBrokenRecoveryDelayFrames = NormalizeBrokenRecoveryDelayFrames(brokenRecoveryDelayFrames);
            long safeLastCohesionBreakFrame = NormalizeCohesionBreakFrame(lastCohesionBreakFrame);
            if (ScatterPressure == safeScatterPressure
                && MaxScatterPressure == safeMaxScatterPressure
                && ScatterPressureBand == safeScatterPressureBand
                && BrokenRecoveryDelayFrames == safeBrokenRecoveryDelayFrames
                && LastCohesionBreakFrame == safeLastCohesionBreakFrame)
            {
                return this;
            }

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health,
                HitStunFramesRemaining,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                safeScatterPressure,
                safeMaxScatterPressure,
                safeScatterPressureBand,
                safeLastCohesionBreakFrame,
                safeBrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent ApplyCohesionBreak(
            int reactionFrames,
            long frame)
        {
            if (!IsAlive)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health,
                System.Math.Max(HitStunFramesRemaining, reactionFrames),
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                ScatterPressure,
                MaxScatterPressure,
                PressureBand.Broken,
                frame,
                BrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent BeginDeath(
            long frame,
            int sourceEntityId,
            int traceId)
        {
            if (LifeState != CharacterTestCharacterLifeState.Alive)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                0,
                hitStunFramesRemaining: 0,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                scatterPressure: 0,
                MaxScatterPressure,
                PressureBand.Stable,
                lastCohesionBreakFrame: -1L,
                BrokenRecoveryDelayFrames,
                CharacterTestCharacterLifeState.Dying,
                frame,
                ReviveFrame,
                sourceEntityId,
                traceId);
        }

        public CharacterTestCharacterCombatStateComponent CompleteDeath(long frame)
        {
            if (LifeState != CharacterTestCharacterLifeState.Dying)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                0,
                hitStunFramesRemaining: 0,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                scatterPressure: 0,
                MaxScatterPressure,
                PressureBand.Stable,
                lastCohesionBreakFrame: -1L,
                BrokenRecoveryDelayFrames,
                CharacterTestCharacterLifeState.Dead,
                DeathFrame >= 0L ? DeathFrame : frame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        public CharacterTestCharacterCombatStateComponent BeginRevive(long frame, int health)
        {
            if (LifeState != CharacterTestCharacterLifeState.Dead)
                return this;

            int safeHealth = Clamp(health, 1, MaxHealth);
            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                safeHealth,
                hitStunFramesRemaining: 0,
                lastDamage: 0,
                lastHitFrame: -1L,
                lastHitSourceEntityId: 0,
                lastHitTraceId: 0,
                scatterPressure: 0,
                MaxScatterPressure,
                PressureBand.Stable,
                lastCohesionBreakFrame: -1L,
                BrokenRecoveryDelayFrames,
                CharacterTestCharacterLifeState.Reviving,
                deathFrame: -1L,
                frame,
                lastDeathSourceEntityId: 0,
                lastDeathTraceId: 0);
        }

        public CharacterTestCharacterCombatStateComponent CompleteRevive(long frame)
        {
            if (LifeState != CharacterTestCharacterLifeState.Reviving)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health,
                hitStunFramesRemaining: 0,
                lastDamage: 0,
                lastHitFrame: -1L,
                lastHitSourceEntityId: 0,
                lastHitTraceId: 0,
                scatterPressure: 0,
                MaxScatterPressure,
                PressureBand.Stable,
                lastCohesionBreakFrame: -1L,
                BrokenRecoveryDelayFrames,
                CharacterTestCharacterLifeState.Alive,
                deathFrame: -1L,
                frame,
                lastDeathSourceEntityId: 0,
                lastDeathTraceId: 0);
        }

        public bool TryGetCohesionBreakRecoveryProgress(long currentFrame, out float progress)
        {
            progress = 0f;
            if (!IsCohesionBroken
                || BrokenRecoveryDelayFrames <= 0
                || LastCohesionBreakFrame < 0L
                || currentFrame < LastCohesionBreakFrame)
            {
                return false;
            }

            long elapsedFrames = currentFrame - LastCohesionBreakFrame;
            if (elapsedFrames >= BrokenRecoveryDelayFrames)
            {
                progress = 1f;
                return true;
            }

            progress = (float)elapsedFrames / BrokenRecoveryDelayFrames;
            return true;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public CharacterTestCharacterCombatStateComponent Tick()
        {
            if (HitStunFramesRemaining <= 0)
                return this;

            return new CharacterTestCharacterCombatStateComponent(
                MaxHealth,
                Health,
                HitStunFramesRemaining - 1,
                LastDamage,
                LastHitFrame,
                LastHitSourceEntityId,
                LastHitTraceId,
                ScatterPressure,
                MaxScatterPressure,
                ScatterPressureBand,
                LastCohesionBreakFrame,
                BrokenRecoveryDelayFrames,
                LifeState,
                DeathFrame,
                ReviveFrame,
                LastDeathSourceEntityId,
                LastDeathTraceId);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static int NormalizeMaxHealth(int maxHealth)
        {
            return maxHealth <= 0 ? 1 : maxHealth;
        }

        private static int NormalizeScatterPressure(int scatterPressure)
        {
            return scatterPressure < 0 ? 0 : scatterPressure;
        }

        private static int NormalizeMaxScatterPressure(int maxScatterPressure)
        {
            return maxScatterPressure <= 0
                ? CharacterTestCharacterAttributeIds.DefaultScatterPressureMax
                : maxScatterPressure;
        }

        private static PressureBand NormalizePressureBand(PressureBand scatterPressureBand)
        {
            int value = (int)scatterPressureBand;
            return value >= (int)PressureBand.Stable && value <= (int)PressureBand.Broken
                ? scatterPressureBand
                : PressureBand.Stable;
        }

        private static long NormalizeCohesionBreakFrame(long frame)
        {
            return frame < -1L ? -1L : frame;
        }

        private static int NormalizeBrokenRecoveryDelayFrames(int frames)
        {
            return frames < 0 ? 0 : frames;
        }

        private static long NormalizeLifecycleFrame(long frame)
        {
            return frame < -1L ? -1L : frame;
        }

        private static CharacterTestCharacterLifeState NormalizeLifeState(
            CharacterTestCharacterLifeState lifeState,
            int health)
        {
            int value = (int)lifeState;
            if (value < (int)CharacterTestCharacterLifeState.Alive
                || value > (int)CharacterTestCharacterLifeState.Reviving)
            {
                return health > 0
                    ? CharacterTestCharacterLifeState.Alive
                    : CharacterTestCharacterLifeState.Dead;
            }

            return health <= 0 && lifeState == CharacterTestCharacterLifeState.Alive
                ? CharacterTestCharacterLifeState.Dead
                : lifeState;
        }
    }
}
