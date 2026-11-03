using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server;

[ModEntry]
internal sealed partial class Mod(Persistence persistence, StaffRoles roles, RpConfig config, ILogger logger)
{
    private void Init()
    {
        var migration = persistence.RequireSchema();
        roles.Load();
        logger.LogInformation("[CyrodiilRP] Server half up. Database at {Migration}, {Owners} owner account(s).",
            migration, config.Owners.Count);
    }
}
