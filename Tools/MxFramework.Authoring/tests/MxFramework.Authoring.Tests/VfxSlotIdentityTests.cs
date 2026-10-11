using System;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class VfxSlotIdentityTests
{
    public static int Run(Action<bool, string> check = null)
    {
        int passed = 0;
        void Assert(bool valid, string name)
        {
            if (!valid) throw new InvalidOperationException(name);
            check?.Invoke(valid, name);
            passed++;
        }
        var oldKey = new AbilityResourceKeyConfig { Id = "old", TypeId = "GameObject", PackageId = "test" };
        var newKey = new AbilityResourceKeyConfig { Id = "new", TypeId = "GameObject", PackageId = "test" };
        var definitions = new[] { new VfxDefinitionConfig { DefinitionId = 12002, PrefabResource = oldKey }, new VfxDefinitionConfig { DefinitionId = 20000, PrefabResource = newKey } };
        foreach (string kind in new[] { "Spawn", "DirectImpact", "AreaImpact", "BlockedImpact", "Expire" })
        {
            var slot = new ProjectilePresentationSlotConfig { Resource = newKey, VfxDefinitionId = 12002 };
            var slots = new ProjectilePresentationSlotSetConfig();
            typeof(ProjectilePresentationSlotSetConfig).GetProperty(kind).SetValue(slots, slot);
            var binding = new ProjectilePresentationBindingConfig { Slots = slots };
            AbilityAuthoringCompiler.ResolvePresentationSlotIds(binding, definitions);
            Assert(slot.VfxDefinitionId == 20000, kind + " re-resolves stale positive ID");
            slot.Resource = new AbilityResourceKeyConfig { Id = "missing", TypeId = "GameObject" };
            AbilityAuthoringCompiler.ResolvePresentationSlotIds(binding, definitions);
            Assert(slot.VfxDefinitionId == 0, kind + " clears unresolved cached ID");
            slot.VfxDefinitionId = 12002;
            AbilityAuthoringCompiler.ResolvePresentationSlotIds(binding, null);
            Assert(slot.VfxDefinitionId == 0, kind + " no catalog fails closed");
        }
        return passed;
    }
}
