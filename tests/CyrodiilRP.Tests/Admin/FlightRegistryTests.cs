using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Admin;

public sealed class FlightRegistryTests
{
    private static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(1);

    private readonly FakeClock _clock = new();
    private readonly FlightRegistry _flights;
    private readonly OnlinePlayer _flyer = CreatePlayer();
    private readonly OnlinePlayer _other = CreatePlayer();

    public FlightRegistryTests() => _flights = new FlightRegistry(_clock);

    [Fact]
    public void Nobody_flies_or_is_spared_before_a_flight()
    {
        Assert.False(_flights.IsFlying(_flyer));
        Assert.False(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_flyer_flies_and_is_spared_and_nobody_else_is()
    {
        _flights.TakeOff(_flyer);

        Assert.True(_flights.IsFlying(_flyer));
        Assert.True(_flights.Spares(_flyer));
        Assert.False(_flights.IsFlying(_other));
        Assert.False(_flights.Spares(_other));
    }

    [Fact]
    public void A_flyer_who_landed_flies_no_more_and_is_spared_the_fall_for_the_landing_grace()
    {
        _flights.TakeOff(_flyer);

        _flights.Land(_flyer, isHeld: false);
        Assert.False(_flights.IsFlying(_flyer));

        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        Assert.True(_flights.Spares(_flyer));

        _clock.Advance(Moment);
        Assert.False(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_flyer_held_where_the_flight_ended_is_spared_until_dropped_and_for_the_landing_grace_after()
    {
        _flights.TakeOff(_flyer);
        _flights.Land(_flyer, isHeld: true);
        Assert.False(_flights.IsFlying(_flyer));

        _clock.Advance(FlightRegistry.LandingGrace * 4);
        Assert.True(_flights.Spares(_flyer));

        _flights.Drop(_flyer);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        Assert.True(_flights.Spares(_flyer));

        _clock.Advance(Moment);
        Assert.False(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_drop_spares_nobody_who_was_not_held_and_gives_a_falling_flyer_no_more_time()
    {
        _flights.Drop(_other);
        Assert.False(_flights.Spares(_other));

        _flights.TakeOff(_flyer);
        _flights.Drop(_flyer);
        Assert.True(_flights.IsFlying(_flyer));

        _flights.Land(_flyer, isHeld: false);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        _flights.Drop(_flyer);
        _clock.Advance(Moment);
        Assert.False(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_new_flight_within_the_grace_is_spared_as_a_flight_and_lands_with_a_grace_of_its_own()
    {
        _flights.TakeOff(_flyer);
        _flights.Land(_flyer, isHeld: false);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);

        _flights.TakeOff(_flyer);
        _clock.Advance(FlightRegistry.LandingGrace);
        Assert.True(_flights.Spares(_flyer));

        _flights.Land(_flyer, isHeld: false);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        Assert.True(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_player_who_left_is_forgotten_in_the_air_held_and_in_the_grace()
    {
        var held = CreatePlayer();
        _flights.TakeOff(_flyer);
        _flights.TakeOff(_other);
        _flights.Land(_other, isHeld: false);
        _flights.TakeOff(held);
        _flights.Land(held, isHeld: true);

        _flights.Forget(_flyer);
        _flights.Forget(_other);
        _flights.Forget(held);

        Assert.False(_flights.IsFlying(_flyer));
        Assert.False(_flights.Spares(_flyer));
        Assert.False(_flights.Spares(_other));
        Assert.False(_flights.Spares(held));
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
