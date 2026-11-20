namespace Todo.Infrastructure.Configuration.Tests;

public sealed class ServiceAccountKeyTests
{
    private const string ValidJson = """
        {
          "type": "service_account",
          "project_id": "todo-cli",
          "client_email": "todo-bot@example.iam.gserviceaccount.com",
          "private_key": "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----\n"
        }
        """;

    [Fact]
    public void ParsesValidServiceAccountKey()
    {
        var parsed = ServiceAccountKey.TryParse(ValidJson, out var key, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(key);
        Assert.Equal("todo-bot@example.iam.gserviceaccount.com", key.ClientEmail);
        Assert.Contains("BEGIN PRIVATE KEY", key.PrivateKey, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsKeyWithoutExplicitType()
    {
        const string json = """
            { "client_email": "a@b.c", "private_key": "k" }
            """;

        Assert.True(ServiceAccountKey.TryParse(json, out var key, out _));
        Assert.Equal("a@b.c", key!.ClientEmail);
    }

    [Fact]
    public void RejectsMissingClientEmail()
    {
        const string json = """
            { "type": "service_account", "private_key": "k" }
            """;

        Assert.False(ServiceAccountKey.TryParse(json, out var key, out var error));
        Assert.Null(key);
        Assert.Equal("The key has no 'client_email' — is this a service account key?", error);
    }

    [Fact]
    public void RejectsMissingPrivateKey()
    {
        const string json = """
            { "type": "service_account", "client_email": "a@b.c" }
            """;

        Assert.False(ServiceAccountKey.TryParse(json, out var key, out var error));
        Assert.Null(key);
        Assert.Equal("The key has no 'private_key' — is this a service account key?", error);
    }

    [Fact]
    public void RejectsWrongType()
    {
        const string json = """
            { "type": "authorized_user", "client_email": "a@b.c", "private_key": "k" }
            """;

        Assert.False(ServiceAccountKey.TryParse(json, out var key, out var error));
        Assert.Null(key);
        Assert.Equal("This is not a service account key (expected type 'service_account').", error);
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        Assert.False(ServiceAccountKey.TryParse("{ not json", out var key, out var error));
        Assert.Null(key);
        Assert.NotNull(error);
    }

    [Fact]
    public void RejectsEmptyJson()
    {
        Assert.False(ServiceAccountKey.TryParse("  ", out var key, out var error));
        Assert.Null(key);
        Assert.NotNull(error);
    }
}
