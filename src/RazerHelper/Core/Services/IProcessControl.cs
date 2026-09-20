namespace RazerHelper.Core.Services;

/// <summary>Who a process id belongs to right now, used to confirm it is still the program that was scanned.</summary>
internal sealed record ProcessIdentity(string Name, DateTime? StartTime);

/// <summary>
/// The only things the app may do to another program's process: check who it
/// is, and ask it to close. There is deliberately no way to kill one, so a
/// program always gets the chance to ask the user to save.
/// </summary>
internal interface IProcessControl
{
    /// <summary>The program behind the id, or null if it has gone or cannot be inspected.</summary>
    ProcessIdentity? Identify(int processId);

    /// <summary>Asks the process's main window to close, like clicking its X. Returns false if that could not be done.</summary>
    bool RequestClose(int processId);
}
