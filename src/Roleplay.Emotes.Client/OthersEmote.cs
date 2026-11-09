using Roleplay.Emotes.Common;

namespace Roleplay.Emotes.Client;

/// <summary>What <see cref="EmotesRpc.PlayEmoteOn"/> says to play on another player's character.</summary>
public sealed record OthersEmote(int Player, string Animation, float PlayRate);
