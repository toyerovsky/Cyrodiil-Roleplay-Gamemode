using System.Numerics;
using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Players;

// Where one held character belongs: where it first stands still, two checks in a row. The first position the server has
// after a join or a load can be one the game never had: the world's origin, under the ground, for a character without a
// save.
internal sealed class HeldSpot(AreaId area, Vector3 position)
{
    // A step or a push is not worth a jolt back.
    public const float Slack = 150f;

    private AreaId _area = area;
    private Vector3 _position = position;
    private bool _isSettled;

    // Where to put the character back, null where it may stay. In another area (a load) it starts over.
    public Vector3? Check(AreaId area, Vector3 position)
    {
        var moved = area != _area || Vector3.Distance(position, _position) > Slack;
        if (_isSettled && area == _area)
            return moved ? _position : null;

        (_area, _position, _isSettled) = (area, position, !moved);
        return null;
    }
}
