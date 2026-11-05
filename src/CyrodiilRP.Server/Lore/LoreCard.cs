using System.Collections.Immutable;

namespace CyrodiilRP.Server.Lore;

// A character's card. An edit makes a new one: the service that keeps the cards puts it in the old one's place.
internal sealed record LoreCard(bool NamePublic, ImmutableDictionary<string, LoreValue> Values)
{
    public static LoreCard Blank { get; } = new(false, ImmutableDictionary.Create<string, LoreValue>(StringComparer.OrdinalIgnoreCase));

    public bool IsEmpty => Values.Values.All(value => value.IsEmpty);

    // All the text on the card, for its limit.
    public int TextLength => Values.Values.Sum(value => value.Text?.Length ?? 0);

    public LoreValue? Get(LoreFieldDefinition field) => Values.GetValueOrDefault(field.Id);

    public LoreCard With(LoreFieldDefinition field, LoreValue value) => this with { Values = Values.SetItem(field.Id, value) };
}
