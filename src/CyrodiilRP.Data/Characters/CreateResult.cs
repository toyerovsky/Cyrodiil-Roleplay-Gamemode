namespace CyrodiilRP.Data.Characters;

public sealed record CreateResult(CreateOutcome Outcome, CharacterSnapshot? Character);
