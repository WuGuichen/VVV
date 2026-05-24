using MxFramework.Resources;
using MxFramework.Story.ResourcesBridge;
using NUnit.Framework;

namespace MxFramework.Tests.StoryResourcesBridge
{
    public sealed class StoryResourcesBridgeTests
    {
        [Test]
        public void BuildsPreloadPlanFromExplicitKeys()
        {
            var metadata = new StoryResourcePreloadMetadata(
                "story.cutscene.1001",
                explicitKeys: new[]
                {
                    new StoryResourceKeyMetadata("story.ui.panel", ResourceTypeIds.VisualTreeAsset),
                    new StoryResourceKeyMetadata("story.audio.line", ResourceTypeIds.AudioClip, "jp"),
                    new StoryResourceKeyMetadata("story.audio.line", ResourceTypeIds.AudioClip, "jp")
                },
                failFast: true,
                maxConcurrentLoads: 4);

            StoryResourcePreloadPlanResult result = StoryResourcePreloadPlanBuilder.Build(metadata);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("story.cutscene.1001", result.Plan.GroupId);
            Assert.IsTrue(result.Plan.FailFast);
            Assert.AreEqual(4, result.Plan.MaxConcurrentLoads);
            Assert.AreEqual(2, result.Plan.ExplicitKeys.Count);
            Assert.AreEqual(new ResourceKey("story.audio.line", ResourceTypeIds.AudioClip, "jp"), result.Plan.ExplicitKeys[0]);
            Assert.AreEqual(new ResourceKey("story.ui.panel", ResourceTypeIds.VisualTreeAsset), result.Plan.ExplicitKeys[1]);
        }

        [Test]
        public void BuildsPreloadPlanFromLabelsDeterministically()
        {
            var metadata = new StoryResourcePreloadMetadata(
                "story.labels",
                labels: new[] { "story.voice", "story.cutscene", "story.voice" });

            StoryResourcePreloadPlanResult result = StoryResourcePreloadPlanBuilder.Build(metadata);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Plan.ExplicitKeys.Count);
            Assert.AreEqual(2, result.Plan.Labels.Count);
            Assert.AreEqual("story.cutscene", result.Plan.Labels[0]);
            Assert.AreEqual("story.voice", result.Plan.Labels[1]);
        }

        [Test]
        public void InvalidMetadataReportsDiagnostics()
        {
            var metadata = new StoryResourcePreloadMetadata(
                "story.invalid",
                explicitKeys: new[]
                {
                    new StoryResourceKeyMetadata("Story.Bad.Id", ResourceTypeIds.TextAsset),
                    new StoryResourceKeyMetadata("story.missing.type", "")
                },
                labels: new[] { "story.ok", "" });

            StoryResourcePreloadPlanResult result = StoryResourcePreloadPlanBuilder.Build(metadata);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Plan);
            Assert.AreEqual(3, result.Diagnostics.Count);
            Assert.AreEqual(StoryResourcesBridgeDiagnosticCode.InvalidResourceKey, result.Diagnostics[0].Code);
            Assert.AreEqual(StoryResourcesBridgeDiagnosticCode.InvalidResourceKey, result.Diagnostics[1].Code);
            Assert.AreEqual(StoryResourcesBridgeDiagnosticCode.InvalidLabel, result.Diagnostics[2].Code);
        }
    }
}
