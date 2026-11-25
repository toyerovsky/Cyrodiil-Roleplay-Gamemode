using System.Numerics;
using CyrodiilRP.Common.Players;

namespace CyrodiilRP.Tests.Admin;

public sealed class FlightRulesTests
{
    private const float Frame = 0.02f;

    // A camera facing +X and looking 30 degrees up, and what is level to its right.
    private static readonly Vector3 LookingUp = new(0.8660254f, 0f, 0.5f);
    private static readonly Vector3 ToTheRight = new(0f, 1f, 0f);

    [Fact]
    public void A_key_pushes_its_way_and_no_key_pushes_nowhere()
    {
        Assert.Equal(Vector2.Zero, FlightRules.FindPush(isAheadHeld: false, isBackHeld: false, isLeftHeld: false, isRightHeld: false));
        Assert.Equal(new Vector2(0f, 1f), FlightRules.FindPush(isAheadHeld: true, isBackHeld: false, isLeftHeld: false, isRightHeld: false));
        Assert.Equal(new Vector2(0f, -1f), FlightRules.FindPush(isAheadHeld: false, isBackHeld: true, isLeftHeld: false, isRightHeld: false));
        Assert.Equal(new Vector2(-1f, 0f), FlightRules.FindPush(isAheadHeld: false, isBackHeld: false, isLeftHeld: true, isRightHeld: false));
        Assert.Equal(new Vector2(1f, 0f), FlightRules.FindPush(isAheadHeld: false, isBackHeld: false, isLeftHeld: false, isRightHeld: true));
    }

    [Fact]
    public void Opposite_keys_cancel_and_two_keys_at_once_push_no_harder_than_one()
    {
        Assert.Equal(Vector2.Zero, FlightRules.FindPush(isAheadHeld: true, isBackHeld: true, isLeftHeld: true, isRightHeld: true));
        Assert.Equal(new Vector2(1f, 0f), FlightRules.FindPush(isAheadHeld: true, isBackHeld: true, isLeftHeld: false, isRightHeld: true));

        var diagonal = FlightRules.FindPush(isAheadHeld: true, isBackHeld: false, isLeftHeld: false, isRightHeld: true);
        Assert.Equal(1f, diagonal.Length(), 0.0001f);
        Assert.Equal(diagonal.X, diagonal.Y, 0.0001f);
        Assert.True(diagonal.X > 0f);
    }

    [Fact]
    public void A_flyer_who_pushes_ahead_goes_the_way_the_camera_looks_up_and_down_too()
    {
        var step = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(0f, 1f), isHurried: false, Frame);

        AssertClose(LookingUp * (FlightRules.Speed * Frame), step);
    }

    [Fact]
    public void A_flyer_who_pushes_sideways_stays_level_and_one_who_pushes_back_goes_back()
    {
        var sideways = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(-1f, 0f), isHurried: false, Frame);
        var back = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(0f, -1f), isHurried: false, Frame);

        AssertClose(-ToTheRight * (FlightRules.Speed * Frame), sideways);
        Assert.Equal(0f, sideways.Z);
        AssertClose(-LookingUp * (FlightRules.Speed * Frame), back);
    }

    [Fact]
    public void A_flyer_who_does_not_push_hangs_where_they_are()
    {
        Assert.Equal(Vector3.Zero, FlightRules.FindStep(LookingUp, ToTheRight, Vector2.Zero, isHurried: true, Frame));
    }

    [Fact]
    public void A_lighter_push_flies_slower_and_a_hurried_flight_faster()
    {
        var light = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(0f, 0.5f), isHurried: false, Frame);
        var hurried = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(0f, 1f), isHurried: true, Frame);

        AssertClose(LookingUp * (FlightRules.Speed * 0.5f * Frame), light);
        AssertClose(LookingUp * (FlightRules.HurriedSpeed * Frame), hurried);
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(float.PositiveInfinity)]
    public void A_long_frame_carries_a_flyer_no_farther_than_the_longest_step(float seconds)
    {
        var step = FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(0f, 1f), isHurried: false, seconds);

        AssertClose(LookingUp * (FlightRules.Speed * FlightRules.LongestStepSeconds), step);
    }

    [Fact]
    public void A_frame_of_no_time_moves_nobody()
    {
        Assert.Equal(Vector3.Zero, FlightRules.FindStep(LookingUp, ToTheRight, push: new Vector2(1f, 1f), isHurried: true, seconds: -0.5f));
    }

    private static void AssertClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 0.001f, $"Expected {expected}, got {actual}.");
}
