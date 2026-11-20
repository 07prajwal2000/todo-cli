using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.Configuration.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task LoadAsyncReturnsConfigurationErrorWhenFileIsMissing()
    {
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Configuration, result.Error!.Code);
        Assert.Contains("todo init", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(_temp.File("config.json"), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistsAsyncReflectsFilePresence()
    {
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        Assert.False((await store.ExistsAsync(CancellationToken.None)).Value);

        await store.SaveAsync(new TodoSettings { SpreadsheetId = "abc" }, CancellationToken.None);

        Assert.True((await store.ExistsAsync(CancellationToken.None)).Value);
    }

    [Fact]
    public async Task SaveThenLoadRoundTripsEveryField()
    {
        var store = new JsonSettingsStore(_temp.ToConfigPaths());
        var settings = new TodoSettings
        {
            Provider = ProviderNames.GoogleSheets,
            SpreadsheetId = "sheet-abc-123",
            DefaultSpace = "work",
            CredentialsFile = "sa.json",
            ServerPort = 12345,
        };

        Assert.True((await store.SaveAsync(settings, CancellationToken.None)).IsSuccess);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.True(loaded.TryGetValue(out var value));
        Assert.Equal(settings.Provider, value.Provider);
        Assert.Equal("sheet-abc-123", value.SpreadsheetId);
        Assert.Equal("work", value.DefaultSpace);
        Assert.Equal("sa.json", value.CredentialsFile);
        Assert.Equal(12345, value.ServerPort);
    }

    [Fact]
    public async Task SaveWritesCamelCaseJson()
    {
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        await store.SaveAsync(
            new TodoSettings { SpreadsheetId = "abc", DefaultSpace = "default" },
            CancellationToken.None);

        var json = await File.ReadAllTextAsync(_temp.File("config.json"), CancellationToken.None);

        Assert.Contains("\"spreadsheetId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"serverPort\"", json, StringComparison.Ordinal);
        Assert.Contains("\"provider\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SpreadsheetId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCreatesMissingDirectory()
    {
        var nested = new ConfigPaths(_temp.File(Path.Combine("deep", "nested")));
        var store = new JsonSettingsStore(nested);

        Assert.True((await store.SaveAsync(
            new TodoSettings { SpreadsheetId = "abc" },
            CancellationToken.None)).IsSuccess);

        Assert.True(File.Exists(nested.SettingsFilePath));
    }

    [Fact]
    public async Task MalformedJsonReturnsConfigurationErrorNamingThePath()
    {
        await File.WriteAllTextAsync(
            _temp.File("config.json"),
            "{ this is not json",
            CancellationToken.None);
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Configuration, result.Error!.Code);
        Assert.Contains(_temp.File("config.json"), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptySpreadsheetIdReturnsConfigurationError()
    {
        await File.WriteAllTextAsync(
            _temp.File("config.json"),
            "{\"provider\":\"google-sheets\",\"spreadsheetId\":\"\"}",
            CancellationToken.None);
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Configuration, result.Error!.Code);
        Assert.Contains("Spreadsheet ID is missing", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAcceptsCommentsAndTrailingCommas()
    {
        await File.WriteAllTextAsync(
            _temp.File("config.json"),
            "{\n  // a comment\n  \"spreadsheetId\": \"abc\",\n}",
            CancellationToken.None);
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal("abc", value.SpreadsheetId);
        Assert.Equal(TodoSettings.DefaultServerPort, value.ServerPort);
    }

    [Fact]
    public async Task RoundTripPreservesCustomServerPortFromDisk()
    {
        await File.WriteAllTextAsync(
            _temp.File("config.json"),
            "{\"spreadsheetId\":\"abc\",\"serverPort\":15000}",
            CancellationToken.None);
        var store = new JsonSettingsStore(_temp.ToConfigPaths());

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(15000, value.ServerPort);
    }

    [Fact]
    public void DescribeLocationIsTheSettingsFile()
    {
        var paths = _temp.ToConfigPaths();

        Assert.Equal(paths.SettingsFilePath, new JsonSettingsStore(paths).DescribeLocation());
    }
}
