using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Props;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;
using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>
/// The placement mode, for setting up a server: what to place is picked from categories (the jobs' foremen and crate
/// deposits with docks setup allowed, the ore nodes with ore placement allowed) and previewed a fixed distance in
/// front of the player, turned to face them. On the numpad: Decimal turns the mode on and off, + and - change the
/// category, / and * the item in it, 4 and 6 turn it, 8 and 2 tilt it (ores), 7 and 9 lower and raise it, 5 places
/// it, 0 removes the nearest placed one of the category. Ores go exactly where the preview is, through anything; a
/// deposit settles where a crate would come to rest there; a foreman stands there.
/// </summary>
[Service]
public sealed partial class PlacementClient(
    IInput input,
    IPlayers players,
    ILocalProps localProps,
    DocksClient docks,
    DocksClientRpc docksRpc,
    MiningClientRpc miningRpc,
    JobsPanel panel,
    ILogger logger)
{
    private const float Ahead = 250f;
    private const float TurnDegrees = 15f;
    private const float TiltDegrees = 15f;
    private const float RaiseCentimeters = 10f;
    // A deposit is found from chest height and may drop this far to the floor
    private const float CarryHeight = 40f;
    private const float MaxDrop = 400f;
    private const double FindFloorEverySeconds = 0.1;

    private readonly List<IDisposable> _binds = [];
    private readonly List<LocalPropHandle> _preview = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IReadOnlyList<PlacementCategory> _categories = [];
    private DocksRules? _docksRules;
    private MiningRules? _miningRules;
    private PlacementItem? _previewOf;
    private Vector3? _floor;
    private double _nextFloor;
    private int _category;
    private int _item;
    private bool _active;
    private float _turn;
    private float _tilt;
    private float _raise;

    private void Start()
    {
        docksRpc.RulesReceived += OnDocksRules;
        miningRpc.RulesReceived += OnMiningRules;
        Bind(Key.DECIMAL, Toggle);
        Bind(Key.ADD, () => ChangeCategory(1));
        Bind(Key.SUBTRACT, () => ChangeCategory(-1));
        Bind(Key.MULTIPLY, () => ChangeItem(1));
        Bind(Key.DIVIDE, () => ChangeItem(-1));
        Bind(Key.NUM_FOUR, () => _turn -= TurnDegrees);
        Bind(Key.NUM_SIX, () => _turn += TurnDegrees);
        Bind(Key.NUM_EIGHT, () => _tilt += TiltDegrees);
        Bind(Key.NUM_TWO, () => _tilt -= TiltDegrees);
        Bind(Key.NUM_SEVEN, () => _raise -= RaiseCentimeters);
        Bind(Key.NUM_NINE, () => _raise += RaiseCentimeters);
        Bind(Key.NUM_FIVE, Place);
        Bind(Key.NUM_ZERO, RemoveNearest);
    }

    private void Stop()
    {
        docksRpc.RulesReceived -= OnDocksRules;
        miningRpc.RulesReceived -= OnMiningRules;
        foreach (var bind in _binds)
            bind.Dispose();
        _binds.Clear();
        ClearPreview();
    }

    // Toggle works whenever there is something to place, the rest only in the mode
    private void Bind(Key key, Action action)
        => _binds.Add(input.RegisterKeyBind(key, () =>
        {
            if (!input.CanApplyInput() || _categories.Count == 0 || (!_active && key != Key.DECIMAL))
                return;
            action();
        }, KeySource.Polled));

    private void OnDocksRules(DocksRules rules)
    {
        _docksRules = rules;
        BuildCategories();
    }

    private void OnMiningRules(MiningRules rules)
    {
        _miningRules = rules;
        BuildCategories();
    }

    private void BuildCategories()
    {
        var categories = new List<PlacementCategory>();
        if (_docksRules is { SetupAllowed: true } docksRules)
        {
            var items = docksRules.RoleNames.Select((name, index) => new PlacementItem(name, PlacementKind.Foreman, index)).ToList();
            items.Add(new PlacementItem("Crate inlet", PlacementKind.Inlet, 0));
            items.Add(new PlacementItem("Crate outlet", PlacementKind.Outlet, 0));
            categories.Add(new PlacementCategory("Jobs", items));
        }
        if (_miningRules is { PlacementAllowed: true, KindNames.Count: > 0 } miningRules)
            categories.Add(new PlacementCategory("Ores",
                miningRules.KindNames.Select((name, index) => new PlacementItem(name, PlacementKind.Ore, index)).ToList()));

        _categories = categories;
        _category = categories.Count == 0 ? 0 : Math.Clamp(_category, 0, categories.Count - 1);
        _item = categories.Count == 0 ? 0 : Math.Clamp(_item, 0, categories[_category].Items.Count - 1);
        ClearPreview();
        if (categories.Count == 0)
            _active = false;
    }

    private PlacementItem? Current => _categories.Count == 0 ? null : _categories[_category].Items[_item];

    private void Toggle()
    {
        _active = !_active;
        _floor = null;
        ClearPreview();
        if (!_active)
        {
            panel.Say(PanelLineKind.Note, "Placement off.");
            return;
        }
        panel.Say(PanelLineKind.Note, "Placement on. Numpad: + and - category, / and * item, 4 and 6 turn, 8 and 2 tilt, 7 and 9 height, 5 places, 0 removes.");
        SayCurrent();
    }

    private void ChangeCategory(int step)
    {
        _category = Wrap(_category + step, _categories.Count);
        _item = 0;
        SayCurrent();
    }

    private void ChangeItem(int step)
    {
        _item = Wrap(_item + step, _categories[_category].Items.Count);
        SayCurrent();
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    private void SayCurrent()
    {
        if (Current is { } item)
            panel.Say(PanelLineKind.Note, $"Placing {_categories[_category].Name}: {item.Name}.");
    }

    private void Update()
    {
        if (!_active || Current is not { } item)
            return;
        if (players.LocalMainCharacter is not { } local)
        {
            _active = false;
            ClearPreview();
            return;
        }
        ShowPreview(item, local.Position, local.Rotation);
    }

    // Straight ahead of the player, at the height they chose
    private Vector3 SpotAhead(Vector3 standing, float facing)
    {
        var radians = facing * MathF.PI / 180f;
        return standing + new Vector3(MathF.Cos(radians) * Ahead, MathF.Sin(radians) * Ahead, _raise);
    }

    // Turned to face the player, then by what they chose
    private float YawFacing(float facing) => facing + 180f + _turn;

    private void ShowPreview(PlacementItem item, Vector3 standing, float facing)
    {
        var spot = SpotAhead(standing, facing);
        var yaw = YawFacing(facing);
        switch (item.Kind)
        {
            case PlacementKind.Ore:
                if (_miningRules is not { } rules || item.Index >= rules.KindAssets.Count || item.Index >= rules.KindScales.Count)
                    return;
                var ore = new Prop(rules.KindAssets[item.Index], spot, new Vector3(_tilt, yaw, 0), rules.KindScales[item.Index]);
                if (_previewOf != item)
                {
                    ClearPreview();
                    _preview.Add(localProps.Place(ore, collides: false));
                    _previewOf = item;
                }
                localProps.Move(_preview[0], ore.Position, ore.Rotation);
                return;
            case PlacementKind.Inlet:
            case PlacementKind.Outlet:
                if (_clock.Elapsed.TotalSeconds >= _nextFloor)
                {
                    _nextFloor = _clock.Elapsed.TotalSeconds + FindFloorEverySeconds;
                    _floor = FindFloor(standing, facing, yaw, out _);
                }
                if (_floor is not { } floor)
                {
                    // No floor there: nothing to show until there is
                    ClearPreview();
                    return;
                }
                var boards = docks.PalletProps(floor, yaw);
                if (_previewOf != item || _preview.Count != boards.Count)
                {
                    ClearPreview();
                    _preview.AddRange(boards.Select(board => localProps.Place(board, collides: false)));
                    _previewOf = item;
                }
                for (var index = 0; index < boards.Count; index++)
                    localProps.Move(_preview[index], boards[index].Position, boards[index].Rotation);
                return;
            case PlacementKind.Foreman:
                // A person has no mesh to show: they stand at the spot
                ClearPreview();
                _previewOf = item;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Not a placement kind.");
        }
    }

    // Where a crate would come to rest in front of the player: a deposit's floor
    private Vector3? FindFloor(Vector3 standing, float facing, float yaw, out PropRest found)
    {
        if (_docksRules is not { } rules)
        {
            found = PropRest.Unknown;
            return null;
        }
        var radians = facing * MathF.PI / 180f;
        var at = standing + new Vector3(MathF.Cos(radians) * Ahead, MathF.Sin(radians) * Ahead, CarryHeight);
        found = localProps.FindRest(new Prop(rules.CrateAsset, at, new Vector3(0, yaw, 0), rules.CrateScale), at, MaxDrop, out var floor);
        return found == PropRest.Rests ? floor : null;
    }

    private void Place()
    {
        if (Current is not { } item || players.LocalMainCharacter is not { } local)
            return;
        var spot = SpotAhead(local.Position, local.Rotation);
        var yaw = YawFacing(local.Rotation);
        switch (item.Kind)
        {
            case PlacementKind.Ore:
                miningRpc.SendPlaceNode(item.Index, spot.X, spot.Y, spot.Z, yaw, _tilt);
                return;
            case PlacementKind.Foreman:
                docksRpc.SendPlaceForeman(item.Index, spot.X, spot.Y, spot.Z, yaw);
                return;
            case PlacementKind.Inlet:
            case PlacementKind.Outlet:
                PlaceZone(item.Kind == PlacementKind.Inlet ? DockZoneKind.Inlet : DockZoneKind.Outlet, local.Position, local.Rotation, yaw);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Not a placement kind.");
        }
    }

    private void PlaceZone(DockZoneKind kind, Vector3 standing, float facing, float yaw)
    {
        var floor = FindFloor(standing, facing, yaw, out var found);
        switch (found)
        {
            case PropRest.Rests when floor is { } at:
                docksRpc.SendPlaceZone((byte)kind, at.X, at.Y, at.Z, yaw);
                return;
            case PropRest.Rests:
                throw new InvalidOperationException("A crate came to rest with no floor.");
            case PropRest.Blocked:
                panel.Say(PanelLineKind.Refusal, "Something is in the way of a deposit there.");
                return;
            case PropRest.NoFloor:
                panel.Say(PanelLineKind.Refusal, "There is no floor there for a deposit.");
                return;
            case PropRest.Unknown:
                logger.LogWarning("Jobs: could not find the floor for a deposit with {Asset}.", _docksRules?.CrateAsset);
                panel.Say(PanelLineKind.Refusal, "A deposit cannot be placed here.");
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), found, "Not a prop rest.");
        }
    }

    private void RemoveNearest()
    {
        if (Current is not { } item)
            return;
        switch (item.Kind)
        {
            case PlacementKind.Ore:
                miningRpc.SendRemoveNode();
                return;
            case PlacementKind.Foreman:
            case PlacementKind.Inlet:
            case PlacementKind.Outlet:
                docksRpc.SendRemoveNearest();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Not a placement kind.");
        }
    }

    private void ClearPreview()
    {
        foreach (var prop in _preview)
            localProps.Remove(prop);
        _preview.Clear();
        _previewOf = null;
    }
}
