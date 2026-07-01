using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetDynamicFrequencyResultsTests
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
        return model;
    }

    // ── Maps natural frequencies from API ────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_MapsNaturalFrequencies()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>
            {
                new()
                {
                    Mode = 1, LoadCase = 1, NaturalFrequencyProp = 2.5f, NaturalPeriod = 0.4f,
                    MassPartX = 0.8f, MassPartY = 0.1f, MassPartZ = 0.05f
                },
                new()
                {
                    Mode = 2, LoadCase = 1, NaturalFrequencyProp = 5.0f, NaturalPeriod = 0.2f,
                    MassPartX = 0.1f, MassPartY = 0.7f, MassPartZ = 0.02f
                }
            });
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Equal(2, result.NaturalFrequencies.Count);

        Assert.Equal(1, result.NaturalFrequencies[0].Mode);
        Assert.Equal(2.5, result.NaturalFrequencies[0].Frequency, 3);
        Assert.Equal(0.4, result.NaturalFrequencies[0].Period, 3);
        Assert.Equal(0.8, result.NaturalFrequencies[0].MassPartX, 3);
        Assert.Equal(0.1, result.NaturalFrequencies[0].MassPartY, 3);
        Assert.Equal(0.05, result.NaturalFrequencies[0].MassPartZ, 3);

        Assert.Equal(2, result.NaturalFrequencies[1].Mode);
        Assert.Equal(5.0, result.NaturalFrequencies[1].Frequency, 3);
        Assert.Equal(0.2, result.NaturalFrequencies[1].Period, 3);
    }

    // ── Maps mode shapes from API (flattened per node) ──────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_MapsModeShapes()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2, 3 },
                    Tx = new List<float?> { 0.1f, 0.5f, 0.9f },
                    Ty = new List<float?> { 0.0f, 0.2f, 0.4f },
                    Tz = new List<float?> { 0.0f, 0.0f, 0.0f },
                    Rx = new List<float?> { 0.01f, 0.02f, 0.03f },
                    Ry = new List<float?> { 0.0f, 0.0f, 0.0f },
                    Rz = new List<float?> { 0.0f, 0.0f, 0.0f }
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Equal(3, result.ModeShapes.Count);

        // Node 1 → (0,0,0)
        Assert.Equal(1, result.ModeShapes[0].Mode);
        Assert.Equal(1, result.ModeShapes[0].NodeId);
        Assert.Equal(0.1, result.ModeShapes[0].Tx, 3);
        Assert.Equal(0.0, result.ModeShapes[0].Ty, 3);
        Assert.Equal(0.0, result.ModeShapes[0].Tz, 3);
        Assert.Equal(0.01, result.ModeShapes[0].Rx, 3);

        // Node 2 → (10,0,0)
        Assert.Equal(1, result.ModeShapes[1].Mode);
        Assert.Equal(2, result.ModeShapes[1].NodeId);
        Assert.Equal(0.5, result.ModeShapes[1].Tx, 3);
        Assert.Equal(0.2, result.ModeShapes[1].Ty, 3);

        // Node 3 → (20,0,0)
        Assert.Equal(1, result.ModeShapes[2].Mode);
        Assert.Equal(3, result.ModeShapes[2].NodeId);
        Assert.Equal(0.9, result.ModeShapes[2].Tx, 3);
        Assert.Equal(0.4, result.ModeShapes[2].Ty, 3);
    }

    // ── Mode filter passed server-side ──────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_WithModeFilter_PassesModesToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync("1,3", Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>
            {
                new() { Mode = 1, LoadCase = 1, NaturalFrequencyProp = 2.5f },
                new() { Mode = 3, LoadCase = 1, NaturalFrequencyProp = 12.0f }
            });
        _api.GetModeShapesAsync("1,3", null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        var result = await session.GetDynamicFrequencyResultsAsync(model,
            modesFilter: new[] { 1, 3 });

        Assert.Equal(2, result.NaturalFrequencies.Count);
        Assert.Equal(1, result.NaturalFrequencies[0].Mode);
        Assert.Equal(3, result.NaturalFrequencies[1].Mode);

        await _api.Received(1).GetNaturalFrequenciesAsync("1,3", Arg.Any<CancellationToken>());
        await _api.Received(1).GetModeShapesAsync("1,3", null, Arg.Any<CancellationToken>());
    }

    // ── Node filter resolves geometry to IDs for mode shapes ────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_WithNodeFilter_ResolvesGeometryToId()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, "1,2", Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2 },
                    Tx = new List<float?> { 0.1f, 0.5f },
                    Ty = new List<float?> { 0.0f, 0.2f },
                    Tz = new List<float?> { 0.0f, 0.0f },
                    Rx = new List<float?> { 0.0f, 0.0f },
                    Ry = new List<float?> { 0.0f, 0.0f },
                    Rz = new List<float?> { 0.0f, 0.0f }
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model,
            nodesFilter: new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0) });

        Assert.Equal(2, result.ModeShapes.Count);
        await _api.Received(1).GetModeShapesAsync(null, "1,2", Arg.Any<CancellationToken>());
    }

    // ── Unmatched node filter warns and skips ────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_UnmatchedNodeFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        var result = await session.GetDynamicFrequencyResultsAsync(model,
            nodesFilter: new[] { new SgPoint3D(99, 99, 99) });

        Assert.Contains(result.Warnings, w => w.Contains("does not match any model node"));
    }

    // ── Null Mode skips natural frequency record ────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_NullMode_SkipsNaturalFrequencyRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>
            {
                new() { Mode = null, LoadCase = 1, NaturalFrequencyProp = 2.5f },
                new() { Mode = 1, LoadCase = 1, NaturalFrequencyProp = 5.0f }
            });
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Single(result.NaturalFrequencies);
        Assert.Equal(1, result.NaturalFrequencies[0].Mode);
    }

    // ── Null Mode skips mode shape record ───────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_NullMode_SkipsModeShapeRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = null, LoadCase = 1,
                    Node = new List<int?> { 1 },
                    Tx = new List<float?> { 0.1f },
                    Ty = new List<float?> { 0.0f },
                    Tz = new List<float?> { 0.0f },
                    Rx = new List<float?> { 0.0f },
                    Ry = new List<float?> { 0.0f },
                    Rz = new List<float?> { 0.0f }
                },
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { 1 },
                    Tx = new List<float?> { 0.5f },
                    Ty = new List<float?> { 0.0f },
                    Tz = new List<float?> { 0.0f },
                    Rx = new List<float?> { 0.0f },
                    Ry = new List<float?> { 0.0f },
                    Rz = new List<float?> { 0.0f }
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Single(result.ModeShapes);
        Assert.Equal(1, result.ModeShapes[0].Mode);
    }

    // ── Null node ID skips individual mode shape entry ───────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_NullNodeId_SkipsEntry()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { null, 2 },
                    Tx = new List<float?> { 0.1f, 0.5f },
                    Ty = new List<float?> { 0.0f, 0.2f },
                    Tz = new List<float?> { 0.0f, 0.0f },
                    Rx = new List<float?> { 0.0f, 0.0f },
                    Ry = new List<float?> { 0.0f, 0.0f },
                    Rz = new List<float?> { 0.0f, 0.0f }
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Single(result.ModeShapes);
        Assert.Equal(2, result.ModeShapes[0].NodeId);
    }

    // ── Nullable values default to zero ──────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_NullableValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>
            {
                new() { Mode = 1, LoadCase = 1 } // all nullable values null
            });
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { 1 },
                    Tx = null, Ty = null, Tz = null,
                    Rx = null, Ry = null, Rz = null
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Single(result.NaturalFrequencies);
        Assert.Equal(0, result.NaturalFrequencies[0].Frequency);
        Assert.Equal(0, result.NaturalFrequencies[0].Period);
        Assert.Equal(0, result.NaturalFrequencies[0].MassPartX);
        Assert.Equal(0, result.NaturalFrequencies[0].MassPartY);
        Assert.Equal(0, result.NaturalFrequencies[0].MassPartZ);

        Assert.Single(result.ModeShapes);
        Assert.Equal(0, result.ModeShapes[0].Tx);
        Assert.Equal(0, result.ModeShapes[0].Ty);
        Assert.Equal(0, result.ModeShapes[0].Tz);
        Assert.Equal(0, result.ModeShapes[0].Rx);
        Assert.Equal(0, result.ModeShapes[0].Ry);
        Assert.Equal(0, result.ModeShapes[0].Rz);
    }

    // ── Empty results emit warning ──────────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_EmptyResults_WarnsNoDynamicResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        var result = await session.GetDynamicFrequencyResultsAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No dynamic frequency results found"));
    }

    // ── Connection guard ────────────────────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetDynamicFrequencyResultsAsync(new SgModelData()));
    }

    // ── API exception wrapping — natural frequencies ────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_NaturalFrequenciesApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetDynamicFrequencyResultsAsync(new SgModelData()));
        Assert.Contains("querying natural frequencies", ex.Message);
    }

    // ── API exception wrapping — mode shapes ────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_ModeShapesApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetDynamicFrequencyResultsAsync(new SgModelData()));
        Assert.Contains("querying mode shapes", ex.Message);
    }

    // ── No filters passes nulls ─────────────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_WithNoFilters_PassesNullsToApi()
    {
        var session = CreateConnectedSession();
        _api.GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>());
        _api.GetModeShapesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>());

        await session.GetDynamicFrequencyResultsAsync(CreateTestModel());

        await _api.Received(1).GetNaturalFrequenciesAsync(null, Arg.Any<CancellationToken>());
        await _api.Received(1).GetModeShapesAsync(null, null, Arg.Any<CancellationToken>());
    }

    // ── Combined mode + node filters ────────────────────────────────

    [Fact]
    public async Task GetDynamicFrequencyResultsAsync_WithBothFilters_PassesBoth()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNaturalFrequenciesAsync("1", Arg.Any<CancellationToken>())
            .Returns(new List<NaturalFrequency>
            {
                new() { Mode = 1, LoadCase = 1, NaturalFrequencyProp = 2.5f }
            });
        _api.GetModeShapesAsync("1", "2", Arg.Any<CancellationToken>())
            .Returns(new List<ModeShape>
            {
                new()
                {
                    Mode = 1, LoadCase = 1,
                    Node = new List<int?> { 2 },
                    Tx = new List<float?> { 0.5f },
                    Ty = new List<float?> { 0.2f },
                    Tz = new List<float?> { 0.0f },
                    Rx = new List<float?> { 0.0f },
                    Ry = new List<float?> { 0.0f },
                    Rz = new List<float?> { 0.0f }
                }
            });

        var result = await session.GetDynamicFrequencyResultsAsync(model,
            modesFilter: new[] { 1 },
            nodesFilter: new[] { new SgPoint3D(10, 0, 0) });

        Assert.Single(result.NaturalFrequencies);
        Assert.Single(result.ModeShapes);

        await _api.Received(1).GetNaturalFrequenciesAsync("1", Arg.Any<CancellationToken>());
        await _api.Received(1).GetModeShapesAsync("1", "2", Arg.Any<CancellationToken>());
    }
}

