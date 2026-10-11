using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Projects stable behavior-card icon keys into reusable vector pictograms.</summary>
    internal static class CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter
    {
        internal const string TargetNearestIconKey =
            "ui.character-test.behavior-card.target-nearest";
        internal const string FallbackIconKey =
            "ui.character-test.behavior-card.fallback";

        private const string HostName = "behaviorCardIconVisual";
        private static readonly Vector2[] Hexagon =
        {
            new Vector2(12f, 0f), new Vector2(36f, 0f), new Vector2(48f, 12f),
            new Vector2(48f, 36f), new Vector2(36f, 48f), new Vector2(12f, 48f),
            new Vector2(0f, 36f), new Vector2(0f, 12f)
        };
        private static readonly Vector2[] InnerHexagon =
        {
            new Vector2(12f, 2f), new Vector2(34f, 2f), new Vector2(46f, 14f),
            new Vector2(46f, 34f), new Vector2(34f, 46f), new Vector2(14f, 46f),
            new Vector2(2f, 34f), new Vector2(2f, 14f)
        };

        internal static void Bind(
            Fgui.GComponent card,
            string iconKey,
            float x,
            float y,
            float size,
            Color32 accent,
            bool visible)
        {
            if (card == null)
                return;
            Fgui.GComponent host = card.GetChild(HostName)?.asCom;
            string resolvedKey = string.IsNullOrWhiteSpace(iconKey) ? FallbackIconKey : iconKey;
            string stateKey = resolvedKey + "|" + accent.r + ":" + accent.g + ":" + accent.b;
            if (host == null)
            {
                host = new Fgui.GComponent
                {
                    name = HostName,
                    touchable = false,
                    opaque = false,
                    sortingOrder = 20
                };
                card.AddChild(host);
            }
            host.visible = visible;
            host.SetXY(x, y);
            host.SetSize(size, size);
            host.scaleX = size / 48f;
            host.scaleY = size / 48f;
            if (!visible || string.Equals(host.data as string, stateKey, StringComparison.Ordinal))
                return;

            host.RemoveChildren(0, -1, true);
            host.data = stateKey;
            Color32 fill = new Color32(8, 31, 40, 245);
            AddPolygon(host, "badgeBorder", Hexagon, accent, 0);
            AddPolygon(host, "badgeFill", InnerHexagon, fill, 1);
            DrawGlyph(host, ResolveKind(resolvedKey), accent);
        }

        private static void DrawGlyph(Fgui.GComponent host, IconKind kind, Color32 color)
        {
            switch (kind)
            {
                case IconKind.Target:
                    AddRing(host, 24f, 24f, 11f, 2f, color);
                    AddRing(host, 24f, 24f, 3f, 3f, color);
                    AddLine(host, 24f, 8f, 24f, 15f, 2f, color);
                    AddLine(host, 24f, 33f, 24f, 40f, 2f, color);
                    AddLine(host, 8f, 24f, 15f, 24f, 2f, color);
                    AddLine(host, 33f, 24f, 40f, 24f, 2f, color);
                    break;
                case IconKind.Hazard:
                    AddTriangle(host, 24f, 9f, 38f, 37f, color);
                    AddLine(host, 24f, 18f, 24f, 28f, 2.5f, color);
                    AddRing(host, 24f, 33f, 1.8f, 1.8f, color);
                    break;
                case IconKind.Fireball:
                    AddRing(host, 29f, 21f, 8f, 3f, color);
                    AddLine(host, 8f, 34f, 23f, 25f, 3f, color);
                    AddLine(host, 12f, 24f, 22f, 20f, 2f, color);
                    AddLine(host, 17f, 38f, 27f, 29f, 2f, color);
                    break;
                case IconKind.Approach:
                    AddRing(host, 15f, 16f, 4f, 3f, color);
                    AddLine(host, 16f, 21f, 20f, 31f, 3f, color);
                    AddLine(host, 19f, 25f, 27f, 22f, 2.5f, color);
                    AddLine(host, 20f, 31f, 14f, 39f, 2.5f, color);
                    AddLine(host, 20f, 31f, 28f, 37f, 2.5f, color);
                    AddArrow(host, 28f, 15f, 40f, 15f, color);
                    break;
                case IconKind.DistanceBand:
                    AddLine(host, 11f, 12f, 11f, 36f, 2f, color);
                    AddLine(host, 37f, 12f, 37f, 36f, 2f, color);
                    AddArrow(host, 16f, 24f, 32f, 24f, color);
                    AddArrow(host, 32f, 30f, 16f, 30f, color);
                    break;
                case IconKind.Follow:
                    AddRing(host, 15f, 17f, 5f, 2.5f, color);
                    AddRing(host, 33f, 30f, 5f, 2.5f, color);
                    AddArrow(host, 19f, 20f, 29f, 27f, color);
                    break;
                case IconKind.Position:
                    AddRing(host, 24f, 18f, 7f, 2.5f, color);
                    AddLine(host, 19f, 24f, 24f, 37f, 2.5f, color);
                    AddLine(host, 29f, 24f, 24f, 37f, 2.5f, color);
                    AddRing(host, 24f, 18f, 2f, 2f, color);
                    break;
                case IconKind.Retreat:
                    AddRing(host, 35f, 24f, 7f, 2f, color);
                    AddArrow(host, 28f, 24f, 9f, 24f, color);
                    break;
                case IconKind.Orbit:
                    AddRing(host, 24f, 24f, 5f, 2.5f, color);
                    AddLine(host, 9f, 17f, 18f, 10f, 2f, color);
                    AddLine(host, 18f, 10f, 16f, 17f, 2f, color);
                    AddLine(host, 39f, 31f, 30f, 38f, 2f, color);
                    AddLine(host, 30f, 38f, 32f, 31f, 2f, color);
                    AddRing(host, 24f, 24f, 15f, 1.5f, new Color32(color.r, color.g, color.b, 150));
                    break;
                case IconKind.Attack:
                    AddLine(host, 13f, 36f, 34f, 15f, 4f, color);
                    AddLine(host, 11f, 31f, 18f, 38f, 3f, color);
                    AddLine(host, 30f, 12f, 37f, 12f, 2.5f, color);
                    AddLine(host, 37f, 12f, 37f, 19f, 2.5f, color);
                    break;
                case IconKind.DashStrike:
                    AddArrow(host, 8f, 19f, 28f, 19f, color);
                    AddArrow(host, 12f, 29f, 32f, 29f, color);
                    AddLine(host, 28f, 36f, 39f, 13f, 3f, color);
                    break;
                case IconKind.Dodge:
                    AddLine(host, 10f, 33f, 18f, 16f, 3f, color);
                    AddLine(host, 18f, 16f, 26f, 33f, 3f, color);
                    AddLine(host, 26f, 33f, 38f, 14f, 3f, color);
                    break;
                case IconKind.Strafe:
                    AddLine(host, 24f, 10f, 24f, 37f, 4f, color);
                    AddArrow(host, 21f, 18f, 9f, 18f, color);
                    AddArrow(host, 27f, 30f, 39f, 30f, color);
                    break;
                default:
                    AddLine(host, 24f, 10f, 38f, 24f, 3f, color);
                    AddLine(host, 38f, 24f, 24f, 38f, 3f, color);
                    AddLine(host, 24f, 38f, 10f, 24f, 3f, color);
                    AddLine(host, 10f, 24f, 24f, 10f, 3f, color);
                    break;
            }
        }

        internal static bool HasRegisteredIcon(string iconKey) => !string.IsNullOrWhiteSpace(iconKey)
            && (string.Equals(iconKey, FallbackIconKey, StringComparison.Ordinal) || ResolveKind(iconKey) != IconKind.Fallback);

        private static IconKind ResolveKind(string iconKey)
        {
            if (string.Equals(iconKey, TargetNearestIconKey, StringComparison.Ordinal)) return IconKind.Target;
            if (iconKey.EndsWith("avoid-hazard", StringComparison.Ordinal)) return IconKind.Hazard;
            if (iconKey.EndsWith("continuous-approach", StringComparison.Ordinal)) return IconKind.Approach;
            if (iconKey.EndsWith("distance-band-approach", StringComparison.Ordinal)) return IconKind.DistanceBand;
            if (iconKey.EndsWith("follow-unit", StringComparison.Ordinal)) return IconKind.Follow;
            if (iconKey.EndsWith("go-to-position", StringComparison.Ordinal)) return IconKind.Position;
            if (iconKey.EndsWith("keep-distance", StringComparison.Ordinal)) return IconKind.Retreat;
            if (iconKey.EndsWith("orbit-target", StringComparison.Ordinal)) return IconKind.Orbit;
            if (iconKey.EndsWith("primary-attack", StringComparison.Ordinal)) return IconKind.Attack;
            if (iconKey.EndsWith("reactive-dodge", StringComparison.Ordinal)) return IconKind.Dodge;
            if (iconKey.EndsWith("strafe-when-blocked", StringComparison.Ordinal)) return IconKind.Strafe;
            return IconKind.Fallback;
        }

        private static void AddPolygon(
            Fgui.GComponent host,
            string name,
            Vector2[] points,
            Color color,
            int sortingOrder)
        {
            var graph = NewGraph(name, sortingOrder);
            graph.SetSize(48f, 48f);
            graph.DrawPolygon(48f, 48f, points, color);
            host.AddChild(graph);
        }

        private static void AddRing(
            Fgui.GComponent host,
            float centerX,
            float centerY,
            float radius,
            float thickness,
            Color color)
        {
            var outer = NewGraph("glyph", 3);
            outer.SetXY(centerX - radius, centerY - radius);
            outer.SetSize(radius * 2f, radius * 2f);
            outer.DrawEllipse(radius * 2f, radius * 2f, color);
            host.AddChild(outer);
            float innerRadius = Mathf.Max(0f, radius - thickness);
            if (innerRadius <= 0f)
                return;
            var inner = NewGraph("glyphCutout", 4);
            inner.SetXY(centerX - innerRadius, centerY - innerRadius);
            inner.SetSize(innerRadius * 2f, innerRadius * 2f);
            inner.DrawEllipse(innerRadius * 2f, innerRadius * 2f, new Color32(8, 31, 40, 255));
            host.AddChild(inner);
        }

        private static void AddLine(
            Fgui.GComponent host,
            float startX,
            float startY,
            float endX,
            float endY,
            float thickness,
            Color color)
        {
            float dx = endX - startX;
            float dy = endY - startY;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            var line = NewGraph("glyph", 5);
            line.SetXY(startX, startY - thickness * 0.5f);
            line.SetSize(length, thickness);
            line.DrawRect(length, thickness, 0, Color.clear, color);
            line.rotation = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            host.AddChild(line);
        }

        private static void AddArrow(
            Fgui.GComponent host,
            float startX,
            float startY,
            float endX,
            float endY,
            Color color)
        {
            AddLine(host, startX, startY, endX, endY, 2.5f, color);
            float angle = Mathf.Atan2(endY - startY, endX - startX);
            const float headLength = 6f;
            AddLine(
                host,
                endX,
                endY,
                endX - Mathf.Cos(angle - 0.65f) * headLength,
                endY - Mathf.Sin(angle - 0.65f) * headLength,
                2.5f,
                color);
            AddLine(
                host,
                endX,
                endY,
                endX - Mathf.Cos(angle + 0.65f) * headLength,
                endY - Mathf.Sin(angle + 0.65f) * headLength,
                2.5f,
                color);
        }

        private static void AddTriangle(
            Fgui.GComponent host,
            float topX,
            float topY,
            float rightX,
            float bottomY,
            Color color)
        {
            AddLine(host, topX, topY, rightX, bottomY, 2.5f, color);
            AddLine(host, rightX, bottomY, 48f - rightX, bottomY, 2.5f, color);
            AddLine(host, 48f - rightX, bottomY, topX, topY, 2.5f, color);
        }

        private static Fgui.GGraph NewGraph(string name, int sortingOrder) => new Fgui.GGraph
        {
            name = name,
            touchable = false,
            sortingOrder = sortingOrder
        };

        private enum IconKind
        {
            Fallback,
            Target,
            Hazard,
            Fireball,
            Approach,
            DistanceBand,
            Follow,
            Position,
            Retreat,
            Orbit,
            Attack,
            DashStrike,
            Dodge,
            Strafe
        }
    }
}
