using System;
using System.Diagnostics.CodeAnalysis;

namespace AsmResolver;

/// <summary>
/// Provides factory members for <see cref="Result{T}"/>.
/// </summary>
public static class Result
{
    /// <summary>
    /// Constructs a successful operation result with the provided result value.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The result object.</returns>
    public static Result<T> Success<T>(T value)
        where T : class
    {
        return new Result<T>(value, null);
    }

    /// <summary>
    /// Constructs an unsuccessful operation result.
    /// </summary>
    /// <param name="ex">The exception information to add describing the cause of the failure.</param>
    /// <typeparam name="T">The type of the result the operation was supposed to return.</typeparam>
    /// <returns>The result object.</returns>
    public static Result<T> Fail<T>(Exception? ex = null)
        where T : class
    {
        return new Result<T>(null, ex);
    }

    /// <summary>
    /// Constructs an unsuccessful operation result with an instance of a <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <typeparam name="T">The type of the result the operation was supposed to return.</typeparam>
    /// <returns>The result object.</returns>
    public static Result<T> InvalidOperation<T>(string message)
        where T : class
    {
        return new Result<T>(null, new InvalidOperationException(message));
    }

}

/// <summary>
/// Represents the result of an operation.
/// </summary>
/// <typeparam name="T">The type of value to return.</typeparam>
public readonly struct Result<T>
    where T : class
{
    internal Result(T? value, Exception? exception)
    {
        Value = value;
        Exception = exception;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful or not.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess => Value is not null;

    /// <summary>
    /// When successful, gets the resulting value of the operation.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// When unsuccessful, gets any exception information that was obtained during the operation.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Asserts the operation was successful and returns the value.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">Occurs when the operation was unsuccessful.</exception>
    public T Unwrap() => Value ?? throw new InvalidOperationException("Cannot unwrap a failed operation.", Exception);

    /// <summary>
    /// Checks whether the operation was successful and returns the value, else returns <c>null</c>.
    /// </summary>
    /// <returns>The value, or <c>null</c></returns>
    public T? UnwrapOrDefault() => IsSuccess ? Value : null;

    /// <summary>
    /// Converts one result type into another.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>The new result.</returns>
    /// <exception cref="InvalidOperationException">
    /// Occurs when the operation was successful but the value stored in <see cref="Value"/> could not be
    /// cast to <typeparamref name="TTarget" />.
    /// </exception>
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
