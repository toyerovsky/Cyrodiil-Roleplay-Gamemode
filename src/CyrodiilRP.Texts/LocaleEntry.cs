using System.Collections.Generic;

namespace CyrodiilRP.Texts;

// Forms: one text, or the plural forms of an object.
internal sealed record LocaleEntry(string Key, IReadOnlyList<string> Forms, int Line);
