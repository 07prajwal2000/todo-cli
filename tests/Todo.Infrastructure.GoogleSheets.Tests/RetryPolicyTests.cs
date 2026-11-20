using System.Net;
using System.Text.Json;
using Google;
using Todo.Application.Common;
using Todo.Application.Setup;

namespace Todo.Infrastructure.GoogleSheets.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void ComputeDelay_grows_exponentially_and_is_truncated_at_eight_seconds()
    {
        var policy = new RetryPolicy();

        Assert.Equal(TimeSpan.FromSeconds(1), policy.ComputeDelay(1, 0));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.ComputeDelay(2, 0));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.ComputeDelay(3, 0));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.ComputeDelay(4, 0));
        Assert.Equal(TimeSpan.FromSeconds(8), policy.ComputeDelay(9, 0));
    }

    [Fact]
    public void ComputeDelay_applies_at_most_twenty_five_percent_jitter()
    {
        var policy = new RetryPolicy();

        Assert.Equal(TimeSpan.FromMilliseconds(1250), policy.ComputeDelay(1, 1));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.ComputeDelay(9, 1));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.ComputeDelay(1, 0));
    }

    [Fact]
    public void ComputeDelay_rejects_a_non_positive_attempt()
    {
        var policy = new RetryPolicy();

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.ComputeDelay(0, 0));
    }

    [Fact]
    public void Constructor_rejects_a_zero_attempt_budget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(0));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public void IsRetryableStatus_matches_the_transient_failures_only(HttpStatusCode statusCode, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.IsRetryableStatus(statusCode));
    }

    [Fact]
    public async Task ExecuteAsync_maps_a_400_to_conflict_with_the_server_message()
    {
        var executor = new GoogleRequestExecutor(
            policy: new RetryPolicy(2, TimeSpan.FromSeconds(1)),
            delay: (_, _) => Task.CompletedTask);

        var error = new GoogleApiException("sheets", "Google.Apis.Requests.RequestError")
        {
            HttpStatusCode = HttpStatusCode.BadRequest,
            Error = new Google.Apis.Requests.RequestError { Code = 400, Message = "Tab already exists." },
        };

        var result = await executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(error),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Conflict, result.Error!.Code);
        Assert.Contains("Tab already exists.", result.Error!.Message, StringComparison.Ordinal);
    }
}

public sealed class SheetsConnectionFactoryTests
{
    private static readonly string ValidJson = BuildKeyJson("todo@example.iam.gserviceaccount.com");

    /// <summary>
    /// A real PKCS#8 key is generated per run so the credential ctor is exercised for real; the key
    /// never leaves the process and no network call is made.
    /// </summary>
    private static string BuildKeyJson(string email)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();

        return JsonSerializer.Serialize(new
        {
            type = "service_account",
            client_email = email,
            private_key = pem,
        });
    }

    private sealed class FakeSettingsStore(TodoSettings settings) : ISettingsStore
    {
        public Task<Result<TodoSettings>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<TodoSettings>.Success(settings));

        public Task<Result> SaveAsync(TodoSettings value, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<bool>.Success(true));

        public string DescribeLocation() => "settings.json";
    }

    private sealed class FakeCredentialStore(string? json) : ICredentialStore
    {
        public Task<Result<string>> ReadRawAsync(CancellationToken cancellationToken) =>
            Task.FromResult(json is null
                ? Result<string>.Failure(TodoError.Configuration("Credentials are missing."))
                : Result<string>.Success(json));

        public Task<Result> WriteRawAsync(string value, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public string DescribeLocation() => "credentials.json";
    }

    private static SheetsConnectionFactory Create(TodoSettings? settings = null, string? json = null) =>
        new(
            new FakeSettingsStore(settings ?? new TodoSettings { SpreadsheetId = "sheet-123" }),
            new FakeCredentialStore(json ?? ValidJson));

    private static SheetsConnectionFactory CreateWithMissingCredentials() =>
        new(new FakeSettingsStore(new TodoSettings { SpreadsheetId = "sheet-123" }), new FakeCredentialStore(null));

    [Fact]
    public async Task GetSpreadsheetIdAsync_returns_the_configured_id()
    {
        var id = await Create().GetSpreadsheetIdAsync(CancellationToken.None);

        Assert.True(id.IsSuccess);
        Assert.Equal("sheet-123", id.Value);
    }

    [Fact]
    public async Task GetSpreadsheetIdAsync_reports_configuration_when_missing()
    {
        var id = await Create(new TodoSettings { SpreadsheetId = "  " })
            .GetSpreadsheetIdAsync(CancellationToken.None);

        Assert.True(id.IsFailure);
        Assert.Equal(ErrorCode.Configuration, id.Error!.Code);
        Assert.Contains("todo init", id.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSpreadsheetIdAsync_caches_the_resolved_id()
    {
        var factory = Create();

        await factory.GetSpreadsheetIdAsync(CancellationToken.None);

        Assert.Equal("sheet-123", factory.SpreadsheetId);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reads_client_email_from_the_key()
    {
        var email = await Create().GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsSuccess);
        Assert.Equal("todo@example.iam.gserviceaccount.com", email.Value);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reports_configuration_for_unreadable_credentials()
    {
        var email = await CreateWithMissingCredentials().GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsFailure);
        Assert.Equal(ErrorCode.Configuration, email.Error!.Code);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reports_configuration_for_malformed_json()
    {
        var email = await Create(json: "{ not json").GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsFailure);
        Assert.Equal(ErrorCode.Configuration, email.Error!.Code);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reports_configuration_when_the_key_fields_are_missing()
    {
        var email = await Create(json: """{ "type": "service_account" }""")
            .GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsFailure);
        Assert.Equal(ErrorCode.Configuration, email.Error!.Code);
        Assert.Contains("client_email", email.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reports_configuration_for_an_unusable_private_key()
    {
        var json = JsonSerializer.Serialize(new
        {
            client_email = "todo@example.iam.gserviceaccount.com",
            private_key = "not-a-pem",
        });

        var email = await Create(json: json).GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsFailure);
        Assert.Equal(ErrorCode.Configuration, email.Error!.Code);
    }

    [Fact]
    public async Task GetServiceAccountEmailAsync_reports_configuration_for_empty_credentials()
    {
        var email = await Create(json: "   ").GetServiceAccountEmailAsync(CancellationToken.None);

        Assert.True(email.IsFailure);
        Assert.Equal(ErrorCode.Configuration, email.Error!.Code);
        Assert.Contains("credentials.json", email.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_fails_with_configuration_when_the_spreadsheet_id_is_missing()
    {
        var service = await Create(new TodoSettings()).GetAsync(CancellationToken.None);

        Assert.True(service.IsFailure);
        Assert.Equal(ErrorCode.Configuration, service.Error!.Code);
    }

    [Fact]
    public async Task GetAsync_refuses_an_invalid_key_before_touching_the_network()
    {
        var service = await Create(json: """{ "client_email": "a@b.c", "private_key": "nope" }""")
            .GetAsync(CancellationToken.None);

        Assert.True(service.IsFailure);
        Assert.Equal(ErrorCode.Configuration, service.Error!.Code);
    }
}
