using System.Collections.Frozen;
using System.Reflection;

namespace Todo.Web.Assets;

/// <summary>
/// Serves the dashboard's static files out of the assembly manifest. The project embeds
/// <c>wwwroot/**</c>, so there is no physical asset directory to rely on at run time.
/// </summary>
public sealed class EmbeddedAssetProvider
{
    public const string IndexFile = "index.html";

    private static readonly FrozenDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".json"] = "application/json; charset=utf-8",
        [".woff2"] = "font/woff2",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;
    private readonly FrozenDictionary<string, string> _resourcesByFile;

    public EmbeddedAssetProvider()
        : this(typeof(EmbeddedAssetProvider).Assembly)
    {
    }

    public EmbeddedAssetProvider(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _assembly = assembly;
        _resourcePrefix = $"{assembly.GetName().Name}.wwwroot.";
        _resourcesByFile = IndexResources(assembly, _resourcePrefix);
    }

    public static string ContentTypeFor(string fileName) =>
        ContentTypes.TryGetValue(Path.GetExtension(fileName), out var contentType)
            ? contentType
            : "application/octet-stream";

    public static string CacheControlFor(string fileName) =>
        string.Equals(fileName, IndexFile, StringComparison.OrdinalIgnoreCase)
            ? "no-cache"
            : "public, max-age=60";

    /// <summary>
    /// Maps a request path to an embedded file. A path of <c>/</c> resolves to the entry page; any
    /// other unknown path resolves to nothing so the SPA does not answer 200 for missing assets.
    /// </summary>
    public bool TryResolve(string? requestPath, out string fileName)
    {
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(requestPath) || requestPath == "/")
        {
            fileName = IndexFile;
            return _resourcesByFile.ContainsKey(IndexFile);
        }

        var trimmed = requestPath.TrimStart('/');

        if (trimmed.Length == 0 || trimmed.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (!_resourcesByFile.ContainsKey(trimmed))
        {
            return false;
        }

        fileName = trimmed;
        return true;
    }

    public Stream OpenRead(string fileName)
    {
        if (!_resourcesByFile.TryGetValue(fileName, out var resourceName))
        {
            throw new FileNotFoundException($"Embedded asset '{fileName}' was not found.", fileName);
        }

        return _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' could not be opened.", resourceName);
    }

    public IReadOnlyList<string> FileNames => [.. _resourcesByFile.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    private static FrozenDictionary<string, string> IndexResources(Assembly assembly, string prefix) =>
        assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .ToFrozenDictionary(
                name => name[prefix.Length..],
                name => name,
                StringComparer.OrdinalIgnoreCase);
}
