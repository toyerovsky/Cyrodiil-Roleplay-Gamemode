using OblivionMp.Sdk;
using OblivionMp.Sdk.Archetypes.Mixins;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client;

// The character's pawn, the game's actor for it: none at the main menu and while a save loads, a new one after every
// load. The game builds its overlay and its windows again with each, so pages and the creator wait for it to settle.
[Service]
internal sealed partial class PawnWatch(IPlayers players)
{
    private IntPtr _pawn;

    private float _since;

    public bool InWorld => _pawn != IntPtr.Zero;

    // Every new pawn gets the next number, one at the address of the last too, so a page shown for one tells the next.
    public int Number { get; private set; }

    public bool HasSettled(float seconds) => InWorld && Time.Elapsed - _since >= seconds;

    // Before the services that read it in their own update.
    [UpdateOrder(0)]
    private void Update()
    {
        var pawn = players.LocalMainCharacter is { } character ? character.Pawn : IntPtr.Zero;
        if (pawn == _pawn)
            return;

        _pawn = pawn;
        _since = Time.Elapsed;
        if (pawn != IntPtr.Zero)
            Number++;
    }
}
