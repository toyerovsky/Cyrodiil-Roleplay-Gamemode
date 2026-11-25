namespace CyrodiilRP.Server.Fishing;

// What takes the bait at a cast, decided as the line goes in: a fish of a size, or a find.
internal abstract record Quarry
{
    public abstract FishFight Fight { get; }

    // Of the species' span of lengths, 0 the shortest and 1 the longest.
    public abstract double SizeShare { get; }
}
