using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorRecipeV5Tests
    {
        [Test]
        public void ExecutionBaseline_RecordsSixLegacyMovementStrategies()
        {
            var rows = new List<object>();
            var distances = new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f };
            // Enum values 0..5 are the frozen six legacy algorithms, even after new strategies are added.
            for (int strategy = 0; strategy < 6; strategy++)
            for (int band = 0; band < 2; band++)
            for (int identity = 0; identity < 4; identity++)
            {
                var stage = new CharacterTestRuntimeAiLabCompiledLogicStage(
                    new CharacterTestRuntimeAiLabLogicStage
                    {
                        StageId = identity < 2 ? "timeline.event.orbit.a" : "timeline.event.orbit.b",
                        Order = identity,
                        Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                        MovementStrategy = (CharacterTestRuntimeAiLabMovementStrategy)strategy,
                        MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                        MoveMinDistance = band == 0 ? 0f : 1f,
                        MoveMaxDistance = band == 0 ? 0f : 2f,
                        MoveSpeedMultiplier = 1f
                    }, Array.Empty<CharacterTestRuntimeAiLabCompiledCondition>());
                foreach (float distance in distances)
                {
                    var sample = new CharacterTestRuntimeAiLabMovementTargetSample(true, distance, Vector2.right, Vector2.right);
                    int entityId = 17 + identity;
                    Vector2 move = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, sample, entityId);
                    rows.Add(new { strategy, band, sourceId = stage.StageId, order = stage.Order, entityId,
                        distance, eligible = CharacterTestRuntimeAiLabMovementResolver.IsEligible(stage, sample), x = move.x, z = move.y });
                }
                Assert.AreEqual(Vector2.zero, CharacterTestRuntimeAiLabMovementResolver.Resolve(
                    stage, new CharacterTestRuntimeAiLabMovementTargetSample(false, 0f, Vector2.right, Vector2.right), 17));
            }
            string directory = Path.Combine(Application.dataPath, "../Library/Task02ExecutionEvidence");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "legacy-movement-v1.json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
            TestContext.Progress.WriteLine("Legacy movement baseline: " + rows.Count + " samples; excludes changed channel arbitration and new strategy semantics.");
        }

        // Kept identical across the old/new execution paths. Results are local evidence, not assets.
        [TestCase(3, 211)]
        [TestCase(96, 211)]
        [TestCase(3, 1000000)]
        public void ExecutionBaseline_RecordsCompileGraphAndWarmedSampling(int eventCount, int length)
        {
            var source = CreateExecutionBaselineSource(eventCount, length);
            string sourceJson = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(source.Clone());
            for (int i = 0; i < 4; i++)
            {
                var warmup = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source.Clone());
                Assert.IsTrue(warmup.Success, JsonConvert.SerializeObject(warmup.Diagnostics));
            }

            const int compileCount = 32;
            var inputs = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload[compileCount];
            for (int i = 0; i < inputs.Length; i++) inputs[i] = source.Clone();
            using (var probeCapture = new ExecutionAllocationCapture())
            {
                var probe = new byte[8192];
                GC.KeepAlive(probe);
                Assert.Greater(probeCapture.Stop(), 0, "GC.Alloc recording must detect a known allocation before accepting zero-allocation evidence.");
            }
            using var compileCapture = new ExecutionAllocationCapture();
            long started = Stopwatch.GetTimestamp();
            CharacterTestRuntimeAiLabCompiledLogicProgram program = null;
            for (int i = 0; i < inputs.Length; i++)
                program = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(inputs[i]).Program;
            long elapsed = Stopwatch.GetTimestamp() - started;
            long compileAllocations = compileCapture.Stop();
            Assert.NotNull(program);

            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(16);
            Assert.IsTrue(evaluator.Start(0, events));
            for (int frame = 1; frame <= 256; frame++) evaluator.Tick(frame, events);
            int readsBefore = runtime.EvaluateCount;
            using var tickCapture = new ExecutionAllocationCapture();
            started = Stopwatch.GetTimestamp();
            const int samples = 4096;
            for (int frame = 257; frame < 257 + samples; frame++) evaluator.Tick(frame, events);
            long tickElapsed = Stopwatch.GetTimestamp() - started;
            long tickAllocations = tickCapture.Stop();
            Assert.Greater(runtime.EvaluateCount, readsBefore, "The measurement must sample a live Gate, not an already completed evaluator.");
            Assert.AreEqual(0, tickAllocations, "Warmed event sampling must not allocate.");

            var graph = new ExecutionGraphMeasurement();
            graph.Visit(program);
            var report = new
            {
                measurementVersion = 2,
                unityVersion = Application.unityVersion,
                pointerBytes = IntPtr.Size,
                eventCount,
                timelineLength = length,
                sourceJsonBytes = Encoding.UTF8.GetByteCount(sourceJson),
                sourceSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(source.Clone()),
                compilerCalls = compileCount,
                compileMicrosecondsMean = elapsed * 1000000d / Stopwatch.Frequency / compileCount,
                compileAllocationSamplesMean = (double)compileAllocations / compileCount,
                reachableReferenceObjects = graph.ReferenceCount,
                estimatedRetainedBytes = graph.EstimatedBytes,
                retainedSizeMethod = "recursive instance fields; 2-pointer object header, 4-pointer array header, pointer alignment; estimate, not heap-profiler retained size",
                sampledFrames = samples,
                warmedTickMicrosecondsMean = tickElapsed * 1000000d / Stopwatch.Frequency / samples,
                warmedTickAllocationSamples = tickAllocations,
                allocationMethod = "Unity GC.Alloc marker count, current thread, per-sample recording, positive allocation control; marker values are time, not bytes. GC.GetAllocatedBytesForCurrentThread is unavailable on this Editor",
                scope = "compiler + live evaluator with deterministic runtime stub; excludes real bridge and geometry cost"
            };
            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            string directory = Path.Combine(Application.dataPath, "../Library/Task02ExecutionEvidence");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"baseline-{eventCount}-{length}.json"), json);
            TestContext.Progress.WriteLine(json);
        }

        private sealed class ExecutionAllocationCapture : IDisposable
        {
            private ProfilerRecorder _recorder;
            public ExecutionAllocationCapture()
            {
                _recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 131072,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                if (!_recorder.Valid) throw new InvalidOperationException("GC.Alloc recorder is unavailable.");
            }
            public long Stop()
            {
                _recorder.Stop();
                if (_recorder.WrappedAround || _recorder.Count >= _recorder.Capacity)
                    throw new InvalidOperationException("Allocation recording capacity was exhausted.");
                return _recorder.Count;
            }
            public void Dispose() => _recorder.Dispose();
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateExecutionBaselineSource(int count, int length)
        {
            var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                EntryTimelineId = "baseline"
            };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "baseline" };
            source.Timelines.Add(timeline);
            for (int i = 0; i < count - 1; i++)
            {
                timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = "move." + i.ToString("D3"), TimelineId = "baseline",
                    StartFrame = 0, DurationFrames = length,
                    TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                    MaxExecutionsPerPass = 1, Priority = count - i,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                        MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                        MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                    }
                });
            }
            // Freeze the cursor at zero while world frames and repeated condition sampling continue.
            var gate = CreateWaitEvent("gate", "baseline", 0);
            gate.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            gate.ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "gate.condition",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "gate.predicate",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget
                }
            };
            timeline.Events.Add(gate);
            return source;
        }

        private sealed class ExecutionGraphMeasurement
        {
            private readonly HashSet<object> _visited = new HashSet<object>(ExecutionReferenceComparer.Instance);
            public int ReferenceCount { get; private set; }
            public long EstimatedBytes { get; private set; }
            public void Visit(object value)
            {
                if (value == null) return;
                Type type = value.GetType();
                if (!type.IsValueType && !_visited.Add(value)) return;
                if (!type.IsValueType) ReferenceCount++;
                if (value is string text)
                {
                    EstimatedBytes += Align(2 * IntPtr.Size + 4 + 2 * (text.Length + 1));
                    return;
                }
                if (value is Array array)
                {
                    Type element = type.GetElementType();
                    EstimatedBytes += Align(4 * IntPtr.Size + array.LongLength * FieldBytes(element));
                    foreach (object item in array) Visit(item);
                    return;
                }
                if (!type.IsValueType)
                {
                    long size = 2 * IntPtr.Size;
                    foreach (FieldInfo field in Fields(type)) size += FieldBytes(field.FieldType);
                    EstimatedBytes += Align(size);
                }
                VisitChildren(value);
            }
            private void VisitChildren(object value)
            {
                if (value == null) return;
                Type type = value.GetType();
                if (type.IsPrimitive || type.IsEnum || value is string) return;
                if (!type.IsValueType && !_visited.Contains(value)) { Visit(value); return; }
                foreach (FieldInfo field in Fields(type)) Visit(field.GetValue(value));
            }
            private static IEnumerable<FieldInfo> Fields(Type type)
            {
                for (; type != null; type = type.BaseType)
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        yield return field;
            }
            private static long FieldBytes(Type type)
            {
                if (!type.IsValueType) return IntPtr.Size;
                if (type.IsEnum) return FieldBytes(Enum.GetUnderlyingType(type));
                if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)) return 1;
                if (type == typeof(char) || type == typeof(short) || type == typeof(ushort)) return 2;
                if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
                if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
                long size = 0;
                foreach (FieldInfo field in Fields(type)) size += FieldBytes(field.FieldType);
                return size;
            }
            private static long Align(long bytes) => (bytes + IntPtr.Size - 1) / IntPtr.Size * IntPtr.Size;
        }

        private sealed class ExecutionReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ExecutionReferenceComparer Instance = new ExecutionReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
