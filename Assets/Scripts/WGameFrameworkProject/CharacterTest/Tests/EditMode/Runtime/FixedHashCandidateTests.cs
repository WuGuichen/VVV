using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using MxFramework.Game.CharacterTest.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Runtime
{
    public sealed class FixedHashCandidateTests
    {
        [Test]
        public void ShortInputCandidatesMatchPinnedUpstreamCVectors()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Runtime/FixedHashReferenceVectors.json");
            var vectors = (JArray)JObject.Parse(File.ReadAllText(path))["vectors"];
            foreach (JArray row in vectors)
            {
                ulong first = Parse(row[0]), second = Parse(row[1]), seed = Parse(row[2]);
                Assert.AreEqual(Parse(row[3]), unchecked((ulong)FixedHashCandidates.Xxh64((long)first, (long)second, seed)), row.ToString());
                Assert.AreEqual(Parse(row[4]), unchecked((ulong)FixedHashCandidates.Xxh3((long)first, (long)second, seed)), row.ToString());
                Assert.AreEqual(Parse(row[5]), unchecked((ulong)FixedHashCandidates.Fnv((long)first, (long)second, seed)), row.ToString());
            }
        }

        [Test]
        public void WideMultiplicationMatchesIndependentBigIntegerArithmetic()
        {
            ulong[] boundaries = { 0, 1, uint.MaxValue, 1UL << 32, 1UL << 63, ulong.MaxValue };
            foreach (ulong a in boundaries) foreach (ulong b in boundaries) CheckProduct(a, b);
            var random = new System.Random(1001);
            var bytes = new byte[16];
            for (int i = 0; i < 10000; i++)
            { random.NextBytes(bytes); CheckProduct(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8)); }
        }

        private static void CheckProduct(ulong first, ulong second)
        {
            BigInteger product = (BigInteger)first * second;
            ulong low = (ulong)(product & ulong.MaxValue), high = (ulong)(product >> 64);
            Assert.AreEqual(low ^ high, FixedHashCandidates.MultiplyFold(first, second));
        }

        private static ulong Parse(JToken value) => ulong.Parse((string)value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
