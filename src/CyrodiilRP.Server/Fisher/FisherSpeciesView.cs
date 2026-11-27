using CyrodiilRP.Server.Fishing;

namespace CyrodiilRP.Server.Fisher;

// A species on the records board. Top: its longest fish, the best first, none when nobody caught one. Own: the reader's
// best when it is not among them, else null.
internal sealed record FisherSpeciesView(FishSpecies Species, string Name, IReadOnlyList<FisherRecordView> Top, FisherRecordView? Own);
