using CyrodiilRP.Server.Localization;

namespace CyrodiilRP.Server.Conditions;

internal sealed record TitledCondition(Phrase Title, ActiveCondition Condition);
