namespace CyrodiilRP.Server.Players;

// An ask on both players' screens until it is answered, withdrawn or runs out. Id: what the one asked answers to.
internal sealed record OpenAsk(int Id, PlayerAsk Ask, string Kind, long ExpiresAtMs, AskAnswers Answers);
