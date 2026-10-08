namespace AcxiomCRM.Models;

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public Dictionary<string, List<string>> Errors { get; set; } = new();
    public bool IsNotFound { get; set; }
    public bool IsUnauthorized { get; set; }

    public static ServiceResult<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    public static ServiceResult<T> Fail(string message, Dictionary<string, List<string>>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? new() };

    public static ServiceResult<T> NotFound(string message = "Resource not found.") =>
        new() { Success = false, Message = message, IsNotFound = true };

    public static ServiceResult<T> Unauthorized(string message = "You do not have permission to access this resource.") =>
        new() { Success = false, Message = message, IsUnauthorized = true };
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Errors { get; set; } = new();
    public bool IsNotFound { get; set; }
    public bool IsUnauthorized { get; set; }

    public static ServiceResult Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static ServiceResult Fail(string message, Dictionary<string, List<string>>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? new() };

    public static ServiceResult NotFound(string message = "Resource not found.") =>
        new() { Success = false, Message = message, IsNotFound = true };

    public static ServiceResult Unauthorized(string message = "You do not have permission to access this resource.") =>
        new() { Success = false, Message = message, IsUnauthorized = true };
}
