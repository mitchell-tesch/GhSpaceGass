using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for moving load scenario creation and assembly (Slice 40).
///     Covers <see cref="SgMovingLoadScenarioData"/> construction, combination-entry
///     construction, <see cref="ModelAssembler"/> mapping to
///     <c>MovingLoadScenarioCreate</c>, deduplication, LC pool folding, dependency order,
///     and API error handling.
/// </summary>
public class MovingLoadScenarioTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember()
    {
        return new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));
    }

    private static SgLoadCaseData LC(string name) => new(name);

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
            });

        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((s, i) => new Section { Id = i + 1 }).ToList();
            });

        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeCreate>)args[0];
                return input.Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList();
            });

        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberCreate>)args[0];
                return input.Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList();
            });

        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });

        _api.CreateCombinationLoadCasesAsync(Arg.Any<List<CombinationLoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<CombinationLoadCaseCreate>)args[0];
                return input.Select((c, i) => new LoadCase { Id = 100 + i + 1, Title = c.Title }).ToList();
            });

        _api.CreateMovingLoadScenariosAsync(Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name })
                    .ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadCombinationEntry construction ─────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MovingLoadCombinationEntry_PrimaryLc_StoresProperties()
    {
        var dead = LC("Dead");
        var entry = new SgMovingLoadCombinationEntry(dead, loadCaseFactor: 1.2, scenarioFactor: 1.35);

        Assert.Same(dead, entry.LoadCase);
        Assert.Null(entry.CombinationLoadCase);
        Assert.False(entry.IsCombinationReference);
        Assert.Equal(1.2, entry.LoadCaseFactor);
        Assert.Equal(1.35, entry.ScenarioFactor);
        Assert.Equal("Dead", entry.Name);
        Assert.Equal("Dead", entry.Key);
    }

    [Fact]
    public void MovingLoadCombinationEntry_CombinationLc_StoresProperties()
    {
        var uls = new SgCombinationLoadCaseData("ULS",
            new[] { new SgCombinationConstituent(LC("Dead"), 1.2) });
        var entry = new SgMovingLoadCombinationEntry(uls, loadCaseFactor: 1.0, scenarioFactor: 1.0);

        Assert.Null(entry.LoadCase);
        Assert.Same(uls, entry.CombinationLoadCase);
        Assert.True(entry.IsCombinationReference);
        Assert.Equal(1.0, entry.LoadCaseFactor);
        Assert.Equal(1.0, entry.ScenarioFactor);
        Assert.Equal("ULS", entry.Name);
        Assert.Equal("ULS", entry.Key);
    }

    [Fact]
    public void MovingLoadCombinationEntry_NullPrimaryLc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMovingLoadCombinationEntry((SgLoadCaseData)null!, 1.0, 1.0));
    }

    [Fact]
    public void MovingLoadCombinationEntry_NullCombinationLc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMovingLoadCombinationEntry((SgCombinationLoadCaseData)null!, 1.0, 1.0));
    }

    [Fact]
    public void MovingLoadCombinationEntry_DefaultStartingCombinationCase_IsNull()
    {
        var entry = new SgMovingLoadCombinationEntry(LC("Dead"), 1.2, 1.35);
        Assert.Null(entry.StartingCombinationCase);
    }

    [Fact]
    public void MovingLoadCombinationEntry_StartingCombinationCase_StoresValue_PrimaryLc()
    {
        var entry = new SgMovingLoadCombinationEntry(LC("Dead"), 1.2, 1.35, startingCombinationCase: 210);
        Assert.Equal(210, entry.StartingCombinationCase);
    }

    [Fact]
    public void MovingLoadCombinationEntry_StartingCombinationCase_StoresValue_CombinationLc()
    {
        var uls = new SgCombinationLoadCaseData("ULS",
            new[] { new SgCombinationConstituent(LC("Dead"), 1.2) });
        var entry = new SgMovingLoadCombinationEntry(uls, 1.0, 1.0, startingCombinationCase: 305);
        Assert.Equal(305, entry.StartingCombinationCase);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadScenarioData construction ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MovingLoadScenario_MinimalConstruction_StoresName()
    {
        var scenario = new SgMovingLoadScenarioData("Truck Left Lane");

        Assert.Equal("Truck Left Lane", scenario.Name);
        Assert.Null(scenario.StartingLoadCase);
        Assert.Null(scenario.TimeInterval);
        Assert.True(scenario.Include);
        Assert.Empty(scenario.Combinations);
        Assert.Equal("Truck Left Lane", scenario.Key);
    }

    [Fact]
    public void MovingLoadScenario_FullConstruction_StoresAllProperties()
    {
        var dead = LC("Dead");
        var live = LC("Live");
        var combos = new[]
        {
            new SgMovingLoadCombinationEntry(dead, 1.2, 1.0),
            new SgMovingLoadCombinationEntry(live, 1.5, 1.0)
        };

        var scenario = new SgMovingLoadScenarioData(
            name: "Truck Left Lane",
            startingLoadCase: dead,
            timeInterval: 0.5,
            include: false,
            combinations: combos);

        Assert.Equal("Truck Left Lane", scenario.Name);
        Assert.Same(dead, scenario.StartingLoadCase);
        Assert.Equal(0.5, scenario.TimeInterval);
        Assert.False(scenario.Include);
        Assert.Equal(2, scenario.Combinations.Count);
        Assert.Same(dead, scenario.Combinations[0].LoadCase);
        Assert.Equal(1.5, scenario.Combinations[1].LoadCaseFactor);
    }

    [Fact]
    public void MovingLoadScenario_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgMovingLoadScenarioData(""));
    }

    [Fact]
    public void MovingLoadScenario_NullName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgMovingLoadScenarioData(null!));
    }

    [Fact]
    public void MovingLoadScenario_WhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgMovingLoadScenarioData("   "));
    }

    [Fact]
    public void MovingLoadScenario_Key_IsName()
    {
        var scenario = new SgMovingLoadScenarioData("Scen A");
        Assert.Equal("Scen A", scenario.Key);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — no scenarios ─────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_NoScenarios_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateMovingLoadScenariosAsync(
            Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_EmptyScenariosList_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: Array.Empty<SgMovingLoadScenarioData>());

        await _api.DidNotReceive().CreateMovingLoadScenariosAsync(
            Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — basic scenarios pushed to API ────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SingleScenario_CallsCreateMovingLoadScenariosAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck Left") };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list.Count == 1 && list[0].Name == "Truck Left"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MultipleScenarios_AllSent()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck Left"),
            new SgMovingLoadScenarioData("Truck Right"),
            new SgMovingLoadScenarioData("Train")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_PopulatesMovingLoadScenarioMap()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck Left"),
            new SgMovingLoadScenarioData("Truck Right")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        Assert.Equal(2, result.Model.MovingLoadScenarioMap.Count);
        Assert.Equal(501, result.Model.MovingLoadScenarioMap["Truck Left"]);
        Assert.Equal(502, result.Model.MovingLoadScenarioMap["Truck Right"]);
    }

    [Fact]
    public async Task Assemble_Scenario_DefaultsIncludeToTrue()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list => list[0].Include == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_IncludeFalse_Persists()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck", include: false)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list => list[0].Include == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_TimeInterval_Propagates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck", timeInterval: 0.25)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].TimeInterval == 0.25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_NoTimeInterval_SendsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].TimeInterval == null),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — starting load case folded into LC pool ───────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_Scenario_StartingLc_AddedToLoadCasePool()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var lc = LC("Truck Case");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck", startingLoadCase: lc)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        // The starting LC should have been created as a primary load case
        Assert.True(result.Model.LoadCaseMap.ContainsKey("Truck Case"));
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Any(lc => lc.Title == "Truck Case")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_StartingLc_ResolvedToId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var lc = LC("Truck Case");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck", startingLoadCase: lc)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        var expectedLcId = result.Model.LoadCaseMap["Truck Case"];
        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].StartingLoadCase == expectedLcId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_NoStartingLc_SendsNullStartingLoadCase()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].StartingLoadCase == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_StartingLcReusedByNodeLoad_NotDuplicated()
    {
        SetupApiReturns();

        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select(_ => new NodeLoad()).ToList();
            });

        var members = new[] { MakeMember() };
        var shared = LC("Shared");
        var nodeLoad = new SgNodeLoadData(new SgPoint3D(0, 0, 0), shared, fz: -10);
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck", startingLoadCase: shared)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            movingLoadScenarios: scenarios);

        // Only one LoadCase creation call, with a single "Shared" LC (not two)
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list =>
                list.Count == 1 && list[0].Title == "Shared"),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — combination entries mapped correctly ─────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_Scenario_CombinationEntry_ReferencesPrimaryLc()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var dead = LC("Dead");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                combinations: new[]
                {
                    new SgMovingLoadCombinationEntry(dead, loadCaseFactor: 1.2, scenarioFactor: 1.35)
                })
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        var deadId = result.Model.LoadCaseMap["Dead"];
        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].Combinations != null &&
                list[0].Combinations.Count == 1 &&
                list[0].Combinations[0].CombineWithLoadCase == deadId &&
                list[0].Combinations[0].LoadCaseFactor == 1.2 &&
                list[0].Combinations[0].ScenarioFactor == 1.35),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_CombinationEntry_ReferencesCombinationLc()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var dead = LC("Dead");
        var uls = new SgCombinationLoadCaseData("ULS",
            new[] { new SgCombinationConstituent(dead, 1.2) });

        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                combinations: new[]
                {
                    new SgMovingLoadCombinationEntry(uls, loadCaseFactor: 1.0, scenarioFactor: 1.5)
                })
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            combinationLoadCases: new[] { uls },
            movingLoadScenarios: scenarios);

        var ulsId = result.Model.CombinationLoadCaseMap["ULS"];
        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].Combinations[0].CombineWithLoadCase == ulsId &&
                list[0].Combinations[0].ScenarioFactor == 1.5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_NoCombinations_SendsEmptyList()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].Combinations != null && list[0].Combinations.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_StartingCombinationCase_Propagates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var dead = LC("Dead");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                combinations: new[]
                {
                    new SgMovingLoadCombinationEntry(dead, 1.2, 1.35, startingCombinationCase: 210)
                })
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].Combinations[0].StartingCombinationCase == 210),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_NoStartingCombinationCase_SendsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var dead = LC("Dead");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                combinations: new[]
                {
                    // No startingCombinationCase argument — should default to null
                    new SgMovingLoadCombinationEntry(dead, 1.2, 1.35)
                })
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list =>
                list[0].Combinations[0].StartingCombinationCase == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Scenario_CombinationLcNotElsewhereUsed_StillCreated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        // Combination-entry LC is not on any other load; the assembler must add it to the LC pool
        var orphanLc = LC("Orphan");
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                combinations: new[]
                {
                    new SgMovingLoadCombinationEntry(orphanLc, 1.0, 1.0)
                })
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        Assert.True(result.Model.LoadCaseMap.ContainsKey("Orphan"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — deduplication + warnings ─────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_DuplicateScenarioNames_Deduplicated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck"),
            new SgMovingLoadScenarioData("Truck"),
            new SgMovingLoadScenarioData("Train")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("moving load scenario", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("Truck", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_DuplicateScenarioNames_CaseInsensitive()
    {
        SetupApiReturns();

        var members = new[] { MakeMember() };
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData("Truck"),
            new SgMovingLoadScenarioData("TRUCK")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            movingLoadScenarios: scenarios);

        await _api.Received(1).CreateMovingLoadScenariosAsync(
            Arg.Is<List<MovingLoadScenarioCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — dependency ordering ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_Scenarios_DependencyOrder_AfterCombinationsBeforeCategories()
    {
        SetupApiReturns();

        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select(_ => new NodeLoad()).ToList();
            });

        var callOrder = new List<string>();
        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("loadcases");
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });
        _api.CreateCombinationLoadCasesAsync(Arg.Any<List<CombinationLoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("combinations");
                var input = (List<CombinationLoadCaseCreate>)args[0];
                return input.Select((c, i) => new LoadCase { Id = 100 + i + 1, Title = c.Title }).ToList();
            });
        _api.CreateMovingLoadScenariosAsync(Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("scenarios");
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name }).ToList();
            });
        _api.CreateLoadCategoriesAsync(Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("categories");
                var input = (List<LoadCategoryCreate>)args[0];
                return input.Select((c, i) => new LoadCategory { Id = i + 1 }).ToList();
            });

        var members = new[] { MakeMember() };
        var dead = LC("Dead");
        var live = LC("Live");
        var uls = new SgCombinationLoadCaseData("ULS",
            new[] { new SgCombinationConstituent(dead, 1.2) });
        var category = new SgLoadCategoryData("Dead Cat");
        var nodeLoad = new SgNodeLoadData(new SgPoint3D(0, 0, 0), live, loadCategory: category, fz: -10);
        var scenarios = new[]
        {
            new SgMovingLoadScenarioData(
                name: "Truck",
                startingLoadCase: dead,
                combinations: new[]
                {
                    new SgMovingLoadCombinationEntry(uls, 1.0, 1.5)
                })
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { uls },
            movingLoadScenarios: scenarios);

        Assert.Equal(new[] { "loadcases", "combinations", "scenarios", "categories" }, callOrder);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — error handling ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ScenarioApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadScenario>>(_ =>
                throw new InvalidOperationException("boom"));

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                movingLoadScenarios: scenarios));

        Assert.Contains("moving load scenario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_ScenarioBulkCountMismatch_Throws()
    {
        SetupApiReturns();
        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovingLoadScenario>()); // return empty

        var members = new[] { MakeMember() };
        var scenarios = new[] { new SgMovingLoadScenarioData("Truck") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                movingLoadScenarios: scenarios));

        Assert.Contains("moving load scenario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
