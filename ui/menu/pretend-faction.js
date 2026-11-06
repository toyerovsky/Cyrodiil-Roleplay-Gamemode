import texts from "../../locales/en/faction.json";
import { fill } from "../shared/locale.js";

const MINUTE_MS = 60_000;
const DAY_MS = 24 * 60 * MINUTE_MS;
const FEE = 500;
const TAKEN = "Fighters Guild";
const ME = 1;

const say = (key, args) => fill(texts[`faction.${key}`], args);
const done = (key, args) => ({ text: say(key, args), done: true });
const refuse = (key, args) => ({ text: say(key, args), done: false });

// The Fighters Guild of Chorrol, as its Master (#faction) or as an Associate who only reads its roll (#factionmember).
function buildGuild(now, self) {
  const member = (id, name, rank, online, daysAway = 0) => ({ id, name, rank, online, lastSeenAt: now - daysAway * DAY_MS });
  return {
    id: 7,
    name: TAKEN,
    foundedAt: now - 21 * DAY_MS,
    self,
    ranks: [
      { number: 1, name: "Master", kind: "Leader", permissions: [] },
      { number: 3, name: "Champion", kind: "Custom", permissions: ["Invite", "Kick", "Ranks"] },
      { number: 4, name: "Warder", kind: "Custom", permissions: ["Invite"] },
      { number: 2, name: "Associate", kind: "Entry", permissions: [] },
    ],
    members: [
      member(ME, "Marcus Varro", 1, true),
      member(2, "Modryn Oreyn", 3, true),
      member(3, "Burz gro-Khash", 4, false, 2),
      member(4, "Ancondilwen Sarethi-Telvanni", 4, true),
      member(5, "Lum gro-Baroth", 2, self === 5, 1),
      member(6, "Vigdis", 2, false, 9),
    ],
  };
}

// The faction tab of the pretend menu: the server's rules of who may do what, and its answers, close enough to try
// every button. #factionless: no faction, an invitation to answer, and a name already taken to be refused with.
export function pretendFaction(scenario, now) {
  let faction = null;
  let invite = null;
  if (scenario === "faction") faction = buildGuild(now, ME);
  if (scenario === "factionmember") faction = buildGuild(now, 5);
  if (scenario === "factionless") invite = { faction: "Mages Guild", by: "Breton", leftMs: 4 * MINUTE_MS };

  const me = () => faction.members.find((member) => member.id === faction.self);
  const findRank = (number) => faction.ranks.find((rank) => rank.number === number);
  const place = (number) => faction.ranks.findIndex((rank) => rank.number === number);
  const allows = (rank, permission) => rank.kind === "Leader" || rank.permissions.includes(permission);
  const mine = () => findRank(me().rank);
  const countLeaders = () => faction.members.filter((member) => findRank(member.rank).kind === "Leader").length;
  const isBelowMe = (rank) => place(rank.number) > place(me().rank);
  const mayGive = (rank) => allows(mine(), "Ranks") && (isBelowMe(rank) || (rank.kind === "Leader" && mine().kind === "Leader"));
  const mayChange = (rank) => allows(mine(), "Ranks") && isBelowMe(rank);
  const mayMove = (rank, step) => {
    const to = place(rank.number) + step;
    return mayChange(rank) && rank.kind === "Custom" && to >= 1 && to <= faction.ranks.length - 2 && to > place(me().rank);
  };
  const mayAssign = (member, rank) => {
    if (!allows(mine(), "Ranks") || member.rank === rank.number) return false;
    if (member.id === me().id) return mine().kind === "Leader" && countLeaders() > 1;
    return place(me().rank) < place(member.rank) && mayGive(rank);
  };
  const mayLeave = () => !(mine().kind === "Leader" && countLeaders() === 1 && faction.members.length > 1);

  function card() {
    const sorted = [...faction.members].sort((a, b) => place(a.rank) - place(b.rank) || a.name.localeCompare(b.name));
    return {
      id: faction.id,
      name: faction.name,
      foundedAt: faction.foundedAt,
      rank: me().rank,
      canInvite: allows(mine(), "Invite"),
      canAddRank: allows(mine(), "Ranks") && mine().kind !== "Entry",
      canLeave: mayLeave(),
      grantable: allows(mine(), "Ranks") ? ["Invite", "Kick", "Ranks"].filter((permission) => allows(mine(), permission)) : [],
      ranks: faction.ranks.map((rank) => ({
        number: rank.number,
        name: rank.name,
        kind: rank.kind,
        permissions: rank.permissions,
        members: faction.members.filter((member) => member.rank === rank.number).length,
        canRename: allows(mine(), "Ranks") && (mine().kind === "Leader" || isBelowMe(rank)),
        canEdit: mayChange(rank),
        canDelete: mayChange(rank) && rank.kind === "Custom",
        canMoveUp: mayMove(rank, -1),
        canMoveDown: mayMove(rank, 1),
        canGive: mayGive(rank),
      })),
      members: sorted.map((member) => ({
        id: member.id,
        name: member.name,
        rank: member.rank,
        online: member.online,
        lastSeenAt: member.lastSeenAt,
        self: member.id === me().id,
        canKick: allows(mine(), "Kick") && member.id !== me().id && place(me().rank) < place(member.rank),
        canRank: faction.ranks.some((rank) => mayAssign(member, rank)),
      })),
    };
  }

  const readNumber = (word) => Number(word.slice(1));

  function found(name) {
    if (name.toLowerCase() === TAKEN.toLowerCase()) return refuse("err.name_taken", { name });
    faction = {
      id: 8,
      name,
      foundedAt: now,
      self: ME,
      ranks: [
        { number: 1, name: say("rank.leader"), kind: "Leader", permissions: [] },
        { number: 2, name: say("rank.member"), kind: "Entry", permissions: [] },
      ],
      members: [{ id: ME, name: "Marcus Varro", rank: 1, online: true, lastSeenAt: now }],
    };
    invite = null;
    return done("founded", { name, fee: FEE });
  }

  function join() {
    faction = buildGuild(now, 5);
    faction.name = invite.faction;
    faction.members.find((member) => member.id === ME).name = "Raminus Polus";
    faction.members.find((member) => member.id === 5).name = "Marcus Varro";
    invite = null;
    return done("joined", { faction: faction.name, rank: findRank(me().rank).name });
  }

  function run(line) {
    const [, verb = "", ...words] = line.split(" ");
    const rest = words.join(" ");
    if (verb === "create") return found(rest);
    if (verb === "accept") return join();
    if (verb === "decline") {
      const declined = invite.faction;
      invite = null;
      return done("you_declined", { faction: declined });
    }

    const rank = verb === "rank" ? findRank(readNumber(words[1])) : findRank(readNumber(words[0] ?? "#0"));
    const member = faction.members.find((each) => each.id === readNumber(words[0] ?? "#0"));
    switch (verb) {
      case "kick":
        faction.members = faction.members.filter((each) => each !== member);
        return done("you_kicked", { name: member.name });
      case "rank":
        member.rank = rank.number;
        return done("rank_set", { name: member.name, rank: rank.name });
      case "perm": {
        const permission = ["Invite", "Kick", "Ranks"].find((each) => each.toLowerCase() === words[1]);
        const grant = words[2] === "on";
        rank.permissions = rank.permissions.filter((each) => each !== permission).concat(grant ? [permission] : []);
        return done(grant ? "perm.granted" : "perm.taken", { rank: rank.name, permission: say(`perm.${words[1]}`) });
      }
      case "rankadd":
        if (faction.ranks.some((each) => each.name.toLowerCase() === rest.toLowerCase())) return refuse("err.rank_name_taken", { name: rest });
        faction.ranks.splice(faction.ranks.length - 1, 0, { number: Math.max(...faction.ranks.map((each) => each.number)) + 1, name: rest, kind: "Custom", permissions: [] });
        return done("rank.added", { name: rest });
      case "rankdel": {
        const entry = faction.ranks.at(-1);
        faction.members.filter((each) => each.rank === rank.number).forEach((each) => (each.rank = entry.number));
        faction.ranks = faction.ranks.filter((each) => each !== rank);
        return done("rank.deleted", { rank: rank.name, entry: entry.name });
      }
      case "rankname": {
        const old = rank.name;
        rank.name = words.slice(1).join(" ");
        return done("rank.renamed", { old, name: rank.name });
      }
      case "rankmove": {
        const from = place(rank.number);
        faction.ranks.splice(from, 1);
        faction.ranks.splice(from + (words[1] === "up" ? -1 : 1), 0, rank);
        return done(words[1] === "up" ? "rank.moved_up" : "rank.moved_down", { rank: rank.name });
      }
      case "leave": {
        const left = faction.name;
        const last = faction.members.length === 1;
        faction = null;
        return done(last ? "left_last" : "left", { faction: left });
      }
      default:
        return refuse("use.list");
    }
  }

  return { view: () => ({ fee: FEE, invite, faction: faction ? card() : null }), run };
}
