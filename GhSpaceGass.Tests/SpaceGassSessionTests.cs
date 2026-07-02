using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class SpaceGassSessionTests
{
    private const int TestPort = 34560;
    private const string TestInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();

    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    private SpaceGassSession CreateSession(string installPath = TestInstallPath)
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        return new SpaceGassSession(
            TestPort, installPath, TestTimeout,
            _processManager, _apiFactory);
    }

    /// <summary>
    ///     Configures the mock API so that GetServiceInfoAsync succeeds (service is ready).
    /// </summary>
    private void ServiceIsReady()
    {
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });
    }

    /// <summary>
    ///     Configures the mock API so that GetServiceInfoAsync fails (service not ready),
    ///     then optionally succeeds on subsequent calls.
    /// </summary>
    private void ServiceIsNotReady(bool becomeReadyLater = false)
    {
        if (becomeReadyLater)
            // First call throws, subsequent calls succeed
            _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
                .Returns(
                    x => throw new Exception("Connection refused"),
                    x => new ServiceInfo { SpaceGassVersion = "14.5" });
        else
            // Always throws
            _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Connection refused"));
    }

    // ── Slice 1: Connect ──────────────────────────────────────────────

    [Fact]
    public async Task Connect_WhenServiceAlreadyRunning_ReusesWithoutLaunching()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        // Should NOT have launched a process
        _processManager.DidNotReceive().Launch(Arg.Any<string>(), Arg.Any<string>());
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task Connect_ServiceUrl_ReturnsCorrectUrl()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        Assert.Equal($"http://localhost:{TestPort}", session.ServiceUrl);
    }

    [Fact]
    public async Task Connect_SpaceGassVersion_CapturedFromServiceInfo()
    {
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.50.134-beta1" });

        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);
        await session.ConnectAsync();

        Assert.Equal("14.50.134-beta1", session.SpaceGassVersion);
    }

    [Fact]
    public async Task Connect_WhenServiceNotRunning_LaunchesAndWaitsForHealthy()
    {
        ServiceIsNotReady(true);
        _processManager.Launch(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var session = CreateSession();
        await session.ConnectAsync();

        // Should have launched the process
        _processManager.Received(1).Launch(
            TestInstallPath,
            Arg.Is<string>(s => s.Contains("--urls") && s.Contains(TestPort.ToString())));
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task Connect_WhenExeNotFound_ThrowsFileNotFoundException()
    {
        ServiceIsNotReady();

        var session = CreateSession(@"C:\NonExistent\SpaceGassApi.exe");

        await Assert.ThrowsAsync<FileNotFoundException>(() => session.ConnectAsync());
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Connect_WhenServiceDoesNotBecomeHealthy_ThrowsTimeoutException()
    {
        ServiceIsNotReady();
        _processManager.Launch(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TimeSpan.FromMilliseconds(600),
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<TimeoutException>(() => session.ConnectAsync());
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Connect_CreatesApiClient_WithCorrectBaseUrl()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        _apiFactory.Received(1).Create($"http://localhost:{TestPort}");
    }

    // ── Slice 2: New Job ──────────────────────────────────────────────

    [Fact]
    public async Task NewJob_WhenConnected_ReturnsJobStatus()
    {
        ServiceIsReady();
        var expectedStatus = new JobStatus
        {
            State = new JobState { IsOpen = true, IsNew = true }
        };
        _api.NewJobAsync(Arg.Any<CancellationToken>()).Returns(expectedStatus);

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.NewJobAsync();

        Assert.NotNull(result);
        Assert.True(result.State?.IsOpen);
        Assert.True(result.State?.IsNew);
    }

    [Fact]
    public async Task NewJob_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var session = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.NewJobAsync());
    }

    // ── Dispose / Cleanup (ADR-0007) ──────────────────────────────────

    [Fact]
    public async Task Dispose_WhenProcessWasLaunched_KillsProcess()
    {
        ServiceIsNotReady(true);
        _processManager.Launch(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var session = CreateSession();
        await session.ConnectAsync();
        session.Dispose();

        _processManager.Received(1).Kill();
    }

    [Fact]
    public async Task Dispose_WhenProcessWasReused_DoesNotKillProcess()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();
        session.Dispose();

        _processManager.DidNotReceive().Kill();
    }

    [Fact]
    public async Task Dispose_SetsIsConnectedFalse()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();
        Assert.True(session.IsConnected);

        session.Dispose();
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Dispose_DisposesApiClient()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();
        session.Dispose();

        _api.Received(1).Dispose();
    }

    [Fact]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();
        session.Dispose();
        session.Dispose(); // should not throw
    }

    // ── Connect — edge cases ──────────────────────────────────────────

    [Fact]
    public async Task Connect_AfterDispose_ThrowsObjectDisposedException()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();
        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ConnectAsync());
    }

    // ── Open Job ──────────────────────────────────────────────────────

    [Fact]
    public async Task OpenJob_WhenConnected_CallsApiWithFilePath()
    {
        ServiceIsReady();
        var expectedStatus = new JobStatus
        {
            State = new JobState { IsOpen = true, IsNew = false }
        };
        _api.OpenJobAsync(Arg.Any<string>(), Arg.Any<JobForceAccessOption?>(), Arg.Any<CancellationToken>())
            .Returns(expectedStatus);

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.OpenJobAsync(@"C:\Models\Test.sg");

        Assert.NotNull(result);
        Assert.True(result.State?.IsOpen);
        await _api.Received(1).OpenJobAsync(@"C:\Models\Test.sg", Arg.Any<JobForceAccessOption?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenJob_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var session = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenJobAsync(@"C:\Models\Test.sg"));
    }

    // ── Save Job ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveJob_WhenConnected_CallsApiWithFilePath()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        await session.SaveJobAsync(@"C:\Models\Output.sg");

        await _api.Received(1).SaveJobAsync(@"C:\Models\Output.sg", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveJob_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var session = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveJobAsync(@"C:\Models\Output.sg"));
    }

    // ── Close Job ──────────────────────────────────────────────────────

    [Fact]
    public async Task CloseJob_WhenConnected_CallsApi()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        await session.CloseJobAsync();

        await _api.Received(1).CloseJobAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseJob_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var session = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CloseJobAsync());
    }

    // ── IsJobOpen ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsJobOpen_WhenJobIsOpen_ReturnsTrue()
    {
        ServiceIsReady();
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = true } });

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.IsJobOpenAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsJobOpen_WhenNoJobIsOpen_ReturnsFalse()
    {
        ServiceIsReady();
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = false } });

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.IsJobOpenAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsJobOpen_WhenApiThrows_ReturnsFalse()
    {
        ServiceIsReady();
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API error"));

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.IsJobOpenAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsJobOpen_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var session = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.IsJobOpenAsync());
    }

    // ── Port property ──────────────────────────────────────────────────

    [Fact]
    public void Port_ReturnsConfiguredPort()
    {
        var session = CreateSession();

        Assert.Equal(TestPort, session.Port);
    }

    // ── IsServiceResponsive ────────────────────────────────────────────

    [Fact]
    public async Task IsServiceResponsive_WhenConnectedAndHealthy_ReturnsTrue()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        var result = await session.IsServiceResponsiveAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsServiceResponsive_WhenServiceDied_ReturnsFalse()
    {
        ServiceIsReady();

        var session = CreateSession();
        await session.ConnectAsync();

        // Now make the service unresponsive
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection refused"));

        var result = await session.IsServiceResponsiveAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsServiceResponsive_WhenNotConnected_ReturnsFalse()
    {
        var session = CreateSession();

        var result = await session.IsServiceResponsiveAsync();

        Assert.False(result);
    }
}