using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class SaveJobTests
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

    private static JobStatus CreateJobStatusWithFile(string filePath = @"C:\Models\test.sg")
    {
        return new JobStatus
        {
            State = new JobState
            {
                IsOpen = true,
                IsNew = false,
                IsModified = false,
                File = new JobFile
                {
                    Name = Path.GetFileName(filePath),
                    Path = filePath
                }
            },
            Structure = new StructureSummary
            {
                Nodes = 4,
                Members = 3,
                Sections = 1,
                Materials = 1,
                NodeRestraints = 2,
                Plates = 2
            },
            Loads = new LoadsSummary
            {
                LoadCases = 2,
                LoadCategories = 1,
                NodeLoads = 3,
                MemberDistributedLoads = 0,
                SelfWeightLoads = 0
            },
            Analysis = new AnalysisResultsSummary
            {
                HasStaticResults = true,
                HasBucklingResults = false,
                HasDynamicResults = false
            }
        };
    }

    // ── SaveAndGetInfoAsync — with explicit file path ─────────────────

    [Fact]
    public async Task SaveAndGetInfo_WithFilePath_CallsSaveWithPath()
    {
        var session = CreateConnectedSession();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Output\model.sg"));

        await session.SaveAndGetInfoAsync(@"C:\Output\model.sg");

        await _api.Received(1).SaveJobAsync(@"C:\Output\model.sg", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAndGetInfo_WithFilePath_ReturnsJobInfo()
    {
        var session = CreateConnectedSession();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Output\model.sg"));

        var result = await session.SaveAndGetInfoAsync(@"C:\Output\model.sg");

        Assert.NotNull(result);
        Assert.Equal(4, result.NodeCount);
        Assert.Equal(3, result.MemberCount);
        Assert.Equal(2, result.PlateCount);
        Assert.Equal(2, result.LoadCaseCount);
        Assert.True(result.HasStaticResults);
    }

    [Fact]
    public async Task SaveAndGetInfo_WithFilePath_QueriesStatusAfterSave()
    {
        var session = CreateConnectedSession();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Output\model.sg"));

        await session.SaveAndGetInfoAsync(@"C:\Output\model.sg");

        // Save should be called before status query
        Received.InOrder(() =>
        {
            _api.SaveJobAsync(@"C:\Output\model.sg", Arg.Any<CancellationToken>());
            _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>());
        });
    }

    // ── SaveAndGetInfoAsync — without file path (use current) ─────────

    [Fact]
    public async Task SaveAndGetInfo_WithoutFilePath_ResolvesCurrentPath()
    {
        var session = CreateConnectedSession();
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Models\existing.sg"));
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Models\existing.sg"));

        await session.SaveAndGetInfoAsync(null);

        await _api.Received(1).SaveJobAsync(@"C:\Models\existing.sg", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAndGetInfo_WithEmptyFilePath_ResolvesCurrentPath()
    {
        var session = CreateConnectedSession();
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Models\existing.sg"));
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Models\existing.sg"));

        await session.SaveAndGetInfoAsync("");

        await _api.Received(1).SaveJobAsync(@"C:\Models\existing.sg", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAndGetInfo_WithoutFilePath_NoFileAssociated_Throws()
    {
        var session = CreateConnectedSession();
        var statusNoFile = new JobStatus
        {
            State = new JobState { IsOpen = true, File = new JobFile { Path = null } }
        };
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>()).Returns(statusNoFile);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SaveAndGetInfoAsync(null));

        Assert.Contains("No file path", ex.Message);
    }

    [Fact]
    public async Task SaveAndGetInfo_WithoutFilePath_EmptyPathInStatus_Throws()
    {
        var session = CreateConnectedSession();
        var statusNoFile = new JobStatus
        {
            State = new JobState { IsOpen = true, File = new JobFile { Path = "" } }
        };
        _api.GetJobStatusAsync(Arg.Any<CancellationToken>()).Returns(statusNoFile);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SaveAndGetInfoAsync(null));

        Assert.Contains("No file path", ex.Message);
    }

    // ── SaveAndGetInfoAsync — error handling ──────────────────────────

    [Fact]
    public async Task SaveAndGetInfo_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SaveAndGetInfoAsync(@"C:\Models\test.sg"));
    }

    [Fact]
    public async Task SaveAndGetInfo_WhenSaveFails_ThrowsWithFormattedMessage()
    {
        var session = CreateConnectedSession();
        _api.SaveJobAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Disk full"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SaveAndGetInfoAsync(@"C:\Models\test.sg"));

        Assert.Contains("saving job", ex.Message);
    }

    [Fact]
    public async Task SaveAndGetInfo_WhenSaveCancelled_ThrowsOperationCancelledException()
    {
        var session = CreateConnectedSession();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => session.SaveAndGetInfoAsync(@"C:\Models\test.sg", cts.Token));
    }

    // ── SaveAndGetInfoAsync — returns file path from result ────────────

    [Fact]
    public async Task SaveAndGetInfo_ReturnsFilePathFromJobStatus()
    {
        var session = CreateConnectedSession();
        _api.GetFullJobStatusAsync(Arg.Any<CancellationToken>())
            .Returns(CreateJobStatusWithFile(@"C:\Output\saved_model.sg"));

        var result = await session.SaveAndGetInfoAsync(@"C:\Output\saved_model.sg");

        Assert.Equal(@"C:\Output\saved_model.sg", result.FilePath);
    }
}

