using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        internal void SetActionRuntime(CharacterTestCharacterActionRuntime actionRuntime)
        {
            _actionRuntime = actionRuntime;
        }

        internal void SetBakedMotionSource(ICharacterTestBakedWeaponTraceSource bakedMotionSource)
        {
            _bakedMotionSource = bakedMotionSource;
        }

        internal void SetPresentationPoseProvider(
            ICharacterTestBodyPartContactPresentationPoseProvider presentationPoseProvider)
        {
            _presentationPoseProvider = presentationPoseProvider;
        }

        /// <summary>
        /// Registers character contact state and derived probe definitions before the Contact Tick hot path.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc: newly registered characters allocate their fixed-capacity contact runtime here.
        /// Existing registrations do not allocate. Registration stops at the fixed sensor capacity.
        /// </remarks>
        internal void PrepareContactRegistrations(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world?.Entities;
            if (entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                PrepareContactRegistration(entity, physicsRuntime, forceRefresh: false);
            }
        }

        /// <summary>
        /// Registers one newly spawned character before it can enter the Contact Tick hot path.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc: called from the runtime world's entity-spawn seam. Registration stops at the fixed sensor
        /// capacity; the later hot path counts and safely skips an unregistered entity instead of expanding storage.
        /// </remarks>
        internal void PrepareContactRegistration(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            PrepareContactRegistration(entity, physicsRuntime, forceRefresh: false);
        }

        private void PrepareContactRegistration(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool forceRefresh)
        {
            if (entity == null)
                return;

            int entityId = entity.Id.Value;
            if (!_characterContactAudioStates.ContainsKey(entityId))
            {
                if (_characterContactAudioStates.Count >= PhysicalAudioSensorCapacity)
                    return;
                _characterContactAudioStates.Add(
                    entityId,
                    CharacterBodyPartContactAudioState.CreateInitial());
            }

            if (!_bodyPartContactRuntimes.ContainsKey(entityId))
            {
                if (_bodyPartContactRuntimes.Count >= PhysicalAudioSensorCapacity)
                    return;
                _bodyPartContactRuntimes.Add(
                    entityId,
                    new CombatBodyPartContactPointRuntime(
                        PhysicalAudioSensorCapacity,
                        PhysicalAudioQueryHitCapacity));
            }

            if (!forceRefresh && _contactRegistrations.ContainsKey(entityId))
                return;

            if (forceRefresh
                && _bodyPartContactRuntimes.TryGetValue(
                    entityId,
                    out CombatBodyPartContactPointRuntime contactRuntime))
            {
                contactRuntime.Reset();
            }

            CharacterImportedPackage package = ResolveCharacterPackage(entity);
            CharacterBodyPartContactPointConfig[] configs = ResolveBodyPartContactPointConfigs(package);
            CharacterBodyPartContactRuntimeBinding[] bindings =
                BuildContactRuntimeBindings(
                    package,
                    configs,
                    physicsRuntime,
                    entity.Identity.GeneratedPackageRoot);
            var registration = new CharacterBodyPartContactRegistration(bindings, package?.Configs?.BodyParts, package?.Geometry);
            if (_contactRegistrations.ContainsKey(entityId))
                _contactRegistrations[entityId] = registration;
            else if (_contactRegistrations.Count < PhysicalAudioSensorCapacity)
                _contactRegistrations.Add(entityId, registration);

            for (int configIndex = 0; configIndex < configs.Length; configIndex++)
            {
                BodyPartContactDerivedKey key = BodyPartContactDerivedKey.From(
                    physicsRuntime,
                    configs[configIndex]);
                if (_bodyPartContactDerivedDefinitions.ContainsKey(key))
                    continue;
                if (_bodyPartContactDerivedDefinitions.Count >= PhysicalAudioSensorCapacity)
                    break;
                _bodyPartContactDerivedDefinitions.Add(
                    key,
                    BodyPartContactDerivedDefinition.Create(
                        configs[configIndex],
                        ResolveContactPointLayerMask(
                            physicsRuntime,
                            configs[configIndex]?.LayerMaskProfileId)));
            }
        }

        /// <summary>
        /// Explicit low-frequency refresh seam for changed character package, action config, baked source or contact
        /// config. Replaces the entity's immutable precompiled binding before its next Contact Tick.
        /// </summary>
        /// <remarks>LowFreqAlloc. Never call from Tick.</remarks>
        internal void RefreshContactRegistration(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            PrepareContactRegistration(entity, physicsRuntime, forceRefresh: true);
        }

        /// <summary>
        /// Releases one removed character's fixed-capacity contact registration and rebuilds shared probe
        /// definitions from the remaining world.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc: invoked by the runtime world's entity-removal seam, never from Contact Tick. Clearing the
        /// resolved cooldown cache also prevents departed emitters from exhausting its fixed capacity in long sessions.
        /// </remarks>
        internal void RemoveContactRegistration(
            CharacterTestCharacterEntityId entityId,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            if (!entityId.IsValid)
                return;

            _characterContactAudioStates.Remove(entityId.Value);
            _bodyPartContactRuntimes.Remove(entityId.Value);
            _contactRegistrations.Remove(entityId.Value);
            ResetTransientContactState();

            // Derived probe definitions are shared by package/config identity rather than entity id. Rebuild them
            // from the still-active world so sequentially removed packages cannot consume the fixed dictionary forever.
            _bodyPartContactDerivedDefinitions.Clear();
            PrepareContactRegistrations(world, physicsRuntime);
        }

        /// <summary>Releases all fixed-capacity contact registration after the runtime world is cleared.</summary>
        /// <remarks>LowFreqAlloc: invoked only by the world-clear lifecycle seam.</remarks>
        internal void ClearContactRegistrations()
        {
            _characterContactAudioStates.Clear();
            _bodyPartContactRuntimes.Clear();
            _contactRegistrations.Clear();
            _bodyPartContactDerivedDefinitions.Clear();
            ResetTransientContactState();
        }

        private void ResetTransientContactState()
        {
            _contactEvents.Clear();
            _bodyPartContactEvents.Clear();
            _bodyPartContactSamples.Clear();
            _bodyPartContactDebugEntryIndices.Clear();
            _bodyPartContactDebugCount = 0;
            _bodyPartContactDebugFrame = default;
            _resolvedContactCooldowns.Clear();
        }
    }
}
