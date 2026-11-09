# Emotes (`Roleplay.Emotes`)

Emotes a player plays on their character for everyone around: the game's own idles, and any that a mod adds. The
code is in the repository's `src/Roleplay.Emotes.*` and `tests/Roleplay.Emotes.Tests`; this folder keeps the manifest
and the packaging. Cyrodiil RP puts every emote on the G wheel (Emotes), Stop first, eight a page.

## How it plays

- The server holds the list (`IEmotes`, `EmoteRegistry`). A pick from the wheel, or `ILocalEmotes.Ask(id)` from a
  client mod, goes through `IEmotes.Play`, which tells that client to play it with the SDK's `IAnimations.Play`.
- A **montage** (`AM_...`) the platform sends round to the other players by its path. A **sequence** (`A_...`) it does
  not, so the server tells every other client to play it on that character too (`IAnimations.PlayOn`). A sequence
  plays in the UpperBody slot: the legs keep standing or walking.
- `Stop` stops whatever the character plays. `Emote.Seconds` stops a montage that loops after that long.

## Adding emotes from a mod

The mod's manifest depends on `Roleplay.Emotes`, its server project references `Roleplay.Emotes.Server`, and a
service registers its emotes when it starts:

```csharp
[Service]
internal sealed partial class MyEmotes(IEmotes emotes)
{
    private void Start() => emotes.Register(
        new Emote("mymod.jig", "Jig", "/Game/MyMod/Animations/AM_Jig.AM_Jig", AnimationKind.Montage)
        {
            Order = 500,
            FirstPersonAnimation = null,
            Translations = new Dictionary<string, string> { ["pl"] = "Zatańcz gigę" },
        });
}
```

- `Id` is unique, a second one throws. `Order` places it: the game's own take 0 to 99.
- `Animation` is an object path, `/Game/Folder/Asset.Asset`. The pak that holds it must be loaded on every client.
- `Icon` names one of the wheel's icons (`ui/rp/icons.js` of the gamemode), the emote icon when left out.
- `Unregister(id)` takes one off, a game's own one too.

## Packaging

```powershell
.\MakeModFolder.ps1 Release
```

lays it out in `Output/mods/Roleplay.Emotes/`, ready to copy into a server's `mods/`. It needs no config.

## Not checked in the game yet

Which of the game's montages loop until stopped (the NPCs' idles may), how each one looks on a player, and whether a
stopped montage stops on the other players' screens too (only the local character is stopped).
