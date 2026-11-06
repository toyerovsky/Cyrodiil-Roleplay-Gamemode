using CyrodiilRP.Data;
using CyrodiilRP.Data.Sanctions;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Sanctions;
using CyrodiilRP.Server.Spawn;
using CyrodiilRP.Server.Staff;
using CyrodiilRP.Server.Traders;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

// Read when asked: the players from memory, the rest from the database.
[Service]
internal sealed partial class AdminViews(
    StaffRoles roles,
    OnlinePlayers online,
    Proximity proximity,
    IEntities entities,
    Freezes freezes,
    TraderSpots traders,
    SpawnPoints spawn,
    Persistence persistence)
{
    private const int AuditPageSize = 25;

    // A screenful of the item tab, which the staff narrow with a type and a few letters.
    private const int ItemsShown = 60;
    private const int HistorySize = 15;

    private static readonly string[] SanctionKeys = [Mutes.MuteKey, Freezes.FreezeKey];

    public AdminView BuildState(OnlinePlayer player, int? focus)
    {
        bool Allows(string name) => roles.Allows(player, name);
        string? area = proximity.TryLocate(player.Id, out var located, out _) ? located.ToString() : null;
        return new AdminView(
            AdminTabs.Players,
            focus,
            player.Number,
            area,
            roles.Acts(player),
            player.DutySince is not null,
            AdminActions.ListTabs(Allows),
            AdminActions.Commands.Where(Allows).ToList(),
            online.All.Where(p => p.IsLoaded).OrderBy(p => p.Number).Select(BuildPlayerView).ToList(),
            PlaceCatalog.Shipped.All.Select(place => new AdminPlaceView(place.Id, place.Name, place.Kind)).ToList(),
            WeatherCatalog.Shipped.Names,
            traders.All.Select(spot => new AdminTraderView(spot.Id, spot.Shop, spot.Area, (int)spot.Position.X, (int)spot.Position.Y,
                (int)spot.Position.Z)).ToList(),
            spawn.Current is { } start
                ? new AdminSpawnView((int)start.Position.X, (int)start.Position.Y, (int)start.Position.Z)
                : null);
    }

    public static List<AdminItemView> SearchItems(AdminItemFilter filter) =>
        ItemCatalog.Shipped.Search(filter.Text, filter.Type, ItemsShown).Select(item => new AdminItemView(item.Id, item.Name, item.Type, 0, false)).ToList();

    // Null when the target left while the database answered.
    public async Task<AdminCardView?> ReadCardAsync(OnlinePlayer target)
    {
        var conditions = target.Character?.Conditions.Values.Where(c => SanctionKeys.Contains(c.Key)).ToList() ?? [];
        var query = new AuditQuery(null, null, target.ReadyMId.ToString(), null, null, null, HistorySize);
        var history = await persistence.Reader.RunAsync((db, ct) => AuditBook.LoadPageAsync(db, query, ct));
        var names = await persistence.Reader.RunAsync((db, ct) => AuditBook.LoadNamesAsync(db, conditions.Select(c => c.ByRef), ct));
        if (!target.IsLive)
            return null;

        string? area = null;
        string? position = null;
        if (proximity.TryLocate(target.Id, out var located, out var at))
        {
            area = located.ToString();
            position = $"{(int)at.X} {(int)at.Y} {(int)at.Z}";
        }

        var sanctions = conditions.Select(c => new AdminSanctionView(c.Key, target.ReadyMId.ToString(), target.OocName, target.Character?.FullName,
            ToMilliseconds(c.AppliedAt), ToMilliseconds(c.ExpiresAt), c.Reason, FindName(names, c.ByRef), target.Number)).ToList();
        return new AdminCardView(
            target.Number,
            target.Name,
            target.OocName,
            target.ReadyMId.ToString(),
            ToMilliseconds(target.AccountCreatedAt),
            ToMilliseconds(target.PreviousVisitAt),
            ToMilliseconds(target.Character?.CreatedAt),
            target.Character?.PlayedSeconds ?? 0,
            area,
            position,
            sanctions,
            history.Entries.Select(BuildEntryView).ToList(),
            ListInventory(target));
    }

    public async Task<List<AdminSanctionView>> ReadSanctionsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await persistence.Reader.RunAsync((db, ct) => SanctionBook.LoadActiveAsync(db, SanctionKeys, now, ct));
        var names = await persistence.Reader.RunAsync((db, ct) => AuditBook.LoadNamesAsync(db, rows.Select(r => r.ByRef), ct));
        return rows.Select(row => new AdminSanctionView(row.Kind, row.ReadyMId.ToString(), row.Nickname, row.Character, ToMilliseconds(row.Since),
            ToMilliseconds(row.Until), row.Reason, FindName(names, row.ByRef), online.TryFind(row.ReadyMId, out var player) ? player.Number : null)).ToList();
    }

    public async Task<AdminAuditView> ReadAuditAsync(AdminAuditFilter filter)
    {
        var query = new AuditQuery(filter.Action, filter.Actor, filter.Target, ToMoment(filter.From), ToMoment(filter.To), filter.Before, AuditPageSize);
        var page = await persistence.Reader.RunAsync((db, ct) => AuditBook.LoadPageAsync(db, query, ct));
        return new AdminAuditView(page.Entries.Select(BuildEntryView).ToList(), page.More, filter.Before is not null);
    }

    private AdminPlayerView BuildPlayerView(OnlinePlayer player)
    {
        string? area = proximity.TryLocate(player.Id, out var located, out _) ? located.ToString() : null;
        int? health = null;
        int? healthMax = null;
        if (entities.TryLookup(player.Id, out MainCharacter main))
        {
            health = (int)MathF.Ceiling(main.Hp);
            healthMax = (int)MathF.Ceiling(main.HpBase);
        }

        return new AdminPlayerView(player.Number, player.Name, player.OocName, player.Roles, player.DutySince is not null,
            player.Character is not null, player.Character?.Conditions.ContainsKey(Mutes.MuteKey) == true, freezes.IsFrozen(player),
            area, health, healthMax);
    }

    private List<AdminItemView>? ListInventory(OnlinePlayer player)
    {
        if (!entities.TryLookup(player.Id, out MainCharacter main))
            return null;

        var inventory = EntityHandle.Of(main).As<Equipment>();
        var items = new List<AdminItemView>();
        for (var i = 0; i < inventory.ItemsCount; i++)
        {
            var entry = inventory.GetItems(i);
            var form = entry.FormId.FormId.Value;
            var item = ItemCatalog.Shipped.FindByForm(form);
            items.Add(new AdminItemView(item?.Id ?? $"{form:X8}", item?.Name ?? $"{form:X8}", item?.Type ?? string.Empty, entry.Count, entry.Worn != 0));
        }

        return items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AdminAuditEntryView BuildEntryView(AuditRow row) =>
        new(row.Id, row.At.ToUnixTimeMilliseconds(), row.Action, row.Actor, row.Target, row.Details);

    private static string? FindName(Dictionary<string, string> names, string? reference) =>
        reference is null ? null : names.GetValueOrDefault(reference, reference);

    private static long? ToMilliseconds(DateTimeOffset? at) => at?.ToUnixTimeMilliseconds();

    private static long ToMilliseconds(DateTimeOffset at) => at.ToUnixTimeMilliseconds();

    private static DateTimeOffset? ToMoment(long? milliseconds) => milliseconds is { } value ? DateTimeOffset.FromUnixTimeMilliseconds(value) : null;
}
