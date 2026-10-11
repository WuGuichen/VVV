using System;
using MxFramework.Combat.Physics;

namespace MxFramework.Combat.Levels
{
    public static class CombatLevelWorldBuilder
    {
        public static CombatPhysicsWorld BuildWorld(CombatLevelDefinition definition)
        {
            var world = new CombatPhysicsWorld();
            PopulateWorld(definition, world, clearExisting: false);
            return world;
        }

        public static void PopulateWorld(
            CombatLevelDefinition definition,
            CombatPhysicsWorld world,
            bool clearExisting)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            CombatLevelValidationReport report = CombatLevelValidator.Validate(definition);
            if (report.HasErrors)
            {
                throw new InvalidOperationException(CreateInvalidLevelMessage(report));
            }

            if (clearExisting)
            {
                world.Clear();
            }

            for (int i = 0; i < definition.AabbColliders.Count; i++)
            {
                CombatLevelAabbColliderDefinition collider = definition.AabbColliders[i];
                world.UpsertBody(collider.ToPhysicsBody());
                world.UpsertAabbCollider(collider.ToAabbCollider());
            }

            for (int i = 0; i < definition.ObbColliders.Count; i++)
            {
                CombatLevelObbColliderDefinition collider = definition.ObbColliders[i];
                world.UpsertBody(collider.ToPhysicsBody());
                world.UpsertObbCollider(collider.ToObbCollider());
            }
        }

        private static string CreateInvalidLevelMessage(CombatLevelValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return "Combat level definition is invalid.";
            }

            CombatLevelValidationIssue first = report.Issues[0];
            return "Combat level definition is invalid: "
                + first.Code
                + " at "
                + first.Path
                + ". "
                + first.Message;
        }
    }
}
