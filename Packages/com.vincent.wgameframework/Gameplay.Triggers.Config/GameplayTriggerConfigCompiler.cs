using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>AllocByDesign deterministic Trigger authoring compiler.</summary>
    public static class GameplayTriggerConfigCompiler
    {
        private const GameplayTriggerOccurrenceKind AllOccurrenceKinds = GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited | GameplayTriggerOccurrenceKind.Pulsed;

        /// <remarks>AllocByDesign. Copies, validates, canonicalizes and lowers the full authoring document.</remarks>
        public static GameplayTriggerCompileResult Compile(GameplayTriggerAuthoringDocument document, GameplayTriggerCompilerOptions options, GameplayTriggerCompilerEnvironment environment)
        {
            var diagnostics = new List<GameplayTriggerConfigDiagnostic>();
            if (!ValidateEntry(document, options, environment, diagnostics)) return Failure(diagnostics);

            GameplayTriggerSourceAuthoring[] sourceRows = CopyAndSort(document.Sources, CompareSources);
            GameplayTriggerConditionAuthoring[] conditionRows = CopyAndSort(document.Conditions, CompareConditions);
            GameplayTriggerDefinitionAuthoring[] definitionRows = CopyAndSort(document.Definitions, CompareDefinitions);
            GameplayTriggerBindingAuthoring[] bindingRows = CopyAndSort(document.Bindings, CompareBindings);
            ValidateTopLevelRows(sourceRows, conditionRows, definitionRows, bindingRows, options, environment, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);

            CompiledCondition[] conditions = CompileConditions(conditionRows, options, diagnostics);
            GameplayTriggerSourceDefinition[] sources = CompileSources(sourceRows, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);
            GameplayTriggerDefinition[] definitions = CompileDefinitions(definitionRows, sources, conditions, options, environment, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);
            BindingCompileRow[] compiledBindings = CompileBindings(bindingRows, definitions, sources, environment, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);

            ValidateExclusiveScopes(definitions, diagnostics);
            ValidateBindingCapabilities(compiledBindings, definitions, sources, environment, diagnostics);
            ValidateInternalTargets(compiledBindings, definitions, environment, diagnostics);
            ValidateUnboundedCycles(compiledBindings, definitionRows, conditions, environment, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);

            GameplayTriggerCompiledActionRequirement[] actionRequirements = CopyActionRequirements(definitions, environment.ActionRequirements);
            GameplayTriggerCompiledSourceRequirement[] sourceRequirements = CopySourceRequirements(compiledBindings);
            GameplayTriggerBinding[] bindings = new GameplayTriggerBinding[compiledBindings.Length];
            for (int i = 0; i < bindings.Length; i++) bindings[i] = compiledBindings[i].Binding;

            if (!TryBuildValidationCatalog(sources, definitions, bindings, actionRequirements, out GameplayTriggerCatalog catalog, diagnostics)) return Failure(diagnostics);
            if (!TryCalculateCapacity(compiledBindings, definitionRows, environment, conditions, out GameplayTriggerRuntimeCapacityShape shape, diagnostics)) return Failure(diagnostics);

            var requirements = new GameplayTriggerRuntimeCapacityRequirements(
                shape,
                HasDelivery(actionRequirements, GameplayTriggerDeliveryContract.InternalDeferred),
                HasDelivery(actionRequirements, GameplayTriggerDeliveryContract.ReliableDeferred));
            var plan = new GameplayTriggerCompiledCatalogPlan(sources, definitions, bindings, actionRequirements, sourceRequirements, shape, requirements, catalog);
            GameplayTriggerExpectedCatalogIdentity expected = environment.ExpectedCatalogIdentity;
            if (expected.HasExpectedGameplayTriggerCatalogHash && expected.ExpectedGameplayTriggerCatalogHash != plan.GameplayTriggerCatalogHash)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.CompiledCatalogHashMismatch, "$.environment.expectedCatalogIdentity.gameplayTriggerCatalogHash", 0, 0, expected.ExpectedGameplayTriggerCatalogHash, plan.GameplayTriggerCatalogHash));
                return Failure(diagnostics);
            }
            return new GameplayTriggerCompileResult(plan, Array.Empty<GameplayTriggerConfigDiagnostic>());
        }

        private static bool ValidateEntry(GameplayTriggerAuthoringDocument document, GameplayTriggerCompilerOptions options, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            if (document == null)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NullDocument, "$"));
                return false;
            }
            if (environment == null)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RegistryInvalid, "$.environment"));
            if (!string.Equals(document.SchemaId, GameplayTriggerConfigSchemas.AuthoringId, StringComparison.Ordinal))
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.AuthoringSchemaMismatch, "$.schemaId"));
            if (document.SchemaVersion != GameplayTriggerConfigSchemas.AuthoringVersion)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.AuthoringVersionMismatch, "$.schemaVersion", 0, 0, GameplayTriggerConfigSchemas.AuthoringVersion, document.SchemaVersion));
            if (options.MaxConditionTreeDepth <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxConditionTreeDepth", 0, 0, 1, options.MaxConditionTreeDepth));
            if (options.MaxConditionInstructions <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxConditionInstructions", 0, 0, 1, options.MaxConditionInstructions));
            if (options.MaxConditionStackDepth <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxConditionStackDepth", 0, 0, 1, options.MaxConditionStackDepth));
            if (options.MaxRoutesPerDefinition <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxRoutesPerDefinition", 0, 0, 1, options.MaxRoutesPerDefinition));
            if (options.MaxActionsPerRoute <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxActionsPerRoute", 0, 0, 1, options.MaxActionsPerRoute));
            if (options.MaxTotalRows <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxTotalRows", 0, 0, 1, options.MaxTotalRows));
            if (document.Sources == null || document.Sources.Length == 0 || document.Conditions == null || document.Conditions.Length == 0 || document.Definitions == null || document.Definitions.Length == 0 || document.Bindings == null || document.Bindings.Length == 0)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NullRow, "$.catalog"));
            return diagnostics.Count == 0;
        }

        private static void ValidateTopLevelRows(GameplayTriggerSourceAuthoring[] sources, GameplayTriggerConditionAuthoring[] conditions, GameplayTriggerDefinitionAuthoring[] definitions, GameplayTriggerBindingAuthoring[] bindings, GameplayTriggerCompilerOptions options, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            long total = (long)sources.Length + conditions.Length + definitions.Length + bindings.Length;
            if (total > options.MaxTotalRows)
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxTotalRows", 0, 0, total, options.MaxTotalRows));
            ValidateRows(sources, "$.sources[null]", r => r.SourceDefinitionId, GameplayTriggerConfigIdKind.SourceDefinition, GameplayTriggerConfigDiagnosticCode.DuplicateSourceId, environment, diagnostics);
            ValidateRows(conditions, "$.conditions[null]", r => r.ConditionId, GameplayTriggerConfigIdKind.Condition, GameplayTriggerConfigDiagnosticCode.DuplicateConditionId, environment, diagnostics);
            ValidateRows(definitions, "$.definitions[null]", r => r.DefinitionId, GameplayTriggerConfigIdKind.Definition, GameplayTriggerConfigDiagnosticCode.DuplicateDefinitionId, environment, diagnostics);
            ValidateRows(bindings, "$.bindings[null]", r => r.BindingId, GameplayTriggerConfigIdKind.Binding, GameplayTriggerConfigDiagnosticCode.DuplicateBindingId, environment, diagnostics);
            var sourceBindingIds = new HashSet<int>();
            for (int i = 0; i < bindings.Length; i++)
            {
                GameplayTriggerBindingAuthoring row = bindings[i];
                if (row == null) continue;
                string path = "$.bindings[id=" + row.BindingId + "].sourceBindingId";
                if (row.SourceBindingId <= 0)
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path, row.BindingId, row.SourceBindingId));
                if (environment.RetiredIds.Contains(GameplayTriggerConfigIdKind.SourceBinding, row.SourceBindingId))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, path, row.SourceBindingId));
                if (!sourceBindingIds.Add(row.SourceBindingId))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId, path, row.BindingId, row.SourceBindingId));
            }
            for (int i = 0; i < environment.ActionRequirements.Count; i++)
            {
                GameplayTriggerActionRequirementDescriptor row = environment.ActionRequirements.Get(i);
                if (environment.RetiredIds.Contains(GameplayTriggerConfigIdKind.ActionKind, row.ActionKindId.Value))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, "$.environment.actionRequirements[kind=" + row.ActionKindId.Value + "]", row.ActionKindId.Value));
            }
            for (int i = 0; i < environment.SourceBindings.Count; i++)
            {
                GameplayTriggerSourceBindingContractDescriptor row = environment.SourceBindings.Get(i);
                if (environment.RetiredIds.Contains(GameplayTriggerConfigIdKind.Provider, row.ProviderId.Value))
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, "$.environment.sourceBindings[id=" + row.SourceBindingId.Value + "].provider", row.ProviderId.Value));
            }
        }

        private static void ValidateRows<T>(T[] rows, string nullPath, Func<T, int> idSelector, GameplayTriggerConfigIdKind idKind, GameplayTriggerConfigDiagnosticCode duplicateCode, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics) where T : class
        {
            int previous = 0;
            bool hasPrevious = false;
            for (int i = 0; i < rows.Length; i++)
            {
                T row = rows[i];
                if (row == null) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NullRow, nullPath)); continue; }
                int id = idSelector(row);
                string setName = idKind == GameplayTriggerConfigIdKind.SourceDefinition ? "sources" : idKind == GameplayTriggerConfigIdKind.Condition ? "conditions" : idKind == GameplayTriggerConfigIdKind.Definition ? "definitions" : "bindings";
                string path = "$." + setName + "[id=" + id + "]";
                if (id <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path, id));
                if (environment.RetiredIds.Contains(idKind, id)) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, path, id));
                if (hasPrevious && previous == id) diagnostics.Add(new GameplayTriggerConfigDiagnostic(duplicateCode, path, id));
                previous = id;
                hasPrevious = true;
            }
        }

        private static CompiledCondition[] CompileConditions(GameplayTriggerConditionAuthoring[] rows, GameplayTriggerCompilerOptions options, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var result = new CompiledCondition[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerConditionAuthoring row = rows[i];
                string rootPath = "$.conditions[id=" + row.ConditionId + "].root";
                if (row.Root == null)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidConditionNode, rootPath, row.ConditionId));
                    continue;
                }
                if (!TryCompileCondition(row, rootPath, options, out GameplayTriggerConditionInstruction[] instructions, out int maxStack, diagnostics)) continue;
                try
                {
                    var program = new GameplayTriggerConditionProgram(instructions);
                    result[i] = new CompiledCondition(row.ConditionId, program, EvaluateConditionConstant(instructions, maxStack) == ConditionConstantValue.AlwaysTrue, maxStack);
                }
                catch (ArgumentException)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidPostfixStack, "$.conditions[id=" + row.ConditionId + "].compiledProgram", row.ConditionId));
                }
            }
            return result;
        }

        private static bool TryCompileCondition(GameplayTriggerConditionAuthoring row, string rootPath, GameplayTriggerCompilerOptions options, out GameplayTriggerConditionInstruction[] instructions, out int maxStack, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var output = new List<GameplayTriggerConditionInstruction>();
            var active = new HashSet<GameplayTriggerConditionNodeAuthoring>();
            var frames = new List<ConditionFrame> { new ConditionFrame(row.Root, rootPath, 1, false) };
            int stackDepth = 0;
            maxStack = 0;
            bool valid = true;
            while (frames.Count > 0)
            {
                int last = frames.Count - 1;
                ConditionFrame frame = frames[last];
                frames.RemoveAt(last);
                GameplayTriggerConditionNodeAuthoring node = frame.Node;
                if (node == null)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidConditionNode, frame.Path, row.ConditionId));
                    valid = false;
                    continue;
                }
                if (frame.Exit)
                {
                    active.Remove(node);
                    if (node.Operation == GameplayTriggerConditionOperation.Not)
                        output.Add(new GameplayTriggerConditionInstruction(node.Operation));
                    else if (node.Operation == GameplayTriggerConditionOperation.All || node.Operation == GameplayTriggerConditionOperation.Any)
                    {
                        output.Add(new GameplayTriggerConditionInstruction(node.Operation));
                        stackDepth--;
                    }
                    continue;
                }
                if (frame.Depth > options.MaxConditionTreeDepth)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ConditionDepthExceeded, rootPath, row.ConditionId, 0, frame.Depth, options.MaxConditionTreeDepth));
                    valid = false;
                    continue;
                }
                if (!active.Add(node))
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ConditionCycle, frame.Path, row.ConditionId));
                    valid = false;
                    continue;
                }
                int childCount = node.Children == null ? 0 : node.Children.Length;
                bool leaf = node.Operation >= GameplayTriggerConditionOperation.PushTrue && node.Operation <= GameplayTriggerConditionOperation.Payload2;
                int expectedChildren = leaf ? 0 : node.Operation == GameplayTriggerConditionOperation.Not ? 1 : node.Operation == GameplayTriggerConditionOperation.All || node.Operation == GameplayTriggerConditionOperation.Any ? 2 : -1;
                if (expectedChildren < 0)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidConditionNode, frame.Path + ".operation", row.ConditionId, (int)node.Operation));
                    active.Remove(node);
                    valid = false;
                    continue;
                }
                if (childCount != expectedChildren)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidConditionArity, frame.Path + ".children", row.ConditionId, 0, expectedChildren, childCount));
                    active.Remove(node);
                    valid = false;
                    continue;
                }
                if (!ValidateLeaf(node, frame.Path, row.ConditionId, diagnostics)) valid = false;
                if (leaf)
                {
                    output.Add(new GameplayTriggerConditionInstruction(node.Operation, node.Operand, node.Comparison));
                    stackDepth++;
                    if (stackDepth > maxStack) maxStack = stackDepth;
                    active.Remove(node);
                }
                else
                {
                    frames.Add(new ConditionFrame(node, frame.Path, frame.Depth, true));
                    for (int child = childCount - 1; child >= 0; child--)
                        frames.Add(new ConditionFrame(node.Children[child], frame.Path + ".children[" + child + "]", frame.Depth + 1, false));
                }
                if ((long)output.Count + frames.Count > (long)options.MaxConditionInstructions * 3L)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ConditionInstructionExceeded, rootPath, row.ConditionId, 0, output.Count, options.MaxConditionInstructions));
                    valid = false;
                    break;
                }
            }
            if (output.Count > options.MaxConditionInstructions)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ConditionInstructionExceeded, rootPath, row.ConditionId, 0, output.Count, options.MaxConditionInstructions));
                valid = false;
            }
            if (maxStack > options.MaxConditionStackDepth)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ConditionStackExceeded, rootPath, row.ConditionId, 0, maxStack, options.MaxConditionStackDepth));
                valid = false;
            }
            instructions = output.ToArray();
            return valid;
        }

        private static ConditionConstantValue EvaluateConditionConstant(GameplayTriggerConditionInstruction[] instructions, int maxStackDepth)
        {
            var stack = new ConditionConstantValue[Math.Max(1, maxStackDepth)];
            int count = 0;
            for (int i = 0; i < instructions.Length; i++)
            {
                GameplayTriggerConditionOperation operation = instructions[i].Operation;
                if (operation == GameplayTriggerConditionOperation.PushTrue)
                {
                    stack[count++] = ConditionConstantValue.AlwaysTrue;
                }
                else if (operation == GameplayTriggerConditionOperation.PushFalse)
                {
                    stack[count++] = ConditionConstantValue.AlwaysFalse;
                }
                else if (operation >= GameplayTriggerConditionOperation.OccurrenceMask && operation <= GameplayTriggerConditionOperation.Payload2)
                {
                    stack[count++] = ConditionConstantValue.RuntimeDependent;
                }
                else if (operation == GameplayTriggerConditionOperation.Not)
                {
                    if (stack[count - 1] == ConditionConstantValue.AlwaysTrue) stack[count - 1] = ConditionConstantValue.AlwaysFalse;
                    else if (stack[count - 1] == ConditionConstantValue.AlwaysFalse) stack[count - 1] = ConditionConstantValue.AlwaysTrue;
                }
                else
                {
                    ConditionConstantValue right = stack[--count];
                    ConditionConstantValue left = stack[count - 1];
                    if (operation == GameplayTriggerConditionOperation.All)
                        stack[count - 1] = left == ConditionConstantValue.AlwaysFalse || right == ConditionConstantValue.AlwaysFalse
                            ? ConditionConstantValue.AlwaysFalse
                            : left == ConditionConstantValue.AlwaysTrue && right == ConditionConstantValue.AlwaysTrue
                                ? ConditionConstantValue.AlwaysTrue
                                : ConditionConstantValue.RuntimeDependent;
                    else
                        stack[count - 1] = left == ConditionConstantValue.AlwaysTrue || right == ConditionConstantValue.AlwaysTrue
                            ? ConditionConstantValue.AlwaysTrue
                            : left == ConditionConstantValue.AlwaysFalse && right == ConditionConstantValue.AlwaysFalse
                                ? ConditionConstantValue.AlwaysFalse
                                : ConditionConstantValue.RuntimeDependent;
                }
            }
            return stack[0];
        }

        private static bool ValidateLeaf(GameplayTriggerConditionNodeAuthoring node, string path, int conditionId, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            bool leaf = node.Operation >= GameplayTriggerConditionOperation.PushTrue && node.Operation <= GameplayTriggerConditionOperation.Payload2;
            if (!leaf) return true;
            bool validComparison = node.Comparison >= GameplayTriggerComparison.Equal && node.Comparison <= GameplayTriggerComparison.GreaterOrEqual;
            bool numeric = node.Operation >= GameplayTriggerConditionOperation.PreviousScalar && node.Operation <= GameplayTriggerConditionOperation.Payload2;
            bool valid = true;
            if (!validComparison || (!numeric && node.Comparison != GameplayTriggerComparison.Equal))
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidComparison, path + ".comparison", conditionId, (int)node.Comparison));
                valid = false;
            }
            if ((node.Operation == GameplayTriggerConditionOperation.PushTrue || node.Operation == GameplayTriggerConditionOperation.PushFalse) && node.Operand != 0L ||
                node.Operation == GameplayTriggerConditionOperation.OccurrenceMask && (node.Operand <= 0L || (node.Operand & ~(long)AllOccurrenceKinds) != 0L) ||
                node.Operation == GameplayTriggerConditionOperation.SignalIdEquals && (node.Operand <= 0L || node.Operand > int.MaxValue) ||
                (node.Operation == GameplayTriggerConditionOperation.HasSubject || node.Operation == GameplayTriggerConditionOperation.HasInstigator) && node.Operand != 0L && node.Operand != 1L)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidConditionOperand, path + ".operand", conditionId, 0, node.Operand));
                valid = false;
            }
            return valid;
        }

        private static GameplayTriggerSourceDefinition[] CompileSources(GameplayTriggerSourceAuthoring[] rows, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var result = new GameplayTriggerSourceDefinition[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerSourceAuthoring row = rows[i];
                string path = "$.sources[id=" + row.SourceDefinitionId + "]";
                bool valid = true;
                if (row.SourceKind < GameplayTriggerSourceKind.Signal || row.SourceKind > GameplayTriggerSourceKind.Threshold) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidSourceKind, path + ".sourceKind", row.SourceDefinitionId)); valid = false; }
                if (row.SignalOrdering < GameplayTriggerSignalOrdering.Ordered || row.SignalOrdering > GameplayTriggerSignalOrdering.UnorderedMultiset) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidSignalOrdering, path + ".signalOrdering", row.SourceDefinitionId)); valid = false; }
                else if (row.SourceKind != GameplayTriggerSourceKind.Signal && row.SignalOrdering != GameplayTriggerSignalOrdering.UnorderedMultiset) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata, path + ".signalOrdering", row.SourceDefinitionId)); valid = false; }
                if (row.ThresholdDirection < GameplayTriggerThresholdDirection.High || row.ThresholdDirection > GameplayTriggerThresholdDirection.Low) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidThresholdDirection, path + ".thresholdDirection", row.SourceDefinitionId)); valid = false; }
                if (row.InitialSamplePolicy < GameplayTriggerInitialSamplePolicy.PrimeSilently || row.InitialSamplePolicy > GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidInitialSamplePolicy, path + ".initialSamplePolicy", row.SourceDefinitionId)); valid = false; }
                if (row.DeparturePolicy < GameplayTriggerSubjectDeparturePolicy.RemoveSilently || row.DeparturePolicy > GameplayTriggerSubjectDeparturePolicy.EmitExited) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidDeparturePolicy, path + ".departurePolicy", row.SourceDefinitionId)); valid = false; }
                Fix64 enter = default;
                Fix64 exit = default;
                if (!TryQuantize(row.EnterThreshold, path + ".enterThreshold", row.SourceDefinitionId, out enter, diagnostics) || !TryQuantize(row.ExitThreshold, path + ".exitThreshold", row.SourceDefinitionId, out exit, diagnostics)) valid = false;
                if (valid && row.SourceKind == GameplayTriggerSourceKind.Threshold && (row.ThresholdDirection == GameplayTriggerThresholdDirection.High ? exit > enter : exit < enter))
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidHysteresis, path + ".threshold", row.SourceDefinitionId, 0, enter.RawValue, exit.RawValue));
                    valid = false;
                }
                if (valid) result[i] = new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(row.SourceDefinitionId), row.SourceKind, row.SignalOrdering, row.ThresholdDirection, enter, exit, row.InitialSamplePolicy, row.DeparturePolicy);
            }
            return result;
        }

        private static bool TryQuantize(GameplayTriggerFix64Authoring value, string path, int id, out Fix64 result, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            if (value.Kind == GameplayTriggerFix64InputKind.Raw) { result = Fix64.FromRaw(value.RawValue); return true; }
            if (value.Kind == GameplayTriggerFix64InputKind.InvariantDecimal && HasSupportedInvariantDecimalScale(value.InvariantDecimal) && value.InvariantDecimal.IndexOf('e') < 0 && value.InvariantDecimal.IndexOf('E') < 0 && decimal.TryParse(value.InvariantDecimal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed))
            {
                decimal minimum = decimal.MinValue / Fix64.Scale;
                decimal maximum = decimal.MaxValue / Fix64.Scale;
                if (parsed >= minimum && parsed <= maximum)
                {
                    decimal scaled = parsed * Fix64.Scale;
                    if (scaled == decimal.Truncate(scaled) && scaled >= long.MinValue && scaled <= long.MaxValue)
                    {
                        result = Fix64.FromRaw((long)scaled);
                        return true;
                    }
                }
            }
            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidHysteresis, path, id));
            result = default;
            return false;
        }

        private static bool HasSupportedInvariantDecimalScale(string value)
        {
            if (value == null) return false;
            int decimalPoint = value.IndexOf('.');
            return decimalPoint < 0 || value.Length - decimalPoint - 1 <= 6;
        }

        private static GameplayTriggerDefinition[] CompileDefinitions(GameplayTriggerDefinitionAuthoring[] rows, GameplayTriggerSourceDefinition[] sources, CompiledCondition[] conditions, GameplayTriggerCompilerOptions options, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var result = new GameplayTriggerDefinition[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerDefinitionAuthoring row = rows[i];
                string path = "$.definitions[id=" + row.DefinitionId + "]";
                if (row.DefinitionVersion <= 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, path + ".definitionVersion", row.DefinitionId));
                if (FindSource(sources, row.SourceDefinitionId) < 0) diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingSourceReference, path + ".sourceDefinitionId", row.DefinitionId, row.SourceDefinitionId));
                if (row.Routes == null || row.Routes.Length == 0 || row.Routes.Length > options.MaxRoutesPerDefinition)
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RouteCountExceeded, path + ".routes", row.DefinitionId, 0, row.Routes == null ? 0 : row.Routes.Length, options.MaxRoutesPerDefinition));
                    continue;
                }
                GameplayTriggerRouteAuthoring[] routeRows = (GameplayTriggerRouteAuthoring[])row.Routes.Clone();
                Array.Sort(routeRows, CompareRoutes);
                var routes = new GameplayTriggerRouteDefinition[routeRows.Length];
                bool validDefinition = row.DefinitionVersion > 0 && FindSource(sources, row.SourceDefinitionId) >= 0;
                for (int routeIndex = 0; routeIndex < routeRows.Length; routeIndex++)
                {
                    GameplayTriggerRouteAuthoring route = routeRows[routeIndex];
                    if (route == null) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NullRow, path + ".routes[null]", row.DefinitionId)); validDefinition = false; continue; }
                    string routePath = path + ".routes[id=" + route.RouteId + "]";
                    bool valid = true;
                    if (route.RouteId <= 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, routePath, row.DefinitionId, route.RouteId)); valid = false; }
                    if (environment.RetiredIds.Contains(GameplayTriggerConfigIdKind.Route, row.DefinitionId, route.RouteId)) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, routePath, row.DefinitionId, route.RouteId)); valid = false; }
                    if (routeIndex > 0 && routeRows[routeIndex - 1] != null && routeRows[routeIndex - 1].RouteId == route.RouteId) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.DuplicateRouteId, routePath, row.DefinitionId, route.RouteId)); valid = false; }
                    if (route.OccurrenceMask == GameplayTriggerOccurrenceKind.None) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.EmptyOccurrenceMask, routePath + ".occurrenceMask", row.DefinitionId, route.RouteId)); valid = false; }
                    else if ((route.OccurrenceMask & ~AllOccurrenceKinds) != 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidOccurrenceMask, routePath + ".occurrenceMask", row.DefinitionId, route.RouteId)); valid = false; }
                    CompiledCondition condition = FindCondition(conditions, route.ConditionId);
                    if (condition.Program == null) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingConditionReference, routePath + ".conditionId", row.DefinitionId, route.ConditionId)); valid = false; }
                    if (!ValidateRoutePolicy(route, routePath, row.DefinitionId, diagnostics)) valid = false;
                    if (route.Actions == null || route.Actions.Length == 0 || route.Actions.Length > options.MaxActionsPerRoute) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ActionCountExceeded, routePath + ".actions", row.DefinitionId, route.RouteId, route.Actions == null ? 0 : route.Actions.Length, options.MaxActionsPerRoute)); valid = false; }
                    var actions = new GameplayTriggerActionInstruction[route.Actions == null ? 0 : route.Actions.Length];
                    for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                    {
                        GameplayTriggerActionAuthoring action = route.Actions[actionIndex];
                        string actionPath = routePath + ".actions[" + actionIndex + "]";
                        bool actionValid = true;
                        if (action.ActionKindId <= 0)
                        {
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidId, actionPath + ".actionKindId", row.DefinitionId, action.ActionKindId));
                            actionValid = false;
                        }
                        else if (environment.RetiredIds.Contains(GameplayTriggerConfigIdKind.ActionKind, action.ActionKindId))
                        {
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RetiredId, actionPath + ".actionKindId", row.DefinitionId, action.ActionKindId));
                            actionValid = false;
                        }
                        else if (!environment.ActionRequirements.TryGet(new GameplayTriggerActionKindId(action.ActionKindId), out _))
                        {
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.UnknownActionContract, actionPath + ".actionKindId", row.DefinitionId, action.ActionKindId));
                            actionValid = false;
                        }
                        if (action.TargetSelector < GameplayTriggerTargetSelector.Owner ||
                            action.TargetSelector > GameplayTriggerTargetSelector.ExplicitBindingTarget ||
                            (action.TargetSelector == GameplayTriggerTargetSelector.ExplicitBindingTarget
                                ? action.TargetSlotId <= 0
                                : action.TargetSlotId != 0))
                        {
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidTargetSelector, actionPath + ".targetSelector", row.DefinitionId, actionIndex));
                            actionValid = false;
                        }
                        if (actionValid) actions[actionIndex] = new GameplayTriggerActionInstruction(new GameplayTriggerActionKindId(action.ActionKindId), action.TargetSelector, action.TargetSlotId, action.Payload0, action.Payload1, action.Payload2);
                        else valid = false;
                    }
                    if (valid) routes[routeIndex] = new GameplayTriggerRouteDefinition(new GameplayTriggerRouteId(route.RouteId), route.OccurrenceMask, condition.Program, actions, new GameplayTriggerActivationPolicy(route.StateScope, route.MaxActivations, route.CooldownFrames, route.PresenceLimit, route.StayIntervalFrames), route.ExecutionOrder, route.ExclusiveGroupId, route.WinnerPriority, route.ArbitrationScope);
                    else validDefinition = false;
                }
                if (validDefinition) result[i] = new GameplayTriggerDefinition(new GameplayTriggerDefinitionId(row.DefinitionId), row.DefinitionVersion, new GameplayTriggerSourceDefinitionId(row.SourceDefinitionId), routes);
            }
            return result;
        }

        private static bool ValidateRoutePolicy(GameplayTriggerRouteAuthoring route, string path, int definitionId, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            bool valid = true;
            if (route.StateScope < GameplayTriggerStateScope.PerInstance || route.StateScope > GameplayTriggerStateScope.PerPair) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidStateScope, path + ".stateScope", definitionId, route.RouteId)); valid = false; }
            if (route.MaxActivations < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NegativeMaxActivations, path + ".maxActivations", definitionId, route.RouteId)); valid = false; }
            if (route.CooldownFrames < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NegativeCooldown, path + ".cooldownFrames", definitionId, route.RouteId)); valid = false; }
            if (route.PresenceLimit < GameplayTriggerPresenceLimit.Ignore || route.PresenceLimit > GameplayTriggerPresenceLimit.OncePerPresence) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidPresenceLimit, path + ".presenceLimit", definitionId, route.RouteId)); valid = false; }
            if (route.StayIntervalFrames < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.NegativeStayInterval, path + ".stayIntervalFrames", definitionId, route.RouteId)); valid = false; }
            if (route.ExclusiveGroupId < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidExclusiveGroup, path + ".exclusiveGroupId", definitionId, route.RouteId)); valid = false; }
            if (route.ArbitrationScope < GameplayTriggerArbitrationScope.Global || route.ArbitrationScope > GameplayTriggerArbitrationScope.PerPair) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidArbitrationScope, path + ".arbitrationScope", definitionId, route.RouteId)); valid = false; }
            if (route.StateScope == GameplayTriggerStateScope.PerInstance ? route.MaxStateKeys != 1 : route.MaxStateKeys <= 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidBindingCapacity, path + ".maxStateKeys", definitionId, route.RouteId, route.MaxStateKeys)); valid = false; }
            if (route.AllowRepeatingCycle ? route.MinIntervalFrames <= 0 || route.CooldownFrames < route.MinIntervalFrames : route.MinIntervalFrames != 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidActionContract, path + ".cyclePolicy", definitionId, route.RouteId, route.MinIntervalFrames, route.CooldownFrames)); valid = false; }
            return valid;
        }

        private static BindingCompileRow[] CompileBindings(GameplayTriggerBindingAuthoring[] rows, GameplayTriggerDefinition[] definitions, GameplayTriggerSourceDefinition[] sources, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var result = new BindingCompileRow[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayTriggerBindingAuthoring row = rows[i];
                string path = "$.bindings[id=" + row.BindingId + "]";
                int definitionIndex = FindDefinition(definitions, row.DefinitionId);
                if (definitionIndex < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingDefinitionReference, path + ".definitionId", row.BindingId, row.DefinitionId)); continue; }
                if ((row.OwnerIndex == 0) != (row.OwnerGeneration == 0) || row.OwnerIndex < 0 || row.OwnerGeneration < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidTargetReference, path + ".owner", row.BindingId)); continue; }
                var targets = new GameplayTriggerBindingTarget[row.ExplicitTargets.Length];
                GameplayTriggerBindingTargetAuthoring[] targetRows = (GameplayTriggerBindingTargetAuthoring[])row.ExplicitTargets.Clone();
                Array.Sort(targetRows, (a, b) => a.SlotId.CompareTo(b.SlotId));
                bool valid = true;
                for (int targetIndex = 0; targetIndex < targetRows.Length; targetIndex++)
                {
                    GameplayTriggerBindingTargetAuthoring target = targetRows[targetIndex];
                    string targetPath = path + ".explicitTargets[slot=" + target.SlotId + "]";
                    if (target.SlotId <= 0 || target.DomainId <= 0 || target.Value <= 0 || target.Generation < 0) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidTargetReference, targetPath, row.BindingId, target.SlotId)); valid = false; continue; }
                    if (targetIndex > 0 && targetRows[targetIndex - 1].SlotId == target.SlotId) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.DuplicateExplicitTargetSlot, targetPath, row.BindingId, target.SlotId)); valid = false; continue; }
                    targets[targetIndex] = new GameplayTriggerBindingTarget(target.SlotId, new GameplayTriggerTargetRef(target.DomainId, target.Value, target.Generation));
                }
                GameplayTriggerSourceBindingId sourceBindingId = new GameplayTriggerSourceBindingId(row.SourceBindingId);
                if (!environment.SourceBindings.TryGet(sourceBindingId, out GameplayTriggerSourceBindingContractDescriptor contract))
                {
                    diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingSourceBindingContract, "$.environment.sourceBindings[id=" + row.SourceBindingId + "]", row.BindingId, row.SourceBindingId));
                    valid = false;
                }
                GameplayTriggerDefinition definition = definitions[definitionIndex];
                int sourceIndex = FindSource(sources, definition.SourceDefinitionId.Value);
                if (valid && contract.SourceDefinitionId != definition.SourceDefinitionId) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceDefinitionContractMismatch, "$.environment.sourceBindings[id=" + row.SourceBindingId + "].sourceDefinitionId", row.SourceBindingId, contract.SourceDefinitionId.Value, definition.SourceDefinitionId.Value)); valid = false; }
                if (valid && contract.SourceKind != sources[sourceIndex].SourceKind) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.SourceKindContractMismatch, "$.environment.sourceBindings[id=" + row.SourceBindingId + "].sourceKind", row.SourceBindingId, (int)contract.SourceKind, (int)sources[sourceIndex].SourceKind)); valid = false; }
                if (valid && contract.SourceKind == GameplayTriggerSourceKind.Signal && contract.SignalOrdering != sources[sourceIndex].SignalOrdering) { diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata, "$.environment.sourceBindings[id=" + row.SourceBindingId + "].signalOrdering", row.SourceBindingId, 0, (int)sources[sourceIndex].SignalOrdering, (int)contract.SignalOrdering)); valid = false; }
                if (!valid) continue;
                var owner = new GameplayEntityId(row.OwnerIndex, row.OwnerGeneration);
                result[i] = new BindingCompileRow(row, new GameplayTriggerBinding(new GameplayTriggerBindingId(row.BindingId), new GameplayTriggerDefinitionId(row.DefinitionId), sourceBindingId, owner, row.InitiallyEnabled, targets), contract);
            }
            return result;
        }

        private static void ValidateExclusiveScopes(GameplayTriggerDefinition[] definitions, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var scopes = new Dictionary<int, GameplayTriggerArbitrationScope>();
            for (int i = 0; i < definitions.Length; i++)
            {
                GameplayTriggerDefinition definition = definitions[i];
                for (int r = 0; r < definition.RouteCount; r++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(r);
                    if (route.ExclusiveGroupId == 0) continue;
                    if (scopes.TryGetValue(route.ExclusiveGroupId, out GameplayTriggerArbitrationScope scope) && scope != route.ArbitrationScope)
                        diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.ExclusiveScopeMismatch, "$.exclusiveGroups[id=" + route.ExclusiveGroupId + "]", route.ExclusiveGroupId, (int)route.ArbitrationScope));
                    else scopes[route.ExclusiveGroupId] = route.ArbitrationScope;
                }
            }
        }

        private static void ValidateBindingCapabilities(BindingCompileRow[] bindings, GameplayTriggerDefinition[] definitions, GameplayTriggerSourceDefinition[] sources, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                BindingCompileRow row = bindings[i];
                GameplayTriggerDefinition definition = definitions[FindDefinition(definitions, row.Authoring.DefinitionId)];
                GameplayTriggerSourceEntityCapability capabilities = row.Contract.EntityCapabilities;
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    string path = "$.environment.sourceBindings[id=" + row.Authoring.SourceBindingId + "].entityCapabilities";
                    if ((route.ActivationPolicy.StateScope == GameplayTriggerStateScope.PerSubject || route.ActivationPolicy.StateScope == GameplayTriggerStateScope.PerPair || route.ArbitrationScope == GameplayTriggerArbitrationScope.PerSubject || route.ArbitrationScope == GameplayTriggerArbitrationScope.PerPair) && (capabilities & GameplayTriggerSourceEntityCapability.SubjectGuaranteed) == 0)
                        diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingSubjectCapability, path, row.Authoring.SourceBindingId, route.RouteId.Value));
                    if ((route.ActivationPolicy.StateScope == GameplayTriggerStateScope.PerInstigator || route.ActivationPolicy.StateScope == GameplayTriggerStateScope.PerPair || route.ArbitrationScope == GameplayTriggerArbitrationScope.PerInstigator || route.ArbitrationScope == GameplayTriggerArbitrationScope.PerPair) && (capabilities & GameplayTriggerSourceEntityCapability.InstigatorGuaranteed) == 0)
                        diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingInstigatorCapability, path, row.Authoring.SourceBindingId, route.RouteId.Value));
                    if (route.ActivationPolicy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence && (capabilities & GameplayTriggerSourceEntityCapability.PresenceEpoch) == 0)
                        diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.OncePerPresenceUnsupported, path, row.Authoring.SourceBindingId, route.RouteId.Value));
                    for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                    {
                        GameplayTriggerTargetSelector selector = route.GetAction(actionIndex).TargetSelector;
                        if (selector == GameplayTriggerTargetSelector.ExplicitBindingTarget && !row.Binding.TryGetExplicitTarget(route.GetAction(actionIndex).TargetSlotId, out _))
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingExplicitTargetSlot, "$.bindings[id=" + row.Authoring.BindingId + "].definition.routes[id=" + route.RouteId.Value + "].actions[" + actionIndex + "].targetSlotId", row.Authoring.BindingId, route.GetAction(actionIndex).TargetSlotId));
                        if (selector == GameplayTriggerTargetSelector.Owner && !row.Binding.Owner.IsValid)
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InvalidTargetReference, "$.bindings[id=" + row.Authoring.BindingId + "].owner", row.Authoring.BindingId));
                        if (selector == GameplayTriggerTargetSelector.Subject && (capabilities & GameplayTriggerSourceEntityCapability.SubjectGuaranteed) == 0)
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingSubjectCapability, path, row.Authoring.SourceBindingId, route.RouteId.Value, actionIndex));
                        if (selector == GameplayTriggerTargetSelector.Instigator && (capabilities & GameplayTriggerSourceEntityCapability.InstigatorGuaranteed) == 0)
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.MissingInstigatorCapability, path, row.Authoring.SourceBindingId, route.RouteId.Value, actionIndex));
                    }
                }
            }
        }

        private static void ValidateInternalTargets(BindingCompileRow[] bindings, GameplayTriggerDefinition[] definitions, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                GameplayTriggerDefinition definition = definitions[FindDefinition(definitions, bindings[i].Authoring.DefinitionId)];
                for (int r = 0; r < definition.RouteCount; r++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(r);
                    for (int a = 0; a < route.ActionCount; a++)
                    {
                        GameplayTriggerActionInstruction action = route.GetAction(a);
                        environment.ActionRequirements.TryGet(action.ActionKindId, out GameplayTriggerActionRequirementDescriptor requirement);
                        if (requirement.DeliveryContract != GameplayTriggerDeliveryContract.InternalDeferred) continue;
                        GameplayTriggerTargetRef target = default;
                        bool valid = action.TargetSelector == GameplayTriggerTargetSelector.ExplicitBindingTarget &&
                            bindings[i].Binding.TryGetExplicitTarget(action.TargetSlotId, out target) &&
                            target.DomainId == GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId &&
                            target.Generation == 0 &&
                            target.Value <= int.MaxValue &&
                            FindBindingBySourceBinding(bindings, (int)target.Value) >= 0;
                        if (valid && requirement.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal)
                        {
                            valid = environment.SourceBindings.TryGet(new GameplayTriggerSourceBindingId((int)target.Value), out GameplayTriggerSourceBindingContractDescriptor targetContract) &&
                                targetContract.SourceKind == GameplayTriggerSourceKind.Signal;
                        }
                        if (valid && requirement.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.SetEnabled)
                            valid = (action.Payload0 == 0L || action.Payload0 == 1L) && action.Payload1 == 0L && action.Payload2 == 0L;
                        else if (valid && requirement.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Reset)
                            valid = action.Payload0 == 0L && action.Payload1 == 0L && action.Payload2 == 0L;
                        if (!valid)
                            diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch, "$.bindings[id=" + bindings[i].Authoring.BindingId + "].definition.routes[id=" + route.RouteId.Value + "].actions[" + a + "]", bindings[i].Authoring.BindingId, action.TargetSlotId));
                    }
                }
            }
        }

        private static void ValidateUnboundedCycles(BindingCompileRow[] bindings, GameplayTriggerDefinitionAuthoring[] definitionRows, CompiledCondition[] conditions, GameplayTriggerCompilerEnvironment environment, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var edges = new List<CycleEdge>();
            for (int i = 0; i < bindings.Length; i++)
            {
                GameplayTriggerDefinitionAuthoring definition = FindDefinitionAuthoring(definitionRows, bindings[i].Authoring.DefinitionId);
                for (int r = 0; r < definition.Routes.Length; r++)
                {
                    GameplayTriggerRouteAuthoring route = definition.Routes[r];
                    CompiledCondition condition = FindCondition(conditions, route.ConditionId);
                    if (!condition.IsUnconditional || (route.OccurrenceMask & GameplayTriggerOccurrenceKind.Pulsed) == 0 || route.MaxActivations > 0 || route.AllowRepeatingCycle && route.MinIntervalFrames > 0 && route.CooldownFrames >= route.MinIntervalFrames) continue;
                    for (int a = 0; a < route.Actions.Length; a++)
                    {
                        GameplayTriggerActionAuthoring action = route.Actions[a];
                        if (!environment.ActionRequirements.TryGet(new GameplayTriggerActionKindId(action.ActionKindId), out GameplayTriggerActionRequirementDescriptor requirement) || requirement.DeliveryContract != GameplayTriggerDeliveryContract.InternalDeferred || requirement.InternalOperationKind != GameplayTriggerInternalDeferredOperationKind.Signal || action.TargetSelector != GameplayTriggerTargetSelector.ExplicitBindingTarget) continue;
                        if (!bindings[i].Binding.TryGetExplicitTarget(action.TargetSlotId, out GameplayTriggerTargetRef target) || target.DomainId != GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId || target.Value > int.MaxValue || FindBindingBySourceBinding(bindings, (int)target.Value) < 0) continue;
                        edges.Add(new CycleEdge(bindings[i].Authoring.SourceBindingId, (int)target.Value, definition.DefinitionId, route.RouteId, a, bindings[i].Authoring.BindingId));
                    }
                }
            }
            edges.Sort(CycleEdge.Compare);
            var adjacency = new Dictionary<int, List<CycleEdge>>();
            for (int i = 0; i < edges.Count; i++)
            {
                if (!adjacency.TryGetValue(edges[i].From, out List<CycleEdge> list)) adjacency[edges[i].From] = list = new List<CycleEdge>();
                list.Add(edges[i]);
            }
            if (TryFindCanonicalCyclicEdge(edges, adjacency, out CycleEdge cycle))
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle, "$.bindings[id=" + cycle.BindingId + "].definition.routes[id=" + cycle.RouteId + "].actions[" + cycle.ActionIndex + "]", cycle.From, cycle.To));
            }
        }

        private static bool TryFindCanonicalCyclicEdge(List<CycleEdge> edges, Dictionary<int, List<CycleEdge>> adjacency, out CycleEdge cycle)
        {
            var nodeSet = new HashSet<int>();
            for (int i = 0; i < edges.Count; i++)
            {
                nodeSet.Add(edges[i].From);
                nodeSet.Add(edges[i].To);
            }
            int[] nodes = new int[nodeSet.Count];
            nodeSet.CopyTo(nodes);
            Array.Sort(nodes);

            var indices = new Dictionary<int, int>();
            var lowLinks = new Dictionary<int, int>();
            var componentByNode = new Dictionary<int, int>();
            var cyclicComponents = new List<bool>();
            var onStack = new HashSet<int>();
            var componentStack = new List<int>();
            var traversal = new List<TarjanFrame>();
            int nextIndex = 0;
            for (int rootIndex = 0; rootIndex < nodes.Length; rootIndex++)
            {
                int root = nodes[rootIndex];
                if (indices.ContainsKey(root)) continue;
                EnterTarjanNode(root, false, 0, nextIndex++, indices, lowLinks, onStack, componentStack, traversal);
                while (traversal.Count > 0)
                {
                    int frameIndex = traversal.Count - 1;
                    TarjanFrame frame = traversal[frameIndex];
                    adjacency.TryGetValue(frame.Node, out List<CycleEdge> outgoing);
                    int outgoingCount = outgoing == null ? 0 : outgoing.Count;
                    if (frame.NextEdgeIndex < outgoingCount)
                    {
                        CycleEdge edge = outgoing[frame.NextEdgeIndex];
                        frame.NextEdgeIndex++;
                        traversal[frameIndex] = frame;
                        if (!indices.ContainsKey(edge.To))
                        {
                            EnterTarjanNode(edge.To, true, frame.Node, nextIndex++, indices, lowLinks, onStack, componentStack, traversal);
                        }
                        else if (onStack.Contains(edge.To))
                        {
                            lowLinks[frame.Node] = Math.Min(lowLinks[frame.Node], indices[edge.To]);
                        }
                        continue;
                    }

                    traversal.RemoveAt(frameIndex);
                    if (frame.HasParent)
                        lowLinks[frame.Parent] = Math.Min(lowLinks[frame.Parent], lowLinks[frame.Node]);
                    if (lowLinks[frame.Node] != indices[frame.Node]) continue;

                    int componentId = cyclicComponents.Count;
                    int componentSize = 0;
                    int popped;
                    do
                    {
                        int last = componentStack.Count - 1;
                        popped = componentStack[last];
                        componentStack.RemoveAt(last);
                        onStack.Remove(popped);
                        componentByNode[popped] = componentId;
                        componentSize++;
                    }
                    while (popped != frame.Node);
                    bool cyclic = componentSize > 1;
                    if (!cyclic && outgoing != null)
                    {
                        for (int i = 0; i < outgoing.Count; i++)
                        {
                            if (outgoing[i].To == frame.Node) { cyclic = true; break; }
                        }
                    }
                    cyclicComponents.Add(cyclic);
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                CycleEdge edge = edges[i];
                int component = componentByNode[edge.From];
                if (component == componentByNode[edge.To] && cyclicComponents[component])
                {
                    cycle = edge;
                    return true;
                }
            }
            cycle = default;
            return false;
        }

        private static void EnterTarjanNode(int node, bool hasParent, int parent, int index, Dictionary<int, int> indices, Dictionary<int, int> lowLinks, HashSet<int> onStack, List<int> componentStack, List<TarjanFrame> traversal)
        {
            indices[node] = index;
            lowLinks[node] = index;
            onStack.Add(node);
            componentStack.Add(node);
            traversal.Add(new TarjanFrame(node, hasParent, parent));
        }

        private static bool TryBuildValidationCatalog(GameplayTriggerSourceDefinition[] sources, GameplayTriggerDefinition[] definitions, GameplayTriggerBinding[] bindings, GameplayTriggerCompiledActionRequirement[] requirements, out GameplayTriggerCatalog catalog, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            var sinks = new List<IGameplayTriggerActionSink>();
            var internalDescriptors = new List<GameplayTriggerInternalDeferredActionDescriptor>();
            for (int i = 0; i < requirements.Length; i++)
            {
                GameplayTriggerCompiledActionRequirement requirement = requirements[i];
                if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred)
                    internalDescriptors.Add(new GameplayTriggerInternalDeferredActionDescriptor(requirement.ActionKindId, requirement.InternalOperationKind, requirement.SignalId));
                else sinks.Add(new ValidationSink(requirement.ActionKindId, requirement.DeliveryContract));
            }
            if (!GameplayTriggerCatalog.TryCreate(sources, definitions, bindings, sinks.ToArray(), internalDescriptors.ToArray(), out catalog, out GameplayTriggerCatalogValidationResult validation))
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected, "$.runtimeCatalog", validation.PrimaryId, validation.SecondaryId, (int)validation.ErrorCode));
                return false;
            }
            return true;
        }

        private static bool TryCalculateCapacity(BindingCompileRow[] bindings, GameplayTriggerDefinitionAuthoring[] definitions, GameplayTriggerCompilerEnvironment environment, CompiledCondition[] conditions, out GameplayTriggerRuntimeCapacityShape shape, List<GameplayTriggerConfigDiagnostic> diagnostics)
        {
            try
            {
                int inputItems = 0, relations = 0, occurrences = 0, candidates = 0, routeStates = 0, intents = 0, prepared = 0, stack = 0, deferred = 0, reliable = 0;
                for (int i = 0; i < bindings.Length; i++)
                {
                    BindingCompileRow binding = bindings[i];
                    GameplayTriggerDefinitionAuthoring definition = FindDefinitionAuthoring(definitions, binding.Authoring.DefinitionId);
                    int input = binding.Contract.MaxInputItemsPerEvaluation;
                    int active = binding.Contract.MaxActiveRelations;
                    inputItems = checked(inputItems + input);
                    relations = checked(relations + active);
                    occurrences = checked(occurrences + (binding.Contract.SourceKind == GameplayTriggerSourceKind.Signal ? input : input + active));
                    var routeWeights = new WeightSet();
                    var actionWeights = new WeightSet();
                    var deferredWeights = new WeightSet();
                    var reliableWeights = new WeightSet();
                    for (int r = 0; r < definition.Routes.Length; r++)
                    {
                        GameplayTriggerRouteAuthoring route = definition.Routes[r];
                        routeStates = checked(routeStates + route.MaxStateKeys);
                        prepared = Math.Max(prepared, route.Actions.Length);
                        CompiledCondition condition = FindCondition(conditions, route.ConditionId);
                        stack = Math.Max(stack, condition.MaxStackDepth);
                        int deferredActions = 0, reliableActions = 0;
                        for (int a = 0; a < route.Actions.Length; a++)
                        {
                            environment.ActionRequirements.TryGet(new GameplayTriggerActionKindId(route.Actions[a].ActionKindId), out GameplayTriggerActionRequirementDescriptor requirement);
                            if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.InternalDeferred) deferredActions++;
                            if (requirement.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred) reliableActions++;
                        }
                        routeWeights.Add(route.OccurrenceMask, 1);
                        actionWeights.Add(route.OccurrenceMask, route.Actions.Length);
                        deferredWeights.Add(route.OccurrenceMask, deferredActions);
                        reliableWeights.Add(route.OccurrenceMask, reliableActions);
                    }
                    candidates = checked(candidates + WeightedMaximum(binding.Contract.SourceKind, input, active, routeWeights));
                    intents = checked(intents + WeightedMaximum(binding.Contract.SourceKind, input, active, actionWeights));
                    deferred = checked(deferred + WeightedMaximum(binding.Contract.SourceKind, input, active, deferredWeights));
                    reliable = checked(reliable + WeightedMaximum(binding.Contract.SourceKind, input, active, reliableWeights));
                }
                shape = new GameplayTriggerRuntimeCapacityShape(bindings.Length, bindings.Length, inputItems, relations, occurrences, candidates, routeStates, candidates, intents, prepared, stack, deferred, reliable);
                return true;
            }
            catch (OverflowException)
            {
                diagnostics.Add(new GameplayTriggerConfigDiagnostic(GameplayTriggerConfigDiagnosticCode.CapacityArithmeticOverflow, "$.capacityRequirements"));
                shape = default;
                return false;
            }
        }

        private static int WeightedMaximum(GameplayTriggerSourceKind kind, int input, int active, WeightSet weights)
        {
            if (kind == GameplayTriggerSourceKind.Signal) return checked(input * weights.Pulsed);
            int overlapDelta = checked(weights.Stayed - weights.Entered - weights.Exited);
            return checked(input * weights.Entered + active * weights.Exited + Math.Min(input, active) * Math.Max(0, overlapDelta));
        }

        private static GameplayTriggerCompiledActionRequirement[] CopyActionRequirements(GameplayTriggerDefinition[] definitions, GameplayTriggerActionRequirementRegistry registry)
        {
            var used = new HashSet<int>();
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
            {
                GameplayTriggerDefinition definition = definitions[definitionIndex];
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    for (int actionIndex = 0; actionIndex < route.ActionCount; actionIndex++)
                        used.Add(route.GetAction(actionIndex).ActionKindId.Value);
                }
            }
            var result = new List<GameplayTriggerCompiledActionRequirement>(used.Count);
            for (int i = 0; i < registry.Count; i++)
            {
                GameplayTriggerActionRequirementDescriptor descriptor = registry.Get(i);
                if (used.Contains(descriptor.ActionKindId.Value)) result.Add(new GameplayTriggerCompiledActionRequirement(descriptor));
            }
            return result.ToArray();
        }

        private static GameplayTriggerCompiledSourceRequirement[] CopySourceRequirements(BindingCompileRow[] bindings)
        {
            var result = new GameplayTriggerCompiledSourceRequirement[bindings.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new GameplayTriggerCompiledSourceRequirement(bindings[i].Contract);
            Array.Sort(result, (a, b) => a.SourceBindingId.CompareTo(b.SourceBindingId));
            return result;
        }

        private static bool HasDelivery(GameplayTriggerCompiledActionRequirement[] requirements, GameplayTriggerDeliveryContract delivery)
        {
            for (int i = 0; i < requirements.Length; i++) if (requirements[i].DeliveryContract == delivery) return true;
            return false;
        }

        private static GameplayTriggerCompileResult Failure(List<GameplayTriggerConfigDiagnostic> diagnostics) => new GameplayTriggerCompileResult(null, GameplayTriggerConfigDiagnosticOrdering.Freeze(diagnostics));
        private static T[] CopyAndSort<T>(T[] rows, Comparison<T> comparison) { T[] copy = (T[])rows.Clone(); Array.Sort(copy, comparison); return copy; }
        private static int CompareSources(GameplayTriggerSourceAuthoring a, GameplayTriggerSourceAuthoring b) => a == null ? b == null ? 0 : -1 : b == null ? 1 : a.SourceDefinitionId.CompareTo(b.SourceDefinitionId);
        private static int CompareConditions(GameplayTriggerConditionAuthoring a, GameplayTriggerConditionAuthoring b) => a == null ? b == null ? 0 : -1 : b == null ? 1 : a.ConditionId.CompareTo(b.ConditionId);
        private static int CompareDefinitions(GameplayTriggerDefinitionAuthoring a, GameplayTriggerDefinitionAuthoring b) => a == null ? b == null ? 0 : -1 : b == null ? 1 : a.DefinitionId.CompareTo(b.DefinitionId);
        private static int CompareBindings(GameplayTriggerBindingAuthoring a, GameplayTriggerBindingAuthoring b) => a == null ? b == null ? 0 : -1 : b == null ? 1 : a.BindingId.CompareTo(b.BindingId);
        private static int CompareRoutes(GameplayTriggerRouteAuthoring a, GameplayTriggerRouteAuthoring b) => a == null ? b == null ? 0 : -1 : b == null ? 1 : a.RouteId.CompareTo(b.RouteId);
        private static int FindSource(GameplayTriggerSourceDefinition[] rows, int id) { for (int i = 0; i < rows.Length; i++) if (rows[i].SourceDefinitionId.Value == id) return i; return -1; }
        private static int FindDefinition(GameplayTriggerDefinition[] rows, int id) { for (int i = 0; i < rows.Length; i++) if (rows[i] != null && rows[i].DefinitionId.Value == id) return i; return -1; }
        private static int FindBindingBySourceBinding(BindingCompileRow[] rows, int sourceBindingId) { for (int i = 0; i < rows.Length; i++) if (rows[i].Authoring.SourceBindingId == sourceBindingId) return i; return -1; }
        private static CompiledCondition FindCondition(CompiledCondition[] rows, int id) { for (int i = 0; i < rows.Length; i++) if (rows[i].ConditionId == id) return rows[i]; return default; }
        private static GameplayTriggerDefinitionAuthoring FindDefinitionAuthoring(GameplayTriggerDefinitionAuthoring[] rows, int id) { for (int i = 0; i < rows.Length; i++) if (rows[i].DefinitionId == id) return rows[i]; return null; }

        private readonly struct CompiledCondition
        {
            public CompiledCondition(int conditionId, GameplayTriggerConditionProgram program, bool unconditional, int maxStackDepth) { ConditionId = conditionId; Program = program; IsUnconditional = unconditional; MaxStackDepth = maxStackDepth; }
            public int ConditionId { get; }
            public GameplayTriggerConditionProgram Program { get; }
            public bool IsUnconditional { get; }
            public int MaxStackDepth { get; }
        }

        private enum ConditionConstantValue
        {
            AlwaysFalse = 0,
            AlwaysTrue = 1,
            RuntimeDependent = 2
        }

        private readonly struct ConditionFrame
        {
            public ConditionFrame(GameplayTriggerConditionNodeAuthoring node, string path, int depth, bool exit) { Node = node; Path = path; Depth = depth; Exit = exit; }
            public GameplayTriggerConditionNodeAuthoring Node { get; }
            public string Path { get; }
            public int Depth { get; }
            public bool Exit { get; }
        }

        private readonly struct BindingCompileRow
        {
            public BindingCompileRow(GameplayTriggerBindingAuthoring authoring, GameplayTriggerBinding binding, GameplayTriggerSourceBindingContractDescriptor contract) { Authoring = authoring; Binding = binding; Contract = contract; }
            public GameplayTriggerBindingAuthoring Authoring { get; }
            public GameplayTriggerBinding Binding { get; }
            public GameplayTriggerSourceBindingContractDescriptor Contract { get; }
        }

        private struct TarjanFrame
        {
            public TarjanFrame(int node, bool hasParent, int parent)
            {
                Node = node;
                HasParent = hasParent;
                Parent = parent;
                NextEdgeIndex = 0;
            }

            public int Node;
            public bool HasParent;
            public int Parent;
            public int NextEdgeIndex;
        }

        private readonly struct CycleEdge
        {
            public CycleEdge(int from, int to, int definitionId, int routeId, int actionIndex, int bindingId) { From = from; To = to; DefinitionId = definitionId; RouteId = routeId; ActionIndex = actionIndex; BindingId = bindingId; }
            public int From { get; }
            public int To { get; }
            public int DefinitionId { get; }
            public int RouteId { get; }
            public int ActionIndex { get; }
            public int BindingId { get; }
            public static int Compare(CycleEdge a, CycleEdge b) { int v = a.From.CompareTo(b.From); if (v != 0) return v; v = a.DefinitionId.CompareTo(b.DefinitionId); if (v != 0) return v; v = a.RouteId.CompareTo(b.RouteId); if (v != 0) return v; v = a.ActionIndex.CompareTo(b.ActionIndex); return v != 0 ? v : a.To.CompareTo(b.To); }
        }

        private struct WeightSet
        {
            public int Entered, Stayed, Exited, Pulsed;
            public void Add(GameplayTriggerOccurrenceKind mask, int value) { if ((mask & GameplayTriggerOccurrenceKind.Entered) != 0) Entered = checked(Entered + value); if ((mask & GameplayTriggerOccurrenceKind.Stayed) != 0) Stayed = checked(Stayed + value); if ((mask & GameplayTriggerOccurrenceKind.Exited) != 0) Exited = checked(Exited + value); if ((mask & GameplayTriggerOccurrenceKind.Pulsed) != 0) Pulsed = checked(Pulsed + value); }
        }

        private sealed class ValidationSink : IGameplayTriggerActionSink
        {
            public ValidationSink(GameplayTriggerActionKindId actionKindId, GameplayTriggerDeliveryContract deliveryContract) { ActionKindId = actionKindId; DeliveryContract = deliveryContract; }
            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; }
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) { token = 0; return false; }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }
    }
}
