using MxFramework.DebugUI;
using MxFramework.DebugUI.Input;
using MxFramework.Input;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiInputAdapterTests
    {
        [Test]
        public void SetEnabled_PushesDebugContextAsOverlay()
        {
            var input = new FakeInputProvider();
            input.SetContext(InputContext.Gameplay);
            var adapter = new DebugUiInputAdapter(input);

            adapter.SetEnabled(true);

            Assert.AreEqual(InputContext.Debug, input.CurrentContext);
            adapter.Dispose();
            Assert.AreEqual(InputContext.Gameplay, input.CurrentContext);
        }

        [Test]
        public void ProcessFrame_MapsDebugIntentsToVisibility()
        {
            var input = new FakeInputProvider();
            var target = new TestTarget { VisibilityValue = DebugUiVisibility.Hidden };
            var adapter = new DebugUiInputAdapter(input);
            adapter.SetEnabled(true);
            input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
            input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleConsole));
            input.Enqueue(new InputCommand(0, 1, InputIntent.DebugStep));

            DebugUiInputAdapterResult result = adapter.ProcessFrame(0, target);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.HandledCommandCount);
            Assert.AreEqual(DebugUiVisibility.Expanded, target.VisibilityValue);
            Assert.AreEqual(1, target.StepCount);
            Assert.AreEqual(1, target.RefreshCount);
        }

        private sealed class TestTarget : IDebugUiInputTarget
        {
            public DebugUiVisibility VisibilityValue;
            public int RefreshCount;
            public int StepCount;

            public DebugUiVisibility Visibility => VisibilityValue;

            public void SetVisibility(DebugUiVisibility visibility)
            {
                VisibilityValue = visibility;
            }

            public void RefreshNow()
            {
                RefreshCount++;
            }

            public void RequestDebugStep()
            {
                StepCount++;
            }
        }
    }
}
