using System.Diagnostics;
using System.Numerics;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Props;
using OblivionMP.SDK.Common.Statuses;
using OblivionMP.SDK.Serverside.Props;
using OblivionMP.SDK.Serverside.Statuses;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using ReadyM.SDK.Server.Events;

namespace Roleplay.Jobs.Server;

/// <summary>
/// The authority on carrying. A carryable lying in the world is a prop placed for everyone (<see cref="IProps"/>) and
/// kept in <see cref="CarryableStore"/>. The carry key picks up the one in front of the player, which takes it out of
/// the world, weighs its kind's weight on them as a Burden and tells everyone who carries what; pressed again, it puts
/// it down in front of them. A carrier who dies or leaves puts it down where they stood.
/// </summary>
[Service]
public sealed partial class CarryService(
    IEntities entities,
    IServerEvents serverEvents,
    IProps props,
    IStatuses statuses,
    CarryRpcHandlers rpc,
    CarryableStore store,
    JobsConfig config,
    ILogger logger)
{
    private sealed record Carrier(CarryItem Kind, int KindIndex, bool Cargo, IReadOnlyList<StatusHandle> Statuses, Vector3 LastPosition,
        float LastYaw);

    private const string IdPrefix = "jobs/carry/";
    private const double WatchEverySeconds = 0.25;

    private readonly HashSet<PlayerId> _ready = [];
    private readonly Dictionary<string, Carryable> _lying = [];
    private readonly Dictionary<PlayerId, Carrier> _carriers = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _nextWatch;
    private IReadOnlyList<CarryItem> _items = [];
    private readonly List<Func<Carryable, string?>> _pickUpRules = [];

    /// <summary>A player put something down, by the carry key, dying or leaving: what now lies there.</summary>
    public event Action<PlayerId, Carryable>? PutDownBy;

    /// <summary>Everything lying in the world.</summary>
    public IReadOnlyCollection<Carryable> Lying => _lying.Values;

    /// <summary>A rule that can refuse picking up a carryable: the sentence why, or null to allow it.</summary>
    public void AddPickUpRule(Func<Carryable, string?> rule) => _pickUpRules.Add(rule);

    /// <summary>Lays a crate of the kind from an inlet: cargo, which pays when delivered.</summary>
    public Carryable LayCargo(string kindName, Vector3 at, float yaw)
    {
        if (KindOf(kindName) is null)
            throw new InvalidOperationException($"Jobs: no carry kind {kindName} to lay as cargo.");
        var carryable = new Carryable(NewId(), kindName, at.X, at.Y, at.Z, yaw, Cargo: true);
        store.Add(carryable);
        Lay(carryable);
        return carryable;
    }

    /// <summary>Takes a carryable lying in the world away for good.</summary>
    public void Clear(string id)
    {
        if (!_lying.Remove(id))
            return;
        store.Remove(id);
        props.Remove(id);
    }

    private CarryConfig Settings => config.Carrying;

    private void Start()
    {
        _items = CarryItem.AllOf(Settings);
        foreach (var carryable in store.Lying)
        {
            if (KindOf(carryable.Kind) is null)
            {
                logger.LogWarning("Jobs: carryable {Id} is a {Kind}, which Carrying.Kinds does not list: left in the save file, not placed.",
                    carryable.Id, carryable.Kind);
                continue;
            }
            Lay(carryable);
        }

        rpc.Ready += OnReady;
        rpc.KeyPressed += OnKey;
        rpc.PutDownRequested += OnPutDown;
        rpc.PlaceRequested += OnPlace;
        serverEvents.OnPlayerDisconnected += OnDisconnected;

        logger.LogInformation("Jobs: carrying running. {Lying} carryables lying, {Kinds} kinds in {Categories} categories, key {Key}; placement {Placement}.",
            _lying.Count, _items.Count, Settings.Categories.Count, Settings.CarryKey, Settings.AllowPlacement ? "on" : "off");
    }

    private void Stop()
    {
        rpc.Ready -= OnReady;
        rpc.KeyPressed -= OnKey;
        rpc.PutDownRequested -= OnPutDown;
        rpc.PlaceRequested -= OnPlace;
        serverEvents.OnPlayerDisconnected -= OnDisconnected;
    }

    // Keeps where each carrier stands, for putting it down when they leave, and puts it down when they die
    private void Update()
    {
        var now = _clock.Elapsed.TotalSeconds;
        if (now < _nextWatch)
            return;
        _nextWatch = now + WatchEverySeconds;

        foreach (var (player, carrier) in _carriers.ToList())
        {
            if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
                continue; // between areas or leaving: the last place stands
            if (character.IsDead)
            {
                PutDown(player, AtFeet(character.Position, character.Rotation), character.Rotation, "died");
                continue;
            }
            _carriers[player] = carrier with { LastPosition = character.Position, LastYaw = character.Rotation };
        }
    }

    private void OnReady(PlayerId player)
    {
        _ready.Add(player);
        rpc.SendCarryRules(player, Settings.CarryKey, Settings.AllowPlacement, Settings.HoldAnimation, Settings.HoldAnimationFirstPerson,
            Settings.MovingAbove, Settings.PutDownAhead, Settings.CarryHeight, Settings.MaxDrop);
        for (var index = 0; index < _items.Count; index++)
        {
            var kind = _items[index];
            var standing = kind.Standing;
            var moving = kind.Moving;
            rpc.SendCarryKind(player, index, kind.Name, kind.Asset, kind.Scale, Settings.Socket,
                standing.OffsetX, standing.OffsetY, standing.OffsetZ, standing.Pitch, standing.Yaw, standing.Roll,
                moving.OffsetX, moving.OffsetY, moving.OffsetZ, moving.Pitch, moving.Yaw, moving.Roll);
        }
        foreach (var (carrier, carried) in _carriers)
            rpc.SendCarrying(player, carrier.RawValue, carried.KindIndex);
    }

    private void OnKey(PlayerId player)
    {
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;

        // A client that thinks it holds nothing while the server says it does: down at its feet
        if (_carriers.ContainsKey(player))
        {
            PutDown(player, AtFeet(character.Position, character.Rotation), character.Rotation, "put it down");
            return;
        }
        if (character.IsDead)
            return;

        // What a rule refuses (a crate under another) is passed over for the next nearest
        var allowed = _lying.Values.Where(candidate => RefusalOf(candidate) is null);
        if (CarryReach.Nearest(allowed, character.Position, character.Rotation, Settings.Reach) is { } carryable)
        {
            PickUp(player, carryable);
            return;
        }
        var refused = CarryReach.Nearest(_lying.Values, character.Position, character.Rotation, Settings.Reach);
        rpc.SendCarryRefused(player, refused is null ? "There is nothing here to pick up." : RefusalOf(refused) ?? "There is nothing here to pick up.");
    }

    private string? RefusalOf(Carryable carryable)
    {
        foreach (var rule in _pickUpRules)
        {
            if (rule(carryable) is { } refusal)
                return refusal;
        }
        return null;
    }

    private void PickUp(PlayerId player, Carryable carryable)
    {
        var index = _items.ToList().FindIndex(kind => kind.Name == carryable.Kind);
        if (index < 0)
            throw new InvalidOperationException($"Jobs: carryable {carryable.Id} lies as a {carryable.Kind}, which Carrying.Kinds does not list.");
        var kind = _items[index];

        _lying.Remove(carryable.Id);
        store.Remove(carryable.Id);
        props.Remove(carryable.Id);

        _carriers[player] = new Carrier(kind, index, carryable.Cargo, ApplyWeight(player, kind), carryable.Position, carryable.Yaw);
        foreach (var ready in _ready)
            rpc.SendCarrying(ready, player.RawValue, index);
        logger.LogInformation("Jobs: {Player} picked up {Id}, a {Kind}.", player, carryable.Id, kind.Name);
    }

    private void OnPutDown(PlayerId player, Vector3 at, float yaw)
    {
        if (!_carriers.ContainsKey(player) || !entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;
        if (!WithinReach(character.Position, at))
        {
            logger.LogWarning("Jobs: {Player} at {Standing} asked to put down at {At}, out of reach.", player, character.Position, at);
            rpc.SendCarryRefused(player, "That is too far to put it down.");
            return;
        }
        PutDown(player, at, yaw, "put it down");
    }

    private void PutDown(PlayerId player, Vector3 at, float yaw, string why)
    {
        if (!_carriers.Remove(player, out var carrier))
            return;

        foreach (var status in carrier.Statuses)
            statuses.Remove(player, status);
        var carryable = new Carryable(NewId(), carrier.Kind.Name, at.X, at.Y, at.Z, yaw, carrier.Cargo);
        store.Add(carryable);
        Lay(carryable);
        foreach (var ready in _ready)
            rpc.SendCarryStopped(ready, player.RawValue);
        logger.LogInformation("Jobs: {Player} {Why}: {Id}, a {Kind}, lies at {At}.", player, why, carryable.Id, carrier.Kind.Name, at);
        PutDownBy?.Invoke(player, carryable);
    }

    private void OnPlace(PlayerId player, int kindIndex, Vector3 at, float yaw)
    {
        if (!Settings.AllowPlacement)
        {
            logger.LogWarning("Jobs: {Player} tried to place a carryable, which this server does not allow.", player);
            rpc.SendCarryRefused(player, "This server does not let you place things to carry.");
            return;
        }
        if (_items.Count == 0)
        {
            rpc.SendCarryRefused(player, "There are no kinds of things to carry.");
            return;
        }
        if (!entities.TryLookup<MainCharacter, PlayerId>(player, out var character))
            return;

        if (!WithinReach(character.Position, at))
        {
            logger.LogWarning("Jobs: {Player} at {Standing} asked to place at {At}, out of reach.", player, character.Position, at);
            rpc.SendCarryRefused(player, "That is too far to place it.");
            return;
        }

        var kind = _items[((kindIndex % _items.Count) + _items.Count) % _items.Count];
        var carryable = new Carryable(NewId(), kind.Name, at.X, at.Y, at.Z, yaw);
        store.Add(carryable);
        Lay(carryable);
        logger.LogInformation("Jobs: {Player} placed {Id}, a {Kind}, at {At}.", player, carryable.Id, kind.Name, at);
    }

    private void OnDisconnected(Player scope, Guid _, DisconnectReason reason)
    {
        PlayerId player = scope.PlayerId;
        _ready.Remove(player);
        if (_carriers.TryGetValue(player, out var carrier))
            PutDown(player, AtFeet(carrier.LastPosition, carrier.LastYaw), carrier.LastYaw, "left");
    }

    // In front of a character, at their feet: where a carrier who cannot say where it rests puts it down
    private Vector3 AtFeet(Vector3 standing, float yaw)
        => CarryReach.Ahead(standing, yaw, Settings.PutDownAhead) with { Z = standing.Z - Settings.FeetBelowPosition };

    // A client's spot is where it found the thing comes to rest: in front of the player, at most a fall below
    private bool WithinReach(Vector3 standing, Vector3 at)
        => CarryReach.IsWithin(standing, at, Settings.PutDownAhead + Settings.Reach, Settings.CarryHeight + Settings.MaxDrop);

    private void Lay(Carryable carryable)
    {
        var kind = KindOf(carryable.Kind) ?? throw new InvalidOperationException($"Jobs: no carry kind {carryable.Kind}.");
        _lying[carryable.Id] = carryable;
        props.Place(carryable.Id, new Prop(kind.Asset, carryable.Position, new Vector3(0f, carryable.Yaw, 0f), kind.Scale));
    }

    // The Burden of its weight, and the Speed it drains
    private List<StatusHandle> ApplyWeight(PlayerId player, CarryItem kind)
    {
        var name = $"Carrying a {kind.Name.ToLowerInvariant()}";
        var applied = new List<StatusHandle>();
        if (kind.Weight > 0)
            applied.Add(statuses.Apply(player, StatusEffect.Custom(BurdenCode, kind.Weight).WithName(name)));
        if (kind.DrainSpeed > 0)
            applied.Add(statuses.Apply(player, StatusEffect.Of(StatusKind.Drain, Stat.Speed, kind.DrainSpeed).WithName(name)));
        return applied;
    }

    private CarryItem? KindOf(string name) => _items.FirstOrDefault(kind => kind.Name == name);

    private static string NewId() => IdPrefix + Guid.NewGuid().ToString("N");

    // The game's Burden effect: adds its magnitude to what the character carries
    private const string BurdenCode = "BRDN";
}
