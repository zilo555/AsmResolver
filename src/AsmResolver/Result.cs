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

    public static Result<T> Fail<T>()
        where T : class
    {
        // TODO: forward exception info.
        return new Result<T>(null, null);
    }

}

public readonly struct Result<T>
    where T : class
{
    internal Result(T? resolved, Exception? exception)
    {
        Resolved = resolved;
        Exception = exception;
    }

    [MemberNotNullWhen(true, nameof(Resolved))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess => Resolved is not null;

    public T? Resolved { get; }

    public Exception? Exception { get; }

    public T Unwrap() => Resolved ?? throw new InvalidOperationException("Cannot unwrap a failed operation.", Exception);

    public T? UnwrapOrDefault() => IsSuccess ? Resolved : null;

    public Result<TTarget> Into<TTarget>()
        where TTarget : class
    {
        if (IsSuccess)
        {
            // Manual cast is required here because we cannot cast generics.
            if (Resolved is not TTarget resolved)
                throw new InvalidOperationException($"Cannot cast an instance of {typeof(T)} into an instance of {typeof(TTarget)}.");

            return new Result<TTarget>(resolved, Exception);
        }

        return new Result<TTarget>(null, Exception);
    }
}
