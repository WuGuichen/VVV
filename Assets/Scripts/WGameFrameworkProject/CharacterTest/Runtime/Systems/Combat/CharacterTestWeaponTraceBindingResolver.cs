using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestWeaponTraceBindingResolver
    {
        bool TryResolve(
            CharacterTestCharacterEntity entity,
            int equipmentSlotId,
            int traceProfileId,
            out CharacterTestWeaponTraceBinding binding);
    }

    internal sealed class CharacterTestDefaultWeaponTraceBindingResolver : ICharacterTestWeaponTraceBindingResolver
    {
        private static CharacterTestDefaultWeaponTraceBindingResolver _instance;
        public static CharacterTestDefaultWeaponTraceBindingResolver Instance =>
            _instance ?? (_instance = new CharacterTestDefaultWeaponTraceBindingResolver());

        /// <remarks>Offline authoring/measurement bootstrap only. Existing readers are not mutated.</remarks>
        internal static void InvalidateCachedInstanceForMeasurement()
        { _instance = null; CharacterTestTacticalPreparationCatalog.Invalidate(); }

        private CharacterTestDefaultWeaponTraceBindingResolver()
        {
        }

        public bool TryResolve(
            CharacterTestCharacterEntity entity,
            int equipmentSlotId,
            int traceProfileId,
            out CharacterTestWeaponTraceBinding binding)
        {
            if (entity != null
                && entity.Equipment.UseDefaultLoadout
                && CharacterWeaponTraceBindingResolver.TryResolve(
                    string.IsNullOrWhiteSpace(entity.Identity.GeneratedPackageRoot)
                        ? CharacterTestGeneratedPackageSource.LoadDefaultCharacterPackage()
                        : CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity),
                    equipmentSlotId,
                    traceProfileId,
                    out CharacterResolvedWeaponTraceBinding resolved,
                    out _))
            {
                binding = new CharacterTestWeaponTraceBinding(
                    resolved.TraceId,
                    length: resolved.Length,
                    radius: resolved.Radius,
                    traceBinding: resolved.TraceBinding,
                    equipmentSlotId: resolved.EquipmentSlotId,
                    weaponId: resolved.WeaponId,
                    proximalExtension: resolved.ProximalExtension,
                    proximalRadius: resolved.ProximalRadius);
                return true;
            }

            binding = default;
            return false;
        }
    }
}
