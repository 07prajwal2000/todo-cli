// Factory methods on Result<T> are the idiomatic way to express a success/failure value type;
// moving them to a non-generic helper would force every call site to spell out the type twice.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Result<T>.Success/Failure are the documented factory pattern for this value type.",
    Scope = "type",
    Target = "~T:Todo.Application.Common.Result`1")]
