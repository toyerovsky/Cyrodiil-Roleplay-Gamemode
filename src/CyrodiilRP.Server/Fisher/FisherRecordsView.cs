namespace CyrodiilRP.Server.Fisher;

// The Records part of the fisher's window, sent when the page asks for it: every species in the catalogue's order.
internal sealed record FisherRecordsView(IReadOnlyList<FisherSpeciesView> Species);
