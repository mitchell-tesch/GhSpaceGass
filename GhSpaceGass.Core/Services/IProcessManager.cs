namespace GhSpaceGass.Core.Services;

/// <summary>
///     Manages the lifecycle of the SpaceGassApi.exe process.
/// </summary>
internal interface IProcessManager
{
    /// <summary>
    ///     True if a process was launched via <see cref="Launch" /> and is still running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///     Launches the process with the given executable path and arguments.
    ///     Returns true if the process started successfully.
    /// </summary>
    bool Launch(string exePath, string arguments, bool showConsole = false);

    /// <summary>
    ///     Terminates the launched process, if any.
    /// </summary>
    void Kill();
}