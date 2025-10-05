using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Services;

public sealed record ServiceError(int StatusCode, string Title, string? Detail = null)
{
    public ProblemDetails ToProblemDetails()
        => new()
        {
            Status = StatusCode,
            Title = Title,
            Detail = Detail
        };
}

public sealed class ServiceResult
{
    public bool Success { get; }
    public ServiceError? Error { get; }

    private ServiceResult(bool success, ServiceError? error)
    {
        Success = success;
        Error = error;
    }

    public static ServiceResult Ok() => new(true, null);

    public static ServiceResult Fail(ServiceError error)
        => new(false, error);

    public void Deconstruct(out bool success, out ServiceError? error)
    {
        success = Success;
        error = Error;
    }
}

public sealed class ServiceResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public ServiceError? Error { get; }

    private ServiceResult(bool success, T? value, ServiceError? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public static ServiceResult<T> Ok(T value) => new(true, value, null);

    public static ServiceResult<T> Fail(ServiceError error) => new(false, default, error);

    public void Deconstruct(out bool success, out T? value, out ServiceError? error)
    {
        success = Success;
        value = Value;
        error = Error;
    }
}
