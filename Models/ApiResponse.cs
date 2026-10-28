namespace DulceAtardecer.Models;

public interface IApiResponse
{
    int StatusCode { get; set; }
}

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; init; }
    public int StatusCode { get; set; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) =>
        new() { Success = true, StatusCode = StatusCodes.Status200OK, Message = message, Data = data };

    public static ApiResponse<object> Fail(
        int statusCode,
        string? message = null,
        IEnumerable<string>? errors = null,
        object? data = null)
    {
        string resolvedMessage = message ?? DefaultMessage(statusCode);
        List<string> resolvedErrors = errors?.Where(error => !string.IsNullOrWhiteSpace(error)).ToList() ?? [];

        return new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = resolvedMessage,
            Data = data,
            Errors = resolvedErrors.Count > 0 ? resolvedErrors : [resolvedMessage],
        };
    }

    public static string DefaultMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "La solicitud no es válida",
        StatusCodes.Status401Unauthorized => "No autenticado",
        StatusCodes.Status403Forbidden => "No tienes permiso para realizar esta acción",
        StatusCodes.Status404NotFound => "Recurso no encontrado",
        StatusCodes.Status405MethodNotAllowed => "Método no permitido",
        StatusCodes.Status409Conflict => "La operación entra en conflicto con el estado actual",
        StatusCodes.Status429TooManyRequests => "Demasiadas solicitudes, intenta más tarde",
        _ => "Ocurrió un error inesperado",
    };
}
