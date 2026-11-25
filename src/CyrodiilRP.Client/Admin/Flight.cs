using CyrodiilRP.Client.Chat;
using CyrodiilRP.Client.Pages;
using CyrodiilRP.Client.Talk;
using CyrodiilRP.Common.Players;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

// A staff member's flight (/afly). The server takes the character out of the game's physics, on the player's entity,
// and this moves it where the camera looks for as long as the server holds it so. The game's move keys or stick
// fly, Shift hurries.
[Service]
internal sealed partial class Flight(
    IPlayers players,
    ILocalCharacter character,
    ICamera camera,
    IInput input,
    PageInput pageInput,
    ChatOverlay chat,
    GameCursor cursor)
{
    private void Update()
    {
        if (players.LocalPlayer is not { } me || !me.IsFrozen || !me.IgnoresCollision || camera.Look is not { } look)
            return;

        // Under a page with the keyboard or a menu of the game the keys are not the flight's.
        if (pageInput.IsHeld || chat.IsOpen || cursor.IsGameMenuOpen)
            return;

        var step = FlightRules.FindStep(look.Forward, look.Right, input.MoveAxis, input.IsKeyDown(ModifierKeys.Shift), Time.DeltaTime);
        character.MoveBy(step);
    }
}
