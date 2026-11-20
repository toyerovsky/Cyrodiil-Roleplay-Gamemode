using CyrodiilRP.Common.Talk;

namespace CyrodiilRP.Server.Talk;

// One topic of the mode as the game is given it: the people of one base form offer it and answer with the reply.
internal sealed record TalkOffer(uint TopicId, uint SpeakerBase, string Name, string Reply)
{
    // The game offers a topic by the speaker's base form alone. People of one base get one topic: a second on the
    // same base would stand twice in the list.
    public static IReadOnlyList<TalkOffer> ListFor(uint peopleBase, uint foremenBase, string name, string reply)
    {
        var people = new TalkOffer(TalkTopic.PeopleId, peopleBase, name, reply);
        return foremenBase == peopleBase ? [people] : [people, new TalkOffer(TalkTopic.ForemenId, foremenBase, name, reply)];
    }
}
