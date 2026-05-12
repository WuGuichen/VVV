using MxFramework.Editor;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class ResourceCatalogEditorValidatorTests
    {
        [SetUp]
        public void SetUp()
        {
            ResourcesProviderTestFixture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            ResourcesProviderTestFixture.Delete();
        }

        [Test]
        public void ValidateCatalog_ResourcesAddressExistsAndTypeMatches_ReturnsNoErrors()
        {
            var catalog = new ResourceCatalog(
                "editor.resources",
                "editor.package",
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.resource_demo",
                        ResourceTypeIds.TextAsset,
                        "resources",
                        ResourcesProviderTestFixture.DemoAddress)
                });

            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { "resources" });

            Assert.IsFalse(report.HasErrors, ResourceCatalogEditorValidator.CreateReportText(catalog, report));
        }

        [Test]
        public void ValidateCatalog_ResourcesAddressMissing_ReturnsAssetMissing()
        {
            var catalog = new ResourceCatalog(
                "editor.resources",
                "editor.package",
                new[]
                {
                    new ResourceCatalogEntry(
                        "demo.text.missing",
                        ResourceTypeIds.TextAsset,
                        "resources",
                        ResourcesProviderTestFixture.MissingAddress)
                });

            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(
                catalog,
                new[] { "resources" });

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "AssetMissing");
            StringAssert.Contains("AssetMissing", ResourceCatalogEditorValidator.CreateReportText(catalog, report));
        }

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected resource catalog validation issue: " + code);
        }
    }
}
