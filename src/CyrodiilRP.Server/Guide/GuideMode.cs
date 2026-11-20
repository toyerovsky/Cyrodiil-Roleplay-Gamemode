using System.Text.Json.Serialization;

namespace CyrodiilRP.Server.Guide;

// Why a guide's card is up, which decides what its keys do. The page reads it by name.
[JsonConverter(typeof(JsonStringEnumConverter<GuideMode>))]
internal enum GuideMode
{
    // The first use of a window: Continue opens it, Esc only closes the card.
    Use,

    // The welcome at the account's first entry: it only closes.
    Entry,

    // Read again from the menu: closing it goes back to the menu.
    Read,
}
