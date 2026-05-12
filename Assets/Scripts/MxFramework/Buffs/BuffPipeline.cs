using System;
using System.Collections.Generic;

namespace MxFramework.Buffs
{
    public sealed class BuffPipeline : IBuffPipeline
    {
        private readonly Dictionary<int, IBuff> _buffs;
        private readonly List<int> _expired;
        private readonly IBuffFactory _factory;
        private readonly IBuffStackingPolicy _stackingPolicy;
        private IBuffTarget _target;

        public BuffPipeline(
            IBuffFactory factory = null,
            IBuffStackingPolicy stackingPolicy = null,
            int initialCapacity = 8)
        {
            if (initialCapacity <= 0)
                initialCapacity = 8;

            _factory = factory;
            _stackingPolicy = stackingPolicy ?? new DefaultBuffStackingPolicy();
            _buffs = new Dictionary<int, IBuff>(initialCapacity);
            _expired = new List<int>(initialCapacity);
        }

        public IBuff AddBuff(IBuff buff, IBuffTarget target)
        {
            if (buff == null)
                throw new ArgumentNullException(nameof(buff));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            _target = target;

            if (_buffs.TryGetValue(buff.Id, out IBuff existing))
            {
                BuffStackResult result = _stackingPolicy.Apply(existing, buff);
                if (!result.KeepExisting)
                    RemoveBuff(existing.Id);

                if (result.RefreshDuration && result.KeepExisting)
                {
                    existing.RefreshDuration();
                    Publish(new BuffEvent(existing.Id, BuffEventType.DurationRefreshed, 0, buff));
                }

                if (result.LayerDelta != 0 && result.KeepExisting)
                    Publish(new BuffEvent(existing.Id, BuffEventType.LayerChanged, result.LayerDelta, buff));

                if (!result.AttachIncoming)
                    return result.KeepExisting ? existing : null;
            }

            _buffs[buff.Id] = buff;
            buff.OnAttach(target);
            Publish(new BuffEvent(buff.Id, BuffEventType.Added, buff.CurrentLayers, buff));
            return buff;
        }

        public bool TryAddBuff(int buffId, IBuffTarget target, out IBuff buff)
        {
            buff = null;
            if (_factory == null || !_factory.TryCreate(buffId, out buff))
                return false;

            buff = AddBuff(buff, target);
            return buff != null;
        }

        public bool RemoveBuff(int buffId)
        {
            if (!_buffs.TryGetValue(buffId, out IBuff buff))
                return false;

            _buffs.Remove(buffId);
            buff.OnDetach(_target);
            Publish(new BuffEvent(buffId, BuffEventType.Removed, -buff.CurrentLayers, buff));
            return true;
        }

        public void RemoveAllBuffs()
        {
            if (_buffs.Count == 0)
                return;

            _expired.Clear();
            foreach (int id in _buffs.Keys)
                _expired.Add(id);

            for (int i = 0; i < _expired.Count; i++)
                RemoveBuff(_expired[i]);

            _expired.Clear();
        }

        public void TickAll(float deltaTime)
        {
            if (_target == null || _buffs.Count == 0)
                return;

            _expired.Clear();
            foreach (KeyValuePair<int, IBuff> pair in _buffs)
            {
                IBuff buff = pair.Value;
                buff.OnTick(deltaTime, _target);
                Publish(new BuffEvent(buff.Id, BuffEventType.Tick, 0, buff));
                if (buff.IsExpired)
                    _expired.Add(pair.Key);
            }

            for (int i = 0; i < _expired.Count; i++)
                RemoveBuff(_expired[i]);

            _expired.Clear();
        }

        public IBuff GetBuff(int buffId)
        {
            return _buffs.TryGetValue(buffId, out IBuff buff) ? buff : null;
        }

        public bool TryGetBuff(int buffId, out IBuff buff)
        {
            return _buffs.TryGetValue(buffId, out buff);
        }

        public bool HasBuff(int buffId)
        {
            return _buffs.ContainsKey(buffId);
        }

        public int GetBuffLayer(int buffId)
        {
            return _buffs.TryGetValue(buffId, out IBuff buff) ? buff.CurrentLayers : 0;
        }

        public BuffSnapshot[] CreateSnapshot()
        {
            var snapshots = new BuffSnapshot[_buffs.Count];
            int index = 0;
            foreach (IBuff buff in _buffs.Values)
                snapshots[index++] = new BuffSnapshot(buff);
            return snapshots;
        }

        private void Publish(in BuffEvent evt)
        {
            if (_target != null)
                _target.BuffEvents.Publish(evt);
        }
    }
}
