using CyrodiilRP.Data;
using CyrodiilRP.Data.Conditions;
using CyrodiilRP.Server.Infrastructure;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Players;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Conditions;

// A character's conditions come with it at the join (CharacterBook.JoinAsync).
[Service]
internal sealed partial class CharacterConditions(OnlinePlayers online, Persistence persistence, Timers timers)
{
    private const string SaveJob = "conditions.save";
    private const string RemoveJob = "conditions.remove";

    private static readonly TimeSpan ExpireEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, ConditionDefinition> _definitions = new(StringComparer.Ordinal);

    public ConditionDefinition Define(string key, Phrase title, Func<ActiveCondition, Phrase>? speechRefusal = null)
    {
        var definition = new ConditionDefinition(key, title, speechRefusal);
        if (!_definitions.TryAdd(key, definition))
            throw new InvalidOperationException($"The condition {key} is defined twice.");

        return definition;
    }

    // Null when the character may speak, otherwise the refusal of the first condition that keeps it from speaking.
    public Phrase? CheckSpeech(OnlinePlayer player)
    {
        foreach (var condition in ListActive(player))
        {
            if (_definitions.GetValueOrDefault(condition.Key)?.SpeechRefusal is { } refusal)
                return refusal(condition);
        }

        return null;
    }

    public IEnumerable<TitledCondition> ListTitled(OnlinePlayer player) =>
        ListActive(player).Where(c => _definitions.ContainsKey(c.Key)).Select(c => new TitledCondition(_definitions[c.Key].Title, c));

    public IEnumerable<ActiveCondition> ListSilencing(OnlinePlayer player) =>
        ListActive(player).Where(c => _definitions.GetValueOrDefault(c.Key)?.SpeechRefusal is not null);

    public void Apply(OnlinePlayer target, ConditionDefinition definition, TimeSpan? duration, string? byRef, string? reason)
    {
        if (target.Character is not { } character)
            throw new InvalidOperationException("A condition needs a character.");

        var now = DateTimeOffset.UtcNow;
        var condition = new ActiveCondition(definition.Key, now, now + duration, byRef, reason);
        character.Conditions[definition.Key] = condition;

        var row = new CharacterCondition
        {
            CharacterId = character.Id,
            Key = condition.Key,
            AppliedAt = condition.AppliedAt,
            ExpiresAt = condition.ExpiresAt,
            ByRef = condition.ByRef,
            Reason = condition.Reason,
        };
        _ = persistence.Writer.EnqueueAsync(SaveJob, (db, ct) => ConditionBook.SaveAsync(db, row, ct));
    }

    public bool Remove(OnlinePlayer target, ConditionDefinition definition) => Remove(target, definition.Key);

    private static IReadOnlyCollection<ActiveCondition> ListActive(OnlinePlayer player) =>
        player.Character is { } character ? character.Conditions.Values : [];

    private bool Remove(OnlinePlayer target, string key)
    {
        if (target.Character is not { } character || !character.Conditions.Remove(key))
            return false;

        _ = persistence.Writer.EnqueueAsync(RemoveJob, (db, ct) => ConditionBook.RemoveAsync(db, character.Id, key, ct));
        return true;
    }

    private void Start() => timers.RunEvery("conditions.expire", ExpireEvery, ExpireDue);

    private void ExpireDue()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var player in online.All)
        {
            if (player.Character is not { Conditions.Count: > 0 } character)
                continue;

            foreach (var key in character.Conditions.Values.Where(c => c.ExpiresAt <= now).Select(c => c.Key).ToList())
                Remove(player, key);
        }
    }
}
