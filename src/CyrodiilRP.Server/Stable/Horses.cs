using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Stable;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Events;
using OblivionMP.SDK.Serverside;
using OblivionMP.SDK.Serverside.Spawners;
using OblivionMP.SDK.Serverside.World;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Stable;

// The horse each character owns, and the one standing in the world for each player online: called beside them from the
// G wheel, ridden by them alone, taken away as they leave. A horse is a person of its cell as a trader is, so the cell's
// save would bring it back after a restart: the sweep takes away every one of ours no player online has called.
[Service]
internal sealed partial class Horses(
    IEntities entities,
    WorldRefs refs,
    ServerRefIds refIds,
    OnlinePlayers online,
    SelfMenu self,
    Persistence persistence,
    RpChat chat,
    Timers timers,
    Fire fire,
    ILogger logger)
{
    private const string CallOption = "self.horse";
    private const int CallOrder = 30;
    private const int Level = 1;
    private const string LoadJob = "horses.load";
    private const string SweepJob = "horses.sweep";

    private readonly Dictionary<OnlinePlayer, Horse> _horses = [];

    private readonly List<Npc> _strays = [];

    private void Start()
    {
        online.Loaded += OnLoaded;
        online.Left += OnLeft;
        self.Add(new SelfOption { Id = CallOption, Label = StableText.OptionCall, Icon = WheelIcons.Horse, Order = CallOrder, State = CheckCall, Run = Call });
        timers.RunEvery(SweepJob, StableRules.SweepEvery, Sweep);
    }

    // Null when the player's character owns none, or its horse is not known yet.
    public Horse? FindOwned(OnlinePlayer player) =>
        _horses.TryGetValue(player, out var horse) && player.Character?.Id == horse.CharacterId ? horse : null;

    // On the writer, so a read and a purchase come back in the order they were asked. The horse standing for the player
    // stays theirs while it is the one they own.
    public async Task RefreshAsync(OnlinePlayer player, long characterId)
    {
        var row = await persistence.Writer.EnqueueAsync(LoadJob, (db, ct) => StableBook.FindAsync(db, characterId, ct));
        if (row is null || !player.IsLive || player.Character?.Id != characterId)
            return;

        var called = FindOwned(player) is { } known && known.Kind == row.Horse ? known.CalledRefId : 0;
        _horses[player] = new Horse(characterId, row.Horse, called);
    }

    // A purchase that was written: the horse it replaced leaves the world.
    public void Own(OnlinePlayer player, long characterId, HorseKind kind)
    {
        if (_horses.Remove(player, out var before))
            TakeAway(before);

        _horses[player] = new Horse(characterId, kind, CalledRefId: 0);
    }

    // Only its owner, and no person of the game, rides a horse of ours. Other horses are the platform's own business.
    [EventHandler(Stage.Validate)]
    public void CheckMount(in HorseMountRequestedEvent mount, EventContext context)
    {
        var horse = mount.Horse;
        if (horse.SpawnerRefId != StableRules.HorseSpawnerRefId)
            return;

        if (!mount.Rider.TryAs(out MainCharacter rider) || !IsCalledBy(horse.RefId, rider.PlayerId))
            context.Cancel(HorseMountReject.Occupied);
    }

    [EventHandler(Stage.Rejected)]
    public void OnMountRefused(in HorseMountRequestedEvent mount, HorseMountReject reason, EventContext context)
    {
        if (mount.Horse.SpawnerRefId == StableRules.HorseSpawnerRefId && online.TryGet(context.Origin, out var rider))
            chat.Notify(rider, StableText.NotYours);
    }

    private bool IsCalledBy(uint horseRefId, PlayerId player) =>
        _horses.Any(pair => pair.Key.Id == player && pair.Value.CalledRefId == horseRefId);

    private void OnLoaded(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        var characterId = character.Id;
        fire.Run(LoadJob, async () =>
        {
            try
            {
                await RefreshAsync(player, characterId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The horse of {Character} could not be read.", CharacterRecord.FormatRef(characterId));
            }
        });
    }

    private void OnLeft(OnlinePlayer player)
    {
        if (_horses.Remove(player, out var horse))
            TakeAway(horse);
    }

    private OptionState CheckCall(OnlinePlayer player) =>
        FindOwned(player) is { } horse ? OptionState.OpenUnless(FindSpot(player, horse, out _)) : OptionState.Hidden;

    // One horse in the world for a player: the one called before, dead or alive, goes.
    private void Call(OnlinePlayer player)
    {
        if (FindOwned(player) is not { } horse)
        {
            chat.Notify(player, StableText.CallNoHorse);
            return;
        }

        if (FindSpot(player, horse, out var spot) is { } refusal)
        {
            chat.Notify(player, refusal);
            return;
        }

        TakeAway(horse);
        var npc = entities.Create<Npc>(spot.Cell, spot.Cell.MasterClient);
        npc.RefId = refIds.Next();
        npc.SpawnerRefId = StableRules.HorseSpawnerRefId;
        npc.BaseId = new FormId(StableRules.FindBase(horse.Kind));
        npc.Position = StableRules.FindSpotBeside(spot.Position, spot.Yaw);
        npc.Rotation = spot.Yaw;
        npc.Level = Level;
        _horses[player] = horse with { CalledRefId = npc.RefId };
        chat.Notify(player, StableText.Called(StableRules.FindName(horse.Kind)));
    }

    // Null when the horse can come. After a far teleport the game can leave the character's cell behind for a while, and a
    // horse made in that cell would never get a body.
    private Phrase? FindSpot(OnlinePlayer player, Horse horse, out CallSpot spot)
    {
        spot = default;
        if (!entities.TryLookup(player.Id, out MainCharacter main) || !entities.TryGetScope(main, out Area area))
            return StableText.CallNotInWorld;

        if (!StableRules.IsOpenWorld(area.AreaId))
            return StableText.CallNotOutside;

        if (IsRiding(player, horse))
            return StableText.CallRiding;

        var parent = main.As<ParentCell>();
        var position = main.Position;
        if (parent.Kind != ParentCellKind.Exterior
            || OblivionCellIds.Exterior(parent.X, parent.Y) != GameSpace.ToExteriorCell(position)
            || !entities.TryLookup(parent.ToFullCellId(area.AreaId), out Cell cell)
            || cell.MasterClient == PlayerId.Invalid)
            return StableText.CallNotSettled;

        spot = new CallSpot(cell, position, main.Rotation);
        return null;
    }

    private bool IsRiding(OnlinePlayer player, Horse horse) =>
        TryFindCalled(horse, out var npc) && npc.As<RiddenBy>().Rider == player.Id;

    private bool TryFindCalled(Horse horse, out Npc npc)
    {
        npc = default;
        return horse.CalledRefId != 0 && refs.TryFindNpc(horse.CalledRefId, out npc) && npc.SpawnerRefId == StableRules.HorseSpawnerRefId;
    }

    private void TakeAway(Horse horse)
    {
        if (TryFindCalled(horse, out var npc))
            entities.Delete(npc);
    }

    private void Sweep()
    {
        foreach (var npc in entities.Query<Npc>())
        {
            if (npc.SpawnerRefId == StableRules.HorseSpawnerRefId && !IsCalled(npc.RefId))
                _strays.Add(npc);
        }

        if (_strays.Count == 0)
            return;

        foreach (var npc in _strays)
            entities.Delete(npc);

        logger.LogInformation("[CyrodiilRP] Took away {Count} horse(s) no player online called.", _strays.Count);
        _strays.Clear();
    }

    private bool IsCalled(uint refId) => _horses.Values.Any(horse => horse.CalledRefId == refId);

    // Where the player stands now: the horse is made in their cell, to their side.
    private readonly record struct CallSpot(Cell Cell, Vector3 Position, float Yaw);
}
