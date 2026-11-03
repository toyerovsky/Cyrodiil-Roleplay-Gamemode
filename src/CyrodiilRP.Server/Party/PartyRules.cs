namespace CyrodiilRP.Server.Party;

// The numbers of the parties, which the texts show.
internal static class PartyRules
{
    // As many as the panel lists without covering the game.
    public const int MaxMembers = 6;

    // A party of one is none: the last one left goes too.
    public const int MinMembers = 2;

    // Within sight: farther, or in another place, a member shows as away.
    public const int NearMetres = 100;

    public const float Near = NearMetres * CentimetresPerMetre;

    // The panel shows health in steps, so a few points of it do not send the party again every second.
    public const int HealthStep = 5;

    private const float CentimetresPerMetre = 100f;

    private const int WholePercent = 100;

    // Up to the next step, so only the dead show none. Null before the game has said the maximum.
    public static int? ComputeHealthPercent(float health, float healthMax)
    {
        if (healthMax <= 0f)
            return null;

        if (health <= 0f)
            return 0;

        var percent = Math.Min(WholePercent, health / healthMax * WholePercent);
        return (int)MathF.Ceiling(percent / HealthStep) * HealthStep;
    }
}
