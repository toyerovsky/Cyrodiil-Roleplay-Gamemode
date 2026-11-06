# Foliage, props, animations, weapon hits and weapon wear in the SDK: what changed and where

The SDK APIs the Jobs mod needs. `IFoliage` finds the instanced meshes baked into the map (vanilla trees, bushes,
rocks) and hides one for everyone. `IAnimations` plays a montage or an animation sequence on the local player's
character, in both views; other players see a montage. `IWeaponHits` reports where the local player's weapon lands,
so a tree comes down on a real hit instead of a timer. `IProps` places a static mesh for everyone (the stump and log
a felled tree leaves). `ILocalProps` places one on a single client that can move every frame (a felled tree's copy as
it falls) or hang on a character's socket (a pickaxe in hand), and `IFoliage.TryGetTransform` says exactly where an
instance stood. `IEquippedWeapon` wears down the local
player's weapon.

- **Where:** oblivion-mod, branch `rp-framework`, the commit after "Implement IStatuses" (first written on
  top of it, rebased onto main on 2026-11-04). The server services now take
  `ReadyM.SDK.Server.Events.IServerEvents`, which main moved them to. The relay must be built from the
  `readym-gameserver` commit that branch pins.

## Why trees need their own API

A vanilla tree is not a TES-paired actor. `UTESObjectTREE` carries `TSoftObjectPtr<UFoliageType> Foliage`, and
`ULandscapeConfig::TreesFoliage` (`TMap<FString, UFoliageType*>`, beside the texture-to-landscape-layer maps) is the
editor import config that turned placed trees into foliage instances. So the TREE reference under a tree can be
disabled and nothing on screen changes. `QueueRefDisable`, which hides furniture and flora, does not reach them.

What does: `UInstancedStaticMeshComponent::RemoveInstance` on the instance, from the game thread, and `AddInstance`
with the saved transform to put it back. The first version moved the instance 1 km down with
`UpdateInstanceTransform`; in game the tree stayed standing, and that version logged nothing to say why.

## Layers

```
server mod ──IFoliage (server)──► HiddenFoliageRegistry ──FoliageRpcContracts──► client FoliageService
                                                                                     │ (also IFoliage (client): FindNear)
                                                                                     ▼
                                                              NativeFoliageControl ──binding──► FoliageControl
                                                                                                 (game thread, UE)

client mod ──IAnimations──► NativeLocalPlayerAnimationControl ──binding──► LocalPlayerAnimationControl ──► AnimationControl
                                                                                  (montage, or sequence in the UpperBody slot)

server mod ──IProps (server)──► PropRegistry ──PropRpcContracts──► client PropService ──► NativePropControl ──► PropControl
                                                                                     (StaticMeshActor per prop, game thread)

client mod ◄──IWeaponHits── WeaponHitService ◄──binding── LocalWeaponHitControl ◄── ProcessEvent: AVImpactSystemVFXBlueprint::OnStartPlay
```

## Shared types: `OblivionMP.SDK.Common`

| File | What | Why |
|---|---|---|
| `Foliage/FoliageId.cs` (new) | `FoliageId(Mesh, X, Y, Z)`: mesh object path and location in whole centimeters | Instance indices change when a part of the world streams back in; mesh plus rounded location does not, and is the same on every client, so it can be sent and saved. |
| `Props/Prop.cs` (new) | `Prop(Asset, Position, Rotation, Scale)`: a TES form's UE asset or a StaticMesh, placed in world space | |
| `Props/UnrealRotator.cs` (new) | `ToQuaternion(rotator)`, `FromQuaternion(q)`, as FRotator::Quaternion and FQuat::Rotator | A prop takes a rotator; turning one about an axis (a tree tipping over) is done on quaternions. UE4SS's own `FRotator::Quaternion` has the pitch sign the other way from the engine's (`Y = CR*SP*CY - ...`), so it is not used. |
| `Props/PropRpcContracts.cs` (new, internal) | `PropsReady`, `PropPlaced`, `PropRemoved`, `PropsCleared` | Same shape as the foliage contract. |
| `Foliage/FoliageRpcContracts.cs` (new, internal) | `FoliageReady` (client to server), `FoliageHidden`, `FoliageShown`, `FoliageCleared` (server to client) | Same shape as the statuses contract: a client that joins late or reconnects asks, and gets a clean slate plus everything hidden. |

## Server SDK: `OblivionMP.SDK.Serverside`

| File | What | Why |
|---|---|---|
| `Foliage/IFoliage.cs` (new) | `Hide`, `Show`, `IsHidden`, `Hidden` | What a server mod injects. Not saved: a mod that needs it across restarts keeps its own list (Jobs does). |
| `Foliage/HiddenFoliageRegistry.cs` (new, internal) | the hidden set and the ready players; sends through `IFoliageSender` | The logic without a relay, so it is unit tested. |
| `Foliage/FoliageRpcHandlers.cs`, `Foliage/FoliageService.cs` (new, internal) | the generated sends, `FoliageReady` as an event; the `[Service]`, and `FoliageApi` forwarding `IFoliage` | As statuses: a `[Service]` is registered by its own type only. |
| `Props/IProps.cs`, `Props/PropRegistry.cs`, `Props/PropRpcHandlers.cs`, `Props/PropService.cs` (new) | `Place(id, prop)`, `Remove`, `TryGet`, `Ids`; the registry, sends and `[Service]` as for foliage | Props are named by an id the mod picks, so a mod can find and remove its own; not saved, like hidden foliage. |
| `Mod.cs` | `services.RegisterSingleton<IFoliage, FoliageApi>()`, `IProps` to `PropsApi` | Makes both injectable. |
| Tests: `Foliage/HiddenFoliageRegistryTests.cs` (new) | resync order, who is told, idempotent hide and show | |
| `Props/UnrealRotatorTests.cs` (new) | yaw turns forward to the right, pitch up, roll tips the right side down; roll, pitch then yaw; round trips, straight up and down included | |
| `Props/PropRegistryTests.cs` (new) | resync order, who is told, replacing under an id, refusing an empty asset or a bad scale | |

## Client SDK: `OblivionMP.SDK`

| File | What | Why |
|---|---|---|
| `IFoliage.cs` (new) | `FindNear(center, radius, meshFilter, max)`, `IsHidden`; `FoliageInstance` with the instance's own scale and its size as it stands (`Radius` on the ground plane, the world heights of its `Top` and `Bottom`, from the mesh's bounding box turned and scaled by the instance); `TryGetTransform(id)`, a `FoliageTransform(Position, Rotation, Scale)`, also once hidden | Nothing else in the SDK sees foliage. Finding is a walk over every instanced mesh loaded, so it is for a key press. The transform lets a copy stand exactly where the instance did, whichever of the hide and the mod's own message arrives first. |
| `ILocalProps.cs` (new) | `Place(prop, collides)` returning a `LocalPropHandle`, `Move(handle, position, rotation)`, `AttachTo(handle, character, mesh, socket, offset, rotation)` with `CharacterMesh.ThirdPerson` or `FirstPerson`, `Remove`; unknown handles throw | An effect each client plays for itself, moved every frame: nothing to replicate. A tool the game cannot equip (the clutter pickaxe) hangs in the hand this way; `Weapon_Socket` is where the game's own hand props (`*_HandProp` blueprints) hang. |
| `IAnimations.cs` (new) | `Play(path, firstPersonPath, rate)` returning the length, `Stop(blendOut)` | A montage replicates: the local player's playing montage is sampled into `AnimationState` by path and replayed on puppets (`MovementControl`'s montage aggregate). A sequence (the combo attacks are sequences) plays in a dynamic `/Engine/Transient...` montage that does not resolve on another client, so only the local player sees it; it is taken anyway because the battle axe's third combo swing looks right for felling. |
| `Internal/FoliageService.cs`, `Internal/FoliageRpcClient.cs` (new) | applies the server's set to native; asks again on every new connection | |
| `Internal/Animations.cs` (new) | argument checks, forwards to native; `PlayOn(character, path, rate)` plays on another player's character as this client draws it | A sequence a client plays on itself reaches nobody else; a mod sends it round peer to peer and plays it there. |
| `IEquippedWeapon.cs`, `Internal/EquippedWeapon.cs` (new) | `Wear(points)` | Durability as the game keeps it (`ExtraHealth`), set on the client that owns the inventory; the equipment sync carries it to the server. The server writing `InventoryEntry.Health` instead would be a different stack to the diff (health is part of an item's identity there), an unequip and re-add. |
| `IWeaponHits.cs` (new) | `event Hit` with `WeaponHit(Position, AttackKind, WithWeapon, Struck, StruckFoliage)`; `StruckSurface` (nothing, landscape, foliage, other) | Where the local player's weapon lands, and what it struck: the impact effect says only where, so a line is traced through the point of contact. A foliage instance struck is named as `IFoliage` names it, so a rock or a tree hit is known exactly, not guessed from bounds. |
| `Internal/PropService.cs`, `Internal/PropRpcClient.cs` (new) | spawns the server's props through native, a handle per id; asks again on every new connection; implements `ILocalProps` from the same handles | A reconnect removes the server's props one by one, not with `RemoveAll`, so local ones stay. |
| `Internal/WeaponHitService.cs` (new) | `[Service]` taking the native hits every update and raising `Hit` | |
| `Mod.cs` | `IFoliage` to the `FoliageService` instance, `ILocalProps` to the `PropService` one, `IWeaponHits` to the `WeaponHitService` one, `IAnimations` to `Animations` | |

## Managed game mod: `OblivionMpCSharpMod`

| File | What |
|---|---|
| `NativeFoliageControl.cs` (new) | binding: `FindNear` into a buffer of `FoliageHit` (mesh path in 256 UTF-16 chars, x, y, z, distance), `SetHidden`, `ShowAll` |
| `NativeLocalPlayerAnimationControl.cs` (new) | binding: `Play` (third and first person path), `Stop` |
| `NativeLocalWeaponHitControl.cs` (new) | binding: `TakeHits` into a buffer of 64; each hit carries what it struck (kind, foliage mesh in 256 UTF-16 chars, location) |
| `NativeLocalPlayerWeaponControl.cs` (new) | binding: `Wear` |
| `NativePropControl.cs` (new) | binding: `Place(handle, asset, position, rotation, scale)`, `Remove`, `RemoveAll` |
| `NativeFoliageControl.cs` | `FoliageHit` carries the instance's scale, radius, top and bottom; `TryGetTransform` into a `FoliageTransform` (location, FQuat, scale) |
| `NativePropControl.cs` | `Place` takes `collides`; `Move(handle, position, rotation)`; `AttachTo(handle, character, firstPerson, socket, offset, rotation)` |
| `NativeBinding.cs`, `DI.cs` | the two binding fields after `LocalPlayerStatusControl` (same order as native), registration, `BindNative` |

## Native mod: `OblivionMp`

| File | What | Why |
|---|---|---|
| `src/Control/FoliageControl.{h,cpp}` (new) | `FindNear` (each hit measured: the mesh's `GetBoundingBox`, asked once per mesh, its eight corners scaled, turned by the instance's quaternion and placed), `SetHidden`, `ShowAll`, `Tick`, `TryGetTransform` (the saved transform of a hidden instance, else looked up in the component it was last found in, else in every known one with its mesh) | See below. |
| `src/Control/LocalPlayerAnimationControl.{h,cpp}` (new) | plays a montage on the local pawn's anim instance, and its `_FP` counterpart on `AVOblivionPlayerCharacter::FirstPersonSkeletalMeshComponent`, loading them when the game has not | First person view draws its own mesh: a montage on the third-person mesh alone is not seen there. A claymore montage is not loaded while a battle axe is equipped; tries the loaded object, the asset registry, then the package loader. |
| `src/Control/LocalWeaponHitControl.{h,cpp}` (new) | a ProcessEvent post callback on `OnStartPlay` of `AVImpactSystemVFXBlueprint` (and its Blueprint subclasses); keeps the hit when its `WeaponMesh` belongs to the local pawn, reached through `GetOwner` and `GetAttachParentActor`, up to 4 steps; the position is the effect actor's | Every melee hit the game registers spawns that effect at the point of contact (it plays the hit sound). The parameters are read by name through the function's properties, not a guessed layout. Its `HitSourceMaterial` is the weapon's, so what was struck is found apart: `KismetSystemLibrary::LineTraceSingle` from the pawn through the point, 60 cm past it, on the Visibility channel, then `GameplayStatics::BreakHitResult` for the component and instance index (parameters written and read by name); a `FoliageInstancedStaticMeshComponent` names the instance by its mesh and `GetInstanceTransform`, a `LandscapeHeightfieldCollisionComponent` is the ground. |
| `src/Control/LocalPlayerWeaponControl.{h,cpp}` (new) | on the TES thread: the equipped weapon's entry (the process's virtual, as `EquipmentControl` reads it), its extra list, `GetItemDurability` (or the form's `GetBaseMaxDurability` when it has none yet) less the points, `SetItemDurability` | |
| `src/Control/PropControl.{h,cpp}` (new) | a `StaticMeshActor` per prop in the local pawn's world: `SetMobility(Movable)`, `SetStaticMesh`, `SetActorScale3D`; `SetActorEnableCollision` as placed; spawned again when its actor goes; destroyed on remove; `Move` sets location and rotation on the live actor (`K2_SetActorLocation`, `K2_SetActorRotation`, as `MovementControl` does); `AttachTo` hangs it on a socket of the character's `Mesh` or `FirstPersonSkeletalMeshComponent` (`K2_AttachToComponent`, snapped, then `K2_SetActorRelativeLocation` and `K2_SetActorRelativeRotation` with their parameters written by name), again after every respawn, and logs the mesh's sockets (`GetAllSocketNames`) when the one asked for is missing; the mesh cache pins what it holds | An asset that is a TES form is followed through its `NewMesh` soft path (`UTESObjectSTAT`, read with `SoftObjectPathLayout`). Not a TES reference, so nothing to save or replicate as an entity. A Static component refuses a new mesh in a running world, hence Movable. |
| `src/Control/AnimationControl.{h,cpp}` | `PlayMontage(animInstance, montage, ...)` returns `Montage_Play`'s length, `PlaySequenceOnSlot` the dynamic montage it made, instead of nothing | The SDK call reports whether it played and for how long (`GetPlayLength`). Existing callers ignore both. |
| `src/DI.{h,cpp}`, `src/NativeBinding.h` | construct the five controls, expose their bindings | |
| `src/ModMain.cpp` | `PropControl().Tick()` beside the foliage tick | Spawns props once there is a world, and again after a load. |
| `src/ModMain.cpp` | `FoliageControl().Tick()` in `OnTick` | Keeps hidden instances hidden; does nothing while none are. |

### FoliageControl

Game thread only; managed calls arrive there already (the managed ECS ticks inside `OnTick`).

- **Which components:** every tree is in two: a `FoliageInstancedStaticMeshComponent` in its world partition cell
  (the tree up close) and a plain `InstancedStaticMeshComponent` of the foliage HLOD layer (the tree from far away).
  The first version found the HLOD copy first and moved that, which is why the tree in front of the player stayed.
  Only the foliage components are touched now: the HLOD one holds a whole region's instances with no cluster tree,
  so each overlap query walks all of them and a removal rebuilds them all. Asking it about every hidden tree on every
  scan pass made each felled tree slow the game further. A felled tree can still show on the horizon.
- **Finding:** every foliage component, filtered by mesh path, `GetInstancesOverlappingSphere`, then
  `GetInstanceTransform` per hit, keeping instances whose origin is in range (the sphere test is against bounds).
- **Hiding:** removed at once from the component the last `FindNear` found it in and from every foliage component
  already known with that mesh. A rolling scan of the object array (32k objects per frame, only while something is
  hidden) registers foliage components and looks each through once, when it first appears: that is how a part of the
  world that streams back in loses its felled trees again. A component that goes is dropped after a pass without it;
  a new one at the same address is told apart by its object index.
- **Showing:** `AddInstance` with the saved transform, in each component it was removed from that is still there and
  does not hold it already; a component that streamed back in brought the instance back itself.
- **Logging:** every find, hide, removal and put-back is one line in `UE4SS.log` prefixed `[FoliageControl]`, and a
  removal that reports success with the instance still there is an error.

## Verified, and not

- In game: finding trees, the server's chop rules, the reward, both montages (first person included), hiding by
  `RemoveInstance` (the tree goes), the late-join resync, and no slowdown after the HLOD fix. Bushes were not picked.
- In game too: `IWeaponHits` (the tree comes down on the counted hits).
- In game too: `IProps` (stumps, and logs before they were dropped), `ILocalProps` placing and moving (falling trees),
  `IFoliage.TryGetTransform`.
- Fixed from a game log: after a load the birch log props came back as `SM_seisland_portal_COL`, a collision mesh,
  drawn grey. `PropControl` cached meshes by raw pointer; the level's mesh was collected and another object took its
  address. The cache now pins the meshes it holds. Not yet seen in game after the fix.
- In game: `ILocalProps.AttachTo` hangs a prop on the hand socket and it follows the swing; one hung on the first
  person arms also shows in third person view, frozen by the character, since the arms are hidden there and the prop is
  not. Jobs no longer uses it (mining takes an equipped warhammer); fixing that is for whoever next needs it.
- A prop with collision makes overlap events (`SetGenerateOverlapEvents`): the game's weapon hits are hitboxes
  (`UVHitBoxComponent`) overlapping what they strike, and a `StaticMeshActor` makes none by default, so a warhammer went
  through an ore node with no hit and no sound. Not yet seen in game with the fix.
- A form whose `NewMesh` is empty (the iron vein containers) is drawn by its blueprint: the prop takes the static mesh
  of a component of `BP_<name>_C` beside the form (a template under the class, or a subobject of its default object),
  found once by a walk over the object array. Not yet seen in game.
- A prop whose asset leads to no static mesh says why: the asset does not load, has no `NewMesh`, an empty one, or one
  of another class (the iron vein containers and the stone brick items do not resolve; the pickaxe and the madness ore
  deposit do).
- Not yet: regrowing (`AddInstance`).
- Jobs no longer plays its own swing: woodcutting, like mining, is the game's power attack, reported by
  `IWeaponHits` with the foliage instance struck. `IAnimations` stays in the SDK, unused by Jobs.

## Networked events: no SDK change

A felled tree, a spent node and a quarried block are networked events declared in `Roleplay.Jobs.Common`
(`TreeFelledEvent`, `OreMinedEvent`, `StoneQuarriedEvent`), on the SDK's `[NetworkedEvent]` as rp-framework already
has it from main. The server raises them with `INetworkedEvents.RaiseAs`, as the player whose hit did it, from the
RPC handler that counted the hit: the relay schedules every mod RPC handler onto the world's update loop
(`ModHostInitializer`), the thread `RaiseAs` requires. They carry strings, ints, floats
and a `System.Numerics.Vector3`, all of which the event serializer takes.

**Why not replace an SDK hook with them:** the hits stay RPCs. A hit is a claim the server checks (reach, the
weapon, the tree, the camp) before anything happens, and most hits change nothing anyone else sees; an event per hit
would fan out to the whole area for nothing. Only the outcome is an event.
