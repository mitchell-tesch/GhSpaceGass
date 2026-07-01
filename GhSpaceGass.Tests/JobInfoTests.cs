using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class JobInfoTests
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

    private static JobStatus CreateFullJobStatus(
        string heading = "Test Job",
        string projectHeading = "Project A",
        string designerInitials = "MT",
        string notes = "Test notes",
        VerticalAxis verticalAxis = VerticalAxis.YAxis,
        LengthUnit length = LengthUnit.Mm,
        ForceUnit force = ForceUnit.KN,
        MomentUnit moment = MomentUnit.KNm,
        StressUnit stress = StressUnit.MPa,
        TemperatureUnit temperature = TemperatureUnit.DegC,
        int nodes = 4, int members = 3, int sections = 1, int materials = 1,
        int restraints = 2, int loadCases = 2, int nodeLoads = 3,
        bool hasStaticResults = true)
    {
        return new JobStatus
        {
            State = new JobState
            {
                IsOpen = true,
                IsNew = false,
                IsModified = true,
                File = new JobFile
                {
                    Name = "test.sg",
                    Path = @"C:\Models\test.sg"
                }
            },
            Job = new Job
            {
                Headings = new JobHeadings
                {
                    Heading = heading,
                    ProjectHeading = projectHeading,
                    DesignerInitials = designerInitials,
                    Notes = notes
                },
                Settings = new JobSettings
                {
                    VerticalAxis = verticalAxis
                },
                Units = new Units
                {
                    Length = length,
                    Force = force,
                    Moment = moment,
                    Stress = stress,
                    Temperature = temperature,
                    Mass = MassUnit.Kg,
                    MassDensity = MassDensityUnit.Kgperm3,
                    Translation = TranslationUnit.Mm,
                    Acceleration = AccelerationUnit.Mpersec2,
                    SectionProperties = SectionPropertiesUnit.Mm,
                    MaterialStrength = MaterialStrengthUnit.MPa
                }
            },
            Structure = new StructureSummary
            {
                Nodes = nodes,
                Members = members,
                Sections = sections,
                Materials = materials,
                NodeRestraints = restraints,
                Plates = 0
            },
            Loads = new LoadsSummary
            {
                LoadCases = loadCases,
                LoadCategories = 1,
                NodeLoads = nodeLoads,
                MemberDistributedLoads = 0,
                SelfWeightLoads = 0
            },
            Analysis = new AnalysisResultsSummary
            {
                HasStaticResults = hasStaticResults,
                HasBucklingResults = false,
                HasDynamicResults = false
            }
        };
    }

    // ── GetJobInfoAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetJobInfoAsync_WhenConnected_ReturnsFullJobInfo()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.NotNull(info);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsHeadingsCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(
            "My Heading",
            "My Project",
            "AB",
            "Some notes");
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal("My Heading", info.Heading);
        Assert.Equal("My Project", info.ProjectHeading);
        Assert.Equal("AB", info.DesignerInitials);
        Assert.Equal("Some notes", info.Notes);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsVerticalAxisCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(verticalAxis: VerticalAxis.YAxis);
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal("YAxis", info.VerticalAxis);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsUnitsCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(
            length: LengthUnit.Mm,
            force: ForceUnit.KN,
            moment: MomentUnit.KNm,
            stress: StressUnit.MPa,
            temperature: TemperatureUnit.DegC);
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal("Mm", info.LengthUnit);
        Assert.Equal("KN", info.ForceUnit);
        Assert.Equal("KNm", info.MomentUnit);
        Assert.Equal("MPa", info.StressUnit);
        Assert.Equal("DegC", info.TemperatureUnit);
        Assert.Equal("Kg", info.MassUnit);
        Assert.Equal("Kgperm3", info.MassDensityUnit);
        Assert.Equal("Mm", info.TranslationUnit);
        Assert.Equal("Mpersec2", info.AccelerationUnit);
        Assert.Equal("Mm", info.SectionPropertiesUnit);
        Assert.Equal("MPa", info.MaterialStrengthUnit);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsStateCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.True(info.IsOpen);
        Assert.False(info.IsNew);
        Assert.True(info.IsModified);
        Assert.Equal(@"C:\Models\test.sg", info.FilePath);
        Assert.Equal("test.sg", info.FileName);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsStructureSummaryCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(nodes: 10, members: 8, sections: 3, materials: 2, restraints: 4);
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal(10, info.NodeCount);
        Assert.Equal(8, info.MemberCount);
        Assert.Equal(3, info.SectionCount);
        Assert.Equal(2, info.MaterialCount);
        Assert.Equal(4, info.RestraintCount);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsLoadsSummaryCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(loadCases: 5, nodeLoads: 12);
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal(5, info.LoadCaseCount);
        Assert.Equal(12, info.NodeLoadCount);
    }

    [Fact]
    public async Task GetJobInfoAsync_MapsAnalysisSummaryCorrectly()
    {
        var session = CreateConnectedSession();
        var status = CreateFullJobStatus(hasStaticResults: true);
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.True(info.HasStaticResults);
        Assert.False(info.HasBucklingResults);
        Assert.False(info.HasDynamicResults);
    }

    [Fact]
    public async Task GetJobInfoAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetJobInfoAsync());
    }

    [Fact]
    public async Task GetJobInfoAsync_WhenApiThrows_WrapsInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API error"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetJobInfoAsync());
        Assert.Contains("getting job status", ex.Message);
    }

    [Fact]
    public async Task GetJobInfoAsync_WithNullSubObjects_DefaultsToEmptyValues()
    {
        var session = CreateConnectedSession();
        var status = new JobStatus(); // all sub-objects null
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var info = await session.GetJobInfoAsync();

        Assert.Equal(string.Empty, info.Heading);
        Assert.Equal(string.Empty, info.VerticalAxis);
        Assert.Equal(string.Empty, info.LengthUnit);
        Assert.Equal(0, info.NodeCount);
        Assert.Equal(0, info.LoadCaseCount);
        Assert.False(info.HasStaticResults);
    }

    // ── UpdateHeadingsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateHeadingsAsync_WhenConnected_CallsApiWithCorrectData()
    {
        var session = CreateConnectedSession();
        _api.UpdateHeadingsAsync(Arg.Any<JobHeadingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new JobHeadings
            {
                Heading = "New Heading",
                ProjectHeading = "New Project",
                DesignerInitials = "CD",
                Notes = "New notes"
            });
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateFullJobStatus(
                "New Heading",
                "New Project",
                "CD",
                "New notes"));

        var info = await session.UpdateHeadingsAsync(
            "New Heading",
            "New Project",
            "CD",
            "New notes");

        await _api.Received(1).UpdateHeadingsAsync(
            Arg.Is<JobHeadingsUpdate>(h =>
                h.Heading == "New Heading" &&
                h.ProjectHeading == "New Project" &&
                h.DesignerInitials == "CD" &&
                h.Notes == "New notes"),
            Arg.Any<CancellationToken>());

        Assert.Equal("New Heading", info.Heading);
        Assert.Equal("New Project", info.ProjectHeading);
    }

    [Fact]
    public async Task UpdateHeadingsAsync_WithPartialInput_OnlySetsProvidedFields()
    {
        var session = CreateConnectedSession();
        _api.UpdateHeadingsAsync(Arg.Any<JobHeadingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new JobHeadings { Heading = "Only Heading" });
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateFullJobStatus("Only Heading"));

        await session.UpdateHeadingsAsync("Only Heading");

        await _api.Received(1).UpdateHeadingsAsync(
            Arg.Is<JobHeadingsUpdate>(h =>
                h.Heading == "Only Heading" &&
                h.ProjectHeading == null &&
                h.DesignerInitials == null &&
                h.Notes == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateHeadingsAsync_ReturnsFullJobInfoAfterUpdate()
    {
        var session = CreateConnectedSession();
        _api.UpdateHeadingsAsync(Arg.Any<JobHeadingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new JobHeadings { Heading = "Updated" });
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateFullJobStatus(
                "Updated",
                nodes: 5, members: 4));

        var info = await session.UpdateHeadingsAsync("Updated");

        // Returns full job info (not just headings)
        Assert.Equal("Updated", info.Heading);
        Assert.Equal(5, info.NodeCount);
        Assert.Equal(4, info.MemberCount);
    }

    [Fact]
    public async Task UpdateHeadingsAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.UpdateHeadingsAsync("Test"));
    }

    [Fact]
    public async Task UpdateHeadingsAsync_WhenApiThrows_WrapsInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.UpdateHeadingsAsync(Arg.Any<JobHeadingsUpdate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("PATCH failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.UpdateHeadingsAsync("Test"));
        Assert.Contains("updating job headings", ex.Message);
    }

    // ── MapJobStatus (static mapping) ────────────────────────────────────

    [Fact]
    public void MapJobStatus_WithImperialUnits_MapsCorrectly()
    {
        var status = new JobStatus
        {
            Job = new Job
            {
                Units = new Units
                {
                    Length = LengthUnit.Ft,
                    Force = ForceUnit.K,
                    Moment = MomentUnit.Kft,
                    Stress = StressUnit.Ksi,
                    Temperature = TemperatureUnit.DegF
                }
            }
        };

        var info = SpaceGassSession.MapJobStatus(status);

        Assert.Equal("Ft", info.LengthUnit);
        Assert.Equal("K", info.ForceUnit);
        Assert.Equal("Kft", info.MomentUnit);
        Assert.Equal("Ksi", info.StressUnit);
        Assert.Equal("DegF", info.TemperatureUnit);
    }

    [Fact]
    public void MapJobStatus_WithZVerticalAxis_MapsCorrectly()
    {
        var status = new JobStatus
        {
            Job = new Job
            {
                Settings = new JobSettings
                {
                    VerticalAxis = VerticalAxis.ZAxis
                }
            }
        };

        var info = SpaceGassSession.MapJobStatus(status);

        Assert.Equal("ZAxis", info.VerticalAxis);
    }

    // ── FormatUnits ─────────────────────────────────────────────────────

    [Fact]
    public void FormatUnits_ProducesExpectedString()
    {
        var info = new SgJobInfo
        {
            LengthUnit = "Mm",
            ForceUnit = "KN",
            MomentUnit = "KNm",
            StressUnit = "MPa",
            TemperatureUnit = "DegC",
            MassUnit = "Kg"
        };

        var result = info.FormatUnits();

        Assert.Contains("Length: mm", result);
        Assert.Contains("Force: kN", result);
        Assert.Contains("Moment: kN·m", result);
        Assert.Contains("Stress: MPa", result);
        Assert.Contains("Temperature: °C", result);
        Assert.Contains("Mass: kg", result);
    }

    // ── FormatStatus ────────────────────────────────────────────────────

    [Fact]
    public void FormatStatus_IncludesAllSections()
    {
        var info = new SgJobInfo
        {
            FilePath = @"C:\Models\test.sg",
            IsModified = true,
            VerticalAxis = "YAxis",
            LengthUnit = "Mm",
            ForceUnit = "KN",
            MomentUnit = "KNm",
            StressUnit = "MPa",
            TemperatureUnit = "DegC",
            MassUnit = "Kg",
            NodeCount = 10,
            MemberCount = 8,
            SectionCount = 2,
            MaterialCount = 1,
            RestraintCount = 4,
            LoadCaseCount = 3,
            NodeLoadCount = 5,
            HasStaticResults = true
        };

        var result = info.FormatStatus();

        Assert.Contains(@"C:\Models\test.sg", result);
        Assert.Contains("Modified", result);
        Assert.Contains("Vertical Axis: Y", result);
        Assert.Contains("10 nodes", result);
        Assert.Contains("8 members", result);
        Assert.Contains("3 cases", result);
        Assert.Contains("Static", result);
    }

    [Fact]
    public void FormatStatus_WhenNewJob_ShowsNewStatus()
    {
        var info = new SgJobInfo { IsNew = true };

        var result = info.FormatStatus();

        Assert.Contains("New job", result);
    }

    [Fact]
    public void FormatStatus_WhenNoResults_ShowsNone()
    {
        var info = new SgJobInfo();

        var result = info.FormatStatus();

        Assert.Contains("Analysis Results: None", result);
    }

    // ── DisplayUnit formatting ──────────────────────────────────────────

    [Theory]
    [InlineData("Mm", "mm")]
    [InlineData("M", "m")]
    [InlineData("Ft", "ft")]
    [InlineData("In", "in")]
    [InlineData("Cm", "cm")]
    [InlineData("KN", "kN")]
    [InlineData("N", "N")]
    [InlineData("K", "kip")]
    [InlineData("Lb", "lb")]
    [InlineData("KNm", "kN·m")]
    [InlineData("Kft", "kip·ft")]
    [InlineData("MPa", "MPa")]
    [InlineData("Ksi", "ksi")]
    [InlineData("DegC", "°C")]
    [InlineData("DegF", "°F")]
    [InlineData("Kg", "kg")]
    [InlineData("T", "t")]
    [InlineData("Kgperm3", "kg/m³")]
    [InlineData("Mpersec2", "m/s²")]
    [InlineData("Gs", "g")]
    [InlineData("UnknownValue", "UnknownValue")]
    public void DisplayUnit_MapsToStandardAbbreviation(string raw, string expected)
    {
        Assert.Equal(expected, SgJobInfo.DisplayUnit(raw));
    }

    // ── FormatVerticalAxis ──────────────────────────────────────────────

    [Theory]
    [InlineData("YAxis", "Y")]
    [InlineData("ZAxis", "Z")]
    [InlineData("", "")]
    public void FormatVerticalAxis_MapsToShortForm(string raw, string expected)
    {
        Assert.Equal(expected, SgJobInfo.FormatVerticalAxis(raw));
    }
}