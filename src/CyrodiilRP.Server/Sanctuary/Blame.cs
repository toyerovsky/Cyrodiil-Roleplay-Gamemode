using System.Numerics;
using CyrodiilRP.Data;

namespace CyrodiilRP.Server.Sanctuary;

// Who hurt the victim. The SDK does not say who hit a player, so this goes by who was fighting next to them just before
// the health dropped. Ranges in centimetres, the window in seconds.
internal static class Blame
{
    // A swing within melee range counts on its own: everyone swinging that close is a suspect. Further away, only a player
    // the game has in combat counts, swinging (a bow) or casting, so a healer or a passer-by casting a light spell is not
    // taken for an attacker.
    public static Verdict<T> Judge<T>(Vector3 victim, double now, IEnumerable<Bystander<T>> others,
        float meleeRange, float rangedRange, float window)
    {
        var close = new List<T>();
        var far = new List<T>();
        foreach (var other in others)
        {
            var distance = Vector3.Distance(victim, other.Position);
            var swung = now - other.LastAttack <= window;
            var cast = now - other.LastCast <= window;
            if (swung && distance <= meleeRange)
                close.Add(other.Player);
            else if ((swung || cast) && other.InCombat && distance <= rangedRange)
                far.Add(other.Player);
        }

        var suspects = close.Count > 0 ? close : far;
        return suspects.Count switch
        {
            0 => new Verdict<T>(SanctuaryVerdict.None, []),
            1 => new Verdict<T>(SanctuaryVerdict.Suspect, suspects),
            _ => new Verdict<T>(SanctuaryVerdict.Several, suspects),
        };
    }
}
