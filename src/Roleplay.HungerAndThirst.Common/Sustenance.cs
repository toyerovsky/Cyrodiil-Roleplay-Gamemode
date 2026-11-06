using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Common;

// How fed and watered a player's character is, on every player. Replication.ServerAuthoritative: only the server
// writes these, every client reads them as player.Satiety and player.Hydration. Both run from 0 (starving,
// parched) to SustenanceScale.Max (full). Whole numbers on purpose: replicated floats ignore changes of 0.1 or less.
//
// Needs an OblivionMP client that registers mod mixins as networked components (ReplicatedShapeRegistration in
// OblivionMpCSharpMod/DI.cs). Without it the client can't read this and logs "No reader job registered for
// component ID" for every update; the RPC variant of this mod (../HungerAndThirst) works around that.
[ArchetypeMixin(Replication.ServerAuthoritative)]
[Extends(typeof(Player))]
public readonly partial struct Sustenance
{
    public partial int Satiety { get; set; }

    public partial int Hydration { get; set; }
}
