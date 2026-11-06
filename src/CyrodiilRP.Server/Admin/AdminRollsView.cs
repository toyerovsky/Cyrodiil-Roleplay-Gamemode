namespace CyrodiilRP.Server.Admin;

// A page of the roll log, newest first, as the audit's: More, older rolls below the last id; Appended, it goes on from
// the page before.
internal sealed record AdminRollsView(IReadOnlyList<AdminRollView> Rolls, bool More, bool Appended);
