using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Welcome;

internal static class WelcomeCards
{
    public static WelcomeView Build(OnlinePlayer player)
    {
        List<WelcomeCharacterView> characters = player.Character is { } character
            ? [new WelcomeCharacterView(character.FirstName, character.LastName, character.LastPlayedAt.ToUnixTimeMilliseconds(), character.PlayedSeconds)]
            : [];
        var empty = Math.Max(0, CharacterSlots.Open - characters.Count);
        return new WelcomeView(player.AccountName, player.PreviousVisitAt is null, characters, empty, CharacterSlots.Locked);
    }
}
