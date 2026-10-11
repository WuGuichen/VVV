using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeContentDigestTests
    {
        [TestCase(null, "ad95131bc0b799c0b1af477fb14fcf26a6a9f76079e48bf090acb7e8367bfd0e")]
        [TestCase("", "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119")]
        [TestCase("a", "c0ded7cc72c03e538eb658c0bed4b84dd2cb3f549dc838a6aa5809df1c4c08f9")]
        [TestCase("文本😀", "2e5faca807cc75453d634c67150c2ac884da6d14c5dcfb2f6c24fadc90af0095")]
        [TestCase("\ufffd", "a324096e8fffee0436e4f9c99f9425695556b5c8be137f47174caaa9331e1c29")]
        [TestCase("a\0b", "09374781c0da69114130a1a72de9f00549f65ad21ae87073123f3430010151aa")]
        public void Sha256MatchesIndependentUtf16CodeUnitVectors(string value, string expected)
        {
            AssertDigest(value, expected);
        }

        [Test]
        public void UnpairedSurrogateIsNotReplacedDuringPreparation()
        {
            AssertDigest(new string((char)0xd800, 1), "c3085645017ddb352b4f02ea9eed12ff40b3a084968b7f2feddda3559ca9136f");
        }

        private static void AssertDigest(string value, string expected)
        {
            RuntimeHashDigest result = RuntimeHashDigest.ComputeContentDigest(value);
            var bytes = new byte[32];
            Write(bytes, 0, result.Word0); Write(bytes, 8, result.Word1);
            Write(bytes, 16, result.Word2); Write(bytes, 24, result.Word3);
            Assert.AreEqual(expected, BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant());
        }

        private static void Write(byte[] bytes, int offset, ulong word)
        { for (int i = 0; i < 8; i++) bytes[offset + i] = (byte)(word >> (8 * i)); }
    }
}
