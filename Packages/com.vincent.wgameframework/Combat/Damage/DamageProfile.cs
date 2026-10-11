using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Damage
{
    public interface IDamageProfileCatalog
    {
        bool TryGet(int damageProfileId, out DamageProfileConfig profile);
        DamageProfileConfig[] CreateSnapshot();
    }

    public static class DamageFormulaIds
    {
        public const int Flat = 1;
        public const int AttackMinusDefense = 2;
        public const int AttackScaledMinusDefense = 3;
        public const int CharacterTestSoftDefenseV1 = 4;
        public const int CharacterTestDefenseAttributeId = 3;

        public static bool IsKnown(int formulaId)
        {
            return formulaId == Flat
                || formulaId == AttackMinusDefense
                || formulaId == AttackScaledMinusDefense
                || formulaId == CharacterTestSoftDefenseV1;
        }

        public static int Normalize(int formulaId)
        {
            return formulaId <= 0 ? Flat : formulaId;
        }
    }

    public enum DamageProfileSourceKind
    {
        Unknown = 0,
        WeaponTrace = 1,
        AreaPulse = 2,
        ProjectileImpact = 3,
        StatusTick = 4,
        Scripted = 5,
    }

    public readonly struct DamageRequest : IEquatable<DamageRequest>
    {
        public DamageRequest(int damageProfileId, DamageProfileUsage usage)
        {
            DamageProfileId = damageProfileId;
            Usage = usage;
        }

        public int DamageProfileId { get; }
        public DamageProfileUsage Usage { get; }

        public bool Equals(DamageRequest other)
        {
            return DamageProfileId == other.DamageProfileId
                && Usage.Equals(other.Usage);
        }

        public override bool Equals(object obj)
        {
            return obj is DamageRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DamageProfileId * 397)
                    ^ Usage.GetHashCode();
            }
        }
    }

    public readonly struct DamageAttributeScale : IEquatable<DamageAttributeScale>
    {
        public DamageAttributeScale(int attributeId, float scale)
        {
            AttributeId = attributeId < 0 ? 0 : attributeId;
            Scale = scale;
        }

        public int AttributeId { get; }
        public float Scale { get; }

        public bool Equals(DamageAttributeScale other)
        {
            return AttributeId == other.AttributeId && Scale.Equals(other.Scale);
        }

        public override bool Equals(object obj)
        {
            return obj is DamageAttributeScale other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AttributeId * 397) ^ Scale.GetHashCode();
            }
        }
    }

    public readonly struct DamageComponentConfig : IEquatable<DamageComponentConfig>
    {
        public DamageComponentConfig(int componentId, int targetAttributeId, int baseValue)
        {
            ComponentId = componentId;
            TargetAttributeId = targetAttributeId < 0 ? 0 : targetAttributeId;
            BaseValue = baseValue < 0 ? 0 : baseValue;
        }

        public int ComponentId { get; }
        public int TargetAttributeId { get; }
        public int BaseValue { get; }

        public bool Equals(DamageComponentConfig other)
        {
            return ComponentId == other.ComponentId
                && TargetAttributeId == other.TargetAttributeId
                && BaseValue == other.BaseValue;
        }

        public override bool Equals(object obj)
        {
            return obj is DamageComponentConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ComponentId;
                hash = (hash * 397) ^ TargetAttributeId;
                hash = (hash * 397) ^ BaseValue;
                return hash;
            }
        }
    }

    public sealed class DamageProfileConfig
    {
        public DamageProfileConfig(
            int damageProfileId,
            string stableId = "",
            string displayName = "",
            int damageTypeId = 0,
            int formulaId = DamageFormulaIds.Flat,
            int baseDamage = 0,
            DamageAttributeScale[] sourceAttributeScales = null,
            DamageAttributeScale[] targetAttributeScales = null,
            float damageMultiplier = 1f,
            int defenseReference = 50,
            float defenseWeight = 0.6f,
            int minDamage = 0,
            int maxDamage = 0,
            int staggerFrames = 0,
            int impactForce = 0,
            FixVector3 knockback = default,
            int impactPower = 0,
            int guardPressure = 0,
            int presentationProfileId = 0,
            int[] tags = null,
            int analysisCategory = 0,
            DamageComponentConfig[] damageComponents = null,
            int launchPower = 0)
        {
            DamageProfileId = damageProfileId;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DamageTypeId = damageTypeId;
            FormulaId = DamageFormulaIds.Normalize(formulaId);
            BaseDamage = baseDamage < 0 ? 0 : baseDamage;
            SourceAttributeScales = Copy(sourceAttributeScales);
            TargetAttributeScales = Copy(targetAttributeScales);
            DamageMultiplier = damageMultiplier < 0f ? 0f : damageMultiplier;
            DefenseReference = defenseReference <= 0 ? 50 : defenseReference;
            DefenseWeight = Clamp01(defenseWeight);
            MinDamage = minDamage < 0 ? 0 : minDamage;
            MaxDamage = maxDamage < 0 ? 0 : maxDamage;
            StaggerFrames = staggerFrames < 0 ? 0 : staggerFrames;
            ImpactForce = impactForce < 0 ? 0 : impactForce;
            Knockback = knockback;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            LaunchPower = launchPower < 0 ? 0 : launchPower;
            GuardPressure = guardPressure < 0 ? 0 : guardPressure;
            PresentationProfileId = presentationProfileId;
            Tags = Copy(tags);
            AnalysisCategory = analysisCategory;
            DamageComponents = Copy(damageComponents);
        }

        public int DamageProfileId { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public int DamageTypeId { get; }
        public int FormulaId { get; }
        public int BaseDamage { get; }
        public DamageAttributeScale[] SourceAttributeScales { get; }
        public DamageAttributeScale[] TargetAttributeScales { get; }
        public float DamageMultiplier { get; }
        public int DefenseReference { get; }
        public float DefenseWeight { get; }
        public int MinDamage { get; }
        public int MaxDamage { get; }
        public int StaggerFrames { get; }
        public int ImpactForce { get; }
        public FixVector3 Knockback { get; }
        public int ImpactPower { get; }
        public int LaunchPower { get; }
        public int GuardPressure { get; }
        public int PresentationProfileId { get; }
        public int[] Tags { get; }
        public int AnalysisCategory { get; }
        public DamageComponentConfig[] DamageComponents { get; }

        private static DamageAttributeScale[] Copy(DamageAttributeScale[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<DamageAttributeScale>();

            var copy = new DamageAttributeScale[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static DamageComponentConfig[] Copy(DamageComponentConfig[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<DamageComponentConfig>();

            var copy = new DamageComponentConfig[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static int[] Copy(int[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<int>();

            var copy = new int[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }

    public readonly struct DamageProfileUsage : IEquatable<DamageProfileUsage>
    {
        public DamageProfileUsage(
            DamageProfileSourceKind sourceKind,
            CharacterActionId actionId = default,
            int actionInstanceId = 0,
            int stableEventId = 0,
            int equipmentSlotId = 0,
            int weaponId = 0,
            int sourceEntityId = 0,
            int targetEntityId = 0,
            int hitZoneId = 0,
            int bodyPartId = 0,
            int frame = 0,
            int localFrame = 0)
        {
            SourceKind = sourceKind;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            StableEventId = stableEventId;
            EquipmentSlotId = equipmentSlotId;
            WeaponId = weaponId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
            Frame = frame < 0 ? 0 : frame;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
        }

        public DamageProfileSourceKind SourceKind { get; }
        public CharacterActionId ActionId { get; }
        public int ActionInstanceId { get; }
        public int StableEventId { get; }
        public int EquipmentSlotId { get; }
        public int WeaponId { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int HitZoneId { get; }
        public int BodyPartId { get; }
        public int Frame { get; }
        public int LocalFrame { get; }

        public bool Equals(DamageProfileUsage other)
        {
            return SourceKind == other.SourceKind
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && StableEventId == other.StableEventId
                && EquipmentSlotId == other.EquipmentSlotId
                && WeaponId == other.WeaponId
                && SourceEntityId == other.SourceEntityId
                && TargetEntityId == other.TargetEntityId
                && HitZoneId == other.HitZoneId
                && BodyPartId == other.BodyPartId
                && Frame == other.Frame
                && LocalFrame == other.LocalFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is DamageProfileUsage other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceKind;
                hash = (hash * 397) ^ ActionId.GetHashCode();
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ EquipmentSlotId;
                hash = (hash * 397) ^ WeaponId;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ HitZoneId;
                hash = (hash * 397) ^ BodyPartId;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ LocalFrame;
                return hash;
            }
        }
    }

    public readonly struct DamageFormulaTrace
    {
        public DamageFormulaTrace(
            int formulaId,
            int baseDamage,
            float sourceAttributeValue,
            float sourceAttributeScale,
            float sourceAttributeContribution,
            float offense,
            float targetDefense,
            int defenseReference,
            float defenseWeight,
            float mitigation,
            float profileDamageMultiplier,
            float externalDamageMultiplier,
            float rawDamage,
            int incomingDamage)
        {
            FormulaId = formulaId;
            BaseDamage = baseDamage < 0 ? 0 : baseDamage;
            SourceAttributeValue = sourceAttributeValue;
            SourceAttributeScale = sourceAttributeScale;
            SourceAttributeContribution = sourceAttributeContribution;
            Offense = offense;
            TargetDefense = targetDefense < 0f ? 0f : targetDefense;
            DefenseReference = defenseReference <= 0 ? 0 : defenseReference;
            DefenseWeight = defenseWeight < 0f ? 0f : defenseWeight;
            Mitigation = mitigation < 0f ? 0f : mitigation;
            ProfileDamageMultiplier = profileDamageMultiplier < 0f ? 0f : profileDamageMultiplier;
            ExternalDamageMultiplier = externalDamageMultiplier < 0f ? 0f : externalDamageMultiplier;
            RawDamage = rawDamage;
            IncomingDamage = incomingDamage < 0 ? 0 : incomingDamage;
        }

        public int FormulaId { get; }
        public int BaseDamage { get; }
        public float SourceAttributeValue { get; }
        public float SourceAttributeScale { get; }
        public float SourceAttributeContribution { get; }
        public float Offense { get; }
        public float TargetDefense { get; }
        public int DefenseReference { get; }
        public float DefenseWeight { get; }
        public float Mitigation { get; }
        public float ProfileDamageMultiplier { get; }
        public float ExternalDamageMultiplier { get; }
        public float RawDamage { get; }
        public int IncomingDamage { get; }
    }

    public readonly struct DamageProfileResolveResult
    {
        public DamageProfileResolveResult(
            bool success,
            string reason,
            int damageProfileId,
            int damageTypeId,
            int formulaId,
            int incomingDamage,
            int staggerFrames,
            int impactForce,
            FixVector3 knockback,
            int impactPower,
            int guardPressure,
            int presentationProfileId,
            DamageProfileUsage usage,
            DamageFormulaTrace formulaTrace = default,
            int[] tags = null,
            int launchPower = 0,
            int analysisCategory = 0)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            DamageProfileId = damageProfileId;
            DamageTypeId = damageTypeId;
            FormulaId = formulaId;
            IncomingDamage = incomingDamage < 0 ? 0 : incomingDamage;
            StaggerFrames = staggerFrames < 0 ? 0 : staggerFrames;
            ImpactForce = impactForce < 0 ? 0 : impactForce;
            Knockback = knockback;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            LaunchPower = launchPower < 0 ? 0 : launchPower;
            GuardPressure = guardPressure < 0 ? 0 : guardPressure;
            PresentationProfileId = presentationProfileId;
            Usage = usage;
            FormulaTrace = formulaTrace;
            Tags = tags ?? Array.Empty<int>();
            AnalysisCategory = analysisCategory;
        }

        public bool Success { get; }
        public int AnalysisCategory { get; }
        public string Reason { get; }
        public int DamageProfileId { get; }
        public int DamageTypeId { get; }
        public int FormulaId { get; }
        public int IncomingDamage { get; }
        public int StaggerFrames { get; }
        public int ImpactForce { get; }
        public FixVector3 Knockback { get; }
        public int ImpactPower { get; }
        public int LaunchPower { get; }
        public int GuardPressure { get; }
        public int PresentationProfileId { get; }
        public DamageProfileUsage Usage { get; }
        public DamageFormulaTrace FormulaTrace { get; }
        public int[] Tags { get; }
    }

    public readonly struct DamageResolvedEvent
    {
        public DamageResolvedEvent(
            DamageRequest request,
            DamageProfileResolveResult profileResult,
            HitResolveResult hitResult,
            int finalDamage)
        {
            Request = request;
            ProfileResult = profileResult;
            HitResult = hitResult;
            FinalDamage = finalDamage < 0 ? 0 : finalDamage;
        }

        public DamageRequest Request { get; }
        public DamageProfileResolveResult ProfileResult { get; }
        public HitResolveResult HitResult { get; }
        public int FinalDamage { get; }
    }

    public readonly struct HitPresentationEvent
    {
        public HitPresentationEvent(DamageResolvedEvent damageEvent, int presentationProfileId)
        {
            DamageEvent = damageEvent;
            PresentationProfileId = presentationProfileId;
        }

        public DamageResolvedEvent DamageEvent { get; }
        public int PresentationProfileId { get; }
    }

    public readonly struct DamageTelemetryRow
    {
        public DamageTelemetryRow(
            long runtimeFrame,
            DamageProfileSourceKind sourceKind,
            CharacterActionId actionId,
            int actionInstanceId,
            int stableEventId,
            int damageProfileId,
            int damageTypeId,
            int equipmentSlotId,
            int weaponId,
            int sourceEntityId,
            int targetEntityId,
            int hitZoneId,
            int bodyPartId,
            int localFrame,
            int incomingDamage,
            int finalDamage,
            HitResolveKind resultKind,
            int hpBefore,
            int hpAfter,
            DamageFormulaTrace formulaTrace = default,
            int impactPower = 0,
            float effectiveImpact = 0f,
            float sourceMassKg = 0f,
            float targetMassKg = 0f,
            int scatterImpact = 0,
            float knockbackSpeed = 0f,
            float previewKnockbackDistanceMeters = 0f,
            FixVector3 hitPointWorld = default,
            FixVector3 hitNormalWorld = default,
            int analysisCategory = 0)
        {
            RuntimeFrame = runtimeFrame;
            SourceKind = sourceKind;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            StableEventId = stableEventId;
            DamageProfileId = damageProfileId;
            AnalysisCategory = analysisCategory;
            DamageTypeId = damageTypeId;
            EquipmentSlotId = equipmentSlotId;
            WeaponId = weaponId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            IncomingDamage = incomingDamage < 0 ? 0 : incomingDamage;
            FinalDamage = finalDamage < 0 ? 0 : finalDamage;
            ResultKind = resultKind;
            HpBefore = hpBefore < 0 ? 0 : hpBefore;
            HpAfter = hpAfter < 0 ? 0 : hpAfter;
            FormulaTrace = formulaTrace;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            EffectiveImpact = effectiveImpact < 0f ? 0f : effectiveImpact;
            SourceMassKg = sourceMassKg < 0f ? 0f : sourceMassKg;
            TargetMassKg = targetMassKg < 0f ? 0f : targetMassKg;
            ScatterImpact = scatterImpact < 0 ? 0 : scatterImpact;
            KnockbackSpeed = knockbackSpeed < 0f ? 0f : knockbackSpeed;
            PreviewKnockbackDistanceMeters = previewKnockbackDistanceMeters < 0f ? 0f : previewKnockbackDistanceMeters;
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
        }

        public long RuntimeFrame { get; }
        public DamageProfileSourceKind SourceKind { get; }
        public CharacterActionId ActionId { get; }
        public int ActionInstanceId { get; }
        public int StableEventId { get; }
        public int DamageProfileId { get; }
        public int AnalysisCategory { get; }
        public int DamageTypeId { get; }
        public int EquipmentSlotId { get; }
        public int WeaponId { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int HitZoneId { get; }
        public int BodyPartId { get; }
        public int LocalFrame { get; }
        public int IncomingDamage { get; }
        public int FinalDamage { get; }
        public HitResolveKind ResultKind { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public DamageFormulaTrace FormulaTrace { get; }
        public int ImpactPower { get; }
        public float EffectiveImpact { get; }
        public float SourceMassKg { get; }
        public float TargetMassKg { get; }
        public int ScatterImpact { get; }
        public float KnockbackSpeed { get; }
        public float PreviewKnockbackDistanceMeters { get; }
        public FixVector3 HitPointWorld { get; }
        public FixVector3 HitNormalWorld { get; }

        public DamageTelemetryRow WithImpact(
            int impactPower,
            float effectiveImpact,
            float sourceMassKg,
            float targetMassKg,
            int scatterImpact,
            float knockbackSpeed,
            float previewKnockbackDistanceMeters)
        {
            return new DamageTelemetryRow(
                RuntimeFrame,
                SourceKind,
                ActionId,
                ActionInstanceId,
                StableEventId,
                DamageProfileId,
                DamageTypeId,
                EquipmentSlotId,
                WeaponId,
                SourceEntityId,
                TargetEntityId,
                HitZoneId,
                BodyPartId,
                LocalFrame,
                IncomingDamage,
                FinalDamage,
                ResultKind,
                HpBefore,
                HpAfter,
                FormulaTrace,
                impactPower,
                effectiveImpact,
                sourceMassKg,
                targetMassKg,
                scatterImpact,
                knockbackSpeed,
                previewKnockbackDistanceMeters,
                HitPointWorld,
                HitNormalWorld,
                AnalysisCategory);
        }
    }

    public readonly struct DamageProfileCatalogRow
    {
        public DamageProfileCatalogRow(DamageProfileConfig profile)
        {
            DamageProfileId = profile?.DamageProfileId ?? 0;
            StableId = profile?.StableId ?? string.Empty;
            DisplayName = profile?.DisplayName ?? string.Empty;
            DamageTypeId = profile?.DamageTypeId ?? 0;
            FormulaId = profile?.FormulaId ?? 0;
            BaseDamage = profile?.BaseDamage ?? 0;
            DamageMultiplier = profile?.DamageMultiplier ?? 0f;
            DefenseReference = profile?.DefenseReference ?? 0;
            DefenseWeight = profile?.DefenseWeight ?? 0f;
            MinDamage = profile?.MinDamage ?? 0;
            MaxDamage = profile?.MaxDamage ?? 0;
            StaggerFrames = profile?.StaggerFrames ?? 0;
            ImpactForce = profile?.ImpactForce ?? 0;
            ImpactPower = profile?.ImpactPower ?? 0;
            LaunchPower = profile?.LaunchPower ?? 0;
            GuardPressure = profile?.GuardPressure ?? 0;
            PresentationProfileId = profile?.PresentationProfileId ?? 0;
            AnalysisCategory = profile?.AnalysisCategory ?? 0;
        }

        public int DamageProfileId { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public int DamageTypeId { get; }
        public int FormulaId { get; }
        public int BaseDamage { get; }
        public float DamageMultiplier { get; }
        public int DefenseReference { get; }
        public float DefenseWeight { get; }
        public int MinDamage { get; }
        public int MaxDamage { get; }
        public int StaggerFrames { get; }
        public int ImpactForce { get; }
        public int ImpactPower { get; }
        public int LaunchPower { get; }
        public int GuardPressure { get; }
        public int PresentationProfileId { get; }
        public int AnalysisCategory { get; }
    }

    public static class DamageProfileResolver
    {
        public static DamageProfileResolveResult Resolve(
            DamageProfileConfig profile,
            DamageProfileUsage usage,
            Func<int, int> sourceAttributeReader = null,
            Func<int, int> targetAttributeReader = null,
            float damageMultiplier = 1f,
            float staggerScale = 1f,
            float impactPowerScale = 1f)
        {
            if (profile == null)
                return Failure("Damage profile is missing.", usage);

            if (profile.DamageProfileId <= 0)
                return Failure("Damage profile id is empty.", usage);

            if (!DamageFormulaIds.IsKnown(profile.FormulaId))
                return Failure("Damage formula id is unknown: " + profile.FormulaId, usage, profile);

            DamageFormulaTrace trace = profile.FormulaId == DamageFormulaIds.CharacterTestSoftDefenseV1
                ? ResolveSoftDefenseTrace(profile, sourceAttributeReader, targetAttributeReader, damageMultiplier)
                : ResolveLinearTrace(profile, sourceAttributeReader, targetAttributeReader, damageMultiplier);

            int impactPower = Round(profile.ImpactPower * ClampNonNegative(impactPowerScale));

            return new DamageProfileResolveResult(
                true,
                string.Empty,
                profile.DamageProfileId,
                profile.DamageTypeId,
                profile.FormulaId,
                trace.IncomingDamage,
                0,
                0,
                FixVector3.Zero,
                impactPower,
                0,
                profile.PresentationProfileId,
                usage,
                trace,
                profile.Tags,
                profile.LaunchPower,
                profile.AnalysisCategory);
        }

        private static DamageProfileResolveResult Failure(
            string reason,
            DamageProfileUsage usage,
            DamageProfileConfig profile = null)
        {
            return new DamageProfileResolveResult(
                false,
                reason,
                profile?.DamageProfileId ?? 0,
                profile?.DamageTypeId ?? 0,
                profile?.FormulaId ?? 0,
                0,
                0,
                0,
                FixVector3.Zero,
                0,
                0,
                profile?.PresentationProfileId ?? 0,
                usage,
                default,
                profile?.Tags,
                profile?.LaunchPower ?? 0,
                profile?.AnalysisCategory ?? 0);
        }

        private static DamageFormulaTrace ResolveLinearTrace(
            DamageProfileConfig profile,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            float externalDamageMultiplier)
        {
            float sourceContribution = Sum(profile.SourceAttributeScales, sourceAttributeReader);
            float targetContribution = Sum(profile.TargetAttributeScales, targetAttributeReader);
            float profileMultiplier = ClampNonNegative(profile.DamageMultiplier);
            float externalMultiplier = ClampNonNegative(externalDamageMultiplier);
            float rawDamage = (profile.BaseDamage + sourceContribution + targetContribution)
                * profileMultiplier
                * externalMultiplier;
            int incomingDamage = NonNegativeDamage(Round(rawDamage));
            return new DamageFormulaTrace(
                profile.FormulaId,
                profile.BaseDamage,
                SourceAttributeValue(profile.SourceAttributeScales, sourceAttributeReader),
                SourceAttributeScale(profile.SourceAttributeScales),
                sourceContribution,
                profile.BaseDamage + sourceContribution,
                Math.Max(0f, targetContribution),
                0,
                0f,
                0f,
                profileMultiplier,
                externalMultiplier,
                rawDamage,
                incomingDamage);
        }

        private static DamageFormulaTrace ResolveSoftDefenseTrace(
            DamageProfileConfig profile,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            float externalDamageMultiplier)
        {
            float sourceContribution = Sum(profile.SourceAttributeScales, sourceAttributeReader);
            float offense = Math.Max(0f, profile.BaseDamage + sourceContribution);
            float targetDefense = Math.Max(0f, targetAttributeReader?.Invoke(DamageFormulaIds.CharacterTestDefenseAttributeId) ?? 0f);
            float mitigation = targetDefense <= 0f
                ? 0f
                : profile.DefenseWeight * targetDefense / (targetDefense + profile.DefenseReference);
            mitigation = Clamp01(mitigation);
            float profileMultiplier = ClampNonNegative(profile.DamageMultiplier);
            float externalMultiplier = ClampNonNegative(externalDamageMultiplier);
            float rawDamage = offense * (1f - mitigation) * profileMultiplier * externalMultiplier;
            int incomingDamage = NonNegativeDamage(Round(rawDamage));
            return new DamageFormulaTrace(
                profile.FormulaId,
                profile.BaseDamage,
                SourceAttributeValue(profile.SourceAttributeScales, sourceAttributeReader),
                SourceAttributeScale(profile.SourceAttributeScales),
                sourceContribution,
                offense,
                targetDefense,
                profile.DefenseReference,
                profile.DefenseWeight,
                mitigation,
                profileMultiplier,
                externalMultiplier,
                rawDamage,
                incomingDamage);
        }

        private static float Sum(DamageAttributeScale[] scales, Func<int, int> reader)
        {
            if (scales == null || scales.Length == 0 || reader == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId <= 0 || scale.Scale.Equals(0f))
                    continue;
                total += reader(scale.AttributeId) * scale.Scale;
            }

            return total;
        }

        private static float SourceAttributeValue(DamageAttributeScale[] scales, Func<int, int> reader)
        {
            if (scales == null || scales.Length == 0 || reader == null)
                return 0f;
            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId > 0 && !scale.Scale.Equals(0f))
                    return reader(scale.AttributeId);
            }

            return 0f;
        }

        private static float SourceAttributeScale(DamageAttributeScale[] scales)
        {
            if (scales == null || scales.Length == 0)
                return 0f;
            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId > 0 && !scale.Scale.Equals(0f))
                    return scale.Scale;
            }

            return 0f;
        }

        private static int NonNegativeDamage(int value)
        {
            return value < 0 ? 0 : value;
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static float ClampNonNegative(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }
    }
}

