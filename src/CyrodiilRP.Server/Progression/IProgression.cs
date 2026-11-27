using ReadyM.Api.Idents;

namespace CyrodiilRP.Server.Progression;

public interface IProgression
{
    bool Grant(PlayerId player, ProgressionSource source, int xp);
}
