using Todo.Application.Common;
using Todo.Application.Setup;
using Todo.Application.Spaces;
using Todo.Application.Tests.Fakes;
using Todo.Domain.Spaces;

namespace Todo.Application.Tests;

public sealed class SetupServiceTests
{
    private const string CredentialsJson = """{"type":"service_account"}""";

    private readonly FakeSettingsStore _settingsStore = new();
    private readonly FakeCredentialStore _credentialStore = new();
    private readonly FakeBackendProbe _probe = new();
    private readonly FakeSpaceRepository _spaceRepository = new(SpaceName.Default);
    private readonly SetupService _service;

    public SetupServiceTests() =>
        _service = new SetupService(
            _settingsStore,
            _credentialStore,
            _probe,
            new SpaceService(_spaceRepository));

    private static InitRequest Request(
        string credentialsJson = CredentialsJson,
        string spreadsheetId = "sheet-123",
        string defaultSpace = "default") => new()
        {
            CredentialsJson = credentialsJson,
            SpreadsheetId = spreadsheetId,
            DefaultSpace = defaultSpace,
        };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/space")]
    [InlineData("_reserved")]
    public async Task InitializeAsyncRejectsAnInvalidDefaultSpace(string defaultSpace)
    {
        var result = await _service.InitializeAsync(Request(defaultSpace: defaultSpace), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Equal(0, _credentialStore.WriteCalls);
        Assert.Equal(0, _settingsStore.SaveCalls);
        Assert.Equal(0, _probe.ProbeCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InitializeAsyncRejectsAnEmptySpreadsheetId(string spreadsheetId)
    {
        var result = await _service.InitializeAsync(Request(spreadsheetId: spreadsheetId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Contains("spreadsheet", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _credentialStore.WriteCalls);
        Assert.Equal(0, _settingsStore.SaveCalls);
    }

    [Fact]
    public async Task InitializeAsyncWritesCredentialsSavesSettingsProbesAndEnsuresTheSpace()
    {
        var result = await _service.InitializeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _credentialStore.WriteCalls);
        Assert.Equal(CredentialsJson, _credentialStore.Written);
        Assert.Equal(1, _settingsStore.SaveCalls);
        Assert.Equal(1, _probe.ProbeCalls);
    }

    [Fact]
    public async Task InitializeAsyncPersistsTheTrimmedRequestValues()
    {
        var result = await _service.InitializeAsync(
            new InitRequest
            {
                CredentialsJson = CredentialsJson,
                SpreadsheetId = "  sheet-123  ",
                DefaultSpace = "  default  ",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("sheet-123", result.Value.SpreadsheetId);
        Assert.Equal("default", result.Value.DefaultSpace);
        Assert.Equal("sheet-123", _settingsStore.Saved!.SpreadsheetId);
        Assert.Equal("default", _settingsStore.Saved!.DefaultSpace);
    }

    [Fact]
    public async Task InitializeAsyncEnsuresTheSpaceWhenItDoesNotExistYet()
    {
        var result = await _service.InitializeAsync(Request(defaultSpace: "work"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(SpaceName.Create("work"), _spaceRepository.Spaces);
    }

    [Fact]
    public async Task InitializeAsyncDoesNotRecreateAnExistingSpace()
    {
        var result = await _service.InitializeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _spaceRepository.CreateCalls);
        Assert.Equal(1, _spaceRepository.ExistsCalls);
    }

    [Fact]
    public async Task InitializeAsyncPropagatesACredentialWriteFailure()
    {
        _credentialStore.WriteFailure = TodoError.Permission("Cannot write credentials.");

        var result = await _service.InitializeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permission, result.Error!.Code);
        Assert.Equal(0, _settingsStore.SaveCalls);
        Assert.Equal(0, _probe.ProbeCalls);
    }

    [Fact]
    public async Task InitializeAsyncPropagatesASettingsSaveFailure()
    {
        _settingsStore.SaveFailure = TodoError.Configuration("Cannot write settings.");

        var result = await _service.InitializeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Configuration, result.Error!.Code);
        Assert.Equal(0, _probe.ProbeCalls);
    }

    [Fact]
    public async Task InitializeAsyncPropagatesAProbeFailure()
    {
        _probe.ProbeFailure = TodoError.Unavailable("Spreadsheet is not reachable.");

        var result = await _service.InitializeAsync(Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Unavailable, result.Error!.Code);
        Assert.Equal(1, _probe.ProbeCalls);
        Assert.Equal(0, _spaceRepository.ExistsCalls);
    }

    [Fact]
    public async Task InitializeAsyncPropagatesASpaceCreationFailure()
    {
        _spaceRepository.CreateFailure = TodoError.Permission("No write access.");

        var result = await _service.InitializeAsync(Request(defaultSpace: "work"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Permission, result.Error!.Code);
    }
}
