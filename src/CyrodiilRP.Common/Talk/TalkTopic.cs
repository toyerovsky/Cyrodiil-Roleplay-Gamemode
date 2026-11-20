namespace CyrodiilRP.Common.Talk;

// The topic the mode puts in the game's dialogue with its people. The server offers it under these ids, and a player's
// own game tells by them which of the topics the character spoke are the mode's.
public static class TalkTopic
{
    // On the people the mode stands: traders, bankers, stablemasters, mentors and registrars.
    public const uint PeopleId = 0x43525001;

    // On the Jobs mod's foremen, when they are made from another base form than the mode's people.
    public const uint ForemenId = 0x43525002;

    public static bool IsOurs(uint topicId) => topicId is PeopleId or ForemenId;
}
