using System;
using MxFramework.Combat.Physics;

namespace MxFramework.Audio.Physical
{
    public readonly struct PhysicalAudioCueRule : IEquatable<PhysicalAudioCueRule>
    {
        public PhysicalAudioCueRule(
            CombatPhysicalContactKind contactKind,
            CombatSurfaceMaterialKind surfaceMaterialKind,
            int sourceSlotId,
            int minimumIntensity,
            int eventId,
            int cooldownFrames,
            int priority,
            int bodyPartId = 0,
            int contactPointId = 0)
        {
            if (sourceSlotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSlotId), "Physical audio source slot id cannot be negative.");
            }

            if (minimumIntensity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumIntensity), "Physical audio minimum intensity cannot be negative.");
            }

            if (eventId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventId), "Physical audio event id must be positive.");
            }

            if (cooldownFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownFrames), "Physical audio cooldown cannot be negative.");
            }

            if (bodyPartId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyPartId), "Physical audio body part id cannot be negative.");
            }

            if (contactPointId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactPointId), "Physical audio contact point id cannot be negative.");
            }

            ContactKind = contactKind;
            SurfaceMaterialKind = surfaceMaterialKind;
            SourceSlotId = sourceSlotId;
            MinimumIntensity = minimumIntensity;
            EventId = eventId;
            CooldownFrames = cooldownFrames;
            Priority = priority < 0 ? 0 : priority;
            BodyPartId = bodyPartId;
            ContactPointId = contactPointId;
        }

        public CombatPhysicalContactKind ContactKind { get; }

        public CombatSurfaceMaterialKind SurfaceMaterialKind { get; }

        public int SourceSlotId { get; }

        public int MinimumIntensity { get; }

        public int EventId { get; }

        public int CooldownFrames { get; }

        public int Priority { get; }

        public int BodyPartId { get; }

        public int ContactPointId { get; }

        public bool Equals(PhysicalAudioCueRule other)
        {
            return ContactKind == other.ContactKind
                && SurfaceMaterialKind == other.SurfaceMaterialKind
                && SourceSlotId == other.SourceSlotId
                && MinimumIntensity == other.MinimumIntensity
                && EventId == other.EventId
                && CooldownFrames == other.CooldownFrames
                && Priority == other.Priority
                && BodyPartId == other.BodyPartId
                && ContactPointId == other.ContactPointId;
        }

        public override bool Equals(object obj)
        {
            return obj is PhysicalAudioCueRule other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ContactKind;
                hash = (hash * 397) ^ (int)SurfaceMaterialKind;
                hash = (hash * 397) ^ SourceSlotId;
                hash = (hash * 397) ^ MinimumIntensity;
                hash = (hash * 397) ^ EventId;
                hash = (hash * 397) ^ CooldownFrames;
                hash = (hash * 397) ^ Priority;
                hash = (hash * 397) ^ BodyPartId;
                hash = (hash * 397) ^ ContactPointId;
                return hash;
            }
        }
    }

    /// <summary>
    /// Static physical-contact-to-audio-event lookup table.
    /// </summary>
    /// <remarks>NoAlloc after construction; lookup uses a linear scan over the prebuilt rule array.</remarks>
    public sealed class PhysicalAudioCueRuleTable
    {
        public static readonly PhysicalAudioCueRuleTable Empty =
            new PhysicalAudioCueRuleTable(Array.Empty<PhysicalAudioCueRule>(), 0);

        private readonly PhysicalAudioCueRule[] _rules;

        public PhysicalAudioCueRuleTable(PhysicalAudioCueRule[] rules)
            : this(rules, rules == null ? 0 : rules.Length)
        {
        }

        public PhysicalAudioCueRuleTable(PhysicalAudioCueRule[] rules, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Physical audio rule count cannot be negative.");
            }

            if (rules == null || count == 0)
            {
                _rules = Array.Empty<PhysicalAudioCueRule>();
                return;
            }

            if (count > rules.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Physical audio rule count exceeds the rule array length.");
            }

            _rules = new PhysicalAudioCueRule[count];
            Array.Copy(rules, _rules, count);
        }

        public int Count => _rules.Length;

        public bool TryResolve(in CombatPhysicalContactEvent contactEvent, out PhysicalAudioCueRule rule)
        {
            int bestScore = int.MinValue;
            int bestIndex = -1;
            CombatSurfaceMaterialKind materialKind = contactEvent.SurfaceMaterial.Kind;
            for (int i = 0; i < _rules.Length; i++)
            {
                PhysicalAudioCueRule candidate = _rules[i];
                if (candidate.ContactKind != contactEvent.Kind)
                {
                    continue;
                }

                if (candidate.MinimumIntensity > contactEvent.Intensity)
                {
                    continue;
                }

                bool materialExact = candidate.SurfaceMaterialKind == materialKind;
                bool materialFallback = candidate.SurfaceMaterialKind == CombatSurfaceMaterialKind.Default;
                if (!materialExact && !materialFallback)
                {
                    continue;
                }

                bool slotExact = candidate.SourceSlotId == contactEvent.SourceSlotId;
                bool slotFallback = candidate.SourceSlotId == 0;
                if (!slotExact && !slotFallback)
                {
                    continue;
                }

                bool bodyPartExact = candidate.BodyPartId == contactEvent.BodyPartId;
                bool bodyPartFallback = candidate.BodyPartId == 0;
                if (!bodyPartExact && !bodyPartFallback)
                {
                    continue;
                }

                bool contactPointExact = candidate.ContactPointId == contactEvent.ContactPointId;
                bool contactPointFallback = candidate.ContactPointId == 0;
                if (!contactPointExact && !contactPointFallback)
                {
                    continue;
                }

                int score = candidate.MinimumIntensity;
                if (materialExact)
                {
                    score += 1000000;
                }

                if (slotExact)
                {
                    score += 100000;
                }

                if (bodyPartExact)
                {
                    score += 10000;
                }

                if (contactPointExact)
                {
                    score += 1000;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                rule = default;
                return false;
            }

            rule = _rules[bestIndex];
            return true;
        }
    }
}
