using CyrodiilRP.Client.Pages;
using ReadyM.Cef;
using ReadyM.Cef.Rpc;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Client.Bank;

[Service]
internal sealed partial class BankOverlay(CefHost cef, WebPages pages, PageInput pageInput, RpcHandlers rpc, PageLocale locale)
{
    private const string PageName = "bank";

    private PageWindow? _window;

    // Another page taking the input closes the window, so gold never moves unseen.
    private PageWindow Window => _window ??= new PageWindow(
        new OverlayPage(pages, locale, PageName, WebPages.BankLayer, SendLocale),
        cef, pageInput, SendState, SendClosed, lost: rpc.SendBankClose);

    private void Start()
    {
        rpc.BankChanged += Window.Deliver;
        rpc.BankAnswered += OnAnswered;
        rpc.BankClosed += Window.Close;
        locale.Changed += Window.ApplyLocale;
    }

    private void Stop()
    {
        rpc.BankChanged -= Window.Deliver;
        rpc.BankAnswered -= OnAnswered;
        rpc.BankClosed -= Window.Close;
        locale.Changed -= Window.ApplyLocale;
        Window.Close();
    }

    private void OnAnswered(string text, bool done)
    {
        if (Window.ShownBrowser is { } browser)
            SendAnswer(browser, text, done);
    }

    [ReceiveFromCef]
    private void OnBankReady(BrowserHandle browser) => Window.MarkReady(browser);

    [ReceiveFromCef]
    private void OnBankDeposit(BrowserHandle browser, int amount) => rpc.SendBankDeposit(amount);

    [ReceiveFromCef]
    private void OnBankWithdraw(BrowserHandle browser, int amount) => rpc.SendBankWithdraw(amount);

    // Esc or the close button on the page.
    [ReceiveFromCef]
    private void OnBankClose(BrowserHandle browser)
    {
        Window.Close();
        rpc.SendBankClose();
    }

    [SendToCef]
    private partial void SendLocale(BrowserHandle browser, string locale);

    [SendToCef]
    private partial void SendState(BrowserHandle browser, string json);

    [SendToCef]
    private partial void SendAnswer(BrowserHandle browser, string text, bool done);

    [SendToCef]
    private partial void SendClosed(BrowserHandle browser);
}
