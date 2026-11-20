using System.Text.Json.Serialization;

namespace CyrodiilRP.Data;

// What a guide's card is about: the welcome at an account's first entry, then each of the mode's windows met for the
// first time. Stored by name, and a page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<GuideTopic>))]
public enum GuideTopic
{
    Welcome,

    Trader,

    Bank,

    Stable,

    Mentor,

    Foreman,

    NoticeBoard,

    Gambling,

    Dice,

    Lore,

    Exchange,
}
