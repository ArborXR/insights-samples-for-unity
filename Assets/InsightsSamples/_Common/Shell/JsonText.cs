using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A tiny JSON object writer for the structured log lines automation reads.
    /// </summary>
    public sealed class JsonText
    {
        private readonly StringBuilder _builder = new StringBuilder("{");
        private bool _first = true;

        public JsonText Field(string key, string value)
        {
            Key(key);
            if (value == null) _builder.Append("null");
            else Quote(value);
            return this;
        }

        public JsonText Field(string key, long value)
        {
            Key(key);
            _builder.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonText Field(string key, IEnumerable<KeyValuePair<string, string>> values)
        {
            Key(key);
            _builder.Append('{');
            var first = true;
            foreach (var pair in values)
            {
                if (!first) _builder.Append(',');
                first = false;
                Quote(pair.Key);
                _builder.Append(':');
                Quote(pair.Value ?? "");
            }
            _builder.Append('}');
            return this;
        }

        public JsonText Raw(string key, string json)
        {
            Key(key);
            _builder.Append(json);
            return this;
        }

        public override string ToString() => _builder + "}";

        private void Key(string key)
        {
            if (!_first) _builder.Append(',');
            _first = false;
            Quote(key);
            _builder.Append(':');
        }

        private void Quote(string value) => Quote(_builder, value);

        public static string Quoted(string value)
        {
            var builder = new StringBuilder();
            Quote(builder, value ?? "");
            return builder.ToString();
        }

        private static void Quote(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
