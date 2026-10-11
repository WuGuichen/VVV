using System;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionIdTests
    {
        private sealed class Payload
        {
            public CharacterActionId ActionId { get; set; }
        }

        [Test]
        public void NoneAndPositiveValues_HaveStableValueSemantics()
        {
            Assert.IsFalse(CharacterActionId.None.IsValid);
            Assert.AreEqual(0, default(CharacterActionId).Value);

            var first = new CharacterActionId(301);
            var same = new CharacterActionId(301);
            var later = new CharacterActionId(302);
            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(first, same);
            Assert.AreEqual(301, first.GetHashCode());
            Assert.That(first.CompareTo(later), Is.LessThan(0));
            Assert.AreEqual("#301", first.ToString());
        }

        [Test]
        public void NegativeValue_IsRejected_AndTryCreateRejectsZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterActionId(-1));
            Assert.IsFalse(CharacterActionId.TryCreate(0, out CharacterActionId none));
            Assert.AreEqual(CharacterActionId.None, none);
            Assert.IsTrue(CharacterActionId.TryCreate(1, out CharacterActionId valid));
            Assert.AreEqual(new CharacterActionId(1), valid);
        }

        [Test]
        public void Json_RoundTripsAsInteger_AndRejectsLegacyString()
        {
            string json = JsonConvert.SerializeObject(new Payload
            {
                ActionId = new CharacterActionId(301)
            });
            StringAssert.Contains("\"ActionId\":301", json);
            Payload restored = JsonConvert.DeserializeObject<Payload>(json);
            Assert.AreEqual(new CharacterActionId(301), restored.ActionId);
            Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<Payload>("{\"ActionId\":\"light_attack\"}"));
        }
    }
}
