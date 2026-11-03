using CyrodiilRP.Data.Characters;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Characters;

// The caller checks the player is still live.
[Service]
internal sealed partial class Roster(Persistence persistence, RpConfig config, ILogger logger)
{
    private const string NameJob = "characters.name";
    private const string CreateJob = "characters.create";

    private readonly NameReservations _reservations = new();

    public ReservedName? FindReservation(OnlinePlayer player) => _reservations.Find(player);

    // A name given by a player who left before the creator closed is anybody's again, and nothing of it was written.
    public void Forget(OnlinePlayer player) => _reservations.Release(player);

    // Null once the name is the player's to finish a character with, otherwise why not. Nothing is written: the character
    // is, whole, when the game's creator closes.
    public async Task<Phrase?> ReserveAsync(OnlinePlayer player, string firstName, string lastName)
    {
        if (player.Character is { } existing)
            return CharactersText.AlreadyCreated(existing.FullName);

        if (!TryName(firstName, lastName, out var first, out var last, out var problem))
            return problem;

        if (player.IsBusy)
            return CharactersText.Busy;

        var name = new ReservedName(first, last);
        if (!_reservations.TryReserve(player, name))
            return CharactersText.NameTaken(name.FullName);

        player.IsBusy = true;
        try
        {
            if (!await persistence.Writer.EnqueueAsync(NameJob, (db, ct) => CharacterBook.IsNameTakenAsync(db, first, last, ct)))
                return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] Checking the name {Character} for {Account} failed.", name.FullName, player.ReadyMId);
            _reservations.Release(player);
            return CharactersText.SaveFailed;
        }
        finally
        {
            player.IsBusy = false;
        }

        _reservations.Release(player);
        return CharactersText.NameTaken(name.FullName);
    }

    // The name the player holds becomes their character, with the starting gold owed in the same write. A failed write
    // keeps the name, so closing the creator again tries again.
    public async Task<CreationResult> CreateAsync(OnlinePlayer player)
    {
        if (player.Character is not null || player.IsBusy || _reservations.Find(player) is not { } name)
            return CreationResult.Ignored;

        var accountId = player.AccountId;
        player.IsBusy = true;
        CreateResult result;
        try
        {
            result = await persistence.Writer.EnqueueAsync(CreateJob,
                (db, ct) => CharacterBook.CreateAsync(db, accountId, name.FirstName, name.LastName, config.StartingGold, DateTimeOffset.UtcNow, ct));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[CyrodiilRP] Creating {Character} for {Account} failed.", name.FullName, player.ReadyMId);
            return CreationResult.Failed;
        }
        finally
        {
            player.IsBusy = false;
        }

        _reservations.Release(player);
        if (result.Outcome is CreateOutcome.NameTaken)
            return CreationResult.NameTaken;

        player.Character = CharacterRecord.Create(result.Character!, []);
        player.PlayCountedUntil = DateTimeOffset.UtcNow;
        if (result.Outcome is CreateOutcome.Created)
            logger.LogInformation("[CyrodiilRP] {Account} created {Character}.", player.ReadyMId, name.FullName);

        return CreationResult.Created;
    }

    private static bool TryName(string firstName, string lastName, out string first, out string last, out Phrase? problem)
    {
        first = NameRules.Normalize(firstName);
        last = NameRules.Normalize(lastName);
        problem = NameRules.Check(first, CharactersText.FirstName) ?? NameRules.Check(last, CharactersText.LastName);
        return problem is null;
    }
}
