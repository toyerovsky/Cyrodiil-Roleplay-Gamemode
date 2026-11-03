using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Players;

namespace CyrodiilRP.Tests.Characters;

public sealed class NameReservationsTests
{
    private readonly NameReservations _reservations = new();
    private readonly OnlinePlayer _marius = CreatePlayer();
    private readonly OnlinePlayer _lucia = CreatePlayer();

    [Fact]
    public void One_player_holds_a_name_whatever_its_letter_case()
    {
        Assert.True(_reservations.TryReserve(_marius, new ReservedName("Marius", "Varro")));

        Assert.False(_reservations.TryReserve(_lucia, new ReservedName("MARIUS", "varro")));
        Assert.Null(_reservations.Find(_lucia));
        Assert.Equal(new ReservedName("Marius", "Varro"), _reservations.Find(_marius));
    }

    [Fact]
    public void A_player_giving_another_name_lets_go_of_the_one_before()
    {
        _reservations.TryReserve(_marius, new ReservedName("Marius", "Varro"));

        Assert.True(_reservations.TryReserve(_marius, new ReservedName("Gaius", "Varro")));

        Assert.True(_reservations.TryReserve(_lucia, new ReservedName("Marius", "Varro")));
        Assert.Equal(new ReservedName("Gaius", "Varro"), _reservations.Find(_marius));
    }

    [Fact]
    public void The_same_player_may_give_the_same_name_again()
    {
        _reservations.TryReserve(_marius, new ReservedName("Marius", "Varro"));

        Assert.True(_reservations.TryReserve(_marius, new ReservedName("Marius", "Varro")));
    }

    [Fact]
    public void A_player_who_leaves_frees_the_name_for_anybody()
    {
        _reservations.TryReserve(_marius, new ReservedName("Marius", "Varro"));

        _reservations.Release(_marius);

        Assert.Null(_reservations.Find(_marius));
        Assert.True(_reservations.TryReserve(_lucia, new ReservedName("Marius", "Varro")));
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UnixEpoch) { IsLoaded = true };
}
