using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers.Config;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestLevelGameplayTriggerAuthoringDocument
    {
        internal const string CurrentFormat =
            "mx.characterTest.levelGameplayTriggers.v2";
        internal const string LegacyV1Format =
            "mx.characterTest.levelGameplayTriggers.v1";
        internal const int CurrentSchemaVersion = 2;
        internal const int LegacyV1SchemaVersion = 1;

        public CharacterTestLevelGameplayTriggerAuthoringDocument()
        {
            Format = CurrentFormat;
            SchemaVersion = CurrentSchemaVersion;
            IdState = new CharacterTestLevelGameplayTriggerIdState();
            Authoring = CreateEmptyAuthoring();
            Entries = new List<CharacterTestLevelGameplayTriggerEntry>();
            RegionBindings =
                new List<CharacterTestLevelGameplayTriggerRegionBinding>();
        }

        public string Format { get; set; }

        public int SchemaVersion { get; set; }

        public CharacterTestLevelGameplayTriggerIdState IdState { get; set; }

        public GameplayTriggerAuthoringDocument Authoring { get; set; }

        public List<CharacterTestLevelGameplayTriggerEntry> Entries { get; set; }

        public List<CharacterTestLevelGameplayTriggerRegionBinding>
            RegionBindings { get; set; }

        internal CharacterTestLevelGameplayTriggerAuthoringDocument Normalize()
        {
            var normalized = new CharacterTestLevelGameplayTriggerAuthoringDocument
            {
                Format = string.IsNullOrWhiteSpace(Format)
                    ? string.Empty
                    : Format.Trim(),
                SchemaVersion = SchemaVersion,
                IdState = (IdState
                    ?? new CharacterTestLevelGameplayTriggerIdState()).Normalize(),
                Authoring = CloneCanonicalAuthoring(
                    Authoring ?? CreateEmptyAuthoring()),
                Entries = new List<CharacterTestLevelGameplayTriggerEntry>(),
                RegionBindings =
                    new List<CharacterTestLevelGameplayTriggerRegionBinding>()
            };

            if (Entries != null)
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i] != null)
                        normalized.Entries.Add(Entries[i].Normalize());
                }
            }

            if (RegionBindings != null)
            {
                for (int i = 0; i < RegionBindings.Count; i++)
                {
                    if (RegionBindings[i] != null)
                        normalized.RegionBindings.Add(RegionBindings[i].Normalize());
                }
            }

            normalized.Entries.Sort(
                (left, right) =>
                    string.Compare(
                        left.StableId,
                        right.StableId,
                        StringComparison.Ordinal));
            normalized.RegionBindings.Sort(
                (left, right) =>
                    left.SourceBindingId.CompareTo(right.SourceBindingId));
            normalized.EnsureSubjectFilterDefaults();
            return normalized;
        }

        internal CharacterTestLevelGameplayTriggerAuthoringDocument
            UpgradeToCurrent()
        {
            CharacterTestLevelGameplayTriggerAuthoringDocument upgraded =
                Normalize();
            upgraded.Format = CurrentFormat;
            upgraded.SchemaVersion = CurrentSchemaVersion;
            upgraded.EnsureSubjectFilterDefaults();
            return upgraded;
        }

        private void EnsureSubjectFilterDefaults()
        {
            if (RegionBindings == null)
                return;

            for (int i = 0; i < RegionBindings.Count; i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    RegionBindings[i];
                if (binding == null || binding.SubjectFilter != null)
                    continue;

                string templateId = string.Empty;
                if (Entries != null)
                {
                    for (int entryIndex = 0;
                         entryIndex < Entries.Count;
                         entryIndex++)
                    {
                        CharacterTestLevelGameplayTriggerEntry entry =
                            Entries[entryIndex];
                        if (entry?.SourceBindingId == binding.SourceBindingId)
                        {
                            templateId = entry.TemplateId;
                            break;
                        }
                    }
                }

                binding.SubjectFilter =
                    CharacterTestLevelGameplayTriggerSubjectFilter
                        .CreateTemplateDefault(templateId);
            }
        }

        internal int AllocateId(GameplayTriggerConfigIdKind idKind)
        {
            IdState ??= new CharacterTestLevelGameplayTriggerIdState();
            switch (idKind)
            {
                case GameplayTriggerConfigIdKind.SourceDefinition:
                    return Allocate(
                        IdState.NextSourceDefinitionId,
                        value => IdState.NextSourceDefinitionId = value);
                case GameplayTriggerConfigIdKind.Condition:
                    return Allocate(
                        IdState.NextConditionId,
                        value => IdState.NextConditionId = value);
                case GameplayTriggerConfigIdKind.Definition:
                    return Allocate(
                        IdState.NextDefinitionId,
                        value => IdState.NextDefinitionId = value);
                case GameplayTriggerConfigIdKind.Route:
                    return Allocate(
                        IdState.NextRouteId,
                        value => IdState.NextRouteId = value);
                case GameplayTriggerConfigIdKind.Binding:
                    return Allocate(
                        IdState.NextBindingId,
                        value => IdState.NextBindingId = value);
                case GameplayTriggerConfigIdKind.SourceBinding:
                    return Allocate(
                        IdState.NextSourceBindingId,
                        value => IdState.NextSourceBindingId = value);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(idKind),
                        "Level authoring cannot allocate this Trigger ID kind.");
            }
        }

        internal bool TryRemoveEntry(string stableId)
        {
            if (Entries == null || string.IsNullOrWhiteSpace(stableId))
                return false;

            int entryIndex = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null
                    && string.Equals(
                        Entries[i].StableId,
                        stableId.Trim(),
                        StringComparison.Ordinal))
                {
                    entryIndex = i;
                    break;
                }
            }

            if (entryIndex < 0)
                return false;

            CharacterTestLevelGameplayTriggerEntry entry = Entries[entryIndex];
            GameplayTriggerAuthoringDocument source =
                Authoring ?? CreateEmptyAuthoring();
            Authoring = new GameplayTriggerAuthoringDocument(
                source.SchemaId,
                source.SchemaVersion,
                RemoveSource(source.Sources, entry.SourceDefinitionId),
                RemoveCondition(source.Conditions, entry.ConditionId),
                RemoveDefinition(source.Definitions, entry.DefinitionId),
                RemoveBinding(source.Bindings, entry.BindingId));

            Retire(
                GameplayTriggerConfigIdKind.SourceDefinition,
                entry.SourceDefinitionId);
            Retire(GameplayTriggerConfigIdKind.Condition, entry.ConditionId);
            Retire(GameplayTriggerConfigIdKind.Definition, entry.DefinitionId);
            if (entry.RouteIds != null)
            {
                for (int i = 0; i < entry.RouteIds.Count; i++)
                {
                    Retire(
                        GameplayTriggerConfigIdKind.Route,
                        entry.DefinitionId,
                        entry.RouteIds[i]);
                }
            }
            Retire(GameplayTriggerConfigIdKind.Binding, entry.BindingId);
            Retire(
                GameplayTriggerConfigIdKind.SourceBinding,
                entry.SourceBindingId);

            if (RegionBindings != null)
            {
                for (int i = RegionBindings.Count - 1; i >= 0; i--)
                {
                    if (RegionBindings[i]?.SourceBindingId
                        == entry.SourceBindingId)
                    {
                        RegionBindings.RemoveAt(i);
                    }
                }
            }

            Entries.RemoveAt(entryIndex);
            return true;
        }

        internal static GameplayTriggerAuthoringDocument CreateEmptyAuthoring()
        {
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                Array.Empty<GameplayTriggerSourceAuthoring>(),
                Array.Empty<GameplayTriggerConditionAuthoring>(),
                Array.Empty<GameplayTriggerDefinitionAuthoring>(),
                Array.Empty<GameplayTriggerBindingAuthoring>());
        }

        private void Retire(
            GameplayTriggerConfigIdKind idKind,
            int primaryId,
            int secondaryId = 0)
        {
            if (primaryId <= 0
                || (idKind == GameplayTriggerConfigIdKind.Route
                    ? secondaryId <= 0
                    : secondaryId != 0))
            {
                return;
            }

            IdState ??= new CharacterTestLevelGameplayTriggerIdState();
            IdState.RetiredIds ??=
                new List<CharacterTestLevelGameplayTriggerRetiredId>();
            for (int i = 0; i < IdState.RetiredIds.Count; i++)
            {
                CharacterTestLevelGameplayTriggerRetiredId row =
                    IdState.RetiredIds[i];
                if (row != null
                    && row.IdKind == idKind
                    && row.PrimaryId == primaryId
                    && row.SecondaryId == secondaryId)
                {
                    return;
                }
            }

            IdState.RetiredIds.Add(
                new CharacterTestLevelGameplayTriggerRetiredId
                {
                    IdKind = idKind,
                    PrimaryId = primaryId,
                    SecondaryId = secondaryId
                });
            IdState.SortRetiredIds();
        }

        private static int Allocate(int nextId, Action<int> assignNextId)
        {
            if (nextId <= 0 || nextId == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Trigger authoring ID range is exhausted or invalid.");
            }

            int allocated = nextId;
            assignNextId(nextId + 1);
            return allocated;
        }

        private static GameplayTriggerAuthoringDocument CloneCanonicalAuthoring(
            GameplayTriggerAuthoringDocument source)
        {
            string json = GameplayTriggerAuthoringJson.Serialize(
                source,
                indented: false);
            if (!GameplayTriggerAuthoringJson.TryDeserialize(
                    json,
                    out GameplayTriggerAuthoringDocument clone,
                    out GameplayTriggerAuthoringJsonDiagnostic diagnostic))
            {
                throw new InvalidOperationException(
                    "Could not normalize Trigger authoring at "
                    + diagnostic.Path
                    + ": "
                    + diagnostic.Message);
            }

            return clone;
        }

        private static GameplayTriggerSourceAuthoring[] RemoveSource(
            GameplayTriggerSourceAuthoring[] rows,
            int sourceDefinitionId)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<GameplayTriggerSourceAuthoring>();

            var result = new List<GameplayTriggerSourceAuthoring>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null
                    && rows[i].SourceDefinitionId != sourceDefinitionId)
                {
                    result.Add(rows[i]);
                }
            }

            return result.ToArray();
        }

        private static GameplayTriggerConditionAuthoring[] RemoveCondition(
            GameplayTriggerConditionAuthoring[] rows,
            int conditionId)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<GameplayTriggerConditionAuthoring>();

            var result = new List<GameplayTriggerConditionAuthoring>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].ConditionId != conditionId)
                    result.Add(rows[i]);
            }

            return result.ToArray();
        }

        private static GameplayTriggerDefinitionAuthoring[] RemoveDefinition(
            GameplayTriggerDefinitionAuthoring[] rows,
            int definitionId)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<GameplayTriggerDefinitionAuthoring>();

            var result = new List<GameplayTriggerDefinitionAuthoring>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].DefinitionId != definitionId)
                    result.Add(rows[i]);
            }

            return result.ToArray();
        }

        private static GameplayTriggerBindingAuthoring[] RemoveBinding(
            GameplayTriggerBindingAuthoring[] rows,
            int bindingId)
        {
            if (rows == null || rows.Length == 0)
                return Array.Empty<GameplayTriggerBindingAuthoring>();

            var result = new List<GameplayTriggerBindingAuthoring>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].BindingId != bindingId)
                    result.Add(rows[i]);
            }

            return result.ToArray();
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerIdState
    {
        public CharacterTestLevelGameplayTriggerIdState()
        {
            NextSourceDefinitionId = 1;
            NextConditionId = 1;
            NextDefinitionId = 1;
            NextRouteId = 1;
            NextBindingId = 1;
            NextSourceBindingId = 1;
            RetiredIds = new List<CharacterTestLevelGameplayTriggerRetiredId>();
        }

        public int NextSourceDefinitionId { get; set; }

        public int NextConditionId { get; set; }

        public int NextDefinitionId { get; set; }

        public int NextRouteId { get; set; }

        public int NextBindingId { get; set; }

        public int NextSourceBindingId { get; set; }

        public List<CharacterTestLevelGameplayTriggerRetiredId> RetiredIds
        {
            get;
            set;
        }

        internal CharacterTestLevelGameplayTriggerIdState Normalize()
        {
            var normalized = new CharacterTestLevelGameplayTriggerIdState
            {
                NextSourceDefinitionId = NextSourceDefinitionId,
                NextConditionId = NextConditionId,
                NextDefinitionId = NextDefinitionId,
                NextRouteId = NextRouteId,
                NextBindingId = NextBindingId,
                NextSourceBindingId = NextSourceBindingId,
                RetiredIds =
                    new List<CharacterTestLevelGameplayTriggerRetiredId>()
            };
            if (RetiredIds != null)
            {
                for (int i = 0; i < RetiredIds.Count; i++)
                {
                    if (RetiredIds[i] != null)
                        normalized.RetiredIds.Add(RetiredIds[i].Normalize());
                }
            }
            normalized.SortRetiredIds();
            return normalized;
        }

        internal void SortRetiredIds()
        {
            RetiredIds?.Sort(
                (left, right) =>
                {
                    int comparison = left.IdKind.CompareTo(right.IdKind);
                    if (comparison != 0)
                        return comparison;
                    comparison = left.PrimaryId.CompareTo(right.PrimaryId);
                    return comparison != 0
                        ? comparison
                        : left.SecondaryId.CompareTo(right.SecondaryId);
                });
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerRetiredId
    {
        public GameplayTriggerConfigIdKind IdKind { get; set; }

        public int PrimaryId { get; set; }

        public int SecondaryId { get; set; }

        internal CharacterTestLevelGameplayTriggerRetiredId Normalize()
        {
            return new CharacterTestLevelGameplayTriggerRetiredId
            {
                IdKind = IdKind,
                PrimaryId = PrimaryId,
                SecondaryId = SecondaryId
            };
        }

        internal GameplayTriggerRetiredIdDescriptor ToDescriptor()
        {
            return new GameplayTriggerRetiredIdDescriptor(
                IdKind,
                PrimaryId,
                SecondaryId);
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerEntry
    {
        public CharacterTestLevelGameplayTriggerEntry()
        {
            StableId = string.Empty;
            DisplayName = string.Empty;
            TemplateId = string.Empty;
            Notes = string.Empty;
            RouteIds = new List<int>();
        }

        public string StableId { get; set; }

        public string DisplayName { get; set; }

        public string TemplateId { get; set; }

        public string Notes { get; set; }

        public int SourceDefinitionId { get; set; }

        public int ConditionId { get; set; }

        public int DefinitionId { get; set; }

        public List<int> RouteIds { get; set; }

        public int BindingId { get; set; }

        public int SourceBindingId { get; set; }

        internal CharacterTestLevelGameplayTriggerEntry Normalize()
        {
            var normalized = new CharacterTestLevelGameplayTriggerEntry
            {
                StableId = Trim(StableId),
                DisplayName = Trim(DisplayName),
                TemplateId = Trim(TemplateId),
                Notes = Trim(Notes),
                SourceDefinitionId = SourceDefinitionId,
                ConditionId = ConditionId,
                DefinitionId = DefinitionId,
                RouteIds = RouteIds == null
                    ? new List<int>()
                    : new List<int>(RouteIds),
                BindingId = BindingId,
                SourceBindingId = SourceBindingId
            };
            normalized.RouteIds.Sort();
            return normalized;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerRegionBinding
    {
        public CharacterTestLevelGameplayTriggerRegionBinding()
        {
            RegionStableId = string.Empty;
        }

        public int SourceBindingId { get; set; }

        public string RegionStableId { get; set; }

        public int ProviderId { get; set; }

        public int LayerMask { get; set; }

        public int MaxQueryHits { get; set; }

        public int MaxMappedSubjects { get; set; }

        public CharacterTestLevelGameplayTriggerSubjectFilter SubjectFilter
        {
            get;
            set;
        }

        internal CharacterTestLevelGameplayTriggerRegionBinding Normalize()
        {
            return new CharacterTestLevelGameplayTriggerRegionBinding
            {
                SourceBindingId = SourceBindingId,
                RegionStableId = string.IsNullOrWhiteSpace(RegionStableId)
                    ? string.Empty
                    : RegionStableId.Trim(),
                ProviderId = ProviderId,
                LayerMask = LayerMask,
                MaxQueryHits = MaxQueryHits,
                MaxMappedSubjects = MaxMappedSubjects,
                SubjectFilter = SubjectFilter?.Normalize()
            };
        }
    }

    internal sealed class CharacterTestLevelGameplayTriggerSubjectFilter
    {
        public GameplayTargetIdentityTypes AcceptedIdentityTypes { get; set; }

        public GameplayTargetCapabilities RequiredCapabilities { get; set; }

        public bool RequireAlive { get; set; }

        public GameplayTargetRelationFilter RelationFilter { get; set; }

        public int ReferenceTeamId { get; set; }

        internal CharacterTestLevelGameplayTriggerSubjectFilter Normalize()
        {
            return new CharacterTestLevelGameplayTriggerSubjectFilter
            {
                AcceptedIdentityTypes = AcceptedIdentityTypes,
                RequiredCapabilities = RequiredCapabilities,
                RequireAlive = RequireAlive,
                RelationFilter = RelationFilter,
                ReferenceTeamId = ReferenceTeamId
            };
        }

        internal GameplayComponentTargetFilter ToRuntimeFilter()
        {
            return new GameplayComponentTargetFilter(
                AcceptedIdentityTypes,
                RequiredCapabilities,
                RequireAlive,
                RelationFilter,
                referenceTeamId: ReferenceTeamId);
        }

        internal static CharacterTestLevelGameplayTriggerSubjectFilter
            CreateTemplateDefault(string templateId)
        {
            bool requiresDamageable =
                string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal);
            return new CharacterTestLevelGameplayTriggerSubjectFilter
            {
                AcceptedIdentityTypes =
                    GameplayTargetIdentityTypes.Character,
                RequiredCapabilities = requiresDamageable
                    ? GameplayTargetCapabilities.Damageable
                    : GameplayTargetCapabilities.None,
                RequireAlive = true,
                RelationFilter = GameplayTargetRelationFilter.Any,
                ReferenceTeamId = 0
            };
        }
    }
}
