# Staff movement through walls

## 1. What it is

Two staff commands that pass what the game would stop a character at: a wall, a locked door, a floor. The platform gives
the server no way to switch a character's collision off, so there is no real noclip. Both are made of teleports: the
server sets the character's position, which nothing in the way stops.

## 2. Rules (as the staff see them)

- Both work on duty only and for moderators and above (permissions `astep`, `afly`).
- `/astep` moves 3 m the way the character faces, `/astep 8` moves 8 m, from 1 to 50 m. It lands a little above the
  ground, as every teleport does.
- `/astep up 5` and `/astep down 5` move straight up or down, to the very height.
- The way the character faces is its turn on the ground. Looking up or down does not change a step ahead.
- `/afly` turns a flight on and off. Flying, the character keeps the height it had and is carried the way it walks,
  through walls. `/astep up` and `down` change the height. Turned off, the character falls from where it is.
- A flight ends with the duty, and when the player leaves.
- `/aback` returns from a step, as from any teleport.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| `/astep [up\|down] [metres]` | One short teleport | `Admin/TeleportCommands.cs`, `Teleports.TryPut` |
| `/afly` | Turns the flight on or off | `Admin/Flights.cs` |

## 4. How it works

- **A step ahead** is `FrontCone.FindAhead`: the position, the yaw and the distance. `Teleports.TryMove` lifts it by 1 m
  so the character does not land in the ground. Up and down use `Teleports.TryPut`, which does not lift.
- **A flight** (`Flights`, `FlightRules`): every 50 ms, only while somebody flies, the server reads the way the flyer
  pushes (`ControlInputVector` of the character's movement, or `LastControlInputVector` when the game used it up this
  frame) and puts the character 30 cm on that way, at the height it holds (600 cm a second, on top of its own walking
  where nothing stops it). Standing still, the character is only put back at its height, which undoes the fall.
- **The height** a flyer holds is taken when the flight starts and after every teleport (`Teleports.Moved`), `/astep up`
  and `down` among them. In another area (through a door) the flyer holds the height they come out at.
- **Audit**: a step is `admin.teleport` (`step Ahead 3 m`), a flight `admin.flight_start` and `admin.flight_end`.

## 5. Not checked in the game

Neither command was tried with a player. For the flight this decides whether it is usable at all:

- whether the server sees the push of a walking player live, in both vectors or only one;
- whether a position set 20 times a second looks like flying or like rubber-banding;
- how the game takes a fall after `/afly` is turned off above the ground.

The speed and the interval are `FlightRules.Speed` and `FlightRules.MoveEvery`.
