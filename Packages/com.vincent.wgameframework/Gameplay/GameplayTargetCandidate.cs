using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Snapshot of targetable gameplay data. Tags and statuses use int ids until 01B types are available.</summary>
    public readonly struct GameplayTargetCandidate
    {
        private readonly IRuntimeEntity _entity;
        private readonly int _entityId;
        private readonly int _teamId;
        private readonly bool _isAlive;
        private readonly int[] _tags;
        private readonly int[] _statuses;

        public GameplayTargetCandidate(
            int entityId,
            int teamId,
            bool isAlive,
            IReadOnlyList<int> tags = null,
            IReadOnlyList<int> statuses = null)
        {
            _entity = null;
            _entityId = entityId;
            _teamId = teamId;
            _isAlive = isAlive;
            _tags = CopyIds(tags);
            _statuses = CopyIds(statuses);
        }

        public GameplayTargetCandidate(
            IRuntimeEntity entity,
            IReadOnlyList<int> tags = null,
            IReadOnlyList<int> statuses = null)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _entity = entity;
            _entityId = entity.EntityId;
            _teamId = entity.TeamId;
            _isAlive = entity.IsAlive;
            _tags = CopyIds(tags);
            _statuses = CopyIds(statuses);
        }

        public GameplayTargetCandidate(
            int entityId,
            int teamId,
            bool isAlive,
            GameplayTagSet tags,
            GameplayStatusSet statuses)
        {
            _entity = null;
            _entityId = entityId;
            _teamId = teamId;
            _isAlive = isAlive;
            _tags = CopyTagIds(tags);
            _statuses = CopyStatusIds(statuses);
        }

        public GameplayTargetCandidate(
            IRuntimeEntity entity,
            GameplayTagSet tags,
            GameplayStatusSet statuses)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _entity = entity;
            _entityId = entity.EntityId;
            _teamId = entity.TeamId;
            _isAlive = entity.IsAlive;
            _tags = CopyTagIds(tags);
            _statuses = CopyStatusIds(statuses);
        }

        public IRuntimeEntity Entity => _entity;

        public int EntityId => _entity != null ? _entity.EntityId : _entityId;

        public int TeamId => _entity != null ? _entity.TeamId : _teamId;

        public bool IsAlive => _entity != null ? _entity.IsAlive : _isAlive;

        public IReadOnlyList<int> Tags => _tags ?? Array.Empty<int>();

        public IReadOnlyList<int> Statuses => _statuses ?? Array.Empty<int>();

        public bool HasTag(int tagId)
        {
            return Contains(_tags, tagId);
        }

        public bool HasStatus(int statusId)
        {
            return Contains(_statuses, statusId);
        }

        private static bool Contains(int[] ids, int id)
        {
            if (ids == null)
                return false;

            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id)
                    return true;
            }

            return false;
        }

        private static int[] CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];

            return copy;
        }

        private static int[] CopyTagIds(GameplayTagSet ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            int index = 0;
            foreach (GameplayTagId id in ids)
                copy[index++] = id.Value;

            return copy;
        }

        private static int[] CopyStatusIds(GameplayStatusSet ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            int index = 0;
            foreach (GameplayStatusId id in ids)
                copy[index++] = id.Value;

            return copy;
        }
    }
}
