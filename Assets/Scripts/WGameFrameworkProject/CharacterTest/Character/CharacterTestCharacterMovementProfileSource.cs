using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterMovementProfileRuntime
    {
        public CharacterTestCharacterMovementProfileRuntime(
            CharacterMovementProfileConfig profile,
            float maxFallSpeed,
            float jumpBufferSeconds,
            float coyoteTimeSeconds,
            float jumpReleaseVelocityMultiplier,
            float apexGravityMultiplier,
            float fallGravityMultiplier,
            bool usedFallback,
            string sourceDescription)
        {
            Profile = profile ?? CharacterTestCharacterMovementProfileSource.DefaultProfile;
            MaxFallSpeed = maxFallSpeed < 0f ? maxFallSpeed : -Math.Abs(maxFallSpeed);
            JumpBufferSeconds = Math.Max(0f, jumpBufferSeconds);
            CoyoteTimeSeconds = Math.Max(0f, coyoteTimeSeconds);
            JumpReleaseVelocityMultiplier = Clamp01(jumpReleaseVelocityMultiplier);
            ApexGravityMultiplier = Math.Max(0.01f, apexGravityMultiplier);
            FallGravityMultiplier = Math.Max(0.01f, fallGravityMultiplier);
            UsedFallback = usedFallback;
            SourceDescription = sourceDescription ?? string.Empty;
            WalkSpeedMillimetersPerSecond =
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(Profile.WalkSpeed);
            RunSpeedMillimetersPerSecond =
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(Profile.RunSpeed);
            AccelerationMillimetersPerSecondSquared =
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(Profile.Acceleration);
            DecelerationMillimetersPerSecondSquared =
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(Profile.Deceleration);
            TurnSpeedMilliDegreesPerSecond =
                CharacterTestDeterministicUnits.DegreesToMilliDegreesUnnormalized(Profile.TurnSpeed);
            GroundFrictionMillimetersPerSecondSquared =
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(Profile.GroundFriction);
            AirControlMilli = CharacterTestDeterministicUnits.ScaleToMilli(Profile.AirControl);
            GravityMillimetersPerSecondSquared =
                CharacterTestDeterministicUnits.AccelerationToMillimetersPerSecondSquared(Profile.Gravity);
            UpwardMotionGravityScaleMilli = Profile.UpwardMotionGravityScaleMilli;
            JumpImpulseMillimetersPerSecond =
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(Profile.JumpImpulse);
            SlopeLimitMilliDegrees =
                CharacterTestDeterministicUnits.DegreesToMilliDegrees(Profile.SlopeLimitDegrees);
            MaxFallSpeedMillimetersPerSecond =
                CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(MaxFallSpeed);
            JumpReleaseVelocityMultiplierMilli =
                CharacterTestDeterministicUnits.ScaleToMilli(JumpReleaseVelocityMultiplier);
            ApexGravityMultiplierMilli =
                CharacterTestDeterministicUnits.ScaleToMilli(ApexGravityMultiplier);
            FallGravityMultiplierMilli =
                CharacterTestDeterministicUnits.ScaleToMilli(FallGravityMultiplier);
        }

        public CharacterMovementProfileConfig Profile { get; }
        public float MaxFallSpeed { get; }
        public float JumpBufferSeconds { get; }
        public float CoyoteTimeSeconds { get; }
        public float JumpReleaseVelocityMultiplier { get; }
        public float ApexGravityMultiplier { get; }
        public float FallGravityMultiplier { get; }
        public bool UsedFallback { get; }
        public string SourceDescription { get; }
        public int WalkSpeedMillimetersPerSecond { get; }
        public int RunSpeedMillimetersPerSecond { get; }
        public int AccelerationMillimetersPerSecondSquared { get; }
        public int DecelerationMillimetersPerSecondSquared { get; }
        public int TurnSpeedMilliDegreesPerSecond { get; }
        public int GroundFrictionMillimetersPerSecondSquared { get; }
        public int AirControlMilli { get; }
        public int GravityMillimetersPerSecondSquared { get; }
        public int UpwardMotionGravityScaleMilli { get; }
        public int JumpImpulseMillimetersPerSecond { get; }
        public int SlopeLimitMilliDegrees { get; }
        public int MaxFallSpeedMillimetersPerSecond { get; }
        public int JumpReleaseVelocityMultiplierMilli { get; }
        public int ApexGravityMultiplierMilli { get; }
        public int FallGravityMultiplierMilli { get; }

        public string StableId => Profile != null ? Profile.StableId : string.Empty;
        public int Id => Profile?.Id ?? 0;

        public int GetJumpBufferFrames(int tickRate)
        {
            return CharacterTestDeterministicUnits.SecondsToFrames(JumpBufferSeconds, tickRate);
        }

        public int GetCoyoteTimeFrames(int tickRate)
        {
            return CharacterTestDeterministicUnits.SecondsToFrames(CoyoteTimeSeconds, tickRate);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }

    public static class CharacterTestCharacterMovementProfileSource
    {
        public const string ProfilesFileName = "character_movement_profiles.json";
        public const int DefaultProfileId = CharacterTestAuthoredContentIds.D402_Id1;

        private const string EmbeddedSourceDescription = "CharacterTest/embedded/character_movement_profiles.json";

        public static readonly CharacterMovementProfileConfig DefaultProfile =
            new CharacterMovementProfileConfig(
                DefaultProfileId,
                CharacterMovementMode.Run,
                walkSpeed: 2.5f,
                runSpeed: 4f,
                acceleration: 24f,
                deceleration: 28f,
                turnSpeed: 720f,
                groundFriction: 10f,
                airControl: 0.35f,
                gravity: -30f,
                jumpImpulse: 8f,
                slopeLimitDegrees: 45f,
                locomotionBlendId: "locomotion.move", stableId: "movement.default");

        public static CharacterTestCharacterMovementProfileRuntime Load(
            CharacterTestCharacterActionConfigSourceOptions options,
            CharacterActionSetConfig actionSet)
        {
            int requestedProfileId = actionSet != null && actionSet.MovementProfileId > 0
                ? actionSet.MovementProfileId
                : DefaultProfileId;

            string[] candidateDirectories = options != null
                ? options.CandidateDirectories
                : CharacterTestCharacterActionConfigSource.CreateDefaultCandidateDirectories();

            for (int i = 0; i < candidateDirectories.Length; i++)
            {
                string directory = candidateDirectories[i];
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                string path = Path.Combine(directory, ProfilesFileName);
                if (!File.Exists(path))
                    continue;

                if (TryLoadFromFile(path, requestedProfileId, out CharacterTestCharacterMovementProfileRuntime runtime))
                    return runtime;
            }

            return CreateFallback(requestedProfileId, EmbeddedSourceDescription);
        }

        private static bool TryLoadFromFile(
            string path,
            int requestedProfileId,
            out CharacterTestCharacterMovementProfileRuntime runtime)
        {
            runtime = default;
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                JArray profiles = root["profiles"] as JArray;
                if (profiles == null)
                    return false;

                for (int i = 0; i < profiles.Count; i++)
                {
                    JObject item = profiles[i] as JObject;
                    if (item == null)
                        continue;

                    if (item["id"]?.Type != JTokenType.Integer)
                        throw new InvalidDataException("Movement profile requires an authored numeric id.");
                    int id = item["id"].Value<int>();
                    if (!CharacterTestAuthoredContentIds.IsActive(402, id))
                        throw new InvalidDataException("Unknown or retired movement profile identity.");
                    if (id != requestedProfileId)
                        continue;

                    CharacterMovementProfileConfig profile = new CharacterMovementProfileConfig(
                        id,
                        ReadEnum(item, "defaultMode", DefaultProfile.DefaultMode),
                        ReadFloat(item, "walkSpeed", DefaultProfile.WalkSpeed),
                        ReadFloat(item, "runSpeed", DefaultProfile.RunSpeed),
                        ReadFloat(item, "acceleration", DefaultProfile.Acceleration),
                        ReadFloat(item, "deceleration", DefaultProfile.Deceleration),
                        ReadFloat(item, "turnSpeed", DefaultProfile.TurnSpeed),
                        ReadFloat(item, "groundFriction", DefaultProfile.GroundFriction),
                        ReadFloat(item, "airControl", DefaultProfile.AirControl),
                        ReadFloat(item, "gravity", DefaultProfile.Gravity),
                        ReadFloat(item, "jumpImpulse", DefaultProfile.JumpImpulse),
                        ReadFloat(item, "slopeLimitDegrees", DefaultProfile.SlopeLimitDegrees),
                        ReadString(item, "locomotionBlendId", DefaultProfile.LocomotionBlendId),
                        stableId: ReadString(item, "stableId", string.Empty),
                        upwardGravityReference: ReadFloat(
                            item, "upwardGravityReference", DefaultProfile.UpwardGravityReference));

                    runtime = new CharacterTestCharacterMovementProfileRuntime(
                        profile,
                        ReadFloat(item, "maxFallSpeed", -50f),
                        ReadFloat(item, "jumpBufferSeconds", 0.12f),
                        ReadFloat(item, "coyoteTimeSeconds", 0.1f),
                        ReadFloat(item, "jumpReleaseVelocityMultiplier", 0.45f),
                        ReadFloat(item, "apexGravityMultiplier", 0.75f),
                        ReadFloat(item, "fallGravityMultiplier", 1.35f),
                        usedFallback: false,
                        sourceDescription: path.Replace('\\', '/'));
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static CharacterTestCharacterMovementProfileRuntime CreateFallback(
            int requestedProfileId,
            string sourceDescription)
        {
            if (requestedProfileId != DefaultProfileId)
                throw new InvalidDataException("Requested movement profile is not present in authored content: " + requestedProfileId);
            CharacterMovementProfileConfig profile = DefaultProfile;

            return new CharacterTestCharacterMovementProfileRuntime(
                profile,
                maxFallSpeed: -50f,
                jumpBufferSeconds: 0.12f,
                coyoteTimeSeconds: 0.1f,
                jumpReleaseVelocityMultiplier: 0.45f,
                apexGravityMultiplier: 0.75f,
                fallGravityMultiplier: 1.35f,
                usedFallback: true,
                sourceDescription: sourceDescription);
        }

        private static string ReadString(JObject item, string name, string fallback)
        {
            JToken token = item[name];
            return token != null && token.Type != JTokenType.Null
                ? token.ToString()
                : fallback;
        }

        private static float ReadFloat(JObject item, string name, float fallback)
        {
            JToken token = item[name];
            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            if (float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;

            return fallback;
        }

        private static CharacterMovementMode ReadEnum(
            JObject item,
            string name,
            CharacterMovementMode fallback)
        {
            string value = ReadString(item, name, fallback.ToString());
            return Enum.TryParse(value, ignoreCase: true, out CharacterMovementMode mode)
                && Enum.IsDefined(typeof(CharacterMovementMode), mode)
                    ? mode
                    : fallback;
        }
    }
}
