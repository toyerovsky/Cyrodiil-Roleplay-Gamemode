# Factions

## 1. What it is

One kind of lasting organisation for players: guilds, gangs, houses and companies. A faction has a name, ranks from the
top down, and a roll of members. It is stored in the database and stays over server restarts. A party is something
else: short-lived, in memory, with a panel on screen (`Party/`).

Trello: "Groups: founding, members, ranks, chat" (https://trello.com/c/zKxxljeX). The feature is called factions in the
code, the commands and the texts.

## 2. Rules (as the player sees them)

- A character is in one faction at most.
- Founding: `/faction create <name>` or the F10 menu's Faction tab, anywhere, for `factions.foundingFee` gold (500 by
  default) taken from what the character carries. The fee goes back if the write fails.
- A name has 3 to 32 characters: Latin letters and digits, starting with a letter, with single spaces, apostrophes and
  hyphens between them. It is unique whatever its letter case.
- A new faction has two ranks, named in the founder's language: the leader's rank on top, with every permission, and the
  entry rank at the bottom, which a new member gets. Neither can be deleted or moved.
- A faction makes ranks of its own between the two, up to 10 ranks in all. A rank's name has 2 to 24 characters.
- Permissions a rank may hold: `Invite`, `Kick`, `Ranks`.
- Who may do what goes by the rank's place:
  - a member is removed, or given a rank, only by one of a higher rank who holds the permission;
  - a rank is changed, moved, deleted or given only if it stands below the actor's own;
  - a permission is given or taken only by one who holds it;
  - a leader may make another member a leader, and steps down only once there is another leader;
  - a leader renames every rank, the leader's own too.
- A new rank stands right above the entry rank. A deleted rank leaves its holders at the entry rank.
- Leaving: the only leader cannot leave while others are in. The last member may, and the faction is dissolved.
- An invitation shows as a card at the top of the screen (Y joins, N declines) for 30 s and is good for 5 minutes, on
  the Faction tab or with `/faction accept`. One invitation a player at a time. It ends when either of the two players
  leaves the game, and with its faction.
- A faction holds 100 members at most.
- `/f <text>` reaches the faction's members in the game, wherever they are: `[Fighters Guild] Marcus Varro: text`.
- Inside a faction members read each other's names from the roll: its notices and `/f` use the character's name, not the
  race a stranger sees. The names over heads do not change.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/faction` | Opens the F10 menu on the Faction tab | `Faction/FactionCommands.cs` |
| `/faction create <name>` | Founds a faction | `FactionCommands.Found`, `Factions.FoundAsync` |
| `/faction invite <number>`, `accept`, `decline` | An invitation and its answer | `FactionInvites.cs`, `FactionInviteRegistry.cs` |
| `/faction kick <member>`, `leave` | Off the roll | `FactionCommands.cs` |
| `/faction rank <member> <rank>` | Gives a rank | `FactionRankCommands.Assign`, `FactionRules.CheckAssign` |
| `/faction perm <rank> <invite\|kick\|ranks> [on\|off]` | Gives or takes a permission, turns it without on or off | `FactionRankCommands.Grant`, `FactionRules.CheckGrant` |
| `/faction rankadd`, `rankdel`, `rankname`, `rankmove <rank> <up\|down>` | The ranks themselves | `FactionRankCommands.cs`, `FactionRules.cs` |
| `/faction info` | Name, ranks and the members in the game, in the chat | `FactionCommands.Describe` |
| `/f <text>` | Faction chat (`ChatLineKind.Faction`) | `FactionCommands.Speak` |
| H wheel | "Invite to the faction" (`player.faction_invite`, Order 45), for a rank that invites | `FactionInvites.cs` |
| F10 | Tab `faction`: found one or answer an invitation, or the roll and the ranks with their buttons | `ui/menu/FactionTab.vue` |
| `/afaction dissolve <faction> <reason>` | Staff: dissolves, the reason in the audit log | `Faction/FactionStaffCommands.cs` |
| `/afaction rename`, `lead`, `remove` | Staff: a new name, a member made a leader, a member off the roll | `FactionStaffCommands.cs` |
| `/admin` | Tab `factions`: every faction, one faction's ranks and roll, and the buttons of the three rows above | `ui/admin/FactionsTab.vue` |

A member is named by a player number, by the full name, or as `#<character id>`. A rank and a faction are named by name
or as `#<number>`. Names hold spaces, so the longest name the text starts with is the one meant (`FactionTargets.cs`).
The pages always send the `#` form.

The staff read the factions only in the panel: its Factions tab is for everyone the panel opens for, as the audit and
the roll log are. Changing one needs the permission `afaction` (administrators) and the duty, as the tab's buttons run
`/afaction`.

## 4. How it works

- **Database** (`CyrodiilRP.Data`): `factions` (name, `name_key` unique), `faction_ranks` (key `faction_id, number`,
  `position` from the top, `kind` Leader, Custom or Entry, `permissions` as `text[]`), `faction_members` (key
  `character_id`, so one faction a character). `FactionBook` reads and writes them.
- **Memory**: `Factions.Load` reads every faction once as the mod starts (`Mod.Init`). `FactionRegistry` holds
  `FactionRecord`s for the update thread, so a permission check, `/f` and the menu never wait for the database. Every
  change goes through `Factions`, which makes it in memory and queues it to the writer in the same order. Founding
  alone waits for the database, for the faction's id, with the name held meanwhile.
- **Commands**: `FactionCommands` picks the verb of `/faction` and keeps founding, removing, leaving and the listing.
  The invitations are `FactionInvites`' and the ranks `FactionRankCommands`'.
- **Rules**: `FactionRules` is the one place that says who may do what. The commands refuse by it, and `FactionViews`
  asks it for every "can" the pages get, so a page offers nothing the server would refuse.
- **The Faction tab**: its buttons send `/faction ...` lines (`MenuFaction` -> `FactionRun`). `FactionMenu` runs them
  through `CommandRegistry` with itself as the output and answers `FactionAnswer(text, done, menu)`: the answer and the
  menu as it is after it. The client puts both on the page, or the answer in the corner if the menu closed meanwhile.
  The lines count against the chat's rate limit.
- **The staff's tab**: asks `AdminParts.Factions` and `AdminParts.Faction` and runs `/afaction` through the panel's own
  `AdminRun`. A faction gone since the list was read comes back without its card (`StaffFactionLookupView`), and the
  card closes.
- **Audit**: `faction.found` (name, fee), `faction.end` (the last member left), `faction.dissolve`, `faction.rename`,
  `faction.lead`, `faction.remove`.

## 5. Not built yet

- The scribe: founding is not tied to a place (https://trello.com/c/S5kRwh65).
- The bank and the city treasury: the fee is taken from carried gold and leaves the game
  (https://trello.com/c/mD6oOFZx, https://trello.com/c/25Uv1QC2).
- The faction's treasury and its permission (https://trello.com/c/JzgcNW3I).
- The Faction tab is brought up to date by the reader's own commands. A change another member makes shows the next
  time the menu opens.
- The tabs use medallions the pages already had (`players.webp` in the menu, `account.webp` in the panel).
