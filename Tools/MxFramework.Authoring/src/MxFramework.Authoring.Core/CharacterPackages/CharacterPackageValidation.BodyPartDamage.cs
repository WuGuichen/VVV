using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public static partial class CharacterAuthoringValidationCodes
    {
        public const string BodyPartDamageFutureSchema = "CHARBPD001_FUTURE_SCHEMA";
        public const string BodyPartDamageReadOnlyDocument = "CHARBPD002_READ_ONLY_DOCUMENT";
        public const string BodyPartDamageInvalidRevision = "CHARBPD003_INVALID_REVISION";
        public const string BodyPartDamageTooManyDefinitions = "CHARBPD004_TOO_MANY_DEFINITIONS";
        public const string BodyPartDamageInvalidStateId = "CHARBPD005_INVALID_STATE_ID";
        public const string BodyPartDamageDuplicateStateId = "CHARBPD006_DUPLICATE_STATE_ID";
        public const string BodyPartDamageEmptyMembers = "CHARBPD007_EMPTY_MEMBERS";
        public const string BodyPartDamageDuplicateMember = "CHARBPD008_DUPLICATE_MEMBER";
        public const string BodyPartDamageUnknownMember = "CHARBPD009_UNKNOWN_MEMBER";
        public const string BodyPartDamageUnknownZone = "CHARBPD010_UNKNOWN_ZONE";
        public const string BodyPartDamageInvalidWound = "CHARBPD011_INVALID_WOUND";
        public const string BodyPartDamageInvalidStructure = "CHARBPD012_INVALID_STRUCTURE";
        public const string BodyPartDamageInvalidPriority = "CHARBPD013_INVALID_PRIORITY";
        public const string BodyPartDamageInvalidFocusTag = "CHARBPD014_INVALID_FOCUS_TAG";
        public const string BodyPartDamageInvalidBreakReaction = "CHARBPD015_INVALID_BREAK_REACTION";
        public const string BodyPartDamageEmptyDefinitions = "CHARBPD016_EMPTY_DEFINITIONS";
        public const string BodyPartDamageInvalidSeverPolicy = "CHARBPD017_INVALID_SEVER_POLICY";
        public const string BodyPartDamageSeverRootPart = "CHARBPD018_SEVER_ROOT_PART";
        public const string BodyPartDamageInvalidPresentationEvent = "CHARBPD019_INVALID_PRESENTATION_EVENT";
        public const string BodyPartDamageDuplicatePresentationEvent = "CHARBPD020_DUPLICATE_PRESENTATION_EVENT";
    }

    public static partial class CharacterResourcePackageValidator
    {
        private const string BodyPartDamageSourcePath = "geometry/body_part_damage.json";

        private static void ValidateBodyPartDamage(
            CharacterAuthoringValidationReport report,
            CharacterAuthoringGeometry geometry)
        {
            if (geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Missing)
                return;

            if (geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Future)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageFutureSchema, BodyPartDamageSourcePath,
                    "bodyPartDamage", "format/schemaVersion",
                    "Body-part-damage format or schemaVersion is not current and the original payload is read-only.",
                    "Use mx.characterBodyPartDamage.v1 schema 1.0 or a supported explicit migrator.");
                return;
            }

            CharacterBodyPartDamageDocument document = geometry.BodyPartDamage;
            if (geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Invalid
                || document == null)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageReadOnlyDocument, BodyPartDamageSourcePath,
                    "bodyPartDamage", string.Empty,
                    "Body-part-damage could not be read without data loss and is write-ineligible.",
                    "Repair the JSON document before saving or compiling the package.");
                return;
            }

            if (!string.Equals(document.Format, CharacterBodyPartDamageDocument.CurrentFormat, StringComparison.Ordinal)
                || !string.Equals(
                    document.SchemaVersion,
                    CharacterBodyPartDamageDocument.CurrentSchemaVersion,
                    StringComparison.Ordinal))
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageFutureSchema, BodyPartDamageSourcePath,
                    "bodyPartDamage", "format/schemaVersion",
                    "Body-part-damage requires mx.characterBodyPartDamage.v1 schema 1.0.",
                    "Use the current format without changing stable partStateId identities.");
            }

            if (document.Revision < 1)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageInvalidRevision, BodyPartDamageSourcePath,
                    "bodyPartDamage", "revision", "Body-part-damage revision must be at least 1.",
                    "Use revision 1 or a later positive revision.");
            }

            List<CharacterBodyPartDamageAuthoring> definitions =
                document.PartStates ?? new List<CharacterBodyPartDamageAuthoring>();
            if (definitions.Count == 0)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageEmptyDefinitions, BodyPartDamageSourcePath,
                    "bodyPartDamage/partStates", "partStates",
                    "A Current body-part-damage document must contain at least one part state.",
                    "Add one complete body-part-damage group, or remove the optional document to use Missing compatibility.");
            }
            if (definitions.Count > CharacterBodyPartDamageLimits.MaximumDefinitionCount)
            {
                Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                    CharacterAuthoringValidationCodes.BodyPartDamageTooManyDefinitions, BodyPartDamageSourcePath,
                    "bodyPartDamage/partStates", "partStates",
                    "A character can author at most 8 body-part-damage groups.",
                    "Merge groups until the total definition count is 8 or fewer.");
            }

            var knownParts = new HashSet<string>(StringComparer.Ordinal);
            var knownPartDefinitions = new Dictionary<string, CharacterBodyPartAuthoring>(StringComparer.Ordinal);
            for (int i = 0; geometry.BodyParts != null && i < geometry.BodyParts.Count; i++)
            {
                CharacterBodyPartAuthoring part = geometry.BodyParts[i];
                string partId = part?.PartId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(partId))
                {
                    knownParts.Add(partId);
                    if (!knownPartDefinitions.ContainsKey(partId))
                        knownPartDefinitions.Add(partId, part);
                }
            }

            var knownZones = new HashSet<string>(StringComparer.Ordinal);
            if (geometry.DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current
                && geometry.DamageZones?.Zones != null)
            {
                for (int i = 0; i < geometry.DamageZones.Zones.Count; i++)
                {
                    string zoneId = geometry.DamageZones.Zones[i]?.ZoneId ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(zoneId))
                        knownZones.Add(zoneId);
                }
            }

            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            var ownedMembers = new HashSet<string>(StringComparer.Ordinal);
            var presentationEventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterBodyPartDamageAuthoring definition = definitions[i];
                string stateId = definition?.PartStateId ?? string.Empty;
                string objectPath = "bodyPartDamage/partStates/"
                    + (!string.IsNullOrWhiteSpace(stateId)
                        ? stateId
                        : i.ToString(CultureInfo.InvariantCulture));
                if (!IsStableBodyPartDamageId(stateId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidStateId, BodyPartDamageSourcePath,
                        objectPath, "partStateId", "partStateId must be a stable package-local identity.",
                        "Use lowercase letters, digits, '.', '_' or '-'.");
                }
                else if (!stateIds.Add(stateId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageDuplicateStateId, BodyPartDamageSourcePath,
                        objectPath, "partStateId", "partStateId must be unique.",
                        "Rename or merge the duplicate state group.");
                }

                if (definition == null)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageEmptyMembers, BodyPartDamageSourcePath,
                        objectPath, "partState", "Body-part-damage state cannot be null.",
                        "Remove the null entry or author a complete state.");
                    continue;
                }

                List<string> members = definition.MemberPartIds ?? new List<string>();
                if (members.Count == 0)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageEmptyMembers, BodyPartDamageSourcePath,
                        objectPath, "memberPartIds", "A body-part-damage group must own at least one body part.",
                        "Reference one or more identities from geometry/body_parts.json.");
                }

                var localMembers = new HashSet<string>(StringComparer.Ordinal);
                for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    string member = members[memberIndex] ?? string.Empty;
                    if (!localMembers.Add(member) || !ownedMembers.Add(member))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageDuplicateMember, BodyPartDamageSourcePath,
                            objectPath, "memberPartIds", "A body part can belong to only one state group.",
                            "Remove the duplicate member or merge the affected groups.");
                    }
                    if (!knownParts.Contains(member))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageUnknownMember, BodyPartDamageSourcePath,
                            objectPath, "memberPartIds", "memberPartIds contains an unknown body-part identity.",
                            "Reference an exact partId from geometry/body_parts.json.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.DamageZoneId)
                    && !knownZones.Contains(definition.DamageZoneId))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageUnknownZone, BodyPartDamageSourcePath,
                        objectPath, "damageZoneId", "damageZoneId does not resolve to an authored DamageZone.",
                        "Reference an exact zoneId or leave the optional field empty.");
                }

                bool woundValid = InRange(
                        definition.WoundCapacity,
                        0,
                        CharacterBodyPartDamageLimits.MaximumAxisCapacity)
                    && InRange(
                        definition.OpenDamageScaleMilli,
                        0,
                        CharacterBodyPartDamageLimits.MaximumScaleMilli)
                    && (definition.WoundCapacity == 0 || definition.OpenDamageScaleMilli > 0);
                if (!woundValid)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidWound, BodyPartDamageSourcePath,
                        objectPath, "woundCapacity/openDamageScaleMilli",
                        "Wound capacity must be in [0,1000000], scale in [0,4000], and an enabled wound axis requires a positive scale.",
                        "Use 0 capacity to disable the axis or a bounded positive scale for an enabled axis.");
                }

                bool structureValid = InRange(
                        definition.MaxIntegrity,
                        0,
                        CharacterBodyPartDamageLimits.MaximumAxisCapacity)
                    && InRange(
                        definition.IntegrityDamageScaleMilli,
                        0,
                        CharacterBodyPartDamageLimits.MaximumScaleMilli);
                if (!structureValid)
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidStructure, BodyPartDamageSourcePath,
                        objectPath, "maxIntegrity/integrityDamageScaleMilli",
                        "Structure capacity must be in [0,1000000] and its scale in [0,4000].",
                        "Use bounded integer values; 0 capacity disables the structure axis.");
                }

                if (!InRange(
                        definition.AuthoredPriorityDeltaOpen,
                        CharacterBodyPartDamageLimits.MinimumPriorityDelta,
                        CharacterBodyPartDamageLimits.MaximumPriorityDelta)
                    || !InRange(
                        definition.AuthoredPriorityDeltaBroken,
                        CharacterBodyPartDamageLimits.MinimumPriorityDelta,
                        CharacterBodyPartDamageLimits.MaximumPriorityDelta))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidPriority, BodyPartDamageSourcePath,
                        objectPath, "authoredPriorityDeltaOpen/authoredPriorityDeltaBroken",
                        "Dynamic priority deltas must be in [-1000,1000].",
                        "Keep both authored deltas within the fixed comparator range.");
                }

                if (!string.IsNullOrWhiteSpace(definition.FocusConsumeTag)
                    && (!IsExplicitBodyPartFocusConsumeTag(definition.FocusConsumeTag)
                        || definition.WoundCapacity == 0))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidFocusTag, BodyPartDamageSourcePath,
                        objectPath, "focusConsumeTag",
                        "focusConsumeTag must use an action., ability., or interaction. prefix on an enabled wound axis.",
                        "Use an explicit accepted-action semantic tag, or leave it empty; damage tags are not consume facts.");
                }

                if (!string.IsNullOrWhiteSpace(definition.BreakReactionId)
                    && (!IsStableBodyPartDamageId(definition.BreakReactionId)
                        || definition.MaxIntegrity == 0))
                {
                    Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                        CharacterAuthoringValidationCodes.BodyPartDamageInvalidBreakReaction, BodyPartDamageSourcePath,
                        objectPath, "breakReactionId",
                        "breakReactionId must be a stable identity on an enabled structure axis.",
                        "Use a stable reaction id, or leave it empty when no break reaction is authored.");
                }

                bool hasSeverTag = !string.IsNullOrWhiteSpace(definition.SeverRequiredTag);
                bool hasPresentationEvent = !string.IsNullOrWhiteSpace(definition.PresentationEventId);
                if (!definition.Severable)
                {
                    if (hasSeverTag || hasPresentationEvent)
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageInvalidSeverPolicy, BodyPartDamageSourcePath,
                            objectPath, "severable/severRequiredTag/presentationEventId",
                            "A non-severable part state cannot declare sever tags or presentation events.",
                            "Clear the sever fields, or explicitly enable severable with a complete policy.");
                    }
                }
                else
                {
                    if (definition.MaxIntegrity <= 0
                        || !IsExplicitBodyPartFocusConsumeTag(definition.SeverRequiredTag))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageInvalidSeverPolicy, BodyPartDamageSourcePath,
                            objectPath, "severable/severRequiredTag",
                            "Severable requires an enabled structure axis and an explicit action., ability., or interaction. tag.",
                            "Enable structure integrity and provide the typed tag accepted after a frame starts Broken.");
                    }

                    if (!IsStableBodyPartDamageId(definition.PresentationEventId)
                        || !definition.PresentationEventId.StartsWith("event.", StringComparison.Ordinal))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageInvalidPresentationEvent, BodyPartDamageSourcePath,
                            objectPath, "presentationEventId",
                            "Severable requires a stable event.* presentation identity.",
                            "Author one stable event id; presentation remains a read-only consumer of authority state.");
                    }
                    else if (!presentationEventIds.Add(definition.PresentationEventId))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                            CharacterAuthoringValidationCodes.BodyPartDamageDuplicatePresentationEvent, BodyPartDamageSourcePath,
                            objectPath, "presentationEventId",
                            "presentationEventId must be unique across body-part sever definitions.",
                            "Give each severable part state its own stable event identity.");
                    }

                    for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                    {
                        string member = members[memberIndex] ?? string.Empty;
                        if (knownPartDefinitions.TryGetValue(member, out CharacterBodyPartAuthoring part)
                            && string.IsNullOrWhiteSpace(part.ParentPartId))
                        {
                            Add(report, CharacterAuthoringValidationSeverity.Error, CharacterAuthoringValidationGate.ExportBlocked,
                                CharacterAuthoringValidationCodes.BodyPartDamageSeverRootPart, BodyPartDamageSourcePath,
                                objectPath, "memberPartIds",
                                "A severable definition cannot disable a root body part.",
                                "Keep the character root/core in authority and sever only descendant body parts.");
                            break;
                        }
                    }
                }
            }
        }

        private static bool IsStableBodyPartDamageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static bool IsExplicitBodyPartFocusConsumeTag(string value)
        {
            return IsStableBodyPartDamageId(value)
                && (value.StartsWith("action.", StringComparison.Ordinal)
                    || value.StartsWith("ability.", StringComparison.Ordinal)
                    || value.StartsWith("interaction.", StringComparison.Ordinal));
        }

        private static bool InRange(int value, int minimum, int maximum)
        {
            return value >= minimum && value <= maximum;
        }
    }
}
