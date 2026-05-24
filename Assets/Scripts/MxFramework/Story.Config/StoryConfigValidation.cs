using System;
using System.Collections.Generic;

namespace MxFramework.Story.Config
{
    public enum StoryConfigValidationDiagnosticCode
    {
        ConfigSetIsNull = 1,
        GraphNotFound = 2,
        InvalidStableId = 3,
        DuplicateStableId = 4,
        MissingEntryBeat = 5,
        InvalidBeatReference = 6,
        InvalidBranchTarget = 7,
        InvalidChoiceTarget = 8,
        UnsupportedStepKind = 9,
        UnsupportedWaitPolicy = 10,
        InvalidTextReference = 11,
        InvalidFactReference = 12,
        InvalidFactValue = 13,
        InvalidTriggerId = 14,
        InvalidEffectId = 15
    }

    public readonly struct StoryConfigValidationDiagnostic
    {
        public StoryConfigValidationDiagnostic(
            StoryConfigValidationDiagnosticCode code,
            string sourcePath,
            string tableName,
            int rowId,
            string fieldPath,
            string message,
            int graphId = 0,
            int beatId = 0,
            int stableId = 0)
        {
            Code = code;
            SourcePath = sourcePath ?? string.Empty;
            TableName = tableName ?? string.Empty;
            RowId = rowId;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
            GraphId = graphId;
            BeatId = beatId;
            StableId = stableId;
        }

        public StoryConfigValidationDiagnosticCode Code { get; }
        public string SourcePath { get; }
        public string TableName { get; }
        public int RowId { get; }
        public string FieldPath { get; }
        public string Message { get; }
        public int GraphId { get; }
        public int BeatId { get; }
        public int StableId { get; }
    }

    public sealed class StoryConfigValidationResult
    {
        private readonly StoryConfigValidationDiagnostic[] _diagnostics;

        public StoryConfigValidationResult(IReadOnlyList<StoryConfigValidationDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                _diagnostics = Array.Empty<StoryConfigValidationDiagnostic>();
                return;
            }

            _diagnostics = new StoryConfigValidationDiagnostic[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                _diagnostics[i] = diagnostics[i];
        }

        public IReadOnlyList<StoryConfigValidationDiagnostic> Diagnostics => _diagnostics;
        public bool IsValid => _diagnostics.Length == 0;
        public bool Succeeded => IsValid;
        public int DiagnosticCount => _diagnostics.Length;

        public bool Contains(StoryConfigValidationDiagnosticCode code)
        {
            for (int i = 0; i < _diagnostics.Length; i++)
            {
                if (_diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }
    }

    public static class StoryConfigValidator
    {
        public static StoryConfigValidationResult Validate(
            StoryConfigSet configSet,
            int graphId,
            StoryConfigReferenceIndex referenceIndex = null,
            string sourcePath = null)
        {
            var diagnostics = new List<StoryConfigValidationDiagnostic>();
            string resolvedSourcePath = ResolveSourcePath(configSet, graphId, sourcePath);

            if (configSet == null)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.ConfigSetIsNull,
                    resolvedSourcePath,
                    string.Empty,
                    0,
                    string.Empty,
                    "Story config set is null.",
                    graphId));
                return new StoryConfigValidationResult(diagnostics);
            }

            if (graphId <= 0)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidStableId,
                    resolvedSourcePath,
                    "StoryGraph",
                    graphId,
                    "Id",
                    "Story graph id must be positive.",
                    graphId,
                    stableId: graphId));
                return new StoryConfigValidationResult(diagnostics);
            }

            StoryGraphConfig graph = FindGraph(configSet.Graphs, graphId);
            ValidateGraphRows(configSet.Graphs, graphId, resolvedSourcePath, diagnostics);
            if (graph == null)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.GraphNotFound,
                    resolvedSourcePath,
                    "StoryGraph",
                    graphId,
                    "Id",
                    "Story graph config row was not found.",
                    graphId,
                    stableId: graphId));
                return new StoryConfigValidationResult(diagnostics);
            }

            var beatIds = new HashSet<int>();
            var factKinds = new Dictionary<StoryFactKey, StoryValueKind>();
            ValidateFacts(configSet.Facts, graphId, resolvedSourcePath, factKinds, diagnostics);
            ValidateBeats(configSet.Beats, graphId, resolvedSourcePath, beatIds, diagnostics);
            ValidateSteps(configSet.Steps, graphId, resolvedSourcePath, beatIds, factKinds, referenceIndex, diagnostics);
            ValidateBranches(configSet.Branches, graphId, resolvedSourcePath, beatIds, factKinds, diagnostics);
            ValidateChoices(configSet.Choices, graphId, resolvedSourcePath, beatIds, factKinds, referenceIndex, diagnostics);

            if (!beatIds.Contains(graph.EntryBeatId))
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.MissingEntryBeat,
                    resolvedSourcePath,
                    "StoryGraph",
                    graph.Id,
                    "EntryBeatId",
                    "Story graph entry beat does not exist in StoryBeat rows.",
                    graph.Id,
                    stableId: graph.EntryBeatId));
            }

            return new StoryConfigValidationResult(diagnostics);
        }

        private static void ValidateGraphRows(
            IReadOnlyList<StoryGraphConfig> graphs,
            int requestedGraphId,
            string sourcePath,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            var graphIds = new HashSet<int>();
            for (int i = 0; i < graphs.Count; i++)
            {
                StoryGraphConfig graph = graphs[i];
                if (graph == null)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryGraph", 0, "Graphs[" + i + "]", requestedGraphId, 0, "Story graph row is null.");
                    continue;
                }

                if (graph.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryGraph", graph.Id, "Id", graph.Id, 0, "Story graph id must be positive.");
                    continue;
                }

                if (!graphIds.Add(graph.Id))
                {
                    AddDuplicate(diagnostics, sourcePath, "StoryGraph", graph.Id, "Id", graph.Id, 0, "Duplicate Story graph stable id.");
                }
            }
        }

        private static void ValidateBeats(
            IReadOnlyList<StoryBeatConfig> beats,
            int graphId,
            string sourcePath,
            HashSet<int> beatIds,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < beats.Count; i++)
            {
                StoryBeatConfig beat = beats[i];
                if (beat == null || beat.GraphId != graphId)
                    continue;

                if (beat.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryBeat", beat.Id, "Id", graphId, 0, "Story beat id must be positive.");
                    continue;
                }

                if (!beatIds.Add(beat.Id))
                    AddDuplicate(diagnostics, sourcePath, "StoryBeat", beat.Id, "Id", graphId, beat.Id, "Duplicate Story beat stable id in graph.");

                if (beat.ChoiceSetId < 0)
                    AddInvalidStableId(diagnostics, sourcePath, "StoryBeat", beat.Id, "ChoiceSetId", graphId, beat.Id, "Story choice set id cannot be negative.");

                int[] triggerIds = beat.TriggerIds ?? Array.Empty<int>();
                for (int triggerIndex = 0; triggerIndex < triggerIds.Length; triggerIndex++)
                {
                    if (triggerIds[triggerIndex] <= 0)
                    {
                        diagnostics.Add(new StoryConfigValidationDiagnostic(
                            StoryConfigValidationDiagnosticCode.InvalidTriggerId,
                            sourcePath,
                            "StoryBeat",
                            beat.Id,
                            "TriggerIds[" + triggerIndex + "]",
                            "Story trigger id must be positive.",
                            graphId,
                            beat.Id,
                            triggerIds[triggerIndex]));
                    }
                }
            }
        }

        private static void ValidateSteps(
            IReadOnlyList<StoryStepConfig> steps,
            int graphId,
            string sourcePath,
            HashSet<int> beatIds,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            StoryConfigReferenceIndex referenceIndex,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            var stepIds = new HashSet<ScopedStableId>();
            for (int i = 0; i < steps.Count; i++)
            {
                StoryStepConfig step = steps[i];
                if (step == null || step.GraphId != graphId)
                    continue;

                if (step.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryStep", step.Id, "Id", graphId, step.BeatId, "Story step id must be positive.");
                    continue;
                }

                if (!beatIds.Contains(step.BeatId))
                {
                    AddBeatReference(diagnostics, sourcePath, "StoryStep", step.Id, "BeatId", graphId, step.BeatId, "Story step references a missing beat.");
                }

                if (!stepIds.Add(new ScopedStableId(step.BeatId, step.Id)))
                    AddDuplicate(diagnostics, sourcePath, "StoryStep", step.Id, "Id", graphId, step.BeatId, "Duplicate Story step stable id in beat.");

                if (!IsSupportedStepKind(step.Kind))
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.UnsupportedStepKind,
                        sourcePath,
                        "StoryStep",
                        step.Id,
                        "Kind",
                        "Story step kind is unsupported by Story core.",
                        graphId,
                        step.BeatId,
                        (int)step.Kind));
                }

                if (!IsSupportedWaitPolicy(step.WaitPolicy))
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.UnsupportedWaitPolicy,
                        sourcePath,
                        "StoryStep",
                        step.Id,
                        "WaitPolicy",
                        "Story presentation wait policy is unsupported by Story core.",
                        graphId,
                        step.BeatId,
                        (int)step.WaitPolicy));
                }

                ValidateStepText(step, graphId, sourcePath, referenceIndex, diagnostics);
                ValidateStepFact(step, graphId, sourcePath, factKinds, referenceIndex, diagnostics);
            }
        }

        private static void ValidateBranches(
            IReadOnlyList<StoryBranchConfig> branches,
            int graphId,
            string sourcePath,
            HashSet<int> beatIds,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            var branchIds = new HashSet<ScopedStableId>();
            for (int i = 0; i < branches.Count; i++)
            {
                StoryBranchConfig branch = branches[i];
                if (branch == null || branch.GraphId != graphId)
                    continue;

                if (branch.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryBranch", branch.Id, "Id", graphId, branch.BeatId, "Story branch id must be positive.");
                    continue;
                }

                if (!beatIds.Contains(branch.BeatId))
                    AddBeatReference(diagnostics, sourcePath, "StoryBranch", branch.Id, "BeatId", graphId, branch.BeatId, "Story branch references a missing owner beat.");

                if (!branchIds.Add(new ScopedStableId(branch.BeatId, branch.Id)))
                    AddDuplicate(diagnostics, sourcePath, "StoryBranch", branch.Id, "Id", graphId, branch.BeatId, "Duplicate Story branch stable id in beat.");

                if (branch.TargetBeatId < 0 || (branch.TargetBeatId > 0 && !beatIds.Contains(branch.TargetBeatId)))
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.InvalidBranchTarget,
                        sourcePath,
                        "StoryBranch",
                        branch.Id,
                        "TargetBeatId",
                        "Story branch targets a missing beat.",
                        graphId,
                        branch.BeatId,
                        branch.TargetBeatId));
                }

                ValidateConditionFact(
                    branch.ConditionFactId,
                    graphId,
                    branch.BeatId,
                    branch.Id,
                    "StoryBranch",
                    "ConditionFactId",
                    sourcePath,
                    factKinds,
                    diagnostics);
            }
        }

        private static void ValidateChoices(
            IReadOnlyList<StoryChoiceConfig> choices,
            int graphId,
            string sourcePath,
            HashSet<int> beatIds,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            StoryConfigReferenceIndex referenceIndex,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            var choiceIds = new HashSet<ScopedStableId>();
            for (int i = 0; i < choices.Count; i++)
            {
                StoryChoiceConfig choice = choices[i];
                if (choice == null || choice.GraphId != graphId)
                    continue;

                if (choice.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryChoice", choice.Id, "Id", graphId, choice.BeatId, "Story choice id must be positive.");
                    continue;
                }

                if (!beatIds.Contains(choice.BeatId))
                    AddBeatReference(diagnostics, sourcePath, "StoryChoice", choice.Id, "BeatId", graphId, choice.BeatId, "Story choice references a missing owner beat.");

                if (!choiceIds.Add(new ScopedStableId(choice.BeatId, choice.Id)))
                    AddDuplicate(diagnostics, sourcePath, "StoryChoice", choice.Id, "Id", graphId, choice.BeatId, "Duplicate Story choice stable id in beat.");

                if (choice.TargetBeatId < 0 || (choice.TargetBeatId > 0 && !beatIds.Contains(choice.TargetBeatId)))
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.InvalidChoiceTarget,
                        sourcePath,
                        "StoryChoice",
                        choice.Id,
                        "TargetBeatId",
                        "Story choice targets a missing beat.",
                        graphId,
                        choice.BeatId,
                        choice.TargetBeatId));
                }

                ValidateTextKey(
                    choice.LabelTextKey,
                    graphId,
                    choice.BeatId,
                    choice.Id,
                    "StoryChoice",
                    "LabelTextKey",
                    sourcePath,
                    referenceIndex,
                    diagnostics);

                ValidateConditionFact(
                    choice.ConditionFactId,
                    graphId,
                    choice.BeatId,
                    choice.Id,
                    "StoryChoice",
                    "ConditionFactId",
                    sourcePath,
                    factKinds,
                    diagnostics);

                int[] effectIds = choice.EffectIds ?? Array.Empty<int>();
                for (int effectIndex = 0; effectIndex < effectIds.Length; effectIndex++)
                {
                    if (effectIds[effectIndex] <= 0)
                    {
                        diagnostics.Add(new StoryConfigValidationDiagnostic(
                            StoryConfigValidationDiagnosticCode.InvalidEffectId,
                            sourcePath,
                            "StoryChoice",
                            choice.Id,
                            "EffectIds[" + effectIndex + "]",
                            "Story choice effect id must be positive.",
                            graphId,
                            choice.BeatId,
                            effectIds[effectIndex]));
                    }
                }
            }
        }

        private static void ValidateFacts(
            IReadOnlyList<StoryFactConfig> facts,
            int graphId,
            string sourcePath,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            for (int i = 0; i < facts.Count; i++)
            {
                StoryFactConfig fact = facts[i];
                if (fact == null)
                    continue;

                if (fact.Id <= 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryFact", fact.Id, "Id", graphId, 0, "Story fact id must be positive.");
                    continue;
                }

                if (fact.Namespace < 0)
                {
                    AddInvalidStableId(diagnostics, sourcePath, "StoryFact", fact.Id, "Namespace", graphId, 0, "Story fact namespace cannot be negative.");
                    continue;
                }

                if (!IsSupportedValueKind(fact.ValueKind))
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.InvalidFactValue,
                        sourcePath,
                        "StoryFact",
                        fact.Id,
                        "ValueKind",
                        "Story fact value kind is unsupported.",
                        graphId,
                        0,
                        (int)fact.ValueKind));
                    continue;
                }

                StoryFactKey key = fact.Key;
                if (factKinds.ContainsKey(key))
                {
                    AddDuplicate(diagnostics, sourcePath, "StoryFact", fact.Id, "Id", graphId, 0, "Duplicate Story fact stable key.");
                    continue;
                }

                factKinds.Add(key, fact.ValueKind);
            }
        }

        private static void ValidateStepText(
            StoryStepConfig step,
            int graphId,
            string sourcePath,
            StoryConfigReferenceIndex referenceIndex,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            if (step.Kind == StoryStepKind.Line)
            {
                ValidateTextKey(
                    step.TextKey,
                    graphId,
                    step.BeatId,
                    step.Id,
                    "StoryStep",
                    "TextKey",
                    sourcePath,
                    referenceIndex,
                    diagnostics);
                return;
            }

            if (step.TextKey > 0)
            {
                ValidateTextKey(
                    step.TextKey,
                    graphId,
                    step.BeatId,
                    step.Id,
                    "StoryStep",
                    "TextKey",
                    sourcePath,
                    referenceIndex,
                    diagnostics);
            }
        }

        private static void ValidateStepFact(
            StoryStepConfig step,
            int graphId,
            string sourcePath,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            StoryConfigReferenceIndex referenceIndex,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            if (step.Kind != StoryStepKind.SetFact)
                return;

            if (step.FactNamespace < 0 || step.FactId <= 0)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidFactReference,
                    sourcePath,
                    "StoryStep",
                    step.Id,
                    step.FactId <= 0 ? "FactId" : "FactNamespace",
                    "Story set-fact step must reference a declared Story fact.",
                    graphId,
                    step.BeatId,
                    step.FactId));
                return;
            }

            StoryFactKey key = new StoryFactKey(step.FactNamespace, step.FactId);
            if (!factKinds.TryGetValue(key, out StoryValueKind declaredKind))
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidFactReference,
                    sourcePath,
                    "StoryStep",
                    step.Id,
                    "FactId",
                    "Story set-fact step references a missing Story fact.",
                    graphId,
                    step.BeatId,
                    step.FactId));
                return;
            }

            if (!IsSupportedValueKind(step.FactValueKind) || step.FactValueKind != declaredKind)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidFactValue,
                    sourcePath,
                    "StoryStep",
                    step.Id,
                    "FactValueKind",
                    "Story set-fact value kind must match the declared Story fact kind.",
                    graphId,
                    step.BeatId,
                    (int)step.FactValueKind));
                return;
            }

            if (step.FactValueKind == StoryValueKind.StringRef)
            {
                if (step.FactValueRaw <= 0L || step.FactValueRaw > int.MaxValue)
                {
                    diagnostics.Add(new StoryConfigValidationDiagnostic(
                        StoryConfigValidationDiagnosticCode.InvalidTextReference,
                        sourcePath,
                        "StoryStep",
                        step.Id,
                        "FactValueRaw",
                        "Story string-ref fact value must be a positive 32-bit text key.",
                        graphId,
                        step.BeatId,
                        0));
                    return;
                }

                ValidateTextKey(
                    (int)step.FactValueRaw,
                    graphId,
                    step.BeatId,
                    step.Id,
                    "StoryStep",
                    "FactValueRaw",
                    sourcePath,
                    referenceIndex,
                    diagnostics);
            }
        }

        private static void ValidateConditionFact(
            int conditionFactId,
            int graphId,
            int beatId,
            int rowId,
            string tableName,
            string fieldPath,
            string sourcePath,
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            if (conditionFactId <= 0)
                return;

            StoryValueKind kind;
            if (!TryFindConditionFactKind(factKinds, graphId, conditionFactId, out kind))
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidFactReference,
                    sourcePath,
                    tableName,
                    rowId,
                    fieldPath,
                    "Story condition references a missing graph/global Story fact.",
                    graphId,
                    beatId,
                    conditionFactId));
                return;
            }

            if (kind != StoryValueKind.Bool)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidFactReference,
                    sourcePath,
                    tableName,
                    rowId,
                    fieldPath,
                    "Story condition fact must be declared as Bool.",
                    graphId,
                    beatId,
                    conditionFactId));
            }
        }

        private static void ValidateTextKey(
            int textKey,
            int graphId,
            int beatId,
            int rowId,
            string tableName,
            string fieldPath,
            string sourcePath,
            StoryConfigReferenceIndex referenceIndex,
            List<StoryConfigValidationDiagnostic> diagnostics)
        {
            if (textKey <= 0)
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidTextReference,
                    sourcePath,
                    tableName,
                    rowId,
                    fieldPath,
                    "Story text reference key must be positive.",
                    graphId,
                    beatId,
                    textKey));
                return;
            }

            if (referenceIndex != null && referenceIndex.HasTextKeys && !referenceIndex.ContainsTextKey(textKey))
            {
                diagnostics.Add(new StoryConfigValidationDiagnostic(
                    StoryConfigValidationDiagnosticCode.InvalidTextReference,
                    sourcePath,
                    tableName,
                    rowId,
                    fieldPath,
                    "Story text reference key was not found in the reference index.",
                    graphId,
                    beatId,
                    textKey));
            }
        }

        private static bool TryFindConditionFactKind(
            Dictionary<StoryFactKey, StoryValueKind> factKinds,
            int graphId,
            int conditionFactId,
            out StoryValueKind kind)
        {
            if (factKinds.TryGetValue(new StoryFactKey(graphId, conditionFactId), out kind))
                return true;

            return factKinds.TryGetValue(new StoryFactKey(0, conditionFactId), out kind);
        }

        private static StoryGraphConfig FindGraph(IReadOnlyList<StoryGraphConfig> graphs, int graphId)
        {
            for (int i = 0; i < graphs.Count; i++)
            {
                StoryGraphConfig graph = graphs[i];
                if (graph != null && graph.Id == graphId)
                    return graph;
            }

            return null;
        }

        private static string ResolveSourcePath(StoryConfigSet configSet, int graphId, string sourcePath)
        {
            if (!string.IsNullOrEmpty(sourcePath))
                return sourcePath;

            if (configSet == null)
                return string.Empty;

            StoryGraphConfig graph = FindGraph(configSet.Graphs, graphId);
            return graph == null ? string.Empty : graph.SourcePath ?? string.Empty;
        }

        private static bool IsSupportedStepKind(StoryStepKind kind)
        {
            return kind == StoryStepKind.Line
                || kind == StoryStepKind.Presentation
                || kind == StoryStepKind.SetFact
                || kind == StoryStepKind.Wait;
        }

        private static bool IsSupportedWaitPolicy(StoryPresentationWaitPolicy policy)
        {
            return policy == StoryPresentationWaitPolicy.NoWait
                || policy == StoryPresentationWaitPolicy.WaitForCommand
                || policy == StoryPresentationWaitPolicy.WaitWithFrameTimeout;
        }

        private static bool IsSupportedValueKind(StoryValueKind kind)
        {
            return kind == StoryValueKind.Bool
                || kind == StoryValueKind.Int32
                || kind == StoryValueKind.Int64
                || kind == StoryValueKind.Fix64
                || kind == StoryValueKind.StringRef;
        }

        private static void AddInvalidStableId(
            List<StoryConfigValidationDiagnostic> diagnostics,
            string sourcePath,
            string tableName,
            int rowId,
            string fieldPath,
            int graphId,
            int beatId,
            string message)
        {
            diagnostics.Add(new StoryConfigValidationDiagnostic(
                StoryConfigValidationDiagnosticCode.InvalidStableId,
                sourcePath,
                tableName,
                rowId,
                fieldPath,
                message,
                graphId,
                beatId,
                rowId));
        }

        private static void AddDuplicate(
            List<StoryConfigValidationDiagnostic> diagnostics,
            string sourcePath,
            string tableName,
            int rowId,
            string fieldPath,
            int graphId,
            int beatId,
            string message)
        {
            diagnostics.Add(new StoryConfigValidationDiagnostic(
                StoryConfigValidationDiagnosticCode.DuplicateStableId,
                sourcePath,
                tableName,
                rowId,
                fieldPath,
                message,
                graphId,
                beatId,
                rowId));
        }

        private static void AddBeatReference(
            List<StoryConfigValidationDiagnostic> diagnostics,
            string sourcePath,
            string tableName,
            int rowId,
            string fieldPath,
            int graphId,
            int beatId,
            string message)
        {
            diagnostics.Add(new StoryConfigValidationDiagnostic(
                StoryConfigValidationDiagnosticCode.InvalidBeatReference,
                sourcePath,
                tableName,
                rowId,
                fieldPath,
                message,
                graphId,
                beatId,
                beatId));
        }

        private readonly struct ScopedStableId : IEquatable<ScopedStableId>
        {
            public ScopedStableId(int scopeId, int stableId)
            {
                ScopeId = scopeId;
                StableId = stableId;
            }

            private int ScopeId { get; }
            private int StableId { get; }

            public bool Equals(ScopedStableId other)
            {
                return ScopeId == other.ScopeId && StableId == other.StableId;
            }

            public override bool Equals(object obj)
            {
                return obj is ScopedStableId other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ScopeId * 397) ^ StableId;
                }
            }
        }
    }
}
