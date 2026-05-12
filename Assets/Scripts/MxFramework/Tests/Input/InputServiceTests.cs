using MxFramework.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Input
{
    public sealed class InputServiceTests : InputTestFixture
    {
        [Test]
        public void DefaultInputService_SnapshotReadsWasdWithoutWaitingForServiceUpdate()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var gameObject = new GameObject("InputServiceTest");

            try
            {
                var service = gameObject.AddComponent<DefaultInputService>();

                Press(keyboard.wKey);

                Assert.Greater(service.Snapshot.Move.y, 0.5f);
                Assert.AreEqual(0f, service.Snapshot.Move.x);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
