namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeCommandPayloadCodec
    {
        public const int TargetLockToggleModeLegacy = 0;
        public const int TargetLockToggleModeViewSwitch = 1;

        public static int PackCoordinatePair(float x, float z)
        {
            return PackCoordinatePairMillimeters(
                CharacterTestDeterministicUnits.MetersToMillimeters(x),
                CharacterTestDeterministicUnits.MetersToMillimeters(z));
        }

        public static int PackCoordinatePairMillimeters(int xMillimeters, int zMillimeters)
        {
            ushort packedX = unchecked((ushort)EncodeCoordinateMillimeters(xMillimeters));
            ushort packedZ = unchecked((ushort)EncodeCoordinateMillimeters(zMillimeters));
            return packedX | (packedZ << 16);
        }

        public static void UnpackCoordinatePair(int value, out float x, out float z)
        {
            UnpackCoordinatePairMillimeters(value, out int xMillimeters, out int zMillimeters);
            x = CharacterTestDeterministicUnits.MillimetersToMeters(xMillimeters);
            z = CharacterTestDeterministicUnits.MillimetersToMeters(zMillimeters);
        }

        public static void UnpackCoordinatePairMillimeters(
            int value,
            out int xMillimeters,
            out int zMillimeters)
        {
            xMillimeters = DecodeCoordinateMillimeters(unchecked((short)(value & 0xffff)));
            zMillimeters = DecodeCoordinateMillimeters(unchecked((short)((value >> 16) & 0xffff)));
        }

        public static int PackYAndControlOwner(float y, CharacterTestCharacterControlOwner owner)
        {
            return PackYMillimetersAndControlOwner(
                CharacterTestDeterministicUnits.MetersToMillimeters(y),
                owner);
        }

        public static int PackYMillimetersAndControlOwner(
            int yMillimeters,
            CharacterTestCharacterControlOwner owner)
        {
            ushort packedY = unchecked((ushort)EncodeCoordinateMillimeters(yMillimeters));
            int packedOwner = ((int)owner & 0xff) << 16;
            return packedY | packedOwner;
        }

        public static void UnpackYAndControlOwner(
            int value,
            out float y,
            out CharacterTestCharacterControlOwner owner)
        {
            UnpackYMillimetersAndControlOwner(value, out int yMillimeters, out owner);
            y = CharacterTestDeterministicUnits.MillimetersToMeters(yMillimeters);
        }

        public static void UnpackYMillimetersAndControlOwner(
            int value,
            out int yMillimeters,
            out CharacterTestCharacterControlOwner owner)
        {
            yMillimeters = DecodeCoordinateMillimeters(unchecked((short)(value & 0xffff)));
            owner = (CharacterTestCharacterControlOwner)((value >> 16) & 0xff);
        }

        public static int PackSpawnMetadata(int teamId, int runtimeAiPlannerProfileSlot)
        {
            int safeTeamId = teamId < 0 ? 0 : teamId;
            int safeSlot = runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
            return (safeTeamId & 0xffff) | ((safeSlot & 0xffff) << 16);
        }

        public static void UnpackSpawnMetadata(
            int value,
            out int teamId,
            out int runtimeAiPlannerProfileSlot)
        {
            teamId = value & 0xffff;
            runtimeAiPlannerProfileSlot = (value >> 16) & 0xffff;
        }

        public static bool IsKnownControlOwner(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.None
                || owner == CharacterTestCharacterControlOwner.LocalPlayer
                || owner == CharacterTestCharacterControlOwner.RemotePlayer
                || owner == CharacterTestCharacterControlOwner.Scripted
                || owner == CharacterTestCharacterControlOwner.AiControlled;
        }

        public static int PackControlMovePair(float x, float y)
        {
            return PackControlMovePair(
                CharacterTestDeterministicUnits.AxisToMilli(x),
                CharacterTestDeterministicUnits.AxisToMilli(y));
        }

        public static int PackTargetLockLookDirection(float x, float y)
        {
            return PackControlMovePair(x, y);
        }

        public static int PackControlMovePair(short xMilli, short yMilli)
        {
            ushort packedX = unchecked((ushort)EncodeControlMoveAxisMilli(xMilli));
            ushort packedY = unchecked((ushort)EncodeControlMoveAxisMilli(yMilli));
            return packedX | (packedY << 16);
        }

        public static void UnpackControlMovePair(int value, out float x, out float y)
        {
            UnpackControlMovePairMilli(value, out short xMilli, out short yMilli);
            x = CharacterTestDeterministicUnits.MilliToAxis(xMilli);
            y = CharacterTestDeterministicUnits.MilliToAxis(yMilli);
        }

        public static void UnpackControlMovePairMilli(int value, out short xMilli, out short yMilli)
        {
            xMilli = DecodeControlMoveAxisMilli(unchecked((short)(value & 0xffff)));
            yMilli = DecodeControlMoveAxisMilli(unchecked((short)((value >> 16) & 0xffff)));
        }

        public static int PackControlIntentFlags(
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed,
            bool attackSecondaryPressed = false,
            bool debugSecondaryPressed = false,
            bool debugPrimaryPressed = false,
            int skillSlotPressedMask = 0,
            bool dodgePressed = false,
            int movementSpeedScaleMilli = CharacterTestControlIntent.DefaultMovementSpeedScaleMilli)
        {
            int flags = 0;
            if (jumpPressed)
                flags |= CharacterTestControlIntent.JumpPressedFlag;
            if (jumpHeld)
                flags |= CharacterTestControlIntent.JumpHeldFlag;
            if (jumpReleased)
                flags |= CharacterTestControlIntent.JumpReleasedFlag;
            if (attackPrimaryPressed)
                flags |= CharacterTestControlIntent.AttackPrimaryPressedFlag;
            if (attackSecondaryPressed)
                flags |= CharacterTestControlIntent.AttackSecondaryPressedFlag;
            if (debugPrimaryPressed)
                flags |= CharacterTestControlIntent.DebugPrimaryPressedFlag;
            if (debugSecondaryPressed)
                flags |= CharacterTestControlIntent.DebugSecondaryPressedFlag;
            if (dodgePressed)
                flags |= CharacterTestControlIntent.DodgePressedFlag;
            flags |= (skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits)
                << CharacterTestControlIntent.SkillSlotPressedMaskShift;
            int normalizedSpeedScale = CharacterTestControlIntent.NormalizeMovementSpeedScaleMilli(
                movementSpeedScaleMilli);
            int encodedSpeedPercent = normalizedSpeedScale >= CharacterTestControlIntent.DefaultMovementSpeedScaleMilli
                ? 0
                : (normalizedSpeedScale + 5) / 10;
            flags |= (encodedSpeedPercent & CharacterTestControlIntent.MovementSpeedPercentBits)
                << CharacterTestControlIntent.MovementSpeedPercentShift;
            return flags;
        }

        public static int UnpackControlIntentMovementSpeedScaleMilli(int metadata)
        {
            int schemaVersion = UnpackControlIntentSchemaVersion(metadata);
            if (schemaVersion < CharacterTestControlIntent.MovementSpeedScaleSchemaVersion)
                return CharacterTestControlIntent.DefaultMovementSpeedScaleMilli;

            int encodedPercent = (UnpackControlIntentFlags(metadata)
                    >> CharacterTestControlIntent.MovementSpeedPercentShift)
                & CharacterTestControlIntent.MovementSpeedPercentBits;
            return encodedPercent == 0
                ? CharacterTestControlIntent.DefaultMovementSpeedScaleMilli
                : encodedPercent * 10;
        }

        public static bool AreControlIntentFlagsKnown(int flags)
        {
            return (flags & ~CharacterTestControlIntent.KnownFlags) == 0;
        }

        public static int PackControlIntentMetadata(int flags, int schemaVersion)
        {
            if (schemaVersion >= CharacterTestControlIntent.DodgeSchemaVersion)
            {
                int extendedFlags = (flags >> 16) & 0xff;
                return (schemaVersion << 24) | (extendedFlags << 16) | (flags & 0xffff);
            }

            return (schemaVersion << 16) | (flags & 0xffff);
        }

        public static int UnpackControlIntentSchemaVersion(int metadata)
        {
            int upper = metadata >> 16;
            int extendedSchemaVersion = upper >> 8;
            if (extendedSchemaVersion >= CharacterTestControlIntent.DodgeSchemaVersion
                && extendedSchemaVersion <= CharacterTestControlIntent.SchemaVersion)
            {
                return extendedSchemaVersion;
            }

            return upper;
        }

        public static int UnpackControlIntentFlags(int metadata)
        {
            int schemaVersion = UnpackControlIntentSchemaVersion(metadata);
            if (schemaVersion >= CharacterTestControlIntent.DodgeSchemaVersion
                && schemaVersion <= CharacterTestControlIntent.SchemaVersion)
            {
                int extendedFlags = (metadata >> 16) & 0xff;
                return (extendedFlags << 16) | (metadata & 0xffff);
            }

            return metadata & 0xffff;
        }

        private static short EncodeCoordinateMillimeters(int millimeters)
        {
            int centimeters = CharacterTestDeterministicUnits.MillimetersToWireCentimeters(millimeters);
            if (centimeters < short.MinValue)
                return short.MinValue;
            if (centimeters > short.MaxValue)
                return short.MaxValue;

            return (short)centimeters;
        }

        private static int DecodeCoordinateMillimeters(short value)
        {
            return CharacterTestDeterministicUnits.WireCentimetersToMillimeters(value);
        }

        private static short EncodeControlMoveAxisMilli(short value)
        {
            if (value < CharacterTestDeterministicUnits.MinInputAxisMilli)
                return CharacterTestDeterministicUnits.MinInputAxisMilli;
            if (value > CharacterTestDeterministicUnits.MaxInputAxisMilli)
                return CharacterTestDeterministicUnits.MaxInputAxisMilli;
            return value;
        }

        private static short DecodeControlMoveAxisMilli(short value)
        {
            return EncodeControlMoveAxisMilli(value);
        }
    }
}
