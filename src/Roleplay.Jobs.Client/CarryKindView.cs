using Roleplay.Jobs.Common;

namespace Roleplay.Jobs.Client;

/// <summary>How a kind hangs in the hand, as <see cref="CarryRpc.CarryKind"/> tells it.</summary>
public sealed record CarryKindView(int Index, string Name, string Asset, float Scale, string Socket, CarryFitView Standing, CarryFitView Moving)
{
    public CarryFitView FitFor(CarryPose pose) => pose switch
    {
        CarryPose.Standing => Standing,
        CarryPose.Moving => Moving,
    };

    public CarryKindView WithFit(CarryPose pose, CarryFitView fit) => pose switch
    {
        CarryPose.Standing => this with { Standing = fit },
        CarryPose.Moving => this with { Moving = fit },
    };
}
