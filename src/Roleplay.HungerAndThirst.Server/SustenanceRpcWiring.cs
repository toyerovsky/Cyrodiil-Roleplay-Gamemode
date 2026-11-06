using ReadyM.SDK.Attributes;

namespace Roleplay.HungerAndThirst.Server;

// Connects SustenanceService to the generated sends, so the service doesn't depend on the RPC class.
[Service]
public sealed partial class SustenanceRpcWiring(SustenanceService sustenance, SustenanceRpcHandlers rpc)
{
    private void Start()
    {
        sustenance.RatesChanged += rpc.SendDecayRates;
    }

    private void Stop()
    {
        sustenance.RatesChanged -= rpc.SendDecayRates;
    }
}
