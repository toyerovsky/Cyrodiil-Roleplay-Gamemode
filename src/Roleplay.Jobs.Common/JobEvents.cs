using OblivionMP.SDK.Common.Archetypes;
using OblivionMP.SDK.Common.Foliage;
using ReadyM.Api.Idents;

namespace Roleplay.Jobs.Common;

public static class JobEvents
{
    /// <summary>The player whose character it is, or <see cref="PlayerId.Server"/> for one that is not a player's.</summary>
    public static PlayerId PlayerOf(Character character)
        => character.TryAs(out MainCharacter main) ? main.PlayerId : PlayerId.Server;

    /// <summary>The felled tree's id.</summary>
    public static FoliageId Tree(this in TreeFelledEvent felled) => new(felled.Mesh, felled.X, felled.Y, felled.Z);

    public static string Describe(HarvestRefusal refusal) => refusal switch
    {
        HarvestRefusal.Gone => "Someone got to it first.",
        HarvestRefusal.Other => "Something stops it.",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "Not a harvest refusal."),
    };
}
