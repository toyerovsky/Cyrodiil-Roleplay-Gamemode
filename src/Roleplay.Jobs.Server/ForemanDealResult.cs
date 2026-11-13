namespace Roleplay.Jobs.Server;

/// <summary>What a deal came to: whether it went through, and the sentence that says so, or why not.</summary>
public sealed record ForemanDealResult(bool Done, string Text);
