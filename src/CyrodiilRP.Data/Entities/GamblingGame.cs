using CyrodiilRP.Data.Gambling;

namespace CyrodiilRP.Data;

public sealed class GamblingGame
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public GamblingMode Mode { get; set; }
    public long TableId { get; set; }
    public long FirstCharacterId { get; set; }
    public long SecondCharacterId { get; set; }
    public required string FirstName { get; set; }
    public required string SecondName { get; set; }
    public int Stake { get; set; }
    public int FirstRaises { get; set; }
    public int SecondRaises { get; set; }
    public int Pot { get; set; }
    public GamblingStatus Status { get; set; }
    public long? WinnerCharacterId { get; set; }
    public int Cut { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public required string Rounds { get; set; }
}
