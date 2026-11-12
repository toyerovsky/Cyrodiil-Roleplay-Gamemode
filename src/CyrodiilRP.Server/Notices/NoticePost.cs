namespace CyrodiilRP.Server.Notices;

// What came of pinning a notice. Notice: the one pinned, null unless it is up.
internal sealed record NoticePost(NoticePlan Plan, NoticeRecord? Notice);
