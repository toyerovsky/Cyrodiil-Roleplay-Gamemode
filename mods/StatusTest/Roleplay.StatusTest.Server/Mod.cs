using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace Roleplay.StatusTest.Server;

[ModEntry]
public sealed partial class Mod(ILogger logger)
{
    private void Init()
    {
        logger.LogInformation("StatusTest: server half loaded. Numpad keys on the client apply test statuses.");
    }
}
