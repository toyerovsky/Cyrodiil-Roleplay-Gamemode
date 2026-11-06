using OblivionMP.SDK.Common.Foliage;
using ReadyM.Api.Idents;

namespace Roleplay.Jobs.Client;

/// <summary>A tree that came down.</summary>
/// <param name="Kind">The kind of tree as the server's config names it; empty for a tree of no listed kind.</param>
/// <param name="Feller">Whose hit felled it; everyone who hit it shares the wood.</param>
/// <param name="ByLocalPlayer">Whether this player's hit felled it.</param>
public readonly record struct FelledTree(FoliageId Tree, string Kind, float Scale, PlayerId Feller, bool ByLocalPlayer);
