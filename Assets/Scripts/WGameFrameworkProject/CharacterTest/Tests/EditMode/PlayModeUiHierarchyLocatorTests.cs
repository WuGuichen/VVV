using System.Collections.Generic;
using MxFramework.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MxFramework.Tests
{
    public sealed class PlayModeUiHierarchyLocatorTests
    {
        [Test]
        public void FindFirstClickableTarget_SkipsNonClickableOverlayAndReturnsFirstHandler()
        {
            GameObject overlay = new("Overlay");
            GameObject button = new("Button", typeof(EventTrigger));
            try
            {
                var results = new List<RaycastResult>
                {
                    new() { gameObject = overlay },
                    new() { gameObject = button }
                };

                GameObject target = PlayModeUiHierarchyLocator.FindFirstClickableTarget(results);

                Assert.That(target, Is.SameAs(button));
            }
            finally
            {
                Object.DestroyImmediate(overlay);
                Object.DestroyImmediate(button);
            }
        }

        [Test]
        public void FindFirstClickableTarget_ReturnsClickableAncestorOfHitGraphic()
        {
            GameObject button = new("Button", typeof(EventTrigger));
            GameObject graphic = new("Graphic");
            graphic.transform.SetParent(button.transform, false);
            try
            {
                var results = new List<RaycastResult>
                {
                    new() { gameObject = graphic }
                };

                GameObject target = PlayModeUiHierarchyLocator.FindFirstClickableTarget(results);

                Assert.That(target, Is.SameAs(button));
            }
            finally
            {
                Object.DestroyImmediate(button);
            }
        }

        [Test]
        public void FindFirstClickableTarget_ReturnsNullWhenNoHitHandlesClicks()
        {
            GameObject overlay = new("Overlay");
            try
            {
                var results = new List<RaycastResult>
                {
                    new() { gameObject = overlay }
                };

                Assert.That(PlayModeUiHierarchyLocator.FindFirstClickableTarget(results), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(overlay);
            }
        }
    }
}
