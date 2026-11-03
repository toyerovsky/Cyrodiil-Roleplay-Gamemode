using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Pages;

[Service]
internal sealed partial class PageLocale(RpcHandlers rpc)
{
    public string Current { get; private set; } = "en";

    public event Action<string>? Changed;

    private void Start() => rpc.LocaleChanged += Set;

    private void Stop() => rpc.LocaleChanged -= Set;

    private void Set(string locale)
    {
        if (locale == Current)
            return;

        Current = locale;
        Changed?.Invoke(locale);
    }
}
