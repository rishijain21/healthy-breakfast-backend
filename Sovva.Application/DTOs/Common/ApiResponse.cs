namespace Sovva.Application.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string code, string message) =>
        new() { Success = false, Code = code, Message = message };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }

    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string code, string message) =>
        new() { Success = false, Code = code, Message = message };
    
    public static ApiResponse<T> Ok<T>(T data, string? message = null) =>
        ApiResponse<T>.Ok(data, message);

    public static ApiResponse<T> Fail<T>(string code, string message) =>
        ApiResponse<T>.Fail(code, message);
}
