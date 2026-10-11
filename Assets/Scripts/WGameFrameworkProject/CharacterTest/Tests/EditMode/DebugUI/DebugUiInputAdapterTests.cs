using System;
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
            input.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));
            input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleConsole));
            input.Enqueue(new InputCommand(0, 1, InputIntent.DebugStep));

            DebugUiInputAdapterResult result = adapter.ProcessFrame(0, target);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.HandledCommandCount);
            Assert.AreEqual(DebugUiVisibility.Expanded, target.VisibilityValue);
            Assert.AreEqual(1, target.StepCount);
            Assert.AreEqual(1, target.RefreshCount);
            Assert.AreEqual(4, input.Commands.PendingCount);
            Assert.AreEqual(0, input.Commands.CurrentFrame);
        }

        [Test]
        public void ProcessFrame_DoesNotRepeatPeekedDebugCommands()
        {
            var input = new FakeInputProvider();
            var target = new TestTarget { VisibilityValue = DebugUiVisibility.Hidden };
            var adapter = new DebugUiInputAdapter(input);
            adapter.SetEnabled(true);
            input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));

            DebugUiInputAdapterResult first = adapter.ProcessFrame(0, target);
            DebugUiInputAdapterResult second = adapter.ProcessFrame(0, target);

            Assert.AreEqual(1, first.HandledCommandCount);
            Assert.AreEqual(0, second.HandledCommandCount);
            Assert.AreEqual(DebugUiVisibility.Collapsed, target.VisibilityValue);
        }

        [Test]
        public void ProcessFrame_DoesNotAllocateWhenNoDebugCommands()
        {
            var input = new FakeInputProvider();
            var target = new TestTarget { VisibilityValue = DebugUiVisibility.Hidden };
            var adapter = new DebugUiInputAdapter(input);
            adapter.SetEnabled(true);
            input.Enqueue(new InputCommand(0, 1, InputIntent.AttackPrimary));

            for (int i = 0; i < 8; i++)
                adapter.ProcessFrame(0, target);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            DebugUiInputAdapterResult result = default;
            for (int i = 0; i < 64; i++)
                result = adapter.ProcessFrame(0, target);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.HandledCommandCount);
            Assert.AreEqual(0, allocatedBytes);
        }

        [Test]
        public void ProcessFrame_DebugCycleNavigatesTabsForExpandedTabTarget()
        {
            var input = new FakeInputProvider();
            var target = new TabTarget { VisibilityValue = DebugUiVisibility.Expanded };
            var adapter = new DebugUiInputAdapter(input);
            adapter.SetEnabled(true);
            input.Enqueue(new InputCommand(0, 1, InputIntent.DebugCycle));

            DebugUiInputAdapterResult result = adapter.ProcessFrame(0, target);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.HandledCommandCount);
            Assert.AreEqual(1, target.NextTabCount);
            Assert.AreEqual(DebugUiVisibility.Expanded, target.VisibilityValue);
        }

        [Test]
        public void ProcessFrame_DebugCycleKeepsVisibilityFallbackForPlainTargets()
        {
            var input = new FakeInputProvider();
            var target = new TestTarget { VisibilityValue = DebugUiVisibility.Expanded };
            var adapter = new DebugUiInputAdapter(input);
            adapter.SetEnabled(true);
            input.Enqueue(new InputCommand(0, 1, InputIntent.DebugCycle));

            DebugUiInputAdapterResult result = adapter.ProcessFrame(0, target);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.HandledCommandCount);
            Assert.AreEqual(DebugUiVisibility.Hidden, target.VisibilityValue);
        }

        private class TestTarget : IDebugUiInputTarget
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

        private sealed class TabTarget : TestTarget, IDebugUiTabNavigationTarget
        {
            public int NextTabCount;

            public void RequestNextTab()
            {
                NextTabCount++;
            }
        }
    }
}
