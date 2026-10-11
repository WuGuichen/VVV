using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestRuntimeCommandFactory
    {
        internal const int GenesisMovableObjectProbeSchemaVersion = 1;
        internal const int GenesisPotencyMechanicsProbeSchemaVersion = 1;
        internal const int GenesisLivingBeingProbeSchemaVersion = 1;
        internal const int GenesisLivingNaturalAttributesProbeSchemaVersion = 1;
        internal const int GenesisLivingNaturalAttributesRuntimeStateSchemaVersion = 1;

        internal static RuntimeCommand GenesisMovableObjectProbe(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestGenesisMovableObjectProbeKind scenario,
            CharacterTestGenesisForceOriginKind forceOrigin,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe,
                targetId: 0,
                payload0: (int)scenario,
                payload1: (int)forceOrigin,
                payload2: GenesisMovableObjectProbeSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand GenesisPotencyMechanicsProbe(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestGenesisPotencyMechanicsProbeKind scenario,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe,
                targetId: 0,
                payload0: (int)scenario,
                payload1: 0,
                payload2: GenesisPotencyMechanicsProbeSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand GenesisLivingBeingProbe(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestGenesisLivingBeingProbeKind scenario,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe,
                targetId: 0,
                payload0: (int)scenario,
                payload1: 0,
                payload2: GenesisLivingBeingProbeSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand GenesisLivingNaturalAttributesProbe(
            RuntimeFrame frame,
            int sourceId,
            CharacterTestGenesisLivingNaturalAttributesProbeKind scenario,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe,
                targetId: 0,
                payload0: (int)scenario,
                payload1: 0,
                payload2: GenesisLivingNaturalAttributesProbeSchemaVersion,
                traceId: traceId);
        }

        internal static RuntimeCommand GenesisLivingNaturalAttributesRuntimeState(
            RuntimeFrame frame,
            int sourceId,
            int targetEntityId,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            int magnitude = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState,
                targetEntityId,
                payload0: (int)operation,
                payload1: magnitude,
                payload2: GenesisLivingNaturalAttributesRuntimeStateSchemaVersion,
                traceId: traceId);
        }

        internal static CharacterTestGenesisMovableObjectProbeRequest DecodeGenesisMovableObjectProbe(
            RuntimeCommand command)
        {
            return new CharacterTestGenesisMovableObjectProbeRequest(
                (CharacterTestGenesisMovableObjectProbeKind)command.Payload0,
                (CharacterTestGenesisForceOriginKind)command.Payload1,
                command.Payload2);
        }

        internal static CharacterTestGenesisPotencyMechanicsProbeRequest DecodeGenesisPotencyMechanicsProbe(
            RuntimeCommand command)
        {
            return new CharacterTestGenesisPotencyMechanicsProbeRequest(
                (CharacterTestGenesisPotencyMechanicsProbeKind)command.Payload0,
                command.Payload2);
        }

        internal static CharacterTestGenesisLivingBeingProbeRequest DecodeGenesisLivingBeingProbe(
            RuntimeCommand command)
        {
            return new CharacterTestGenesisLivingBeingProbeRequest(
                (CharacterTestGenesisLivingBeingProbeKind)command.Payload0,
                command.Payload2);
        }

        internal static CharacterTestGenesisLivingNaturalAttributesProbeRequest
            DecodeGenesisLivingNaturalAttributesProbe(RuntimeCommand command)
        {
            return new CharacterTestGenesisLivingNaturalAttributesProbeRequest(
                (CharacterTestGenesisLivingNaturalAttributesProbeKind)command.Payload0,
                command.Payload2);
        }

        internal static CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest
            DecodeGenesisLivingNaturalAttributesRuntimeState(RuntimeCommand command)
        {
            return new CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest(
                command.TargetId,
                (CharacterTestGenesisLivingNaturalAttributesRuntimeOperation)command.Payload0,
                command.Payload1,
                command.Payload2);
        }
    }

    internal enum CharacterTestGenesisMovableObjectProbeKind
    {
        None = 0,
        ValidationSuite = 1,
        Push = 2,
        Pressure = 3,
        Rebound = 4,
        ObjectContact = 5
    }

    internal enum CharacterTestGenesisPotencyMechanicsProbeKind
    {
        None = 0,
        ValidationSuite = 1,
        FireDryGrass = 2,
        FireStone = 3,
        HammerDryGrass = 4,
        HammerStone = 5
    }

    internal enum CharacterTestGenesisLivingBeingProbeKind
    {
        None = 0,
        ValidationSuite = 1,
        Approach = 2,
        Avoid = 3,
        Brace = 4,
        Unbalanced = 5,
        CohesionBroken = 6,
        Restabilize = 7,
        Inactive = 8
    }

    internal enum CharacterTestGenesisLivingNaturalAttributesProbeKind
    {
        None = 0,
        ValidationSuite = 1,
        Embody = 2,
        SelfMaintain = 3,
        Recover = 4,
        InactiveMaterial = 5
    }

    internal readonly struct CharacterTestGenesisMovableObjectProbeRequest
    {
        public CharacterTestGenesisMovableObjectProbeRequest(
            CharacterTestGenesisMovableObjectProbeKind scenario,
            CharacterTestGenesisForceOriginKind forceOrigin,
            int schemaVersion)
        {
            Scenario = scenario;
            ForceOrigin = forceOrigin;
            SchemaVersion = schemaVersion;
        }

        public CharacterTestGenesisMovableObjectProbeKind Scenario { get; }
        public CharacterTestGenesisForceOriginKind ForceOrigin { get; }
        public int SchemaVersion { get; }
    }

    internal readonly struct CharacterTestGenesisPotencyMechanicsProbeRequest
    {
        public CharacterTestGenesisPotencyMechanicsProbeRequest(
            CharacterTestGenesisPotencyMechanicsProbeKind scenario,
            int schemaVersion)
        {
            Scenario = scenario;
            SchemaVersion = schemaVersion;
        }

        public CharacterTestGenesisPotencyMechanicsProbeKind Scenario { get; }
        public int SchemaVersion { get; }
    }

    internal readonly struct CharacterTestGenesisLivingBeingProbeRequest
    {
        public CharacterTestGenesisLivingBeingProbeRequest(
            CharacterTestGenesisLivingBeingProbeKind scenario,
            int schemaVersion)
        {
            Scenario = scenario;
            SchemaVersion = schemaVersion;
        }

        public CharacterTestGenesisLivingBeingProbeKind Scenario { get; }
        public int SchemaVersion { get; }
    }

    internal readonly struct CharacterTestGenesisLivingNaturalAttributesProbeRequest
    {
        public CharacterTestGenesisLivingNaturalAttributesProbeRequest(
            CharacterTestGenesisLivingNaturalAttributesProbeKind scenario,
            int schemaVersion)
        {
            Scenario = scenario;
            SchemaVersion = schemaVersion;
        }

        public CharacterTestGenesisLivingNaturalAttributesProbeKind Scenario { get; }
        public int SchemaVersion { get; }
    }

    internal readonly struct CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest
    {
        public CharacterTestGenesisLivingNaturalAttributesRuntimeStateRequest(
            int targetEntityId,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            int magnitude,
            int schemaVersion)
        {
            TargetEntityId = targetEntityId;
            Operation = operation;
            Magnitude = magnitude;
            SchemaVersion = schemaVersion;
        }

        public int TargetEntityId { get; }
        public CharacterTestGenesisLivingNaturalAttributesRuntimeOperation Operation { get; }
        public int Magnitude { get; }
        public int SchemaVersion { get; }
    }
}
