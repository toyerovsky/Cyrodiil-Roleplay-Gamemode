namespace CyrodiilRP.Data;

// Written with whatever earned it, so it arrives even if the character was away.
public sealed class GoldDelivery
{
    public long Id { get; set; }

    public long CharacterId { get; set; }

    public int Amount { get; set; }

    public GoldDeliveryReason Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}
