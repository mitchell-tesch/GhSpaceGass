using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetSelfWeightLoadsDataTests
{
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();
    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    private SpaceGassSession CreateConnectedSession()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);
        session.ConnectAsync().GetAwaiter().GetResult();
        _api.NewJobAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = true } });
        session.NewJobAsync().GetAwaiter().GetResult();
        return session;
    }

    private static SgModelData MakeModel()
    {
        var model = new SgModelData();
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;
        model.CombinationLoadCaseMap["ULS"] = 3;
        return model;
    }

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSelfWeightLoadsDataAsync(MakeModel()));
    }

    // ── Empty results ────────────────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_Empty_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>());

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Contains(result.Warnings, w => w.Contains("No self-weight loads"));
        Assert.Empty(result.Loads);
    }

    // ── Returns load data ────────────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_ReturnsLoadData()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>
        {
            new() { LoadCase = 1, LoadCategory = 5, AccelerationX = 0, AccelerationY = -9.81, AccelerationZ = 0 },
            new() { LoadCase = 2, LoadCategory = null, AccelerationX = 1.0, AccelerationY = -9.81, AccelerationZ = 0.5 }
        });

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.Loads.Count);
        Assert.Empty(result.Warnings);

        Assert.Equal(1, result.Loads[0].LoadCaseId);
        Assert.Equal("Dead Load", result.Loads[0].LoadCaseName);
        Assert.Equal(5, result.Loads[0].LoadCategoryId);
        Assert.Equal(0, result.Loads[0].AccelerationX);
        Assert.Equal(-9.81, result.Loads[0].AccelerationY);
        Assert.Equal(0, result.Loads[0].AccelerationZ);

        Assert.Equal(2, result.Loads[1].LoadCaseId);
        Assert.Equal("Live Load", result.Loads[1].LoadCaseName);
        Assert.Equal(0, result.Loads[1].LoadCategoryId);
        Assert.Equal(1.0, result.Loads[1].AccelerationX);
        Assert.Equal(0.5, result.Loads[1].AccelerationZ);
    }

    // ── Resolves combination load case names ─────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_ResolvesCombinationLoadCaseName()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>
        {
            new() { LoadCase = 3, AccelerationY = -9.81 }
        });

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Single(result.Loads);
        Assert.Equal("ULS", result.Loads[0].LoadCaseName);
    }

    // ── Unresolved load case ID shows fallback ───────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_UnresolvedLoadCaseId_ShowsFallback()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>
        {
            new() { LoadCase = 99, AccelerationY = -9.81 }
        });

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Single(result.Loads);
        Assert.Equal("LC99", result.Loads[0].LoadCaseName);
    }

    // ── Nullable fields default ──────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_NullableFieldsDefaultToZero()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>
        {
            new() { LoadCase = 1, LoadCategory = null, AccelerationX = null, AccelerationY = null, AccelerationZ = null }
        });

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Single(result.Loads);
        Assert.Equal(0, result.Loads[0].LoadCategoryId);
        Assert.Equal(0, result.Loads[0].AccelerationX);
        Assert.Equal(0, result.Loads[0].AccelerationY);
        Assert.Equal(0, result.Loads[0].AccelerationZ);
    }

    // ── Skips entries with null load case ─────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_SkipsNullLoadCase()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>
        {
            new() { LoadCase = null, AccelerationY = -9.81 },
            new() { LoadCase = 1, AccelerationY = -9.81 }
        });

        var result = await session.GetSelfWeightLoadsDataAsync(MakeModel());

        Assert.Single(result.Loads);
        Assert.Equal(1, result.Loads[0].LoadCaseId);
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_ApiError_Wrapped()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>())
            .Returns<List<SelfWeightLoad>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSelfWeightLoadsDataAsync(MakeModel()));

        Assert.Contains("querying self-weight loads", ex.Message);
    }

    // ── Calls API ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSelfWeightLoadsData_CallsListEndpoint()
    {
        var session = CreateConnectedSession();
        _api.ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<SelfWeightLoad>());

        await session.GetSelfWeightLoadsDataAsync(MakeModel());

        await _api.Received(1).ListSelfWeightLoadsAsync(Arg.Any<CancellationToken>());
    }
}
