namespace CyrodiilRP.Server.Talk;

internal static class TalkRules
{
    // The quest the mode's topic hangs on. No other quest of the session may take the id.
    public const uint QuestId = 0x43525100;

    // The game refuses a quest without a name. Nothing shows this one while the quest has no text for a stage.
    public const string QuestName = "Cyrodiil RP";
}
