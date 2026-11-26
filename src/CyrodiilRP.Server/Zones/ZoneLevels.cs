namespace CyrodiilRP.Server.Zones;

internal static class ZoneLevels
{
    public const int Minimum = 1;
    private const uint HorizontalMix = 73856093;
    private const uint VerticalMix = 19349663;

    public static int ForCell(ZoneDefinition zone, ZoneCell cell, int partyLowest) => zone.Mode switch
    {
        ZoneMode.Fixed => zone.MinLevel + (int)(unchecked((uint)cell.X * HorizontalMix ^ (uint)cell.Y * VerticalMix)
            % (uint)(zone.MaxLevel - zone.MinLevel + 1)),
        ZoneMode.ScalesAbove => Math.Max(zone.MinLevel, Math.Max(Minimum, partyLowest)),
        ZoneMode.PartyLowest => Math.Max(Minimum, partyLowest),
    };

    public static int Lowest<T>(IEnumerable<T> occupants, Func<T, IEnumerable<T>> partyMembers, Func<T, int?> onlineLevel) where T : notnull =>
        occupants.SelectMany(player => partyMembers(player).Prepend(player)).Distinct()
            .Select(onlineLevel).OfType<int>().Select(level => Math.Max(Minimum, level)).DefaultIfEmpty(Minimum).Min();

    public static int Offset(int cellLevel, int sessionLevel) => Math.Clamp(cellLevel, Minimum, ushort.MaxValue) - sessionLevel;

    public static float Damage(float amount, bool npcAttacks, int zoneLevel, int playerLevel, ZoneSettings settings)
    {
        var gap = zoneLevel - playerLevel;
        if (amount >= 0 || gap <= 0)
            return amount;

        return amount * (npcAttacks
            ? Math.Min(settings.MaxTaken, 1 + settings.TakenPerLevel * gap)
            : Math.Max(settings.MinDealt, 1 - settings.DealtPerLevel * gap));
    }
}
