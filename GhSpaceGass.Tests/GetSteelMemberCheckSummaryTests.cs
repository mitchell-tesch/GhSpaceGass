using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetSteelMemberCheckSummaryTests
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

    private static SgModelData CreateTestModel()
    {
        var model = new SgModelData();
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(10, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(20, 0, 0)] = 3;
        model.MemberMap[1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0));
        model.MemberMap[2] = (new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0));
        model.LoadCaseMap["ULS 1"] = 1;
        model.LoadCaseMap["ULS 2"] = 2;
        model.CombinationLoadCaseMap["ULS Envelope"] = 3;
        return model;
    }

    // ── Guard: not connected ──────────────────────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSteelMemberCheckSummaryAsync(CreateTestModel()));
    }

    // ── Calls the API ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_WhenConnected_CallsApiOnce()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>());

        await session.GetSteelMemberCheckSummaryAsync(CreateTestModel());

        await _api.Received(1).GetSteelMemberCheckSummaryAsync(
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Maps API responses to domain model ────────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_MapsAllFields()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>
            {
                new()
                {
                    Member = 1, Section = "310UB40", Flag = "PASS", LoadFactor = 1.5f,
                    CriticalCase = 1, Failure = "Combined bending & axial",
                    SegmentLength = 2500f, TotalLength = 5000f, Yield = 320f
                }
            });

        var result = await session.GetSteelMemberCheckSummaryAsync(CreateTestModel());

        Assert.Single(result.Checks);
        var c = result.Checks[0];
        Assert.Equal(1, c.DesignGroupId);
        Assert.Equal("310UB40", c.Section);
        Assert.Equal("PASS", c.Flag);
        Assert.Equal(1.5, c.LoadFactor, 3);
        Assert.Equal(1, c.CriticalCaseId);
        Assert.Equal("Combined bending & axial", c.FailureMode);
        Assert.Equal(2500.0, c.SegmentLength, 3);
        Assert.Equal(5000.0, c.TotalLength, 3);
        Assert.Equal(320.0, c.Yield, 3);
    }

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_OrdersByDesignGroupId()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>
            {
                new() { Member = 2, Section = "S2", Flag = "FAIL", LoadFactor = 0.8f, CriticalCase = 1 },
                new() { Member = 1, Section = "S1", Flag = "PASS", LoadFactor = 1.5f, CriticalCase = 2 }
            });

        var result = await session.GetSteelMemberCheckSummaryAsync(CreateTestModel());

        Assert.Equal(2, result.Checks.Count);
        Assert.Equal(1, result.Checks[0].DesignGroupId);
        Assert.Equal(2, result.Checks[1].DesignGroupId);
    }

    // ── Null Member ID skips record ───────────────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_NullMember_SkipsRecord()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>
            {
                new() { Member = null, Section = "S1", Flag = "PASS", LoadFactor = 1.5f },
                new() { Member = 1, Section = "S1", Flag = "PASS", LoadFactor = 1.2f, CriticalCase = 1 }
            });

        var result = await session.GetSteelMemberCheckSummaryAsync(CreateTestModel());

        Assert.Single(result.Checks);
        Assert.Equal(1, result.Checks[0].DesignGroupId);
    }

    // ── Design-group filter passed server-side ───────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_WithDesignGroupFilter_PassesToApi()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync("1,2", Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>
            {
                new() { Member = 1, Section = "S1", Flag = "PASS", LoadFactor = 1.5f, CriticalCase = 1 },
                new() { Member = 2, Section = "S2", Flag = "PASS", LoadFactor = 1.2f, CriticalCase = 1 }
            });

        var result = await session.GetSteelMemberCheckSummaryAsync(
            CreateTestModel(), designGroupFilter: new[] { 1, 2 });

        Assert.Equal(2, result.Checks.Count);
        await _api.Received(1).GetSteelMemberCheckSummaryAsync("1,2", Arg.Any<CancellationToken>());
    }

    // ── Unmatched design-group filter warns and skips ─────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_UnmatchedDesignGroupFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>());

        var result = await session.GetSteelMemberCheckSummaryAsync(
            CreateTestModel(), designGroupFilter: new[] { 99 });

        Assert.Contains(result.Warnings,
            w => w.Contains("99") && w.Contains("does not match"));
    }

    // ── Null-safe defaults for optional fields ────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_NullNumericFields_DefaultToZero()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SteelCheckSummary>
            {
                new()
                {
                    Member = 1, Section = null, Flag = null, LoadFactor = null,
                    CriticalCase = null, Failure = null,
                    SegmentLength = null, TotalLength = null, Yield = null
                }
            });

        var result = await session.GetSteelMemberCheckSummaryAsync(CreateTestModel());

        var c = result.Checks[0];
        Assert.Equal(1, c.DesignGroupId);
        Assert.Equal("", c.Section);
        Assert.Equal("", c.Flag);
        Assert.Equal(0.0, c.LoadFactor);
        Assert.Null(c.CriticalCaseId);
        Assert.Equal("", c.FailureMode);
        Assert.Equal(0.0, c.SegmentLength);
        Assert.Equal(0.0, c.TotalLength);
        Assert.Equal(0.0, c.Yield);
    }

    // ── Error handling ────────────────────────────────────────────────

    [Fact]
    public async Task GetSteelMemberCheckSummaryAsync_ApiException_WrappedAsInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.GetSteelMemberCheckSummaryAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSteelMemberCheckSummaryAsync(CreateTestModel()));
        Assert.Contains("steel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
