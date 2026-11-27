namespace CyrodiilRP.Data.Fishing;

// One of the longest fish of a species (a FishSpecies' name) and whose it is: the character's name, and whether everyone
// may read it.
public sealed record FishBoardRow(
    string Species,
    long CharacterId,
    string FirstName,
    string LastName,
    bool NamePublic,
    int BestLengthCm,
    int BestGrams,
    DateTimeOffset BestAt);
