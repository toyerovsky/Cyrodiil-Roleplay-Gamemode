using System.Collections.Immutable;
using System.Text.Json;
using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Fishing;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Lore;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Fisher;

// The Records part of the fisher's window: the longest fish of the server, answered once to the page that asks, from a
// board read on the reader pool and kept for everyone (RecordBoardKeeper).
[Service]
internal sealed partial class FisherRecords(
    RpcHandlers rpc,
    OnlinePlayers online,
    FisherShops shops,
    FishingSkills skills,
    ShownNames names,
    RpChat chat,
    Persistence persistence,
    Fire fire,
    SystemClock clock,
    ILogger logger)
{
    private const string ReadJob = "fisher.records";

    private readonly RecordBoardKeeper _keeper = new(clock, async () => RecordBoard.Create(
        await persistence.Reader.RunAsync((db, ct) => FishingBook.ReadBoardAsync(db, RecordBoardRules.TopCount, ct)), logger));

    private readonly RecordAskRegistry _asks = new();

    private void Start()
    {
        rpc.FisherRecordsAsked += OnAsked;
        online.Left += _asks.Forget;
    }

    private void OnAsked(PlayerId id)
    {
        if (!online.TryGet(id, out var player) || !shops.HasOpen(player) || !_asks.TryAsk(player, clock.MonotonicMs))
            return;

        fire.Run(ReadJob, async () =>
        {
            RecordBoard board;
            try
            {
                board = await _keeper.ReadAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] The fishing records could not be read for {Player}.", player.Ref);
                if (player.IsLive && shops.HasOpen(player))
                    rpc.SendFisherAnswer(player.Id, chat.Translate(player, FisherText.RecordsUnread), false);

                return;
            }

            if (player.IsLive && shops.HasOpen(player))
                rpc.SendFisherRecords(player.Id, JsonSerializer.Serialize(Build(player, board), FisherJson.Default.FisherRecordsView));
        });
    }

    private FisherRecordsView Build(OnlinePlayer player, RecordBoard board)
    {
        var character = player.Character ?? throw new InvalidOperationException($"{player.Ref} reads the fisher's records without a character.");
        var playing = online.All.Where(other => other.IsLive && other.Character is not null).ToLookup(other => other.Character!.Id);
        var reader = new RecordReader(
            character.Id,
            names.Shown(player, player),
            skills.Find(player)?.Records ?? ImmutableDictionary<FishSpecies, FishRecord>.Empty);
        return RecordBoardViews.Build(board, reader,
            holder => RecordNames.Show(holder, playing[holder.CharacterId].FirstOrDefault() is { } subject ? names.Shown(player, subject) : null));
    }
}
