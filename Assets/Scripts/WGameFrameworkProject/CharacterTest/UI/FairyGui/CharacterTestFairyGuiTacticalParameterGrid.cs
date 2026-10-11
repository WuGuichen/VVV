using System;
using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Fits parameter values into the two-column grid authored in the published Composer component.</summary>
    /// <remarks>LowFreqAlloc. Layout runs only when binding a card or language, never on pointer movement.</remarks>
    internal sealed class CharacterTestFairyGuiTacticalParameterGrid
    {
        private sealed class Cell
        {
            internal Fgui.GTextField Value;
            internal Fgui.GObject Hit;
            internal Fgui.GObject[] Icons;
            internal Vector2[] IconOffsets;
            internal Vector2 ValueOffset;
            internal float TextHeight, TextBottom;
        }

        private readonly Fgui.GComponent _root;
        private readonly List<Cell> _cells = new();
        private readonly Fgui.GObject _background;
        private readonly float _left, _top, _right, _cellWidth, _rowHeight, _bottom, _width, _height;
        private readonly Vector2 _backgroundPosition, _backgroundSize;

        internal CharacterTestFairyGuiTacticalParameterGrid(Fgui.GComponent root)
        {
            _root = root;
            Add("movementMultiplier", "movementField", "footLeft", "heelLeft", "footRight", "heelRight");
            Add("rotationMultiplier", "rotationField", "rotationArc", "rotationArrow");
            Add("distance", "distanceField", "distanceLeft", "distanceRight", "distanceLine");
            Add("facing", "facingField", "facingIcon");
            Add("direction", "directionField", "directionIcon");
            Add("preparationDuration", "preparationDurationField", "preparationClockOuter", "preparationClockInner", "preparationClockMinute", "preparationClockHour");
            Add("duration", "durationField", "clockOuter", "clockInner", "clockMinute", "clockHour");
            if (_cells.Count < 2) throw new InvalidOperationException("参数窗缺少已发布的两列网格。");
            _left = _cells[0].Hit.x; _top = _cells[0].Hit.y; _right = _cells[1].Hit.x;
            _cellWidth = _cells[0].Hit.width; _rowHeight = _cells[0].Hit.height;
            _width = root.width; _height = root.height;
            float lastBottom = _top;
            foreach (var cell in _cells) lastBottom = Math.Max(lastBottom, cell.Hit.y + cell.Hit.height);
            _bottom = _height - lastBottom;
            _background = root.GetChild("background");
            _backgroundPosition = _background.xy; _backgroundSize = _background.size;
        }

        private void Add(string valueName, string hitName, params string[] iconNames)
        {
            var value = _root.GetChild(valueName)?.asTextField;
            if (value == null) return;
            var hit = _root.GetChild(hitName);
            var cell = new Cell { Value = value, Hit = hit, ValueOffset = value.xy - hit.xy,
                TextHeight = value.height, TextBottom = hit.height - (value.y - hit.y) - value.height,
                Icons = new Fgui.GObject[iconNames.Length], IconOffsets = new Vector2[iconNames.Length] };
            for (int i = 0; i < iconNames.Length; i++)
            {
                cell.Icons[i] = _root.GetChild(iconNames[i]);
                cell.IconOffsets[i] = cell.Icons[i].xy - hit.xy;
            }
            _cells.Add(cell);
        }

        internal void Layout()
        {
            int column = 0;
            float y = _top, rowHeight = _rowHeight;
            foreach (var cell in _cells)
            {
                var value = cell.Value;
                value.singleLine = true; value.autoSize = Fgui.AutoSizeType.Both;
                float measured = Mathf.Ceil(value.textWidth);
                bool span = measured > _cellWidth - cell.ValueOffset.x;
                if (span && column != 0) { y += rowHeight; column = 0; rowHeight = _rowHeight; }
                float x = column == 0 ? _left : _right;
                float width = span ? _right - _left + _cellWidth : _cellWidth;
                float textWidth = width - cell.ValueOffset.x;
                value.autoSize = Fgui.AutoSizeType.None;
                value.SetSize(textWidth, cell.TextHeight);
                if (measured > textWidth)
                {
                    value.singleLine = false; value.autoSize = Fgui.AutoSizeType.Height;
                    value.height = Math.Max(cell.TextHeight, value.textHeight);
                }
                float height = Math.Max(_rowHeight, cell.ValueOffset.y + value.height + cell.TextBottom);
                value.SetXY(x + cell.ValueOffset.x, y + cell.ValueOffset.y);
                for (int i = 0; i < cell.Icons.Length; i++)
                    cell.Icons[i].SetXY(x + cell.IconOffsets[i].x, y + cell.IconOffsets[i].y);
                cell.Hit.SetXY(x, y); cell.Hit.SetSize(width, height);
                rowHeight = Math.Max(rowHeight, height);
                if (span || column == 1) { y += rowHeight; column = 0; rowHeight = _rowHeight; }
                else column = 1;
            }
            float totalHeight = y + (column == 0 ? 0 : rowHeight) + _bottom;
            _root.SetSize(_width, totalHeight);
            float scale = totalHeight / _height;
            _background.SetXY(_backgroundPosition.x, _backgroundPosition.y * scale);
            _background.SetSize(_backgroundSize.x, _backgroundSize.y * scale);
        }
    }
}
