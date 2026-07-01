using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for custom (user-defined) section and material assembly (Slice 15).
///     Covers SgSectionData/SgMaterialData custom mode, assembler partitioning,
///     API endpoint routing, and deduplication across library/custom boundaries.
/// </summary>
public class CustomSectionMaterialAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
            });

        _api.CreateMaterialsFromUserAsync(Arg.Any<List<MaterialUserCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialUserCreate>)args[0];
                return input.Select((m, i) => new Material { Id = 100 + i + 1 }).ToList();
            });

        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((s, i) => new Section { Id = i + 1 }).ToList();
            });

        _api.CreateSectionsFromUserAsync(Arg.Any<List<SectionUserCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionUserCreate>)args[0];
                return input.Select((s, i) => new Section { Id = 200 + i + 1 }).ToList();
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
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgSectionData — custom mode construction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgSectionData_LibraryMode_IsLibraryTrue()
    {
        var s = new SgSectionData("Aust300", "360 UB 44.7");
        Assert.True(s.IsLibrary);
        Assert.Equal("Aust300", s.Library);
    }

    [Fact]
    public void SgSectionData_CustomMode_IsLibraryFalse()
    {
        var s = new SgSectionData("MySection", 1000, 50000, 20000, 500);
        Assert.False(s.IsLibrary);
        Assert.Null(s.Library);
        Assert.Equal("MySection", s.Name);
        Assert.Equal(1000, s.Area);
        Assert.Equal(50000, s.Iy);
        Assert.Equal(20000, s.Iz);
        Assert.Equal(500, s.J);
    }

    [Fact]
    public void SgSectionData_CustomMode_KeyDiffersFromLibrary()
    {
        var lib = new SgSectionData("Aust300", "MySection");
        var custom = new SgSectionData("MySection", 1000);
        Assert.NotEqual(lib.Key, custom.Key);
        Assert.StartsWith("CUSTOM::", custom.Key);
    }

    [Fact]
    public void SgSectionData_CustomMode_DifferentPropertiesHaveDifferentKeys()
    {
        var s1 = new SgSectionData("Sec", 1000, 50000);
        var s2 = new SgSectionData("Sec", 2000, 50000);
        Assert.NotEqual(s1.Key, s2.Key);
    }

    [Fact]
    public void SgSectionData_CustomMode_SamePropertiesHaveSameKey()
    {
        var s1 = new SgSectionData("Sec", 1000, 50000);
        var s2 = new SgSectionData("Sec", 1000, 50000);
        Assert.Equal(s1.Key, s2.Key);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgMaterialData — custom mode construction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMaterialData_LibraryMode_IsLibraryTrue()
    {
        var m = new SgMaterialData("Aust", "STEEL");
        Assert.True(m.IsLibrary);
    }

    [Fact]
    public void SgMaterialData_CustomMode_IsLibraryFalse()
    {
        var m = new SgMaterialData("Custom Steel", 200000, 0.3, 7850);
        Assert.False(m.IsLibrary);
        Assert.Null(m.Library);
        Assert.Equal("Custom Steel", m.Name);
        Assert.Equal(200000, m.YoungsModulus);
        Assert.Equal(0.3, m.PoissonsRatio);
        Assert.Equal(7850, m.MassDensity);
    }

    [Fact]
    public void SgMaterialData_CustomMode_KeyDiffersFromLibrary()
    {
        var lib = new SgMaterialData("Aust", "STEEL");
        var custom = new SgMaterialData("STEEL", 200000);
        Assert.NotEqual(lib.Key, custom.Key);
        Assert.StartsWith("CUSTOM::", custom.Key);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — custom sections routed to user API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_CustomSection_CallsCreateSectionsFromUserAsync()
    {
        SetupApiReturns();

        var section = new SgSectionData("Custom", 500, 10000, 5000, 200);
        var material = new SgMaterialData("Aust", "STEEL");
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        await _api.Received(1).CreateSectionsFromUserAsync(
            Arg.Is<List<SectionUserCreate>>(list =>
                list.Count == 1 &&
                list[0].Name == "Custom" &&
                list[0].A == 500 &&
                list[0].Iy == 10000 &&
                list[0].Iz == 5000 &&
                list[0].J == 200),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateSectionsFromLibraryAsync(
            Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CustomMaterial_CallsCreateMaterialsFromUserAsync()
    {
        SetupApiReturns();

        var section = new SgSectionData("Aust300", "360 UB 44.7");
        var material = new SgMaterialData("MySteel", 200000, 0.3);
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        await _api.Received(1).CreateMaterialsFromUserAsync(
            Arg.Is<List<MaterialUserCreate>>(list =>
                list.Count == 1 &&
                list[0].Name == "MySteel" &&
                list[0].YoungsModulus == 200000 &&
                list[0].PoissonsRatio == 0.3),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateMaterialsFromLibraryAsync(
            Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MixedLibraryAndCustomSections_BothApisCalled()
    {
        SetupApiReturns();

        var libSection = new SgSectionData("Aust300", "360 UB 44.7");
        var customSection = new SgSectionData("Custom", 500);
        var material = new SgMaterialData("Aust", "STEEL");

        var members = new[]
        {
            new SgMemberData(new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), libSection, material),
            new SgMemberData(new SgPoint3D(5, 0, 0), new SgPoint3D(10, 0, 0), customSection, material)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateSectionsFromLibraryAsync(
            Arg.Is<List<SectionLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateSectionsFromUserAsync(
            Arg.Is<List<SectionUserCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MixedLibraryAndCustomMaterials_BothApisCalled()
    {
        SetupApiReturns();

        var section = new SgSectionData("Aust300", "360 UB 44.7");
        var libMaterial = new SgMaterialData("Aust", "STEEL");
        var customMaterial = new SgMaterialData("Aluminium", 70000);

        var members = new[]
        {
            new SgMemberData(new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), section, libMaterial),
            new SgMemberData(new SgPoint3D(5, 0, 0), new SgPoint3D(10, 0, 0), section, customMaterial)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Is<List<MaterialLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMaterialsFromUserAsync(
            Arg.Is<List<MaterialUserCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CustomSectionWithModFactors_MappedToApi()
    {
        SetupApiReturns();

        var section = new SgSectionData("Sec", 1000, 50000,
            areaFactor: 0.9, iyFactor: 1.1, izFactor: 0.8, torsionFactor: 1.0);
        var material = new SgMaterialData("Aust", "STEEL");
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        await _api.Received(1).CreateSectionsFromUserAsync(
            Arg.Is<List<SectionUserCreate>>(list =>
                list[0].AreaFactor == 0.9 &&
                list[0].IyFactor == 1.1 &&
                list[0].IzFactor == 0.8 &&
                list[0].TorsionFactor == 1.0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_CustomSectionPopulatesSectionMap()
    {
        SetupApiReturns();

        var section = new SgSectionData("Custom", 500);
        var material = new SgMaterialData("Aust", "STEEL");
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        var result = await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        // Custom section gets ID 201 from mock (200 + 1)
        Assert.Equal(201, result.Model.SectionMap[section.Key]);
    }

    [Fact]
    public async Task Assemble_CustomMaterialPopulatesMaterialMap()
    {
        SetupApiReturns();

        var section = new SgSectionData("Aust300", "360 UB 44.7");
        var material = new SgMaterialData("MySteel", 200000);
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        var result = await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        // Custom material gets ID 101 from mock (100 + 1)
        Assert.Equal(101, result.Model.MaterialMap[material.Key]);
    }

    // ── Deduplication across library and custom ────────────────────────

    [Fact]
    public async Task Assemble_DuplicateCustomSections_Deduplicated()
    {
        SetupApiReturns();

        var section = new SgSectionData("Custom", 500);
        var material = new SgMaterialData("Aust", "STEEL");

        var members = new[]
        {
            new SgMemberData(new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), section, material),
            new SgMemberData(new SgPoint3D(5, 0, 0), new SgPoint3D(10, 0, 0), section, material)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Only 1 custom section created (deduplicated)
        await _api.Received(1).CreateSectionsFromUserAsync(
            Arg.Is<List<SectionUserCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_LibraryOnlyMembers_NoCustomApiCalled()
    {
        SetupApiReturns();

        var section = new SgSectionData("Aust300", "360 UB 44.7");
        var material = new SgMaterialData("Aust", "STEEL");
        var member = new SgMemberData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), section, material);

        await _assembler.AssembleAsync(_api, new[] { member }, Tolerance);

        await _api.DidNotReceive().CreateSectionsFromUserAsync(
            Arg.Any<List<SectionUserCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateMaterialsFromUserAsync(
            Arg.Any<List<MaterialUserCreate>>(), Arg.Any<CancellationToken>());
    }
}