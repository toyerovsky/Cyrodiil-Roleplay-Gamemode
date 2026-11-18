using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Specializations;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Interactions;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Traders;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Specializations;

// The specialization window at a mentor or the registrar, who are stood and kept as traders are (Traders/). A character
// has one specialization. The first choice costs FirstPrice, a change ChangePrice and is allowed once the cooldown since
// the last choice is over. The gold leaves the purse before the choice is written, and goes back if the write fails.
[Service]
internal sealed partial class Mentors(
    RpcHandlers rpc,
    OnlinePlayers online,
    InteractionMenu interactions,
    Proximity proximity,
    TraderNpcs npcs,
    CharacterSpecializations specializations,
    Wallet wallet,
    Persistence persistence,
    RpConfig config,
    RpChat chat,
    Audit audit,
    Timers timers,
    Fire fire,
    SystemClock clock,
    ILogger logger) : IFrontTarget
{
    private const string OpenJob = "mentor.open";
    private const string ChooseAction = "economy.specialization_choose";
    private const string WatchJob = "mentor.watch";

    private static readonly TimeSpan WatchEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<OnlinePlayer, OpenMentor> _open = [];

    // The players whose choice is still being written: a second one would take the gold again.
    private readonly HashSet<OnlinePlayer> _choosing = [];

    public float? MeasureInFront(OnlinePlayer actor, float reach, float coneDegrees) =>
        FindInFront(actor, reach, coneDegrees, out var distance) is null ? null : distance;

    public void Use(OnlinePlayer actor, float reach, float coneDegrees)
    {
        if (FindInFront(actor, reach, coneDegrees, out _) is { } spot && actor.Character is { } character)
            Open(actor, character.Id, spot);
    }

    private void Start()
    {
        interactions.AddTarget(this);
        rpc.MentorChosen += OnChosen;
        rpc.MentorLeft += OnLeftWindow;
        online.Left += player =>
        {
            _open.Remove(player);
            _choosing.Remove(player);
        };
        timers.RunEvery(WatchJob, WatchEvery, Watch);
    }

    // The key is the mentor's only when a mentor or the registrar is the nearest of the mode's people in front.
    private TraderSpotRecord? FindInFront(OnlinePlayer actor, float reach, float coneDegrees, out float distance) =>
        npcs.FindNearestInFront(actor, reach, coneDegrees, out distance) is { } spot && SpecializationRules.IsTeacher(spot.Kind) ? spot : null;

    // The choice is read again, so the window never sells on a guess.
    private void Open(OnlinePlayer player, long characterId, TraderSpotRecord spot) => fire.Run(OpenJob, async () =>
    {
        try
        {
            await specializations.RefreshAsync(player, characterId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] The specialization of {Character} could not be read.", CharacterRecord.FormatRef(characterId));
            if (player.IsLive)
                chat.Notify(player, SpecializationText.Unreadable);

            return;
        }

        if (!player.IsLive)
            return;

        if (CheckWindow(player, spot) is { } broken)
        {
            chat.Notify(player, broken);
            return;
        }

        _open[player] = new OpenMentor(spot, characterId, SeenGold: 0);
        SendState(player);
    });

    private void OnChosen(PlayerId id, string name)
    {
        if (!TryFindOpen(id, out var player, out var open))
            return;

        if (!SpecializationRules.TryRead(name, out var kind))
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked a mentor for a specialization {Specialization}, which does not exist.", player.Ref, name);
            Answer(player, SpecializationText.Unknown, false);
            return;
        }

        if (!SpecializationRules.FindOffered(open.Spot.Kind).Contains(kind))
        {
            logger.LogWarning("[CyrodiilRP] {Player} asked a mentor for {Specialization}, which this mentor does not teach.", player.Ref, kind);
            Answer(player, SpecializationText.NotTaught(SpecializationRules.FindName(kind)), false);
            return;
        }

        if (_choosing.Contains(player))
        {
            Answer(player, SpecializationText.Busy, false);
            return;
        }

        if (specializations.Find(player) is not { } chosen)
        {
            Answer(player, SpecializationText.Unreadable, false);
            return;
        }

        var before = chosen.Row;
        var specialization = SpecializationRules.FindName(kind);
        if (before?.Specialization == kind)
        {
            Answer(player, SpecializationText.AlreadyYours(specialization), false);
            return;
        }

        var now = clock.UtcNow;
        if (before is not null && SpecializationRules.FindNextChoice(before.ChosenAt, config.Specializations) is var next && next > now)
        {
            Answer(player, SpecializationText.TooSoon(SpecializationRules.DescribeWait(now, next)), false);
            return;
        }

        var price = SpecializationRules.FindPrice(first: before is null, config.Specializations);
        if (!wallet.TryCount(player.Id, out var gold) || gold < price || (price > 0 && !wallet.TryTake(player.Id, price)))
        {
            Answer(player, SpecializationText.TooPoor(specialization: specialization, price: price, gold: gold), false);
            return;
        }

        var characterId = open.CharacterId;
        _choosing.Add(player);
        fire.Run(ChooseAction, async () =>
        {
            try
            {
                await persistence.Writer.EnqueueAsync(ChooseAction, (db, ct) => SpecializationBook.ChooseAsync(db, characterId, kind, price, now, ct));
            }
            catch (Exception exception)
            {
                _choosing.Remove(player);
                logger.LogError(exception, "[CyrodiilRP] {Player} could not choose {Specialization}.", player.Ref, kind);
                var refunded = price == 0 || (player.IsLive && wallet.TryGive(player.Id, price));
                if (!refunded)
                    logger.LogError("[CyrodiilRP] {Player} paid {Price} gold for a specialization that was not written, and could not be given it back.", player.Ref, price);

                Finish(player, refunded ? SpecializationText.Failed : SpecializationText.Lost, chosenNow: false);
                return;
            }

            _choosing.Remove(player);
            var row = new CharacterSpecialization
            {
                CharacterId = characterId,
                Specialization = kind,
                Price = price,
                ChosenAt = now,
                Changes = before is null ? 0 : before.Changes + 1,
            };
            if (player.IsLive && player.Character?.Id == characterId)
                specializations.Remember(player, characterId, row);

            audit.Log(ChooseAction, player.Ref, CharacterRecord.FormatRef(characterId), new ChoiceDetails(kind, price, before?.Specialization));
            Finish(player, before is { } old
                    ? SpecializationRules.DescribeChange(kind, old.Specialization, price)
                    : SpecializationText.Chosen(specialization),
                chosenNow: true);
        });
    }

    // False once the window closed or was never opened. One that no longer holds is closed here.
    private bool TryFindOpen(PlayerId id, [NotNullWhen(true)] out OnlinePlayer? player, [NotNullWhen(true)] out OpenMentor? open)
    {
        open = null;
        if (!online.TryGet(id, out player) || !_open.TryGetValue(player, out open))
            return false;

        if (CheckWindow(player, open.Spot) is not { } broken)
            return true;

        Close(player, broken);
        return false;
    }

    // Onto the window, or into the corner when it closed while the choice was being written.
    private void Finish(OnlinePlayer player, Phrase text, bool chosenNow)
    {
        if (!player.IsLive)
            return;

        if (!_open.ContainsKey(player))
        {
            chat.Notify(player, text);
            return;
        }

        Answer(player, text, chosenNow);
        SendState(player);
    }

    private void OnLeftWindow(PlayerId id)
    {
        if (online.TryGet(id, out var player))
            _open.Remove(player);
    }

    // A mentor the staff took away takes its windows with it here, within a second.
    private void Watch()
    {
        if (_open.Count == 0)
            return;

        foreach (var (player, open) in _open.ToList())
        {
            if (CheckWindow(player, open.Spot) is { } broken)
                Close(player, broken);
            else if (wallet.TryCount(player.Id, out var gold) && gold != open.SeenGold)
                SendState(player);
        }
    }

    private Phrase? CheckWindow(OnlinePlayer player, TraderSpotRecord spot)
    {
        if (!wallet.IsInWorld(player.Id))
            return SpecializationText.ClosedNotInWorld;

        if (!npcs.TryFind(spot, out var npc))
            return SpecializationText.ClosedGone;

        return proximity.MeasureDistance(player.Id, new AreaId(spot.Area), npc.Position) is { } away && away <= TraderRules.WalkAway
            ? null
            : SpecializationText.ClosedFar(TraderRules.WalkAwayMetres);
    }

    private void Close(OnlinePlayer player, Phrase why)
    {
        _open.Remove(player);
        if (!player.IsLive)
            return;

        rpc.SendMentorClosed(player.Id);
        chat.Notify(player, why);
    }

    private void Answer(OnlinePlayer player, Phrase text, bool chosenNow) => rpc.SendMentorAnswer(player.Id, chat.Translate(player, text), chosenNow);

    private void SendState(OnlinePlayer player)
    {
        wallet.TryCount(player.Id, out var gold);
        var open = _open[player] with { SeenGold = gold };
        _open[player] = open;

        var settings = config.Specializations;
        var before = specializations.Find(player)?.Row;
        var offered = SpecializationRules.FindOffered(open.Spot.Kind)
            .Select(SpecializationRules.FindProfile)
            .Select(profile => new MentorSpecializationView(profile.Kind, profile.Path, profile.Mastered, profile.Trained,
                SpecializationRules.FindMajorSkills(profile), before?.Specialization == profile.Kind))
            .ToList();
        var next = before is null ? (DateTimeOffset?)null : SpecializationRules.FindNextChoice(before.ChosenAt, settings);
        var view = new MentorView(
            SpecializationRules.FindPath(open.Spot.Kind),
            offered,
            before?.Specialization,
            gold,
            SpecializationRules.FindPrice(first: before is null, settings),
            next is { } at && at > clock.UtcNow ? at.ToUnixTimeMilliseconds() : null,
            new MentorCapsView(settings.TrainedSkillCap, settings.LimitedSkillCap, settings.TrainedAttributeCap, settings.LimitedAttributeCap,
                settings.UnchosenSkillCap));
        rpc.SendMentorState(player.Id, JsonSerializer.Serialize(view, MentorJson.Default.MentorView));
    }

    private sealed record ChoiceDetails(SpecializationKind Specialization, int Price, SpecializationKind? Replaced);
}
