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
        Assert.False(_flights.Land(_flyer));
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

        Assert.True(_flights.Land(_flyer));
        Assert.False(_flights.Land(_flyer));
        Assert.False(_flights.IsFlying(_flyer));

        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        Assert.True(_flights.Spares(_flyer));

        _clock.Advance(Moment);
        Assert.False(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_new_flight_within_the_grace_is_spared_as_a_flight_and_lands_with_a_grace_of_its_own()
    {
        _flights.TakeOff(_flyer);
        _flights.Land(_flyer);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);

        _flights.TakeOff(_flyer);
        _clock.Advance(FlightRegistry.LandingGrace);
        Assert.True(_flights.Spares(_flyer));

        _flights.Land(_flyer);
        _clock.Advance(FlightRegistry.LandingGrace - Moment);
        Assert.True(_flights.Spares(_flyer));
    }

    [Fact]
    public void A_player_who_left_is_forgotten_in_the_air_and_in_the_grace()
    {
        _flights.TakeOff(_flyer);
        _flights.TakeOff(_other);
        _flights.Land(_other);

        _flights.Forget(_flyer);
        _flights.Forget(_other);

        Assert.False(_flights.IsFlying(_flyer));
        Assert.False(_flights.Spares(_flyer));
        Assert.False(_flights.Spares(_other));
    }

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
