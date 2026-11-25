# Fishing

## 1. What it is

A character with a fishing rod in the pack fishes in any water of the world it can wade into or stand by: lakes, rivers,
the sea, a city's canals. "Fish" on the G wheel starts it. The cast, the wait, the strike and the landing are played with Space; what comes out
of the water goes into the pack.

## 2. Rules (as the player sees them)

- **The rod.** A fishing rod in the pack (not in hand: the game has no rod to hold). The general trader sells it among
  the tools for 25 gold; like any item it can be traded, auctioned or dropped. Without one "Fish" on the wheel is shut
  and says why, and a character that no longer carries one when it casts stops fishing.
- **Starting.** "Fish" on the G wheel. The player's game looks for water the rod reaches: ahead first, then to either
  side, then behind. A character on the bank faces the water a step or more ahead, one wading may face any way. Refused
  in the corner when no water is in reach, while swimming, out of the world, or while held still (a staff member's
  freeze or flight).
- **Held still.** While fishing the character stands where it began, facing the water, and the camera looks from over
  its right shoulder. A move key (W, A, S, D) stops the fishing, so does "Stop fishing" on the wheel, a staff member's
  freeze, or the character being moved more than 3 m away.
- **The cast.** The aim follows where the player looks, up to 60 degrees to either side of the way the character faces.
  Space casts the float onto the water there, as far as the rod reaches (4.5 m), nearer when the water there is too
  shallow or covered (under a quay, a pier's planks, a bridge or a rock), never less than 1.5 m.
- **The wait.** A fish bites 5 to 20 s after the cast. Before that it may nibble up to five times: the float dips and the
  page says so a moment. A strike before the bite loses the fish ("Too soon", or "That was only a nibble" right after one).
- **The bite.** The float goes under with a splash and a ring closes over it on the page. Space within 1 s strikes.
- **The landing.** Holding Space lifts the rod and bends it, letting go lowers it; the fish pulls one way and the other.
  Kept in the green band the fish tires and is landed after 6 to 10 s in it. Bent to the end the line breaks; slack for
  1.5 s the fish slips off. The float is dragged about under the water and the fish splashes.
- **The catch.** One landed bite in 12 or so brings up a find, the game's crab meat, scales or a pearl (a flawless one
  rarely). The rest are fish, sized and weighed: perch, trout, carp, pike or eel, small ones more often, the longest
  15% of a kind a trophy. The catch goes into the pack and a card shows it. Then the line is in again after 2 s (1 s
  after a loss).
- **Seen by others.** Everyone within 60 m sees the angler hold the rod, the float on the water and the splashes, and
  hears them.

The rod and the fish are items of the mode's own (custom items): the rod a copy of the game's oar with its icon, shown on
the ground as the staff the angler holds, the fish copies of the game's crab meat. They stack, sell, trade and go to the
auction house like any other. The fish are not food for Hunger and Thirst yet.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| G wheel: Fish | asks the player's game for water and starts; shut without a rod | `Fishing/Anglers.cs` |
| The general trader: Fishing Rod | sells the rod | `Traders/stocks.json` |
| G wheel: Stop fishing | stops, as a move key does | `Fishing/Anglers.cs` |
| Space, W A S D | read every frame while fishing, under no page that holds the keyboard | `Client/Fishing/FishingOverlay.cs` |

## 4. How it works

- **The server decides.** `FishingLine` (one angler's line) keeps every time on the server's clock and decides every
  catch: the player's game only says where the float landed and when Space went down and up. A strike counts when
  the player's game heard it within the window after the bite and it reached the server within the window and 0.4 s.
  The numbers are `FishingRules`, the fish and finds with their shares too. `FishItems` defines the rod and the fish
  as the server starts and looks for the rod in the pack when the wheel opens, when the fishing starts and at every cast.
- **The rod at the trader.** A trader's ware may be a custom item: `stocks.json` names it under `custom` (its key, its
  weight, and for the page its name and the game item whose icon it shows) with a price of its own. The research's
  `export_wares.py` bakes its name and icon with the game's wares.
- **The water** is the player's game's, through the SDK's `IWater`: a look straight down finds the surface and the bed,
  and whether something stood over the water first (`Covered`). Only open water takes the float.
  The server checks a cast against where the character stands and the water's height it began at.
- **The page** (`ui/fishing`) is a picture over the game that takes no input: what the line asks for now, the ring over
  the float (`ICamera.TryProject`), the bend meter and the catch's card. The server sends it a `FishingView`.
- **What others see** (`AnglerScenery`): the server tells everyone near when an angler holds a rod, where the float
  lies and where the water splashes (`AnglerShown`, `FishingFloat`, `FishingSplash`), and each game draws it: the
  game's plain staff on the hand (it has no rod), a quill for the float, the staff's hold played on the upper body, the
  game's water splashes (`IVisualEffects`) and sounds (`ISounds`).
- Messages: `FishingLook`, `FishingSpot`, `FishingAim`, `FishingPage`, `FishingBite`, `FishingEnded`, `FishingCast`,
  `FishingSpace`, `FishingStop`, and the three above.

## 5. Not checked in the game yet

None of it has run in the game. The SDK blocks it stands on were checked there from a probe before they were built
(`IWater`'s traces in a river and from a bridge, the projection, the splash and its sound). To check:

- the look for water from the bank, wading, from a pier and a bridge, in a lake, the sea and Bravil's canals;
- whether the mouse still turns the aim while the camera is the mode's (`ICamera.Look` under `ICamera.Set`);
- that Space does not make the frozen character jump;
- the staff on the hand (offset and turn on `Weapon_Socket`), the staff's hold and how others see both;
- the float's quill on the water, its glide, the splashes' size and sound;
- the fish items: name, icon (the scales'), the ambient fish's mesh when dropped;
- the rod item: name, the oar's icon, the staff's mesh when dropped, the trader's sale.

## 6. Not done

- The rod does not wear out or break. A fisher who buys fish, and the fish as food for Hunger and Thirst are the next
  steps.
- One table of fish for every water; no kinds by region or by lake and river.
