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
  stops it or pulls it down. It goes where the flyer looks: W, S, A and D carry it ahead, back and sideways, looking
  up and pushing ahead climbs, and with no key it hangs. Shift hurries it. A key typed into the chat or a window, or
  pressed in a menu of the game, moves nothing.
- A flyer takes no harm, and none for 15 seconds after the flight, which covers the fall from where it ended.
- Nobody finds a flyer in front of them: the H wheel passes a hidden player over.
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
- **A flight in the flyer's game** (`Flight` in the client): for as long as its own player both ignores collision and
  is frozen, it moves the character once a frame by `FlightRules.FindStep`: the way the camera looks
  (`ICamera.Look`), the four keys (`IInput.IsKeyDown`, `FlightRules.FindPush`), `FlightRules.Speed` or `HurriedSpeed`
  while Shift is held. The position replicates as a walk's does, so the server and the chat's ranges follow the flyer.
- **Why the keys and not the game's move input**: `IInput.MoveAxis` is no number while a key is held and the
  character is out of the game's movement (seen in the game, SDK 1.1.0-rp.0f821c38). So the flight reads the keys
  themselves: a gamepad's stick and keys bound otherwise in the game do not fly.
- **Names over heads** need nothing of their own: a hidden character's `Hidden` reads 1, which `Nameplates` already
  fades a name by.
- **Harm** is refused in `Flights.CheckHarm` while `FlightRegistry.Spares` the struck player: in the air, and for
  `FlightRegistry.LandingGrace` after landing.
- **The way back**: `Flights` keeps the spot the flight began at as the one `/aback` returns to (`Teleports.KeepBefore`).
- **Audit**: a step is `admin.teleport` (`step Ahead 3 m`), a flight `admin.flight_start` and `admin.flight_end`.

## 5. Not checked in the game

`/astep` was not tried with a player. A flight was: the character hides, hangs and passes through things. Still to
see:

- the flight by the keys, since the game's move input turned out to be no number for a frozen character;
- whether every part of a hidden character goes, the first person arms and a drawn weapon above all;
- how the game takes a landing in the air, and whether 15 seconds cover the fall from a great height;
- what a door to another area, a mount and a death make of a flight.
