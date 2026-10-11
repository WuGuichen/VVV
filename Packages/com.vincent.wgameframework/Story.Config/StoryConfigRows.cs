using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Story.Config
{
    public sealed class StoryGraphConfig : IConfigData
    {
        public StoryGraphConfig()
        {
            SourcePath = string.Empty;
            Version = StoryDirector.SchemaVersion;
        }

        public StoryGraphConfig(
            int id,
            int entryBeatId,
            int version = StoryDirector.SchemaVersion,
            string sourcePath = null)
        {
            Id = id;
            EntryBeatId = entryBeatId;
            Version = version;
            SourcePath = sourcePath ?? string.Empty;
        }

        public int Id { get; set; }
        public int Version { get; set; }
        public int EntryBeatId { get; set; }
        public string SourcePath { get; set; }

        public static ConfigSchema<StoryGraphConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryGraphConfig>(
                "StoryGraph",
                displayName: "Story Graph",
                description: "Story graph header. Beats, steps, branches, choices, and facts are stored in sibling Story config tables.",
                structureKind: ConfigStructureKind.Graph);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("Version", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "EntryBeatId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule(
                        "EntryBeatId",
                        "StoryBeat",
                        ConfigStructureKind.Table,
                        severity: ConfigValidationSeverity.Error)))
                .AddField(new ConfigField("SourcePath", ConfigFieldType.String));

            return schema;
        }
    }

    public sealed class StoryBeatConfig : IConfigData
    {
        public StoryBeatConfig()
        {
            TriggerIds = Array.Empty<int>();
        }

        public StoryBeatConfig(
            int id,
            int graphId,
            int sortOrder = 0,
            int choiceSetId = 0,
            IReadOnlyList<int> triggerIds = null)
        {
            Id = id;
            GraphId = graphId;
            SortOrder = sortOrder;
            ChoiceSetId = choiceSetId;
            TriggerIds = CopyIds(triggerIds);
        }

        public int Id { get; set; }
        public int GraphId { get; set; }
        public int SortOrder { get; set; }
        public int ChoiceSetId { get; set; }
        public int[] TriggerIds { get; set; }

        public static ConfigSchema<StoryBeatConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryBeatConfig>(
                "StoryBeat",
                displayName: "Story Beat",
                description: "Story beat row owned by one StoryGraph.",
                structureKind: ConfigStructureKind.Table);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule(
                        "GraphId",
                        "StoryGraph",
                        ConfigStructureKind.Graph,
                        severity: ConfigValidationSeverity.Error)))
                .AddField(new ConfigField("SortOrder", ConfigFieldType.Integer))
                .AddField(new ConfigField("ChoiceSetId", ConfigFieldType.Integer))
                .AddField(new ConfigField("TriggerIds", ConfigFieldType.Custom, valueType: typeof(int[])));

            return schema;
        }

        internal static int[] CopyIds(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Array.Empty<int>();

            var copy = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                copy[i] = ids[i];
            return copy;
        }
    }

    public sealed class StoryStepConfig : IConfigData
    {
        public StoryStepConfig()
        {
            Kind = StoryStepKind.None;
            WaitPolicy = StoryPresentationWaitPolicy.NoWait;
            FactValueKind = StoryValueKind.None;
        }

        public StoryStepConfig(
            int id,
            int graphId,
            int beatId,
            StoryStepKind kind,
            int sortOrder = 0,
            int textKey = 0,
            int speakerId = 0,
            int resourceId = 0,
            StoryPresentationWaitPolicy waitPolicy = StoryPresentationWaitPolicy.NoWait,
            int factNamespace = 0,
            int factId = 0,
            StoryValueKind factValueKind = StoryValueKind.None,
            long factValueRaw = 0L,
            int auxId = 0)
        {
            Id = id;
            GraphId = graphId;
            BeatId = beatId;
            Kind = kind;
            SortOrder = sortOrder;
            TextKey = textKey;
            SpeakerId = speakerId;
            ResourceId = resourceId;
            WaitPolicy = waitPolicy;
            FactNamespace = factNamespace;
            FactId = factId;
            FactValueKind = factValueKind;
            FactValueRaw = factValueRaw;
            AuxId = auxId;
        }

        public int Id { get; set; }
        public int GraphId { get; set; }
        public int BeatId { get; set; }
        public int SortOrder { get; set; }
        public StoryStepKind Kind { get; set; }
        public int TextKey { get; set; }
        public int SpeakerId { get; set; }
        public int ResourceId { get; set; }
        public StoryPresentationWaitPolicy WaitPolicy { get; set; }
        public int FactNamespace { get; set; }
        public int FactId { get; set; }
        public StoryValueKind FactValueKind { get; set; }
        public long FactValueRaw { get; set; }
        public int AuxId { get; set; }

        public static ConfigSchema<StoryStepConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryStepConfig>(
                "StoryStep",
                displayName: "Story Step",
                description: "Story beat step row. SetFact rows map FactNamespace/FactId/FactValueKind/FactValueRaw into StoryFactKey and StoryValue.",
                structureKind: ConfigStructureKind.Table);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("GraphId", "StoryGraph", ConfigStructureKind.Graph)))
                .AddField(new ConfigField(
                    "BeatId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("BeatId", "StoryBeat", ConfigStructureKind.Table)))
                .AddField(new ConfigField("SortOrder", ConfigFieldType.Integer))
                .AddField(new ConfigField("Kind", ConfigFieldType.Enum, required: true, enumId: "StoryStepKind"))
                .AddField(new ConfigField(
                    "TextKey",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("TextKey", "StoryText", ConfigStructureKind.Localization, required: false)))
                .AddField(new ConfigField("SpeakerId", ConfigFieldType.Integer))
                .AddField(new ConfigField("ResourceId", ConfigFieldType.Integer))
                .AddField(new ConfigField("WaitPolicy", ConfigFieldType.Enum, enumId: "StoryPresentationWaitPolicy"))
                .AddField(new ConfigField("FactNamespace", ConfigFieldType.Integer))
                .AddField(new ConfigField(
                    "FactId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("FactId", "StoryFact", ConfigStructureKind.Table, required: false)))
                .AddField(new ConfigField("FactValueKind", ConfigFieldType.Enum, enumId: "StoryValueKind"))
                .AddField(new ConfigField("FactValueRaw", ConfigFieldType.Custom, valueType: typeof(long)))
                .AddField(new ConfigField("AuxId", ConfigFieldType.Integer));

            return schema;
        }
    }

    public sealed class StoryBranchConfig : IConfigData
    {
        public StoryBranchConfig()
        {
        }

        public StoryBranchConfig(
            int id,
            int graphId,
            int beatId,
            int targetBeatId,
            int conditionFactId = 0,
            int priority = 0,
            bool isFallback = false)
        {
            Id = id;
            GraphId = graphId;
            BeatId = beatId;
            TargetBeatId = targetBeatId;
            ConditionFactId = conditionFactId;
            Priority = priority;
            IsFallback = isFallback;
        }

        public int Id { get; set; }
        public int GraphId { get; set; }
        public int BeatId { get; set; }
        public int TargetBeatId { get; set; }
        public int ConditionFactId { get; set; }
        public int Priority { get; set; }
        public bool IsFallback { get; set; }

        public static ConfigSchema<StoryBranchConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryBranchConfig>(
                "StoryBranch",
                displayName: "Story Branch",
                description: "Story beat branch row. ConditionFactId references a bool Story fact by id; namespace resolution follows StoryDirector graph/global lookup.",
                structureKind: ConfigStructureKind.Table);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("GraphId", "StoryGraph", ConfigStructureKind.Graph)))
                .AddField(new ConfigField(
                    "BeatId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("BeatId", "StoryBeat", ConfigStructureKind.Table)))
                .AddField(new ConfigField(
                    "TargetBeatId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("TargetBeatId", "StoryBeat", ConfigStructureKind.Table, required: false)))
                .AddField(new ConfigField(
                    "ConditionFactId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("ConditionFactId", "StoryFact", ConfigStructureKind.Table, required: false)))
                .AddField(new ConfigField("Priority", ConfigFieldType.Integer))
                .AddField(new ConfigField("IsFallback", ConfigFieldType.Boolean));

            return schema;
        }
    }

    public sealed class StoryChoiceConfig : IConfigData
    {
        public StoryChoiceConfig()
        {
            EffectIds = Array.Empty<int>();
        }

        public StoryChoiceConfig(
            int id,
            int graphId,
            int beatId,
            int labelTextKey,
            int targetBeatId,
            int sortOrder = 0,
            int conditionFactId = 0,
            IReadOnlyList<int> effectIds = null)
        {
            Id = id;
            GraphId = graphId;
            BeatId = beatId;
            LabelTextKey = labelTextKey;
            TargetBeatId = targetBeatId;
            SortOrder = sortOrder;
            ConditionFactId = conditionFactId;
            EffectIds = StoryBeatConfig.CopyIds(effectIds);
        }

        public int Id { get; set; }
        public int GraphId { get; set; }
        public int BeatId { get; set; }
        public int SortOrder { get; set; }
        public int LabelTextKey { get; set; }
        public int TargetBeatId { get; set; }
        public int ConditionFactId { get; set; }
        public int[] EffectIds { get; set; }

        public static ConfigSchema<StoryChoiceConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryChoiceConfig>(
                "StoryChoice",
                displayName: "Story Choice",
                description: "Story choice row owned by one beat.",
                structureKind: ConfigStructureKind.Table);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("GraphId", "StoryGraph", ConfigStructureKind.Graph)))
                .AddField(new ConfigField(
                    "BeatId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("BeatId", "StoryBeat", ConfigStructureKind.Table)))
                .AddField(new ConfigField("SortOrder", ConfigFieldType.Integer))
                .AddField(new ConfigField(
                    "LabelTextKey",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule("LabelTextKey", "StoryText", ConfigStructureKind.Localization)))
                .AddField(new ConfigField(
                    "TargetBeatId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("TargetBeatId", "StoryBeat", ConfigStructureKind.Table, required: false)))
                .AddField(new ConfigField(
                    "ConditionFactId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("ConditionFactId", "StoryFact", ConfigStructureKind.Table, required: false)))
                .AddField(new ConfigField("EffectIds", ConfigFieldType.Custom, valueType: typeof(int[])));

            return schema;
        }
    }

    public sealed class StoryFactConfig : IConfigData
    {
        public StoryFactConfig()
        {
            ValueKind = StoryValueKind.None;
        }

        public StoryFactConfig(int id, int @namespace, StoryValueKind valueKind)
        {
            Id = id;
            Namespace = @namespace;
            ValueKind = valueKind;
        }

        public int Id { get; set; }
        public int Namespace { get; set; }
        public StoryValueKind ValueKind { get; set; }

        public StoryFactKey Key => new StoryFactKey(Namespace, Id);

        public static ConfigSchema<StoryFactConfig> CreateSchema()
        {
            var schema = new ConfigSchema<StoryFactConfig>(
                "StoryFact",
                displayName: "Story Fact",
                description: "Story fact declaration row. Namespace plus Id forms the deterministic StoryFactKey.",
                structureKind: ConfigStructureKind.Table);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("Namespace", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("ValueKind", ConfigFieldType.Enum, required: true, enumId: "StoryValueKind"));

            return schema;
        }
    }

    public static class StoryConfigSchemas
    {
        public static IReadOnlyList<ConfigSchema> CreateAll()
        {
            return new ConfigSchema[]
            {
                StoryGraphConfig.CreateSchema(),
                StoryBeatConfig.CreateSchema(),
                StoryStepConfig.CreateSchema(),
                StoryBranchConfig.CreateSchema(),
                StoryChoiceConfig.CreateSchema(),
                StoryFactConfig.CreateSchema()
            };
        }
    }
}
