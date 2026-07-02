using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetLoadCaseDataTests
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

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetLoadCaseDataAsync());
    }

    // ── Empty results ────────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_Empty_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>());
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Contains(result.Warnings, w => w.Contains("No load cases"));
        Assert.Empty(result.LoadCases);
        Assert.Empty(result.Categories);
        Assert.Empty(result.Groups);
    }

    // ── Load cases ───────────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_ReturnsPrimaryLoadCases()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = 1, Title = "Dead Load", Type = LoadCaseType.Primary, Notes = "Permanent" },
            new() { Id = 2, Title = "Live Load", Type = LoadCaseType.Primary, Notes = null }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Equal(2, result.LoadCases.Count);
        Assert.Equal(1, result.LoadCases[0].Id);
        Assert.Equal("Dead Load", result.LoadCases[0].Name);
        Assert.Equal("Primary", result.LoadCases[0].Type);
        Assert.Equal("Permanent", result.LoadCases[0].Notes);

        Assert.Equal(2, result.LoadCases[1].Id);
        Assert.Equal("Live Load", result.LoadCases[1].Name);
        Assert.Equal("", result.LoadCases[1].Notes);
    }

    [Fact]
    public async Task GetLoadCaseData_ReturnsCombinationLoadCases_WithItems()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = 1, Title = "Dead Load", Type = LoadCaseType.Primary },
            new() { Id = 2, Title = "Live Load", Type = LoadCaseType.Primary },
            new()
            {
                Id = 3, Title = "ULS", Type = LoadCaseType.Combination,
                HasCombinationItems = true,
                CombinationItems = new List<CombinationLoadCaseItem>
                {
                    new() { LoadCase = 1, MultiplyingFactor = 1.2 },
                    new() { LoadCase = 2, MultiplyingFactor = 1.5 }
                }
            }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Equal(3, result.LoadCases.Count);
        Assert.Equal("Combination", result.LoadCases[2].Type);

        // Combination items resolved to readable strings
        Assert.Equal(2, result.LoadCases[2].CombinationItems.Count);
        Assert.Equal("1.2×Dead Load", result.LoadCases[2].CombinationItems[0]);
        Assert.Equal("1.5×Live Load", result.LoadCases[2].CombinationItems[1]);
    }

    [Fact]
    public async Task GetLoadCaseData_CombinationItem_UnresolvedId_ShowsIdNumber()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new()
            {
                Id = 5, Title = "Combo", Type = LoadCaseType.Combination,
                HasCombinationItems = true,
                CombinationItems = new List<CombinationLoadCaseItem>
                {
                    new() { LoadCase = 99, MultiplyingFactor = 1.0 }
                }
            }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Single(result.LoadCases[0].CombinationItems);
        Assert.Equal("1×LC99", result.LoadCases[0].CombinationItems[0]);
    }

    [Fact]
    public async Task GetLoadCaseData_MapsAllLoadCaseTypes()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = 1, Title = "Dead", Type = LoadCaseType.Primary },
            new() { Id = 2, Title = "Combo", Type = LoadCaseType.Combination },
            new() { Id = 3, Title = "NL Step", Type = LoadCaseType.Step },
            new() { Id = 4, Title = "Old", Type = LoadCaseType.Unused }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Equal("Primary", result.LoadCases[0].Type);
        Assert.Equal("Combination", result.LoadCases[1].Type);
        Assert.Equal("Step", result.LoadCases[2].Type);
        Assert.Equal("Unused", result.LoadCases[3].Type);
    }

    // ── Load categories ──────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_ReturnsCategories()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = 1, Title = "Dead", Type = LoadCaseType.Primary }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>
        {
            new() { Id = 1, Title = "Dead", Notes = "Permanent loads" },
            new() { Id = 2, Title = "Live", Notes = null }
        });
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        var result = await session.GetLoadCaseDataAsync();

        Assert.Equal(2, result.Categories.Count);
        Assert.Equal(1, result.Categories[0].Id);
        Assert.Equal("Dead", result.Categories[0].Name);
        Assert.Equal("Permanent loads", result.Categories[0].Notes);
        Assert.Equal(2, result.Categories[1].Id);
        Assert.Equal("Live", result.Categories[1].Name);
        Assert.Equal("", result.Categories[1].Notes);
    }

    // ── Load case groups ─────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_ReturnsGroups()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = 1, Title = "Dead", Type = LoadCaseType.Primary }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>
        {
            new() { Id = 1, Title = "Gravity", LoadCaseList = "1,2,3" },
            new() { Id = 2, Title = "Wind", LoadCaseList = "4,5" }
        });

        var result = await session.GetLoadCaseDataAsync();

        Assert.Equal(2, result.Groups.Count);
        Assert.Equal(1, result.Groups[0].Id);
        Assert.Equal("Gravity", result.Groups[0].Name);
        Assert.Equal("1,2,3", result.Groups[0].LoadCaseList);
        Assert.Equal(2, result.Groups[1].Id);
        Assert.Equal("Wind", result.Groups[1].Name);
    }

    // ── Skips null IDs ───────────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_SkipsNullIds()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>
        {
            new() { Id = null, Title = "Bad" },
            new() { Id = 1, Title = "Good", Type = LoadCaseType.Primary }
        });
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>
        {
            new() { Id = null, Title = "Bad Cat" },
            new() { Id = 1, Title = "Good Cat" }
        });
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>
        {
            new() { Id = null, Title = "Bad Group" },
            new() { Id = 1, Title = "Good Group", LoadCaseList = "1" }
        });

        var result = await session.GetLoadCaseDataAsync();

        Assert.Single(result.LoadCases);
        Assert.Single(result.Categories);
        Assert.Single(result.Groups);
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_ApiError_WrappedInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>())
            .Returns<List<LoadCase>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetLoadCaseDataAsync());

        Assert.Contains("querying load cases", ex.Message);
    }

    // ── Queries all endpoints ────────────────────────────────────────

    [Fact]
    public async Task GetLoadCaseData_QueriesAllEndpoints()
    {
        var session = CreateConnectedSession();
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>());
        _api.ListLoadCategoriesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCategory>());
        _api.ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCaseGroup>());

        await session.GetLoadCaseDataAsync();

        await _api.Received(1).ListLoadCasesAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListLoadCategoriesAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListLoadCaseGroupsAsync(Arg.Any<CancellationToken>());
    }
}
