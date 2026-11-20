using Avalonia.Threading;
using Batto.Platform;

namespace Batto.Linux.Platform;

/// <summary>Runs the action on the Avalonia UI thread, synchronously when the caller is already there.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }
}
