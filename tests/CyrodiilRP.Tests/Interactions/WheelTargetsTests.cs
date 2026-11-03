using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Interactions;

public sealed class WheelTargetsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Only_players_who_took_their_character_over_can_be_picked_in_front()
    {
        var playing = CreatePlayer(withCharacter: true, playing: true);
        var atTheWelcome = CreatePlayer(withCharacter: true, playing: false);
        var makingOne = CreatePlayer(withCharacter: false, playing: false);
        var notLoaded = new OnlinePlayer(default, Guid.NewGuid(), Now);

        var targets = WheelTargets.Filter([playing, atTheWelcome, makingOne, notLoaded]).ToList();

        Assert.Same(playing, Assert.Single(targets));
    }

    private static OnlinePlayer CreatePlayer(bool withCharacter, bool playing) =>
        new(default, Guid.NewGuid(), Now)
        {
            IsLoaded = true,
            IsPlaying = playing,
            Character = withCharacter ? CharacterRecord.Create(new CharacterSnapshot(1, "Marcus", "Varro", Now, Now, 0), []) : null,
        };
}
