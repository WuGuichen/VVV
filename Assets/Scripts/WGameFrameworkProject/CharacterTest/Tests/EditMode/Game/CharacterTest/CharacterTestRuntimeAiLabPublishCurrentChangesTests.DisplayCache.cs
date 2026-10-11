using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabPublishCurrentChangesTests
    {
        [Test]
        public void DisplayComparisonCacheRefreshesAfterDraftAndCatalogChangesWithoutPublishing()
        {
            var fixture = CreateFixture();
            var go = new GameObject("Display comparison cache test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                void Set(string name, object value) => typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
                Set("_draftWorkspace", fixture.Workspace); Set("_projectCatalogAuthoring", fixture.Authoring);
                Assert.IsFalse(controller.HasUnpublishedBehaviorGroupContentForDisplay(fixture.Group, out string diagnostic), diagnostic);
                Assert.IsFalse(controller.HasUnpublishedBehaviorGroupContentForDisplay(fixture.Group, out diagnostic), diagnostic);
                fixture.Group.DisplayName += " changed";
                fixture.Group.CurrentRevision++; fixture.Workspace.CurrentRevision++;
                Assert.IsTrue(controller.HasUnpublishedBehaviorGroupContentForDisplay(fixture.Group, out diagnostic));
                Assert.AreEqual(controller.HasUnpublishedBehaviorGroupContent(fixture.Group, out _),
                    controller.HasUnpublishedBehaviorGroupContentForDisplay(fixture.Group, out _));
                var invalidCatalog = new CharacterTestRuntimeAiPlannerProjectCatalogState(null, null,
                    "missing-cards", "missing-groups", "", "", "missing catalog");
                Set("_projectCatalogAuthoring", new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(invalidCatalog));
                Assert.IsTrue(controller.HasUnpublishedBehaviorGroupContentForDisplay(fixture.Group, out diagnostic));
                Assert.IsNotEmpty(diagnostic, "Catalog replacement must invalidate a cached successful comparison.");
                Assert.AreEqual(0, fixture.WriteCount);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
