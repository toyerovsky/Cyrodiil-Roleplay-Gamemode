using System.Numerics;
using CyrodiilRP.Common.Building;

namespace CyrodiilRP.Server.Building;

// A new plot as checked: Box, Sign, SignYaw and Form are what to write when the verdict is Accepted.
internal sealed record PlotPlan(MarkingVerdict Verdict, PlotBox Box, Vector3 Sign, float SignYaw, PlotForm Form);
