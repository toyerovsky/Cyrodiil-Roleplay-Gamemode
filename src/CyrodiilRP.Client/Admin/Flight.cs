using System.Numerics;
using CyrodiilRP.Common.Players;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK;
using OblivionMP.SDK.Common.Archetypes.Mixins;
using OblivionMP.SDK.Common.Camera;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Admin;

// A staff member's flight (/afly). The server takes the character out of the game's physics, on the player's entity,
// and this moves it where the camera looks for as long as the server holds it so. Shift hurries it.
[Service]
internal sealed partial class Flight(IPlayers players, ILocalCharacter character, ICamera camera, IInput input, ILogger logger)
{
    private const float WarnEverySeconds = 1f;

    // The keys named in the warning, to tell what was held when the game's move input was no number.
    private static readonly Key[] WatchedKeys = [Key.W, Key.A, Key.S, Key.D, Key.SPACE];

    private float _warnedAt = float.NegativeInfinity;

    private void Update()
    {
        if (players.LocalPlayer is not { } me || !me.IsFrozen || !me.IgnoresCollision || camera.Look is not { } look)
            return;

        // A page with the keyboard leaves the move input at rest, so Shift typed there moves nothing.
        var push = input.MoveAxis;
        var isHurried = input.IsKeyDown(ModifierKeys.Shift);
        var step = FlightRules.FindStep(look.Forward, look.Right, push, isHurried, Time.DeltaTime);
        if (float.IsFinite(step.X) && float.IsFinite(step.Y) && float.IsFinite(step.Z))
            character.MoveBy(step);
        else
            WarnOfStep(push, look);
    }

    // The game's move input of a frozen character is no number in some frames. Such a frame moves nothing: a throw
    // here would stop the frame for every mod.
    private void WarnOfStep(Vector2 push, CameraLook look)
    {
        if (Time.Elapsed - _warnedAt < WarnEverySeconds)
            return;

        _warnedAt = Time.Elapsed;
        var held = string.Join(' ', WatchedKeys.Where(input.IsKeyDown));
        logger.LogWarning(
            "[CyrodiilRP] No flight step: move input {PushX} {PushY}, look pitch {Pitch} yaw {Yaw}, frame {Seconds} s, keys [{Keys}] Shift {Shift} Ctrl {Ctrl} Alt {Alt}.",
            push.X, push.Y, look.Pitch, look.Yaw, Time.DeltaTime, held,
            input.IsKeyDown(ModifierKeys.Shift), input.IsKeyDown(ModifierKeys.Control), input.IsKeyDown(ModifierKeys.Alt));
    }
}
