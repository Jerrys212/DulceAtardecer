namespace DulceAtardecer.Models;

public class ApiException(int statusCode, string? message = null)
    : Exception(message ?? ApiResponse.DefaultMessage(statusCode))
{
    public int StatusCode { get; } = statusCode;
}
