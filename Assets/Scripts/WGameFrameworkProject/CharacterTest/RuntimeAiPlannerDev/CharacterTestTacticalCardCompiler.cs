using System;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestTacticalCardSourceMapEntry
    {
        internal CharacterTestTacticalCardSourceMapEntry(string sourcePath, string eventId, string derivation)
        { SourcePath = sourcePath; EventId = eventId; Derivation = derivation; }
        internal string SourcePath { get; }
        internal string EventId { get; }
        internal string Derivation { get; }
    }

    /// <summary>Immutable compiler receipt; all execution is delegated to the shared behavior backend.</summary>
    internal sealed class CharacterTestTacticalCardCompilation
    {
        private readonly CharacterTestTacticalCardSourceMapEntry[] _mapping;
        private readonly RuntimeHashDigest _sourceFingerprintDigest;
        private readonly RuntimeHashDigest _dependencyFingerprintDigest;
        private readonly int _dependencyCharacterId;
        internal CharacterTestTacticalCardCompilation(CharacterTestTacticalCardDefinition source,
            CharacterTestTacticalCardInputs inputs, CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestTacticalCardSourceMapEntry[] mapping)
            : this(source.Name, source.SemanticFingerprint, inputs, program, mapping) { }

        internal CharacterTestTacticalCardCompilation(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalCardInputs inputs,
            CharacterTestTacticalAuthoringCompilation compiled)
            : this(source.Name, compiled.SourceFingerprint, inputs, compiled.Program, CreateAuthoringMap(compiled)) { }

        internal CharacterTestTacticalCardCompilation(string name, string sourceFingerprint, CharacterTestTacticalCardInputs inputs,
            CharacterTestRuntimeAiLabCompiledLogicProgram program, CharacterTestTacticalCardSourceMapEntry[] mapping)
        {
            Program = program;
            Name = name;
            SourceFingerprint = sourceFingerprint;
            DependencyFingerprint = inputs.DependencyFingerprint;
            _dependencyCharacterId = inputs.SourceCharacterId;
            _sourceFingerprintDigest = RuntimeHashDigest.ComputeContentDigest(SourceFingerprint);
            _dependencyFingerprintDigest = RuntimeHashDigest.ComputeContentDigest(DependencyFingerprint);
            ActionId = inputs.Action?.ActionId.Value ?? 0;
            StartupEndFrame = inputs.StartupEndFrame;
            DurationFrames = inputs.DurationFrames;
            ActionSetId = inputs.ActionSetId;
            Summary = inputs.SpatialSummary;
            SelectedRange = inputs.SelectedRange;
            _mapping = mapping == null ? Array.Empty<CharacterTestTacticalCardSourceMapEntry>()
                : (CharacterTestTacticalCardSourceMapEntry[])mapping.Clone();
        }

        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        internal string Name { get; }
        internal string SourceFingerprint { get; }
        internal string DependencyFingerprint { get; }
        internal RuntimeHashDigest SourceFingerprintDigest => _sourceFingerprintDigest;
        internal RuntimeHashDigest DependencyFingerprintDigest => _dependencyFingerprintDigest;
        internal int DependencyKind => _dependencyCharacterId > 0 ? 1 : 0;
        internal int DependencyCharacterId => _dependencyCharacterId;
        internal string ActionSetId { get; }
        internal int ActionId { get; }
        internal int StartupEndFrame { get; }
        internal int DurationFrames { get; }
        internal CharacterActionSpatialSummary Summary { get; }
        internal CharacterTestTacticalActionRange SelectedRange { get; }
        internal ReadOnlySpan<CharacterTestTacticalCardSourceMapEntry> SourceMapping => _mapping;

        private static CharacterTestTacticalCardSourceMapEntry[] CreateAuthoringMap(CharacterTestTacticalAuthoringCompilation compiled)
        {
            var result = new CharacterTestTacticalCardSourceMapEntry[compiled.Events.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var evt = compiled.Events[i];
                result[i] = new CharacterTestTacticalCardSourceMapEntry(evt.SourcePath, evt.EventId,
                    "Authored event [" + evt.StartFrame + "," + evt.EndFrame + "); dependency=" + compiled.DependencyFingerprint);
            }
            return result;
        }

        /// <remarks>LowFreqAlloc. Human-readable compilation evidence, not a Runtime polling API.</remarks>
        internal string Describe()
        {
            var text = new StringBuilder();
            text.AppendLine(Name).Append("ActionSet: ").AppendLine(ActionSetId)
                .Append("LightAttack opener: ").Append(ActionId).AppendLine()
                .Append("Action-local startup: 0..").Append(StartupEndFrame).Append("; duration: ").Append(DurationFrames).AppendLine()
                .Append("Selected first hurtbox surface range (m): ").Append(SelectedRange.MinDistance.Value.ToString("R", CultureInfo.InvariantCulture))
                .Append("..").Append(SelectedRange.MaxDistance.Value.ToString("R", CultureInfo.InvariantCulture))
                .Append("; approach: ").Append(SelectedRange.OptimalDistance.Value.ToString("R", CultureInfo.InvariantCulture)).AppendLine()
                .Append("Full measurement: ").Append(Summary.ProberStatus).Append("; intervals: ").Append(Summary.Intervals.Count).AppendLine()
                .Append("Sector source: ").Append(Summary.Sector.Source).Append("; prober: ").AppendLine(Summary.ProberVersion)
                .Append("Measurement (mm): step=").Append(Summary.Measurement.StepMillimeters)
                .Append(" search=").Append(Summary.Measurement.SearchMinMillimeters).Append("..").Append(Summary.Measurement.SearchMaxMillimeters).AppendLine()
                .Append("Action content: ").AppendLine(Summary.ActionContentHash)
                .Append("Reference dummy: ").AppendLine(Summary.ReferenceDummyHash)
                .Append("Source semantics: ").AppendLine(SourceFingerprint)
                .Append("Binding dependencies: ").AppendLine(DependencyFingerprint)
                .Append("Execution identity: ").Append(Program.Execution.Identity).AppendLine();
            for (int i = 0; i < Program.Execution.Events.Length; i++)
            {
                var evt = Program.Execution.Events[i];
                text.Append(Program.ExecutionDebugMap?.Event(i) ?? i.ToString(CultureInfo.InvariantCulture))
                    .Append(": ").Append(evt.Kind).Append(" [").Append(evt.StartFrame).Append(',').Append(evt.EndFrame).AppendLine(")");
            }
            foreach (var entry in _mapping)
                text.Append(entry.SourcePath).Append(" -> ").Append(entry.EventId).Append(": ").AppendLine(entry.Derivation);
            return text.ToString();
        }
    }

    /// <summary>Expands the confirmed single-attack declaration into the shared task-02 builder exactly once.</summary>
    /// <remarks>LowFreqAlloc. Does not write authoring input, perform measurements or create a second execution format.</remarks>
    internal static class CharacterTestTacticalCardCompiler
    {
        internal static bool TryCompile(CharacterTestTacticalCardDefinition source, CharacterTestTacticalCardInputs inputs,
            out CharacterTestTacticalCardCompilation compilation, out string issue, bool includeDebugMap = true)
        {
            compilation = null;
            issue = string.Empty;
            if (source == null || inputs == null) { issue = "TacticalCard.Compile:InputsMissing"; return false; }
            if (!inputs.TryValidatePreparedAction(out issue)) return false;
            try
            {
                int end = checked(1 + inputs.DurationFrames);
                int moveEnd = checked(2 + inputs.StartupEndFrame);
                var summary = inputs.SelectedRange;
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                    new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), new[] { inputs.Action }));
                var builder = new CharacterTestBehaviorExecutionBuilder(references, "single-primary",
                    source.SemanticFingerprint + ":" + inputs.DependencyFingerprint);
                builder.AddTimeline("single-primary", end);
                int alive = builder.AddTargetAliveCondition("target.alive");
                int range = builder.AddHurtboxDistanceCondition("release.surface-range", (float)summary.MinDistance.Value,
                    (float)summary.MaxDistance.Value);
                int facing = builder.AddTargetFacingCondition("release.facing", summary.Sector.CenterMilliDegrees / 1000f,
                    summary.Sector.ToleranceMilliDegrees / 1000f);
                builder.ActivationCondition = alive;
                int gate = builder.AddAll("release", alive, range, facing);
                int entry = alive;
                if (source.Approach == CharacterTestTacticalCardApproach.OptimalRange)
                    builder.AddMovement("approach", "single-primary", 0, moveEnd,
                        CharacterTestBehaviorExecutionMovement.ApproachOptimalRange((float)summary.MinDistance.Value,
                            (float)summary.MaxDistance.Value, (float)summary.OptimalDistance.Value,
                            metric: CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance));
                builder.AddTurn("facing", "single-primary", 0, end,
                    new CharacterTestBehaviorExecutionTurn(CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget, speed: 1f));
                builder.AddGate("release", "single-primary", 0, gate);
                builder.AddAction("attack", "single-primary", 1, inputs.DurationFrames, inputs.Action.ActionId.Value, entry);
                var program = builder.Build(includeDebugMap);
                var mapping = !includeDebugMap ? null : new[]
                {
                    new CharacterTestTacticalCardSourceMapEntry("slot", "attack", inputs.SlotSource + ": " + inputs.ActionSetId),
                    new CharacterTestTacticalCardSourceMapEntry("approach", "approach", source.Approach == CharacterTestTacticalCardApproach.None
                        ? "No base approach event; independent turn and authored Root Motion remain."
                        : "Nominal Hurtbox surface range/verified optimum; movement end = action start 1 + inclusive StartupEndFrame + 1."),
                    new CharacterTestTacticalCardSourceMapEntry("eligibility", "release/attack", "Gate 0: alive + Hurtbox surface range + facing. Action 1: alive; resource commit remains authoritative. No aggregate hit-opportunity condition is generated."),
                    new CharacterTestTacticalCardSourceMapEntry("facing", "facing", "Independent current-target turn [0,1+DurationFrames); formal action ControlScale applies."),
                    new CharacterTestTacticalCardSourceMapEntry("completion/exit", "single-primary", "Natural end at 1+DurationFrames; no cancel-window shortening, timeout or generated exit guard.")
                };
                compilation = new CharacterTestTacticalCardCompilation(source, inputs, program, mapping);
                return true;
            }
            catch (ArgumentException error) { issue = "TacticalCard.Compile:" + error.Message; }
            catch (InvalidOperationException error) { issue = "TacticalCard.Compile:" + error.Message; }
            catch (OverflowException error) { issue = "TacticalCard.Compile:" + error.Message; }
            return false;
        }
    }
}
