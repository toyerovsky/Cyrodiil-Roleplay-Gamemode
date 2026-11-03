using System.Runtime.CompilerServices;
using ReadyM.SDK.Attributes;

namespace CyrodiilRP.Server.Infrastructure;

[Service]
internal sealed partial class Callbacks(GameThread gameThread, Watchdog watchdog)
{
    private const string UnknownSubscriber = "?";

    public void Raise<T>(string name, Action<T>? handlers, T argument) =>
        RaiseEach(name, handlers, handler => ((Action<T>)handler)(argument));

    public void Raise<T1, T2>(string name, Action<T1, T2>? handlers, T1 first, T2 second) =>
        RaiseEach(name, handlers, handler => ((Action<T1, T2>)handler)(first, second));

    public void Raise<T1, T2, T3>(string name, Action<T1, T2, T3>? handlers, T1 first, T2 second, T3 third) =>
        RaiseEach(name, handlers, handler => ((Action<T1, T2, T3>)handler)(first, second, third));

    // Every handler on its own: one that throws is logged under the event and the class that subscribed, and the rest
    // still run.
    private void RaiseEach(string name, Delegate? handlers, Action<Delegate> call)
    {
        if (handlers is null)
            return;

        using var scope = gameThread.Enter();
        foreach (var handler in handlers.GetInvocationList())
            watchdog.Run($"{name} {NameSubscriber(handler)}", () => call(handler));
    }

    // A lambda's method lives in a class the compiler nests inside the one that wrote it.
    private static string NameSubscriber(Delegate handler)
    {
        var type = handler.Method.DeclaringType;
        while (type is { DeclaringType: { } outer } && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            type = outer;

        return type?.Name ?? UnknownSubscriber;
    }
}
