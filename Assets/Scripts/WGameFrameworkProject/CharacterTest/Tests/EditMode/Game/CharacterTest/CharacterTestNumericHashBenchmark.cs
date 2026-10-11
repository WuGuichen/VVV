using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Unity.Profiling;

namespace MxFramework.Tests.Game.CharacterTest
{
    /// <summary>Explicit benchmark entry; not run implicitly by the test runner.</summary>
    public static class CharacterTestNumericHashBenchmark
    {
        public static object Run(string label)
        {
            if (UnityEditorInternal.ProfilerDriver.deepProfiling)
                throw new InvalidOperationException("Disable Deep Profiling before measuring numeric hash migration.");
            const int warmup = 300, measured = 600, repetitions = 3;
            var rows = new List<object>();
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
                runner.BeginReplayRecording();
                var slice = runner.ServerSlice;
                var participant = slice.Services.Network.CreateSnapshotParticipant();
                var accumulator = (RuntimeHashAccumulator)typeof(CharacterTestNetworkSnapshotParticipant)
                    .GetField("_stateHashAccumulator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(participant);
                var totalMs = new double[measured];
                var hashMs = new double[measured];
                var hashValues = new long[measured];
                var fields = new long[measured];
                var bytes = new long[measured];
                var allocationEvents = new int[measured];
                var timer = new Stopwatch();
                using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                allocations.Stop();
                for (int frame = 0; frame < warmup + measured; frame++)
                {
                    long start = Stopwatch.GetTimestamp();
                    runner.Tick(1d / 60d);
                    long hashStart = Stopwatch.GetTimestamp();
                    allocations.Reset(); allocations.Start();
                    long hash = participant.ComputeStateHash(slice.CurrentFrame);
                    allocations.Stop();
                    long end = Stopwatch.GetTimestamp();
                    if (frame < warmup) continue;
                    int index = frame - warmup;
                    totalMs[index] = (end - start) * 1000d / Stopwatch.Frequency;
                    hashMs[index] = (end - hashStart) * 1000d / Stopwatch.Frequency;
                    hashValues[index] = hash;
                    fields[index] = accumulator.FieldCount;
                    bytes[index] = accumulator.EncodedByteCount;
                    allocationEvents[index] = allocations.Count;
                }
                rows.Add(new { repetition, serverActors = slice.CharacterRuntime.Entities.Count,
                    clientActors = runner.ClientSlice.CharacterRuntime.Entities.Count,
                    recording = runner.IsReplayRecording, rootHash = Summarize(hashMs), simulationAndRootHash = Summarize(totalMs),
                    fields, bytes, allocationEvents, hashValues });
            }
            var report = new { label, unity = UnityEngine.Application.unityVersion, deepProfiling = false,
                scope = "Controlled EditMode paired-world loopback, replay enabled, one extra post-tick root hash; excludes rendering and full Lab UI",
                warmup, measured, repetitions, rows };
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/NumericHashMigration"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, label + ".json"), JsonConvert.SerializeObject(report, Formatting.Indented));
            return new { label, directory, repetitions, measured };
        }

        private static object Summarize(double[] samples)
        {
            double sum = 0;
            foreach (double value in samples) sum += value;
            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            return new { meanMs = sum / samples.Length, p95Ms = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1], maxMs = sorted[sorted.Length - 1] };
        }
    }
}
