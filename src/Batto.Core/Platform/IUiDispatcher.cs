namespace Batto.Platform;

/// <summary>Marshals work onto the UI thread. The Core never references a UI toolkit.</summary>
public interface IUiDispatcher
{
    void Invoke(Action action);
}

/// <summary>Runs the action on the calling thread. Used when there is no UI (tests, headless runs).</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public static readonly ImmediateUiDispatcher Instance = new();

    public void Invoke(Action action) => action();
}
