namespace Batto.Storage;

/// <summary>Starts the application with the user session. Windows: Run registry key. Linux: XDG autostart entry.</summary>
public interface IAutostart
{
    bool IsEnabled();

    void Enable(string executablePath);

    void Disable();
}
