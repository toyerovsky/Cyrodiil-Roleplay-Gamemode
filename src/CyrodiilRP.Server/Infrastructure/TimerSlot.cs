namespace CyrodiilRP.Server.Infrastructure;

// Due first, then the order timers were added, so two due at once run in the order they were asked for.
internal readonly record struct TimerSlot(long Due, long Order) : IComparable<TimerSlot>
{
    public int CompareTo(TimerSlot other) => Due != other.Due ? Due.CompareTo(other.Due) : Order.CompareTo(other.Order);
}
