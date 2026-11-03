using CyrodiilRP.Common.Chat;
using CyrodiilRP.Server.Chat;
using CyrodiilRP.Server.Economy;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Network;
using CyrodiilRP.Server.Players;
using CyrodiilRP.Server.Spawn;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Characters;

[Service]
internal sealed partial class CharacterCreation(
    OnlinePlayers online,
    Roster roster,
    FreshStart freshStart,
    GoldDeliveries gold,
    SpawnPoints spawn,
    RpcHandlers rpc,
    RpChat chat,
    Fire fire)
{
    private void Start()
    {
        online.Left += roster.Forget;
        rpc.NewCharacterAsked += OnNewCharacterAsked;
        rpc.NameSubmitted += OnNameSubmitted;
        rpc.AppearanceFinished += OnAppearanceFinished;
    }

    // Roster refuses a second character too, whatever a client asks.
    private void OnNewCharacterAsked(PlayerId id)
    {
        if (online.TryGet(id, out var player) && CharacterSlots.MayCreate(player))
            rpc.SendStartNaming(id);
    }

    private void OnNameSubmitted(PlayerId id, string firstName, string lastName)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded)
            return;

        fire.Run("characters.name", async () =>
        {
            var problem = await roster.ReserveAsync(player, firstName, lastName);
            if (!player.IsLive)
                return;

            rpc.SendNameAnswer(id, problem is null ? string.Empty : chat.Translate(player, problem));
            if (problem is null)
                StartAppearance(player);
        });
    }

    // Only now is the character written. The starting kit replaces what the player's save carried before the record is
    // written, so no character is ever saved with it, and the starting gold comes after, into the kit.
    private void OnAppearanceFinished(PlayerId id)
    {
        if (!online.TryGet(id, out var player) || !player.IsLoaded || roster.FindReservation(player) is not { } name)
            return;

        if (!freshStart.TryApply(id))
        {
            chat.Refuse(player, CharactersText.SaveFailed);
            StartAppearance(player);
            return;
        }

        fire.Run("characters.create", async () =>
        {
            var result = await roster.CreateAsync(player);
            if (!player.IsLive)
                return;

            switch (result)
            {
                case CreationResult.Created:
                    player.IsPlaying = true;
                    spawn.MoveNew(player);
                    chat.SendTo(player, ChatLineKind.System, CharactersText.Welcome(player.Name));
                    chat.SendTo(player, ChatLineKind.System, CharactersText.HelpHint);
                    chat.Notify(player, CharactersText.Welcome(player.Name));
                    gold.Deliver(player);
                    break;

                // Back to the form, with the answer a taken name always gets.
                case CreationResult.NameTaken:
                    rpc.SendStartNaming(id);
                    rpc.SendNameAnswer(id, chat.Translate(player, CharactersText.NameTaken(name.FullName)));
                    break;

                // The name is still held: closing the creator again tries again.
                case CreationResult.Failed:
                    chat.Refuse(player, CharactersText.SaveFailed);
                    StartAppearance(player);
                    break;
            }
        });
    }

    private void StartAppearance(OnlinePlayer player)
    {
        chat.Notify(player, CharactersText.CreatorHint);
        rpc.SendStartAppearance(player.Id);
    }
}
