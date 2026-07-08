using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetNodeReactionsTests
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

    /// <summary>
    ///     Builds a minimal SgModelData with known node and load case maps.
    ///     Node 1 → (0,0,0), Node 2 → (5,0,0), Node 3 → (10,0,0)
    ///     Load Case "Dead Load" → 1, "Live Load" → 2
    /// </summary>
    private static SgModelData CreateTestModel()
    {
        var model = new SgModelData();
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(5, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(10, 0, 0)] = 3;
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;
        return model;
    }

    // ── Guard: not connected ──────────────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetNodeReactionsAsync(CreateTestModel()));
    }

    // ── Calls the API ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WhenConnected_CallsApiGetNodeReactions()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        await session.GetNodeReactionsAsync(CreateTestModel());

        await _api.Received(1).GetNodeReactionsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Maps API responses to domain model ────────────────────────────

    [Fact]
    public async Task GetNodeReactions_MapsApiResultsToDomainModel()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>
            {
                new() { Node = 1, LoadCase = 1, Fx = 10f, Fy = 20f, Fz = 30f, Mx = 1f, My = 2f, Mz = 3f },
                new() { Node = 3, LoadCase = 1, Fx = -10f, Fy = -20f, Fz = -30f, Mx = -1f, My = -2f, Mz = -3f }
            });

        var result = await session.GetNodeReactionsAsync(CreateTestModel());

        Assert.Equal(2, result.Reactions.Count);

        var r1 = result.Reactions[0];
        Assert.Equal(1, r1.NodeId);
        Assert.Equal(1, r1.LoadCaseId);
        Assert.Equal(10.0, r1.Fx, 3);
        Assert.Equal(20.0, r1.Fy, 3);
        Assert.Equal(30.0, r1.Fz, 3);
        Assert.Equal(1.0, r1.Mx, 3);
        Assert.Equal(2.0, r1.My, 3);
        Assert.Equal(3.0, r1.Mz, 3);

        var r2 = result.Reactions[1];
        Assert.Equal(3, r2.NodeId);
        Assert.Equal(1, r2.LoadCaseId);
        Assert.Equal(-10.0, r2.Fx, 3);
    }

    // ── Multiple load cases ───────────────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_MultipleLoadCases_AllMapped()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>
            {
                new() { Node = 1, LoadCase = 1, Fx = 10f, Fy = 0, Fz = 50f, Mx = 0, My = 0, Mz = 0 },
                new() { Node = 1, LoadCase = 2, Fx = 5f, Fy = 0, Fz = 25f, Mx = 0, My = 0, Mz = 0 },
                new() { Node = 3, LoadCase = 1, Fx = -10f, Fy = 0, Fz = -50f, Mx = 0, My = 0, Mz = 0 },
                new() { Node = 3, LoadCase = 2, Fx = -5f, Fy = 0, Fz = -25f, Mx = 0, My = 0, Mz = 0 }
            });

        var result = await session.GetNodeReactionsAsync(CreateTestModel());

        Assert.Equal(4, result.Reactions.Count);
        // Check both load cases present
        Assert.Contains(result.Reactions, r => r.LoadCaseId == 1);
        Assert.Contains(result.Reactions, r => r.LoadCaseId == 2);
    }

    // ── Empty results ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WhenNoReactions_ReturnsEmptyWithWarning()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        var result = await session.GetNodeReactionsAsync(CreateTestModel());

        Assert.Empty(result.Reactions);
        Assert.Single(result.Warnings);
        Assert.Contains("No node reactions found", result.Warnings[0]);
    }

    // ── API throws ────────────────────────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WhenApiThrows_ThrowsInvalidOperationWithFormattedMessage()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection lost"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetNodeReactionsAsync(CreateTestModel()));

        Assert.Contains("querying node reactions", ex.Message);
    }

    // ── Null node/loadcase fields handled ─────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WhenNullableFieldsAreNull_DefaultsToZero()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>
            {
                new() { Node = 1, LoadCase = 1, Fx = null, Fy = null, Fz = null, Mx = null, My = null, Mz = null }
            });

        var result = await session.GetNodeReactionsAsync(CreateTestModel());

        Assert.Single(result.Reactions);
        var r = result.Reactions[0];
        Assert.Equal(0.0, r.Fx);
        Assert.Equal(0.0, r.Fy);
        Assert.Equal(0.0, r.Fz);
        Assert.Equal(0.0, r.Mx);
        Assert.Equal(0.0, r.My);
        Assert.Equal(0.0, r.Mz);
    }

    // ── Node filter — passes IDs to API ─────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WithNodeFilter_PassesNodeIdsToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        var filterNodeIds = new List<int>
        {
            1,
            3
        };

        await session.GetNodeReactionsAsync(model, filterNodeIds);

        // Should pass "1,3" to the API
        await _api.Received(1).GetNodeReactionsAsync(
            Arg.Is<string?>(s => s != null && s.Contains("1") && s.Contains("3")),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ── Node filter — unmatched ID warns ────────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WithUnmatchedNodeFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>
            {
                new() { Node = 1, LoadCase = 1, Fx = 10f, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
            });

        var filterNodeIds = new List<int>
        {
            1,
            99
        };

        var result = await session.GetNodeReactionsAsync(model, filterNodeIds);

        Assert.Contains(result.Warnings, w => w.Contains("node ID 99") && w.Contains("does not match"));
    }

    // ── Load case filter — resolves names to IDs ──────────────────────

    [Fact]
    public async Task GetNodeReactions_WithLoadCaseFilter_PassesLoadCaseIdsToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        var loadCaseFilter = new List<string> { "Dead Load" };

        await session.GetNodeReactionsAsync(model, loadCaseFilter: loadCaseFilter);

        // Should pass "1" to the API (Dead Load → ID 1)
        await _api.Received(1).GetNodeReactionsAsync(
            Arg.Any<string?>(),
            Arg.Is<string?>(s => s == "1"),
            Arg.Any<CancellationToken>());
    }

    // ── Load case filter — unmatched name warns ───────────────────────

    [Fact]
    public async Task GetNodeReactions_WithUnmatchedLoadCaseFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        var loadCaseFilter = new List<string> { "Dead Load", "Wind Load" };

        var result = await session.GetNodeReactionsAsync(model, loadCaseFilter: loadCaseFilter);

        Assert.Contains(result.Warnings, w => w.Contains("Wind Load") && w.Contains("does not match"));
    }

    // ── Both filters applied simultaneously ───────────────────────────

    [Fact]
    public async Task GetNodeReactions_WithBothFilters_PassesBothToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        var filterNodeIds = new List<int> { 1 };
        var loadCaseFilter = new List<string> { "Live Load" };

        await session.GetNodeReactionsAsync(model,
            filterNodeIds, loadCaseFilter);

        await _api.Received(1).GetNodeReactionsAsync(
            Arg.Is<string?>(s => s == "1"), // node ID 1
            Arg.Is<string?>(s => s == "2"), // Live Load → ID 2
            Arg.Any<CancellationToken>());
    }

    // ── No filters — passes nulls to API ──────────────────────────────

    [Fact]
    public async Task GetNodeReactions_WithNoFilters_PassesNullsToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>());

        await session.GetNodeReactionsAsync(model);

        await _api.Received(1).GetNodeReactionsAsync(
            null, null, Arg.Any<CancellationToken>());
    }

    // ── Reactions with null Node/LoadCase ID are skipped ───────────────

    [Fact]
    public async Task GetNodeReactions_SkipsReactionsWithNullNodeOrLoadCase()
    {
        var session = CreateConnectedSession();
        _api.GetNodeReactionsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeReaction>
            {
                new() { Node = null, LoadCase = 1, Fx = 10f, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
                new() { Node = 1, LoadCase = null, Fx = 10f, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
                new() { Node = 1, LoadCase = 1, Fx = 10f, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
            });

        var result = await session.GetNodeReactionsAsync(CreateTestModel());

        // Only the third reaction (valid IDs) should be mapped
        Assert.Single(result.Reactions);
        Assert.Equal(1, result.Reactions[0].NodeId);
        Assert.Equal(1, result.Reactions[0].LoadCaseId);
    }
}