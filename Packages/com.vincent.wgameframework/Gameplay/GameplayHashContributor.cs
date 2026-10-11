using System;
using System.Collections.Generic;
using MxFramework.Modifiers;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Contributes deterministic gameplay entity state to the runtime result hash.</summary>
    public sealed class GameplayHashContributor : IRuntimeHashContributor
    {
        public const string StableContributorId = "mxframework.gameplay.world";

        private static class HashFields
        {
            public const int WorldPresent = 1;
            public const int WorldTick = 2;
            public const int AttributeId = 3;
            public const int AttributeIds = 4;
            public const int EntityCount = 5;
            public const int Entities = 6;
            public const int EntityIndex = 1;
            public const int EntityId = 2;
            public const int EntityTeam = 3;
            public const int EntityAlive = 4;
            public const int EntityAttributes = 5;
            public const int EntityAttributeId = 1;
            public const int EntityAttributeFinal = 2;
            public const int EntityModifiers = 6;
            public const int ModifierId = 1;
            public const int ModifierParamIndex = 2;
        }

        private readonly GameplayWorld _world;
        private readonly IReadOnlyList<IRuntimeEntity> _entities;
        private readonly int[] _attributeIds;

        public GameplayHashContributor(
            IReadOnlyList<IRuntimeEntity> entities,
            IReadOnlyList<int> attributeIds)
            : this(StableContributorId, entities, attributeIds)
        {
        }

        public GameplayHashContributor(
            string contributorId,
            IReadOnlyList<IRuntimeEntity> entities,
            IReadOnlyList<int> attributeIds)
        {
            if (string.IsNullOrEmpty(contributorId))
                throw new ArgumentException("Gameplay hash contributor id cannot be null or empty.", nameof(contributorId));

            DiagnosticContributorId = contributorId;
            _world = null;
            _entities = entities ?? Array.Empty<IRuntimeEntity>();
            _attributeIds = CopyDistinctSorted(attributeIds);
        }

        public GameplayHashContributor(
            GameplayWorld world,
            IReadOnlyList<int> attributeIds)
            : this(StableContributorId, world, attributeIds)
        {
        }

        public GameplayHashContributor(
            string contributorId,
            GameplayWorld world,
            IReadOnlyList<int> attributeIds)
        {
            if (string.IsNullOrEmpty(contributorId))
                throw new ArgumentException("Gameplay hash contributor id cannot be null or empty.", nameof(contributorId));
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            DiagnosticContributorId = contributorId;
            _world = world;
            _entities = Array.Empty<IRuntimeEntity>();
            _attributeIds = CopyDistinctSorted(attributeIds);
        }

        public int ContributorId => RuntimeHashContributorIds.GameplayWorld;
        public int HashSchemaId => RuntimeHashSchemaIds.GameplayWorld;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;
        public string DiagnosticContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            IReadOnlyList<IRuntimeEntity> sourceEntities = ResolveEntities(accumulator);
            EntityEntry[] entities = CopyAndSortEntities(sourceEntities);
            accumulator.AddInt(HashFields.EntityCount, entities.Length);
            accumulator.BeginArray(HashFields.AttributeIds, _attributeIds.Length);
            for (int i = 0; i < _attributeIds.Length; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(HashFields.AttributeId, _attributeIds[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            accumulator.BeginArray(HashFields.Entities, entities.Length);
            for (int i = 0; i < entities.Length; i++)
            {
                accumulator.BeginElement(i);
                AddEntity(accumulator, i, entities[i].Entity);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        private IReadOnlyList<IRuntimeEntity> ResolveEntities(RuntimeHashAccumulator accumulator)
        {
            if (_world == null)
            {
                accumulator.AddInt(HashFields.WorldPresent, 0);
                return _entities;
            }

            accumulator.AddInt(HashFields.WorldPresent, 1);
            accumulator.AddLong(HashFields.WorldTick, _world.TickCount);
            return _world.Entities.CreateSnapshot();
        }

        private void AddEntity(RuntimeHashAccumulator accumulator, int index, IRuntimeEntity entity)
        {
            accumulator.AddInt(HashFields.EntityIndex, index);
            accumulator.AddInt(HashFields.EntityId, entity.EntityId);
            accumulator.AddInt(HashFields.EntityTeam, entity.TeamId);
            accumulator.AddInt(HashFields.EntityAlive, entity.IsAlive ? 1 : 0);

            accumulator.BeginArray(HashFields.EntityAttributes, _attributeIds.Length);
            for (int i = 0; i < _attributeIds.Length; i++)
            {
                int attributeId = _attributeIds[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(HashFields.EntityAttributeId, attributeId);
                accumulator.AddInt(HashFields.EntityAttributeFinal, entity.Attributes.GetAttribute(attributeId));
                accumulator.EndElement();
            }
            accumulator.EndArray();

            AddModifiers(accumulator, entity);
        }

        private static void AddModifiers(RuntimeHashAccumulator accumulator, IRuntimeEntity entity)
        {
            ModifierSnapshot[] modifiers = entity is RuntimeEntity runtimeEntity && runtimeEntity.Modifiers != null
                ? runtimeEntity.Modifiers.CreateSnapshot() ?? Array.Empty<ModifierSnapshot>()
                : Array.Empty<ModifierSnapshot>();

            Array.Sort(modifiers, CompareModifiers);
            accumulator.BeginArray(HashFields.EntityModifiers, modifiers.Length);

            for (int i = 0; i < modifiers.Length; i++)
            {
                ModifierSnapshot modifier = modifiers[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(HashFields.ModifierId, modifier.Id);
                accumulator.AddInt(HashFields.ModifierParamIndex, modifier.ParamIndex);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        private static int[] CopyDistinctSorted(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<int>();

            var copy = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];

            Array.Sort(copy);

            int count = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                if (count > 0 && copy[count - 1] == copy[i])
                    continue;

                copy[count++] = copy[i];
            }

            if (count == copy.Length)
                return copy;

            var distinct = new int[count];
            Array.Copy(copy, distinct, count);
            return distinct;
        }

        private static EntityEntry[] CopyAndSortEntities(IReadOnlyList<IRuntimeEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<EntityEntry>();

            var entries = new EntityEntry[entities.Count];
            for (int i = 0; i < entities.Count; i++)
            {
                IRuntimeEntity entity = entities[i];
                if (entity == null)
                    throw new ArgumentException("Gameplay hash entity list cannot contain null entries.", nameof(entities));

                entries[i] = new EntityEntry(entity);
            }

            Array.Sort(entries, CompareEntities);

            for (int i = 1; i < entries.Length; i++)
            {
                if (entries[i - 1].Entity.EntityId == entries[i].Entity.EntityId)
                    throw new ArgumentException("Gameplay hash entity ids must be unique. Duplicate id: " + entries[i].Entity.EntityId, nameof(entities));
            }

            return entries;
        }

        private static int CompareEntities(EntityEntry left, EntityEntry right)
        {
            return left.Entity.EntityId.CompareTo(right.Entity.EntityId);
        }

        private static int CompareModifiers(ModifierSnapshot left, ModifierSnapshot right)
        {
            int id = left.Id.CompareTo(right.Id);
            return id != 0 ? id : left.ParamIndex.CompareTo(right.ParamIndex);
        }

        private readonly struct EntityEntry
        {
            public EntityEntry(IRuntimeEntity entity)
            {
                Entity = entity;
            }

            public IRuntimeEntity Entity { get; }
        }
    }
}
