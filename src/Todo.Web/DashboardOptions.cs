namespace Todo.Web;

public sealed record DashboardOptions
{
    public int Port { get; init; } = 12000;

    public bool OpenBrowser { get; init; } = true;
}
