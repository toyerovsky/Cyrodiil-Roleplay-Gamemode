using OblivionMP.SDK.Common.Foliage;

namespace Roleplay.Jobs.Client;

/// <summary>A hit the server counted.</summary>
/// <param name="Tree">The tree.</param>
/// <param name="Kind">The kind of tree as the server's config names it, such as "Oak"; empty for a tree of no listed kind.</param>
/// <param name="Damage">Damage it has taken so far, from everyone.</param>
/// <param name="Health">Damage that fells it, with this player's axe.</param>
public readonly record struct ChopHit(FoliageId Tree, string Kind, int Damage, int Health);
