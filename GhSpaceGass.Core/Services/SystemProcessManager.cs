using System.Diagnostics;

namespace GhSpaceGass.Core.Services;

internal class SystemProcessManager : IProcessManager
{
    private Process? _process;

    public bool Launch(string exePath, string arguments, bool showConsole = false)
    {
        try
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = !showConsole,
                    // Do NOT redirect stdout/stderr — if the pipe buffer fills
                    // (4 KB) and nobody reads it, the child process blocks on
                    // any console write, freezing the SpaceGass API server.
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                },
                EnableRaisingEvents = true
            };
            return _process.Start();
        }
        catch (Exception)
        {
            _process = null;
            return false;
        }
    }

    public void Kill()
    {
        if (_process is null || _process.HasExited) return;
        try
        {
            _process.Kill(true);
            _process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public bool IsRunning => _process is { HasExited: false };
}