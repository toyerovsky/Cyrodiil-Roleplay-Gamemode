# Statuses in the SDK: what changed and where

Statuses let a ReadyM mod put Fortify, Drain, Damage and Restore effects (or any other magic effect) on a player's
character, for a time or until removed. The server owns them; the player's client has the game apply them.

- **Where:** oblivion-mod, branch `ai-playground`, commit "Implement IStatuses" (first written on top of
  `2026-10-23-mixin-replication-fix`, rebased onto main on 2026-11-04). The server services now take
  `ReadyM.SDK.Server.Events.IServerEvents`, which main moved them to.
- **Usage doc:** `docs/features/statuses/manual.md` in oblivion-mod.

## Layers

```
server mod ──IStatuses (server)──► PlayerStatusRegistry ──StatusRpcContracts──► client StatusService
                                                                                     │ (also IStatuses (client))
                                                                                     ▼
                                                     NativeLocalPlayerStatusControl ──binding──► LocalPlayerStatusControl
                                                                                                  (TES thread, the engine)
```

## Shared types: `OblivionMp.Sdk.Common`

| File | What | Why |
|---|---|---|
| `Statuses/Stat.cs` (new) | `Stat` (attributes, `Health`/`Magicka`/`Fatigue`, skills; values are the engine's actor value codes), `StatGroup`, `DisplayName()` | One enum for every number a status can act on, so `Fortify(Stat.Fatigue, ...)` and `Fortify(Stat.Strength, ...)` read the same. Not named `Attribute`, which collides with `System.Attribute`. |
| `Statuses/StatusEffect.cs` (new) | `StatusKind` (Fortify, Drain, Damage, Restore) and `StatusEffect` (4cc code, optional stat, magnitude, name). `Of(kind, stat, magnitude)` picks the game effect (FOAT, DRSP, DGHE, ...); `Custom("FTHR", 50)` takes any effect by code; `WithName`. | Generic and customizable, as asked. Refuses what the game cannot do (Damage/Restore on a skill, magnitude 0, unknown stat, bad code) at the call, not in game. |
| `Statuses/StatusHandle.cs` (new) | `StatusHandle`, `ActiveStatus` | A status is removed or looked up by handle. |
| `Statuses/StatusRegistry.cs` (new, internal) | One character's statuses with their expiry, on a clock the caller passes | Shared by both sides; pure, so it is unit tested. |
| `Statuses/StatusRpcContracts.cs` (new, internal) | `StatusesReady` (client to server), `StatusApplied`, `StatusRemoved`, `StatusesCleared` (server to client) | The server owns statuses; a client that joins late or reconnects asks with `StatusesReady` and gets a clean slate plus everything with the time left. |
| `AssemblyInfo.cs` | `InternalsVisibleTo` `OblivionMp.Sdk` and `OblivionMp.Sdk.Serverside.Tests` | The client SDK handles the internal contract; the tests reach the internal registries. |

## Server SDK: `OblivionMp.Sdk.Serverside`

| File | What | Why |
|---|---|---|
| `Statuses/IStatuses.cs` (new) | `IStatuses`: `Apply(player, effect[, seconds])`, `Remove`, `Clear`, `Active`; shorthands `Fortify/Drain/Damage/Restore(player, stat, magnitude[, seconds])` | The API a server mod such as HungerAndThirst injects. |
| `Statuses/PlayerStatusRegistry.cs` (new, internal) | Every player's statuses; sends through `IStatusSender`; `Resync`, `Forget`, `Expire` | The logic, testable without a relay (time is passed in). Handles never set the top bit, which the client keeps for its own. |
| `Statuses/StatusRpcHandlers.cs` (new, internal) | The generated sends, `StatusesReady` as an event | The transport, kept apart from the logic. |
| `Statuses/StatusService.cs` (new, internal) | `[Service]` that runs the registry on the server clock, resyncs on `StatusesReady`, forgets a player on disconnect; `StatusesApi` forwards `IStatuses` to it | A `[Service]` is registered by its own type only, so the public interface goes through a thin forwarder to the one instance. |
| `Mod.cs` | `services.RegisterSingleton<IStatuses, StatusesApi>()` | Makes `IStatuses` injectable. |

## Client SDK: `OblivionMp.Sdk`

| File | What | Why |
|---|---|---|
| `IStatuses.cs` (new) | Client `IStatuses` (local player): `Apply(effect[, seconds])`, `Remove`, `Active`, and the same shorthands | Client-only mods can use statuses too; `Active()` also lists the server's. |
| `Internal/StatusService.cs` (new) | `[Service]`: holds this client's statuses and the server's, ends timed ones on its clock, sends `StatusesReady` whenever the local player connects, hands everything to native | One place that turns statuses into native calls. Client handles have the top bit set, so they never meet the server's. |
| `Internal/StatusRpcClient.cs` (new) | The client end of the contract, as events | The service depends on it, not the other way round. |
| `Mod.cs` | `RegisterDelegate<IStatuses>(r => r.Resolve<StatusService>())` | Same single-instance reason as the server. |

## Managed game mod: `OblivionMpCSharpMod`

| File | What | Why |
|---|---|---|
| `NativeLocalPlayerStatusControl.cs` (new) | Binding wrapper: `Set(id, code, av, magnitude, name, seconds?)`, `Remove(id)` | Managed mods cannot reach the TES thread; every engine write in the SDK goes through native. |
| `NativeBinding.cs`, `DI.cs` | The binding field (after `MarkerControl`, same position as native), registration, `BindNative` | Standard binding exchange; its size is checked at `BindNative`. |

## Native mod: `OblivionMp`

| File | What | Why |
|---|---|---|
| `src/Control/LocalPlayerStatusControl.{h,cpp}` (new) | Each wanted status is one dynamic form with one effect, reconciled every TES tick in gameplay. See the kinds below. | The engine applies, ticks and shows the effect itself. |
| `src/DI.{h,cpp}`, `src/NativeBinding.h` | Construct the control, expose its binding | Wiring. |
| `src/Control/LocalPlayerSpellFormsControl.cpp` | Skip dynamic abilities | Status abilities are not player-made spells and must not be replicated as such. |
| `src/Oblivion/Addresses.h` | `MAGIC_CASTER_APPLY_ACTIVE_ITEM_PTR` (0x06653420), `EFFECT_SETTING_FLAGS_OFFSET` (0xB0) | Read from the binary for this work, with what each does. |

### How the native control applies a status, and why each choice

Every choice below was read from the game binary (addresses in the code) and then checked in game.

| Status | Form | Why |
|---|---|---|
| Held until removed, Fortify or harmless | `SpellItem`, type Ability, `Actor::AddSpell` | Shows with the constant effects. Reconciled against the player's spell list, so a load door never doubles it and a form found on the player is adopted. |
| Held until removed, hostile and Recover (Drain, Weakness) | `SpellItem`, type **Lesser Power**, applied like a timed one, 24 h duration topped up at half | On the player, an ability's Recover effect edits the **base** value (`ModBaseActorValue`, in `0x069A41B0`); Drain Fatigue 50 wrapped the base's 16-bit offset and showed fatigue 65,619/65,619. As a spell effect it moves the fortify stack instead, which cannot wrap. |
| Timed, harmless | `AlchemyItem`, `Actor::UsePotion(player, potion, null, directApply=1)` | Applied at once, no inventory, no animation; shows with the temporary effects and counts down. `ItemUseHook` ignores `directApply`, so it is not replicated as a drink. |
| Timed, hostile | `SpellItem`, type **Lesser Power**, set as the active magic item then `0x06653420` | A potion whose every effect is hostile is a **poison**: the engine hands its effects to `FindTouchTarget` (whoever the player faces) instead of the drinker, so they went nowhere or onto an NPC. A Spell would train its magic school on every apply (`UseSkill` in the cost step `0x065CA000`); a lesser power trains nothing and, at manual cost 0, takes no magicka. Flagged immune to silence. |
| (never) | `CastMagicItem` | For anything but an ability or disease it only sets the active item and starts the cast animation; nothing applies until a release that never comes, and the player is left stuck mid-cast (the first timed attempt did exactly that). |

Other rules in the control:

- **Early removal** finishes the form's active effects (`ActiveEffect::Finish`), found by the form's own `EffectItem`
  at `ActiveEffect+0x18`. (Matching `+0x38`, which the RVA atlas calls the magic item, against the form's MagicItem
  pointer found nothing in game.)
- **Menus:** the game logic keeps running in menus (`MenuFreezeBypassHook`), and the magic menu keeps pointers to the
  effects it lists, so an effect freed under it crashed the menu (`0x067758AF`, a Burden ending in the effects tab).
  While `IsEngineMenuMode()`, the control removes nothing, applies nothing, and holds any timed effect about to end a
  second ahead (its countdown climbs). A per-second effect (Damage, Restore) is set to magnitude 0 only once its real
  engine end has passed (Update applies `dt * [effect+0x28]`; RemoveEffect undoes nothing for these, so zeroing is
  safe). When the menu closes the duration goes back to its real end, or the effect ends at once if it is past it.
- **Busy casting:** an apply borrows the player's active magic item, so it waits while the player casts; a refused
  apply (`CannotCast`) is retried every 5 seconds. The player's selected spell or scroll is restored if it moved.
- **Names** are written with `LocalizationBypassHook::Marker` in front, as `GetNameToPublish` does for crafted potions,
  so they do not show as "[NL.]".

## Tests

`OblivionMp.Sdk.Serverside.Tests/Statuses/` (new): `StatusEffectTests`, `StatusRegistryTests`,
`PlayerStatusRegistryTests`. 137 pass with the existing suite (Debug and Release); the client suite
(`OblivionMpCSharpMod.Tests`) passes as before (116). The native control is verified in game only, per the repo's
UE4SS testing rule.

## Not done here

- **Fortify Health removal at low health can kill.** The engine's own removal leaves the player at 1 health through
  `ApplyDamageAVMod`, the mod forwards that to the server as a `HealthChanged`, and the server's float health can land
  below `KillHealth` (1.0) after rounding. It affects vanilla Fortify Health potions too; left for its own session.
- **Vanilla effects ending under the magic menu** can hit the same menu crash; only statuses are protected.
- **HungerAndThirst** uses statuses for its tiers (`../../HungerAndThirst`).
