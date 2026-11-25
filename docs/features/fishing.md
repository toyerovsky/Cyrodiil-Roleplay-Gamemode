# Fishing

## 1. What it is

A character with a fishing rod in the pack fishes in any water of the world it can wade into or stand by: lakes, rivers,
the sea, a city's canals, a cave's pool. "Fish" on the G wheel starts it. The cast takes power, the wait has its nibbles,
the bite wants a quick strike and the fight is directional: the fish runs and the rod leans against it. Where, when, in
what weather and on what bait decide which of thirteen species bite; a fishing skill from 1 to 100 grows with the
catches, and each character keeps its best fish of every species. A fisher buys the catch, sells rods, baits and the
lure, and mends rods.

## 2. Rules (as the player sees them)

- **The rod.** A fishing rod in the pack (not in hand: the game has no rod to hold). Three grades, each reaching farther
  with a stronger line and quicker bites: the plain Fishing Rod (7 m), the Sturdy (10 m) and the Master's (13 m). The
  general trader sells the plain one, the fisher all three. A rod wears after fights, Good, Worn, Damaged, then Broken,
  which does not fish; the fisher mends it. The best rod carried fishes: the highest grade, then the best condition.
  Without a whole one "Fish" on the wheel is shut and says why.
- **The bait.** "Bait" on the G wheel, shown while the pack holds any, opens a wheel of the baits carried with their
  counts and "No bait": Fishing Worms, Dough Bait, the game's Crab Meat, or the Spoon Lure. The choice stays the
  character's. Worms, dough and crab meat go one a cast; the lure stays on the line and goes only when the line snaps or
  the fish runs it out. A cast with a chosen bait the pack no longer holds goes with a bare hook and says so. A bare hook
  waits 30% longer, and each species likes its own bait.
- **Starting.** "Fish" on the G wheel. The player's game looks for water the rod reaches: ahead first, then to either
  side, then behind. A character on the bank faces the water a step or more ahead, one wading may face any way. Refused
  in the corner when no water is in reach, while swimming, out of the world, while held still (a staff member's freeze
  or flight), or while the character's fishing is still being read as it starts playing.
- **Held still.** While fishing the character stands where it began, facing the water, and the camera looks from over
  its right shoulder. E stops the fishing at any stage, so does "Stop fishing" on the wheel, a staff member's freeze, or
  the character being moved more than 3 m away. W, A, S and D do not stop it.
- **The cast.** The aim follows where the player looks, up to 60 degrees to either side. Holding Space swings a power
  meter up in 1.1 s and down again; letting go casts as far as the power gives, from 1.5 m to the rod's reach, onto the
  nearest open water at or before that point. The angler swings the rod and the float flies to the water.
- **The wait.** An untrained angler waits 5 to 20 s for a bite, less with skill, a better rod and a bait. About 3 s
  before the first nibble (or the bite) a fish shows under the water and circles the float. It may nibble up to five
  times: the float dips. A strike before the bite loses the fish ("Too soon", or "That was only a nibble" right after
  one).
- **The bite.** The float is pulled under with a splash and a ring closes over it. Space within the window strikes: 1 s,
  up to 1.3 s with skill. The press is the strike's: reeling starts with the next one.
- **The fight.** The fish rests, runs to the left, to the right, or dives away, and turns every 1 to 3 s; a page
  arrow, the float's drag and the splashes show which. A and D lean the rod: leaning against a run (D when it runs left,
  A when it runs right) eases the line and tires the fish fast, leaning with it strains the line. Space held reels: cheap
  while the fish rests, a heavy strain against a run. The line snaps at full tension, a line left slack for 2 s throws
  the hook, and a fish three times the rod's reach out has run the line out. A fresh fish reeled close bolts away; a
  tired one there is landed. Fish jump now and then. A plain rod and a quick hand land every species in 8 to 25 s; the
  rare ones are much safer on a better rod.
- **The catch.** The angler holds the fish up for about 3 s, seen by everyone near, and those in chat range read it as
  an action ("* Jan lands a pike, 74 cm.", the angler named as the reader knows them). The page shows a card: the
  species' painted icon, length, weight, trophy (the longest 15% of a species), a new personal record, the xp gained and
  a level reached; a rare fish (slaughterfish, glass catfish, Emperor's golden carp) plays a fanfare. About one bite in
  16 brings up a find instead: crab meat, scales or a pearl (a flawless one rarely). The catch goes into the pack, then
  the line is in again after 3.5 s (1.5 s after a loss).
- **Wear.** After each fight, landed or lost, the rod wears a step with a chance of 1 in 12 (plain), 18 (sturdy) or 26
  (master's), three times likelier when the line snapped, and a notice says so. A rod that broke with no other left ends
  the fishing.
- **The skill.** Level 1 to 100, from the xp of every catch: the species' own, more for a bigger one, half again for a
  trophy and half again the first time a species is caught; a find gives 3. From level 1 to 100 the wait for a bite
  shortens to 0.6 of itself, fish grow (the size's share is a roll to the power 1.8 down to 0.9), the strike's window
  widens by 0.3 s and the line takes 15% more. Some species bite only from a level: trout, eel, histcarp and sea bass
  from 5, the longfin from 8, the pike from 10, the slaughterfish from 20, the glass catfish from 30, the Emperor's
  golden carp from 45.
- **Where and when.** The water: Lake Rumare, Niben Bay, the sea (the Abecean, Topal Bay), the rivers (the Niben and its
  rivers, the Strid and the Brena), the other lakes (Arrius, Poppad, Canulus), any other water outside (streams, ponds)
  and anything underground. The hour: dawn 5:00 to 8:00, day to 18:00, dusk to 21:00, night. The area's weather. The
  commons live everywhere they suit; the Rumare carp and the golden carp in Lake Rumare; the histcarp in Niben Bay and the
  southern rivers, best in the rain; the longfin and the sea bass at sea; eels and catfish at night and underground; the
  slaughterfish in deep water at night; the trout in rivers, lakes and streams at dawn.
- **Seen by others.** Everyone within 60 m sees the angler hold the rod (its grade by the staff's mesh), the cast, the
  float, the fish circling, biting, on the line, jumping, held up or swimming off, and the splashes, and hears them.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| G wheel: Fish | asks the player's game for water and starts; shut without a whole rod | `Fishing/Anglers.cs` |
| G wheel: Bait | a wheel of the baits carried and "No bait"; shown while any is carried | `Fishing/BaitWheel.cs` |
| G wheel: Stop fishing | stops, as E does | `Fishing/Anglers.cs` |
| Space, A, D, E | read every frame while fishing, under no page that holds the keyboard | `Client/Fishing/` |
| The general trader: Fishing Rod | sells the plain rod | `Traders/stocks.json` |
| The fisher, "Services" | sells the catch, buys rods, baits and the lure, mends rods | `Fisher/` |

## 4. How it works

- **The server decides.** `FishingLine` (one angler's line) keeps every time and the whole fight on the server's clock
  and decides every catch, from rolls the caller passes, so the tests drive it: the player's game only says where the
  float landed, when Space went down and up and which way the rod leans (each at most once in 60 ms, a quicker change
  waiting its turn). A strike counts when the player's game heard it within the window after the bite and it reached the
  server within the window and 0.4 s.
- **What bites** is picked as the line goes in (`CatchPicker`): a find in `FishingRules.FindShare` of the bites, else a
  species weighted by its habitat (`FishHabitatCatalog`: its share of each water, a factor for the hour, the weather and
  the bait) above its level, sized by the skill. `FishingSurroundings` reads the water from `WaterMap` (the shipped
  `waters.json`: named waters as outlines in the server's centimetres, each with its surface's height; the first whose
  outline holds the float and whose surface lies within 3 m of the float's is the water), the hour from the world's
  `Time` and the weather from the area's `Weather` through `Admin/weathers.json`. Tamriel and the world spaces standing in
  it (the cities, the Imperial City's districts, Kvatch) use the map; any other area is underground.
- **The fight** (`FishFightRules`). The fish's effort is its strength x its size (0.7 to 1.3) x (0.4 + 0.6 x its
  stamina). Its next pull comes every 1 to 3 s by species, resting likelier as it tires (15% fresh, 65% spent), a run a
  dive in 30%; a turn to a run may bring a jump (the species' jumpiness). The line's tension moves towards what the
  moment asks for at 1 a second: in a run 0.15 + (0.35 sideways or 0.5 away, + 0.6 with the rod leaning the run's way,
  + 1.2 reeling) x effort / the line's strength, + 0.2 reeling, x 0.7 leaning against the run; at rest 0.4 while reeling,
  else it falls to nothing at 2 a second. The line's strength is the rod's (1, 1.25, 1.5, less worn) x the skill's
  tolerance. Stamina falls 1 / the species' stamina seconds a second within tension 0.15 to 0.85, 0.3 of that outside,
  1.8 times while the rod leans against the run. The reel brings the fish 90 cm a second at rest, a quarter of that
  against a run; it runs away 80 x effort and aside 20 x effort a second, dragging the float up to 3 m to the side.
  Snapped at tension 1, thrown off after 2 s at or below 0.05, run out past 3 x the rod's reach, landed within 1.5 m once
  its stamina is down to 0.35. Bots in `tests/CyrodiilRP.Tests/Fishing/FishingFightTests.cs` hold these numbers.
- **The skill and the records** (`FishingSkills`, `FishingSkillRules`) are read when the character starts playing
  (`online.Playing`), on the database's writer, and written whole after each catch: `character_fishing` (level, xp, fish
  landed, bait) and `character_fish_records` (per species the best length and weight, how many, when), through
  `Data/Fishing/FishingBook`.
- **The items.** The rods (three grades in four conditions), the mode's baits and the fish and their trophies are custom
  items `FishItems` defines as the server starts (`FishCatalog`, `RodCatalog`, `BaitCatalog`); `FishingStock` finds,
  counts, takes, gives and exchanges them in a pack. A rod wears by exchanging its item for the next condition's.
- **The page** (`ui/fishing`) is a picture over the game that takes no input: the power meter, what the line asks for,
  the ring over the float, the fight's tension, stamina, distance and the fish's pull, the rod, the bait and the skill,
  and the catch's card with the painted icons of `ui/shared/fishing`. The server sends it a `FishingView` on every
  change, every 100 ms in a fight.
- **What others see** (`Client/Fishing`): the server tells everyone near when an angler holds a rod and of what grade
  (`AnglerShown`), the cast (`AnglerCast`), where the float lies (`FishingFloat`, `FishingFloatGone`; in a fight the fish
  follows it under the water), what the fish does (`FishingFish`: Approach, Nibble, Bite, Hooked, Jump, Landed, Escaped,
  Gone at the next cast or the end) and where the water splashes (`FishingSplash`: Nibble, Bite, Thrash, Jump, Catch,
  Snap; each game plays the landing's own). Each game draws it with the game's staves for the rods, a kelp bladder for
  the float, the ambient fish's mesh, the staff's animations, the game's water effects (`IVisualEffects`) and sounds
  (`ISounds`). A find shows no fish.
- Messages: `FishingLook`, `FishingSpot`, `FishingAim`, `FishingPage`, `FishingBite`, `FishingEnded`, `FishingCast`,
  `FishingSpace`, `FishingLean`, `FishingStop`, and the five above.

## 5. Not checked in the game yet

None of it has run in the game. The SDK blocks it stands on were checked there from a probe before they were built
(`IWater`'s traces in a river and from a bridge, the projection, the splash and its sound). To check:

- the look for water from the bank, wading, from a pier and a bridge, in a lake, the sea and Bravil's canals;
- the waters map at its places: Lake Rumare by the Imperial City, Niben Bay at Bravil, the sea at Anvil, a river, a
  highland pond (Stream), a cave (Underground), and a city world (Bravil, Leyawiin) taking the map;
- the area's weather and the world's hour as the server reads them while fishing;
- whether the mouse still turns the aim while the camera is the mode's, and that Space does not make the frozen
  character jump;
- the rods on the hand (offset and turn on `Weapon_Socket`) for each grade, the cast, strike, fight and showing
  animations, and how others see them;
- the float's flight, its bobbing and dips, the fish circling, biting, following the float, jumping, held up and
  swimming off, the splashes' size and sound;
- the fight's feel: whether the numbers tuned by bots feel right in a player's hands, and whether the page and the
  splashes tell the runs in time;
- the items: the fish's names, icon and mesh when dropped, the rods' names, icon and staff mesh, the baits as items;
- the catch's chat line and its Polish wording, the notices of wear and bait.

## 6. Not done

- The fish as food for Hunger and Thirst and the fisher are the economy part's; see their docs.
- No leaderboard of the records yet: they are kept per character in the database.
