using CyrodiilRP.Server.Zones;

namespace CyrodiilRP.Server;

internal sealed class ZoneSettings
{
    public bool Enabled { get; set; } = true;
    public List<ZoneDefinition> Entries { get; set; } = [];
    public float TakenPerLevel { get; set; } = 0.08f;
    public float MaxTaken { get; set; } = 3f;
    public float DealtPerLevel { get; set; } = 0.05f;
    public float MinDealt { get; set; } = 0.35f;
    public double EliteHealth { get; set; } = 3;
    public double EliteDamage { get; set; } = 1.5;
    public double BossHealth { get; set; } = 8;
    public double BossDamage { get; set; } = 2;
    public double GroupHealthPerPlayer { get; set; } = 0.6;

    public void RequireValid()
    {
        if (!float.IsFinite(TakenPerLevel) || TakenPerLevel < 0
            || !float.IsFinite(MaxTaken) || MaxTaken < 1
            || !float.IsFinite(DealtPerLevel) || DealtPerLevel < 0
            || !float.IsFinite(MinDealt) || MinDealt is < 0 or > 1
            || new[] { EliteHealth, EliteDamage, BossHealth, BossDamage }.Any(x => !double.IsFinite(x) || x <= 0)
            || !double.IsFinite(GroupHealthPerPlayer) || GroupHealthPerPlayer < 0)
            throw new InvalidOperationException("Zone damage multipliers are outside their allowed ranges.");

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var zone in Entries)
        {
            if (string.IsNullOrWhiteSpace(zone.Id) || !identifiers.Add(zone.Id)
                || !Enum.IsDefined(zone.Mode) || !HasValidLevels(zone)
                || !double.IsFinite(zone.NpcHealth) || zone.NpcHealth <= 0 || !double.IsFinite(zone.NpcDamage) || zone.NpcDamage <= 0
                || zone.Origin.Length != ZoneMap.Coordinates || !double.IsFinite(zone.Radius) || zone.Radius <= 0
                || !zone.Name.TryGetValue("en", out var english) || string.IsNullOrWhiteSpace(english)
                || !zone.Name.TryGetValue("pl", out var polish) || string.IsNullOrWhiteSpace(polish)
                || zone.Areas.Any(string.IsNullOrWhiteSpace)
                || zone.Interiors.Any(cell => !ZoneMap.TryParseFormId(cell, out _))
                // Interiors have no grid position, so a gradient cannot place them.
                || (zone.Interiors.Count > 0 && zone.Mode == ZoneMode.Gradient)
                || zone.Shapes.Any(shape => shape.Length < ZoneMap.FewestPoints || shape.Any(point => point.Length != ZoneMap.Coordinates)))
                throw new InvalidOperationException($"Zone '{zone.Id}' has an invalid id, name, mode, level range or shape.");
        }
    }

    private static bool HasValidLevels(ZoneDefinition zone) => zone.Mode switch
    {
        ZoneMode.Fixed or ZoneMode.Gradient => zone.MinLevel >= ZoneLevels.Minimum && zone.MaxLevel >= zone.MinLevel && zone.MaxLevel <= ushort.MaxValue,
        ZoneMode.ScalesAbove => zone.MinLevel is >= ZoneLevels.Minimum and <= ushort.MaxValue,
        ZoneMode.PartyLowest => true,
    };
}
