using ReadyM.SDK.Entities;

namespace CyrodiilRP.Server.Progression;

internal sealed class NpcDeathRegistry
{
    private readonly Dictionary<EntityHandle, NpcDeath> _deaths = [];

    public void Before(EntityHandle handle, float health)
    {
        var state = _deaths.GetValueOrDefault(handle) ?? new NpcDeath(null, false, false);
        _deaths[handle] = health > 0 && state.Paid
            ? new NpcDeath(null, false, true) : state with { WasAlive = health > 0 };
    }

    public bool Observe(EntityHandle handle, bool playerDamage, float health, long now, out long? firstDamage)
    {
        // A change that skipped the Filter stage left no health before it, so it cannot tell a death apart.
        if (!_deaths.TryGetValue(handle, out var state))
        {
            firstDamage = null;
            return false;
        }
        firstDamage = state.FirstDamage;
        if (!state.WasAlive || state.Paid)
            return false;
        firstDamage ??= playerDamage ? now : null;
        var dead = health <= 0;
        _deaths[handle] = state with { FirstDamage = firstDamage, Paid = dead, WasAlive = false };
        return dead;
    }

    public void ForgetGone()
    {
        foreach (var handle in _deaths.Keys.Where(x => !x.IsAlive()).ToArray())
            _deaths.Remove(handle);
    }

    private sealed record NpcDeath(long? FirstDamage, bool Paid, bool WasAlive);
}
