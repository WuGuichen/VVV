using System;
using System.Linq;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeNumericHashTests
    {
        private const int RootSchema = 91000001;
        private const int ItemSchema = 91000002;
        private const int FrameKey = 1, ItemsKey = 2, ItemKey = 3, NumberKey = 4, DigestKey = 5;

        [Test]
        public void NumericApiHasNoTextOrBoxedEnumInputs()
        {
            Assert.IsNull(typeof(RuntimeHashAccumulator).GetConstructor(Type.EmptyTypes));
            Assert.IsNull(typeof(RuntimeHashAccumulator).Assembly.GetType("MxFramework.Runtime.RuntimeHashLegacyExtensions"));
            var forbidden = new[] { typeof(string), typeof(object), typeof(Enum), typeof(RuntimeHashKey) };
            foreach (var method in typeof(RuntimeHashAccumulator).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                foreach (var parameter in method.GetParameters())
                    Assert.IsFalse(forbidden.Contains(parameter.ParameterType), method.Name);
        }

        [Test]
        public void PublishedProtocolRegistriesDoNotReuseSchemaOrContributorIds()
        {
            var schemas = typeof(RuntimeHashSchemaIds).GetFields()
                .Where(field => field.IsLiteral && field.Name != nameof(RuntimeHashSchemaIds.Version1))
                .Select(field => (int)field.GetRawConstantValue()).ToArray();
            var contributors = typeof(RuntimeHashContributorIds).GetFields()
                .Where(field => field.IsLiteral)
                .Select(field => (int)field.GetRawConstantValue()).ToArray();
            Assert.IsTrue(schemas.All(value => value > 0));
            Assert.IsTrue(contributors.All(value => value > 0));
            Assert.AreEqual(schemas.Length, schemas.Distinct().Count());
            Assert.AreEqual(contributors.Length, contributors.Distinct().Count());
        }

        [Test]
        public void ResetRestoresSchemaSeedCountersAndOpenScopes()
        {
            var reused = New();
            for (int frame = 0; frame < 20; frame++)
            {
                reused.Reset();
                var independent = New();
                Write(reused, frame); Write(independent, frame);
                Assert.AreEqual(independent.ToHash(), reused.ToHash());
                Assert.AreEqual(independent.FieldCount, reused.FieldCount);
                Assert.AreEqual(independent.EncodedByteCount, reused.EncodedByteCount);
                reused.BeginArray(ItemsKey, 1);
            }
        }

        [Test]
        public void FieldTypeSchemaAndOrderArePartOfTheProtocol()
        {
            var a = New(); var b = New();
            a.AddInt(NumberKey, 1); b.AddLong(NumberKey, 1);
            Assert.AreNotEqual(a.ToHash(), b.ToHash());
            a.Reset(); b.Reset();
            a.AddInt(FrameKey, 1); a.AddInt(NumberKey, 2);
            b.AddInt(NumberKey, 2); b.AddInt(FrameKey, 1);
            Assert.AreNotEqual(a.ToHash(), b.ToHash());
            Assert.AreNotEqual(New().ToHash(), new RuntimeHashAccumulator(RootSchema + 1, 1).ToHash());
            Assert.AreNotEqual(New().ToHash(), new RuntimeHashAccumulator(RootSchema, 2).ToHash());
        }

        [Test]
        public void NullEmptyAndNestedBoundariesAreDistinct()
        {
            var a = New(); var b = New();
            a.BeginObject(ItemKey, ItemSchema, 1, false); a.EndObject();
            b.BeginObject(ItemKey, ItemSchema, 1); b.EndObject();
            Assert.AreNotEqual(a.ToHash(), b.ToHash());
            a.Reset(); b.Reset();
            a.BeginArray(ItemsKey, 0); a.EndArray();
            b.BeginArray(ItemsKey, 1); b.BeginElement(0); b.EndElement(); b.EndArray();
            Assert.AreNotEqual(a.ToHash(), b.ToHash());
        }

        [Test]
        public void InvalidStructuresFailBeforePublishingAHash()
        {
            var hash = New();
            Assert.Throws<ArgumentOutOfRangeException>(() => hash.AddInt(0, 1));
            hash.BeginArray(ItemsKey, 1);
            Assert.Throws<InvalidOperationException>(() => hash.AddInt(NumberKey, 1));
            Assert.Throws<InvalidOperationException>(() => hash.EndArray());
            Assert.Throws<InvalidOperationException>(() => hash.ToHash());
            Assert.Throws<ArgumentOutOfRangeException>(() => hash.BeginElement(1));
            hash.BeginElement(0); hash.AddInt(NumberKey, 7); hash.EndElement(); hash.EndArray();
            Assert.DoesNotThrow(() => hash.ToHash());
            hash.Reset(); hash.BeginObject(ItemKey, ItemSchema, 1, false);
            Assert.Throws<InvalidOperationException>(() => hash.AddInt(NumberKey, 1));
            hash.EndObject();
            hash.Reset();
            for (int i = 0; i < RuntimeHashAccumulator.MaximumDepth; i++) hash.BeginObject(ItemKey, ItemSchema, 1);
            Assert.Throws<InvalidOperationException>(() => hash.BeginObject(ItemKey, ItemSchema, 1));
            hash.Reset(); Assert.DoesNotThrow(() => hash.ToHash());
        }

        [Test]
        public void QuantizationRetainsHalfAwayFromZeroAndRejectsInvalidValues()
        {
            var a = New(); var b = New();
            a.AddDoubleQuantized(NumberKey, 1.001, 100); b.AddDoubleQuantized(NumberKey, 1.004, 100);
            Assert.AreEqual(a.ToHash(), b.ToHash());
            b.Reset(); b.AddDoubleQuantized(NumberKey, 1.006, 100);
            Assert.AreNotEqual(a.ToHash(), b.ToHash());
            a.Reset(); b.Reset();
            a.AddDoubleQuantized(NumberKey, -0.5, 1); b.AddDoubleQuantized(NumberKey, -1, 1);
            Assert.AreEqual(a.ToHash(), b.ToHash());
            Assert.Throws<ArgumentException>(() => a.AddDoubleQuantized(NumberKey, double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AddDoubleQuantized(NumberKey, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AddDoubleQuantized(NumberKey, double.MaxValue, 1));
        }

        [Test]
        public void PreparedNumericHashingDoesNotAllocate()
        {
            var hash = New();
            for (int i = 0; i < 20; i++) { hash.Reset(); Write(hash, i); hash.ToHash(); }
            var options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options))
            { GC.KeepAlive(new byte[8192]); positive.Stop(); Assert.Greater(positive.Count, 0); }
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options);
            for (int i = 0; i < 100; i++) { hash.Reset(); Write(hash, i); hash.ToHash(); }
            recorder.Stop(); Assert.AreEqual(0, recorder.Count);
        }

        [Test]
        public void EncodedByteCountUsesNumericFieldIdsAndFullDigest()
        {
            var hash = New();
            Assert.AreEqual(18, hash.EncodedByteCount);
            hash.AddInt(NumberKey, 1);
            Assert.AreEqual(28, hash.EncodedByteCount);
            hash.AddDigest(DigestKey, new RuntimeHashDigest(1, 2, 3, 4));
            Assert.AreEqual(66, hash.EncodedByteCount);
            Assert.AreEqual(2, hash.FieldCount);
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            Assert.AreEqual(5254586731490267045L, hash.ToHash(), "Frozen v3 XXH64 field/digest protocol.");
#else
            Assert.AreEqual(-278789458857578907L, hash.ToHash(), "Frozen v3 little-endian field/digest protocol.");
#endif
        }

        [Test]
        public void ContentHashPreservesTheFrozenVersionOneFingerprint()
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddInt("a", -3); hash.AddLong("b", long.MaxValue);
            hash.AddDoubleQuantized("p", -0.25, 100);
            hash.AddStringStable("文本😀", null); hash.AddStringStable("empty", "");
            hash.AddStringStable("arr." + int.MaxValue + ".末", "x\0y\ud800");
            Assert.AreEqual(1194455866938868474L, hash.ToHash());
        }

        private static RuntimeHashAccumulator New() => new RuntimeHashAccumulator(RootSchema, 1);
        private static void Write(RuntimeHashAccumulator hash, int frame)
        {
            hash.AddLong(FrameKey, frame);
            hash.BeginArray(ItemsKey, 2);
            for (int i = 0; i < 2; i++)
            {
                hash.BeginElement(i);
                hash.BeginObject(ItemKey, ItemSchema, 1);
                hash.AddLong(NumberKey, i == 0 ? long.MinValue : long.MaxValue);
                hash.AddDigest(DigestKey, new RuntimeHashDigest(1, 2, 3, 4));
                hash.EndObject(); hash.EndElement();
            }
            hash.EndArray();
        }
    }
}
