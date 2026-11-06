using System.Numerics;
using CyrodiilRP.Data;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using OblivionMP.SDK.Common.Archetypes;
using ReadyM.Api.Idents;
using ReadyM.Relay.Common.Oblivion.ECS.Values;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Server.Entities;

namespace CyrodiilRP.Server.Sanctuary;

// Runs the Sanctuary every tick, on the update thread. The SDK has no hit event and does not say who hit whom, so the
// guardian watches: it notes who swings or casts, keeps a Ward on the health of everyone inside, and when a health drops,
// Blame names the players who were fighting next to the victim. Each tick first gathers every drop and marks every
// aggressor, and only then decides whose health goes back, so the order players are visited in changes nothing.
[Service]
internal sealed partial class Guardian(
    IEntities entities,
    PlayerAreas areas,
    OnlinePlayers online,
    SanctuaryNotices notices,
    SanctuaryLog log,
    RpConfig config,
    GameThread gameThread,
    Watchdog watchdog,
    ILogger logger)
{
    private const string TickJob = "sanctuary.tick";
    private const double ShieldedNoticeSeconds = 3;

    // A health the staff wrote reaches the player's game and comes back within this: until then, a drop is theirs.
    private const double StaffWriteSettleSeconds = 1;

    private readonly PlayerWatchRegistry _players = new();
    private readonly NpcWatchRegistry _npcs = new();
    private readonly List<Seen> _seen = [];
    private readonly List<Hurt> _hurt = [];
    private Zone _zone = null!;

    // Seconds since the server started, the clock of every streak and incident. It runs while the Sanctuary is off too.
    private double _now;

    private SanctuarySettings Settings => config.Sanctuary;

    public bool IsOn { get; private set; }

    public void Turn(bool on)
    {
        if (on == IsOn)
            return;

        IsOn = on;
        if (on)
            return;

        // Off: nobody is inside it or an aggressor, and the wards start over when it comes back.
        _players.ClearStreaks();
        foreach (var watch in _players.All.Values)
        {
            Close(watch.Incidents);
            watch.Reset();
        }

        _npcs.Clear(watch => Close(watch.Incidents));
        UpdateBadges();
    }

    public SanctuaryStatus Describe(OnlinePlayer player)
    {
        var area = areas.Find(player.Id);
        var inside = IsOn && _zone.Contains(area);
        var left = _players.TryGet(player.Id, out var watch) ? SecondsLeft(watch) : 0;
        return new SanctuaryStatus(IsOn, inside, inside ? _zone.NameOf(area!) : null, left > 0, left);
    }

    // The aggressors online, the longest streak first.
    public List<SanctuaryAggressor> ListAggressors()
    {
        var found = new List<SanctuaryAggressor>();
        foreach (var (id, watch) in _players.All)
        {
            if (SecondsLeft(watch) is > 0 and var left && online.TryGet(id, out var player))
                found.Add(new SanctuaryAggressor(player, left, areas.Find(id) is { } area ? _zone.NameOf(area) : null));
        }

        return found.OrderByDescending(aggressor => aggressor.SecondsLeft).ThenBy(aggressor => aggressor.Player.Number).ToList();
    }

    // False when the player was no aggressor.
    public bool TryClear(OnlinePlayer player)
    {
        if (!_players.TryGet(player.Id, out var watch) || !watch.Aggression.IsOn(_now))
            return false;

        watch.Aggression.Clear();
        notices.ProtectedAgain(player);
        UpdateBadges();
        return true;
    }

    // What the staff set (/ahp) is the health from now on: not a hit to blame anyone for, and not put back.
    public void AcceptStaffHealth(PlayerId player, float health)
    {
        if (!_players.TryGet(player, out var watch))
            return;

        watch.Ward.Accept(health);
        watch.StaffWriteAt = _now;
    }

    private void Start()
    {
        _zone = new Zone(Settings.ExtraAreas, Settings.ExcludedAreas);
        IsOn = Settings.Enabled;
        online.Left += OnLeft;
        logger.LogInformation("[CyrodiilRP] Sanctuary {State}: {Count} areas, aggressors unprotected for {Seconds} s, bounty {Bounty}.",
            IsOn ? "on" : "off", _zone.Count, Settings.AggressorSeconds, Settings.Bounty);
    }

    [UpdateOrder(2)]
    private void Update()
    {
        using var scope = gameThread.Enter();
        watchdog.Run(TickJob, Tick);
    }

    private void Tick()
    {
        _now += Time.DeltaTime;
        if (!IsOn)
            return;

        See();

        _hurt.Clear();
        foreach (var seen in _seen)
            WatchPlayer(seen);

        GuardNpcs();

        foreach (var hurt in _hurt)
            Decide(hurt);

        EndAggression();
        CloseQuietIncidents();
        _npcs.Sweep(watch => Close(watch.Incidents));
        UpdateBadges();
    }

    // An open incident is logged, and a streak waits for the account to come back.
    private void OnLeft(OnlinePlayer player)
    {
        if (_players.Detach(player.Id, _now) is { } watch)
            Close(watch.Incidents);
    }

    // Who is where and who is fighting, in one pass, so nothing queries inside a query.
    private void See()
    {
        _seen.Clear();
        foreach (var main in entities.Query<MainCharacter>())
        {
            // The relay has the character a moment before the mode has the player.
            if (!online.TryGet(main.PlayerId, out var player))
                continue;

            var watch = _players.Attach(player.Id, player.ReadyMId, _now);
            watch.NoteFighting(main.AttackingRequest, main.CastingRequest, _now);
            _seen.Add(new Seen(player, watch, areas.Find(player.Id), main.Position, main.Hp, main.HpBase, main.IsDead, main.InCombat));
        }
    }

    private void WatchPlayer(Seen seen)
    {
        var watch = seen.Watch;
        if (seen.Area is not { } area)
            return;

        var inside = _zone.Contains(area);
        if (watch.Inside != inside)
        {
            if (watch.Inside is not null)
                notices.Zone(seen.Player, inside);

            watch.Inside = inside;
        }

        if (seen.Dead)
        {
            WatchDeath(seen, area, inside);
            return;
        }

        watch.DeathCounted = false;
        if (!inside)
        {
            watch.Ward.Accept(seen.Hp);
            return;
        }

        var lost = watch.Ward.Watch(seen.Hp, seen.HpBase);
        if (lost <= 0f)
            return;

        if (_now - watch.StaffWriteAt < StaffWriteSettleSeconds)
        {
            watch.Ward.Accept(seen.Hp);
            return;
        }

        var hit = Judge(seen.Position, area, seen.Player.Id);
        MarkAll(hit, seen.Player.Name, area);
        _hurt.Add(new Hurt(seen, area, lost, hit));
    }

    // A protected player dies only to one blow that takes all the health, an aggressor may die to any. Either way the
    // player gets up through the platform's own rebirth, as an innocent: the Sanctuary revives nobody.
    private void WatchDeath(Seen seen, string area, bool inside)
    {
        var watch = seen.Watch;
        var lostAll = watch.Ward.Kept ?? seen.HpBase;
        watch.Ward.Accept(0f);
        if (watch.Aggression.IsOn(_now))
        {
            watch.Aggression.Clear();
            notices.TellStaff(SanctuaryText.StaffAggressorDied(seen.Player.Name, _zone.NameOf(area)));
        }

        if (!inside || watch.DeathCounted)
            return;

        watch.DeathCounted = true;
        var verdict = Judge(seen.Position, area, seen.Player.Id);
        MarkAll(verdict, seen.Player.Name, area);
        Record(watch.Incidents, PlayerVictim(seen.Player), area, lostAll, died: true, verdict);
    }

    private void Decide(Hurt hurt)
    {
        var seen = hurt.Seen;
        var watch = seen.Watch;
        var shielded = !watch.Aggression.IsOn(_now) && (Settings.HealAllDamage || hurt.Verdict.Kind != SanctuaryVerdict.None);
        if (shielded && entities.TryLookup(seen.Player.Id, out MainCharacter main))
        {
            main.Hp = watch.Ward.RestoreTo(seen.HpBase);
            if (_now - watch.ShieldedAt >= ShieldedNoticeSeconds)
            {
                watch.ShieldedAt = _now;
                notices.Shielded(seen.Player);
            }
        }
        else
        {
            watch.Ward.Accept(seen.Hp);
        }

        Record(watch.Incidents, PlayerVictim(seen.Player), hurt.Area, hurt.Lost, died: false, hurt.Verdict);
    }

    // NPCs and guards inside are always untouchable, whoever hit them. One without a ref id has nothing stable to keep
    // its ward under, so it is not watched.
    private void GuardNpcs()
    {
        foreach (var npc in entities.Query<Npc>())
        {
            var refId = npc.RefId;
            if (refId == 0 || !entities.TryGetScope(npc, out Area scope))
                continue;

            var area = scope.AreaId.ToManaged();
            if (!_zone.Contains(area))
                continue;

            var watch = _npcs.Attach(refId);
            if (npc.IsDead)
            {
                // A single blow took it all, and the SDK cannot bring an NPC back.
                if (!watch.DeathCounted)
                {
                    watch.DeathCounted = true;
                    var verdict = Judge(npc.Position, area, null);
                    MarkAll(verdict, watch.Name, area);
                    Record(watch.Incidents, NpcVictim(watch), area, watch.Ward.Kept ?? npc.HpBase, died: true, verdict);
                }

                continue;
            }

            watch.DeathCounted = false;
            var lost = watch.Ward.Watch(npc.Hp, npc.HpBase);
            if (lost <= 0f)
                continue;

            npc.Hp = watch.Ward.RestoreTo(npc.HpBase);
            var hit = Judge(npc.Position, area, null);
            MarkAll(hit, watch.Name, area);
            Record(watch.Incidents, NpcVictim(watch), area, lost, died: false, hit);
        }
    }

    private Verdict<Seen> Judge(Vector3 victim, string area, PlayerId? victimPlayer) =>
        Blame.Judge(victim, _now, ListBystanders(area, victimPlayer), Settings.MeleeRange, Settings.RangedRange, Settings.AttackWindowSeconds);

    private IEnumerable<Bystander<Seen>> ListBystanders(string area, PlayerId? victim)
    {
        foreach (var other in _seen)
        {
            if (other.Player.Id == victim || other.Dead || other.Area != area)
                continue;

            yield return new Bystander<Seen>(other, other.Position, other.Watch.LastAttack, other.Watch.LastCast, other.InCombat);
        }
    }

    // Everyone Blame names gives up the protection: a lone attacker and every one of a group alike. Every attack starts
    // the clock again, only the first of a streak is announced.
    private void MarkAll(Verdict<Seen> verdict, string victim, string area)
    {
        foreach (var suspect in verdict.Suspects)
        {
            if (!suspect.Watch.Aggression.Mark(_now, Settings.AggressorSeconds))
                continue;

            notices.Aggressor(suspect.Player);
            AddBounty(suspect.Player.Id);
            notices.TellStaff(SanctuaryText.StaffAttacked(suspect.Player.Name, victim, _zone.NameOf(area)));
        }
    }

    private void AddBounty(PlayerId aggressor)
    {
        if (Settings.Bounty <= 0f || !entities.TryLookup(aggressor, out MainCharacter main))
            return;

        main.TryGetSocialAttributeValue((byte)CharacterSocialAttribute.Bounty, out var bounty);
        main.SetSocialAttribute((byte)CharacterSocialAttribute.Bounty, bounty + Settings.Bounty);
    }

    private void Record(IncidentTrack track, VictimOf victim, string area, float lost, bool died, Verdict<Seen> verdict)
    {
        var suspects = verdict.Suspects.Select(suspect => new IncidentPerson(suspect.Player.ReadyMId, suspect.Player.Name));
        if (track.Record(area, _now, lost, died, suspects, () => new OpenIncident(victim.Kind, victim.Player, victim.Name, area, _now)) is { } closed)
            Write(closed);
    }

    private void EndAggression()
    {
        foreach (var (id, watch) in _players.All)
        {
            if (watch.Aggression.Expired(_now) && online.TryGet(id, out var player))
                notices.ProtectedAgain(player);
        }
    }

    private void CloseQuietIncidents()
    {
        foreach (var watch in _players.All.Values)
        {
            if (watch.Incidents.CloseIfQuiet(_now, Settings.IncidentSeconds) is { } done)
                Write(done);
        }

        foreach (var watch in _npcs.All)
        {
            if (watch.Incidents.CloseIfQuiet(_now, Settings.IncidentSeconds) is { } done)
                Write(done);
        }
    }

    private void Close(IncidentTrack track)
    {
        if (track.Close() is { } done)
            Write(done);
    }

    private void Write(OpenIncident incident) => log.Write(incident, _zone.NameOf(incident.Area));

    // The badge of each player who is watched follows their place and streak, sent only when it changes.
    private void UpdateBadges()
    {
        foreach (var (id, watch) in _players.All)
        {
            var inside = IsOn && watch.Inside == true;
            var aggressor = watch.Aggression.IsOn(_now);
            var state = inside ? new HudState(true, aggressor, aggressor ? watch.Aggression.Until : double.NegativeInfinity) : HudState.Hidden;
            if (state == watch.Shown || !online.TryGet(id, out var player))
                continue;

            watch.Shown = state;
            notices.Badge(player, new SanctuaryHudView(state.Inside, state.Aggressor, aggressor ? SecondsLeft(watch) : 0));
        }
    }

    private int SecondsLeft(PlayerWatch watch) =>
        watch.Aggression.IsOn(_now) ? (int)Math.Ceiling(watch.Aggression.Until - _now) : 0;

    private static VictimOf PlayerVictim(OnlinePlayer player) =>
        new(SanctuaryVictim.Player, new IncidentPerson(player.ReadyMId, player.Name), player.Name);

    private static VictimOf NpcVictim(NpcWatch watch) => new(SanctuaryVictim.Npc, null, watch.Name);

    // One character as this tick saw it.
    private readonly record struct Seen(
        OnlinePlayer Player, PlayerWatch Watch, string? Area, Vector3 Position, float Hp, float HpBase, bool Dead, bool InCombat);

    // A player inside whose health dropped this tick, waiting for the decision.
    private readonly record struct Hurt(Seen Seen, string Area, float Lost, Verdict<Seen> Verdict);

    private sealed record VictimOf(SanctuaryVictim Kind, IncidentPerson? Player, string Name);
}
