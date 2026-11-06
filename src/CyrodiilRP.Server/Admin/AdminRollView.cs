using CyrodiilRP.Server.Dice;

namespace CyrodiilRP.Server.Admin;

// One roll in the log. At in Unix milliseconds, Character the name it was rolled under, Area the game's id of where.
internal sealed record AdminRollView(
    long Id,
    long At,
    string Character,
    string Asked,
    IReadOnlyList<DieView> Dice,
    int Total,
    string? Reason,
    string? Area);
