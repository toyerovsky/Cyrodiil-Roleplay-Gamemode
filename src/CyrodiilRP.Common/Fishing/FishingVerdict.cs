namespace CyrodiilRP.Common.Fishing;

// What the player's game found when the server asked where the water is around the character.
public enum FishingVerdict
{
    // Water the rod reaches, from the bank or wading in it.
    Found,

    // No water the rod reaches around the character.
    NoWater,

    // The character swims: too deep to fish.
    Swimming,

    // The character has no body in the world yet.
    NotInWorld,
}
