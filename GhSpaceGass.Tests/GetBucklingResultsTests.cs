using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetBucklingResultsTests
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

    // ── Maps load factors from API ──────────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_MapsLoadFactors()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>
            {
                new()
                {
                    Mode = 1, LoadCase = 1, LoadFactor = 5.2f, NodeAtMaxTrans = 2f, TransAxis = "Y", NodeAtMaxRotn = 3f,
                    RotnAxis = "Z"
                },
                new()
                {
                    Mode = 2, LoadCase = 1, LoadFactor = 8.1f, NodeAtMaxTrans = 1f, TransAxis = "X", NodeAtMaxRotn = 2f,
                    RotnAxis = "Y"
                }
            });
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Equal(2, result.LoadFactors.Count);

        Assert.Equal(1, result.LoadFactors[0].Mode);
        Assert.Equal(5.2, result.LoadFactors[0].LoadFactor, 3);
        Assert.Equal(new SgPoint3D(10, 0, 0), result.LoadFactors[0].NodeAtMaxTranslation); // node 2 → (10,0,0)
        Assert.Equal("Y", result.LoadFactors[0].TranslationAxis);
        Assert.Equal(new SgPoint3D(20, 0, 0), result.LoadFactors[0].NodeAtMaxRotation); // node 3 → (20,0,0)
        Assert.Equal("Z", result.LoadFactors[0].RotationAxis);

        Assert.Equal(2, result.LoadFactors[1].Mode);
        Assert.Equal(8.1, result.LoadFactors[1].LoadFactor, 3);
    }

    // ── Maps effective lengths from API ──────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_MapsEffectiveLengths()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>
            {
                new() { Member = 1, Mode = 1, LoadCase = 1, Ly = 8.5f, Lz = 10.0f, Pcr = 500f, Length = 10f },
                new() { Member = 2, Mode = 1, LoadCase = 1, Ly = 7.2f, Lz = 9.0f, Pcr = 450f, Length = 10f }
            });

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Equal(2, result.EffectiveLengths.Count);

        Assert.Equal(1, result.EffectiveLengths[0].MemberId);
        Assert.Equal(1, result.EffectiveLengths[0].Mode);
        Assert.Equal(8.5, result.EffectiveLengths[0].Ley, 3);
        Assert.Equal(10.0, result.EffectiveLengths[0].Lez, 3);
        Assert.Equal(500, result.EffectiveLengths[0].Pcr, 3);
        Assert.Equal(10, result.EffectiveLengths[0].Length, 3);
    }

    // ── Member filter resolves geometry to IDs ──────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_WithMemberFilter_ResolvesGeometryToId()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync("1", null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>
            {
                new() { Member = 1, Mode = 1, Ly = 8.5f, Lz = 10.0f, Pcr = 500f, Length = 10f }
            });

        var result = await session.GetBucklingResultsAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)) });

        Assert.Single(result.EffectiveLengths);
        await _api.Received(1).GetBucklingEffectiveLengthsAsync("1", null, null, Arg.Any<CancellationToken>());
    }

    // ── Unmatched member filter warns ───────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_UnmatchedMemberFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        var result = await session.GetBucklingResultsAsync(model,
            new[] { (new SgPoint3D(0, 0, 0), new SgPoint3D(99, 0, 0)) });

        Assert.Contains(result.Warnings, w => w.Contains("does not match any model member"));
    }

    // ── Mode filter applied client-side to load factors ─────────────

    [Fact]
    public async Task GetBucklingResultsAsync_WithModeFilter_FiltersLoadFactorsClientSide()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>
            {
                new() { Mode = 1, LoadCase = 1, LoadFactor = 5.2f },
                new() { Mode = 2, LoadCase = 1, LoadFactor = 8.1f },
                new() { Mode = 3, LoadCase = 1, LoadFactor = 12.3f }
            });
        // Mode filter also passed server-side for effective lengths
        _api.GetBucklingEffectiveLengthsAsync(null, "1,3", null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        var result = await session.GetBucklingResultsAsync(model,
            modesFilter: new[] { 1, 3 });

        Assert.Equal(2, result.LoadFactors.Count);
        Assert.Equal(1, result.LoadFactors[0].Mode);
        Assert.Equal(3, result.LoadFactors[1].Mode);
    }

    // ── Mode filter passed server-side for effective lengths ────────

    [Fact]
    public async Task GetBucklingResultsAsync_WithModeFilter_PassesModesToApi()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, "2", null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        await session.GetBucklingResultsAsync(model, modesFilter: new[] { 2 });

        await _api.Received(1).GetBucklingEffectiveLengthsAsync(null, "2", null, Arg.Any<CancellationToken>());
    }

    // ── Null Mode skips load factor record ───────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_NullMode_SkipsLoadFactorRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>
            {
                new() { Mode = null, LoadCase = 1, LoadFactor = 5.0f },
                new() { Mode = 1, LoadCase = 1, LoadFactor = 8.0f }
            });
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Single(result.LoadFactors);
        Assert.Equal(1, result.LoadFactors[0].Mode);
    }

    // ── Null Member or Mode skips effective length record ───────────

    [Fact]
    public async Task GetBucklingResultsAsync_NullMemberOrMode_SkipsEffectiveLengthRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>
            {
                new() { Member = null, Mode = 1, Ly = 5f, Lz = 6f },
                new() { Member = 1, Mode = null, Ly = 5f, Lz = 6f },
                new() { Member = 1, Mode = 1, Ly = 8.5f, Lz = 10.0f, Pcr = 500f, Length = 10f }
            });

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Single(result.EffectiveLengths);
    }

    // ── Nullable values default to zero ──────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_NullableValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>
            {
                new() { Mode = 1, LoadCase = 1 } // all nullable values null
            });
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>
            {
                new() { Member = 1, Mode = 1 } // all nullable values null
            });

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Single(result.LoadFactors);
        Assert.Equal(0, result.LoadFactors[0].LoadFactor);
        Assert.Null(result.LoadFactors[0].NodeAtMaxTranslation);
        Assert.Equal("", result.LoadFactors[0].TranslationAxis);
        Assert.Null(result.LoadFactors[0].NodeAtMaxRotation);
        Assert.Equal("", result.LoadFactors[0].RotationAxis);

        Assert.Single(result.EffectiveLengths);
        Assert.Equal(0, result.EffectiveLengths[0].Ley);
        Assert.Equal(0, result.EffectiveLengths[0].Lez);
        Assert.Equal(0, result.EffectiveLengths[0].Pcr);
        Assert.Equal(0, result.EffectiveLengths[0].Length);
    }

    // ── Empty results emit warning ──────────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_EmptyResults_WarnsNoBucklingResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        var result = await session.GetBucklingResultsAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No buckling results found"));
    }

    // ── Connection guard ────────────────────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetBucklingResultsAsync(new SgModelData()));
    }

    // ── API exception wrapping — load factors ───────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_LoadFactorsApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetBucklingResultsAsync(new SgModelData()));
        Assert.Contains("querying buckling load factors", ex.Message);
    }

    // ── API exception wrapping — effective lengths ──────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_EffectiveLengthsApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetBucklingResultsAsync(new SgModelData()));
        Assert.Contains("querying buckling effective lengths", ex.Message);
    }

    // ── No filters passes nulls ─────────────────────────────────────

    [Fact]
    public async Task GetBucklingResultsAsync_WithNoFilters_PassesNullsToApi()
    {
        var session = CreateConnectedSession();
        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>());
        _api.GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>());

        await session.GetBucklingResultsAsync(CreateTestModel());

        await _api.Received(1).GetBucklingEffectiveLengthsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    // ── Load case filter passed server-side to effective lengths ─────

    [Fact]
    public async Task GetBucklingResultsAsync_WithLoadCaseFilter_PassesLoadCasesServerSide()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;

        _api.GetBucklingLoadFactorsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BucklingLoadFactor>
            {
                new() { Mode = 1, LoadCase = 1, LoadFactor = 5.0f },
                new() { Mode = 1, LoadCase = 2, LoadFactor = 3.0f }
            });
        _api.GetBucklingEffectiveLengthsAsync(null, null, "1", Arg.Any<CancellationToken>())
            .Returns(new List<BucklingEffectiveLength>
            {
                new() { Member = 1, Mode = 1, LoadCase = 1, Ly = 8.5f, Lz = 10.0f, Pcr = 500f, Length = 10f }
            });

        var result = await session.GetBucklingResultsAsync(model,
            loadCaseFilter: new[] { "Dead Load" });

        // Load factors filtered client-side (no server-side support)
        Assert.Single(result.LoadFactors);
        Assert.Equal(5.0, result.LoadFactors[0].LoadFactor, 3);

        // Effective lengths filtered server-side
        Assert.Single(result.EffectiveLengths);
        await _api.Received(1).GetBucklingEffectiveLengthsAsync(null, null, "1", Arg.Any<CancellationToken>());
    }
}