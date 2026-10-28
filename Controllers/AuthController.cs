using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Middleware;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DulceAtardecer.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class AuthController(IUserRepository userRepository) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";
    private const string RefreshCookiePath = "/api/v1/auth";
    private const string RequestedWithHeader = "X-Requested-With";

    [AllowAnonymous]
    [EnableRateLimiting(PolicyNames.LoginRateLimit)]
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<LoginResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        AuthResult result = await userRepository.LoginAsync(
            request.Username,
            request.Password,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetRefreshCookie(result.RefreshToken);

        return Ok(ApiResponse.Ok(new LoginResponseDto(
            result.AccessToken.Token,
            result.AccessToken.ExpiresAt,
            result.User.Adapt<CurrentUserDto>())));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AccessTokenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Headers.ContainsKey(RequestedWithHeader))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, $"Falta el encabezado {RequestedWithHeader}");
        }

        if (!Request.Cookies.TryGetValue(RefreshCookieName, out string? refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized);
        }

        RefreshResult result;
        try
        {
            result = await userRepository.RefreshAsync(refreshToken, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            DeleteRefreshCookie();
            return Unauthorized(ApiResponse.Fail(StatusCodes.Status401Unauthorized, ex.Message));
        }

        if (result.RefreshToken is not null)
        {
            SetRefreshCookie(result.RefreshToken);
        }

        return Ok(ApiResponse.Ok(new AccessTokenDto(result.AccessToken.Token, result.AccessToken.ExpiresAt)));
    }

    [AllowWhenPasswordChangeRequired]
    [HttpPost("logout")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await userRepository.LogoutAsync(User.GetSessionId(), cancellationToken);
        DeleteRefreshCookie();

        return Ok(ApiResponse.Ok<object?>(null, "Sesión cerrada"));
    }

    [AllowWhenPasswordChangeRequired]
    [HttpGet("me")]
    [ProducesResponseType<ApiResponse<CurrentUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        UserWithRole user = await userRepository.GetCurrentAsync(User.GetUserId(), cancellationToken);

        return Ok(ApiResponse.Ok(user.Adapt<CurrentUserDto>()));
    }

    [AllowWhenPasswordChangeRequired]
    [HttpPost("change-password")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await userRepository.ChangePasswordAsync(
            User.GetUserId(),
            User.GetSessionId(),
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, "Contraseña actualizada"));
    }

    private void SetRefreshCookie(IssuedToken refreshToken) =>
        Response.Cookies.Append(RefreshCookieName, refreshToken.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = refreshToken.ExpiresAt,
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
        });
}
