namespace Roleplay.Jobs.Server;

public sealed class HerbalismConfig
{
    // Picking the game's plants follows Alchemy: a plant is known from its tier of Alchemy on (Novice 0, Apprentice 25,
    // Journeyman 50, Expert 75, Master 100). One not known is often crushed instead, and gives nothing. Off lets anyone
    // pick anything as the game does.
    public bool Enabled { get; set; } = true;

    // The plants, each with the name shown over it and its tier, next to the server DLL. A plant not in it is not
    // gated at all.
    public string HerbFile { get; set; } = "herbs.json";

    // Up to this many tiers short of a plant, a character sees it is a herb ("???" over it) without knowing which.
    public int RecognisedTiersBelow { get; set; } = 1;

    // The chance, 0 to 1, that a character picks a plant they recognise, or one they do not know at all, instead of
    // crushing it. A plant they know is picked as the game picks it.
    public double RecognisedPickChance { get; set; } = 0.5;
    public double UnawarePickChance { get; set; } = 0.25;

    // A plant picked without knowing it gives an item named UnknownHerbName (its ingredient under another name, worth
    // nothing), which turns into the ingredient once the picker's Alchemy reaches the plant. Off gives the ingredient.
    public bool UnknownHerbs { get; set; } = true;
    public string UnknownHerbName { get; set; } = "Unknown herb";

    // This many tiers above a plant, a character knows it well enough to find ExtraYield more of its ingredient at
    // every harvest, even one where the season gives nothing.
    public int MasteredTiersAbove { get; set; } = 2;
    public int ExtraYield { get; set; } = 1;

    // Names over the unpicked plants within MarkerDistance centimeters of the player, as the crosshair names them.
    public bool Markers { get; set; } = true;
    public float MarkerDistance { get; set; } = 800f;

    // How often each player's Alchemy is read, to tell their client when it reaches another tier.
    public double AlchemyCheckSeconds { get; set; } = 2;
}
