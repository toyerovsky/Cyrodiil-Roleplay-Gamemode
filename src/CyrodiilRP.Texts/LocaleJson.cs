using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CyrodiilRP.Texts;

// System.Text.Json is not in netstandard2.0, and the diagnostics need each key's line.
internal sealed class LocaleJson
{
    private const int HexDigits = 4;

    private readonly string _text;
    private int _at;
    private int _line;

    private LocaleJson(string text) => _text = text;

    public static List<LocaleEntry> Parse(string text)
    {
        var reader = new LocaleJson(text);
        var entries = new List<LocaleEntry>();
        reader.Expect('{');
        if (reader.Peek() == '}')
        {
            reader._at++;
            reader.ExpectEnd();
            return entries;
        }

        while (true)
        {
            reader.Peek();
            var line = reader._line;
            var key = reader.ReadString();
            reader.Expect(':');
            entries.Add(new LocaleEntry(key, reader.Peek() == '{' ? reader.ReadForms() : new[] { reader.ReadString() }, line));
            if (reader.Peek() == ',')
            {
                reader._at++;
                continue;
            }

            reader.Expect('}');
            reader.ExpectEnd();
            return entries;
        }
    }

    private List<string> ReadForms()
    {
        var forms = new List<string>();
        Expect('{');
        while (true)
        {
            ReadString();
            Expect(':');
            forms.Add(ReadString());
            if (Peek() == ',')
            {
                _at++;
                continue;
            }

            Expect('}');
            return forms;
        }
    }

    private string ReadString()
    {
        Expect('"');
        var builder = new StringBuilder();
        while (true)
        {
            var c = ReadChar();
            if (c == '"')
                return builder.ToString();

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            var escaped = ReadChar();
            switch (escaped)
            {
                case '"': case '\\': case '/': builder.Append(escaped); break;
                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'u':
                    if (_at + HexDigits > _text.Length
                        || !int.TryParse(_text.Substring(_at, HexDigits), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        throw CreateError("\\u needs four hex digits");

                    builder.Append((char)code);
                    _at += HexDigits;
                    break;
                default: throw CreateError($"an unknown escape \\{escaped}");
            }
        }
    }

    private char ReadChar()
    {
        if (_at >= _text.Length)
            throw CreateError("the text ends inside a string");

        return _text[_at++];
    }

    private char Peek()
    {
        SkipSpace();
        return _at < _text.Length ? _text[_at] : '\0';
    }

    private void Expect(char c)
    {
        if (Peek() != c)
            throw CreateError($"'{c}' expected");

        _at++;
    }

    private void ExpectEnd()
    {
        if (Peek() != '\0')
            throw CreateError("text after the end");
    }

    private void SkipSpace()
    {
        while (_at < _text.Length && char.IsWhiteSpace(_text[_at]))
        {
            if (_text[_at] == '\n')
                _line++;

            _at++;
        }
    }

    private LocaleFormatException CreateError(string what) => new($"line {_line + 1}: {what}", _line);
}
