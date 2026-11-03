namespace CyrodiilRP.Server.Localization;

// A text before the reader's language is known. The generated *Text classes make one per key, so a key that does not
// exist or a missing argument does not compile.
internal sealed class Phrase(string key, params TextArg[] args)
{
    public string Key { get; } = key;

    public TextArg[] Args { get; } = args;

    public override string ToString() => Key;
}
