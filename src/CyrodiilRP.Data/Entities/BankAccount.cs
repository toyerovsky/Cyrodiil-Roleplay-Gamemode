namespace CyrodiilRP.Data;

// The gold a character keeps at the bank. The row is made by the first deposit, so a character without one has nothing
// there.
public sealed class BankAccount
{
    public long CharacterId { get; set; }

    public long Balance { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
