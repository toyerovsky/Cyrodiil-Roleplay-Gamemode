using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Sanctuary;

namespace CyrodiilRP.Tests.Sanctuary;

public sealed class BlameTests
{
    private const float Melee = 400f;
    private const float Ranged = 4000f;
    private const float Window = 1.5f;
    private const double Now = 100;
    private const double Never = double.NegativeInfinity;
    private const float CentimetresPerMetre = 100f;

    private static readonly Vector3 Victim = new(1000f, 2000f, 300f);

    private static Bystander<string> At(string name, float metres, double attack = Never, double cast = Never, bool combat = false) =>
        new(name, Victim + new Vector3(metres * CentimetresPerMetre, 0f, 0f), attack, cast, combat);

    private static Verdict<string> Judge(params Bystander<string>[] others) =>
        Blame.Judge(Victim, Now, others, Melee, Ranged, Window);

    [Fact]
    public void Nobody_fighting_means_no_suspect()
    {
        var verdict = Judge(At("Idle", 1f), At("Far swinger", 80f, attack: Now, combat: true));
        Assert.Equal(SanctuaryVerdict.None, verdict.Kind);
        Assert.Empty(verdict.Suspects);
    }

    [Fact]
    public void One_swinger_in_reach_is_the_suspect()
    {
        var verdict = Judge(At("Brawler", 2f, attack: Now - 0.2), At("Idle", 1f));
        Assert.Equal(SanctuaryVerdict.Suspect, verdict.Kind);
        Assert.Equal(["Brawler"], verdict.Suspects);
    }

    [Fact]
    public void A_swing_in_reach_outweighs_a_caster_further_away()
    {
        var verdict = Judge(At("Brawler", 3f, attack: Now), At("Mage", 20f, cast: Now, combat: true));
        Assert.Equal(["Brawler"], verdict.Suspects);
    }

    [Fact]
    public void Every_swinger_in_reach_is_a_suspect()
    {
        var verdict = Judge(At("One", 2f, attack: Now), At("Two", 3f, attack: Now - 1));
        Assert.Equal(SanctuaryVerdict.Several, verdict.Kind);
        Assert.Equal(["One", "Two"], verdict.Suspects);
    }

    [Fact]
    public void A_caster_in_combat_is_the_suspect()
    {
        var verdict = Judge(At("Mage", 30f, cast: Now - 0.5, combat: true));
        Assert.Equal(SanctuaryVerdict.Suspect, verdict.Kind);
        Assert.Equal(["Mage"], verdict.Suspects);
    }

    [Fact]
    public void A_caster_out_of_combat_is_not()
    {
        Assert.Equal(SanctuaryVerdict.None, Judge(At("Healer", 3f, cast: Now)).Kind);
        Assert.Equal(SanctuaryVerdict.None, Judge(At("Healer", 30f, cast: Now)).Kind);
    }

    [Fact]
    public void An_archer_counts_only_in_combat()
    {
        Assert.Equal(SanctuaryVerdict.Suspect, Judge(At("Archer", 25f, attack: Now, combat: true)).Kind);
        Assert.Equal(SanctuaryVerdict.None, Judge(At("Archer", 25f, attack: Now)).Kind);
    }

    [Fact]
    public void Two_in_combat_at_range_are_both_suspects()
    {
        var verdict = Judge(At("Archer", 25f, attack: Now, combat: true), At("Mage", 15f, cast: Now, combat: true));
        Assert.Equal(SanctuaryVerdict.Several, verdict.Kind);
        Assert.Equal(2, verdict.Suspects.Count);
    }

    [Fact]
    public void Beyond_the_ranged_range_does_not_count()
    {
        Assert.Equal(SanctuaryVerdict.None, Judge(At("Mage", Ranged / CentimetresPerMetre + 1, cast: Now, combat: true)).Kind);
    }

    [Fact]
    public void A_swing_outside_the_window_does_not_count()
    {
        Assert.Equal(SanctuaryVerdict.None, Judge(At("Brawler", 1f, attack: Now - Window - 0.5)).Kind);
    }

    [Fact]
    public void The_edges_of_reach_and_window_count()
    {
        Assert.Equal(SanctuaryVerdict.Suspect, Judge(At("Brawler", Melee / CentimetresPerMetre, attack: Now - Window)).Kind);
    }
}
