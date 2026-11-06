namespace Roleplay.Jobs.Client;

/// <summary>How a chop turned out: the sentence Jobs has for it, and for a felling what the player got, each such as
/// "3 Gnarl Bark" (empty when nothing).</summary>
public readonly record struct ChopEnd(ChopEndReason Reason, string Message, IReadOnlyList<string> Gains);
