using System.Text;
using CyrodiilRP.Common.Talk;
using CyrodiilRP.Server.Localization;
using CyrodiilRP.Server.Talk;
using CyrodiilRP.Server.Traders;
using CyrodiilRP.Tests.Fakes;
using ReadyM.Native.Container;

namespace CyrodiilRP.Tests.Talk;

public sealed class TalkOfferTests
{
    private const uint OtherBase = 0x000025FD;

    private static readonly Localizer Texts = Localizer.LoadFolder(Path.Combine(AppContext.BaseDirectory, "locales"), new ListLogger());

    [Fact]
    public void People_and_foremen_of_one_base_form_share_one_topic()
    {
        var offer = Assert.Single(TalkOffer.ListFor(TraderRules.BaseNpc, TraderRules.BaseNpc, "Services", "Of course."));

        Assert.Equal(new TalkOffer(TalkTopic.PeopleId, TraderRules.BaseNpc, "Services", "Of course."), offer);
    }

    [Fact]
    public void Foremen_of_another_base_form_get_a_topic_of_their_own_with_the_same_words()
    {
        var offers = TalkOffer.ListFor(TraderRules.BaseNpc, OtherBase, "Services", "Of course.");

        Assert.Equal(
            [
                new TalkOffer(TalkTopic.PeopleId, TraderRules.BaseNpc, "Services", "Of course."),
                new TalkOffer(TalkTopic.ForemenId, OtherBase, "Services", "Of course."),
            ],
            offers);
    }

    [Fact]
    public void Every_topic_the_mode_offers_is_told_from_the_topics_of_other_mods()
    {
        foreach (var offer in TalkOffer.ListFor(TraderRules.BaseNpc, OtherBase, "Services", "Of course."))
            Assert.True(TalkTopic.IsOurs(offer.TopicId));

        Assert.False(TalkTopic.IsOurs(0));
        Assert.False(TalkTopic.IsOurs(TalkRules.QuestId));
    }

    [Fact]
    public void The_topic_and_the_reply_fit_the_games_fields_in_every_language()
    {
        Assert.NotEmpty(Texts.Locales);
        foreach (var locale in Texts.Locales)
        {
            var topic = Texts.Get(locale, TalkText.Topic);
            var reply = Texts.Get(locale, TalkText.Reply);

            Assert.NotEqual(TalkText.Topic.Key, topic);
            Assert.NotEqual(TalkText.Reply.Key, reply);
            Assert.InRange(Encoding.UTF8.GetByteCount(topic), 1, NativeString64.Capacity);
            Assert.InRange(Encoding.UTF8.GetByteCount(reply), 1, NativeString256.Capacity);
        }
    }

    [Fact]
    public void The_quests_name_fits_the_games_field()
    {
        Assert.InRange(Encoding.UTF8.GetByteCount(TalkRules.QuestName), 1, NativeString64.Capacity);
    }
}
