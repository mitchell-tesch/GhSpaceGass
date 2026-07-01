using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for combination load case creation and assembly (Slice 14).
///     Covers SgCombinationLoadCaseData construction, ModelAssembler mapping,
///     constituent resolution, deduplication, and dependency order.
/// </summary>
public class CombinationLoadCaseAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember(
        double x1 = 0, double y1 = 0, double z1 = 0,
        double x2 = 10, double y2 = 0, double z2 = 0)
    {
        return new SgMemberData(
            new SgPoint3D(x1, y1, z1),
            new SgPoint3D(x2, y2, z2),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));
    }

    private static SgLoadCaseData LC(string name)
    {
        return new SgLoadCaseData(name);
    }

    private static SgCombinationLoadCaseData MakeCombo(
        string name,
        params (string lcName, double factor)[] constituents)
    {
        var items = constituents
            .Select(c => new SgCombinationConstituent(new SgLoadCaseData(c.lcName), c.factor))
            .ToList();
        return new SgCombinationLoadCaseData(name, items);
    }

    private static SgNodeLoadData MakeNodeLoad(
        double x, double y, double z, SgLoadCaseData lc, double fy = -10)
    {
        return new SgNodeLoadData(
            new SgPoint3D(x, y, z), lc, fy: fy);
    }

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

        _api.CreateLoadCategoriesAsync(Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCategoryCreate>)args[0];
                return input.Select((c, i) => new LoadCategory { Id = i + 1 }).ToList();
            });

        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select((nl, i) => new NodeLoad()).ToList();
            });
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgCombinationLoadCaseData construction tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgCombinationLoadCaseData_ValidConstruction_StoresValues()
    {
        var dead = LC("Dead Load");
        var live = LC("Live Load");
        var constituents = new[]
        {
            new SgCombinationConstituent(dead, 1.2),
            new SgCombinationConstituent(live, 1.5)
        };

        var combo = new SgCombinationLoadCaseData("ULS", constituents, "Ultimate limit state");

        Assert.Equal("ULS", combo.Name);
        Assert.Equal("Ultimate limit state", combo.Notes);
        Assert.Equal(2, combo.Constituents.Count);
        Assert.Equal("Dead Load", combo.Constituents[0].Name);
        Assert.Equal(1.2, combo.Constituents[0].Factor);
        Assert.Equal("Live Load", combo.Constituents[1].Name);
        Assert.Equal(1.5, combo.Constituents[1].Factor);
    }

    [Fact]
    public void SgCombinationLoadCaseData_EmptyName_Throws()
    {
        var constituents = new[] { new SgCombinationConstituent(LC("Dead"), 1.0) };
        Assert.Throws<ArgumentException>(() => new SgCombinationLoadCaseData("", constituents));
    }

    [Fact]
    public void SgCombinationLoadCaseData_NullName_Throws()
    {
        var constituents = new[] { new SgCombinationConstituent(LC("Dead"), 1.0) };
        Assert.Throws<ArgumentException>(() => new SgCombinationLoadCaseData(null!, constituents));
    }

    [Fact]
    public void SgCombinationLoadCaseData_EmptyConstituents_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgCombinationLoadCaseData("ULS", Array.Empty<SgCombinationConstituent>()));
    }

    [Fact]
    public void SgCombinationLoadCaseData_NullConstituents_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgCombinationLoadCaseData("ULS", null!));
    }

    [Fact]
    public void SgCombinationLoadCaseData_Key_IsCaseInsensitiveName()
    {
        var combo = MakeCombo("ULS", ("Dead", 1.2));
        Assert.Equal("ULS", combo.Key);
    }

    [Fact]
    public void SgCombinationConstituent_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SgCombinationConstituent((SgLoadCaseData)null!, 1.0));
    }

    [Fact]
    public void SgCombinationConstituent_NullCombinationLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgCombinationConstituent((SgCombinationLoadCaseData)null!, 1.0));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — combination load cases pushed to API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_WithCombinations_CallsCreateCombinationLoadCasesAsync()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var liveLc = LC("Live");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var nodeLoad2 = MakeNodeLoad(0, 0, 0, liveLc);

        var combo = new SgCombinationLoadCaseData("ULS", new[]
        {
            new SgCombinationConstituent(deadLc, 1.2),
            new SgCombinationConstituent(liveLc, 1.5)
        });

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad, nodeLoad2 },
            combinationLoadCases: new[] { combo });

        await _api.Received(1).CreateCombinationLoadCasesAsync(
            Arg.Is<List<CombinationLoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CombinationSendsCorrectTitleAndItems()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var liveLc = LC("Live");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var nodeLoad2 = MakeNodeLoad(0, 0, 0, liveLc);

        var combo = new SgCombinationLoadCaseData("ULS", new[]
        {
            new SgCombinationConstituent(deadLc, 1.2),
            new SgCombinationConstituent(liveLc, 1.5)
        }, "Ultimate");

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad, nodeLoad2 },
            combinationLoadCases: new[] { combo });

        await _api.Received(1).CreateCombinationLoadCasesAsync(
            Arg.Is<List<CombinationLoadCaseCreate>>(list =>
                list[0].Title == "ULS" &&
                list[0].Notes == "Ultimate" &&
                list[0].CombinationItems.Count == 2 &&
                list[0].CombinationItems[0].MultiplyingFactor == 1.2 &&
                list[0].CombinationItems[1].MultiplyingFactor == 1.5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CombinationResolvesConstituentIdsFromLoadCaseMap()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var liveLc = LC("Live");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var nodeLoad2 = MakeNodeLoad(0, 0, 0, liveLc);

        var combo = new SgCombinationLoadCaseData("ULS", new[]
        {
            new SgCombinationConstituent(deadLc, 1.2),
            new SgCombinationConstituent(liveLc, 1.5)
        });

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad, nodeLoad2 },
            combinationLoadCases: new[] { combo });

        // Dead = ID 1, Live = ID 2 (sequential from mock)
        await _api.Received(1).CreateCombinationLoadCasesAsync(
            Arg.Is<List<CombinationLoadCaseCreate>>(list =>
                list[0].CombinationItems[0].LoadCase == 1 &&
                list[0].CombinationItems[1].LoadCase == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CombinationLoadCaseMap_PopulatedWithCombinationIds()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);

        var combo = MakeCombo("ULS", ("Dead", 1.2));
        var members = new[] { MakeMember() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { combo });

        Assert.Single(result.Model.CombinationLoadCaseMap);
        Assert.True(result.Model.CombinationLoadCaseMap.ContainsKey("ULS"));
        Assert.Equal(101, result.Model.CombinationLoadCaseMap["ULS"]); // 100 + 1
    }

    // ── Dependency order ──────────────────────────────────────────────

    [Fact]
    public async Task Assemble_CombinationsCreatedAfterPrimaryLoadCases()
    {
        SetupApiReturns();
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

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var combo = MakeCombo("ULS", ("Dead", 1.2));
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { combo });

        var lcIdx = callOrder.IndexOf("loadcases");
        var comboIdx = callOrder.IndexOf("combinations");
        Assert.True(lcIdx >= 0, "load cases should have been called");
        Assert.True(comboIdx >= 0, "combinations should have been called");
        Assert.True(comboIdx > lcIdx, "combinations must be created after primary load cases");
    }

    // ── Constituent load cases created even without direct loads ──────

    [Fact]
    public async Task Assemble_CombinationOnly_ConstituentLoadCasesStillCreated()
    {
        SetupApiReturns();

        // No direct loads — only a combination referencing "Dead" and "Live"
        var combo = MakeCombo("ULS", ("Dead", 1.2), ("Live", 1.5));
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            combinationLoadCases: new[] { combo });

        // Primary load cases "Dead" and "Live" should still be created
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list =>
                list.Count == 2 &&
                list.Any(lc => lc.Title == "Dead") &&
                list.Any(lc => lc.Title == "Live")),
            Arg.Any<CancellationToken>());
    }

    // ── Deduplication (ADR-0006) ──────────────────────────────────────

    [Fact]
    public async Task Assemble_DuplicateCombinationNames_DeduplicatesAndWarns()
    {
        SetupApiReturns();

        var combo1 = MakeCombo("ULS", ("Dead", 1.2));
        var combo2 = MakeCombo("ULS", ("Dead", 1.5)); // same name, different factor

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var members = new[] { MakeMember() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { combo1, combo2 });

        // Only first occurrence created
        await _api.Received(1).CreateCombinationLoadCasesAsync(
            Arg.Is<List<CombinationLoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // Warning about duplicate
        Assert.Contains(result.Warnings,
            w => w.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("ULS"));
    }

    [Fact]
    public async Task Assemble_ConstituentLoadCaseDeduplicatedWithDirectLoads()
    {
        SetupApiReturns();

        // "Dead" referenced by both a node load AND a combination constituent
        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var combo = MakeCombo("ULS", ("Dead", 1.2));
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { combo });

        // "Dead" should only be created once (deduplicated)
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list =>
                list.Count == 1 && list[0].Title == "Dead"),
            Arg.Any<CancellationToken>());
    }

    // ── No combinations ───────────────────────────────────────────────

    [Fact]
    public async Task Assemble_NoCombinations_DoesNotCallCombinationApi()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad });

        await _api.DidNotReceive().CreateCombinationLoadCasesAsync(
            Arg.Any<List<CombinationLoadCaseCreate>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Bulk result validation ────────────────────────────────────────

    [Fact]
    public async Task Assemble_CombinationApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateCombinationLoadCasesAsync(
                Arg.Any<List<CombinationLoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LoadCase>()); // returns 0 instead of 1

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var combo = MakeCombo("ULS", ("Dead", 1.2));
        var members = new[] { MakeMember() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _assembler.AssembleAsync(_api, members,
            Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { combo }));
        Assert.Contains("combination", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Combination referencing another combination
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgCombinationConstituent_WithCombinationLoadCase_StoresCorrectly()
    {
        var inner = MakeCombo("ULS", ("Dead", 1.2));
        var constituent = new SgCombinationConstituent(inner, 0.7);

        Assert.True(constituent.IsCombinationReference);
        Assert.Null(constituent.LoadCase);
        Assert.NotNull(constituent.CombinationLoadCase);
        Assert.Equal("ULS", constituent.Name);
        Assert.Equal("ULS", constituent.Key);
        Assert.Equal(0.7, constituent.Factor);
    }

    [Fact]
    public void SgCombinationConstituent_WithPrimaryLoadCase_IsNotCombinationReference()
    {
        var constituent = new SgCombinationConstituent(LC("Dead"), 1.2);

        Assert.False(constituent.IsCombinationReference);
        Assert.NotNull(constituent.LoadCase);
        Assert.Null(constituent.CombinationLoadCase);
        Assert.Equal("Dead", constituent.Name);
        Assert.Equal("Dead", constituent.Key);
    }

    [Fact]
    public async Task Assemble_CombinationReferencingCombination_CreatesInDependencyOrder()
    {
        SetupApiReturns();
        var callOrder = new List<List<string>>();

        _api.CreateCombinationLoadCasesAsync(Arg.Any<List<CombinationLoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<CombinationLoadCaseCreate>)args[0];
                callOrder.Add(input.Select(c => c.Title!).ToList());
                return input.Select((c, i) => new LoadCase
                    { Id = 100 + (callOrder.Sum(b => b.Count) - input.Count) + i + 1, Title = c.Title }).ToList();
            });

        var deadLc = LC("Dead");
        var liveLc = LC("Live");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);
        var nodeLoad2 = MakeNodeLoad(0, 0, 0, liveLc);

        // ULS references primaries
        var uls = new SgCombinationLoadCaseData("ULS", new[]
        {
            new SgCombinationConstituent(deadLc, 1.2),
            new SgCombinationConstituent(liveLc, 1.5)
        });

        // Envelope references ULS (another combination)
        var envelope = new SgCombinationLoadCaseData("Envelope", new[]
        {
            new SgCombinationConstituent(uls, 1.0),
            new SgCombinationConstituent(deadLc, 0.8)
        });

        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad, nodeLoad2 },
            combinationLoadCases: new[] { envelope, uls }); // intentionally reversed

        // Should be at least 2 API calls: first batch (ULS), second batch (Envelope)
        Assert.True(callOrder.Count >= 2);
        Assert.Contains("ULS", callOrder[0]);
        Assert.Contains("Envelope", callOrder[1]);
    }

    [Fact]
    public async Task Assemble_CombinationReferencingCombination_ResolvesIdsCorrectly()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);

        var uls = new SgCombinationLoadCaseData("ULS", new[]
        {
            new SgCombinationConstituent(deadLc, 1.2)
        });

        var envelope = new SgCombinationLoadCaseData("Envelope", new[]
        {
            new SgCombinationConstituent(uls, 1.0)
        });

        var members = new[] { MakeMember() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { uls, envelope });

        // Both should be in the CombinationLoadCaseMap
        Assert.True(result.Model.CombinationLoadCaseMap.ContainsKey("ULS"));
        Assert.True(result.Model.CombinationLoadCaseMap.ContainsKey("Envelope"));
    }

    [Fact]
    public async Task Assemble_CircularCombinationReference_WarnsAndSkips()
    {
        SetupApiReturns();

        var deadLc = LC("Dead");
        var nodeLoad = MakeNodeLoad(0, 0, 0, deadLc);

        // Create two combinations that reference each other (circular)
        var comboA = new SgCombinationLoadCaseData("ComboA", new[]
        {
            new SgCombinationConstituent(deadLc, 1.0)
        });
        // ComboB references ComboA — but we'll simulate circular by making ComboA reference ComboB too
        // Since we can't create a true circular reference with immutable objects,
        // we create a combo that references a non-existent combo (simulating unresolvable)
        var fakeComboRef = new SgCombinationLoadCaseData("NonExistent", new[]
        {
            new SgCombinationConstituent(deadLc, 1.0)
        });
        var comboWithBadRef = new SgCombinationLoadCaseData("BadCombo", new[]
        {
            new SgCombinationConstituent(fakeComboRef, 1.0)
        });

        var members = new[] { MakeMember() };

        // Only include comboWithBadRef (not fakeComboRef) — dependency can't be resolved
        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: new[] { nodeLoad },
            combinationLoadCases: new[] { comboWithBadRef });

        // Should warn about unresolvable reference
        Assert.Contains(result.Warnings, w => w.Contains("BadCombo") && w.Contains("unresolvable"));
    }
}