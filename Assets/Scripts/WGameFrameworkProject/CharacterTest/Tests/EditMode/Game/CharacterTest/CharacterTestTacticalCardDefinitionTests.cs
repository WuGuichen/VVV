using System;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardDefinitionTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void RoundTripPreservesExplicitChoices(int approachValue)
        {
            var approach = (CharacterTestTacticalCardApproach)approachValue;
            var source = new CharacterTestTacticalCardDefinition("名称 \"首卡\"\n测试", approach);
            Assert.IsTrue(CharacterTestTacticalCardDefinition.TryRead(source.Write(), out var loaded, out string issue), issue);
            Assert.AreEqual(source.Name, loaded.Name);
            Assert.AreEqual(approach, loaded.Approach);
            Assert.AreEqual(source.SemanticFingerprint, loaded.SemanticFingerprint);
            Assert.AreEqual(source.Write(), loaded.Write());
        }

        [Test]
        public void SemanticIdentityIgnoresNameButIncludesApproach()
        {
            var first = new CharacterTestTacticalCardDefinition("第一名称", CharacterTestTacticalCardApproach.OptimalRange);
            var renamed = new CharacterTestTacticalCardDefinition("第二名称", CharacterTestTacticalCardApproach.OptimalRange);
            var stationary = new CharacterTestTacticalCardDefinition("第一名称", CharacterTestTacticalCardApproach.None);
            Assert.AreEqual(first.SemanticFingerprint, renamed.SemanticFingerprint);
            Assert.AreNotEqual(first.SemanticFingerprint, stationary.SemanticFingerprint);
        }

        [Test]
        public void EveryFieldIsRequiredAndStringChoicesHaveNoDefaults()
        {
            JObject original = JObject.Parse(Create().Write());
            foreach (JProperty property in original.Properties())
            {
                var missing = (JObject)original.DeepClone();
                missing.Remove(property.Name);
                AssertRejected(missing.ToString(), property.Name);
                var invalidType = (JObject)original.DeepClone();
                invalidType[property.Name] = JValue.CreateNull();
                AssertRejected(invalidType.ToString(), property.Name);
                if (property.Name == "name" || property.Name == "version")
                    continue;
                var unknownChoice = (JObject)original.DeepClone();
                unknownChoice[property.Name] = "unconfirmed-choice";
                AssertRejected(unknownChoice.ToString(), property.Name);
            }
        }

        [Test]
        public void RejectsDuplicateUnknownAndFutureInputs()
        {
            string valid = Create().Write();
            AssertRejected(valid.Replace("\"version\":1", "\"version\":1,\"version\":1"), "version");
            AssertRejected(valid.Replace("\"version\":1", "\"unexpected\":true,\"version\":1"), "unexpected");
            AssertRejected(valid.Replace("\"version\":1", "\"version\":2"), "version");
            AssertRejected(valid.Replace("\"version\":1", "\"version\":1.0"), "version");
            AssertRejected(valid.Replace("\"version\":1", "\"version\":\"1\""), "version");
            AssertRejected(valid + " {}", "$");
            AssertRejected(valid.Substring(0, valid.Length - 1), "$");
            AssertRejected("[]", "$");
            AssertRejected(null, "$");
            AssertRejected("/* comment */" + valid, "$");
        }

        [Test]
        public void RejectsBlankNamesAndInvalidConstructorChoices()
        {
            var source = JObject.Parse(Create().Write());
            source["name"] = " \t";
            AssertRejected(source.ToString(), "name");
            Assert.Throws<ArgumentException>(() => new CharacterTestTacticalCardDefinition("", CharacterTestTacticalCardApproach.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterTestTacticalCardDefinition("card", (CharacterTestTacticalCardApproach)99));
        }

        [TestCase("'version':1")]
        [TestCase("version:1")]
        [TestCase("\"version\":0x1")]
        [TestCase("\"version\":01")]
        [TestCase("\"version\":+1")]
        [TestCase("\"version\":NaN")]
        [TestCase("\"version\":1.")]
        public void RejectsNonJsonPropertyAndNumberSyntax(string invalidMember)
        {
            AssertRejected(Create().Write().Replace("\"version\":1", invalidMember), "$");
        }

        [Test]
        public void RejectsTrailingCommasSingleQuotedValuesAndInvalidEscapes()
        {
            string valid = Create().Write();
            AssertRejected(valid.Substring(0, valid.Length - 1) + ",}", "$");
            AssertRejected(valid.Replace("\"optimalRange\"", "'optimalRange'"), "$");
            AssertRejected(valid.Replace("接近并普攻一次", "invalid\\x41"), "$");
            AssertRejected(valid.Replace("接近并普攻一次", "invalid\\u123"), "$");
            AssertRejected(valid.Replace("接近并普攻一次", "literal\nnewline"), "$");
            AssertRejected(valid.Replace("\"version\":1", "\"version\":1/*comment*/"), "$");
        }

        [Test]
        public void StrictSyntaxPreservesEscapedPunctuationUnicodeAndWhitespace()
        {
            const string name = "逗号, 冒号: 引号\" 反斜杠\\ 单引号' 大括号{} 方括号[] /\b\f\n\r\t";
            var source = new CharacterTestTacticalCardDefinition(name, CharacterTestTacticalCardApproach.OptimalRange);
            string encoded = source.Write();
            Assert.IsTrue(CharacterTestTacticalCardDefinition.TryRead(" \t\r\n" + encoded + "\r\n",
                out var loaded, out string issue), issue);
            Assert.AreEqual(name, loaded.Name);
            Assert.AreEqual(source.SemanticFingerprint, loaded.SemanticFingerprint);
            string unicode = encoded.Replace("\"version\"", "\"vers\\u0069on\"")
                .Replace("逗", "\\u9017").Replace("/", "\\/");
            Assert.IsTrue(CharacterTestTacticalCardDefinition.TryRead(unicode, out loaded, out issue), issue);
            Assert.AreEqual(name, loaded.Name);
        }

        private static CharacterTestTacticalCardDefinition Create() =>
            new CharacterTestTacticalCardDefinition("接近并普攻一次", CharacterTestTacticalCardApproach.OptimalRange);

        private static void AssertRejected(string json, string field)
        {
            Assert.IsFalse(CharacterTestTacticalCardDefinition.TryRead(json, out var loaded, out string issue), json);
            Assert.IsNull(loaded);
            StringAssert.Contains("TacticalCard.Source." + field, issue);
        }
    }
}
