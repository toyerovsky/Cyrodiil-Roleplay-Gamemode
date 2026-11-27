using CyrodiilRP.Server.Faction;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Staff;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;
using ReadyM.Api.DI;
using CyrodiilRP.Server.Progression;

namespace CyrodiilRP.Server;

[ModEntry]
internal sealed partial class Mod(Persistence persistence, StaffRoles roles, Factions factions, RpConfig config, ILogger logger,
    IDependencyContainer services, Progression.Progression progression)
{
    private void Init()
    {
        config.Stable.RequireValid();
        config.Specializations.RequireValid();
        config.Away.RequireValid();
        config.NoticeBoards.Check();
        config.Gambling.Check();
        config.Lore.RequireValid();
        config.Auction.Check();
        config.Zones.RequireValid();
        config.Progression.RequireValid();
        services.RegisterSingleton<IProgression>(progression);
        var migration = persistence.RequireSchema();
        roles.Load();
        factions.Load();
        logger.LogInformation("[CyrodiilRP] Server half up. Database at {Migration}, {Owners} owner account(s).",
            migration, config.Owners.Count);
    }
}
