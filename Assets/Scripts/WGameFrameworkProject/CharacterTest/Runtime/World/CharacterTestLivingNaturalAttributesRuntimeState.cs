using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisLivingNaturalAttributesRuntimeOperation
    {
        None = 0,
        Embody = 1,
        ApplyExternalPressure = 2,
        ReleaseAndRecover = 3,
        OverlimitDeactivate = 4,
        Reset = 5
    }

    internal enum CharacterTestLivingNaturalAttributesLifecycleState
    {
        None = 0,
        LivingBodyStable = 1,
        SelfMaintaining = 2,
        Recovering = 3,
        InactiveMaterial = 4
    }

    internal readonly struct CharacterTestLivingNaturalAttributesRuntimeState
    {
        internal const string StateModel = "character-test-runtime-authority";
        internal const int DefaultBodyScaleMillimeters = 1800;
        internal const int DefaultMassGrams = CharacterTestCharacterPhysicsComponent.DefaultMassGrams;
        internal const int DefaultContactSurfaceCount = 1;
        internal const int DefaultNearFieldContactCount = 1;
        internal const int DefaultSelfCohesion = 100;
        internal const int DefaultCohesionLimit = 100;
        internal const int DefaultExternalPressure = 40;
        internal const int DefaultRecoveryAmount = 60;
        internal const int DefaultOverlimitPressure = 120;
        internal const int MaximumCommandMagnitude = 1000;

        private CharacterTestLivingNaturalAttributesRuntimeState(
            CharacterTestLivingNaturalAttributesLifecycleState lifecycleState,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation lastOperation,
            bool bodyBoundaryPresent,
            int bodyScaleMillimeters,
            int massGrams,
            int contactSurfaceCount,
            int nearFieldContactCount,
            int selfCohesion,
            int cohesionLimit,
            int externalPressure,
            int scatter,
            int recoveryTicks,
            int recoveryAmount,
            long lastInputFrame,
            long lastTransitionFrame)
        {
            LifecycleState = lifecycleState;
            LastOperation = lastOperation;
            BodyBoundaryPresent = bodyBoundaryPresent;
            BodyScaleMillimeters = bodyScaleMillimeters < 0 ? 0 : bodyScaleMillimeters;
            MassGrams = massGrams < 0 ? 0 : massGrams;
            ContactSurfaceCount = contactSurfaceCount < 0 ? 0 : contactSurfaceCount;
            NearFieldContactCount = nearFieldContactCount < 0 ? 0 : nearFieldContactCount;
            SelfCohesion = selfCohesion < 0 ? 0 : selfCohesion;
            CohesionLimit = cohesionLimit <= 0 ? 1 : cohesionLimit;
            ExternalPressure = externalPressure < 0 ? 0 : externalPressure;
            Scatter = scatter < 0 ? 0 : scatter;
            RecoveryTicks = recoveryTicks < 0 ? 0 : recoveryTicks;
            RecoveryAmount = recoveryAmount < 0 ? 0 : recoveryAmount;
            LastInputFrame = lastInputFrame;
            LastTransitionFrame = lastTransitionFrame;
        }

        public CharacterTestLivingNaturalAttributesLifecycleState LifecycleState { get; }
        public CharacterTestGenesisLivingNaturalAttributesRuntimeOperation LastOperation { get; }
        public bool BodyBoundaryPresent { get; }
        public int BodyScaleMillimeters { get; }
        public int MassGrams { get; }
        public int ContactSurfaceCount { get; }
        public int NearFieldContactCount { get; }
        public int SelfCohesion { get; }
        public int CohesionLimit { get; }
        public int ExternalPressure { get; }
        public int Scatter { get; }
        public int RecoveryTicks { get; }
        public int RecoveryAmount { get; }
        public long LastInputFrame { get; }
        public long LastTransitionFrame { get; }
        public bool CanAct => BodyBoundaryPresent && LifecycleState != CharacterTestLivingNaturalAttributesLifecycleState.InactiveMaterial;
        public bool ReturnedToMaterial => MaterialReading == CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem;
        public CharacterTestGenesisLivingNaturalAttributeMaterialReading MaterialReading =>
            LifecycleState == CharacterTestLivingNaturalAttributesLifecycleState.InactiveMaterial
                ? CharacterTestGenesisLivingNaturalAttributeMaterialReading.MaterialSystem
                : CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool CanEmbody =>
            BodyBoundaryPresent
            && BodyScaleMillimeters > 0
            && MassGrams > 0
            && ContactSurfaceCount > 0
            && NearFieldContactCount > 0
            && MaterialReading == CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool CanSelfMaintain =>
            CanAct
            && SelfCohesion >= Scatter
            && MaterialReading == CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool CanRecover =>
            CanAct
            && Scatter < CohesionLimit
            && MaterialReading == CharacterTestGenesisLivingNaturalAttributeMaterialReading.LivingBody;
        public bool RecoveredToStable =>
            LifecycleState == CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable
            && Scatter == 0
            && CanRecover;

        public static CharacterTestLivingNaturalAttributesRuntimeState CreateDefault(
            RuntimeFrame frame = default,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation lastOperation =
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.None)
        {
            return new CharacterTestLivingNaturalAttributesRuntimeState(
                CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable,
                lastOperation,
                bodyBoundaryPresent: true,
                DefaultBodyScaleMillimeters,
                DefaultMassGrams,
                DefaultContactSurfaceCount,
                DefaultNearFieldContactCount,
                DefaultSelfCohesion,
                DefaultCohesionLimit,
                externalPressure: 0,
                scatter: 0,
                recoveryTicks: 0,
                recoveryAmount: 0,
                lastInputFrame: frame.Value,
                lastTransitionFrame: frame.Value);
        }

        public static CharacterTestLivingNaturalAttributesRuntimeState FromSnapshot(
            CharacterTestLivingNaturalAttributesLifecycleState lifecycleState,
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation lastOperation,
            bool bodyBoundaryPresent,
            int bodyScaleMillimeters,
            int massGrams,
            int contactSurfaceCount,
            int nearFieldContactCount,
            int selfCohesion,
            int cohesionLimit,
            int externalPressure,
            int scatter,
            int recoveryTicks,
            int recoveryAmount,
            long lastInputFrame,
            long lastTransitionFrame)
        {
            if (lifecycleState == CharacterTestLivingNaturalAttributesLifecycleState.None)
                return CreateDefault();

            return new CharacterTestLivingNaturalAttributesRuntimeState(
                lifecycleState,
                lastOperation,
                bodyBoundaryPresent,
                bodyScaleMillimeters,
                massGrams,
                contactSurfaceCount,
                nearFieldContactCount,
                selfCohesion,
                cohesionLimit,
                externalPressure,
                scatter,
                recoveryTicks,
                recoveryAmount,
                lastInputFrame,
                lastTransitionFrame);
        }

        public CharacterTestLivingNaturalAttributesRuntimeState ApplyOperation(
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            int magnitude,
            RuntimeFrame frame)
        {
            int safeMagnitude = ResolveMagnitude(operation, magnitude);
            switch (operation)
            {
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Embody:
                    return CreateDefault(
                        frame,
                        CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Embody);
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure:
                    return ApplyExternalPressure(safeMagnitude, frame);
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover:
                    return ReleaseAndRecover(safeMagnitude, frame);
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate:
                    return OverlimitDeactivate(safeMagnitude, frame);
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Reset:
                    return CreateDefault();
                default:
                    return this;
            }
        }

        public string ToSummaryLine(int entityId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} entity={1} operation={2} lifecycle={3} canEmbody={4} canSelfMaintain={5} canRecover={6} canAct={7} bodyBoundary={8} cohesion={9}/{10} scatter={11} externalPressure={12} recoveryTicks={13} recoveryAmount={14} materialReading={15} returnedToMaterial={16} lastInputFrame={17} lastTransitionFrame={18}",
                StateModel,
                entityId,
                LastOperation,
                LifecycleState,
                CanEmbody ? "true" : "false",
                CanSelfMaintain ? "true" : "false",
                CanRecover ? "true" : "false",
                CanAct ? "true" : "false",
                BodyBoundaryPresent ? "true" : "false",
                SelfCohesion,
                CohesionLimit,
                Scatter,
                ExternalPressure,
                RecoveryTicks,
                RecoveryAmount,
                MaterialReading,
                ReturnedToMaterial ? "true" : "false",
                LastInputFrame,
                LastTransitionFrame);
        }

        internal static bool IsValidOperation(
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation)
        {
            return operation >= CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Embody
                && operation <= CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.Reset;
        }

        internal static bool IsValidCommandMagnitude(int magnitude)
        {
            return magnitude >= 0 && magnitude <= MaximumCommandMagnitude;
        }

        private CharacterTestLivingNaturalAttributesRuntimeState ApplyExternalPressure(
            int pressure,
            RuntimeFrame frame)
        {
            int scatter = Clamp(Scatter + pressure, 0, CohesionLimit - 1);
            return new CharacterTestLivingNaturalAttributesRuntimeState(
                CharacterTestLivingNaturalAttributesLifecycleState.SelfMaintaining,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure,
                bodyBoundaryPresent: true,
                BodyScaleMillimeters > 0 ? BodyScaleMillimeters : DefaultBodyScaleMillimeters,
                MassGrams > 0 ? MassGrams : DefaultMassGrams,
                ContactSurfaceCount > 0 ? ContactSurfaceCount : DefaultContactSurfaceCount,
                NearFieldContactCount > 0 ? NearFieldContactCount : DefaultNearFieldContactCount,
                SelfCohesion > 0 ? SelfCohesion : DefaultSelfCohesion,
                CohesionLimit > 0 ? CohesionLimit : DefaultCohesionLimit,
                pressure,
                scatter,
                RecoveryTicks,
                recoveryAmount: 0,
                frame.Value,
                frame.Value);
        }

        private CharacterTestLivingNaturalAttributesRuntimeState ReleaseAndRecover(
            int recoveryAmount,
            RuntimeFrame frame)
        {
            int scatter = Scatter - recoveryAmount;
            if (scatter < 0)
                scatter = 0;

            CharacterTestLivingNaturalAttributesLifecycleState nextState =
                scatter == 0
                    ? CharacterTestLivingNaturalAttributesLifecycleState.LivingBodyStable
                    : CharacterTestLivingNaturalAttributesLifecycleState.Recovering;
            return new CharacterTestLivingNaturalAttributesRuntimeState(
                nextState,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover,
                bodyBoundaryPresent: true,
                BodyScaleMillimeters > 0 ? BodyScaleMillimeters : DefaultBodyScaleMillimeters,
                MassGrams > 0 ? MassGrams : DefaultMassGrams,
                ContactSurfaceCount > 0 ? ContactSurfaceCount : DefaultContactSurfaceCount,
                NearFieldContactCount > 0 ? NearFieldContactCount : DefaultNearFieldContactCount,
                SelfCohesion > 0 ? SelfCohesion : DefaultSelfCohesion,
                CohesionLimit > 0 ? CohesionLimit : DefaultCohesionLimit,
                externalPressure: 0,
                scatter,
                RecoveryTicks + 1,
                recoveryAmount,
                frame.Value,
                frame.Value);
        }

        private CharacterTestLivingNaturalAttributesRuntimeState OverlimitDeactivate(
            int overlimitPressure,
            RuntimeFrame frame)
        {
            int limit = CohesionLimit > 0 ? CohesionLimit : DefaultCohesionLimit;
            return new CharacterTestLivingNaturalAttributesRuntimeState(
                CharacterTestLivingNaturalAttributesLifecycleState.InactiveMaterial,
                CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate,
                bodyBoundaryPresent: false,
                BodyScaleMillimeters > 0 ? BodyScaleMillimeters : DefaultBodyScaleMillimeters,
                MassGrams > 0 ? MassGrams : DefaultMassGrams,
                contactSurfaceCount: 0,
                nearFieldContactCount: 0,
                selfCohesion: 0,
                limit,
                overlimitPressure,
                scatter: limit + overlimitPressure,
                RecoveryTicks,
                recoveryAmount: 0,
                frame.Value,
                frame.Value);
        }

        private static int ResolveMagnitude(
            CharacterTestGenesisLivingNaturalAttributesRuntimeOperation operation,
            int magnitude)
        {
            if (magnitude > 0)
                return magnitude;

            switch (operation)
            {
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ApplyExternalPressure:
                    return DefaultExternalPressure;
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.ReleaseAndRecover:
                    return DefaultRecoveryAmount;
                case CharacterTestGenesisLivingNaturalAttributesRuntimeOperation.OverlimitDeactivate:
                    return DefaultOverlimitPressure;
                default:
                    return 0;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
                return min;
            if (value < min)
                return min;
            return value > max ? max : value;
        }
    }
}
