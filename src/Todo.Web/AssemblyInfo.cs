using System.Runtime.CompilerServices;

// The endpoint wiring, request guard and asset provider are internal on purpose: the only surface
// the CLI (or anyone else) needs is DashboardHost. Tests still need to compose the same application.
[assembly: InternalsVisibleTo("Todo.Web.Tests")]
