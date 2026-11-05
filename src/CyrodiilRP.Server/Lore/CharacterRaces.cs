using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Lore;

// The race of a player's character, from the identity the game keeps on it. Update thread only.
[Service]
internal sealed partial class CharacterRaces(IEntities entities, ILogger logger)
{
    // A race the mode has no name for is told once, so the table can be checked against the game.
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    // Unknown while the character is not in the world or has not been made yet.
    public GameRace Find(PlayerId player)
    {
        if (!entities.TryLookup(player, out MainCharacter main) || main.Race.Value == 0)
            return GameRace.Unknown;

        var race = GameRaces.FromFormId(main.Race.Value);
        if (race != GameRace.Unknown)
            return race;

        var path = main.RacePath.ToString();
        race = GameRaces.FromPath(path);
        if (race == GameRace.Unknown && _reported.Add($"{main.Race.Value:X8} {path}"))
            logger.LogWarning("[CyrodiilRP] The race {FormId:X8} ({Path}) has no lore name: strangers see {Label}.",
                main.Race.Value, path, GameRaces.StrangerLabel);

        return race;
    }
}
