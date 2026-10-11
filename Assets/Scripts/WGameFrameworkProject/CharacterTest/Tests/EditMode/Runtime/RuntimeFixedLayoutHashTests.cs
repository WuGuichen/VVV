using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Diagnostics;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeFixedLayoutHashTests
    {
        private static long _consumed;

        [Test]
        public void ProductionMatchesAllPinnedReferenceInputs()
        {
            var vectors = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,
                "Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/FixedHashReferenceVectors.json")))["vectors"];
            foreach (JArray row in vectors)
            {
                ulong first = Parse(row[0]), second = Parse(row[1]), seed = Parse(row[2]);
                Assert.AreEqual(Parse(row[3]), unchecked((ulong)RuntimeFixedLayoutHash64.Hash16((long)first, (long)second, seed)));
                if (seed == CharacterTestSessionHashContract.DomainSeed)
                    Assert.AreEqual(Parse(row[3]), unchecked((ulong)CharacterTestRuntimeAiPlannerSessionAuthorityHash
                        .CombineGameplayStateHash((long)first, (long)second)));
            }
        }

        [TestCase(0L, 0L, 0xF4125BAB659A1748UL)]
        [TestCase(-1L, -1L, 0xC95F98035D272D06UL)]
        [TestCase(long.MinValue, long.MaxValue, 0x2E49DCFEE81E86DCUL)]
        [TestCase(0L, -1L, 0xEE453464142266E4UL)]
        [TestCase(-1L, 0L, 0x6381A85501D21711UL)]
        [TestCase(1L, 1L, 0xA039CD2482425CD4UL)]
        [TestCase(1L, 2L, 0xCE85C2A036FB5B29UL)]
        [TestCase(2L, 1L, 0xE002B7FB21B75BECUL)]
        public void SessionLayoutGolden(long first, long second, ulong expected)
        {
            Assert.AreEqual(2, RuntimeFixedLayoutHash64.AlgorithmId);
            Assert.AreEqual(1001, CharacterTestSessionHashContract.SchemaId);
            Assert.AreEqual(1, CharacterTestSessionHashContract.LayoutVersion);
            Assert.AreEqual(expected, unchecked((ulong)CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(first, second)));
        }

        [Test]
        public void ConcurrentAndRepeatedCallsAreIndependent()
        {
            var values = new long[4096];
            Parallel.For(0, values.Length, i =>
            {
                long value = 0;
                for (int repeat = 0; repeat < 20; repeat++)
                    value = CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(i, ~i);
                values[i] = value;
            });
            for (int i = 0; i < values.Length; i++)
                Assert.AreEqual(FixedHashCandidates.Xxh64(i, ~i, CharacterTestSessionHashContract.DomainSeed), values[i]);
        }

        [Test]
        public void WarmProductionCallsDoNotAllocate()
        {
            for (int i = 0; i < 1000; i++) _consumed ^= CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(i, ~i);
            const ProfilerRecorderOptions options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options))
            {
                GC.KeepAlive(new byte[8192]); positive.Stop();
                Assert.IsTrue(positive.Valid);
                Assert.Greater(positive.Count, 0, "A zero allocation result is valid only with a working positive control.");
            }
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options);
            for (int i = 0; i < 65536; i++) _consumed ^= CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(i, ~i);
            recorder.Stop();
            Assert.AreEqual(0, recorder.Count);
        }

        private static ulong Parse(JToken value) => ulong.Parse((string)value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
