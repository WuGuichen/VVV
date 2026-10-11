using MxFramework.UI.FairyGui;
using NUnit.Framework;

namespace MxFramework.PackageConsumerSmoke
{
    public sealed class FairyGuiConsumerSmokeTests
    {
        [Test]
        public void FairyGuiAdapter_ResolvesStageFallbackWithoutLiveStage()
        {
            Assert.That(MxFairyGuiStageLayout.DefaultDesignWidth, Is.EqualTo(1920));
            Assert.That(MxFairyGuiStageLayout.DefaultDesignHeight, Is.EqualTo(1080));
            Assert.That(MxFairyGuiStageLayout.ResolveStageWidth(null, 960f), Is.EqualTo(960f));
            Assert.That(MxFairyGuiStageLayout.ResolveStageHeight(null, 540f), Is.EqualTo(540f));
        }
    }
}
