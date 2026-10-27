namespace DulceAtardecer.Models;

public class ApiException(
    int statusCode,
    string? message = null,
    IDictionary<string, string[]>? errors = null)
    : Exception(message ?? ApiResponse.DefaultMessage(statusCode))
{
    public int StatusCode { get; } = statusCode;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}
