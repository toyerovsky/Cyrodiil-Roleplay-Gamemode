using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Client;

/// <summary>What <see cref="EmotesRpc.PlayEmote"/> says to play on this player's character. Seconds 0 lets it play out.</summary>
public sealed record EmoteOrder(string Animation, string? FirstPersonAnimation, float PlayRate, float Seconds);
