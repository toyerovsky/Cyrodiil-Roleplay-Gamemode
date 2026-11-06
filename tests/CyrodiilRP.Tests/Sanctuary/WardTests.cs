using CyrodiilRP.Server.Sanctuary;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class WardTests
{
    [Fact]
    public void The_first_look_takes_the_health_as_it_is()
    {
        var ward = new Ward();
        Assert.Equal(0f, ward.Watch(60f, 100f));
        Assert.Equal(60f, ward.Kept);
    }

    [Fact]
    public void A_drop_is_reported_and_the_kept_health_stays()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(30f, ward.Watch(50f, 100f));
        Assert.Equal(80f, ward.Kept);
        Assert.Equal(80f, ward.RestoreTo(100f));
    }

    [Fact]
    public void The_same_drop_seen_again_before_the_write_lands_is_reported_again()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        ward.Watch(50f, 100f);
        Assert.Equal(30f, ward.Watch(50f, 100f));
    }

    [Fact]
    public void Healing_raises_the_kept_health()
    {
        var ward = new Ward();
        ward.Watch(40f, 100f);
        Assert.Equal(0f, ward.Watch(70f, 100f));
        Assert.Equal(70f, ward.Kept);
    }

    [Fact]
    public void A_small_step_down_is_not_a_drop_yet()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(0f, ward.Watch(79.7f, 100f));
        Assert.Equal(80f, ward.Kept);
    }

    [Fact]
    public void Small_steps_down_add_up_to_a_drop()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        Assert.Equal(0f, ward.Watch(79.6f, 100f));
        Assert.Equal(0f, ward.Watch(79.6f, 100f));
        Assert.Equal(0.8f, ward.Watch(79.2f, 100f), 3);
        Assert.Equal(80f, ward.RestoreTo(100f));
    }

    [Fact]
    public void A_lower_maximum_is_not_a_drop()
    {
        var ward = new Ward();
        ward.Watch(100f, 100f);
        Assert.Equal(0f, ward.Watch(90f, 90f));
        Assert.Equal(90f, ward.Kept);
    }

    [Fact]
    public void Accept_takes_a_loss_that_is_not_put_back()
    {
        var ward = new Ward();
        ward.Watch(80f, 100f);
        ward.Watch(50f, 100f);
        ward.Accept(50f);
        Assert.Equal(0f, ward.Watch(50f, 100f));
    }

    [Fact]
    public void A_health_the_staff_set_lower_is_accepted_and_no_drop()
    {
        var ward = new Ward();
        ward.Watch(100f, 100f);
        ward.Accept(10f);
        Assert.Equal(0f, ward.Watch(10f, 100f));
        Assert.Equal(10f, ward.RestoreTo(100f));
    }

    [Fact]
    public void A_forgotten_ward_takes_the_next_health_as_it_is()
    {
        var ward = new Ward();
        ward.Watch(100f, 100f);
        ward.Forget();
        Assert.Equal(0f, ward.Watch(40f, 100f));
        Assert.Equal(40f, ward.Kept);
    }

    [Fact]
    public void Nothing_worth_keeping_restores_the_full_bar()
    {
        Assert.Equal(120f, new Ward().RestoreTo(120f));

        var dead = new Ward();
        dead.Watch(0f, 120f);
        Assert.Equal(120f, dead.RestoreTo(120f));
    }
}
