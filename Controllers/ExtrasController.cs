using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DulceAtardecer.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
public class ExtrasController(IExtraRepository extraRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<List<ActiveExtraDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        IReadOnlyList<Extra> extras = await extraRepository.GetActiveAsync(cancellationToken);

        return Ok(ApiResponse.Ok(extras.Adapt<List<ActiveExtraDto>>()));
    }

    [HttpGet("all")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<List<ExtraDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<Extra> extras = await extraRepository.GetAllAsync(cancellationToken);

        return Ok(ApiResponse.Ok(extras.Adapt<List<ExtraDto>>()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ExtraDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Extra extra = await extraRepository.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(extra.Adapt<ExtraDto>()));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ExtraDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateExtraDto request, CancellationToken cancellationToken)
    {
        Extra extra = await extraRepository.CreateAsync(
            request.Name,
            request.Price.GetValueOrDefault(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = extra.Id, version = "1" },
            ApiResponse.Ok(extra.Adapt<ExtraDto>(), "Extra creado"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ExtraDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateExtraDto request, CancellationToken cancellationToken)
    {
        Extra extra = await extraRepository.UpdateAsync(
            id,
            request.Name,
            request.Price.GetValueOrDefault(),
            cancellationToken);

        return Ok(ApiResponse.Ok(extra.Adapt<ExtraDto>(), "Extra actualizado"));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await extraRepository.SetActiveAsync(id, true, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Extra activado" : "El extra ya estaba activo"));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await extraRepository.SetActiveAsync(id, false, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Extra desactivado" : "El extra ya estaba inactivo"));
    }
}
