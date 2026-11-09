using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Tests.Fakes;

namespace CyrodiilRP.Tests.Interactions;

public sealed class WheelOptionsTests
{
    private const string StaffOnly = "aheal";
    private const string Closed = "test.closed";

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeWheelRules _rules = new();
    private readonly WheelOptions _options;
    private readonly OnlinePlayer _actor = CreatePlayer();
    private readonly OnlinePlayer _target = CreatePlayer();
    private readonly List<string> _ran = [];

    public WheelOptionsTests() => _options = new WheelOptions(_rules);

    [Fact]
    public void Options_come_in_their_order_whatever_order_they_were_added_in()
    {
        _options.Add(CreateOption("second", order: 20));
        _options.Add(CreateOption("first", order: 10));

        Assert.Equal(["first", "second"], ListIds());
    }

    [Fact]
    public void A_staff_option_shows_only_to_a_role_with_its_permission()
    {
        _options.Add(CreateOption("heal", permission: StaffOnly));
        Assert.Empty(ListIds());

        _rules.Permissions.Add(StaffOnly);
        Assert.Equal(["heal"], ListIds());
    }

    [Fact]
    public void An_option_that_speaks_is_hidden_from_the_muted()
    {
        _options.Add(CreateOption("greet", speaks: true));
        _options.Add(CreateOption("trade"));
        _rules.Muted = true;

        Assert.Equal(["trade"], ListIds());
    }

    [Fact]
    public void An_option_out_of_reach_is_hidden()
    {
        _options.Add(CreateOption("pay"));
        _rules.Near = false;

        Assert.Empty(ListIds());
    }

    [Fact]
    public void A_closed_option_shows_disabled_with_its_reason_and_a_hidden_one_not_at_all()
    {
        _options.Add(CreateOption("pay", OptionState.Close(new Phrase(Closed)), order: 1));
        _options.Add(CreateOption("secret", OptionState.Hidden, order: 2));
        _options.Add(CreateOption("greet", order: 3));

        var items = _options.BuildItems(_actor, _target);

        Assert.Equal(["pay", "greet"], items.Select(item => item.Id));
        Assert.True(items[0].Disabled);
        Assert.Equal(Closed, items[0].Reason);
        Assert.False(items[1].Disabled);
        Assert.Equal("", items[1].Reason);
    }

    [Fact]
    public void A_wheel_of_more_than_a_page_keeps_every_option_in_order()
    {
        for (var i = 0; i < WheelOptions.PageSize + 2; i++)
            _options.Add(CreateOption($"option{i}", order: i));

        var items = _options.BuildItems(_actor, _target);

        Assert.Equal(Enumerable.Range(0, WheelOptions.PageSize + 2).Select(i => $"option{i}"), items.Select(item => item.Id));
    }

    [Fact]
    public void An_id_added_twice_is_refused()
    {
        _options.Add(CreateOption("greet"));

        Assert.Throws<InvalidOperationException>(() => _options.Add(CreateOption("greet")));
    }

    [Fact]
    public void A_pick_runs_the_option_on_the_target()
    {
        _options.Add(CreateOption("greet"));

        _options.Pick(_actor, _target, "greet");

        Assert.Equal(["greet"], _ran);
    }

    [Fact]
    public void A_pick_after_the_target_walked_away_or_left_runs_nothing_and_says_so()
    {
        _options.Add(CreateOption("greet"));

        _rules.Near = false;
        _options.Pick(_actor, _target, "greet");
        _rules.Near = true;
        _target.IsLive = false;
        _options.Pick(_actor, _target, "greet");

        Assert.Empty(_ran);
        Assert.Equal([InteractionsText.Gone.Key, InteractionsText.Gone.Key], _rules.Notices);
    }

    [Fact]
    public void A_pick_of_what_has_since_closed_runs_nothing_and_gives_the_reason()
    {
        var open = true;
        _options.Add(new InteractionOption
        {
            Id = "pay",
            Label = new Phrase("test.pay"),
            Icon = WheelIcons.Coins,
            State = (_, _) => open ? OptionState.Open : OptionState.Close(new Phrase(Closed)),
            Run = (_, _) => _ran.Add("pay"),
        });

        open = false;
        _options.Pick(_actor, _target, "pay");

        Assert.Empty(_ran);
        Assert.Equal([Closed], _rules.Notices);
    }

    [Fact]
    public void A_pick_of_what_is_hidden_now_runs_nothing_and_says_it_is_gone()
    {
        _options.Add(CreateOption("join", OptionState.Hidden));

        _options.Pick(_actor, _target, "join");

        Assert.Empty(_ran);
        Assert.Equal([InteractionsText.Gone.Key], _rules.Notices);
    }

    [Fact]
    public void A_pick_of_an_option_the_wheel_does_not_have_is_ignored()
    {
        _options.Pick(_actor, _target, "nothing");

        Assert.Empty(_ran);
        Assert.Empty(_rules.Notices);
    }

    private List<string> ListIds() => _options.BuildItems(_actor, _target).Select(item => item.Id).ToList();

    private InteractionOption CreateOption(string id, OptionState? state = null, int order = 0, string? permission = null, bool speaks = false) =>
        new()
        {
            Id = id,
            Label = new Phrase($"test.{id}"),
            Icon = WheelIcons.Hand,
            Order = order,
            Permission = permission,
            Speaks = speaks,
            State = (_, _) => state ?? OptionState.Open,
            Run = (_, _) => _ran.Add(id),
        };

    private static OnlinePlayer CreatePlayer() => new(default, Guid.NewGuid(), Now) { IsLoaded = true };
}
