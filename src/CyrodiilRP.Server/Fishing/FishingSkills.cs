using System.Collections.Immutable;
using CyrodiilRP.Data;
using CyrodiilRP.Data.Fishing;
using CyrodiilRP.Server.Characters;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Players;
using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Fishing;

// Each playing character's fishing skill, catches, bait and best fish: read as the character starts playing, held while
// it plays, and written after each change, in order, on the database's writer.
[Service]
internal sealed partial class FishingSkills(OnlinePlayers online, Persistence persistence, Fire fire, Callbacks callbacks, SystemClock clock, ILogger logger)
{
    private const string LoadJob = "fishing.load";
    private const string SaveJob = "fishing.save";
    private const string BaitEvent = "fishing.bait";

    private readonly AnglerProgressRegistry _progress = new();

    public event Action<OnlinePlayer>? BaitChosen;

    private void Start()
    {
        online.Playing += Load;
        online.Left += _progress.Remove;
    }

    // Null until the character's fishing is read.
    public AnglerProgress? Find(OnlinePlayer player) => _progress.Find(player);

    public CatchGain RecordCatch(OnlinePlayer player, FishQuarry fish)
    {
        var progress = Require(player);
        var gain = progress.Judge(fish);
        var after = progress.AfterCatch(fish, gain, clock.UtcNow);
        _progress.Set(player, after);
        SaveProgress(after);
        var species = fish.Fish.Species;
        var record = after.Records[species];
        var characterId = after.CharacterId;
        fire.Run(SaveJob, () => persistence.Writer.EnqueueAsync(SaveJob, (db, ct) => FishingBook.SaveRecordAsync(
            db, characterId, species.ToString(), record.BestLengthCm, record.BestGrams, record.Count, record.BestAt, ct)));
        return gain;
    }

    public CatchGain RecordFind(OnlinePlayer player)
    {
        var progress = Require(player);
        var gain = progress.JudgeFind();
        var after = progress.AfterFind(gain);
        _progress.Set(player, after);
        SaveProgress(after);
        return gain;
    }

    public CatchGain RecordRelease(OnlinePlayer player, int xp)
    {
        var progress = Require(player);
        var gain = progress.JudgeRelease(xp);
        var after = progress.AfterRelease(gain);
        _progress.Set(player, after);
        SaveProgress(after);
        return gain;
    }

    public void ChooseBait(OnlinePlayer player, BaitKind bait)
    {
        var after = Require(player) with { Bait = bait };
        _progress.Set(player, after);
        SaveProgress(after);
        callbacks.Raise(BaitEvent, BaitChosen, player);
    }

    private AnglerProgress Require(OnlinePlayer player) =>
        _progress.Find(player) ?? throw new InvalidOperationException($"The fishing of {player.Ref} is not read yet.");

    private void SaveProgress(AnglerProgress progress)
    {
        var at = clock.UtcNow;
        fire.Run(SaveJob, () => persistence.Writer.EnqueueAsync(SaveJob, (db, ct) => FishingBook.SaveProgressAsync(
            db, progress.CharacterId, progress.Skill.Level, progress.Skill.Xp, progress.Catches, progress.Bait.ToString(), at, ct)));
    }

    // On the writer, so a read comes back after the writes asked before it.
    private void Load(OnlinePlayer player)
    {
        if (player.Character is not { } character)
            return;

        var characterId = character.Id;
        fire.Run(LoadJob, async () =>
        {
            var stored = await persistence.Writer.EnqueueAsync(LoadJob, async (db, ct) =>
                new StoredFishing(await FishingBook.FindAsync(db, characterId, ct), await FishingBook.ListRecordsAsync(db, characterId, ct)));
            if (player.IsLive && player.Character?.Id == characterId)
                _progress.Set(player, ToProgress(characterId, stored));
        });
    }

    private AnglerProgress ToProgress(long characterId, StoredFishing stored)
    {
        var records = ImmutableDictionary.CreateBuilder<FishSpecies, FishRecord>();
        foreach (var best in stored.Records)
        {
            if (Enum.TryParse<FishSpecies>(best.Species, out var species) && Enum.IsDefined(species))
                records[species] = new FishRecord(best.BestLengthCm, best.BestGrams, best.Count, best.BestAt);
            else
                logger.LogError("[CyrodiilRP] {Character} has a fishing record of a species the mode does not know: {Species}.",
                    CharacterRecord.FormatRef(characterId), best.Species);
        }

        if (stored.Progress is not { } row)
            return AnglerProgress.Fresh(characterId) with { Records = records.ToImmutable() };

        var bait = ParseBait(characterId, row.Bait);
        return new AnglerProgress(characterId, new FishingSkill(row.Level, row.Xp), row.Catches, bait, records.ToImmutable());
    }

    private BaitKind ParseBait(long characterId, string stored)
    {
        if (Enum.TryParse<BaitKind>(stored, out var bait) && Enum.IsDefined(bait))
            return bait;

        logger.LogError("[CyrodiilRP] {Character} fishes with a bait the mode does not know: {Bait}. A bare hook instead.",
            CharacterRecord.FormatRef(characterId), stored);
        return BaitKind.None;
    }

    private sealed record StoredFishing(CharacterFishing? Progress, List<CharacterFishRecord> Records);
}
