using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Spawn;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using OblivionMp.Sdk.Common.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Spawn;

// Without a spawn, new characters stay where the game put them.
[Service]
internal sealed partial class SpawnPoints(Persistence persistence, IEntities entities, Fire fire, Timers timers, ILogger logger)
{
    private const string LoadJob = "spawn.load";
    private const string SetJob = "spawn.write";

    private static readonly TimeSpan LoadRetry = TimeSpan.FromSeconds(10);

    public SpawnPlace? Current { get; private set; }

    private void Start() => Load();

    private void Load() => fire.Run(LoadJob, LoadAsync);

    private async Task LoadAsync()
    {
        SpawnPoint? row;
        try
        {
            row = await persistence.Reader.RunAsync(SpawnPointBook.GetAsync);
        }
        catch (Exception exception) when (DatabaseFailures.Is(exception))
        {
            logger.LogWarning("[CyrodiilRP] The spawn could not be read ({Problem}), trying again in {Seconds} s.",
                exception.GetBaseException().Message, LoadRetry.TotalSeconds);
            timers.RunAfter(LoadJob, LoadRetry, Load);
            return;
        }

        // One set while the read was on its way is newer.
        if (row is not null && Current is null)
            Current = new SpawnPlace(new Vector3(row.X, row.Y, row.Z), row.Yaw);
    }

    public async Task SetAsync(SpawnPlace place, string byRef)
    {
        var row = new SpawnPoint
        {
            Area = GameSpace.Outside.ToString(),
            X = place.Position.X,
            Y = place.Position.Y,
            Z = place.Position.Z,
            Yaw = place.Yaw,
            SetByRef = byRef,
            SetAt = DateTimeOffset.UtcNow,
        };
        await persistence.Writer.EnqueueAsync(SetJob, (db, ct) => SpawnPointBook.SetAsync(db, row, ct));
        Current = place;
    }

    public void MoveNew(OnlinePlayer player)
    {
        if (Current is not { } spawn
            || !entities.TryLookup(player.Id, out MainCharacter main)
            || !entities.TryGetScope(main, out Area area))
            return;

        if (!SpawnRules.Allows(area.AreaId))
        {
            logger.LogInformation("[CyrodiilRP] {Player} began in {Area}, outside the spawn's world, and stays there.",
                player.Id, area.AreaId);
            return;
        }

        main.Position = spawn.Position + Teleports.Lift;
        main.Rotation = spawn.Yaw;
    }
}
