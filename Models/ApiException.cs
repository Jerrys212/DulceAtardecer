namespace DulceAtardecer.Models;

public class ApiException(
    int statusCode,
    string? message = null,
    IEnumerable<string>? errors = null,
    object? responseData = null)
    : Exception(message ?? ApiResponse.DefaultMessage(statusCode))
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<string> Errors { get; } = errors?.ToList() ?? [];
    public object? ResponseData { get; } = responseData;
}
