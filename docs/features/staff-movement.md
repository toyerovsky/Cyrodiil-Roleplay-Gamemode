# Staff movement through walls

## 1. What it is

Two staff commands that pass what the game would stop a character at: a wall, a locked door, a floor. `/astep` is one
short teleport. `/afly` is a flight: the staff member becomes a camera nobody sees, which goes where they look.

## 2. Rules (as the staff see them)

- Both work on duty only and for moderators and above (permissions `astep`, `afly`).
- `/astep` moves 3 m the way the character faces, `/astep 8` moves 8 m, from 1 to 50 m. It lands a little above the
  ground, as every teleport does.
- `/astep up 5` and `/astep down 5` move straight up or down, to the very height.
- The way the character faces is its turn on the ground. Looking up or down does not change a step ahead.
- `/afly` turns a flight on and off. Flying, the character is hidden from everyone, the flyer included, and nothing
  stops it or pulls it down. It goes where the flyer looks: the game's move keys or the stick carry it ahead, back
  and sideways, looking up and pushing ahead climbs, and with no push it hangs. Shift hurries it. With the chat, a
  window of the mode or a menu of the game open, it hangs.
- A flyer takes no harm, and none for 15 seconds after the flight, which covers the fall from where it ended. A flyer
  the staff froze in the air hangs there unharmed, and the 15 seconds run from the unfreeze.
- Nobody finds a flyer in front of them or around them: the H wheel and the lore card's Nearby pass a hidden player
  over.
- Turned off, the character is where the flight took it, seen again, and falls if it is in the air.
- A flight ends with the duty, when the player leaves, and when the staff freeze the flyer. A frozen player does not
  take off.
- `/aback` returns from a step, as from any teleport, and from a flight to where it began.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/astep [up\|down] [metres]` | One short teleport | `Admin/TeleportCommands.cs`, `Teleports.TryPut` |
| `/afly` | Turns the flight on or off | `Admin/Flights.cs` (server), `Admin/Flight.cs` (client) |

## 4. How it works

- **A step ahead** is `FrontCone.FindAhead`: the position, the yaw and the distance. `Teleports.TryMove` lifts it by 1 m
  so the character does not land in the ground. Up and down use `Teleports.TryPut`, which does not lift.
- **A flight on the server** (`Flights`, `FlightRegistry`): the command sets three properties of the flyer's `Player`,
  which the platform replicates to every client: `IsHidden`, `IgnoresCollision` and `IsFrozen`. Landing sets them
  back. The server does not move the flyer. `IsFrozen` is also what a freeze sets (`Sanctions/Freezes.cs`), so the two
  never hold at once: `Freezes.Froze` lands a flyer, and landing leaves a frozen one frozen.
- **Who is not found**: `WheelTargets.Filter` leaves out a player whose `IsHidden` is set (`Proximity.IsHidden`).
- **A flight in the flyer's game** (`Flight` in the client): for as long as its own player both ignores collision and
  is frozen, it moves the character once a frame by `FlightRules.FindStep`: the way the camera looks
  (`ICamera.Look`), the game's move input (`IInput.MoveAxis`), `FlightRules.Speed` or `HurriedSpeed` while Shift is
  held (`IInput.IsKeyDown`). The position replicates as a walk's does, so the server and the chat's ranges follow the
  flyer.
- **Names over heads** need nothing of their own: a hidden character's `Hidden` reads 1, which `Nameplates` already
  fades a name by.
- **Harm** is refused in `Flights.CheckHarm` while `FlightRegistry.Spares` the struck player: in the air, held in
  the air by the freeze that ended the flight, and for `FlightRegistry.LandingGrace` once the fall begins, at the
  landing or at the unfreeze (`Freezes.Thawed`, `FlightRegistry.Drop`).
- **The way back**: `Flights` keeps the spot the flight began at as the one `/aback` returns to (`Teleports.KeepBefore`).
- **Audit**: a step is `admin.teleport` (`step Ahead 3 m`), a flight `admin.flight_start` and `admin.flight_end`,
  the end also for a flyer who leaves.

## 5. Not checked in the game

`/astep` was not tried with a player. A flight was: the character hides, hangs and passes through things. Still to
see:

- the flight by the game's move input since SDK 1.1.0-rp.b04915cf: that it reads for a frozen character, that left
  is left, and that it rests when the keys are let go (by W, A, S and D read as keys the flight was seen to work);
- whether every part of a hidden character goes, the first person arms and a drawn weapon above all;
- how the game takes a landing in the air, and whether 15 seconds cover the fall from a great height;
- a flyer frozen in the air and its fall after the unfreeze. One who leaves and rejoins while frozen there has no
  grace for that fall;
- what a door to another area, a mount and a death make of a flight.
