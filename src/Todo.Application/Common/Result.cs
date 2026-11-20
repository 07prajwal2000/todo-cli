using System.Diagnostics.CodeAnalysis;

namespace Todo.Application.Common;

public readonly struct Result
{
    private Result(TodoError? error) => Error = error;

    public TodoError? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(TodoError error) => new(error);

    public static implicit operator Result(TodoError error) => Failure(error);
}

public readonly struct Result<T>
{
    private Result(T value, TodoError? error)
    {
        ValueOrDefault = value;
        Error = error;
    }

    public TodoError? Error { get; }

    public T? ValueOrDefault { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(TodoError error) => new(default!, error);

    public static implicit operator Result<T>(TodoError error) => Failure(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public T Value => IsSuccess
        ? ValueOrDefault!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = IsSuccess ? ValueOrDefault : default;
        return IsSuccess;
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(Value)) : Result<TOut>.Failure(Error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        IsSuccess ? bind(Value) : Result<TOut>.Failure(Error!);
}
