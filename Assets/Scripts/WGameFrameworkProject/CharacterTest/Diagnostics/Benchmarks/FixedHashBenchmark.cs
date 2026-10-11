#if UNITY_EDITOR || CHARACTER_TEST_HASH_BENCHMARK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Diagnostics
{
    /// <summary>Explicit diagnostic benchmark; no automatic Editor startup or normal Player execution.</summary>
    public static class FixedHashBenchmark
    {
        public const int InputCount = 65536;
        private const int BatchCount = 31;
        private static ulong _sink;
        private static readonly string EvidenceDirectory = Path.GetFullPath("Library/FixedHashOptimization");

        public static object CaptureSession(string label)
        {
            var captured = new List<HashPair>(2700);
            var rounds = new List<object>();
            for (int round = 0; round < 3; round++)
            {
                using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
                var inner = PrepareAuthority(runner);
                var recording = runner.BeginReplayRecording();
                if (!recording.Recording || !runner.IsReplayRecording)
                    throw new InvalidOperationException("Replay recording did not start: " + recording.Summary);
                var slice = runner.ServerSlice;
                var participant = slice.Services.Network.CreateSnapshotParticipant();
                var authority = inner.ServerSessionAuthorityParticipant;
                var samples = new double[600];
                for (int i = 0; i < 900; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    runner.Tick(1d / 60d);
                    long end = Stopwatch.GetTimestamp();
                    if (i < 300) continue;
                    samples[i - 300] = (end - start) * 1000d / Stopwatch.Frequency;
                    var frame = new RuntimeFrame(slice.CurrentFrame.Value - 1);
                    long gameplay = participant.ComputeStateHash(frame);
                    long authorityHash = authority.AuthorityHash;
                    if (!((ICharacterTestNetworkReplayEvidenceProvider)inner).TryGetLatestReplayEvidence(out var evidenceFrame, out long result)
                        || evidenceFrame != frame
                        || result != CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(gameplay, authorityHash))
                        throw new InvalidOperationException("The real completed-frame result did not use the production combination.");
                    captured.Add(new HashPair(gameplay, authorityHash));
                }
                rounds.Add(new { round, actors = slice.CharacterRuntime.Entities.Count, tick = Summarize(samples) });
            }
            Directory.CreateDirectory(EvidenceDirectory);
            if (label == "after")
            {
                var baseline = ReadCorpus();
                for (int i = 0; i < captured.Count; i++)
                    if (captured[i].First != baseline[i].First || captured[i].Second != baseline[i].Second)
                        throw new InvalidOperationException("Upstream session hash inputs changed at captured sample " + i + ".");
            }
            if (label == "before")
            {
                var corpus = new HashPair[InputCount];
                int at = 0;
                foreach (var pair in captured) corpus[at++] = pair;
                foreach (long value in new[] { 0L, 1L, -1L, long.MinValue, long.MaxValue })
                {
                    corpus[at++] = new HashPair(value, value);
                    corpus[at++] = new HashPair(value, 0);
                    corpus[at++] = new HashPair(0, value);
                }
                for (int i = 0; i < 64; i++)
                {
                    long bit = unchecked((long)(1UL << i));
                    corpus[at++] = new HashPair(bit, 0);
                    corpus[at++] = new HashPair(0, bit);
                }
                ulong random = 1001;
                while (at < corpus.Length) corpus[at++] = new HashPair(unchecked((long)Next(ref random)), unchecked((long)Next(ref random)));
                using var file = new BinaryWriter(File.Create(Path.Combine(EvidenceDirectory, "corpus.bin")));
                foreach (var pair in corpus) { file.Write(pair.First); file.Write(pair.Second); }
            }
            var report = new { label, unity = Application.unityVersion, warmup = 300, measured = 600,
                scope = "Paired-world loopback Tick, replay enabled, no rendering or full Lab UI; corpus reads excluded from Tick timer",
                realPairs = captured.Count, rounds };
            File.WriteAllText(Path.Combine(EvidenceDirectory, "session-" + label + ".json"), JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }

        internal static CharacterTestNetworkSessionRunner PrepareAuthority(CharacterTestNetworkLoopbackRunner runner)
        {
            var inner = (CharacterTestNetworkSessionRunner)typeof(CharacterTestNetworkLoopbackRunner)
                .GetField("_runner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(), CharacterTestCharacterActionConfigSource.Load().Registry);
            var created = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                "fixed-hash-benchmark", 1, Array.Empty<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(), references,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId);
            if (!created.Success) throw new InvalidOperationException("Benchmark catalog validation failed.");
            var catalog = created.Catalog;
            var unlocked = CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(Array.Empty<string>()).Collection;
            CharacterTestRuntimeAiPlannerSessionAuthorityComposition.Configure(inner,
                new CharacterTestRuntimeAiPlannerSessionAuthorityConfiguration(catalog, 1,
                    peer => new CharacterTestRuntimeAiPlannerSessionAuthorityContext(peer,
                        new[] { "fixed-hash-character" }, unlocked, catalog, 1)));
            inner.SubmitRuntimeAiPlannerSessionDeckRequest(1,
                new CharacterTestRuntimeAiPlannerSessionDeckRequest(inner.ClientPeerId, "fixed-hash-character",
                    1, catalog.CatalogId, catalog.Revision, catalog.CatalogNumericId, catalog.NumericIdentityHash,
                    catalog.CatalogHash, Array.Empty<int>(), Array.Empty<string>()));
            for (int i = 0; i < 4; i++) runner.Tick(1d / 60d);
            if (!inner.ServerSessionAuthorityParticipant.IsLocked || !inner.ClientSessionAuthorityParticipant.IsLocked)
                throw new InvalidOperationException("The real session authority handshake did not lock the empty deck.");
            return inner;
        }

        public static object Run(string label, bool verifyOldProduction = false)
        {
#if UNITY_EDITOR
            if (UnityEditorInternal.ProfilerDriver.deepProfiling)
                throw new InvalidOperationException("Disable Deep Profiling before the compiled benchmark.");
#endif
            var inputs = ReadCorpus();
            if (verifyOldProduction)
                foreach (var pair in inputs)
                    if (FixedHashCandidates.Legacy(pair.First, pair.Second)
                        != CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(pair.First, pair.Second))
                        throw new InvalidOperationException("The captured legacy reference differs from the production entry.");
            var rows = new List<object>();
            var samples = new double[5][];
            for (int candidate = 0; candidate < samples.Length; candidate++)
            {
                samples[candidate] = new double[BatchCount];
                for (int warm = 0; warm < 4; warm++) RunBatch(candidate, inputs);
            }
            for (int batch = 0; batch < BatchCount; batch++)
                for (int order = 0; order < samples.Length; order++)
                {
                    int candidate = (order + batch) % samples.Length;
                    samples[candidate][batch] = RunBatch(candidate, inputs);
                }
            var names = new[] { "legacy-entry", "fnv-direct", "xxh64", "xxh3", "production-entry" };
            for (int candidate = 0; candidate < samples.Length; candidate++)
                rows.Add(new { algorithm = names[candidate], timing = Summarize(samples[candidate]),
                    unit = "ns/op", allocationEvents = AllocationEvents(candidate, inputs),
                    allocationBytes = AllocationBytes(candidate, inputs), batches = samples[candidate] });
            var report = new { label, unity = Application.unityVersion, editor = Application.isEditor,
                architecture = IntPtr.Size * 8, backend = "Mono", inputCount = InputCount, batchCount = BatchCount,
                domainSeed = FixedHashCandidates.DomainSeed.ToString("X16"), rows, consumedChecksum = _sink.ToString("X16") };
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(Path.Combine(EvidenceDirectory, label + ".json"), JsonConvert.SerializeObject(report, Formatting.Indented));
            return report;
        }

        private static HashPair[] ReadCorpus()
        {
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(EvidenceDirectory, "corpus.bin")));
            if (reader.BaseStream.Length != InputCount * 16L) throw new InvalidDataException("Unexpected fixed hash corpus size.");
            var result = new HashPair[InputCount];
            for (int i = 0; i < result.Length; i++) result[i] = new HashPair(reader.ReadInt64(), reader.ReadInt64());
            return result;
        }

        private static double RunBatch(int candidate, HashPair[] inputs)
        {
            // Dispatch occurs outside each candidate's timing interval.
            switch (candidate)
            {
                case 0: return LegacyBatch(inputs);
                case 1: return FnvBatch(inputs);
                case 2: return Xxh64Batch(inputs);
                case 3: return Xxh3Batch(inputs);
                case 4: return ProductionBatch(inputs);
                default: throw new ArgumentOutOfRangeException(nameof(candidate));
            }
        }

        private static int AllocationEvents(int candidate, HashPair[] inputs)
        {
            bool previous = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            try
            {
                const ProfilerRecorderOptions options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 256, options))
                {
                    if (!positive.Valid) return -1;
                    GC.KeepAlive(new byte[8192]); positive.Stop();
                    if (positive.Count == 0) return -1;
                }
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, options);
                RunBatch(candidate, inputs); recorder.Stop();
                return recorder.WrappedAround ? -1 : recorder.Count;
            }
            finally { UnityEngine.Profiling.Profiler.enabled = previous; }
        }

        private static long AllocationBytes(int candidate, HashPair[] inputs)
        {
            _ = GC.GetAllocatedBytesForCurrentThread();
            long positiveStart = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[8192]);
            if (GC.GetAllocatedBytesForCurrentThread() - positiveStart < 8192)
                return -1; // Unsupported counter on some Unity Mono builds; never report a false zero.
            long start = GC.GetAllocatedBytesForCurrentThread();
            RunBatch(candidate, inputs);
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }

        private static object Summarize(double[] values)
        {
            double sum = 0;
            foreach (double value in values) sum += value;
            var sorted = (double[])values.Clone(); Array.Sort(sorted);
            return new { mean = sum / sorted.Length, median = sorted[sorted.Length / 2],
                p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1], max = sorted[sorted.Length - 1] };
        }

        private static ulong Next(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        private readonly struct HashPair
        {
            internal HashPair(long first, long second) { First = first; Second = second; }
            internal readonly long First, Second;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double LegacyBatch(HashPair[] inputs)
        {
            ulong sum = 0;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < inputs.Length; i++)
            {
                HashPair pair = inputs[i];
                ulong value = unchecked((ulong)FixedHashCandidates.Legacy(pair.First, pair.Second));
                sum = unchecked(((sum << 1) | (sum >> 63)) + value);
            }
            long end = Stopwatch.GetTimestamp();
            _sink = sum;
            return (end - start) * 1_000_000_000d / Stopwatch.Frequency / inputs.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double FnvBatch(HashPair[] inputs)
        {
            ulong sum = 0;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < inputs.Length; i++)
            {
                HashPair pair = inputs[i];
                ulong value = unchecked((ulong)FixedHashCandidates.Fnv(pair.First, pair.Second, FixedHashCandidates.DomainSeed));
                sum = unchecked(((sum << 1) | (sum >> 63)) + value);
            }
            long end = Stopwatch.GetTimestamp();
            _sink = sum;
            return (end - start) * 1_000_000_000d / Stopwatch.Frequency / inputs.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double Xxh64Batch(HashPair[] inputs)
        {
            ulong sum = 0;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < inputs.Length; i++)
            {
                HashPair pair = inputs[i];
                ulong value = unchecked((ulong)FixedHashCandidates.Xxh64(pair.First, pair.Second, FixedHashCandidates.DomainSeed));
                sum = unchecked(((sum << 1) | (sum >> 63)) + value);
            }
            long end = Stopwatch.GetTimestamp();
            _sink = sum;
            return (end - start) * 1_000_000_000d / Stopwatch.Frequency / inputs.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double Xxh3Batch(HashPair[] inputs)
        {
            ulong sum = 0;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < inputs.Length; i++)
            {
                HashPair pair = inputs[i];
                ulong value = unchecked((ulong)FixedHashCandidates.Xxh3(pair.First, pair.Second, FixedHashCandidates.DomainSeed));
                sum = unchecked(((sum << 1) | (sum >> 63)) + value);
            }
            long end = Stopwatch.GetTimestamp();
            _sink = sum;
            return (end - start) * 1_000_000_000d / Stopwatch.Frequency / inputs.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double ProductionBatch(HashPair[] inputs)
        {
            ulong sum = 0;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < inputs.Length; i++)
            {
                HashPair pair = inputs[i];
                ulong value = unchecked((ulong)CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(pair.First, pair.Second));
                sum = unchecked(((sum << 1) | (sum >> 63)) + value);
            }
            long end = Stopwatch.GetTimestamp();
            _sink = sum;
            return (end - start) * 1_000_000_000d / Stopwatch.Frequency / inputs.Length;
        }

#if CHARACTER_TEST_HASH_BENCHMARK && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunPlayer()
        {
            try
            {
                string label = "player";
                foreach (string argument in Environment.GetCommandLineArgs())
                    if (argument.StartsWith("--fixed-hash-label=", StringComparison.Ordinal))
                        label = argument.Substring("--fixed-hash-label=".Length);
                Run(label);
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(EvidenceDirectory);
                File.WriteAllText(Path.Combine(EvidenceDirectory, "player-error.txt"), exception.ToString());
                Application.Quit(2);
            }
        }
#endif
    }
}
#endif
