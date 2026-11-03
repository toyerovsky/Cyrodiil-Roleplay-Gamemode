using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Server.Characters;

// The platform keeps one game save per ReadyM account, so a second character would start with everything the first one
// earned. One character an account until it keeps a save per character.
internal static class CharacterSlots
{
    public const int Open = 1;

    public const int Locked = 1;

    public static bool MayPlay(OnlinePlayer player) => player.IsLoaded && !player.IsPlaying && player.Character is not null;

    public static bool MayCreate(OnlinePlayer player) => player.IsLoaded && !player.IsPlaying && player.Character is null;
}
