using Todo.Application.Common;

namespace Todo.Infrastructure.Configuration.Tests;

public sealed class FileCredentialStoreTests : IDisposable
{
    private const string ValidKeyJson = """
        {
          "type": "service_account",
          "client_email": "todo-bot@example.iam.gserviceaccount.com",
          "private_key": "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----\n"
        }
        """;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ReadRawAsyncReturnsConfigurationErrorWhenFileIsMissing()
    {
        var store = new FileCredentialStore(_temp.ToConfigPaths());

        var result = await store.ReadRawAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Configuration, result.Error!.Code);
        Assert.Contains("todo init", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(_temp.File("credentials.json"), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteThenReadRoundTripsTheJson()
    {
        var store = new FileCredentialStore(_temp.ToConfigPaths());

        Assert.True((await store.WriteRawAsync(ValidKeyJson, CancellationToken.None)).IsSuccess);

        var read = await store.ReadRawAsync(CancellationToken.None);

        Assert.True(read.TryGetValue(out var json));
        Assert.Equal(ValidKeyJson, json);
    }

    [Fact]
    public async Task WriteCreatesMissingDirectory()
    {
        var nested = new ConfigPaths(_temp.File(Path.Combine("deep", "nested")));
        var store = new FileCredentialStore(nested);

        Assert.True((await store.WriteRawAsync(ValidKeyJson, CancellationToken.None)).IsSuccess);

        Assert.True(File.Exists(nested.CredentialsFilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"client_email\": ")]
    public async Task WriteRejectsNonJsonWithValidationError(string json)
    {
        var store = new FileCredentialStore(_temp.ToConfigPaths());

        var result = await store.WriteRawAsync(json, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error!.Code);
        Assert.Equal("The service account key is not valid JSON.", result.Error.Message);
        Assert.False(File.Exists(_temp.File("credentials.json")));
    }

    [Fact]
    public void DescribeLocationIsTheCredentialsFile()
    {
        var paths = _temp.ToConfigPaths();

        Assert.Equal(paths.CredentialsFilePath, new FileCredentialStore(paths).DescribeLocation());
    }

    [Fact]
    public async Task WriteRestrictsPermissionsToOwnerOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new FileCredentialStore(_temp.ToConfigPaths());
        await store.WriteRawAsync(ValidKeyJson, CancellationToken.None);

        var mode = File.GetUnixFileMode(_temp.File("credentials.json"));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }
}
