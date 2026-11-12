using CyrodiilRP.Data.Notices;

namespace CyrodiilRP.Server;

internal sealed class NoticeBoardSettings
{
    // A string goes to a player with its length in two bytes, so 65,534 bytes of UTF-8 at most, and a letter is at most
    // three of them: a full board's window stays under this many letters.
    public const int MostLettersShown = 15_000;

    // The notices a board holds. A new one on a full board takes the oldest down.
    public int Shown { get; set; } = 10;

    public int MinLength { get; set; } = 1;

    public int MaxLength { get; set; } = 300;

    // Notices one character has up at once, on every board together.
    public int PerCharacter { get; set; } = 3;

    // Between two notices of one character.
    public int CooldownMinutes { get; set; } = 5;

    public int ExpiryDays { get; set; } = 7;

    // The game's form whose mesh stands for a board: "sign02" (0x000255D7), a blank wooden sign.
    public string Asset { get; set; } = "/Game/Forms/worldobjects/activator/sign02.sign02";

    public TimeSpan Cooldown => TimeSpan.FromMinutes(CooldownMinutes);

    public TimeSpan Expiry => TimeSpan.FromDays(ExpiryDays);

    // A server owner's mistake stops the mod at start rather than turning into a rule nobody meant.
    public void Check()
    {
        if (Shown < 1 || PerCharacter < 1 || CooldownMinutes < 0 || ExpiryDays < 1)
            throw new InvalidOperationException("noticeBoards in config.json: shown, perCharacter and expiryDays must be 1 or more, cooldownMinutes 0 or more.");

        if (MinLength < 1 || MaxLength < MinLength || MaxLength > NoticeBoardBook.MostTextLength)
            throw new InvalidOperationException($"noticeBoards in config.json: minLength must be 1 or more and maxLength from minLength to {NoticeBoardBook.MostTextLength}.");

        if ((long)Shown * MaxLength > MostLettersShown)
            throw new InvalidOperationException($"noticeBoards in config.json: shown times maxLength must stay under {MostLettersShown}, or a full board does not fit one message.");

        if (string.IsNullOrWhiteSpace(Asset))
            throw new InvalidOperationException("noticeBoards in config.json: asset must name the game's form or mesh a board stands as.");
    }
}
