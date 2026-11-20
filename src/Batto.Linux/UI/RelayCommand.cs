using System.Windows.Input;

namespace Batto.Linux.UI;

/// <summary>Command that runs an action. Enabled by default; the window uses it for buttons.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
