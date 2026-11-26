namespace CyrodiilRP.Server.Building;

// A plot's sign window a player has open. SeenGold and SeenStanding: what it last showed, so a change is sent.
internal sealed record OpenPlotSale(long PlotId, long CharacterId, int SeenGold, PlotStanding SeenStanding);
