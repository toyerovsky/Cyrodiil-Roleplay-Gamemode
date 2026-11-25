using CyrodiilRP.Common.Players;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

// A staff member's flight (/afly). The server takes the character out of the game's physics, on the player's entity,
// and this moves it where the camera looks for as long as the server holds it so. Shift hurries it.
[Service]
internal sealed partial class Flight(IPlayers players, ILocalCharacter character, ICamera camera, IInput input)
{
    private void Update()
    {
        if (players.LocalPlayer is not { } me || !me.IsFrozen || !me.IgnoresCollision || camera.Look is not { } look)
            return;

        // A page with the keyboard leaves the move input at rest, so Shift typed there moves nothing.
        var step = FlightRules.FindStep(look.Forward, look.Right, input.MoveAxis, input.IsKeyDown(ModifierKeys.Shift), Time.DeltaTime);
        character.MoveBy(step);
    }
}
