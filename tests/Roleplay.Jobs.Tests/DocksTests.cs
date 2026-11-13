using System.Numerics;
using Roleplay.Jobs.Common;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public sealed class DocksTests : IDisposable
{
    private const float Size = 300f;

    private readonly string _folder = Directory.CreateTempSubdirectory("jobs-tests-").FullName;
    private readonly JobsConfig _config = new();
    private readonly FelledTreeStoreTests.FailOnErrorLogger _logger = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData(0f, 0f, 0f, true)]       // the middle of the floor
    [InlineData(140f, 140f, 290f, true)] // a top corner
    [InlineData(160f, 0f, 50f, false)]   // past a side
    [InlineData(0f, 0f, 320f, false)]    // above
    [InlineData(0f, 0f, -80f, false)]    // under the floor
    [InlineData(0f, 0f, -30f, true)]     // a little under it: a slope
    public void A_deposit_holds_what_is_inside_its_cube(float x, float y, float z, bool holds)
    {
        var zone = new DockZone("zone", DockZoneKind.Outlet, 1000f, 2000f, 100f, 0f);

        Assert.Equal(holds, zone.Holds(new Vector3(1000f + x, 2000f + y, 100f + z), Size));
    }

    [Fact]
    public void A_turned_deposit_holds_along_its_own_axes()
    {
        var turned = new DockZone("zone", DockZoneKind.Inlet, 0f, 0f, 0f, 45f);
        var straight = turned with { Yaw = 0f };
        var corner = new Vector3(0f, 200f, 10f);

        Assert.True(turned.Holds(corner, Size));    // 141 along and 141 across its own axes
        Assert.False(straight.Holds(corner, Size)); // 200 across
        Assert.False(turned.Holds(new Vector3(150f, 150f, 10f), Size)); // 212 along
    }

    private static readonly Vector3 Crate = new(60f, 40f, 45f);

    [Fact]
    public void An_inlet_stacks_a_tight_pyramid_of_crates_inside_itself()
    {
        var inlet = new DockZone("inlet", DockZoneKind.Inlet, 500f, -200f, 40f, 30f);

        var slots = InletSlots.Of(inlet, rows: 3, layers: 3, Crate, gap: 2f, spacing: 90f, Size, lift: 12f);

        Assert.Equal([9, 4, 1], slots.GroupBy(slot => slot.Layer).Select(layer => layer.Count()));
        Assert.All(slots, slot => Assert.True(inlet.Holds(slot.Position, Size)));
        Assert.Equal(inlet.Floor.Z + 12f, slots.First().Position.Z, 3); // on the pallet
        Assert.Equal(inlet.Floor.Z + 12f + 2 * Crate.Z, slots.Single(slot => slot.Layer == 2).Position.Z, 3);
        // Neighbours on the floor touch, but for the gap: one crate's length plus the gap apart
        var corner = slots.Single(slot => slot is { Layer: 0, Row: 0, Column: 0 });
        var next = slots.Single(slot => slot is { Layer: 0, Row: 1, Column: 0 });
        Assert.Equal(Crate.X + 2f, Vector3.Distance(corner.Position, next.Position), 3);
    }

    [Fact]
    public void A_crate_above_rests_on_the_four_under_it()
    {
        var inlet = new DockZone("inlet", DockZoneKind.Inlet, 0f, 0f, 0f, 0f);
        var slots = InletSlots.Of(inlet, 3, 3, Crate, 2f, 90f, Size, 0f);

        var top = slots.Single(slot => slot.Layer == 2);
        var under = InletSlots.Supports(top, slots).ToList();

        Assert.Equal(4, under.Count);
        Assert.All(under, slot => Assert.Equal(1, slot.Layer));
        Assert.Empty(InletSlots.Supports(slots[0], slots));
        // Resting on four, its middle is over theirs
        var middle = under.Aggregate(Vector3.Zero, (sum, slot) => sum + slot.Position) / 4f;
        Assert.Equal(0f, ChopGeometryDistance(middle, top.Position), 3);
    }

    [Fact]
    public void Without_the_crates_size_an_inlet_lays_a_flat_square()
    {
        var inlet = new DockZone("inlet", DockZoneKind.Inlet, 0f, 0f, 0f, 0f);

        var slots = InletSlots.Of(inlet, 3, 3, Vector3.Zero, 2f, 90f, Size, 0f);

        Assert.Equal(9, slots.Count);
        Assert.All(slots, slot => Assert.Equal(0, slot.Layer));
    }

    [Fact]
    public void A_crate_too_big_for_three_a_side_makes_a_smaller_pile()
    {
        var inlet = new DockZone("inlet", DockZoneKind.Inlet, 0f, 0f, 0f, 0f);

        var slots = InletSlots.Of(inlet, 3, 3, new Vector3(120f, 120f, 80f), 2f, 90f, Size, 0f);

        Assert.Equal([4, 1], slots.GroupBy(slot => slot.Layer).Select(layer => layer.Count()));
    }

    [Fact]
    public void The_piles_footprint_is_its_floor_layer_with_the_gaps()
    {
        Assert.Equal(new Vector2(3 * 62f - 2f, 3 * 42f - 2f), InletSlots.Footprint(3, Crate, 2f, 90f, Size));
        Assert.Equal(new Vector2(270f, 270f), InletSlots.Footprint(3, Vector3.Zero, 2f, 90f, Size));
        Assert.Equal(new Vector2(2 * 122f - 2f, 2 * 122f - 2f), InletSlots.Footprint(3, new Vector3(120f, 120f, 80f), 2f, 90f, Size));
    }

    [Fact]
    public void A_slot_is_taken_by_a_crate_in_it_and_covered_by_one_on_it()
    {
        var slot = new InletSlot(0, 0, 0, Vector3.Zero);

        Assert.True(InletSlots.IsTaken(slot, new Vector3(10f, -5f, 5f), Crate, 90f));
        Assert.False(InletSlots.IsTaken(slot, new Vector3(62f, 0f, 0f), Crate, 90f));   // the next crate along
        Assert.False(InletSlots.IsTaken(slot, new Vector3(0f, 0f, 45f), Crate, 90f));   // the one on top
        Assert.True(InletSlots.IsCovered(Vector3.Zero, new Vector3(31f, 21f, 45f), Crate, 90f));
        Assert.False(InletSlots.IsCovered(Vector3.Zero, new Vector3(62f, 0f, 0f), Crate, 90f));
    }

    private static float ChopGeometryDistance(Vector3 one, Vector3 other) => new Vector2(one.X - other.X, one.Y - other.Y).Length();

    [Fact]
    public void The_lumber_camp_leader_sells_the_axe_and_buys_firewood()
    {
        var leader = _config.Docks.Foremen.Single(role => role.Name == "Lumber Camp Leader");

        var offers = ForemanDeals.OffersFor(leader, new Dictionary<string, int> { ["Firewood"] = 12 });

        var axe = Assert.Single(offers, offer => offer.Kind == ForemanOfferKind.Buy);
        Assert.Equal(ForemanDeals.BuyPrefix + "0", axe.Id);
        Assert.Equal("Lumber Axe", axe.Name);
        Assert.Equal(50, axe.Price);
        Assert.Equal("WeapIronWarAxe", axe.Icon);
        var firewood = Assert.Single(offers, offer => offer.Kind == ForemanOfferKind.Sell);
        Assert.Equal(ForemanDeals.SellPrefix + "Firewood", firewood.Id);
        Assert.Equal(12, firewood.Carried);
    }

    [Fact]
    public void Both_kinds_of_stone_brick_are_bought_as_one()
    {
        var quarry = _config.Docks.Foremen.Single(role => role.Name == "Quarry Foreman");

        var offers = ForemanDeals.OffersFor(quarry, new Dictionary<string, int> { ["Stone Brick"] = 7 });

        var stone = Assert.Single(offers, offer => offer.Kind == ForemanOfferKind.Sell);
        Assert.Equal(ForemanDeals.SellPrefix + "Stone Brick", stone.Id);
        Assert.Equal(7, stone.Carried);
        Assert.Equal(150, Assert.Single(offers, offer => offer.Kind == ForemanOfferKind.Buy).Price);
    }

    [Fact]
    public void The_dock_foreman_only_pays_wages()
    {
        var docks = _config.Docks.Foremen.Single(role => role.PaysWages);

        Assert.Empty(ForemanDeals.OffersFor(docks, new Dictionary<string, int>()));
        Assert.Equal("Dock Foreman", docks.Name);
    }

    [Fact]
    public void Nothing_carried_is_offered_at_zero()
    {
        var leader = _config.Docks.Foremen.Single(role => role.Name == "Lumber Camp Leader");

        Assert.Equal(0, ForemanDeals.OffersFor(leader, new Dictionary<string, int>()).Single(offer => offer.Kind == ForemanOfferKind.Sell).Carried);
    }

    [Fact]
    public void Wages_owed_survive_a_restart_and_are_paid_once()
    {
        var account = Guid.NewGuid();
        var first = Wages();
        first.AddCrate(account);
        first.AddCrate(account);

        var second = Wages();
        Assert.Equal(2, second.Unpaid(account));
        Assert.Equal(2, second.TakeAll(account));
        Assert.Equal(0, Wages().TakeAll(account));
    }

    [Fact]
    public void Deposits_and_foremen_survive_a_restart()
    {
        var first = Docks();
        first.Add(new DockZone("jobs/dock/a", DockZoneKind.Inlet, 1f, 2f, 3f, 90f));
        first.Add(new Foreman("jobs/foreman/b", "Dock Foreman", "Tamriel", "Interior", 4f, 5f, 6f, 180f, 0));
        first.Replace(new Foreman("jobs/foreman/b", "Dock Foreman", "Tamriel", "Interior", 4f, 5f, 6f, 180f, 77));

        var second = Docks();

        Assert.Equal(DockZoneKind.Inlet, Assert.Single(second.Zones).Kind);
        Assert.Equal(77u, Assert.Single(second.Foremen).NpcRefId);
        Assert.True(second.Remove("jobs/dock/a"));
        Assert.Empty(Docks().Zones);
    }

    [Fact]
    public void Every_foreman_has_its_own_origin_clear_of_the_gamemodes_traders()
    {
        var origins = Enumerable.Range(0, _config.Docks.Foremen.Count).Select(index => unchecked((uint)ForemanNpcs.OriginOf(index))).ToList();

        Assert.Equal(origins.Count, origins.Distinct().Count());
        Assert.DoesNotContain(0xFFFFFFFCu, origins);
        Assert.DoesNotContain(0xFFFFFFFDu, origins);
    }

    private WageStore Wages()
    {
        var store = new WageStore(_config, _logger);
        store.UseFolder(_folder);
        return store;
    }

    private DocksStore Docks()
    {
        var store = new DocksStore(_config, _logger);
        store.UseFolder(_folder);
        return store;
    }
}
