using CyrodiilRP.Client.Pages;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Talk;

// The game draws its mouse cursor exactly while one of its own menus is up (the dialogue, the inventory, the map), and
// tells a mod nothing else about them. It hands the cursor to every page each tick, so the chat's page, which is
// always there, says when it comes and goes. A page of the mode holding the input shows the cursor too.
[Service]
internal sealed partial class GameCursor(PageInput pageInput)
{
    private bool _shown;

    public bool IsGameMenuOpen => _shown && !pageInput.IsHeld;

    public void Report(bool shown) => _shown = shown;
}
