using System;
using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private readonly Dictionary<CharacterTestResolvedHitKey, CharacterTestAccessoryHitEvidence> _accessoryEvidenceByHit =
            new Dictionary<CharacterTestResolvedHitKey, CharacterTestAccessoryHitEvidence>();
        private readonly Func<HitResolveResult, CharacterTestAccessoryHitEvidence> _resolveAccessoryEvidence;
        private CharacterTestAccessoryHitEvidence ResolveAccessoryEvidence(HitResolveResult result)
            => _accessoryEvidenceByHit.TryGetValue(new CharacterTestResolvedHitKey(result), out var evidence) ? evidence : default;

        // All non-trace producers write payload and evidence together. An unclassified overwrite removes
        // any earlier proof under the same hit key; chain/tick writers cannot inherit a root's metadata.
        private void SetAccessoryAwareDamagePayload(CharacterTestResolvedHitKey key, DamageProfileResolveResult payload,
            CharacterTestAccessoryHitEvidence evidence = default)
        {
            _damagePayloadsByHit[key] = payload;
            if (evidence.Producer == CharacterTestAccessoryHitProducer.Unknown) _accessoryEvidenceByHit.Remove(key);
            else _accessoryEvidenceByHit[key] = evidence;
        }
    }
}
