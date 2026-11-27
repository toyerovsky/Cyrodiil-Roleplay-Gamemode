namespace CyrodiilRP.Common.Fishing;

// A place for the angler's camera over the shoulder: how far behind the angler it stands (ahead of them below zero) and
// how much higher than the usual. Centimetres.
public readonly record struct CameraRung(float Back, float Raise);
