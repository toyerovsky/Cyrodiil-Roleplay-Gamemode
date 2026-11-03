---
name: performance
description: How Cyrodiil RP keeps the server's tick short and what it sends to players small. Use before writing a timer, an event handler, a database call or anything sent to players.
---

# Performance

## The tick

- One update thread for every mod. Everything of ours on it runs through `Watchdog.Run` (callbacks, timers, awaits), which
  logs `[slow] <name> took N ms` past 5 ms, once per 30 s per name. A `[slow]` line is a bug to look at. The name is
  given where the work starts: `timer exchange.watch` (`timers.RunEvery("exchange.watch", ...)`), `rpc.pick_wheel
  RadialWheel` (the event `Callbacks.Raise` names, then the class that subscribed), `await accounts.join` (the name
  `Fire.Run` was given, for every await of that work).
- Database work never runs on the tick: `Persistence.Reader` (16 reads at once) and `Persistence.Writer` (one queue, in
  order) run it on the pool, `Fire.Run` brings the await back to a later tick.
- `GameThread` runs only the work queued when the tick began, so work that posts more cannot hold the tick.
- A timer over every player runs only as often as its result can change. Play time is written once a minute, the audit
  once a second in one batch, expired conditions are dropped once a second.
- Anything that must survive a restart is a row with an expiry checked at load, not a timer.

## What players get

- Chat lines go only to the players in range (`Proximity`), never to everyone and filtered later.
- A page gets a message on a change, not on a timer. The chat page asks for its history once, when it loads.
- The pages ship with the mod to every player: `check-dist` keeps them under its budget, and only the Latin font subsets
  go in.
- So do `CyrodiilRP.Client.dll` and `CyrodiilRP.Common.dll`: what only the server uses stays out of Common (the
  `code-rules` skill).

## The audit

`Audit.Log` buffers at most 10,000 entries and writes on a connection of its own, so a burst of staff actions never
delays the writer. A failed batch is put back and tried again.

See also the `code-rules` and `oblivionmp-platform` skills.
