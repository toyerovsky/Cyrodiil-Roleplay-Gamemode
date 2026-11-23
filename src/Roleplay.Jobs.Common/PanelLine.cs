namespace Roleplay.Jobs.Common;

/// <summary>A line under the Jobs panel's bar. Kind: the page's name for it ("gain", "note", "refusal"). Gain: what a gain
/// line counts, null for any other.</summary>
public sealed record PanelLine(string Kind, string Text, PanelGain? Gain);
