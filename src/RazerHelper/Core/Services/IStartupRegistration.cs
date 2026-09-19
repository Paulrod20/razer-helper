namespace RazerHelper.Core.Services;

/// <summary>Whether the app starts when the user signs in to Windows.</summary>
internal interface IStartupRegistration
{
    /// <summary>True when Windows will start the app at sign-in (a Task Manager "Disabled" also counts as off).</summary>
    bool IsEnabled { get; }

    /// <summary>Turns start at sign-in on or off. Throws if Windows refuses the change.</summary>
    void SetEnabled(bool enabled);
}
