using CyrodiilRP.Common.Players;
using CyrodiilRP.Server.Infrastructure;
using OblivionMp.Sdk.Common.Archetypes;
using OblivionMp.Sdk.Common.Archetypes.Mixins;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;
using Yooni.Native.Container;

namespace CyrodiilRP.Server.Players;

// Checked every second: the game writes the account's nickname as it connects, which can land after the character's
// name, and reads its own name back from the creator's race menu.
[Service]
internal sealed partial class Nicknames(IEntities entities, OnlinePlayers online, Timers timers)
{
    private const string CheckJob = "players.nicknames";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    // Players whose game name differed at the last check. The game sends its whole identity back after the race menu, and
    // a name written over a race the server has not heard yet would take that race back, so it waits for a second look.
    private readonly HashSet<PlayerId> _misnamed = [];

    private void Start() => timers.RunEvery(CheckJob, CheckEvery, Check);

    private void Check()
    {
        foreach (var player in entities.Query<Player>())
        {
            if (!online.TryGet(player.PlayerId, out var connected) || !connected.IsLoaded)
                continue;

            WriteGameName(player.PlayerId, connected);

            var shown = player.Nickname.ToString();
            var wanted = connected.Character is { } character
                ? connected.DutySince is null ? character.FullName : Nameplate.DutyMark + character.FullName
                : null;
            if (shown.Length > 0 && shown != wanted && shown != connected.ShownNickname && connected.AccountName.Length == 0)
                online.RememberNickname(connected, shown);

            if (wanted is null || shown == wanted)
                continue;

            player.SetNickname(new NativeString256(wanted, false));
            connected.ShownNickname = wanted;
        }
    }

    private void WriteGameName(PlayerId id, OnlinePlayer connected)
    {
        var wanted = connected.Character?.FullName;
        if (wanted is null || !entities.TryLookup(id, out MainCharacter main) || main.Race.Value == 0
            || main.Name.ToString() == wanted)
        {
            _misnamed.Remove(id);
            return;
        }

        if (_misnamed.Add(id))
            return;

        main.Name = new NativeString256(wanted, false);
        _misnamed.Remove(id);
    }
}
