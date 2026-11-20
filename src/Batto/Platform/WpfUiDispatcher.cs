using System.Windows;
using Batto.Platform;

namespace Batto.Platform;

/// <summary>Marshals actions onto the WPF dispatcher thread when called from a background thread.</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
