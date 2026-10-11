using System;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeNumericCustomStateTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void DefinitionTypeMustBeAPredefinedPositiveInteger(int typeId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeCustomState(typeId, "label", 1, "{}"));
        }

        [Test]
        public void PreparationIsExplicitAndCopiesRetainIt()
        {
            var source = new RuntimeCustomState(TestCustomStateTypeIds.Opaque, "label", 1, "{\"x\":1}");
            Assert.Throws<InvalidOperationException>(() => { _ = source.PreparedPayloadDigest; });
            RuntimeCustomState prepared = source.PreparePayloadDigest();
            Assert.IsFalse(source.HasPreparedPayloadDigest);
            Assert.AreSame(prepared, prepared.PreparePayloadDigest());
            RuntimeCustomState copy = prepared.Copy();
            Assert.AreEqual(prepared.NumericTypeId, copy.NumericTypeId);
            Assert.AreEqual(prepared.PayloadJson, copy.PayloadJson);
            Assert.AreEqual(prepared.PreparedPayloadDigest, copy.PreparedPayloadDigest);
        }

        [Test]
        public void NullAndEmptyPayloadsKeepDistinctContent()
        {
            var absent = new RuntimeCustomState(TestCustomStateTypeIds.Opaque, "label", 1, null, true);
            var empty = new RuntimeCustomState(TestCustomStateTypeIds.Opaque, "label", 1, "", true);
            Assert.IsNull(absent.PayloadJson);
            Assert.AreEqual("", empty.PayloadJson);
            Assert.AreNotEqual(absent.PreparedPayloadDigest, empty.PreparedPayloadDigest);
        }

        [Test]
        public void JsonCarriesNumericIdentityAndDoesNotInferItFromTheLabel()
        {
            var source = new RuntimeCustomState(TestCustomStateTypeIds.Opaque, "arbitrary display label", 1, "{}");
            RuntimeCustomState restored = JsonConvert.DeserializeObject<RuntimeCustomState>(JsonConvert.SerializeObject(source));
            Assert.AreEqual(source.NumericTypeId, restored.NumericTypeId);
            Assert.AreEqual(source.TypeId, restored.TypeId);
            Assert.AreEqual(source.PayloadJson, restored.PayloadJson);
            Assert.Catch(() => JsonConvert.DeserializeObject<RuntimeCustomState>(
                "{\"typeId\":\"arbitrary display label\",\"schemaVersion\":1,\"payloadJson\":\"{}\"}"));
        }
    }
}
