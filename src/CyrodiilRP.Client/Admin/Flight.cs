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
// and this moves it where the camera looks for as long as the server holds it so. W, A, S and D fly, Shift hurries.
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

        // IsKeyDown reads the key itself, so a key typed into a page or pressed in a menu of the game is left out here.
        if (pageInput.IsHeld || chat.IsOpen || cursor.IsGameMenuOpen)
            return;

        // The game's own move input (IInput.MoveAxis) is no number for a character out of the game's movement.
        var push = FlightRules.FindPush(input.IsKeyDown(Key.W), input.IsKeyDown(Key.S), input.IsKeyDown(Key.A), input.IsKeyDown(Key.D));
        var step = FlightRules.FindStep(look.Forward, look.Right, push, input.IsKeyDown(ModifierKeys.Shift), Time.DeltaTime);
        character.MoveBy(step);
    }
}
