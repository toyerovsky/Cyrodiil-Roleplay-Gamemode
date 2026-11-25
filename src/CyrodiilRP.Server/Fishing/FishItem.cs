namespace CyrodiilRP.Server.Fishing;

// A fish as an item: the plain one or its trophy.
internal sealed record FishItem(FishKind Fish, bool Trophy)
{
    public string Key => Trophy ? Fish.TrophyKey : Fish.ItemKey;

    public string Name => Trophy ? Fish.TrophyName : Fish.ItemName;

    public int Value => Trophy ? Fish.TrophyValue : Fish.Value;
}
