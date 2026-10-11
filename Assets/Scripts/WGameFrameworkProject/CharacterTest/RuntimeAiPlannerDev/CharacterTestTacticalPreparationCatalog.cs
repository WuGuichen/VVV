using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns isolated authoring data only: no combat module, dummy, simulation or views.</summary>
    /// <remarks>LowFreqAlloc on cache miss. Never bind a live action runtime to this private world.</remarks>
    internal sealed class CharacterTestTacticalRoleData
    {
        private readonly CharacterTestCharacterRuntimeWorld _world;
        internal CharacterTestCharacterActionRuntime Actions { get; }
        internal CharacterTestCharacterEntity Source { get; }
        internal CharacterTestCharacterPhysicsRuntime Physics { get; }
        internal CharacterTestTacticalRoleData(int character, CharacterTestCharacterActionRuntime catalog)
        {
            var definitions = new CharacterTestCharacterDefinitionRegistry();
            if (!definitions.TryGet(character, out var definition)) throw new ArgumentException("Source character definition is unavailable.");
            _world = new CharacterTestCharacterRuntimeWorld();
            Actions = new CharacterTestCharacterActionRuntime(catalog.ImportResult, catalog.Registry, catalog.ValidationReport,
                catalog.ActionSet, catalog.ConfigSourceKind, catalog.ConfigSourceDescription, catalog.ActionSetsSourcePath,
                catalog.ActionsSourcePath, catalog.ConfigSourceDiagnostics.ToArray());
            Actions.BindRuntimeWorld(_world);
            Source = _world.Spawn(new CharacterTestCharacterSpawnRequest(character, 2, 0, 0, 0, CharacterTestCharacterControlOwner.None), definition);
            // Reuse the same authored-rig registration as the formal runtime, including its resource validity rules.
            new CharacterTestAuthorityPoseTerrainSystem(Actions).Register(Source);
            Physics = new CharacterTestCharacterPhysicsRuntime(new CombatPhysicsWorld(), CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), Fix64.Zero, Fix64.Zero, staticColliderCount: 0,
                sourceDescription: "tactical-authoring-data");
        }
        internal CharacterActionTacticalInput Input(int actionId)
        {
            if (!Actions.Registry.TryGetAction(new CharacterActionId(actionId), out var action)) throw new ArgumentException("Action unavailable: " + actionId);
            return new CharacterActionTacticalInput(action, Source, Physics, CharacterTestDefaultBakedWeaponTraceSource.Instance,
                CharacterTestDefaultWeaponTraceBindingResolver.Instance);
        }
    }

    internal static class CharacterTestTacticalPreparationCatalog
    {
        private static readonly Dictionary<(int Character, object Registry), CharacterTestTacticalRoleData> Roles = new Dictionary<(int, object), CharacterTestTacticalRoleData>();
        internal static long Revision { get; private set; }
        internal static long RoleBuildCount { get; private set; }
        internal static CharacterTestCharacterActionRuntime Catalog => CharacterTestCharacterActionConfigSource.LoadInspectionCatalog();
        internal static CharacterTestTacticalRoleData Role(int character, CharacterTestCharacterActionRuntime loadedCatalog = null)
        {
            var catalog = loadedCatalog ?? Catalog;
            var key = (character, (object)catalog.Registry);
            if (!Roles.TryGetValue(key, out var role))
            {
                role = new CharacterTestTacticalRoleData(character, catalog);
                if (Roles.Count >= 32) Roles.Clear();
                Roles.Add(key, role); RoleBuildCount++;
            }
            return role;
        }
        internal static CharacterActionTacticalInput FallbackInput(int character, int action) => Role(character).Input(action);
        internal static void SummaryChanged() { Revision++; }
        internal static void Invalidate() { Roles.Clear(); CharacterTestCharacterActionConfigSource.InvalidateInspectionCatalog(); Revision++; }
    }
}
