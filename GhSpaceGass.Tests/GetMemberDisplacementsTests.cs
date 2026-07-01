using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetMemberDisplacementsTests
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

    // ── Maps API results to domain model (global + local) ───────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_MapsGlobalAndLocalTranslations()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1, 2, 3 },
                    Location = new List<float?> { 0f, 5f, 10f },
                    TxGlobal = new List<float?> { 0f, 0.1f, 0.2f },
                    TyGlobal = new List<float?> { 0f, -0.5f, -1.0f },
                    TzGlobal = new List<float?> { 0f, 0f, 0f },
                    TxLocal = new List<float?> { 0f, 0.05f, 0.1f },
                    TyLocal = new List<float?> { 0f, -0.3f, -0.6f },
                    TzLocal = new List<float?> { 0f, 0f, 0f }
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model);

        Assert.Equal(3, result.Displacements.Count);

        // First station
        var d0 = result.Displacements[0];
        Assert.Equal(1, d0.MemberId);
        Assert.Equal(1, d0.LoadCaseId);
        Assert.Equal(1, d0.Station);
        Assert.Equal(0.0, d0.Location, 3);
        Assert.Equal(0, d0.TxGlobal, 3);
        Assert.Equal(0, d0.TyGlobal, 3);
        Assert.Equal(0, d0.TzGlobal, 3);
        Assert.Equal(0, d0.TxLocal, 3);
        Assert.Equal(0, d0.TyLocal, 3);
        Assert.Equal(0, d0.TzLocal, 3);

        // Middle station — global
        var d1 = result.Displacements[1];
        Assert.Equal(2, d1.Station);
        Assert.Equal(5.0, d1.Location, 3);
        Assert.Equal(0.1, d1.TxGlobal, 3);
        Assert.Equal(-0.5, d1.TyGlobal, 3);
        Assert.Equal(0, d1.TzGlobal, 3);

        // Middle station — local
        Assert.Equal(0.05, d1.TxLocal, 3);
        Assert.Equal(-0.3, d1.TyLocal, 3);
        Assert.Equal(0, d1.TzLocal, 3);
    }

    // ── Multiple members ────────────────────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_MultipleMembers_AllMapped()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1, 2 },
                    Location = new List<float?> { 0f, 10f },
                    TxGlobal = new List<float?> { 0f, 0.1f },
                    TyGlobal = new List<float?> { 0f, 0f },
                    TzGlobal = new List<float?> { 0f, 0f },
                    TxLocal = new List<float?> { 0f, 0f },
                    TyLocal = new List<float?> { 0f, 0f },
                    TzLocal = new List<float?> { 0f, 0f }
                },
                new()
                {
                    Member = 2, LoadCase = 1,
                    Station = new List<int?> { 1, 2 },
                    Location = new List<float?> { 0f, 10f },
                    TxGlobal = new List<float?> { 0.1f, 0.2f },
                    TyGlobal = new List<float?> { 0f, 0f },
                    TzGlobal = new List<float?> { 0f, 0f },
                    TxLocal = new List<float?> { 0f, 0f },
                    TyLocal = new List<float?> { 0f, 0f },
                    TzLocal = new List<float?> { 0f, 0f }
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model);

        Assert.Equal(4, result.Displacements.Count);
        Assert.Contains(result.Displacements, d => d.MemberId == 1);
        Assert.Contains(result.Displacements, d => d.MemberId == 2);
    }

    // ── Member filter resolves geometry to IDs ──────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_WithMemberFilter_ResolvesGeometryToId()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync("1", null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    TxGlobal = new List<float?> { 0f },
                    TyGlobal = new List<float?> { 0f },
                    TzGlobal = new List<float?> { 0f },
                    TxLocal = new List<float?> { 0f },
                    TyLocal = new List<float?> { 0f },
                    TzLocal = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)) });

        Assert.Single(result.Displacements);
        await _api.Received(1).GetMemberIntermediateDisplacementsAsync("1", null, Arg.Any<CancellationToken>());
    }

    // ── Unmatched member filter warns ───────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_UnmatchedMemberFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>());

        var result = await session.GetMemberDisplacementsAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(99, 0, 0)) });

        Assert.Contains(result.Warnings, w => w.Contains("does not match any model member"));
    }

    // ── Load case filter resolves names to IDs ──────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_WithLoadCaseFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, "2", Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new()
                {
                    Member = 1, LoadCase = 2,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    TxGlobal = new List<float?> { 0.3f },
                    TyGlobal = new List<float?> { 0f },
                    TzGlobal = new List<float?> { 0f },
                    TxLocal = new List<float?> { 0f },
                    TyLocal = new List<float?> { 0f },
                    TzLocal = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model,
            loadCaseFilter: new[] { "Live Load" });

        Assert.Single(result.Displacements);
        Assert.Equal(2, result.Displacements[0].LoadCaseId);
    }

    // ── Unmatched load case filter warns ─────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_UnmatchedLoadCaseFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>());

        var result = await session.GetMemberDisplacementsAsync(model,
            loadCaseFilter: new[] { "Nonexistent" });

        Assert.Contains(result.Warnings, w => w.Contains("Nonexistent"));
    }

    // ── Empty results ───────────────────────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_EmptyResults_WarnsNoDisplacements()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>());

        var result = await session.GetMemberDisplacementsAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No member displacements found"));
    }

    // ── Null Member or LoadCase skips record ────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_NullMemberOrLoadCase_SkipsRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new() { Member = null, LoadCase = 1, Station = new List<int?> { 1 } },
                new() { Member = 1, LoadCase = null, Station = new List<int?> { 1 } },
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 },
                    Location = new List<float?> { 0f },
                    TxGlobal = new List<float?> { 0.5f },
                    TyGlobal = new List<float?> { 0f },
                    TzGlobal = new List<float?> { 0f },
                    TxLocal = new List<float?> { 0.3f },
                    TyLocal = new List<float?> { 0f },
                    TzLocal = new List<float?> { 0f }
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model);

        Assert.Single(result.Displacements);
        Assert.Equal(0.5, result.Displacements[0].TxGlobal, 3);
        Assert.Equal(0.3, result.Displacements[0].TxLocal, 3);
    }

    // ── Nullable values default to zero ─────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_NullableValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Station = new List<int?> { 1 }
                    // All displacement lists null
                }
            });

        var result = await session.GetMemberDisplacementsAsync(model);

        Assert.Single(result.Displacements);
        Assert.Equal(0, result.Displacements[0].TxGlobal);
        Assert.Equal(0, result.Displacements[0].TyGlobal);
        Assert.Equal(0, result.Displacements[0].TzGlobal);
        Assert.Equal(0, result.Displacements[0].TxLocal);
        Assert.Equal(0, result.Displacements[0].TyLocal);
        Assert.Equal(0, result.Displacements[0].TzLocal);
        Assert.Equal(0, result.Displacements[0].Location);
    }

    // ── Connection guard ────────────────────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetMemberDisplacementsAsync(new SgModelData()));
    }

    // ── API exception wrapping ──────────────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_ApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetMemberDisplacementsAsync(new SgModelData()));
        Assert.Contains("querying member displacements", ex.Message);
    }

    // ── No filters passes nulls ─────────────────────────────────────

    [Fact]
    public async Task GetMemberDisplacementsAsync_WithNoFilters_PassesNullsToApi()
    {
        var session = CreateConnectedSession();
        _api.GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberIntermediateDisplacement>());

        await session.GetMemberDisplacementsAsync(CreateTestModel());

        await _api.Received(1).GetMemberIntermediateDisplacementsAsync(null, null, Arg.Any<CancellationToken>());
    }
}