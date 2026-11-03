using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Characters;

// The welcome offers Play to the character an account has and a new one only to an account without: the second slot
// waits for a save per character, whatever a client asks.
public sealed class CharacterSlotsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void A_player_with_a_character_may_play_it_and_may_not_make_another()
    {
        var player = CreatePlayer(withCharacter: true);

        Assert.True(CharacterSlots.MayPlay(player));
        Assert.False(CharacterSlots.MayCreate(player));
    }

    [Fact]
    public void A_player_without_a_character_may_make_one_and_has_nothing_to_play()
    {
        var player = CreatePlayer(withCharacter: false);

        Assert.True(CharacterSlots.MayCreate(player));
        Assert.False(CharacterSlots.MayPlay(player));
    }

    [Fact]
    public void Once_playing_the_welcome_is_over_until_the_next_join()
    {
        var player = CreatePlayer(withCharacter: true);
        player.IsPlaying = true;

        Assert.False(CharacterSlots.MayPlay(player));
        Assert.False(CharacterSlots.MayCreate(player));
    }

    [Fact]
    public void Nothing_is_offered_before_the_account_is_loaded()
    {
        var player = CreatePlayer(withCharacter: false);
        player.IsLoaded = false;

        Assert.False(CharacterSlots.MayCreate(player));
        Assert.False(CharacterSlots.MayPlay(player));
    }

    [Fact]
    public void An_account_has_one_slot_open_and_the_second_locked()
    {
        Assert.Equal(1, CharacterSlots.Open);
        Assert.Equal(1, CharacterSlots.Locked);
    }

    private static OnlinePlayer CreatePlayer(bool withCharacter) =>
        new(default, Guid.NewGuid(), Now)
        {
            IsLoaded = true,
            Character = withCharacter ? CharacterRecord.Create(new CharacterSnapshot(1, "Marcus", "Varro", Now, Now, 0), []) : null,
        };
}
