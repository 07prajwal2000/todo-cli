using System.Text.Json;

namespace Todo.Infrastructure.Configuration;

public sealed record ServiceAccountKey
{
    public const string ExpectedType = "service_account";

    public string ClientEmail { get; init; } = string.Empty;

    public string PrivateKey { get; init; } = string.Empty;

    public static bool TryParse(string json, out ServiceAccountKey? key, out string? error)
    {
        key = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The service account key is empty.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException)
        {
            error = "The service account key is not valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The service account key is not valid JSON.";
                return false;
            }

            if (root.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && !string.Equals(type.GetString(), ExpectedType, StringComparison.Ordinal))
            {
                error = $"This is not a service account key (expected type '{ExpectedType}').";
                return false;
            }

            if (!TryReadString(root, "client_email", out var clientEmail))
            {
                error = "The key has no 'client_email' — is this a service account key?";
                return false;
            }

            if (!TryReadString(root, "private_key", out var privateKey))
            {
                error = "The key has no 'private_key' — is this a service account key?";
                return false;
            }

            key = new ServiceAccountKey { ClientEmail = clientEmail, PrivateKey = privateKey };
            return true;
        }
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }
}
