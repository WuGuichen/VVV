using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;

namespace MxFramework.Preview
{
    /// <summary>
    /// Default <see cref="IPreviewWorld"/>: two dummy IBuffTargets (caster + target) backed
    /// by AttributeStore. Pure framework, no WGame characters / levels / assets.
    /// </summary>
    public sealed class DummyPreviewWorld : IPreviewWorld, IRuntimePreviewModeSource, IRuntimePreviewFailureSource
    {
        // Reserved attribute ids for the dummy world. WGame layer maps real ids; we keep
        // names stable for the JSON DTO so the editor still gets human-readable labels.
        public const int AttrHp = 1;
        public const int AttrAttack = 2;

        private readonly IBuffFactory _buffFactory;
        private readonly Dictionary<string, DummyTarget> _targets = new Dictionary<string, DummyTarget>(StringComparer.Ordinal);
        private readonly Dictionary<string, DummyTarget> _casters = new Dictionary<string, DummyTarget>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuffPipeline> _pipelines = new Dictionary<string, BuffPipeline>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<AttributeChange>> _attrChanges = new Dictionary<string, List<AttributeChange>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<int, string>> _buffCasterByTarget = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<int, DateTime>> _buffAddedAt = new Dictionary<string, Dictionary<int, DateTime>>(StringComparer.Ordinal);
        private readonly List<DamageTick> _damageTicks = new List<DamageTick>();
        private readonly List<StatusChange> _statusChanges = new List<StatusChange>();
        private readonly PreviewDamageByAttrFactory _previewFactory;
        private string _lastFailureReason = string.Empty;
        private string _lastFailureMessage = string.Empty;
        // tick interval used when applying Tick(frames)
        public float SecondsPerFrame { get; set; } = 1f / 60f;

        public DummyPreviewWorld(IBuffFactory buffFactory)
        {
            _buffFactory = buffFactory;
            _previewFactory = buffFactory as PreviewDamageByAttrFactory;
        }

        public string PreviewMode => "dummy";
        public string FallbackReason => _buffFactory == null ? "Dummy world has no IBuffFactory." : string.Empty;
        public string LastFailureReason => _lastFailureReason;
        public string LastFailureMessage => _lastFailureMessage;

        public IBuffTarget GetOrCreateTarget(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) targetId = "TestTarget";
            if (!_targets.TryGetValue(targetId, out DummyTarget t))
            {
                t = new DummyTarget(targetId);
                t.Store.RegisterAttribute(AttrHp, 1000);
                t.Store.RegisterAttribute(AttrAttack, 100);
                _targets[targetId] = t;
                _attrChanges[targetId] = new List<AttributeChange>();
                _buffCasterByTarget[targetId] = new Dictionary<int, string>();
                _buffAddedAt[targetId] = new Dictionary<int, DateTime>();
                BuffPipeline pipe = new BuffPipeline(_buffFactory);
                _pipelines[targetId] = pipe;
                t.Store.OnAttributeChanged.Subscribe(evt =>
                {
                    _attrChanges[targetId].Add(new AttributeChange
                    {
                        OwnerId = targetId,
                        Attribute = AttributeName(evt.AttributeId),
                        Before = evt.OldValue,
                        After = evt.NewValue,
                        DeltaSource = evt.Source != null ? evt.Source.ToString() : string.Empty,
                    });
                });
            }
            return t;
        }

        public IBuffTarget GetOrCreateCaster(string casterId)
        {
            if (string.IsNullOrEmpty(casterId)) casterId = "TestCaster";
            if (!_casters.TryGetValue(casterId, out DummyTarget c))
            {
                c = new DummyTarget(casterId);
                c.Store.RegisterAttribute(AttrHp, 1000);
                c.Store.RegisterAttribute(AttrAttack, 100);
                _casters[casterId] = c;
            }
            return c;
        }

        public void Reset(bool reloadBase)
        {
            foreach (KeyValuePair<string, BuffPipeline> kv in _pipelines)
                kv.Value.RemoveAllBuffs();
            _targets.Clear();
            _casters.Clear();
            _pipelines.Clear();
            _attrChanges.Clear();
            _buffCasterByTarget.Clear();
            _buffAddedAt.Clear();
            _damageTicks.Clear();
            _statusChanges.Clear();
            ClearFailure();
            _previewFactory?.Clear();
            // reloadBase is a no-op in the dummy world; base data is just the constants above.
        }

        public void LoadPreviewPatch(string sourceJson)
        {
            _previewFactory?.LoadPatch(sourceJson);
        }

        public void Tick(int frames)
        {
            if (frames <= 0) return;
            float dt = SecondsPerFrame;
            for (int f = 0; f < frames; f++)
            {
                foreach (KeyValuePair<string, BuffPipeline> kv in _pipelines)
                    kv.Value.TickAll(dt);
            }
        }

        public IReadOnlyList<BuffSnapshot> SnapshotBuffs(string targetId)
        {
            List<BuffSnapshot> list = new List<BuffSnapshot>();
            if (string.IsNullOrEmpty(targetId)) return list;
            if (!_pipelines.TryGetValue(targetId, out BuffPipeline pipe)) return list;

            MxFramework.Buffs.BuffSnapshot[] raw = pipe.CreateSnapshot();
            Dictionary<int, string> casters = _buffCasterByTarget.TryGetValue(targetId, out Dictionary<int, string> c) ? c : null;
            Dictionary<int, DateTime> addedAt = _buffAddedAt.TryGetValue(targetId, out Dictionary<int, DateTime> a) ? a : null;
            for (int i = 0; i < raw.Length; i++)
            {
                MxFramework.Buffs.BuffSnapshot s = raw[i];
                long remainMs = s.IsPermanent ? -1 : (long)(s.RemainingTime * 1000f);
                long totalMs = s.IsPermanent ? -1 : (long)(s.Duration * 1000f);
                string casterId = casters != null && casters.TryGetValue(s.Id, out string cid) ? cid : string.Empty;
                string addedAtStr = addedAt != null && addedAt.TryGetValue(s.Id, out DateTime ts)
                    ? ts.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty;
                list.Add(new BuffSnapshot
                {
                    BuffId = s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    OwnerId = targetId,
                    Stack = s.CurrentLayers,
                    RemainingMs = remainMs,
                    TotalMs = totalMs,
                    CasterId = casterId,
                    AddedAt = addedAtStr,
                });
            }
            return list;
        }

        public IReadOnlyList<AttributeChange> SnapshotAttributeChanges(string targetId)
        {
            if (!string.IsNullOrEmpty(targetId) && _attrChanges.TryGetValue(targetId, out List<AttributeChange> list))
            {
                AttributeChange[] copy = list.ToArray();
                list.Clear();
                return copy;
            }
            return Array.Empty<AttributeChange>();
        }

        public IReadOnlyList<DamageTick> DrainDamageTicks()
        {
            DamageTick[] copy = _damageTicks.ToArray();
            _damageTicks.Clear();
            return copy;
        }

        public IReadOnlyList<StatusChange> DrainStatusChanges()
        {
            StatusChange[] copy = _statusChanges.ToArray();
            _statusChanges.Clear();
            return copy;
        }

        public bool ApplyBuff(string buffId, string casterId, string targetId, int stack, long? durationOverrideMs)
        {
            ClearFailure();
            if (_buffFactory == null)
            {
                SetFailure("missing_buff_factory", "Dummy preview world has no IBuffFactory.");
                return false;
            }

            if (!int.TryParse(buffId, out int id))
            {
                SetFailure("invalid_buff_id", $"buffId '{buffId}' is not a numeric runtime buff id.");
                return false;
            }

            IBuffTarget target = GetOrCreateTarget(targetId);
            IBuffTarget caster = GetOrCreateCaster(casterId);

            if (!_pipelines.TryGetValue(string.IsNullOrEmpty(targetId) ? "TestTarget" : targetId, out BuffPipeline pipe))
                pipe = _pipelines[((DummyTarget)target).Id];

            _previewFactory?.SetApplyContext(caster.Attributes, _damageTicks.Add);

            // TODO: framework BuffPipeline.AddBuff currently has no caster / stackOverride / durationOverrideMs
            // parameters. We add the buff and then call AddLayer(stack-1) to approximate stacking. The
            // caster is recorded out-of-band so SnapshotBuffs can echo it back.
            if (!pipe.TryAddBuff(id, target, out IBuff buff) || buff == null)
            {
                SetFailure("unknown_buff_or_config", $"Buff factory rejected buffId={buffId}.");
                return false;
            }
            if (stack > 1) buff.AddLayer(stack - 1);

            string tid = ((DummyTarget)target).Id;
            _buffCasterByTarget[tid][buff.Id] = ((DummyTarget)caster).Id;
            _buffAddedAt[tid][buff.Id] = DateTime.UtcNow;
            return true;
        }

        public string GetAnyTargetId()
        {
            foreach (string k in _targets.Keys) return k;
            return null;
        }

        private static string AttributeName(int id)
        {
            switch (id)
            {
                case AttrHp: return "Hp";
                case AttrAttack: return "Attack";
                default: return "Attr" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private void ClearFailure()
        {
            _lastFailureReason = string.Empty;
            _lastFailureMessage = string.Empty;
        }

        private void SetFailure(string reason, string message)
        {
            _lastFailureReason = reason ?? string.Empty;
            _lastFailureMessage = message ?? string.Empty;
        }

        private sealed class DummyTarget : IBuffTarget
        {
            public string Id { get; }
            public AttributeStore Store { get; }
            private readonly EventBus<BuffEvent> _buffEvents = new EventBus<BuffEvent>();

            public DummyTarget(string id)
            {
                Id = id;
                Store = new AttributeStore();
            }

            public IAttributeOwner Attributes => Store;
            public IAttributeModifierOwner AttributeModifiers => Store;
            public IEventBus<BuffEvent> BuffEvents => _buffEvents;
        }
    }
}
