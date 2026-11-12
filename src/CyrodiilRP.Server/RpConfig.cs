using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server;

// server/config.json, which never leaves the server.
[ModConfig]
internal sealed partial class RpConfig
{
    public DatabaseSettings Database { get; set; } = new();

    // ReadyM account ids (the admin panel's Players view), holding every permission.
    public List<Guid> Owners { get; set; } = [];

    public int StartingGold { get; set; } = 50;

    public ChatRanges Chat { get; set; } = new();

    public EconomySettings Economy { get; set; } = new();

    public LoreSettings Lore { get; set; } = new();

    public FactionSettings Factions { get; set; } = new();

    public NoticeBoardSettings NoticeBoards { get; set; } = new();
}
