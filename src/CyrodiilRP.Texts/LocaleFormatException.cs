using System;

namespace CyrodiilRP.Texts;

internal sealed class LocaleFormatException(string message, int line) : FormatException(message)
{
    public int Line { get; } = line;
}
