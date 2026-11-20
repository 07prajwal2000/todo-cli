namespace Todo.Infrastructure.Configuration.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory(string? childDirectory = null)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "todo-cli-tests",
            Guid.NewGuid().ToString("N"));

        if (childDirectory is not null)
        {
            Path = System.IO.Path.Combine(Path, childDirectory);
        }

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public ConfigPaths ToConfigPaths() => new(Path);

    public string File(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
