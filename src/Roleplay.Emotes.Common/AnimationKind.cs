namespace Roleplay.Emotes.Common;

/// <summary>What an emote's animation asset is, which decides how other players get to see it.</summary>
public enum AnimationKind
{
    /// <summary>An <c>AnimMontage</c> (<c>AM_...</c>): the platform sends it round by its path on its own.</summary>
    Montage = 1,

    /// <summary>An <c>AnimSequence</c> (<c>A_...</c>): played in the UpperBody slot, and the mod sends it round.</summary>
    Sequence = 2,
}
