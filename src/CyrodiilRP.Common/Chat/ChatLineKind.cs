namespace CyrodiilRP.Common.Chat;

// Sent as a byte, so append only.
public enum ChatLineKind : byte
{
    Say,
    Shout,
    Whisper,
    Action, // /me
    Ambient, // /do
    Attempt, // /try, /roll
    LocalOoc,
    GlobalOoc,
    Private,
    System,
    CommandReply,
    Rejection,
    Announcement,
    Staff,
    Party, // /p
}
