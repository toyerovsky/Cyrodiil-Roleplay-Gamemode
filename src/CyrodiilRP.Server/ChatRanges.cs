namespace CyrodiilRP.Server;

// Centimetres, as the server holds positions.
internal sealed class ChatRanges
{
    public float Say { get; set; } = 1500f;

    public float Shout { get; set; } = 4000f;

    public float Whisper { get; set; } = 300f;

    // For /me, /do, /try, /roll and /b.
    public float Emote { get; set; } = 1500f;
}
