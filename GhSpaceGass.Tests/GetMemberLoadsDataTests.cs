using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetMemberLoadsDataTests
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
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(1, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(2, 0, 0)] = 3;
        model.MemberMap[1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0));
        model.MemberMap[2] = (new SgPoint3D(1, 0, 0), new SgPoint3D(2, 0, 0));
        model.LoadCaseMap["Dead Load"] = 10;
        model.LoadCaseMap["Live Load"] = 20;
        return model;
    }

    private void SetupEmpty()
    {
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>());
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());
    }

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetMemberLoadsDataAsync(MakeModel()));
    }

    // ── Empty results ────────────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_AllEmpty_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Contains(result.Warnings, w => w.Contains("No member loads"));
        Assert.Empty(result.MemberEntries);
    }

    // ── Concentrated loads ───────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_ConcentratedLoads_GroupedByMember()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>
        {
            new() { Member = 1, LoadCase = 10, LoadCategory = null, Fx = 10, Fy = -20, Fz = 0, Mx = 0, My = 0, Mz = 5, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local },
            new() { Member = 2, LoadCase = 20, LoadCategory = 3, Fx = 0, Fy = -50, Fz = 0, Mx = 0, My = 0, Mz = 0, Position = 2.5, PositionUnits = LoadPositionUnits.Actual, Axes = LoadAxes.GlobalInclined }
        });
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.MemberEntries.Count);

        // Member 1
        Assert.Equal(1, result.MemberEntries[0].MemberId);
        Assert.Equal(new SgPoint3D(0, 0, 0), result.MemberEntries[0].Start);
        Assert.Single(result.MemberEntries[0].ConcentratedLoads);
        Assert.Equal(10, result.MemberEntries[0].ConcentratedLoads[0].LoadCaseId);
        Assert.Equal("Dead Load", result.MemberEntries[0].ConcentratedLoads[0].LoadCaseName);
        Assert.Equal(10, result.MemberEntries[0].ConcentratedLoads[0].Fx);
        Assert.Equal(5, result.MemberEntries[0].ConcentratedLoads[0].Mz);
        Assert.Equal(50, result.MemberEntries[0].ConcentratedLoads[0].Position);
        Assert.Equal("Percent", result.MemberEntries[0].ConcentratedLoads[0].PositionUnits);
        Assert.Equal("Local", result.MemberEntries[0].ConcentratedLoads[0].Axes);

        // Member 2
        Assert.Equal(2, result.MemberEntries[1].MemberId);
        Assert.Equal("Global Inclined", result.MemberEntries[1].ConcentratedLoads[0].Axes);
        Assert.Equal("Actual", result.MemberEntries[1].ConcentratedLoads[0].PositionUnits);
        Assert.Equal(3, result.MemberEntries[1].ConcentratedLoads[0].LoadCategoryId);
    }

    // ── Distributed loads ────────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_DistributedLoads()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>());
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>
        {
            new() { Member = 1, LoadCase = 10, FxStart = 0, FyStart = -10, FzStart = 0, FxFinish = 0, FyFinish = -20, FzFinish = 0, StartPosition = 0, FinishPosition = 100, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Single(result.MemberEntries);
        Assert.Single(result.MemberEntries[0].DistributedLoads);
        Assert.Equal(-10, result.MemberEntries[0].DistributedLoads[0].FyStart);
        Assert.Equal(-20, result.MemberEntries[0].DistributedLoads[0].FyFinish);
        Assert.Equal(0, result.MemberEntries[0].DistributedLoads[0].StartPosition);
        Assert.Equal(100, result.MemberEntries[0].DistributedLoads[0].FinishPosition);
    }

    // ── Distributed moments ──────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_DistributedMoments()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>());
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>
        {
            new() { Member = 1, LoadCase = 10, MxStart = 5, MyStart = 0, MzStart = 0, MxFinish = 10, MyFinish = 0, MzFinish = 0, StartPosition = 0, FinishPosition = 100, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Single(result.MemberEntries);
        Assert.Single(result.MemberEntries[0].DistributedMoments);
        Assert.Equal(5, result.MemberEntries[0].DistributedMoments[0].MxStart);
        Assert.Equal(10, result.MemberEntries[0].DistributedMoments[0].MxFinish);
    }

    // ── Prestress loads ──────────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_PrestressLoads()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>());
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>
        {
            new() { Member = 2, LoadCase = 10, LoadCategory = null, Prestress = 500 }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Single(result.MemberEntries);
        Assert.Equal(2, result.MemberEntries[0].MemberId);
        Assert.Single(result.MemberEntries[0].PrestressLoads);
        Assert.Equal(500, result.MemberEntries[0].PrestressLoads[0].Prestress);
    }

    // ── Thermal loads (member only) ──────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_ThermalLoads_MemberOnly()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>());
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>
        {
            new() { ElementId = 1, ElementType = ThermalElementType.Member, LoadCase = 10, ThermalLoadProp = 30, YThermalGradient = 5, ZThermalGradient = 0 },
            new() { ElementId = 99, ElementType = ThermalElementType.Plate, LoadCase = 10, ThermalLoadProp = 20, YThermalGradient = 0, ZThermalGradient = 0 }
        });

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        // Only the member thermal load, not the plate one
        Assert.Single(result.MemberEntries);
        Assert.Equal(1, result.MemberEntries[0].MemberId);
        Assert.Single(result.MemberEntries[0].ThermalLoads);
        Assert.Equal(30, result.MemberEntries[0].ThermalLoads[0].Temperature);
        Assert.Equal(5, result.MemberEntries[0].ThermalLoads[0].YGradient);
    }

    // ── Mixed types on same member ───────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_MixedTypes_SameMember()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>
        {
            new() { Member = 1, LoadCase = 10, Fy = -10, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>
        {
            new() { Member = 1, LoadCase = 20, FyStart = -5, FyFinish = -5, StartPosition = 0, FinishPosition = 100, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>
        {
            new() { Member = 1, LoadCase = 10, Prestress = 100 }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Single(result.MemberEntries);
        Assert.Single(result.MemberEntries[0].ConcentratedLoads);
        Assert.Single(result.MemberEntries[0].DistributedLoads);
        Assert.Empty(result.MemberEntries[0].DistributedMoments);
        Assert.Single(result.MemberEntries[0].PrestressLoads);
        Assert.Empty(result.MemberEntries[0].ThermalLoads);
    }

    // ── Members ordered by ID ────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_MembersOrderedById()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>
        {
            new() { Member = 2, LoadCase = 10, Fy = -1, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local },
            new() { Member = 1, LoadCase = 10, Fy = -2, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.MemberEntries.Count);
        Assert.Equal(1, result.MemberEntries[0].MemberId);
        Assert.Equal(2, result.MemberEntries[1].MemberId);
    }

    // ── Unresolved member ID warns and skips ─────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_UnresolvedMemberId_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberConcentratedLoad>
        {
            new() { Member = 99, LoadCase = 10, Fy = -1, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local },
            new() { Member = 1, LoadCase = 10, Fy = -2, Position = 50, PositionUnits = LoadPositionUnits.Percent, Axes = LoadAxes.Local }
        });
        _api.ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedLoad>());
        _api.ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberDistributedMoment>());
        _api.ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<MemberPrestressLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetMemberLoadsDataAsync(MakeModel());

        Assert.Single(result.MemberEntries);
        Assert.Contains(result.Warnings, w => w.Contains("99"));
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_ApiError_Wrapped()
    {
        var session = CreateConnectedSession();
        _api.ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>())
            .Returns<List<MemberConcentratedLoad>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetMemberLoadsDataAsync(MakeModel()));

        Assert.Contains("querying member concentrated loads", ex.Message);
    }

    // ── Queries all endpoints ────────────────────────────────────────

    [Fact]
    public async Task GetMemberLoadsData_QueriesAllEndpoints()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        await session.GetMemberLoadsDataAsync(MakeModel());

        await _api.Received(1).ListMemberConcentratedLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListMemberDistributedLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListMemberDistributedMomentsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListMemberPrestressLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListThermalLoadsAsync(Arg.Any<CancellationToken>());
    }
}
