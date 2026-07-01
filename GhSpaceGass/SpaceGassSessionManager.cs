using GhSpaceGass.Core.Services;

namespace GhSpaceGass;

/// <summary>
///     Static holder for the singleton <see cref="SpaceGassSession" />.
///     Provides thread-safe access and cleanup for the shared session.
/// </summary>
public static class SpaceGassSessionManager
{
    private static SpaceGassSession _session;
    private static readonly object Lock = new();

    /// <summary>
    ///     The current session, or null if none has been created.
    /// </summary>
    public static SpaceGassSession Current
    {
        get
        {
            lock (Lock)
            {
                return _session;
            }
        }
    }

    /// <summary>
    ///     Returns the existing session if it matches the port, or disposes the old one and creates a new one.
    /// </summary>
    public static SpaceGassSession GetOrCreate(
        int port = SpaceGassSession.DefaultPort,
        string installPath = SpaceGassSession.DefaultInstallPath)
    {
        lock (Lock)
        {
            if (_session != null && _session.Port != port)
            {
                _session.Dispose();
                _session = null;
            }

            _session ??= new SpaceGassSession(port, installPath);
            return _session;
        }
    }

    /// <summary>
    ///     Disposes the current session and clears the singleton.
    ///     Called on GH unload or when the Connect component is removed.
    /// </summary>
    public static void DisposeSession()
    {
        lock (Lock)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}