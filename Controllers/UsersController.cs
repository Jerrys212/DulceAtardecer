using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DulceAtardecer.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
public class UsersController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<UserDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll([FromQuery] UserQueryDto query, CancellationToken cancellationToken)
    {
        PagedResult<UserWithRole> users = await userRepository.GetPagedAsync(
            query.IsActive,
            query.Role,
            query.Search,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(ApiResponse.Ok(new PagedResult<UserDto>(
            users.Items.Adapt<List<UserDto>>(),
            users.Page,
            users.PageSize,
            users.TotalCount)));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<ApiResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        UserWithRole user = await userRepository.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(user.Adapt<UserDto>()));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<CreatedUserDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateUserDto request, CancellationToken cancellationToken)
    {
        (UserWithRole user, string temporaryPassword) = await userRepository.CreateAsync(
            request.Username,
            request.FullName,
            request.Role,
            cancellationToken);

        UserDto userDto = user.Adapt<UserDto>();

        return CreatedAtAction(
            nameof(GetById),
            new { id = userDto.Id, version = "1" },
            ApiResponse.Ok(new CreatedUserDto(userDto, temporaryPassword), "Usuario creado", StatusCodes.Status201Created));
    }

    [HttpPut("{id}")]
    [ProducesResponseType<ApiResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, UpdateUserDto request, CancellationToken cancellationToken)
    {
        UserWithRole user = await userRepository.UpdateAsync(
            id,
            request.FullName,
            request.Role,
            User.GetUserId(),
            cancellationToken);

        return Ok(ApiResponse.Ok(user.Adapt<UserDto>(), "Usuario actualizado"));
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        await userRepository.DeactivateAsync(id, User.GetUserId(), cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, "Usuario desactivado"));
    }

    [HttpPost("{id}/reactivate")]
    [ProducesResponseType<ApiResponse<TemporaryPasswordDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(string id, CancellationToken cancellationToken)
    {
        string temporaryPassword = await userRepository.ReactivateAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(new TemporaryPasswordDto(temporaryPassword), "Usuario reactivado"));
    }

    [HttpPost("{id}/reset-password")]
    [ProducesResponseType<ApiResponse<TemporaryPasswordDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        string temporaryPassword = await userRepository.ResetPasswordAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(new TemporaryPasswordDto(temporaryPassword), "Contraseña restablecida"));
    }

    [HttpGet("{id}/sessions")]
    [ProducesResponseType<ApiResponse<List<SessionDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessions(string id, CancellationToken cancellationToken)
    {
        IReadOnlyList<Session> sessions = await userRepository.GetActiveSessionsAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(sessions.Adapt<List<SessionDto>>()));
    }

    [HttpPost("{id}/sessions/revoke")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSessions(string id, CancellationToken cancellationToken)
    {
        await userRepository.RevokeSessionsAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, "Sesiones revocadas"));
    }
}
