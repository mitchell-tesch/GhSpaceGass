using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetSectionMaterialPropertiesTests
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

    // ═══════════════════════════════════════════════════════════════
    // GET SECTION PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSectionProperties_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSectionPropertiesAsync());
    }

    [Fact]
    public async Task GetSectionProperties_EmptyResult_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        _api.ListSectionsAsync(Arg.Any<CancellationToken>()).Returns(new List<Section>());

        var result = await session.GetSectionPropertiesAsync();

        Assert.Contains(result.Warnings, w => w.Contains("No sections"));
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task GetSectionProperties_ReturnsSectionData()
    {
        var session = CreateConnectedSession();
        _api.ListSectionsAsync(Arg.Any<CancellationToken>()).Returns(new List<Section>
        {
            new()
            {
                Id = 1, Name = "360 UB 44.7", Library = "Aust300",
                Source = PropertySource.Library,
                A = 5720, Iy = 121e6, Iz = 9.6e6, J = 235000,
                Ay = 0, Az = 0, PrincipalAngle = 0, Mark = "B1",
                AreaFactor = 1.0, IyFactor = 1.0, IzFactor = 1.0, TorsionFactor = 1.0,
                Transposed = false, AngleType = SpaceGassApi.Models.AngleType.NotApplicable
            },
            new()
            {
                Id = 2, Name = "Custom150", Library = null,
                Source = PropertySource.User,
                A = 2250, Iy = 500000, Iz = 125000, J = 50000,
                Ay = 1000, Az = 800, PrincipalAngle = 45, Mark = null,
                AreaFactor = 0.9, IyFactor = null, IzFactor = null, TorsionFactor = null,
                Transposed = true, AngleType = SpaceGassApi.Models.AngleType.SingleType
            }
        });

        var result = await session.GetSectionPropertiesAsync();

        Assert.Equal(2, result.Sections.Count);
        Assert.Empty(result.Warnings);

        // First section (library)
        Assert.Equal(1, result.Sections[0].Id);
        Assert.Equal("360 UB 44.7", result.Sections[0].Name);
        Assert.Equal("Aust300", result.Sections[0].Library);
        Assert.Equal("Library", result.Sections[0].Source);
        Assert.Equal(5720, result.Sections[0].Area);
        Assert.Equal(121e6, result.Sections[0].Iy);
        Assert.Equal(9.6e6, result.Sections[0].Iz);
        Assert.Equal(235000, result.Sections[0].J);
        Assert.Equal(0, result.Sections[0].Ay);
        Assert.Equal(0, result.Sections[0].Az);
        Assert.Equal(0, result.Sections[0].PrincipalAngle);
        Assert.Equal("B1", result.Sections[0].Mark);
        Assert.Equal(1.0, result.Sections[0].AreaFactor);
        Assert.Equal(1.0, result.Sections[0].IyFactor);
        Assert.Equal(1.0, result.Sections[0].IzFactor);
        Assert.Equal(1.0, result.Sections[0].TorsionFactor);
        Assert.False(result.Sections[0].Transposed);
        Assert.Equal("Not Applicable", result.Sections[0].AngleType);

        // Second section (custom)
        Assert.Equal(2, result.Sections[1].Id);
        Assert.Equal("Custom150", result.Sections[1].Name);
        Assert.Equal("", result.Sections[1].Library);
        Assert.Equal("User", result.Sections[1].Source);
        Assert.Equal(2250, result.Sections[1].Area);
        Assert.Equal(45, result.Sections[1].PrincipalAngle);
        Assert.Equal(0.9, result.Sections[1].AreaFactor);
        Assert.True(result.Sections[1].Transposed);
        Assert.Equal("Single", result.Sections[1].AngleType);
    }

    [Fact]
    public async Task GetSectionProperties_NullableFieldsDefaultToZeroOrEmpty()
    {
        var session = CreateConnectedSession();
        _api.ListSectionsAsync(Arg.Any<CancellationToken>()).Returns(new List<Section>
        {
            new() { Id = 1, Name = "MinimalSection", Library = null, Source = null }
        });

        var result = await session.GetSectionPropertiesAsync();

        Assert.Single(result.Sections);
        Assert.Equal(0, result.Sections[0].Area);
        Assert.Equal(0, result.Sections[0].Iy);
        Assert.Equal(0, result.Sections[0].Iz);
        Assert.Equal(0, result.Sections[0].J);
        Assert.Equal(0, result.Sections[0].Ay);
        Assert.Equal(0, result.Sections[0].Az);
        Assert.Equal(0, result.Sections[0].PrincipalAngle);
        Assert.Equal("", result.Sections[0].Library);
        Assert.Equal("", result.Sections[0].Mark);
        Assert.Equal("Unknown", result.Sections[0].Source);
        Assert.Equal(0, result.Sections[0].AreaFactor);
        Assert.Equal(0, result.Sections[0].IyFactor);
        Assert.Equal(0, result.Sections[0].IzFactor);
        Assert.Equal(0, result.Sections[0].TorsionFactor);
        Assert.False(result.Sections[0].Transposed);
        Assert.Equal("Not Applicable", result.Sections[0].AngleType);
    }

    [Fact]
    public async Task GetSectionProperties_SkipsSectionsWithNullId()
    {
        var session = CreateConnectedSession();
        _api.ListSectionsAsync(Arg.Any<CancellationToken>()).Returns(new List<Section>
        {
            new() { Id = null, Name = "Broken" },
            new() { Id = 1, Name = "Valid", Library = "Lib" }
        });

        var result = await session.GetSectionPropertiesAsync();

        Assert.Single(result.Sections);
        Assert.Equal("Valid", result.Sections[0].Name);
    }

    [Fact]
    public async Task GetSectionProperties_ApiError_WrappedInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.ListSectionsAsync(Arg.Any<CancellationToken>())
            .Returns<List<Section>>(x => throw new Exception("network error"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetSectionPropertiesAsync());

        Assert.Contains("querying section properties", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    // GET MATERIAL PROPERTIES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMaterialProperties_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetMaterialPropertiesAsync());
    }

    [Fact]
    public async Task GetMaterialProperties_EmptyResult_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>()).Returns(new List<Material>());

        var result = await session.GetMaterialPropertiesAsync();

        Assert.Contains(result.Warnings, w => w.Contains("No materials"));
        Assert.Empty(result.Materials);
    }

    [Fact]
    public async Task GetMaterialProperties_ReturnsMaterialData()
    {
        var session = CreateConnectedSession();
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>()).Returns(new List<Material>
        {
            new()
            {
                Id = 1, Name = "STEEL", Library = "Aust",
                Source = PropertySource.Library,
                YoungsModulus = 200000, PoissonsRatio = 0.3,
                MassDensity = 7850, ThermalCoeff = 1.17e-5,
                ConcreteStrength = null
            },
            new()
            {
                Id = 2, Name = "CONCRETE40", Library = null,
                Source = PropertySource.User,
                YoungsModulus = 32800, PoissonsRatio = 0.2,
                MassDensity = 2400, ThermalCoeff = 1.0e-5,
                ConcreteStrength = 40
            }
        });

        var result = await session.GetMaterialPropertiesAsync();

        Assert.Equal(2, result.Materials.Count);
        Assert.Empty(result.Warnings);

        // Steel
        Assert.Equal(1, result.Materials[0].Id);
        Assert.Equal("STEEL", result.Materials[0].Name);
        Assert.Equal("Aust", result.Materials[0].Library);
        Assert.Equal("Library", result.Materials[0].Source);
        Assert.Equal(200000, result.Materials[0].YoungsModulus);
        Assert.Equal(0.3, result.Materials[0].PoissonsRatio);
        Assert.Equal(7850, result.Materials[0].Density);
        Assert.Equal(1.17e-5, result.Materials[0].ThermalCoefficient);
        Assert.Equal(0, result.Materials[0].ConcreteStrength);

        // Concrete
        Assert.Equal(2, result.Materials[1].Id);
        Assert.Equal("CONCRETE40", result.Materials[1].Name);
        Assert.Equal("", result.Materials[1].Library);
        Assert.Equal("User", result.Materials[1].Source);
        Assert.Equal(40, result.Materials[1].ConcreteStrength);
    }

    [Fact]
    public async Task GetMaterialProperties_NullableFieldsDefaultToZeroOrEmpty()
    {
        var session = CreateConnectedSession();
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>()).Returns(new List<Material>
        {
            new() { Id = 1, Name = "Minimal", Library = null, Source = null }
        });

        var result = await session.GetMaterialPropertiesAsync();

        Assert.Single(result.Materials);
        Assert.Equal(0, result.Materials[0].YoungsModulus);
        Assert.Equal(0, result.Materials[0].PoissonsRatio);
        Assert.Equal(0, result.Materials[0].Density);
        Assert.Equal(0, result.Materials[0].ThermalCoefficient);
        Assert.Equal(0, result.Materials[0].ConcreteStrength);
        Assert.Equal("", result.Materials[0].Library);
        Assert.Equal("Unknown", result.Materials[0].Source);
    }

    [Fact]
    public async Task GetMaterialProperties_SkipsMaterialsWithNullId()
    {
        var session = CreateConnectedSession();
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>()).Returns(new List<Material>
        {
            new() { Id = null, Name = "Broken" },
            new() { Id = 1, Name = "Valid", Library = "Lib" }
        });

        var result = await session.GetMaterialPropertiesAsync();

        Assert.Single(result.Materials);
        Assert.Equal("Valid", result.Materials[0].Name);
    }

    [Fact]
    public async Task GetMaterialProperties_ApiError_WrappedInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>())
            .Returns<List<Material>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetMaterialPropertiesAsync());

        Assert.Contains("querying material properties", ex.Message);
    }
}
