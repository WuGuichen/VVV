using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentDiagnosticWriter
    {
        private readonly List<GameplayComponentDiagnosticField> _fields =
            new List<GameplayComponentDiagnosticField>();

        public IReadOnlyList<GameplayComponentDiagnosticField> Fields => _fields;
        public int Count => _fields.Count;

        public void AddInt(string key, int value)
        {
            AddString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddLong(string key, long value)
        {
            AddString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void AddBool(string key, bool value)
        {
            AddString(key, value ? "1" : "0");
        }

        public void AddString(string key, string value)
        {
            _fields.Add(new GameplayComponentDiagnosticField(key, value));
        }

        public GameplayComponentDiagnosticField[] CreateSnapshot()
        {
            if (_fields.Count == 0)
                return Array.Empty<GameplayComponentDiagnosticField>();

            return _fields.ToArray();
        }

        public void Clear()
        {
            _fields.Clear();
        }
    }
}
