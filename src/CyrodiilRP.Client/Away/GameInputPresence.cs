using OblivionMP.SDK;
using OblivionMpCSharpMod.Input;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Away;

// A click or a menu's key in one of the game's own menus (the inventory, a dialogue, bartering): the player is there,
// though nothing of the mode's pages saw it and the character stands still. Polled binds read the hardware, so they
// fire with a game menu open; nothing is taken from the game.
[Service]
internal sealed partial class GameInputPresence(IInput input, PresenceReports reports)
{
    private static readonly Key[] Signs =
    [
        Key.LEFT_MOUSE_BUTTON,
        Key.RIGHT_MOUSE_BUTTON,
        Key.MIDDLE_MOUSE_BUTTON,
        Key.XBUTTON_ONE,
        Key.XBUTTON_TWO,
        Key.LEFT_ARROW,
        Key.UP_ARROW,
        Key.RIGHT_ARROW,
        Key.DOWN_ARROW,
        Key.RETURN,
        Key.TAB,
        Key.SPACE,
        Key.ESCAPE,
        Key.E,
    ];

    private readonly List<IDisposable> _binds = [];

    private void Start()
    {
        foreach (var sign in Signs)
            _binds.Add(input.RegisterKeyBind(sign, reports.Note, KeySource.Polled));
    }

    private void Stop()
    {
        foreach (var bind in _binds)
            bind.Dispose();
        _binds.Clear();
    }
}
