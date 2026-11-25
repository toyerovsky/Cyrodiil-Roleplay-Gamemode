using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// A worn rod the player carries. Item: its key. Price: what mending one to good costs, in gold.
internal sealed record FisherRepairView(string Item, string Icon, string Name, RodCondition Condition, int Price);
