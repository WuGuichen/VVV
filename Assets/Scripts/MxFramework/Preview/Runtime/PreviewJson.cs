using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Preview
{
    // Minimal JSON model + parser/writer for the preview RPC.
    // Sufficient for JSON-RPC envelope and small DTOs; not a general purpose JSON lib.
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    public sealed class JsonValue
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string String;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public static JsonValue NewNull() => new JsonValue { Kind = JsonKind.Null };
        public static JsonValue NewBool(bool b) => new JsonValue { Kind = JsonKind.Bool, Bool = b };
        public static JsonValue NewNum(double v) => new JsonValue { Kind = JsonKind.Number, Number = v };
        public static JsonValue NewStr(string s) => new JsonValue { Kind = JsonKind.String, String = s ?? string.Empty };
        public static JsonValue NewArr() => new JsonValue { Kind = JsonKind.Array, Array = new List<JsonValue>() };
        public static JsonValue NewObj() => new JsonValue { Kind = JsonKind.Object, Object = new Dictionary<string, JsonValue>() };

        public string GetString(string key, string fallback = null)
        {
            if (Kind != JsonKind.Object || !Object.TryGetValue(key, out JsonValue v) || v == null) return fallback;
            return v.Kind == JsonKind.String ? v.String : fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            if (Kind != JsonKind.Object || !Object.TryGetValue(key, out JsonValue v) || v == null) return fallback;
            return v.Kind == JsonKind.Bool ? v.Bool : fallback;
        }

        public long GetLong(string key, long fallback = 0)
        {
            if (Kind != JsonKind.Object || !Object.TryGetValue(key, out JsonValue v) || v == null) return fallback;
            return v.Kind == JsonKind.Number ? (long)v.Number : fallback;
        }

        public bool TryGetLong(string key, out long value)
        {
            value = 0;
            if (Kind != JsonKind.Object || !Object.TryGetValue(key, out JsonValue v) || v == null || v.Kind != JsonKind.Number) return false;
            value = (long)v.Number;
            return true;
        }

        public JsonValue GetField(string key)
        {
            if (Kind != JsonKind.Object || !Object.TryGetValue(key, out JsonValue v)) return null;
            return v;
        }
    }

    public static class PreviewJson
    {
        // ---------- Parser ----------
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int i = 0;
            SkipWs(text, ref i);
            JsonValue v = ParseValue(text, ref i);
            SkipWs(text, ref i);
            return v;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return JsonValue.NewStr(ParseString(s, ref i));
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') { Expect(s, ref i, "null"); return JsonValue.NewNull(); }
            return ParseNumber(s, ref i);
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            JsonValue obj = JsonValue.NewObj();
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':'");
                i++; SkipWs(s, ref i);
                obj.Object[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return obj; }
                throw new FormatException("Expected ',' or '}'");
            }
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            JsonValue arr = JsonValue.NewArr();
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return arr; }
            while (true)
            {
                SkipWs(s, ref i);
                arr.Array.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return arr; }
                throw new FormatException("Expected ',' or ']'");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("Expected '\"'");
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) throw new FormatException("Bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Bad \\u escape");
                            string hex = s.Substring(i, 4); i += 4;
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default: throw new FormatException("Bad escape");
                    }
                    continue;
                }
                sb.Append(c);
            }
            throw new FormatException("Unterminated string");
        }

        private static JsonValue ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { Expect(s, ref i, "true"); return JsonValue.NewBool(true); }
            Expect(s, ref i, "false"); return JsonValue.NewBool(false);
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-' || s[i] == '+') i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            string token = s.Substring(start, i - start);
            return JsonValue.NewNum(double.Parse(token, CultureInfo.InvariantCulture));
        }

        private static void Expect(string s, ref int i, string lit)
        {
            if (i + lit.Length > s.Length || s.Substring(i, lit.Length) != lit)
                throw new FormatException("Expected " + lit);
            i += lit.Length;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') i++;
                else break;
            }
        }

        // ---------- Writer ----------
        // Container-aware writer: tracks whether the next token in the current container
        // needs a leading comma. Keys always start a new pair (resets first-flag inside obj).
        public sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder(256);
            // each frame: true = next value should NOT have a leading comma
            private readonly Stack<bool> _firstFrame = new Stack<bool>();
            // kind: true = object (expect Key before value), false = array
            private readonly Stack<bool> _isObject = new Stack<bool>();
            // inside object, are we right after Key()? (then suppress comma + pretend "first")
            private bool _afterKey;

            public Writer Begin() { _sb.Clear(); _firstFrame.Clear(); _isObject.Clear(); _afterKey = false; return this; }
            public override string ToString() => _sb.ToString();

            public Writer ObjStart() { Sep(); _sb.Append('{'); _firstFrame.Push(true); _isObject.Push(true); return this; }
            public Writer ObjEnd() { _sb.Append('}'); _firstFrame.Pop(); _isObject.Pop(); Consumed(); return this; }
            public Writer ArrStart() { Sep(); _sb.Append('['); _firstFrame.Push(true); _isObject.Push(false); return this; }
            public Writer ArrEnd() { _sb.Append(']'); _firstFrame.Pop(); _isObject.Pop(); Consumed(); return this; }

            public Writer Key(string name)
            {
                Sep();
                EscapeString(name);
                _sb.Append(':');
                _afterKey = true;
                return this;
            }

            public Writer Str(string v) { Sep(); EscapeString(v ?? string.Empty); Consumed(); return this; }
            public Writer Num(long v) { Sep(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); Consumed(); return this; }
            public Writer Num(double v) { Sep(); _sb.Append(v.ToString("R", CultureInfo.InvariantCulture)); Consumed(); return this; }
            public Writer Bool(bool v) { Sep(); _sb.Append(v ? "true" : "false"); Consumed(); return this; }
            public Writer Null() { Sep(); _sb.Append("null"); Consumed(); return this; }

            public Writer KeyStr(string k, string v) => Key(k).Str(v);
            public Writer KeyNum(string k, long v) => Key(k).Num(v);
            public Writer KeyNum(string k, double v) => Key(k).Num(v);
            public Writer KeyBool(string k, bool v) => Key(k).Bool(v);
            public Writer KeyNull(string k) => Key(k).Null();

            public Writer Raw(string raw) { Sep(); _sb.Append(raw); Consumed(); return this; }

            private void Sep()
            {
                if (_afterKey) { _afterKey = false; return; }
                if (_firstFrame.Count == 0) return;
                bool first = _firstFrame.Peek();
                if (!first) _sb.Append(',');
            }

            private void Consumed()
            {
                if (_firstFrame.Count == 0) return;
                _firstFrame.Pop();
                _firstFrame.Push(false);
            }

            private void EscapeString(string s)
            {
                _sb.Append('"');
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"': _sb.Append("\\\""); break;
                        case '\\': _sb.Append("\\\\"); break;
                        case '\b': _sb.Append("\\b"); break;
                        case '\f': _sb.Append("\\f"); break;
                        case '\n': _sb.Append("\\n"); break;
                        case '\r': _sb.Append("\\r"); break;
                        case '\t': _sb.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                                _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                _sb.Append(c);
                            break;
                    }
                }
                _sb.Append('"');
            }
        }
    }
}
