using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetMemberIntermediateForcesTests
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
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;
        return model;
    }

    // ── Maps API results to domain model ────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_MapsStationResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1, 2, 3 },
                    Location = new List<float?> { 0f, 5f, 10f },
                    Fx = new List<float?> { 10f, 10f, 10f },
                    Fy = new List<float?> { 5f, 0f, -5f },
                    Fz = new List<float?> { 0f, 0f, 0f },
                    Mx = new List<float?> { 0f, 0f, 0f },
                    My = new List<float?> { 0f, 0f, 0f },
                    Mz = new List<float?> { 25f, 0f, -25f }
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model);

        Assert.Equal(3, result.Forces.Count);

        // First station
        Assert.Equal(1, result.Forces[0].MemberId);
        Assert.Equal(1, result.Forces[0].LoadCaseId);
        Assert.Equal(1, result.Forces[0].Station);
        Assert.Equal(0.0, result.Forces[0].Location, 3);
        Assert.Equal(10, result.Forces[0].Fx, 3);
        Assert.Equal(5, result.Forces[0].Fy, 3);
        Assert.Equal(25, result.Forces[0].Mz, 3);

        // Last station
        Assert.Equal(3, result.Forces[2].Station);
        Assert.Equal(10.0, result.Forces[2].Location, 3);
        Assert.Equal(-5, result.Forces[2].Fy, 3);
        Assert.Equal(-25, result.Forces[2].Mz, 3);
    }

    // ── Multiple members, same load case ────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_MultipleMembers_AllMapped()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1, 2 },
                    Location = new List<float?> { 0f, 10f },
                    Fx = new List<float?> { 10f, 10f },
                    Fy = new List<float?> { 0f, 0f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 0f, 0f }
                },
                new()
                {
                    Member = 2, LoadCase = 1,
                    Station = new List<int?> { 1, 2 },
                    Location = new List<float?> { 0f, 10f },
                    Fx = new List<float?> { -5f, -5f },
                    Fy = new List<float?> { 0f, 0f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 0f, 0f }
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model);

        // 2 stations each for 2 members = 4 total
        Assert.Equal(4, result.Forces.Count);
        Assert.Contains(result.Forces, f => f.MemberId == 1);
        Assert.Contains(result.Forces, f => f.MemberId == 2);
    }

    // ── Member filter resolves geometry to IDs ──────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_WithMemberFilter_ResolvesGeometryToId()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync("1", null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    Fx = new List<float?> { 10f },
                    Fy = new List<float?> { 0f },
                    Fz = new List<float?> { 0f },
                    Mx = new List<float?> { 0f },
                    My = new List<float?> { 0f },
                    Mz = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)) });

        Assert.Single(result.Forces);
        await _api.Received(1).GetMemberIntermediateForcesAsync("1", null, Arg.Any<CancellationToken>());
    }

    // ── Unmatched member filter warns ───────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_UnmatchedMemberFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>());

        var result = await session.GetMemberIntermediateForcesAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(99, 0, 0)) });

        Assert.Contains(result.Warnings, w => w.Contains("does not match any model member"));
    }

    // ── Load case filter resolves names to IDs ──────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_WithLoadCaseFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, "1", Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    Fx = new List<float?> { 5f },
                    Fy = new List<float?> { 0f },
                    Fz = new List<float?> { 0f },
                    Mx = new List<float?> { 0f },
                    My = new List<float?> { 0f },
                    Mz = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model,
            loadCaseFilter: new[] { "Dead Load" });

        Assert.Single(result.Forces);
        Assert.Equal(1, result.Forces[0].LoadCaseId);
    }

    // ── Empty results ───────────────────────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_EmptyResults_WarnsNoForces()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>());

        var result = await session.GetMemberIntermediateForcesAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No intermediate member forces found"));
    }

    // ── Null Member or LoadCase skips record ────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_NullMemberOrLoadCase_SkipsRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new() { Member = null, LoadCase = 1, Station = new List<int?> { 1 } },
                new() { Member = 1, LoadCase = null, Station = new List<int?> { 1 } },
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    Fx = new List<float?> { 5f },
                    Fy = new List<float?> { 0f },
                    Fz = new List<float?> { 0f },
                    Mx = new List<float?> { 0f },
                    My = new List<float?> { 0f },
                    Mz = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model);

        Assert.Single(result.Forces);
        Assert.Equal(5, result.Forces[0].Fx, 3);
    }

    // ── Nullable force values default to zero ───────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_NullableValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 }
                    // All force lists null
                }
            });

        var result = await session.GetMemberIntermediateForcesAsync(model);

        Assert.Single(result.Forces);
        Assert.Equal(0, result.Forces[0].Fx);
        Assert.Equal(0, result.Forces[0].Fy);
        Assert.Equal(0, result.Forces[0].Fz);
        Assert.Equal(0, result.Forces[0].Mx);
        Assert.Equal(0, result.Forces[0].My);
        Assert.Equal(0, result.Forces[0].Mz);
        Assert.Equal(0, result.Forces[0].Location);
    }

    // ── Connection guard ────────────────────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetMemberIntermediateForcesAsync(new SgModelData()));
    }

    // ── API exception wrapping ──────────────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_ApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetMemberIntermediateForcesAsync(new SgModelData()));
        Assert.Contains("querying intermediate member forces", ex.Message);
    }

    // ── No filters passes nulls ─────────────────────────────────────

    [Fact]
    public async Task GetMemberIntermediateForcesAsync_WithNoFilters_PassesNullsToApi()
    {
        var session = CreateConnectedSession();
        _api.GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateForce>());

        await session.GetMemberIntermediateForcesAsync(CreateTestModel());

        await _api.Received(1).GetMemberIntermediateForcesAsync(null, null, Arg.Any<CancellationToken>());
    }
}