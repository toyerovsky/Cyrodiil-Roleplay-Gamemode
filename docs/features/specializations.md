# Specializations

## 1. What it is

Nine specializations, three in each of three paths, decide how far a character's skills and attributes grow, so no
character is good at everything and the people of Cyrodiil need each other. A character chooses one at a mentor of its
path or at the Imperial Registrar, and can change it later for gold, once a cooldown has passed.

## 2. Rules (as the player sees them)

- **The paths:** Warrior (Knight, Barbarian, Ranger), Mage (Wizard, Alchemist, Illusionist), Support (Healer, Smith,
  Thief).
- **Mastered, trained, limited:** each specialization masters a few attributes and skills, which grow to 100, and
  trains a few more, which stop at 60 (skills) or 75 (attributes). Every other skill stops at 30 and every other
  attribute at 50. Speed is everyone's and is never held. A Wizard masters Destruction and brews only to 60 in Alchemy;
  an Alchemist is the inverse; no mage goes past 30 in Blade, Blunt, Hand to Hand, Block or either armour.
- **Before the choice** every skill stops at 50, so nobody levels everything up first and chooses after.
- **What a cap stops:** a skill at its cap learns nothing from use (no level progress, no attribute bonus), a trainer
  refuses it before taking gold, a skill book keeps its lesson, and anything else that raises it is put back at once.
  A message in the corner says so.
- **Major skills:** the character's class takes 7 major skills from the specialization: every skill it masters, then
  trained ones. Majors learn faster and only they count towards the next level. The skills' values do not change.
  Before the first choice the character keeps the majors of the class they made.
- **Potions:** a potion the player made fortifies only what the specialization masters, and Speed. One that fortifies
  anything else is not drunk.
- **Mentors:** H over a mentor opens its window: the three specializations of its path with what each masters and
  trains, the character's own, the gold carried, and Choose. The Imperial Registrar teaches all nine.
- **The first choice** is free and taken at once. **A change** costs 1000 gold, waits the cooldown (`changeCooldownSeconds`, see 4) after the last
  choice, and asks first (Change it, or Keep mine). Skills and attributes above the new caps drop to them for good.
- **Where it shows:** the character's slot on the welcome screen, the character tab of the player's menu (F10, with
  where to get one while there is none) and the staff panel's card of a player, each in the reader's language.
- The window closes when the player walks more than 5 m from the mentor, leaves the world, or the mentor is taken
  away. A mentor is labelled over the head ("Warrior Mentor", "Mage Mentor", "Support Mentor", "Imperial
  Registrar") and, as a trader, cannot be killed for good.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/anpc here mentor warrior` (`mage`, `support`) | Staff: stands a mentor of that path where the staff member stands | `Traders/TraderCommands.cs` |
| `/anpc here registrar` | Staff: stands the Imperial Registrar, meant for the Imperial Palace | `Traders/TraderCommands.cs` |
| `/anpc remove`, `list` | Staff: takes the one in front away, lists them among the traders | `Traders/TraderCommands.cs` |
| H over a mentor or the registrar | Opens the window | `Specializations/Mentors.cs` |
| `/aspec <player> <specialization\|none>` | Staff: sets a character's specialization for free, without a cooldown, keeping the time of the last choice, or takes it away | `Specializations/SpecializationCommands.cs` |
| `/acap <player> <stat> <0-100\|clear>` | Staff: a cap on one attribute or skill in place of the specialization's, kept over restarts, or taken off | `Specializations/SpecializationCommands.cs` |
| `/astat <player> <stat> <0-100>` | Staff: sets an attribute's or a skill's value, at most its cap, for a character in the world | `Specializations/SpecializationCommands.cs` |
| Staff panel, a player's card | Specialization and caps: a chip per specialization (asked twice), every stat with its value and cap, Value, Cap and Clear | `ui/admin/SpecializationSection.vue` |
| The window | Choose, Change it or Keep mine, Esc or Leave | `ui/mentor`, `Specializations/MentorOverlay.cs` (client) |

There is no player command.

## 4. How it works

- **The specializations** are `SpecializationRules.Profiles` in the server, each a path, its mastered and its trained
  stats. The caps come from `specializations` in `server/config.json`: `trainedSkillCap` 60, `limitedSkillCap` 30,
  `trainedAttributeCap` 75, `limitedAttributeCap` 50, `unchosenSkillCap` 50, `firstPrice` 0, `changePrice` 1000,
  `changeCooldownSeconds` (left out of the shipped config: 30 s in a Debug build and 60 s in a Release one, short enough to test a change twice; a live server sets its own, such as 259200 for three days) and `potions` (`PlayerMade`, `All` or `Off`). A wrong value stops the mod at start.
- **The majors** come from `SpecializationRules.FindMajorSkills` and go with the caps. The window lists them.
- **The caps** go to the player through the SDK's `ISkillCaps` (oblivion-mod's `docs/features/skill-caps`), which the
  player's game holds where it raises a stat. `CharacterSpecializations` sets them as the character loads, after each
  choice, and again in the player's language when it changes.
- **The NPCs** are trader spots under shops of their own (`mentor.warrior`, `mentor.mage`, `mentor.support`,
  `registrar`), stood, kept, adopted and healed by `TraderNpcs`, each kind under an origin of its own
  (`TraderOrigin.WarriorMentorSpawnerRefId` to `RegistrarSpawnerRefId`) by which every client labels it.
- **Database:** `character_specializations` (key `character_id`, the specialization by name, the price of the last
  choice, when it was made, how many changes). `SpecializationBook` reads it and writes a choice over the one before.
- **Gold is never lost to a failed write** (`Mentors`): the price leaves the purse first, then the choice is written.
  If the write fails, the gold goes back.
- **The window** gets `MentorState` (a `MentorView`) as it opens and when the choice or the gold changes, and
  `MentorAnswer` after each choice.
- **Shown elsewhere:** `CharacterSpecializations.FindName` gives the name, translated where each view is built
  (`WelcomeCards`, `MenuCards`, `AdminViews.ReadCardAsync`). It raises `Changed` after a read or a choice, and a
  welcome already up is sent again, so a read that finishes after it went out still shows.
- **Staff fixes:** `character_cap_overrides` (key `character_id` and the stat by name, the cap 0 to 100) holds the staff's
  caps, applied over the specialization's in `SpecializationRules.BuildCaps`. A value goes to the game through the
  character's `SkillAttributes`, which the player's game applies to the base value; a value above the cap is refused
  first, as the game would put it back. The three commands are an administrator's (`StaffRoleSeeds`).
- **Audit:** `staff.specialization`, `staff.cap` and `staff.stat` with what was set and what was there before;
  `economy.specialization_choose`, with the specialization, the price and the one it replaced.

## 5. Not built yet

- The onboarding quest whose reward is the visit to the Imperial Registrar: for now the registrar is a person the staff
  stand in the palace.
- Skills of a specialization of its own (robes as their own skill): the game has none to cap.
- A level-up still offers a capped attribute; the points picked for it are lost.
