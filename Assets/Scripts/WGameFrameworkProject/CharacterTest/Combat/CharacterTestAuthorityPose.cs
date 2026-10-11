using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestAuthorityPoseDefinitionSource
    {
        private static readonly CharacterAuthorityPoseRigDefinition DefaultDefinition = Create();

        public static CharacterAuthorityPoseRigDefinition CreateDefaultDefinition()
        {
            return DefaultDefinition;
        }

        private static CharacterAuthorityPoseRigDefinition Create()
        {
            Fix64 stiffness = Fix64.FromRatio(22, 100);
            Fix64 damping = Fix64.FromRatio(58, 100);
            Fix64 strengthScale = Fix64.FromRatio(9, 1000);
            return new CharacterAuthorityPoseRigDefinition(
                new[]
                {
                    Binding(CharacterTestPoseIds.Id18, CharacterAuthorityPoseRegionIds.Default, CharacterTestPoseIds.Id11, CharacterTestPoseIds.Id12, stiffness, damping, Fix64.FromRatio(4, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id26, CharacterTestPoseIds.Id10, CharacterTestPoseIds.Id11, 0, stiffness, damping, Fix64.FromRatio(35, 1000), strengthScale),
                    Binding(CharacterTestPoseIds.Id19, CharacterTestPoseIds.Id3, CharacterTestPoseIds.Id3, 0, stiffness, damping, Fix64.FromRatio(28, 1000), strengthScale),
                    Binding(CharacterTestPoseIds.Id20, CharacterTestPoseIds.Id4, CharacterTestPoseIds.Id4, 0, stiffness, damping, Fix64.FromRatio(3, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id22, CharacterTestPoseIds.Id6, CharacterTestPoseIds.Id6, CharacterTestPoseIds.Id12, stiffness, damping, Fix64.FromRatio(3, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id21, CharacterTestPoseIds.Id5, CharacterTestPoseIds.Id5, 0, stiffness, damping, Fix64.FromRatio(3, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id23, CharacterTestPoseIds.Id7, CharacterTestPoseIds.Id7, 0, stiffness, damping, Fix64.FromRatio(3, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id17, CharacterTestPoseIds.Id2, CharacterTestPoseIds.Id2, 0, stiffness, damping, Fix64.FromRatio(35, 1000), strengthScale),
                    Binding(CharacterTestPoseIds.Id24, CharacterTestPoseIds.Id8, CharacterTestPoseIds.Id8, 0, stiffness, damping, Fix64.FromRatio(3, 100), strengthScale),
                    Binding(CharacterTestPoseIds.Id25, CharacterTestPoseIds.Id9, CharacterTestPoseIds.Id9, 0, stiffness, damping, Fix64.FromRatio(4, 100), strengthScale)
                },
                new[]
                {
                    new CharacterAuthorityTwoBoneIkBinding(
                        CharacterTestPoseIds.Id28,
                        CharacterTestPoseIds.Id13,
                        CharacterTestPoseIds.Id14,
                        CharacterTestPoseIds.Id6,
                        Fix64.FromRatio(32, 100),
                        Fix64.FromRatio(29, 100),
                        new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                        Fix64.FromRatio(61, 100)),
                    new CharacterAuthorityTwoBoneIkBinding(
                        CharacterTestPoseIds.Id27,
                        CharacterTestPoseIds.Id15,
                        CharacterTestPoseIds.Id16,
                        CharacterTestPoseIds.Id4,
                        Fix64.FromRatio(32, 100),
                        Fix64.FromRatio(29, 100),
                        new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                        Fix64.FromRatio(61, 100))
                });
        }

        private static CharacterAuthoritySecondaryBinding Binding(
            int bindingId,
            int regionId,
            int channelId,
            int socketId,
            Fix64 stiffness,
            Fix64 damping,
            Fix64 maxOffset,
            Fix64 strengthScale)
        {
            return new CharacterAuthoritySecondaryBinding(
                bindingId,
                regionId,
                channelId,
                socketId,
                stiffness,
                damping,
                maxOffset,
                strengthScale);
        }
    }

    public readonly struct CharacterTestAuthorityPosePreviewRequest
    {
        public const int SchemaVersion = 1;

        public CharacterTestAuthorityPosePreviewRequest(
            int targetEntityId,
            CharacterTestProceduralEffectPreviewId effectId,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId;
            EffectId = effectId;
            SchemaVersionValue = schemaVersion;
        }

        public int TargetEntityId { get; }
        public CharacterTestProceduralEffectPreviewId EffectId { get; }
        public int SchemaVersionValue { get; }
    }

    internal static class CharacterTestAuthorityPosePreviewMapper
    {
        public static bool TryCreateRequest(
            CharacterTestProceduralEffectPreviewId effectId,
            CharacterTestCharacterEntity target,
            string triggerName,
            out CharacterAuthorityPoseImpulseRequest request)
        {
            request = default;
            if (target == null || effectId == CharacterTestProceduralEffectPreviewId.None)
                return false;

            int regionId;
            FixVector3 direction;
            Fix64 strength;
            int durationFrames;
            switch (effectId)
            {
                case CharacterTestProceduralEffectPreviewId.LightSecondary:
                    regionId = CharacterTestPoseIds.Id10;
                    direction = ResolveForward(target);
                    strength = Fix64.FromRatio(32, 10);
                    durationFrames = 5;
                    break;
                case CharacterTestProceduralEffectPreviewId.HeavySecondary:
                    regionId = CharacterTestPoseIds.Id10;
                    direction = ResolveForward(target);
                    strength = Fix64.FromRatio(55, 10);
                    durationFrames = 7;
                    break;
                case CharacterTestProceduralEffectPreviewId.FullRigPulse:
                    regionId = CharacterTestPoseIds.Id3;
                    direction = ResolveForward(target);
                    strength = Fix64.FromRatio(42, 10);
                    durationFrames = 5;
                    break;
                case CharacterTestProceduralEffectPreviewId.SideSecondary:
                    regionId = CharacterTestPoseIds.Id6;
                    direction = ResolveRight(target);
                    strength = Fix64.FromRatio(38, 10);
                    durationFrames = 5;
                    break;
                default:
                    return false;
            }

            request = new CharacterAuthorityPoseImpulseRequest(
                regionId,
                direction,
                strength,
                durationFrames,
                new RuntimeTraceIdentity(1001, target.Id.Value, (int)effectId, 0),
                CharacterAuthorityPoseMatchSource.PreviewCommand,
                CharacterAuthorityPoseIssue.None);
            return request.HasEffect;
        }

        private static FixVector3 ResolveForward(CharacterTestCharacterEntity entity)
        {
            double yaw = entity.Transform.YawDegrees * Math.PI / 180.0d;
            return new FixVector3(
                ToFix64(Math.Sin(yaw)),
                Fix64.Zero,
                ToFix64(Math.Cos(yaw)));
        }

        private static FixVector3 ResolveRight(CharacterTestCharacterEntity entity)
        {
            double yaw = entity.Transform.YawDegrees * Math.PI / 180.0d;
            return new FixVector3(
                ToFix64(Math.Cos(yaw)),
                Fix64.Zero,
                ToFix64(-Math.Sin(yaw)));
        }

        private static Fix64 ToFix64(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    // Catalog domain 400. Values are persisted and never recycled.
    internal static class CharacterTestPoseIds
    {
        internal const int Id1 = 1; // default
        internal const int Id2 = 2; // core
        internal const int Id3 = 3; // head
        internal const int Id4 = 4; // left_hand
        internal const int Id5 = 5; // left_leg
        internal const int Id6 = 6; // right_hand
        internal const int Id7 = 7; // right_leg
        internal const int Id8 = 8; // shell
        internal const int Id9 = 9; // tail
        internal const int Id10 = 10; // torso
        internal const int Id11 = 11; // spine
        internal const int Id12 = 12; // mainHand
        internal const int Id13 = 13; // right_upper_arm
        internal const int Id14 = 14; // right_lower_arm
        internal const int Id15 = 15; // left_upper_arm
        internal const int Id16 = 16; // left_lower_arm
        internal const int Id17 = 17; // region:core
        internal const int Id18 = 18; // region:default
        internal const int Id19 = 19; // region:head
        internal const int Id20 = 20; // region:left_hand
        internal const int Id21 = 21; // region:left_leg
        internal const int Id22 = 22; // region:right_hand
        internal const int Id23 = 23; // region:right_leg
        internal const int Id24 = 24; // region:shell
        internal const int Id25 = 25; // region:tail
        internal const int Id26 = 26; // region:torso
        internal const int Id27 = 27; // ik:left_arm
        internal const int Id28 = 28; // ik:right_arm
    }
}
