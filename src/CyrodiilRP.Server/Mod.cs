using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server;

[ModEntry]
internal sealed partial class Mod(Persistence persistence, StaffRoles roles, Factions factions, RpConfig config, ILogger logger)
{
    private void Init()
    {
        var migration = persistence.RequireSchema();
        roles.Load();
        factions.Load();
        logger.LogInformation("[CyrodiilRP] Server half up. Database at {Migration}, {Owners} owner account(s).",
            migration, config.Owners.Count);
    }
}
