using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Common.Building;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Building;

// A plot's sign: H in front of it opens a window of the plot, its price and tax, and Buy while it is for sale. The gold
// leaves the purse before the purchase is written, and goes back if the write fails or another bought the plot first.
[Service]
internal sealed partial class PlotSales(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    Plots plots,
    Wallet wallet,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    ILogger logger) : IFrontTarget
{
    private const string BuyAction = "economy.plot_buy";
    private const string WatchJob = "plots.sales";
    private const float CentimetresPerMetre = 100f;

    private readonly Dictionary<OnlinePlayer, OpenPlotSale> _open = [];

    // The players whose purchase is still being written: a second one would take the gold again.
    private readonly HashSet<OnlinePlayer> _buying = [];

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) =>
        FindInFront(actor, reach, coneDegrees, out var distance) is null ? null : distance;

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (FindInFront(actor, reach, coneDegrees, out _) is not { } plot || actor.Character is not { } character)
            return;

        _open[actor] = new OpenPlotSale(plot.Id, character.Id, SeenGold: 0, PlotSaleRules.StandingOf(plot, character.Id));
        SendState(actor);
    }

    // The sign nearest in front, of the plots in the player's area.
    private PlotRecord? FindInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance)
    {
        PlotRecord? nearest = null;
        distance = float.MaxValue;
        foreach (var plot in plots.All)
        {
            if (proximity.MeasureInFront(actor.Id, plot.AreaId, plot.SignPosition, reach, coneDegrees) is { } away && away < distance)
            {
                nearest = plot;
                distance = away;
            }
        }

        return nearest;
    }

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.PlotSaleBought += OnBought;
        rpc.PlotSaleLeft += OnLeftWindow;
        online.Left += player =>
        {
            _open.Remove(player);
            _buying.Remove(player);
        };
        timers.RunEvery(WatchJob, PlotSaleRules.LookEvery, Watch);
    }

    private void OnBought(PlayerId id)
    {
        if (!TryFindOpen(id, out var player, out var open, out var plot))
            return;

        if (_buying.Contains(player))
        {
            Answer(player, BuildingText.SaleBusy, bought: false);
            return;
        }

        wallet.TryCount(player.Id, out var gold);
        var most = plots.Settings.PlotsPerCharacter;
        var verdict = PlotSaleRules.Judge(plot, open.CharacterId, plots.CountOwnedBy(open.CharacterId), most, gold);
        if (verdict != SaleVerdict.Accepted)
        {
            Answer(player, Explain(verdict, plot, most, gold), bought: false);
            return;
        }

        if (plot.Price > 0 && !wallet.TryTake(player.Id, plot.Price))
        {
            Answer(player, BuildingText.SaleTooPoor(plot.Id, plot.Price, gold), bought: false);
            return;
        }

        var characterId = open.CharacterId;
        _buying.Add(player);
        fire.Run(BuyAction, async () =>
        {
            bool bought;
            try
            {
                bought = await plots.BuyAsync(plot, characterId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[CyrodiilRP] {Player} could not buy plot #{Plot}.", player.Ref, plot.Id);
                Refund(player, plot, BuildingText.SaleFailed);
                return;
            }
            finally
            {
                _buying.Remove(player);
            }

            if (!bought)
            {
                Refund(player, plot, BuildingText.SaleTaken(plot.Id));
                return;
            }

            audit.Log(BuyAction, player.Ref, CharacterRecord.FormatRef(characterId), new PurchaseDetails(plot.Id, plot.Price, plot.WeeklyTax));
            Finish(player, BuildingText.SaleBought(plot.Id, plot.Price, plot.WeeklyTax), bought: true);
        });
    }

    // The plot was not written to the buyer: the gold goes back.
    private void Refund(OnlinePlayer player, PlotRecord plot, Phrase why)
    {
        var refunded = plot.Price == 0 || (player.IsLive && wallet.TryGive(player.Id, plot.Price));
        if (!refunded)
            logger.LogError("[CyrodiilRP] {Player} paid {Price} gold for plot #{Plot}, which was not written, and could not be given it back.",
                player.Ref, plot.Price, plot.Id);

        Finish(player, refunded ? why : BuildingText.SaleLost, bought: false);
    }

    private Phrase Explain(SaleVerdict verdict, PlotRecord plot, int most, int gold) => verdict switch
    {
        SaleVerdict.Accepted => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "An accepted purchase needs no explaining."),
        SaleVerdict.Taken => BuildingText.SaleTaken(plot.Id),
        SaleVerdict.Yours => BuildingText.SaleYours(plot.Id),
        SaleVerdict.TooMany => BuildingText.SaleTooMany(most),
        SaleVerdict.TooPoor => BuildingText.SaleTooPoor(plot.Id, plot.Price, gold),
    };

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenPlotSale? open,
        [NotNullWhen(true)] out PlotRecord? plot)
    {
        open = null;
        plot = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open) is { } broken)
        {
            Close(player, broken);
            return false;
        }

        return plots.TryFindPlot(open.PlotId, out plot);
    }

    // Onto the window, or into the corner when it closed while the purchase was being written.
    private void Finish(OnlinePlayer player, Phrase text, bool bought)
    {
        if (!player.IsLive)
            return;

        if (!_open.ContainsKey(player))
        {
            chat.Notify(player, text);
            return;
        }

        Answer(player, text, bought);
        SendState(player);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // A window closes a walk away from the sign, out of the world or with the plot, and shows a change of gold or owner.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open) is { } broken)
                Close(player, broken);
            else if ((wallet.TryCount(player.Id, out var gold) && gold != open.SeenGold)
                     || (plots.TryFindPlot(open.PlotId, out var plot) && PlotSaleRules.StandingOf(plot, open.CharacterId) != open.SeenStanding))
                SendState(player);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, OpenPlotSale open)
    {
        if (!wallet.IsInWorld(player.Id))
            return BuildingText.SaleClosedNotInWorld;

        if (!plots.TryFindPlot(open.PlotId, out var plot))
            return BuildingText.SaleClosedGone;

        return proximity.MeasureDistance(player.Id, plot.AreaId, plot.SignPosition) is { } away && away <= PlotSaleRules.WalkAway
            ? null
            : BuildingText.SaleClosedFar(PlotSaleRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendPlotSaleClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool bought) => rpc.SendPlotSaleAnswer(player.Id, chat.Translate(player, text), bought);

    private void SendState(OnlinePlayer player)
    {
        if (!_open.TryGetValue(player, out var open) || !plots.TryFindPlot(open.PlotId, out var plot))
            return;

        wallet.TryCount(player.Id, out var gold);
        var standing = PlotSaleRules.StandingOf(plot, open.CharacterId);
        _open[player] = open with { SeenGold = gold, SeenStanding = standing };
        var box = plot.Box;
        var view = new PlotSaleView(plot.Id, plot.Kind, box.Width, box.Depth,
            box.Width * BuildingGrid.TileCentimetres / CentimetresPerMetre, box.Depth * BuildingGrid.TileCentimetres / CentimetresPerMetre,
            (int)MathF.Round(box.Height / CentimetresPerMetre), plot.Price, plot.WeeklyTax, standing, gold, plots.CountOwnedBy(open.CharacterId),
            plots.Settings.PlotsPerCharacter);
        rpc.SendPlotSaleState(player.Id, JsonSerializer.Serialize(view, PlotSaleJson.Default.PlotSaleView));
    }

    private sealed record PurchaseDetails(long Plot, int Price, int WeeklyTax);
}
