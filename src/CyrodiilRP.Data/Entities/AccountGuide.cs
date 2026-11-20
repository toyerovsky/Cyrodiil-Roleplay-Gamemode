namespace CyrodiilRP.Data;

// A guide the account has seen once, so it does not come up again on its own, for this character or a later one.
public sealed class AccountGuide
{
    public long AccountId { get; set; }

    public GuideTopic Topic { get; set; }

    public DateTimeOffset SeenAt { get; set; }
}
