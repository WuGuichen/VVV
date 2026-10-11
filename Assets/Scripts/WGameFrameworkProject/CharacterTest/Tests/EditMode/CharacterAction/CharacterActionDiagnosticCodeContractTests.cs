using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionDiagnosticCodeContractTests
    {
        [Test]
        public void BuiltInNumericDiagnosticCarriesCodeAndDiagnosticText()
        {
            CharacterActionDiagnostic diagnostic = CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.MissingActionBinding,
                "Binding missing.");

            Assert.AreEqual(CharacterActionDiagnosticCode.MissingActionBinding, diagnostic.CodeId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, diagnostic.Code);
        }

        [Test]
        public void RuntimeDiagnosticCreationDoesNotParseStringIdentities()
        {
            foreach (var constructor in typeof(CharacterActionDiagnostic).GetConstructors())
                Assert.AreEqual(typeof(CharacterActionDiagnosticCode), constructor.GetParameters()[0].ParameterType);
            foreach (var method in typeof(CharacterActionDiagnostic).GetMethods(
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                if (method.Name == "Error" || method.Name == "Warning" || method.Name == "Info")
                    Assert.AreEqual(typeof(CharacterActionDiagnosticCode), method.GetParameters()[0].ParameterType);
        }

        [Test]
        public void ValidationDiagnosticCarriesKnownCodeAndKeepsUnknownAsAuthoringOnly()
        {
            var known = new CharacterActionValidationDiagnostic(
                "actions.json",
                "actions[0]",
                CharacterActionDiagnosticCodes.ResourceMissing,
                CharacterActionDiagnosticSeverity.Error,
                "Resource missing.").ToDiagnostic();
            var authoring = new CharacterActionValidationDiagnostic(
                "import.json",
                "$.metadata",
                "AUTHORING_CUSTOM_CODE",
                CharacterActionDiagnosticSeverity.Warning,
                "Importer supplied diagnostic.");
            CharacterActionDiagnostic authoringDiagnostic;

            Assert.AreEqual(CharacterActionDiagnosticCode.ResourceMissing, known.CodeId);
            Assert.IsFalse(authoring.TryToDiagnostic(out authoringDiagnostic));
            Assert.Throws<System.InvalidOperationException>(() => authoring.ToDiagnostic());
            Assert.AreEqual("AUTHORING_CUSTOM_CODE", authoring.Code);
        }
    }
}
