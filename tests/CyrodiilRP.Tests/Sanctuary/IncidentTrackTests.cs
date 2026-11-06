using CyrodiilRP.Data;
using CyrodiilRP.Server.Sanctuary;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class IncidentTrackTests
{
    private const string Market = "L_ICMarketDistrict";
    private const string Temple = "L_ICTempleDistrict";
    private const double Quiet = 3;

    private static readonly IncidentPerson Victim = new(Guid.NewGuid(), "Lucia Gallus");
    private static readonly IncidentPerson Brawler = new(Guid.NewGuid(), "Brannoc");
    private static readonly IncidentPerson Archer = new(Guid.NewGuid(), "Glarthir");

    private static OpenIncident Start(string area, double now) => new(SanctuaryVictim.Player, Victim, Victim.Name, area, now);

    [Fact]
    public void Hits_close_together_in_one_area_are_one_incident()
    {
        var track = new IncidentTrack();
        Assert.Null(track.Record(Market, 10, 12f, false, [Brawler], () => Start(Market, 10)));
        Assert.Null(track.Record(Market, 11, 8f, false, [Brawler], () => Start(Market, 11)));

        var open = Assert.IsType<OpenIncident>(track.Open);
        Assert.Equal(20f, open.Damage);
        Assert.Equal([Brawler], open.Suspects);
        Assert.Equal(SanctuaryVerdict.Suspect, open.Verdict);
    }

    [Fact]
    public void Every_suspect_counts_once_and_two_make_several()
    {
        var track = new IncidentTrack();
        track.Record(Market, 10, 5f, false, [Brawler], () => Start(Market, 10));
        track.Record(Market, 11, 5f, false, [Brawler, Archer], () => Start(Market, 11));

        Assert.Equal([Brawler, Archer], track.Open!.Suspects);
        Assert.Equal(SanctuaryVerdict.Several, track.Open.Verdict);
    }

    [Fact]
    public void A_fall_names_nobody()
    {
        var track = new IncidentTrack();
        track.Record(Market, 10, 40f, false, [], () => Start(Market, 10));
        Assert.Equal(SanctuaryVerdict.None, track.Open!.Verdict);
    }

    [Fact]
    public void A_hit_in_another_area_closes_the_open_incident_and_starts_a_new_one()
    {
        var track = new IncidentTrack();
        track.Record(Market, 10, 5f, false, [Brawler], () => Start(Market, 10));

        var closed = track.Record(Temple, 11, 7f, true, [], () => Start(Temple, 11));

        Assert.Equal(Market, closed!.Area);
        Assert.Equal(Temple, track.Open!.Area);
        Assert.True(track.Open.Died);
        Assert.Equal(7f, track.Open.Damage);
    }

    [Fact]
    public void An_incident_closes_once_it_has_been_quiet()
    {
        var track = new IncidentTrack();
        track.Record(Market, 10, 5f, false, [Brawler], () => Start(Market, 10));

        Assert.Null(track.CloseIfQuiet(10 + Quiet - 0.1, Quiet));
        Assert.NotNull(track.CloseIfQuiet(10 + Quiet, Quiet));
        Assert.Null(track.Open);
        Assert.Null(track.CloseIfQuiet(100, Quiet));
    }

    [Fact]
    public void A_death_stays_on_the_incident()
    {
        var track = new IncidentTrack();
        track.Record(Market, 10, 80f, true, [Brawler], () => Start(Market, 10));
        track.Record(Market, 11, 0f, false, [], () => Start(Market, 11));
        Assert.True(track.Open!.Died);
    }
}
