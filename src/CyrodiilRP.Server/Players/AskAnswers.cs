namespace CyrodiilRP.Server.Players;

// What the service that asked does with a yes or a no.
internal sealed record AskAnswers(Action<PlayerAsk> Accept, Action<PlayerAsk> Decline);
