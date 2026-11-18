namespace CyrodiilRP.Server.Specializations;

// The caps a page names next to trained and limited stats.
internal sealed record MentorCapsView(int TrainedSkill, int LimitedSkill, int TrainedAttribute, int LimitedAttribute, int UnchosenSkill);
