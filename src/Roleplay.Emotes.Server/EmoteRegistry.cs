namespace Roleplay.Emotes.Server;

// The emotes by Order, the same Order in the order they came.
internal sealed class EmoteRegistry
{
    private readonly List<Emote> _emotes = [];

    public IReadOnlyList<Emote> All => _emotes;

    public void Add(Emote emote)
    {
        Validate(emote);
        if (Find(emote.Id) is not null)
            throw new InvalidOperationException($"The emote {emote.Id} is registered twice.");

        var at = _emotes.FindIndex(e => e.Order > emote.Order);
        _emotes.Insert(at < 0 ? _emotes.Count : at, emote);
    }

    public bool Remove(string id) => _emotes.RemoveAll(e => e.Id == id) > 0;

    public Emote? Find(string id) => _emotes.Find(e => e.Id == id);

    private static void Validate(Emote emote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emote.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(emote.Name);
        CheckPath(emote.Animation, emote.Id);
        if (emote.FirstPersonAnimation is { } firstPerson)
            CheckPath(firstPerson, emote.Id);
        if (!Enum.IsDefined(emote.Kind))
            throw new ArgumentException($"The emote {emote.Id} has no kind of animation.", nameof(emote));
        if (emote.PlayRate <= 0f)
            throw new ArgumentException($"The emote {emote.Id} plays at {emote.PlayRate}, not above 0.", nameof(emote));
        if (emote.Seconds is <= 0f)
            throw new ArgumentException($"The emote {emote.Id} stops after {emote.Seconds} seconds, not above 0.", nameof(emote));
    }

    // An object path: /Game/Folder/Asset.Asset
    private static void CheckPath(string path, string id)
    {
        if (!path.StartsWith('/') || !path.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException($"The emote {id} names {path}, which is not an object path as /Game/Folder/Asset.Asset.", nameof(path));
    }
}
