namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>Resolves a presentation style from the same equipped weapon/slot authority as hit traces.</summary>
    /// <remarks>LowFreqAlloc through trace diagnostics on failed resolution; called once per presentation spawn.</remarks>
    public static class CharacterWeaponBladeTrailStyleResolver
    {
        public static bool TryResolve(CharacterImportedPackage package, int equipmentSlotId,
            int traceProfileId, out string resourceKey, out int residueMilliseconds)
            => TryResolve(package, equipmentSlotId, traceProfileId, out resourceKey, out residueMilliseconds, out _);

        public static bool TryResolve(CharacterImportedPackage package, int equipmentSlotId,
            int traceProfileId, out string resourceKey, out int residueMilliseconds, out int styleId)
        {
            resourceKey = string.Empty;
            residueMilliseconds = 0;
            styleId = 0;
            if (equipmentSlotId <= 0 || !CharacterWeaponTraceBindingResolver.TryResolve(package, equipmentSlotId, traceProfileId,
                out CharacterResolvedWeaponTraceBinding trace, out _)) return false;
            if (trace.TraceBinding.EquipSlot > 0)
            {
                if (trace.TraceBinding.EquipSlot != equipmentSlotId) return false;
            }
            else if (trace.Attachment == null || trace.Attachment.EquipSlot != equipmentSlotId)
                return false;
            foreach (var weapon in package.Configs.Weapons)
            {
                if (weapon == null || weapon.Id != trace.WeaponId) continue;
                if (string.IsNullOrWhiteSpace(weapon.BladeTrailResourceKey)) return false;
                resourceKey = weapon.BladeTrailResourceKey;
                residueMilliseconds = weapon.BladeTrailResidueMilliseconds;
                styleId = weapon.BladeTrailStyleId;
                return styleId >= 0 && styleId <= 4
                    && (residueMilliseconds == 0 || (residueMilliseconds >= 80 && residueMilliseconds <= 150));
            }
            return false;
        }
    }
}
