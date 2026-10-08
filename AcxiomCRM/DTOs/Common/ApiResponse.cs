namespace AcxiomCRM.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public Dictionary<string, List<string>>? Errors { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, Dictionary<string, List<string>>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, List<string>>? Errors { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, Dictionary<string, List<string>>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}
