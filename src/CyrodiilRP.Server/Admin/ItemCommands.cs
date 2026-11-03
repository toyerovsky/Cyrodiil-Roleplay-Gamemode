using CyrodiilRP.Data.Staff;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Commands;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using OblivionMp.Sdk.Serverside.Items;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Admin;

[Service]
internal sealed partial class ItemCommands(CommandRegistry commands, IEntities entities, Inventories inventories, RpChat chat, Audit audit)
{
    public const int MostPieces = 1000;

    private const string Give = "give";
    private const string Take = "take";
    private const string GiveAction = "admin.item_give";
    private const string TakeAction = "admin.item_take";
    private const string CandidateSeparator = ", ";

    // give|take, the number and at least a word of the item.
    private const int LeastWords = 3;

    private void Start() => commands.AddStaff(StaffPermissions.Item, AdminText.CmdItem, Run);

    private void Run(CommandCall call)
    {
        var giving = call.Words.Count > 0 && call.Words[0].Equals(Give, StringComparison.OrdinalIgnoreCase);
        var taking = call.Words.Count > 0 && call.Words[0].Equals(Take, StringComparison.OrdinalIgnoreCase);
        if ((!giving && !taking) || call.Words.Count < LeastWords)
        {
            call.Refuse(AdminText.CmdItem);
            return;
        }

        if (!call.TryTarget(1, out var target) || !call.MayPunish(target, self: true))
            return;

        var count = 1;
        var skipLast = call.Words.Count > LeastWords && CommandLine.TryNumber(call.Words[^1], out count);
        if (count is < 1 or > MostPieces)
        {
            call.Refuse(AdminText.ItemCount(MostPieces));
            return;
        }

        var text = string.Join(' ', call.Words.Skip(2).Take(call.Words.Count - 2 - (skipLast ? 1 : 0)));
        var found = ItemCatalog.Shipped.Find(text);
        if (found.One is not { } item)
        {
            call.Refuse(found.Candidates.Count == 0
                ? AdminText.NoItem(text)
                : AdminText.WhichItem(string.Join(CandidateSeparator, found.Candidates.Select(c => $"{c.Name} ({c.Id})"))));
            return;
        }

        if (!entities.TryLookup(target.Id, out MainCharacter main))
        {
            call.Refuse(AdminText.NotInWorld(target.Name));
            return;
        }

        var inventory = EntityHandle.Of(main).As<Equipment>();
        if (giving)
        {
            inventories.Add(inventory, new InventoryEntry(new FormId(item.Form), count, 0));
            audit.Log(GiveAction, call.Sender.Ref, target.Ref, new ItemDetails(item.Id, count));
            chat.Notify(target, AdminText.ItemGiven(item: item.Name, count: count));
            call.Reply(AdminText.ItemGave(name: target.Name, item: item.Name, count: count));
            return;
        }

        if (!TryTakeAll(inventory, item.Form, count))
        {
            call.Refuse(AdminText.ItemTooFew(name: target.Name, item: item.Name));
            return;
        }

        audit.Log(TakeAction, call.Sender.Ref, target.Ref, new ItemDetails(item.Id, count));
        chat.Notify(target, AdminText.ItemTaken(item: item.Name, count: count));
        call.Reply(AdminText.ItemTook(name: target.Name, item: item.Name, count: count));
    }

    // Copies differ by their health and charge, and the inventory takes one kind at a time: from each kind in turn
    // until enough, or nothing when there are too few of them all.
    private bool TryTakeAll(Equipment inventory, uint form, int count)
    {
        var held = new Dictionary<StackKind, int>();
        for (var i = 0; i < inventory.ItemsCount; i++)
        {
            var entry = inventory.GetItems(i);
            if (entry.FormId.FormId.Value == form)
            {
                var kind = new StackKind(entry.Health, entry.Charge);
                held[kind] = held.GetValueOrDefault(kind) + entry.Count;
            }
        }

        if (held.Values.Sum() < count)
            return false;

        var left = count;
        foreach (var (kind, available) in held)
        {
            var taking = Math.Min(left, available);
            inventories.TryTake(inventory, new ItemStackData { BaseId = new FormId(form), Count = taking, Durability = kind.Health, Charge = kind.Charge });
            left -= taking;
            if (left == 0)
                break;
        }

        return true;
    }

    private sealed record StackKind(float Health, float Charge);

    private sealed record ItemDetails(string Item, int Count);
}
