using System;
using System.Diagnostics.CodeAnalysis;

namespace AsmResolver;

public static class Result
{
    public static Result<T> Success<T>(T resolved)
        where T : class
    {
        return new Result<T>(resolved, null);
    }

    public static Result<T> Fail<T>(Exception? ex = null)
        where T : class
    {
        return new Result<T>(null, ex);
    }

    public static Result<T> InvalidOperation<T>(string message)
        where T : class
    {
        return new Result<T>(null, new InvalidOperationException(message));
    }

}

public readonly struct Result<T>
    where T : class
{
    internal Result(T? value, Exception? exception)
    {
        Value = value;
        Exception = exception;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public Exception? Exception { get; }

    public T Unwrap() => Value ?? throw new InvalidOperationException("Cannot unwrap a failed operation.", Exception);

    public T? UnwrapOrDefault() => IsSuccess ? Value : null;

    public Result<TTarget> Into<TTarget>()
        where TTarget : class
    {
        if (IsSuccess)
        {
            // Manual cast is required here because we cannot cast generics.
            if (Value is not TTarget target)
                throw new InvalidOperationException($"Cannot cast an instance of {typeof(T)} into an instance of {typeof(TTarget)}.");

            return new Result<TTarget>(target, Exception);
        }

        return new Result<TTarget>(null, Exception);
    }
}
