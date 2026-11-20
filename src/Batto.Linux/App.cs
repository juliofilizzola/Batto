using Avalonia;
using Avalonia.Markup.Xaml;

namespace Batto.Linux;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
