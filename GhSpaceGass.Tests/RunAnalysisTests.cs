using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class RunAnalysisTests
{
    private const int TestPort = 34560;
    private const string TestInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();

    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    private SpaceGassSession CreateConnectedSession()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });

        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);
        session.ConnectAsync().GetAwaiter().GetResult();
        return session;
    }

    // ── Guard: not connected ──────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunStaticAnalysisAsync());
    }

    // ── Calls the API ─────────────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenConnected_CallsApiRunStaticAnalysis()
    {
        var session = CreateConnectedSession();
        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:01.000"
            });

        await session.RunStaticAnalysisAsync();

        await _api.Received(1).RunStaticAnalysisAsync(
            Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    // ── Success mapping ───────────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenCompleted_ReturnsSucceeded()
    {
        var session = CreateConnectedSession();
        var runId = Guid.NewGuid();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = runId,
                ElapsedTime = "00:00:02.500"
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(runId, result.RunId);
        Assert.Equal("00:00:02.500", result.ElapsedTime);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.Warnings);
    }

    // ── Failure mapping ───────────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenFailed_ReturnsNotSucceededWithError()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Failed,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:00.100",
                ErrorMessage = "Singular stiffness matrix at node 3"
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Singular stiffness matrix at node 3", result.ErrorMessage);
    }

    // ── Warnings mapping ──────────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenCompletedWithWarnings_IncludesWarnings()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:01.000",
                Warnings = new List<string> { "Large displacement at node 5", "Ill-conditioned matrix" }
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("Large displacement at node 5", result.Warnings);
        Assert.Contains("Ill-conditioned matrix", result.Warnings);
    }

    // ── API throws ────────────────────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenApiThrows_ThrowsInvalidOperationWithFormattedMessage()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection lost"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunStaticAnalysisAsync());

        Assert.Contains("Linear Static analysis", ex.Message);
    }

    // ── Cancelled status mapping ──────────────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenCancelled_ReturnsNotSucceeded()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Cancelled,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:00.500"
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.False(result.Succeeded);
    }

    // ── Null warnings handled gracefully ──────────────────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenWarningsNull_ReturnsEmptyWarningsList()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:01.000",
                Warnings = null
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Warnings);
        Assert.Empty(result.Warnings);
    }

    // ── Async polling: API returns Running then Completed ─────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenApiReturnsRunning_PollsUntilCompleted()
    {
        var session = CreateConnectedSession();
        var runId = Guid.NewGuid();

        // Initial POST returns Running
        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Running,
                RunId = runId
            });

        // First poll returns Running, second poll returns Completed
        _api.GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>())
            .Returns(
                new AnalysisRun { Status = AnalysisRunStatus.Running, RunId = runId },
                new AnalysisRun
                {
                    Status = AnalysisRunStatus.Completed,
                    RunId = runId,
                    ElapsedTime = "00:00:03.000"
                });

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(runId, result.RunId);
        Assert.Equal("00:00:03.000", result.ElapsedTime);
        // Verify polling happened
        await _api.Received(2).GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>());
    }

    // ── Async polling: API returns Queued then Completed ──────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenApiReturnsQueued_PollsUntilCompleted()
    {
        var session = CreateConnectedSession();
        var runId = Guid.NewGuid();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Queued,
                RunId = runId
            });

        _api.GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = runId,
                ElapsedTime = "00:00:01.500"
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        await _api.Received(1).GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>());
    }

    // ── Async polling: API returns Running then Failed ────────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenPollReturnsFailed_ReturnsNotSucceeded()
    {
        var session = CreateConnectedSession();
        var runId = Guid.NewGuid();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Running,
                RunId = runId
            });

        _api.GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Failed,
                RunId = runId,
                ErrorMessage = "Unstable structure"
            });

        var result = await session.RunStaticAnalysisAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Unstable structure", result.ErrorMessage);
    }

    // ── No polling when API immediately returns Completed ─────────────

    [Fact]
    public async Task RunStaticAnalysis_WhenImmediatelyCompleted_DoesNotPoll()
    {
        var session = CreateConnectedSession();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Completed,
                RunId = Guid.NewGuid(),
                ElapsedTime = "00:00:01.000"
            });

        await session.RunStaticAnalysisAsync();

        // Should never call GetAnalysisRunAsync
        await _api.DidNotReceive().GetAnalysisRunAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Polling throws — wrapped in InvalidOperationException ─────────

    [Fact]
    public async Task RunStaticAnalysis_WhenPollThrows_ThrowsInvalidOperationException()
    {
        var session = CreateConnectedSession();
        var runId = Guid.NewGuid();

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysisRun
            {
                Status = AnalysisRunStatus.Running,
                RunId = runId
            });

        _api.GetAnalysisRunAsync(runId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Network timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunStaticAnalysisAsync());

        Assert.Contains("polling analysis status", ex.Message);
    }
}