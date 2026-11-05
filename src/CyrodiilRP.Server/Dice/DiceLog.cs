using CyrodiilRP.Data;
using CyrodiilRP.Data.Dice;
using CyrodiilRP.Server.Admin;
using CyrodiilRP.Server.Infrastructure;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Dice;

// Every roll in the database, for the staff, and each character's set. Writes go through the one writer, in order.
[Service]
internal sealed partial class DiceLog(Persistence persistence, Fire fire)
{
    // As many rows as the panel's dice tab shows at a time.
    public const int PageSize = 50;

    private const string WriteJob = "dice.write";
    private const string SetJob = "dice.set";

    public void Write(long characterId, string characterName, RollResult result, string? area)
    {
        var roll = new DiceRoll
        {
            At = result.At,
            CharacterId = characterId,
            CharacterName = characterName,
            Asked = DiceFormat.FormatAsked(result.Groups),
            Dice = DiceFormat.Store(result.Dice),
            Total = result.Total,
            Reason = result.Reason,
            Area = area,
        };
        fire.Run(WriteJob, () => persistence.Writer.EnqueueAsync(WriteJob, (db, ct) => DiceBook.WriteAsync(db, roll, ct)));
    }

    public void SaveSet(long characterId, string set) =>
        fire.Run(SetJob, () => persistence.Writer.EnqueueAsync(SetJob, (db, ct) => DiceBook.SaveSetAsync(db, characterId, set, ct)));

    public Task<DiceRecord> LoadAsync(long characterId, int recent) =>
        persistence.Reader.RunAsync((db, ct) => DiceBook.LoadAsync(db, characterId, recent, ct));

    public async Task<AdminRollsView> ReadPageAsync(AdminRollFilter filter)
    {
        var page = await persistence.Reader.RunAsync((db, ct) => DiceBook.LoadPageAsync(db, filter.Player, filter.Before, PageSize, ct));
        var rolls = page.Rolls
            .Select(roll => new AdminRollView(roll.Id, roll.At.ToUnixTimeMilliseconds(), roll.CharacterName, roll.Asked, DiceFormat.Read(roll.Dice),
                roll.Total, roll.Reason, roll.Area))
            .ToList();
        return new AdminRollsView(rolls, page.More, filter.Before is not null);
    }

    public static DiceRollView BuildView(DiceRoll roll) =>
        new(roll.At.ToUnixTimeMilliseconds(), roll.Asked, DiceFormat.Read(roll.Dice), roll.Total, roll.Reason);
}
