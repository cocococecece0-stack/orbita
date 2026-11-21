using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Упорядоченный JSON-объект. Порядок ключей сохраняется при записи, поэтому
    /// сериализация сохранения детерминирована. Класс входит только в ядро и не
    /// использует внешние пакеты (Newtonsoft.Json в ядре запрещён).
    /// </summary>
    internal sealed class GalaxyJsonObject
    {
        private readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>();

        public int Count { get { return _items.Count; } }

        public IList<KeyValuePair<string, object>> Items { get { return _items; } }

        public void Set(string key, object value)
        {
            if (key == null) key = string.Empty;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Key, key, StringComparison.Ordinal))
                {
                    _items[i] = new KeyValuePair<string, object>(key, value);
                    return;
                }
            }
            _items.Add(new KeyValuePair<string, object>(key, value));
        }

        public bool TryGetExact(string key, out object value)
        {
            if (key != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Key, key, StringComparison.Ordinal))
                    {
                        value = _items[i].Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }
    }

    /// <summary>Минимальный защищённый JSON-кодек ядра: объекты, массивы, строки, числа, bool, null.</summary>
    internal static class GalaxyJson
    {
        private const int MaxDepth = 64;

        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON-строка отсутствует.");
            Parser parser = new Parser(json);
            object result = parser.ParseValue(0);
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Error("Лишние данные после JSON.");
            return result;
        }

        public static string Stringify(object value)
        {
            StringBuilder builder = new StringBuilder(256);
            Write(value, builder, 0);
            return builder.ToString();
        }

        public static GalaxyJsonObject Object(object value)
        {
            return value as GalaxyJsonObject;
        }

        public static List<object> Array(object value)
        {
            return value as List<object>;
        }

        /// <summary>Первое найденное значение по списку синонимов (регистр не важен).</summary>
        public static object Get(GalaxyJsonObject obj, params string[] keys)
        {
            object value;
            if (obj == null || keys == null) return null;
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGet(obj, keys[i], out value)) return value;
            }
            return null;
        }

        public static bool TryGet(GalaxyJsonObject obj, string key, out object value)
        {
            object exact;
            if (obj != null && obj.TryGetExact(key, out exact))
            {
                value = exact;
                return true;
            }
            if (obj != null && key != null)
            {
                IList<KeyValuePair<string, object>> items = obj.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = items[i].Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        public static string Text(object value, string fallback)
        {
            if (value == null) return fallback;
            string text = value as string;
            if (text != null) return text;
            if (value is bool) return ((bool)value) ? "true" : "false";
            if (value is char) return ((char)value).ToString();
            if (value is IFormattable) return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }

        public static long Long(object value, long fallback)
        {
            if (value == null) return fallback;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is short) return (short)value;
            if (value is byte) return (byte)value;
            if (value is uint) return (uint)value;
            if (value is ulong) return (long)Math.Min((ulong)value, (ulong)long.MaxValue);
            if (value is float || value is double || value is decimal)
            {
                double number = ToDouble(value, double.NaN);
                if (double.IsNaN(number) || double.IsInfinity(number)) return fallback;
                if (number >= long.MaxValue) return long.MaxValue;
                if (number <= long.MinValue) return long.MinValue;
                return (long)Math.Round(number, MidpointRounding.AwayFromZero);
            }
            if (value is bool) return ((bool)value) ? 1L : 0L;
            string text = value as string;
            if (text != null)
            {
                long parsed;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;
                double number;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number))
                    return Long(number, fallback);
            }
            return fallback;
        }

        public static int Int(object value, int fallback)
        {
            long number = Long(value, long.MinValue);
            if (number == long.MinValue) return fallback;
            if (number > int.MaxValue) return int.MaxValue;
            if (number < int.MinValue) return int.MinValue;
            return (int)number;
        }

        public static float Float(object value, float fallback)
        {
            double number = ToDouble(value, double.NaN);
            if (double.IsNaN(number) || double.IsInfinity(number)) return fallback;
            if (number > float.MaxValue) return float.MaxValue;
            if (number < -float.MaxValue) return -float.MaxValue;
            return (float)number;
        }

        public static bool Bool(object value, bool fallback)
        {
            if (value == null) return fallback;
            if (value is bool) return (bool)value;
            if (value is string)
            {
                string text = (string)value;
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "да", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "1", StringComparison.Ordinal)) return true;
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "нет", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "0", StringComparison.Ordinal)) return false;
                return fallback;
            }
            long number = Long(value, long.MinValue);
            if (number == long.MinValue) return fallback;
            return number != 0L;
        }

        public static List<string> StringList(object value)
        {
            List<string> result = new List<string>();
            if (value == null) return result;
            string single = value as string;
            if (single != null)
            {
                if (single.Length > 0) result.Add(single);
                return result;
            }
            List<object> array = value as List<object>;
            if (array != null)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    string text = Text(array[i], null);
                    if (!string.IsNullOrEmpty(text)) result.Add(text);
                }
                return result;
            }
            GalaxyJsonObject obj = value as GalaxyJsonObject;
            if (obj != null)
            {
                IList<KeyValuePair<string, object>> items = obj.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (Bool(items[i].Value, false) && !string.IsNullOrEmpty(items[i].Key)) result.Add(items[i].Key);
                }
            }
            return result;
        }

        public static Dictionary<string, int> IntMap(object value)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (value == null) return result;
            GalaxyJsonObject obj = value as GalaxyJsonObject;
            if (obj != null)
            {
                IList<KeyValuePair<string, object>> items = obj.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!string.IsNullOrEmpty(items[i].Key)) result[items[i].Key] = Int(items[i].Value, 0);
                }
                return result;
            }
            IDictionary<string, int> typed = value as IDictionary<string, int>;
            if (typed != null)
            {
                foreach (KeyValuePair<string, int> pair in typed)
                {
                    if (!string.IsNullOrEmpty(pair.Key)) result[pair.Key] = pair.Value;
                }
                return result;
            }
            List<object> array = value as List<object>;
            if (array != null)
            {
                for (int i = 0; i < array.Count; i++) result[i.ToString(CultureInfo.InvariantCulture)] = Int(array[i], 0);
            }
            return result;
        }

        public static GalaxyJsonObject MapObject(IDictionary<string, int> map)
        {
            GalaxyJsonObject obj = new GalaxyJsonObject();
            if (map == null) return obj;
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++) obj.Set(keys[i], map[keys[i]]);
            return obj;
        }

        public static GalaxyJsonObject MapObject(IDictionary<string, string> map)
        {
            GalaxyJsonObject obj = new GalaxyJsonObject();
            if (map == null) return obj;
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++) obj.Set(keys[i], map[keys[i]]);
            return obj;
        }

        public static List<object> StringArray(IList<string> items)
        {
            List<object> result = new List<object>();
            if (items == null) return result;
            for (int i = 0; i < items.Count; i++) result.Add(items[i] == null ? string.Empty : items[i]);
            return result;
        }

        private static double ToDouble(object value, double fallback)
        {
            if (value == null) return fallback;
            if (value is double) return (double)value;
            if (value is float) return (float)value;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is short) return (short)value;
            if (value is byte) return (byte)value;
            if (value is uint) return (uint)value;
            if (value is decimal) return (double)(decimal)value;
            if (value is bool) return ((bool)value) ? 1d : 0d;
            string text = value as string;
            if (text != null)
            {
                double parsed;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
            }
            return fallback;
        }

        private static void Write(object value, StringBuilder builder, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("Слишком глубокая структура JSON.");
            if (value == null) { builder.Append("null"); return; }
            if (value is bool) { builder.Append(((bool)value) ? "true" : "false"); return; }
            if (value is string) { WriteString((string)value, builder); return; }
            if (value is char) { WriteString(((char)value).ToString(), builder); return; }

            if (value is float)
            {
                float number = (float)value;
                builder.Append(float.IsNaN(number) || float.IsInfinity(number) ? "0" : number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is double)
            {
                double number = (double)value;
                builder.Append(double.IsNaN(number) || double.IsInfinity(number) ? "0" : number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is decimal)
            {
                builder.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is IFormattable && !(value is IEnumerable))
            {
                builder.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            }

            GalaxyJsonObject obj = value as GalaxyJsonObject;
            if (obj != null)
            {
                builder.Append('{');
                IList<KeyValuePair<string, object>> items = obj.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    WriteString(items[i].Key, builder);
                    builder.Append(':');
                    Write(items[i].Value, builder, depth + 1);
                }
                builder.Append('}');
                return;
            }

            IDictionary<string, int> intMap = value as IDictionary<string, int>;
            if (intMap != null)
            {
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, int> pair in Sorted(intMap))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(pair.Key, builder);
                    builder.Append(':');
                    builder.Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                }
                builder.Append('}');
                return;
            }
            IDictionary<string, string> stringMap = value as IDictionary<string, string>;
            if (stringMap != null)
            {
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, string> pair in Sorted(stringMap))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(pair.Key, builder);
                    builder.Append(':');
                    WriteString(pair.Value, builder);
                }
                builder.Append('}');
                return;
            }
            IDictionary<string, object> objectMap = value as IDictionary<string, object>;
            if (objectMap != null)
            {
                builder.Append('{');
                bool first = true;
                List<string> keys = new List<string>(objectMap.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(keys[i], builder);
                    builder.Append(':');
                    Write(objectMap[keys[i]], builder, depth + 1);
                }
                builder.Append('}');
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in enumerable)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    Write(item, builder, depth + 1);
                }
                builder.Append(']');
                return;
            }

            WriteString(value.ToString(), builder);
        }

        private static IEnumerable<KeyValuePair<string, int>> Sorted(IDictionary<string, int> map)
        {
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>(keys.Count);
            for (int i = 0; i < keys.Count; i++) result.Add(new KeyValuePair<string, int>(keys[i], map[keys[i]]));
            return result;
        }

        private static IEnumerable<KeyValuePair<string, string>> Sorted(IDictionary<string, string> map)
        {
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>(keys.Count);
            for (int i = 0; i < keys.Count; i++) result.Add(new KeyValuePair<string, string>(keys[i], map[keys[i]]));
            return result;
        }

        private static void WriteString(string text, StringBuilder builder)
        {
            if (text == null) { builder.Append("null"); return; }
            builder.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text)
            {
                _text = text;
                _index = 0;
            }

            public bool AtEnd { get { return _index >= _text.Length; } }

            public FormatException Error(string message)
            {
                return new FormatException("JSON, позиция " + _index.ToString(CultureInfo.InvariantCulture) + ": " + message);
            }

            public void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\uFEFF') _index++;
                    else break;
                }
            }

            public object ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Error("Слишком глубокая структура JSON.");
                SkipWhitespace();
                if (AtEnd) throw Error("Ожидалось значение.");
                char c = _text[_index];
                if (c == '{') return ParseObject(depth + 1);
                if (c == '[') return ParseArray(depth + 1);
                if (c == '"') return ParseString();
                if (c == 't') { ExpectLiteral("true"); return true; }
                if (c == 'f') { ExpectLiteral("false"); return false; }
                if (c == 'n') { ExpectLiteral("null"); return null; }
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                throw Error("Неожиданный символ '" + c + "'.");
            }

            private GalaxyJsonObject ParseObject(int depth)
            {
                _index++; // {
                GalaxyJsonObject obj = new GalaxyJsonObject();
                SkipWhitespace();
                if (!AtEnd && _text[_index] == '}') { _index++; return obj; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_index] != '"') throw Error("Ожидался ключ объекта.");
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || _text[_index] != ':') throw Error("Ожидался символ ':'.");
                    _index++;
                    object value = ParseValue(depth);
                    obj.Set(key, value);
                    SkipWhitespace();
                    if (AtEnd) throw Error("Незакрытый объект.");
                    if (_text[_index] == '}') { _index++; return obj; }
                    if (_text[_index] != ',') throw Error("Ожидалась запятая.");
                    _index++;
                }
            }

            private List<object> ParseArray(int depth)
            {
                _index++; // [
                List<object> list = new List<object>();
                SkipWhitespace();
                if (!AtEnd && _text[_index] == ']') { _index++; return list; }
                while (true)
                {
                    list.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (AtEnd) throw Error("Незакрытый массив.");
                    if (_text[_index] == ']') { _index++; return list; }
                    if (_text[_index] != ',') throw Error("Ожидалась запятая.");
                    _index++;
                }
            }

            private void ExpectLiteral(string literal)
            {
                if (_index + literal.Length > _text.Length ||
                    string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                    throw Error("Некорректное литеральное значение.");
                _index += literal.Length;
            }

            private string ParseString()
            {
                _index++; // "
                StringBuilder builder = new StringBuilder(32);
                while (true)
                {
                    if (AtEnd) throw Error("Незакрытая строка.");
                    char c = _text[_index++];
                    if (c == '"') return builder.ToString();
                    if (c == '\\')
                    {
                        if (AtEnd) throw Error("Незавершённый escape-символ.");
                        char escape = _text[_index++];
                        switch (escape)
                        {
                            case '"': builder.Append('"'); break;
                            case '\\': builder.Append('\\'); break;
                            case '/': builder.Append('/'); break;
                            case 'b': builder.Append('\b'); break;
                            case 'f': builder.Append('\f'); break;
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u': builder.Append(ParseUnicode()); break; // строка: может содержать пару суррогатов
                            default: throw Error("Неизвестный escape-символ.");
                        }
                        continue;
                    }
                    if (c < 0x20) throw Error("Управляющий символ в строке.");
                    builder.Append(c);
                }
            }

            private string ParseUnicode()
            {
                if (_index + 4 > _text.Length) throw Error("Неполный \\u-код.");
                int code = 0;
                for (int i = 0; i < 4; i++)
                {
                    char digit = _text[_index + i];
                    int value;
                    if (digit >= '0' && digit <= '9') value = digit - '0';
                    else if (digit >= 'a' && digit <= 'f') value = digit - 'a' + 10;
                    else if (digit >= 'A' && digit <= 'F') value = digit - 'A' + 10;
                    else throw Error("Неверный \\u-код.");
                    code = code * 16 + value;
                }
                _index += 4;
                if (code >= 0xD800 && code <= 0xDBFF && _index + 6 <= _text.Length &&
                    _text[_index] == '\\' && _text[_index + 1] == 'u')
                {
                    int low = 0;
                    bool valid = true;
                    for (int i = 0; i < 4; i++)
                    {
                        char digit = _text[_index + 2 + i];
                        int value;
                        if (digit >= '0' && digit <= '9') value = digit - '0';
                        else if (digit >= 'a' && digit <= 'f') value = digit - 'a' + 10;
                        else if (digit >= 'A' && digit <= 'F') value = digit - 'A' + 10;
                        else { valid = false; break; }
                        low = low * 16 + value;
                    }
                    if (valid && low >= 0xDC00 && low <= 0xDFFF)
                    {
                        _index += 6;
                        return new string(new char[] { (char)code, (char)low });
                    }
                }
                if (code >= 0xD800 && code <= 0xDFFF) return "\uFFFD";
                return ((char)code).ToString();
            }

            private object ParseNumber()
            {
                int start = _index;
                if (!AtEnd && _text[_index] == '-') _index++;
                while (!AtEnd && ((_text[_index] >= '0' && _text[_index] <= '9') ||
                                  _text[_index] == '.' || _text[_index] == 'e' || _text[_index] == 'E' ||
                                  _text[_index] == '+' || _text[_index] == '-')) _index++;
                string token = _text.Substring(start, _index - start);
                bool floating = token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0;
                if (!floating)
                {
                    long integer;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    {
                        if (integer >= int.MinValue && integer <= int.MaxValue) return (int)integer;
                        return integer;
                    }
                }
                double number;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                    !double.IsNaN(number) && !double.IsInfinity(number)) return number;
                throw Error("Неверное число.");
            }
        }
    }
}
