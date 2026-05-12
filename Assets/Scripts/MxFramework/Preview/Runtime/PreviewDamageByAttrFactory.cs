using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Attributes;
using MxFramework.Buffs;

namespace MxFramework.Preview
{
    /// <summary>
    /// Preview-only buff factory for the authoring editor vertical slice.
    /// It intentionally supports a tiny, data-only subset: DamageByAttr records loaded from patch JSON.
    /// </summary>
    internal sealed class PreviewDamageByAttrFactory : IBuffFactory
    {
        private readonly Dictionary<int, PreviewDamageByAttrConfig> _configs =
            new Dictionary<int, PreviewDamageByAttrConfig>();

        private IAttributeOwner _caster;
        private Action<DamageTick> _recordDamage;

        public void LoadPatch(string sourceJson)
        {
            _configs.Clear();
            if (string.IsNullOrWhiteSpace(sourceJson))
                return;

            JsonValue root = PreviewJson.Parse(sourceJson);
            if (root == null || root.Kind != JsonKind.Object)
                return;

            JsonValue patches = root.GetField("patches");
            if (patches != null && patches.Kind == JsonKind.Array)
            {
                for (int i = 0; i < patches.Array.Count; i++)
                    LoadPatchDocument(patches.Array[i]);
                return;
            }

            LoadPatchDocument(root);
        }

        public void Clear()
        {
            _configs.Clear();
            _caster = null;
            _recordDamage = null;
        }

        public void SetApplyContext(IAttributeOwner caster, Action<DamageTick> recordDamage)
        {
            _caster = caster;
            _recordDamage = recordDamage;
        }

        public bool TryCreate(int buffId, out IBuff buff)
        {
            buff = null;
            if (!_configs.TryGetValue(buffId, out PreviewDamageByAttrConfig config))
                return false;

            buff = new PreviewDamageByAttrBuff(config, _caster, _recordDamage);
            return true;
        }

        private void LoadPatchDocument(JsonValue patch)
        {
            if (patch == null || patch.Kind != JsonKind.Object)
                return;

            JsonValue entries = patch.GetField("entries");
            if (entries == null || entries.Kind != JsonKind.Array)
                return;

            for (int i = 0; i < entries.Array.Count; i++)
                LoadEntry(entries.Array[i]);
        }

        private void LoadEntry(JsonValue entry)
        {
            if (entry == null || entry.Kind != JsonKind.Object)
                return;

            JsonValue fields = entry.GetField("fields");
            if (fields == null || fields.Kind != JsonKind.Object)
                return;

            string type = ReadScalar(fields, "Type");
            if (!string.Equals(type, "DamageByAttr", StringComparison.OrdinalIgnoreCase))
                return;

            string idText = entry.GetString("id") ?? ReadScalar(fields, "Id");
            if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                return;

            _configs[id] = new PreviewDamageByAttrConfig
            {
                Id = id,
                DurationSeconds = Math.Max(0.001f, ReadMilliseconds(fields, "Duration", 5000) / 1000f),
                HitCooldownSeconds = Math.Max(0.001f, ReadMilliseconds(fields, "HitCooldown", 1000) / 1000f),
                Values = ReadScalar(fields, "Values"),
                DamageType = ReadScalar(fields, "DmgType"),
                ElementType = ReadScalar(fields, "EleType"),
            };
        }

        private static float ReadMilliseconds(JsonValue fields, string key, float fallback)
        {
            string raw = ReadScalar(fields, key);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;
            return fallback;
        }

        private static string ReadScalar(JsonValue fields, string key)
        {
            JsonValue value = fields.GetField(key);
            if (value == null)
                return string.Empty;

            if (value.Kind == JsonKind.String)
                return value.String ?? string.Empty;
            if (value.Kind == JsonKind.Number)
                return value.Number.ToString("R", CultureInfo.InvariantCulture);
            if (value.Kind == JsonKind.Bool)
                return value.Bool ? "true" : "false";
            return string.Empty;
        }

        private sealed class PreviewDamageByAttrConfig
        {
            public int Id;
            public float DurationSeconds;
            public float HitCooldownSeconds;
            public string Values;
            public string DamageType;
            public string ElementType;
        }

        private sealed class PreviewDamageByAttrBuff : BuffBase
        {
            private readonly PreviewDamageByAttrConfig _config;
            private readonly IAttributeOwner _caster;
            private readonly Action<DamageTick> _recordDamage;
            private float _elapsed;
            private int _tickIndex;

            public PreviewDamageByAttrBuff(
                PreviewDamageByAttrConfig config,
                IAttributeOwner caster,
                Action<DamageTick> recordDamage)
                : base(config.Id, config.DurationSeconds, maxLayers: 99)
            {
                _config = config;
                _caster = caster;
                _recordDamage = recordDamage;
            }

            public override void OnTick(float deltaTime, IBuffTarget target)
            {
                if (target == null || deltaTime <= 0f)
                    return;

                _elapsed += deltaTime;
                while (_elapsed + 0.0001f >= _config.HitCooldownSeconds && !IsExpired)
                {
                    _elapsed -= _config.HitCooldownSeconds;
                    ApplyDamage(target);
                }

                base.OnTick(deltaTime, target);
            }

            private void ApplyDamage(IBuffTarget target)
            {
                int casterAttack = _caster != null
                    ? _caster.GetAttribute(DummyPreviewWorld.AttrAttack)
                    : target.Attributes.GetAttribute(DummyPreviewWorld.AttrAttack);
                int amount = Math.Max(0, (int)Math.Round(EvaluateDamage(casterAttack), MidpointRounding.AwayFromZero));
                if (amount <= 0)
                    return;

                target.Attributes.AddAttribute(DummyPreviewWorld.AttrHp, -amount, this);
                _recordDamage?.Invoke(new DamageTick
                {
                    BuffId = Id.ToString(CultureInfo.InvariantCulture),
                    TickIndex = _tickIndex++,
                    Amount = amount,
                    DamageType = _config.DamageType ?? string.Empty,
                    ElementType = _config.ElementType ?? string.Empty,
                });
            }

            private double EvaluateDamage(int casterAttack)
            {
                string expression = _config.Values ?? string.Empty;
                const string casterAttackToken = "caster.Attack";
                int tokenIndex = expression.IndexOf(casterAttackToken, StringComparison.OrdinalIgnoreCase);
                if (tokenIndex >= 0)
                {
                    int multiplyIndex = expression.IndexOf('*', tokenIndex + casterAttackToken.Length);
                    if (multiplyIndex >= 0)
                    {
                        string factorText = expression.Substring(multiplyIndex + 1).Trim();
                        if (double.TryParse(factorText, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor))
                            return casterAttack * factor;
                    }

                    return casterAttack;
                }

                if (double.TryParse(expression.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double literal))
                    return literal;

                return 0;
            }
        }
    }
}
