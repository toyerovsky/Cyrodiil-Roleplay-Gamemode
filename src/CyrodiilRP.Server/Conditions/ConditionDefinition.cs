using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Conditions;

// The key is stored in the database, so it is never renamed. SpeechRefusal is what a character it keeps from speaking
// is told, null for a condition that lets the character speak.
internal sealed record ConditionDefinition(string Key, Phrase Title, Func<ActiveCondition, Phrase>? SpeechRefusal);
