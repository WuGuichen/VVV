using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionHitEventAuthoringModel
    {
        public CombatHitTargetTypes TargetTypes { get; set; } = CombatHitEventConfig.DefaultTargetTypes;
        public string EnvironmentHitPolicy { get; set; } = CombatEnvironmentHitPolicy.None.ToString();
        public string StableEventId { get; set; } = string.Empty;
        public int MaxAcceptedHits { get; set; } = 1;
        public string HitSameTargetPolicy { get; set; } = CombatHitSameTargetPolicy.AllowRepeat.ToString();
        public string DamageTypeId { get; set; } = string.Empty;
        public string FormulaId { get; set; } = string.Empty;
        public int BaseDamage { get; set; }
        public CharacterActionDamageAttributeScaleJsonDto[] SourceAttributeScales { get; set; } = Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
        public int ImpactPower { get; set; }
        public int LaunchPower { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string AnalysisCategory { get; set; } = string.Empty;
        public CharacterActionHitBuffJsonDto[] OnHitBuffs { get; set; } = Array.Empty<CharacterActionHitBuffJsonDto>();

        public bool HasWorldTargets => CombatHitEventConfig.HasAnyWorldTarget(
            TargetTypes == CombatHitTargetTypes.None ? CombatHitEventConfig.DefaultTargetTypes : TargetTypes);

        public CharacterActionHitEventAuthoringModel Clone()
        {
            string formulaId = FormulaId ?? string.Empty;
            CharacterActionDamageAttributeScaleJsonDto[] sourceAttributeScales =
                NormalizeDamageAttributeScales(SourceAttributeScales);
            if (sourceAttributeScales.Length > 0 && string.IsNullOrWhiteSpace(formulaId))
                formulaId = CharacterActionDamageAuthoringText.DefaultDamageFormulaId;

            return new CharacterActionHitEventAuthoringModel
            {
                TargetTypes = TargetTypes,
                EnvironmentHitPolicy = EnvironmentHitPolicy ?? CombatEnvironmentHitPolicy.None.ToString(),
                StableEventId = StableEventId ?? string.Empty,
                MaxAcceptedHits = MaxAcceptedHits <= 0 ? 1 : MaxAcceptedHits,
                HitSameTargetPolicy = HitSameTargetPolicy ?? CombatHitSameTargetPolicy.AllowRepeat.ToString(),
                DamageTypeId = DamageTypeId ?? string.Empty,
                FormulaId = formulaId,
                BaseDamage = Math.Max(0, BaseDamage),
                SourceAttributeScales = sourceAttributeScales,
                ImpactPower = Math.Max(0, ImpactPower),
                LaunchPower = Math.Max(0, LaunchPower),
                Tags = NormalizeTags(Tags),
                AnalysisCategory = AnalysisCategory ?? string.Empty,
                OnHitBuffs = NormalizeOnHitBuffs(OnHitBuffs),
            };
        }

        public static CharacterActionHitEventAuthoringModel FromCombatTrackEvent(CombatTrackEventJsonDto evt)
        {
            var model = new CharacterActionHitEventAuthoringModel();
            if (evt == null)
                return model;

            CharacterActionHitEventJsonDto hitEvent = evt.HitEvent;
            CharacterActionHitDamageJsonDto damage = hitEvent?.Damage;
            model.TargetTypes = ParseTargetTypes(hitEvent?.TargetTypes);
            model.EnvironmentHitPolicy = string.IsNullOrWhiteSpace(hitEvent?.EnvironmentHitPolicy)
                ? CombatEnvironmentHitPolicy.None.ToString()
                : hitEvent.EnvironmentHitPolicy;
            model.StableEventId = CharacterActionAuthoringIds.Label(
                hitEvent?.StableEventId ?? evt.StableEventId, 401);
            model.MaxAcceptedHits = hitEvent?.MaxAcceptedHits > 0 ? hitEvent.MaxAcceptedHits : 1;
            model.HitSameTargetPolicy = string.IsNullOrWhiteSpace(hitEvent?.HitSameTargetPolicy)
                ? CombatHitSameTargetPolicy.AllowRepeat.ToString()
                : hitEvent.HitSameTargetPolicy;
            model.DamageTypeId = CharacterActionAuthoringIds.Label(
                damage?.DamageTypeId ?? 0, 409, damage?.DamageTypeLabel);
            model.FormulaId = CharacterActionAuthoringIds.Label(
                damage?.FormulaId ?? 0, 410, damage?.FormulaLabel);
            model.BaseDamage = damage == null ? 0 : Math.Max(0, damage.BaseDamage);
            model.SourceAttributeScales = NormalizeDamageAttributeScales(damage?.SourceAttributeScales);
            model.ImpactPower = damage == null ? 0 : Math.Max(0, damage.ImpactPower);
            model.LaunchPower = damage == null ? 0 : Math.Max(0, damage.LaunchPower);
            model.Tags = CharacterActionAuthoringIds.Labels(damage?.Tags, 410, damage?.TagLabels);
            model.AnalysisCategory = CharacterActionAuthoringIds.Label(
                damage?.AnalysisCategory ?? 0, 411, damage?.AnalysisCategoryLabel);
            model.OnHitBuffs = NormalizeOnHitBuffs(hitEvent?.OnHitBuffs);
            return model.Clone();
        }

        public void ApplyToCombatTrackEvent(CombatTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            CharacterActionHitEventJsonDto hitEvent = evt.HitEvent ?? new CharacterActionHitEventJsonDto();
            CharacterActionHitEventAuthoringModel normalized = Clone();
            hitEvent.TargetTypes = ToTargetTypeNames(normalized.TargetTypes);
            hitEvent.EnvironmentHitPolicy = normalized.EnvironmentHitPolicy;
            hitEvent.StableEventId = string.IsNullOrWhiteSpace(normalized.StableEventId)
                ? evt.StableEventId
                : CharacterActionAuthoringIds.Resolve(normalized.StableEventId, 401);
            hitEvent.MaxAcceptedHits = Math.Max(1, normalized.MaxAcceptedHits);
            hitEvent.HitSameTargetPolicy = normalized.HitSameTargetPolicy;
            hitEvent.Damage = normalized.ToDamageDto();
            hitEvent.OnHitBuffs = normalized.OnHitBuffs;
            evt.HitEvent = hitEvent;
        }

        public static CharacterActionHitEventAuthoringModel FromGameplayTrackEvent(GameplayTrackEventJsonDto evt)
        {
            var model = new CharacterActionHitEventAuthoringModel();
            if (evt == null)
                return model;

            CharacterActionHitEventJsonDto hitEvent = evt.HitEvent;
            CharacterActionHitDamageJsonDto damage = hitEvent?.Damage;
            model.TargetTypes = ParseTargetTypes(hitEvent?.TargetTypes);
            model.EnvironmentHitPolicy = string.IsNullOrWhiteSpace(hitEvent?.EnvironmentHitPolicy)
                ? CombatEnvironmentHitPolicy.None.ToString()
                : hitEvent.EnvironmentHitPolicy;
            model.StableEventId = CharacterActionAuthoringIds.Label(
                hitEvent?.StableEventId ?? evt.StableEventId, 401);
            model.MaxAcceptedHits = hitEvent?.MaxAcceptedHits > 0 ? hitEvent.MaxAcceptedHits : 1;
            model.HitSameTargetPolicy = string.IsNullOrWhiteSpace(hitEvent?.HitSameTargetPolicy)
                ? CombatHitSameTargetPolicy.AllowRepeat.ToString()
                : hitEvent.HitSameTargetPolicy;
            model.DamageTypeId = CharacterActionAuthoringIds.Label(
                damage?.DamageTypeId ?? 0, 409, damage?.DamageTypeLabel);
            model.FormulaId = CharacterActionAuthoringIds.Label(
                damage?.FormulaId ?? 0, 410, damage?.FormulaLabel);
            model.BaseDamage = damage == null ? 0 : Math.Max(0, damage.BaseDamage);
            model.SourceAttributeScales = NormalizeDamageAttributeScales(damage?.SourceAttributeScales);
            model.ImpactPower = damage == null ? 0 : Math.Max(0, damage.ImpactPower);
            model.LaunchPower = damage == null ? 0 : Math.Max(0, damage.LaunchPower);
            model.Tags = CharacterActionAuthoringIds.Labels(damage?.Tags, 410, damage?.TagLabels);
            model.AnalysisCategory = CharacterActionAuthoringIds.Label(
                damage?.AnalysisCategory ?? 0, 411, damage?.AnalysisCategoryLabel);
            model.OnHitBuffs = NormalizeOnHitBuffs(hitEvent?.OnHitBuffs);
            return model.Clone();
        }

        public void ApplyToGameplayTrackEvent(GameplayTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            CharacterActionHitEventJsonDto hitEvent = evt.HitEvent ?? new CharacterActionHitEventJsonDto();
            CharacterActionHitEventAuthoringModel normalized = Clone();
            hitEvent.TargetTypes = ToTargetTypeNames(normalized.TargetTypes);
            hitEvent.EnvironmentHitPolicy = normalized.EnvironmentHitPolicy;
            hitEvent.StableEventId = string.IsNullOrWhiteSpace(normalized.StableEventId)
                ? evt.StableEventId
                : CharacterActionAuthoringIds.Resolve(normalized.StableEventId, 401);
            hitEvent.MaxAcceptedHits = Math.Max(1, normalized.MaxAcceptedHits);
            hitEvent.HitSameTargetPolicy = normalized.HitSameTargetPolicy;
            hitEvent.Damage = normalized.ToDamageDto();
            hitEvent.OnHitBuffs = normalized.OnHitBuffs;
            evt.HitEvent = hitEvent;
        }

        public static CharacterActionHitEventAuthoringModel FromProjectileRecord(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            if (record == null)
                return new CharacterActionHitEventAuthoringModel();

            return new CharacterActionHitEventAuthoringModel
            {
                TargetTypes = record.TargetTypes,
                EnvironmentHitPolicy = string.IsNullOrWhiteSpace(record.EnvironmentHitPolicy)
                    ? CombatEnvironmentHitPolicy.None.ToString()
                    : record.EnvironmentHitPolicy,
                StableEventId = record.StableEventId,
                MaxAcceptedHits = record.HitEventMaxAcceptedHits <= 0 ? record.MaxAcceptedImpacts : record.HitEventMaxAcceptedHits,
                HitSameTargetPolicy = string.IsNullOrWhiteSpace(record.HitEventSameTargetPolicy)
                    ? ToHitEventSameTargetPolicy(record.HitSameTargetPolicy)
                    : record.HitEventSameTargetPolicy,
                DamageTypeId = record.DamageTypeId,
                BaseDamage = record.BaseDamage,
                ImpactPower = record.ImpactPower,
                LaunchPower = record.LaunchPower,
                Tags = SplitTags(record.Tags),
                AnalysisCategory = record.AnalysisCategory,
                OnHitBuffs = NormalizeOnHitBuffs(record.OnHitBuffs),
            }.Clone();
        }

        public CharacterActionHitDamageJsonDto ToDamageDto()
        {
            CharacterActionHitEventAuthoringModel normalized = Clone();
            return new CharacterActionHitDamageJsonDto
            {
                DamageTypeId = CharacterActionAuthoringIds.Resolve(normalized.DamageTypeId, 409),
                FormulaId = CharacterActionAuthoringIds.Resolve(normalized.FormulaId, 410),
                DamageTypeLabel = normalized.DamageTypeId ?? string.Empty,
                FormulaLabel = normalized.FormulaId ?? string.Empty,
                BaseDamage = normalized.BaseDamage,
                SourceAttributeScales = normalized.SourceAttributeScales,
                ImpactPower = normalized.ImpactPower,
                LaunchPower = normalized.LaunchPower,
                Tags = CharacterActionAuthoringIds.ResolveMany(normalized.Tags, 410),
                TagLabels = normalized.Tags ?? Array.Empty<string>(),
                AnalysisCategory = CharacterActionAuthoringIds.Resolve(normalized.AnalysisCategory, 411),
                AnalysisCategoryLabel = normalized.AnalysisCategory ?? string.Empty,
            };
        }

        public void SetAttackScale(float attackScale)
        {
            SourceAttributeScales = BuildAttackScaleArray(attackScale);
            if (SourceAttributeScales.Length > 0 && string.IsNullOrWhiteSpace(FormulaId))
                FormulaId = CharacterActionDamageAuthoringText.DefaultDamageFormulaId;
            else if (SourceAttributeScales.Length == 0
                && string.Equals(FormulaId, CharacterActionDamageAuthoringText.DefaultDamageFormulaId, StringComparison.Ordinal))
            {
                FormulaId = string.Empty;
            }
        }

        public float GetAttackScale()
        {
            return ResolveAttackScale(SourceAttributeScales);
        }

        public static float ResolveAttackScale(CharacterActionDamageAttributeScaleJsonDto[] scales)
        {
            scales = scales ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
            for (int i = 0; i < scales.Length; i++)
            {
                CharacterActionDamageAttributeScaleJsonDto scale = scales[i];
                if (scale != null && scale.AttributeId == 2)
                    return Math.Max(0f, scale.Scale);
            }

            return 0f;
        }

        public static CharacterActionDamageAttributeScaleJsonDto[] BuildAttackScaleArray(float attackScale)
        {
            if (attackScale <= 0.0001f)
                return Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();

            return new[]
            {
                new CharacterActionDamageAttributeScaleJsonDto { AttributeId = 2, Scale = attackScale },
            };
        }

        public static CharacterActionDamageAttributeScaleJsonDto[] NormalizeDamageAttributeScales(
            CharacterActionDamageAttributeScaleJsonDto[] source)
        {
            source = source ?? Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
            var scales = new List<CharacterActionDamageAttributeScaleJsonDto>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                CharacterActionDamageAttributeScaleJsonDto scale = source[i];
                if (scale == null || scale.AttributeId <= 0 || scale.Scale <= 0.0001f)
                    continue;
                scales.Add(new CharacterActionDamageAttributeScaleJsonDto
                {
                    AttributeId = scale.AttributeId,
                    Scale = scale.Scale,
                });
            }

            return scales.ToArray();
        }

        public static CombatHitTargetTypes ParseTargetTypes(string[] values)
        {
            values = values ?? Array.Empty<string>();
            CombatHitTargetTypes result = CombatHitTargetTypes.None;
            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], false, out CombatHitTargetTypes parsed)
                    && Enum.IsDefined(typeof(CombatHitTargetTypes), parsed))
                {
                    result |= parsed;
                }
            }

            return result == CombatHitTargetTypes.None ? CombatHitEventConfig.DefaultTargetTypes : result;
        }

        public static string[] ToTargetTypeNames(CombatHitTargetTypes targetTypes)
        {
            var values = new List<string>();
            CombatHitTargetTypes[] candidates =
            {
                CombatHitTargetTypes.HostileCharacter,
                CombatHitTargetTypes.FriendlyCharacter,
                CombatHitTargetTypes.Self,
                CombatHitTargetTypes.ThirdPartyCharacter,
                CombatHitTargetTypes.DamageableObject,
                CombatHitTargetTypes.WorldWall,
                CombatHitTargetTypes.WorldGround,
                CombatHitTargetTypes.WorldStatic,
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                CombatHitTargetTypes candidate = candidates[i];
                if ((targetTypes & candidate) != 0)
                    values.Add(candidate.ToString());
            }

            return values.Count == 0
                ? new[] { CombatHitTargetTypes.HostileCharacter.ToString() }
                : values.ToArray();
        }

        public static string ToHitEventSameTargetPolicy(string projectilePolicy)
        {
            return string.Equals(projectilePolicy, CombatProjectileHitSameTargetPolicy.OncePerProjectile.ToString(), StringComparison.Ordinal)
                ? CombatHitSameTargetPolicy.OncePerEvent.ToString()
                : CombatHitSameTargetPolicy.AllowRepeat.ToString();
        }

        public static string FromHitEventSameTargetPolicy(string hitEventPolicy)
        {
            return string.Equals(hitEventPolicy, CombatHitSameTargetPolicy.OncePerEvent.ToString(), StringComparison.Ordinal)
                ? CombatProjectileHitSameTargetPolicy.OncePerProjectile.ToString()
                : CombatProjectileHitSameTargetPolicy.AllowRepeat.ToString();
        }

        public static CharacterActionHitBuffJsonDto[] NormalizeOnHitBuffs(CharacterActionHitBuffJsonDto[] buffs)
        {
            buffs = buffs ?? Array.Empty<CharacterActionHitBuffJsonDto>();
            var values = new List<CharacterActionHitBuffJsonDto>(buffs.Length);
            var seenBuffIds = new HashSet<int>();
            for (int i = 0; i < buffs.Length; i++)
            {
                CharacterActionHitBuffJsonDto buff = buffs[i];
                if (buff == null || buff.BuffId <= 0)
                    continue;
                if (!seenBuffIds.Add(buff.BuffId))
                    continue;

                values.Add(new CharacterActionHitBuffJsonDto
                {
                    BuffId = buff.BuffId,
                });
            }

            values.Sort(CompareOnHitBuffs);
            return values.ToArray();
        }

        public static bool OnHitBuffsEqual(CharacterActionHitBuffJsonDto[] left, CharacterActionHitBuffJsonDto[] right)
        {
            left = NormalizeOnHitBuffs(left);
            right = NormalizeOnHitBuffs(right);
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i].BuffId != right[i].BuffId)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareOnHitBuffs(CharacterActionHitBuffJsonDto left, CharacterActionHitBuffJsonDto right)
        {
            return left.BuffId.CompareTo(right.BuffId);
        }

        private static string[] SplitTags(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();
            return NormalizeTags(value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string[] NormalizeTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
                return Array.Empty<string>();

            var list = new List<string>(tags.Length);
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                tag = tag.Trim();
                if (!list.Contains(tag))
                    list.Add(tag);
            }

            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
