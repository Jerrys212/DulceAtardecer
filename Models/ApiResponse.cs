namespace DulceAtardecer.Models;

public record ApiResponse<T>(
    bool Success,
    int StatusCode,
    string? Message,
    T? Data,
    IDictionary<string, string[]>? Errors = null);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null, int statusCode = StatusCodes.Status200OK) =>
        new(true, statusCode, message, data);

    public static ApiResponse<object> Fail(int statusCode, string? message = null, IDictionary<string, string[]>? errors = null) =>
        new(false, statusCode, message ?? DefaultMessage(statusCode), null, errors);

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
