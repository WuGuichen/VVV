using System;
using System.Collections.Generic;
using MxFramework.Buffs;
using MxFramework.Modifiers;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Contributes deterministic gameplay entity state to the runtime result hash.</summary>
    public sealed class GameplayHashContributor : IRuntimeHashContributor
    {
        public const string StableContributorId = "mxframework.gameplay.world";

        private const double TimeScale = 1000d;

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

            ContributorId = contributorId;
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

            ContributorId = contributorId;
            _world = world;
            _entities = Array.Empty<IRuntimeEntity>();
            _attributeIds = CopyDistinctSorted(attributeIds);
        }

        public string ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            IReadOnlyList<IRuntimeEntity> sourceEntities = ResolveEntities(accumulator);
            EntityEntry[] entities = CopyAndSortEntities(sourceEntities);
            accumulator.AddInt("gameplay.entity.count", entities.Length);
            accumulator.AddInt("gameplay.attribute.id.count", _attributeIds.Length);

            for (int i = 0; i < _attributeIds.Length; i++)
                accumulator.AddInt("gameplay.attribute.id", _attributeIds[i]);

            for (int i = 0; i < entities.Length; i++)
                AddEntity(accumulator, i, entities[i].Entity);
        }

        private IReadOnlyList<IRuntimeEntity> ResolveEntities(RuntimeHashAccumulator accumulator)
        {
            if (_world == null)
            {
                accumulator.AddInt("gameplay.world.present", 0);
                return _entities;
            }

            accumulator.AddInt("gameplay.world.present", 1);
            accumulator.AddLong("gameplay.world.tick", _world.TickCount);
            return _world.Entities.CreateSnapshot();
        }

        private void AddEntity(RuntimeHashAccumulator accumulator, int index, IRuntimeEntity entity)
        {
            accumulator.AddInt("gameplay.entity.index", index);
            accumulator.AddInt("gameplay.entity.id", entity.EntityId);
            accumulator.AddInt("gameplay.entity.team", entity.TeamId);
            accumulator.AddInt("gameplay.entity.alive", entity.IsAlive ? 1 : 0);

            accumulator.AddInt("gameplay.entity.attribute.count", _attributeIds.Length);
            for (int i = 0; i < _attributeIds.Length; i++)
            {
                int attributeId = _attributeIds[i];
                accumulator.AddInt("gameplay.entity.attribute.id", attributeId);
                accumulator.AddInt("gameplay.entity.attribute.final", entity.Attributes.GetAttribute(attributeId));
            }

            AddBuffs(accumulator, entity);
            AddModifiers(accumulator, entity);
        }

        private static void AddBuffs(RuntimeHashAccumulator accumulator, IRuntimeEntity entity)
        {
            BuffSnapshot[] buffs = entity.BuffPipeline == null
                ? Array.Empty<BuffSnapshot>()
                : entity.BuffPipeline.CreateSnapshot() ?? Array.Empty<BuffSnapshot>();

            Array.Sort(buffs, CompareBuffs);
            accumulator.AddInt("gameplay.entity.buff.count", buffs.Length);

            for (int i = 0; i < buffs.Length; i++)
            {
                BuffSnapshot buff = buffs[i];
                accumulator.AddInt("gameplay.entity.buff.index", i);
                accumulator.AddInt("gameplay.entity.buff.id", buff.Id);
                accumulator.AddInt("gameplay.entity.buff.layers", buff.CurrentLayers);
                accumulator.AddInt("gameplay.entity.buff.maxLayers", buff.MaxLayers);
                accumulator.AddInt("gameplay.entity.buff.permanent", buff.IsPermanent ? 1 : 0);
                accumulator.AddInt("gameplay.entity.buff.expired", !buff.IsPermanent && buff.RemainingTime <= 0f ? 1 : 0);
                AddSingle(accumulator, "gameplay.entity.buff.duration", buff.Duration);
                AddSingle(accumulator, "gameplay.entity.buff.remaining", buff.RemainingTime);
            }
        }

        private static void AddModifiers(RuntimeHashAccumulator accumulator, IRuntimeEntity entity)
        {
            ModifierSnapshot[] modifiers = entity is RuntimeEntity runtimeEntity && runtimeEntity.Modifiers != null
                ? runtimeEntity.Modifiers.CreateSnapshot() ?? Array.Empty<ModifierSnapshot>()
                : Array.Empty<ModifierSnapshot>();

            Array.Sort(modifiers, CompareModifiers);
            accumulator.AddInt("gameplay.entity.modifier.count", modifiers.Length);

            for (int i = 0; i < modifiers.Length; i++)
            {
                ModifierSnapshot modifier = modifiers[i];
                accumulator.AddInt("gameplay.entity.modifier.index", i);
                accumulator.AddInt("gameplay.entity.modifier.id", modifier.Id);
                accumulator.AddInt("gameplay.entity.modifier.paramIndex", modifier.ParamIndex);
            }
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

        private static int CompareBuffs(BuffSnapshot left, BuffSnapshot right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static int CompareModifiers(ModifierSnapshot left, ModifierSnapshot right)
        {
            int id = left.Id.CompareTo(right.Id);
            return id != 0 ? id : left.ParamIndex.CompareTo(right.ParamIndex);
        }

        private static void AddSingle(RuntimeHashAccumulator accumulator, string key, float value)
        {
            if (float.IsNaN(value))
                throw new ArgumentException("Gameplay hash float value must not be NaN.", nameof(value));

            if (float.IsPositiveInfinity(value))
            {
                accumulator.AddInt(key + ".kind", 1);
                return;
            }

            if (float.IsNegativeInfinity(value))
            {
                accumulator.AddInt(key + ".kind", -1);
                return;
            }

            accumulator.AddInt(key + ".kind", 0);
            accumulator.AddDoubleQuantized(key, value, TimeScale);
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
