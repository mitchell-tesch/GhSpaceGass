using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for analysis type dispatch and settings pass-through (Slice 17).
/// </summary>
public class AnalysisDispatchTests
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

    private void SetupCompletedRun()
    {
        var completedRun = new AnalysisRun
        {
            Status = AnalysisRunStatus.Completed,
            RunId = Guid.NewGuid(),
            ElapsedTime = "00:00:01.000"
        };

        _api.RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(completedRun);
        _api.RunNonlinearAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(completedRun);
        _api.RunBucklingAnalysisAsync(Arg.Any<BucklingSettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(completedRun);
        _api.RunDynamicFrequencyAnalysisAsync(Arg.Any<DynamicFrequencySettingsUpdate?>(), Arg.Any<CancellationToken>())
            .Returns(completedRun);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dispatch by type
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAnalysis_LinearStatic_CallsRunStaticAnalysisAsync()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        await session.RunAnalysisAsync();

        await _api.Received(1).RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive()
            .RunNonlinearAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive()
            .RunBucklingAnalysisAsync(Arg.Any<BucklingSettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAnalysis_NonlinearStatic_CallsRunNonlinearAnalysisAsync()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        await session.RunAnalysisAsync(SgAnalysisType.NonlinearStatic);

        await _api.Received(1)
            .RunNonlinearAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive()
            .RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAnalysis_Buckling_CallsRunBucklingAnalysisAsync()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        await session.RunAnalysisAsync(SgAnalysisType.Buckling);

        await _api.Received(1)
            .RunBucklingAnalysisAsync(Arg.Any<BucklingSettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAnalysis_DynamicFrequency_CallsRunDynamicFrequencyAnalysisAsync()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        await session.RunAnalysisAsync(SgAnalysisType.DynamicFrequency);

        await _api.Received(1)
            .RunDynamicFrequencyAnalysisAsync(Arg.Any<DynamicFrequencySettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // Settings pass-through
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAnalysis_WithStaticSettings_PassesSettingsToApi()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        var settings = SgAnalysisSettingsData.ForLinearStatic(new StaticSettingsUpdate { LoadSteps = 10 });

        await session.RunAnalysisAsync(SgAnalysisType.LinearStatic, settings);

        await _api.Received(1).RunStaticAnalysisAsync(
            Arg.Is<StaticSettingsUpdate?>(s => s != null && s.LoadSteps == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAnalysis_WithBucklingSettings_PassesModes()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        var settings = SgAnalysisSettingsData.ForBuckling(new BucklingSettingsUpdate { Modes = 5 });

        await session.RunAnalysisAsync(SgAnalysisType.Buckling, settings);

        await _api.Received(1).RunBucklingAnalysisAsync(
            Arg.Is<BucklingSettingsUpdate?>(s => s != null && s.Modes == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAnalysis_NoSettings_PassesNull()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        await session.RunAnalysisAsync();

        await _api.Received(1).RunStaticAnalysisAsync(
            Arg.Is<StaticSettingsUpdate?>(s => s == null),
            Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // Result mapping (same as before)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunAnalysis_Completed_ReturnsSucceeded()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        var result = await session.RunAnalysisAsync(SgAnalysisType.Buckling);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAnalysis_BackwardCompat_RunStaticAnalysisAsyncStillWorks()
    {
        var session = CreateConnectedSession();
        SetupCompletedRun();

        var result = await session.RunStaticAnalysisAsync();

        Assert.True(result.Succeeded);
        await _api.Received(1).RunStaticAnalysisAsync(Arg.Any<StaticSettingsUpdate?>(), Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgAnalysisSettingsData factory tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ForLinearStatic_SetsTypeAndStaticSettings()
    {
        var s = SgAnalysisSettingsData.ForLinearStatic(new StaticSettingsUpdate { LoadSteps = 3 });
        Assert.Equal(SgAnalysisType.LinearStatic, s.Type);
        Assert.NotNull(s.StaticSettings);
        Assert.Equal(3, s.StaticSettings!.LoadSteps);
        Assert.Null(s.BucklingSettings);
        Assert.Null(s.DynamicSettings);
    }

    [Fact]
    public void ForBuckling_SetsTypeAndBucklingSettings()
    {
        var s = SgAnalysisSettingsData.ForBuckling(new BucklingSettingsUpdate { Modes = 10 });
        Assert.Equal(SgAnalysisType.Buckling, s.Type);
        Assert.NotNull(s.BucklingSettings);
        Assert.Equal(10, s.BucklingSettings!.Modes);
        Assert.Null(s.StaticSettings);
    }

    [Fact]
    public void ForDynamicFrequency_SetsTypeAndDynamicSettings()
    {
        var s = SgAnalysisSettingsData.ForDynamicFrequency(new DynamicFrequencySettingsUpdate { Modes = 8 });
        Assert.Equal(SgAnalysisType.DynamicFrequency, s.Type);
        Assert.NotNull(s.DynamicSettings);
        Assert.Equal(8, s.DynamicSettings!.Modes);
    }
}