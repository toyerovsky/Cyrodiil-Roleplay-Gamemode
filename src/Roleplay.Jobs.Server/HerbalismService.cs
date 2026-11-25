using System.Diagnostics;
using System.Globalization;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using OblivionMP.SDK.Common.Items;
using OblivionMP.SDK.Common.Statuses;
using OblivionMP.SDK.Serverside.Items;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.Relay.Common.Oblivion.Rpc;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;
using ReadyM.SDK.Server.Events;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Server;

/// <summary>
/// Herbalism: picking the game's plants by Alchemy (<see cref="HerbLore"/>). The game's harvest already asks the server
/// before it gives anything (an <see cref="ItemTransferEvent"/> with <see cref="ItemTransferCause.Harvest"/>), so this
/// decides each one: a plant the picker does not know is often crushed, and otherwise gives an "Unknown herb" (a custom
/// item copied from its ingredient) that turns into the ingredient once their Alchemy reaches the plant; one they have
/// mastered gives <see cref="HerbalismConfig.ExtraYield"/> more. Each client is told the herb list and its player's
/// Alchemy tier, and names the plants by them.
/// </summary>
[Service]
public sealed partial class HerbalismService(
    IEntities entities,
    IServerEvents serverEvents,
    ICustomItems customItems,
    HerbalismRpcHandlers rpc,
    HerbRegistry herbs,
    JobsConfig config,
    ILogger logger)
{
    private const char ListSeparator = '|';
    private const string UnknownKeyPrefix = "jobs.herb.unknown.";

    // A mastered harvest checked in Validate, given in Apply once nothing refused it.
    private sealed record PendingExtra(uint PlantRef, Herb Herb);

    private readonly HashSet<PlayerId> _ready = [];
    private readonly Dictionary<PlayerId, HerbTier> _told = [];
    private readonly Dictionary<PlayerId, PendingExtra> _extra = [];
    private readonly HashSet<uint> _unlisted = [];
    private readonly Inventories _inventories = new();
    // One herb for each ingredient, by the ingredient's form id: what an unknown herb of it turns into, and when
    private readonly Dictionary<uint, Herb> _byIngredient = [];
    // The ingredient each unknown herb turns into, by what inventories name it: the SDK makes them a tick after Start
    private readonly Dictionary<NetworkFormId, uint> _ingredientByUnknown = [];
    private readonly HashSet<NetworkFormId> _carriedUnknown = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextCheck;

    private HerbalismConfig Settings => config.Herbalism;

    private void Start()
    {
        foreach (var herb in herbs.All)
            _byIngredient.TryAdd(JobItemResolver.ParseFormId(herb.Ingredient), herb);
        if (Settings.Enabled && Settings.UnknownHerbs)
            foreach (var ingredient in _byIngredient.Keys)
                customItems.Define(UnknownKey(ingredient),
                    CustomItem.CopyOf(new FormId(ingredient)).WithName(Settings.UnknownHerbName).WithValue(0));

        rpc.Ready += OnReady;
        serverEvents.OnPlayerDisconnected += OnDisconnected;
        logger.LogInformation("Jobs: herbalism {State}, {Herbs} herbs; recognised {Below} tiers below, mastered {Above} above for {Extra} more.",
            Settings.Enabled ? "on" : "off", herbs.All.Count, Settings.RecognisedTiersBelow, Settings.MasteredTiersAbove, Settings.ExtraYield);
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
    }

    private void Update()
    {
        if (!Settings.Enabled || _clock.Elapsed.TotalSeconds < _nextCheck)
            return;
        _nextCheck = _clock.Elapsed.TotalSeconds + Settings.AlchemyCheckSeconds;

        foreach (var player in _ready)
            TellAlchemy(player);
    }

    private void OnReady(PlayerId player)
    {
        if (!Settings.Enabled)
            return;

        var list = herbs.All.ToList();
        rpc.SendHerbRules(player,
            string.Join(ListSeparator, list.Select(herb => JobItemResolver.ParseFormId(herb.Plant).ToString("X8", CultureInfo.InvariantCulture))),
            string.Join(ListSeparator, list.Select(herb => herb.Name)),
            string.Join(ListSeparator, list.Select(herb => ((int)herb.Tier).ToString(CultureInfo.InvariantCulture))),
            Settings.RecognisedTiersBelow, Settings.MasteredTiersAbove, Settings.Markers, Settings.MarkerDistance);
        _ready.Add(player);
        _told.Remove(player);
        TellAlchemy(player);
    }

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason)
    {
        _ready.Remove(scope.PlayerId);
        _told.Remove(scope.PlayerId);
        _extra.Remove(scope.PlayerId);
    }

    private void TellAlchemy(PlayerId player)
    {
        if (!TryReadAlchemy(player, out var tier))
            return;

        if (!_told.TryGetValue(player, out var told) || told != tier)
        {
            _told[player] = tier;
            rpc.SendHerbAlchemy(player, (byte)tier);
            logger.LogInformation("Jobs: {Player}'s Alchemy reaches {Tier} for herbs.", player, tier);
        }

        Recognise(player, tier);
    }

    // The unknown herbs the player carries whose plants their Alchemy reaches become the ingredient: those picked before
    // their Alchemy grew, and those that came from another player, the staff or a sale since the last check.
    private void Recognise(PlayerId player, HerbTier alchemy)
    {
        if (!Settings.UnknownHerbs || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;

        LearnUnknownHerbs();
        var inventory = character.As<Equipment>();
        _carriedUnknown.Clear();
        for (var i = 0; i < inventory.ItemsCount; i++)
        {
            var item = inventory.GetItems(i).FormId;
            if (_ingredientByUnknown.ContainsKey(item))
                _carriedUnknown.Add(item);
        }

        if (_carriedUnknown.Count == 0)
            return;

        var gains = new List<string>();
        foreach (var unknown in _carriedUnknown)
        {
            var ingredient = _ingredientByUnknown[unknown];
            var herb = _byIngredient[ingredient];
            if (HerbLore.Know(herb.Tier, alchemy, Settings.RecognisedTiersBelow, Settings.MasteredTiersAbove)
                    is HerbKnowledge.Unaware or HerbKnowledge.Recognised)
                continue;

            var count = _inventories.CountCarried(inventory, StackOf(unknown, 1));
            if (count == 0 || !_inventories.TryTake(inventory, StackOf(unknown, count)))
                continue;

            _inventories.Add(inventory, new InventoryEntry(new FormId(ingredient), count, 0));
            gains.Add($"{count} {herb.Name}");
        }

        if (gains.Count == 0)
            return;

        rpc.SendHerbGathered(player, string.Join(ListSeparator, gains), $"You recognise the herbs you carry: {string.Join(", ", gains)}.");
        logger.LogInformation("Jobs: {Player} ({Alchemy}) recognised {Gains}.", player, alchemy, string.Join(", ", gains));
    }

    private void LearnUnknownHerbs()
    {
        if (_ingredientByUnknown.Count == _byIngredient.Count)
            return;

        foreach (var ingredient in _byIngredient.Keys)
        {
            if (customItems.TryGet(UnknownKey(ingredient), out var unknown))
                _ingredientByUnknown.TryAdd(unknown, ingredient);
        }
    }

    // An ingredient has no wear and no charge
    private static ItemStackData StackOf(NetworkFormId item, int count) =>
        new() { BaseId = item, Count = count, Durability = -1, Charge = -1 };

    public static string UnknownKey(uint ingredient) => UnknownKeyPrefix + ingredient.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// A plant the picker does not know is often crushed instead of picked: the harvest is refused and the plant marked
    /// picked by the server, so their game finds nothing on it. One they have mastered gives more, in Apply.
    /// </summary>
    [EventHandler(Stage.Validate)]
    public void CheckHarvest(in ItemTransferEvent transfer, EventContext context)
    {
        if (transfer.Cause != ItemTransferCause.Harvest || !Settings.Enabled)
            return;

        var player = context.Origin;
        _extra.Remove(player);
        // A harvest names its plant by ref only: the base form comes from the plant the server keeps
        if (!TryFindPlant(transfer.From.RefId, out var plant))
            return; // the platform's own Apply refuses a plant it does not keep

        var baseForm = plant.BaseId.FormId.Value;
        if (!herbs.TryFind(baseForm, out var herb))
        {
            if (_unlisted.Add(baseForm))
                logger.LogWarning("Jobs: the plant {Plant:X8} is not in the herb list, so anyone may pick it.", baseForm);
            return;
        }

        // The platform's own check refuses a sender with no character; one whose skills are not read yet waits
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out _))
            return;
        if (!TryReadAlchemy(player, out var alchemy))
        {
            logger.LogWarning("Jobs: {Player} picked {Herb} before their Alchemy could be read; refused.", player, herb.Name);
            context.Cancel();
            rpc.SendHerbRefused(player, "Your skills can't be read yet. Try again in a moment.");
            return;
        }

        if (plant.IsHarvested)
            return; // picked a moment ago: the platform refuses it

        var knowledge = HerbLore.Know(herb.Tier, alchemy, Settings.RecognisedTiersBelow, Settings.MasteredTiersAbove);
        switch (Decide(knowledge, Random.Shared.NextDouble(), Settings))
        {
            case HerbHarvest.Picked:
                logger.LogInformation("Jobs: {Player} ({Alchemy}) picks {Herb} ({Tier}) as the game does.", player, alchemy, herb.Name, herb.Tier);
                return;
            case HerbHarvest.PickedUnknown:
                if (!customItems.TryGet(UnknownKey(JobItemResolver.ParseFormId(herb.Ingredient)), out var unknown)
                    || !entities.TryLookup<MainCharacter, PlayerId>(player, out var picker))
                {
                    logger.LogWarning("Jobs: no unknown herb for {Herb} yet; {Player} picks it as it is.", herb.Name, player);
                    return;
                }

                // As a crushed plant: the game finds nothing on it, and the server gives what was picked
                context.Cancel();
                plant.IsHarvested = true;
                plant.HarvestedBy = PlayerId.Server;
                _inventories.Add(picker.As<Equipment>(), new InventoryEntry(unknown, 1, 0));
                rpc.SendHerbGathered(player, $"1 {Settings.UnknownHerbName}", "You pick something, though you can't tell what it is.");
                logger.LogInformation("Jobs: {Player} ({Alchemy}) picked {Herb} ({Tier}) without knowing it.", player, alchemy, herb.Name, herb.Tier);
                return;
            case HerbHarvest.PickedMore:
                _extra[player] = new PendingExtra(transfer.From.RefId, herb);
                return;
            case HerbHarvest.Fumbled:
                context.Cancel();
                plant.IsHarvested = true;
                plant.HarvestedBy = PlayerId.Server;
                rpc.SendHerbRefused(player, DescribeFumble(knowledge));
                logger.LogInformation("Jobs: {Player} ({Alchemy}) crushed {Herb} ({Tier}).", player, alchemy, herb.Name, herb.Tier);
                return;
            default:
                throw new InvalidOperationException("Unknown harvest decision.");
        }
    }

    /// <summary>What a harvest comes to: roll is uniform in [0, 1).</summary>
    public static HerbHarvest Decide(HerbKnowledge knowledge, double roll, HerbalismConfig settings) => knowledge switch
    {
        HerbKnowledge.Unaware => roll < settings.UnawarePickChance ? PickedBlind(settings) : HerbHarvest.Fumbled,
        HerbKnowledge.Recognised => roll < settings.RecognisedPickChance ? PickedBlind(settings) : HerbHarvest.Fumbled,
        HerbKnowledge.Known => HerbHarvest.Picked,
        HerbKnowledge.Mastered => settings.ExtraYield > 0 ? HerbHarvest.PickedMore : HerbHarvest.Picked,
        _ => throw new ArgumentOutOfRangeException(nameof(knowledge), knowledge, "Unknown herb knowledge."),
    };

    /// <summary>A mastered harvest nothing refused: the extra comes on top of what the game gives.</summary>
    [EventHandler(Stage.Apply)]
    public void GiveExtra(in ItemTransferEvent transfer, EventContext context)
    {
        if (transfer.Cause != ItemTransferCause.Harvest)
            return;

        var player = context.Origin;
        if (!_extra.Remove(player, out var pending) || pending.PlantRef != transfer.From.RefId)
            return;
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;

        var ingredient = new FormId(JobItemResolver.ParseFormId(pending.Herb.Ingredient));
        _inventories.Add(character.As<Equipment>(), new InventoryEntry(ingredient, Settings.ExtraYield, 0));
        var gain = $"{Settings.ExtraYield} {pending.Herb.Name}";
        rpc.SendHerbGathered(player, gain, $"You know this plant well and find {gain} more.");
        logger.LogInformation("Jobs: {Player} found {Gain} more at {Plant:X8}.", player, gain, pending.PlantRef);
    }

    private static HerbHarvest PickedBlind(HerbalismConfig settings) =>
        settings.UnknownHerbs ? HerbHarvest.PickedUnknown : HerbHarvest.Picked;

    public static string DescribeFumble(HerbKnowledge knowledge) => knowledge switch
    {
        HerbKnowledge.Unaware => "You don't know what you're looking for, and the plant comes apart in your hands.",
        HerbKnowledge.Recognised => "Some kind of herb, but you can't tell which part to take, and you crush it.",
        HerbKnowledge.Known or HerbKnowledge.Mastered =>
            throw new ArgumentOutOfRangeException(nameof(knowledge), knowledge, "A plant the picker knows is never crushed."),
        _ => throw new ArgumentOutOfRangeException(nameof(knowledge), knowledge, "Unknown herb knowledge."),
    };

    private bool TryReadAlchemy(PlayerId player, out HerbTier tier)
    {
        tier = HerbTier.Novice;
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character)
            || !character.As<SkillAttributes>().TryGetEffective((CharacterAttribute)(byte)Stat.Alchemy, out var value))
            return false;

        tier = HerbLore.TierOf(value);
        return true;
    }

    private bool TryFindPlant(uint plantRef, out Flora plant)
    {
        foreach (var candidate in entities.Query<Flora>())
        {
            if (candidate.RefId == plantRef)
            {
                plant = candidate;
                return true;
            }
        }

        plant = default;
        return false;
    }
}
