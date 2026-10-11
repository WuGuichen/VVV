using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>CharacterTest project mapping from gameplay contact facts to independent acoustic identities.</summary>
    internal static class CharacterTestAcousticMaterialMapper
    {
        public static readonly AcousticMaterialId GenericSolid = new AcousticMaterialId(510002);
        public static readonly AcousticMaterialId Metal = new AcousticMaterialId(510004);
        public static readonly AcousticMaterialId Stone = new AcousticMaterialId(510005);
        public static readonly AcousticMaterialId Rubber = new AcousticMaterialId(510008);
        public static readonly AcousticMaterialId FabricLeather = new AcousticMaterialId(510009);
        public static readonly AcousticMaterialId SoilGravel = new AcousticMaterialId(510011);
        public static readonly AcousticMaterialId Air = new AcousticMaterialId(510014);

        /// <remarks>NoAlloc.</remarks>
        public static AcousticMaterialId Map(CombatSurfaceMaterialKind materialKind)
        {
            switch (materialKind)
            {
                case CombatSurfaceMaterialKind.Rough:
                    return SoilGravel;
                case CombatSurfaceMaterialKind.HardSlick:
                    return Stone;
                case CombatSurfaceMaterialKind.Elastic:
                case CombatSurfaceMaterialKind.BouncePad:
                    return Rubber;
                default:
                    return GenericSolid;
            }
        }

        /// <remarks>NoAlloc.</remarks>
        public static AcousticBodyProperties CreateReceiver(
            CombatSurfaceMaterialKind materialKind,
            float contactArea)
        {
            switch (materialKind)
            {
                case CombatSurfaceMaterialKind.Rough:
                    return new AcousticBodyProperties(
                        SoilGravel,
                        mass: 0.7f,
                        contactArea,
                        AcousticSurfaceKind.Rough,
                        AcousticConstructionKind.Solid);
                case CombatSurfaceMaterialKind.HardSlick:
                    return new AcousticBodyProperties(
                        Stone,
                        mass: 0.85f,
                        contactArea,
                        AcousticSurfaceKind.Smooth,
                        AcousticConstructionKind.Solid);
                case CombatSurfaceMaterialKind.Elastic:
                case CombatSurfaceMaterialKind.BouncePad:
                    return new AcousticBodyProperties(
                        Rubber,
                        mass: 0.58f,
                        contactArea,
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.SoftFilled);
                default:
                    return new AcousticBodyProperties(
                        GenericSolid,
                        mass: 0.7f,
                        contactArea,
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.Solid);
            }
        }
    }

    internal enum CharacterTestAcousticSubjectRole
    {
        Body = 1,
        Foot = 2,
        Weapon = 3,
    }

    /// <summary>Stable numeric project keys used by the low-frequency acoustic property cache.</summary>
    internal static class CharacterTestAcousticSubjectId
    {
        private const int RoleShift = 24;
        private const int EntityMask = 0x00ffffff;

        /// <remarks>NoAlloc.</remarks>
        public static int ForCharacter(int entityId, CharacterTestAcousticSubjectRole role)
        {
            if (entityId <= 0)
                return 0;
            return ((int)role << RoleShift) | (entityId & EntityMask);
        }

        /// <remarks>NoAlloc.</remarks>
        public static int ResolveCharacterEntityId(int combatEntityOrBodyId)
        {
            if (combatEntityOrBodyId > CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                return combatEntityOrBodyId - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
            return combatEntityOrBodyId > 0 ? combatEntityOrBodyId : 0;
        }
    }

    /// <summary>
    /// CharacterTest composition-root cache for project acoustic properties. Gameplay-facing strings and Unity
    /// objects are translated at spawn/equipment seams; audio Tick reads stable integer keys only.
    /// </summary>
    /// <remarks>LowFreqAlloc on registration. <see cref="TryGetProperties"/> is NoAlloc.</remarks>
    internal sealed class CharacterTestAcousticBodyPropertyProvider : IAcousticBodyPropertyProvider
    {
        internal const int DefaultCapacity = 192;
        private const float CharacterMassReferenceKilograms = 160f;
        private const float CharacterContactAreaReferenceMeters = 0.7f;

        private readonly Dictionary<int, AcousticBodyProperties> _properties;
        private readonly int _capacity;

        public CharacterTestAcousticBodyPropertyProvider(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _properties = new Dictionary<int, AcousticBodyProperties>(capacity);
        }

        public int Count => _properties.Count;

        /// <remarks>LowFreqAlloc: called from entity spawn or explicit equipment/property refresh.</remarks>
        public bool RegisterCharacter(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return false;

            float bodyMass = Clamp01(entity.Physics.EffectiveMassKg / CharacterMassReferenceKilograms);
            float bodyArea = Clamp01(entity.Physics.EffectiveRadiusMeters / CharacterContactAreaReferenceMeters);
            if (!RegisterProperties(
                    CharacterTestAcousticSubjectId.ForCharacter(entity.Id.Value, CharacterTestAcousticSubjectRole.Body),
                    new AcousticBodyProperties(
                        CharacterTestAcousticMaterialMapper.GenericSolid,
                        bodyMass,
                        bodyArea,
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.Solid)))
            {
                return false;
            }

            if (!RegisterProperties(
                    CharacterTestAcousticSubjectId.ForCharacter(entity.Id.Value, CharacterTestAcousticSubjectRole.Foot),
                    new AcousticBodyProperties(
                        CharacterTestAcousticMaterialMapper.FabricLeather,
                        mass: Math.Max(0.1f, bodyMass * 0.45f),
                        contactArea: Math.Max(0.2f, bodyArea * 0.7f),
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.SoftFilled)))
            {
                return false;
            }

            // CharacterTest currently exposes only a default-loadout flag. That low-frequency equipment seam maps
            // explicitly to the demo katana; projects with richer equipment data replace this cached role value.
            return RegisterProperties(
                CharacterTestAcousticSubjectId.ForCharacter(entity.Id.Value, CharacterTestAcousticSubjectRole.Weapon),
                new AcousticBodyProperties(
                    CharacterTestAcousticMaterialMapper.Metal,
                    CharacterTestKatanaAirDisplacementProfile.KatanaNormalizedMass,
                    CharacterTestKatanaAirDisplacementProfile.KatanaNormalizedSweptArea,
                    AcousticSurfaceKind.Smooth,
                    AcousticConstructionKind.Solid));
        }

        /// <remarks>LowFreqAlloc when the key is first registered; updating an existing key is NoAlloc.</remarks>
        internal bool RegisterProperties(int subjectId, in AcousticBodyProperties properties)
        {
            if (subjectId <= 0)
                return false;
            if (_properties.ContainsKey(subjectId))
            {
                _properties[subjectId] = properties;
                return true;
            }
            if (_properties.Count >= _capacity)
                return false;
            _properties.Add(subjectId, properties);
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetProperties(int subjectId, out AcousticBodyProperties properties)
        {
            if (subjectId <= 0)
            {
                properties = default;
                return false;
            }
            return _properties.TryGetValue(subjectId, out properties);
        }

        /// <remarks>NoAlloc.</remarks>
        public void RemoveCharacter(CharacterTestCharacterEntityId entityId)
        {
            _properties.Remove(CharacterTestAcousticSubjectId.ForCharacter(
                entityId.Value,
                CharacterTestAcousticSubjectRole.Body));
            _properties.Remove(CharacterTestAcousticSubjectId.ForCharacter(
                entityId.Value,
                CharacterTestAcousticSubjectRole.Foot));
            _properties.Remove(CharacterTestAcousticSubjectId.ForCharacter(
                entityId.Value,
                CharacterTestAcousticSubjectRole.Weapon));
        }

        /// <remarks>NoAlloc.</remarks>
        public void Clear() => _properties.Clear();

        private static float Clamp01(float value)
        {
            if (value <= 0f || float.IsNaN(value))
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    internal static class CharacterTestPhysicalAudioFactBuilder
    {
        private const float MillimetersToMeters = 0.001f;
        private const float ContactPressureReference = 100000f;
        private const float ContactImpulseReference = 200f;
        private const float ContactIntensityReference = 6000f;

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            in CombatPhysicalContactEvent contactEvent,
            out PhysicalAudioInteractionFact fact)
        {
            return TryCreate(contactEvent, null, out fact);
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            in CombatPhysicalContactEvent contactEvent,
            IAcousticBodyPropertyProvider propertyProvider,
            out PhysicalAudioInteractionFact fact)
        {
            return TryCreate(contactEvent, propertyProvider, default, out fact);
        }

        /// <remarks>NoAlloc. Raw dynamics are normalized only at this CharacterTest presentation bridge.</remarks>
        public static bool TryCreate(
            in CombatPhysicalContactEvent contactEvent,
            IAcousticBodyPropertyProvider propertyProvider,
            in PhysicalAudioContactDynamics dynamics,
            out PhysicalAudioInteractionFact fact)
        {
            PhysicalAudioInteractionKind kind;
            PhysicalAudioInteractionMode mode;
            AcousticBodyProperties impactor;
            float receiverContactArea;
            switch (contactEvent.Kind)
            {
                case CombatPhysicalContactKind.BodyPartContact:
                    kind = PhysicalAudioInteractionKind.Footstep;
                    mode = PhysicalAudioInteractionMode.Step;
                    impactor = ResolveCharacterProperties(
                        propertyProvider,
                        contactEvent.SourceEntityId.Value,
                        CharacterTestAcousticSubjectRole.Foot,
                        new AcousticBodyProperties(
                            CharacterTestAcousticMaterialMapper.FabricLeather,
                            mass: 0.35f,
                            contactArea: 0.5f,
                            AcousticSurfaceKind.Normal,
                            AcousticConstructionKind.SoftFilled));
                    receiverContactArea = 0.7f;
                    break;
                case CombatPhysicalContactKind.Landing:
                    kind = PhysicalAudioInteractionKind.Landing;
                    mode = PhysicalAudioInteractionMode.Landing;
                    impactor = ResolveCharacterProperties(
                        propertyProvider,
                        contactEvent.SourceEntityId.Value,
                        CharacterTestAcousticSubjectRole.Body,
                        new AcousticBodyProperties(
                        CharacterTestAcousticMaterialMapper.GenericSolid,
                        mass: 0.65f,
                        contactArea: 0.7f,
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.Solid));
                    receiverContactArea = 0.8f;
                    break;
                case CombatPhysicalContactKind.ObjectImpact:
                    kind = PhysicalAudioInteractionKind.ObjectImpact;
                    mode = PhysicalAudioInteractionMode.Impact;
                    impactor = ResolveCharacterProperties(
                        propertyProvider,
                        contactEvent.SourceEntityId.Value,
                        CharacterTestAcousticSubjectRole.Body,
                        new AcousticBodyProperties(
                        CharacterTestAcousticMaterialMapper.GenericSolid,
                        mass: 0.5f,
                        contactArea: 0.45f,
                        AcousticSurfaceKind.Normal,
                        AcousticConstructionKind.Solid));
                    receiverContactArea = 0.55f;
                    break;
                default:
                    fact = default;
                    return false;
            }

            AcousticBodyProperties receiver = CharacterTestAcousticMaterialMapper.CreateReceiver(
                contactEvent.SurfaceMaterial.Kind,
                receiverContactArea);
            receiver = MergeReceiverBodyProperties(
                propertyProvider,
                CharacterTestAcousticSubjectId.ResolveCharacterEntityId(contactEvent.TargetBodyId.Value),
                receiver);
            float relativeSpeed = contactEvent.RelativeSpeedMillimetersPerSecond * MillimetersToMeters;
            float normalSpeed = contactEvent.NormalSpeedMillimetersPerSecond * MillimetersToMeters;
            float tangentialSpeed = contactEvent.TangentialSpeedMillimetersPerSecond * MillimetersToMeters;
            float pressure = Clamp01(contactEvent.Pressure / ContactPressureReference);
            float intensity = Clamp01(Math.Max(
                contactEvent.Intensity / ContactIntensityReference,
                pressure));
            float impulse = intensity;
            float angleDegrees = 90f;
            PhysicalAudioContactDynamicsSource dynamicsSource =
                PhysicalAudioContactDynamicsSource.Unknown;
            if (contactEvent.Kind == CombatPhysicalContactKind.BodyPartContact
                && dynamics.IsResolved)
            {
                relativeSpeed = dynamics.RelativeSpeedMetersPerSecond;
                normalSpeed = dynamics.NormalSpeedMetersPerSecond;
                tangentialSpeed = dynamics.TangentialSpeedMetersPerSecond;
                pressure = Clamp01(dynamics.AveragePressurePascals / ContactPressureReference);
                impulse = Clamp01(dynamics.NormalImpulseNewtonSeconds / ContactImpulseReference);
                intensity = Math.Max(impulse, pressure);
                angleDegrees = dynamics.AngleDegrees;
                dynamicsSource = dynamics.Source;
            }
            fact = new PhysicalAudioInteractionFact(
                kind,
                mode,
                MapPhase(contactEvent.Phase),
                impactor,
                receiver,
                relativeSpeed,
                normalSpeed,
                tangentialSpeed,
                pressure,
                impulse,
                angleDegrees,
                intensity,
                CreateContactNormal(contactEvent.Normal),
                CreateAudioTransform(contactEvent.Point),
                dynamicsSource,
                ResolveEmitterId(contactEvent),
                contactEvent.Frame.Value,
                contactEvent.Sequence);
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            in CharacterTestWeaponHitAudioEvent hitEvent,
            out PhysicalAudioInteractionFact fact)
        {
            return TryCreate(hitEvent, null, out fact);
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryCreate(
            in CharacterTestWeaponHitAudioEvent hitEvent,
            IAcousticBodyPropertyProvider propertyProvider,
            out PhysicalAudioInteractionFact fact)
        {
            if (hitEvent.ResultKind != HitResolveKind.Damage
                && hitEvent.ResultKind != HitResolveKind.Blocked)
            {
                fact = default;
                return false;
            }

            bool blocked = hitEvent.ResultKind == HitResolveKind.Blocked;
            AcousticBodyProperties impactor = ResolveCharacterProperties(
                propertyProvider,
                hitEvent.SourceEntityId,
                CharacterTestAcousticSubjectRole.Weapon,
                new AcousticBodyProperties(
                    CharacterTestAcousticMaterialMapper.Metal,
                    mass: 0.6f,
                    contactArea: blocked ? 0.25f : 0.2f,
                    AcousticSurfaceKind.Smooth,
                    AcousticConstructionKind.Solid));
            AcousticBodyProperties receiver = CharacterTestAcousticMaterialMapper.CreateReceiver(
                hitEvent.SurfaceMaterial.Kind,
                blocked ? 0.3f : 0.35f);
            receiver = MergeReceiverBodyProperties(propertyProvider, hitEvent.TargetEntityId, receiver);
            float relativeSpeed = Math.Min(50f, hitEvent.ImpactPower * 0.5f);
            float intensity = Clamp01(Math.Max(
                hitEvent.FinalDamage / 40f,
                Math.Max(hitEvent.ImpactPower / 80f, hitEvent.EffectiveImpact / 80f)));
            fact = new PhysicalAudioInteractionFact(
                blocked ? PhysicalAudioInteractionKind.WeaponBlock : PhysicalAudioInteractionKind.WeaponHit,
                blocked ? PhysicalAudioInteractionMode.Block : PhysicalAudioInteractionMode.Hit,
                PhysicalAudioInteractionPhase.Started,
                impactor,
                receiver,
                relativeSpeed,
                normalSpeed: relativeSpeed,
                tangentialSpeed: 0f,
                pressure: intensity,
                impulse: intensity,
                angleDegrees: blocked ? 60f : 90f,
                intensity,
                CreateContactNormal(hitEvent.HitNormalWorld),
                CreateAudioTransform(hitEvent.HitPointWorld),
                CreateWeaponEmitterId(hitEvent),
                hitEvent.Frame.Value,
                hitEvent.TraceId);
            return true;
        }

        private static AcousticBodyProperties ResolveCharacterProperties(
            IAcousticBodyPropertyProvider propertyProvider,
            int combatEntityOrCharacterId,
            CharacterTestAcousticSubjectRole role,
            in AcousticBodyProperties fallback)
        {
            int entityId = CharacterTestAcousticSubjectId.ResolveCharacterEntityId(combatEntityOrCharacterId);
            int subjectId = CharacterTestAcousticSubjectId.ForCharacter(entityId, role);
            return propertyProvider != null
                && propertyProvider.TryGetProperties(subjectId, out AcousticBodyProperties properties)
                    ? properties
                    : fallback;
        }

        private static AcousticBodyProperties MergeReceiverBodyProperties(
            IAcousticBodyPropertyProvider propertyProvider,
            int characterEntityId,
            in AcousticBodyProperties authoritativeSurface)
        {
            int subjectId = CharacterTestAcousticSubjectId.ForCharacter(
                characterEntityId,
                CharacterTestAcousticSubjectRole.Body);
            if (propertyProvider == null
                || !propertyProvider.TryGetProperties(subjectId, out AcousticBodyProperties body))
            {
                return authoritativeSurface;
            }

            // Combat hit/contact material is the target surface authority. The project body cache contributes only
            // the target's mass, contact area and construction, so hit-zone/collider material remains observable.
            return new AcousticBodyProperties(
                authoritativeSurface.MaterialId,
                body.Mass,
                body.ContactArea,
                authoritativeSurface.Surface,
                body.Construction);
        }

        private static PhysicalAudioInteractionPhase MapPhase(CombatPhysicalContactPhase phase)
        {
            switch (phase)
            {
                case CombatPhysicalContactPhase.Started:
                    return PhysicalAudioInteractionPhase.Started;
                case CombatPhysicalContactPhase.Stayed:
                    return PhysicalAudioInteractionPhase.Stayed;
                case CombatPhysicalContactPhase.Ended:
                    return PhysicalAudioInteractionPhase.Ended;
                default:
                    return PhysicalAudioInteractionPhase.Ended;
            }
        }

        private static AudioTransform CreateAudioTransform(FixVector3 point)
        {
            return new AudioTransform(
                ToMeters(point.X),
                ToMeters(point.Y),
                ToMeters(point.Z));
        }

        private static PhysicalAudioDirection CreateContactNormal(FixVector3 normal)
        {
            return new PhysicalAudioDirection(
                ToUnitFloat(normal.X),
                ToUnitFloat(normal.Y),
                ToUnitFloat(normal.Z));
        }

        private static int ResolveEmitterId(in CombatPhysicalContactEvent contactEvent)
        {
            if (!contactEvent.SourceEntityId.IsNone)
            {
                if (contactEvent.Kind == CombatPhysicalContactKind.BodyPartContact
                    && (contactEvent.ContactPointId > 0 || contactEvent.BodyPartId > 0))
                {
                    unchecked
                    {
                        int hash = 37;
                        hash = (hash * 397) ^ contactEvent.SourceEntityId.Value;
                        hash = (hash * 397) ^ contactEvent.ContactPointId;
                        hash = (hash * 397) ^ contactEvent.BodyPartId;
                        hash &= int.MaxValue;
                        return hash == 0 ? contactEvent.SourceEntityId.Value : hash;
                    }
                }

                return contactEvent.SourceEntityId.Value;
            }

            return !contactEvent.SourceBodyId.IsNone
                ? contactEvent.SourceBodyId.Value
                : contactEvent.TargetBodyId.Value;
        }

        private static int CreateWeaponEmitterId(in CharacterTestWeaponHitAudioEvent hitEvent)
        {
            unchecked
            {
                int hash = 53;
                hash = (hash * 397) ^ hitEvent.SourceEntityId;
                hash = (hash * 397) ^ hitEvent.ActionInstanceId;
                hash = (hash * 397) ^ hitEvent.TraceId;
                hash &= int.MaxValue;
                return hash == 0 ? hitEvent.SourceEntityId : hash;
            }
        }

        private static float ToMeters(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private static float ToUnitFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f || float.IsNaN(value))
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}
