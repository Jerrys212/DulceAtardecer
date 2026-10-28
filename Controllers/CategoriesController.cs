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
public class CategoriesController(ICategoryRepository categoryRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<List<ActiveCategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailable(CancellationToken cancellationToken)
    {
        IReadOnlyList<Category> categories = await categoryRepository.GetAvailableAsync(cancellationToken);

        return Ok(ApiResponse.Ok(categories.Adapt<List<ActiveCategoryDto>>()));
    }

    [HttpGet("all")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<List<CategoryDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<Category> categories = await categoryRepository.GetAllAsync(cancellationToken);

        return Ok(ApiResponse.Ok(categories.Adapt<List<CategoryDto>>()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Category category = await categoryRepository.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(category.Adapt<CategoryDto>()));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCategoryDto request, CancellationToken cancellationToken)
    {
        Category category = await categoryRepository.CreateAsync(
            request.Name,
            request.Subcategories ?? [],
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id, version = "1" },
            ApiResponse.Ok(category.Adapt<CategoryDto>(), "Categoría creada"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateCategoryDto request, CancellationToken cancellationToken)
    {
        Category category = await categoryRepository.UpdateAsync(id, request.Name, cancellationToken);

        return Ok(ApiResponse.Ok(category.Adapt<CategoryDto>(), "Categoría actualizada"));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await categoryRepository.SetActiveAsync(id, true, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Categoría activada" : "La categoría ya estaba activa"));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await categoryRepository.SetActiveAsync(id, false, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Categoría desactivada" : "La categoría ya estaba inactiva"));
    }

    [HttpPost("{id:guid}/subcategories")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<SubcategoryDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSubcategory(
        Guid id,
        CreateSubcategoryDto request,
        CancellationToken cancellationToken)
    {
        Subcategory subcategory = await categoryRepository.CreateSubcategoryAsync(id, request.Name, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id, version = "1" },
            ApiResponse.Ok(subcategory.Adapt<SubcategoryDto>(), "Subcategoría creada"));
    }

    [HttpPut("{id:guid}/subcategories/{subcategoryId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<SubcategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSubcategory(
        Guid id,
        Guid subcategoryId,
        UpdateSubcategoryDto request,
        CancellationToken cancellationToken)
    {
        Subcategory subcategory = await categoryRepository.UpdateSubcategoryAsync(
            id,
            subcategoryId,
            request.Name,
            cancellationToken);

        return Ok(ApiResponse.Ok(subcategory.Adapt<SubcategoryDto>(), "Subcategoría actualizada"));
    }

    [HttpPost("{id:guid}/subcategories/{subcategoryId:guid}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateSubcategory(Guid id, Guid subcategoryId, CancellationToken cancellationToken)
    {
        bool changed = await categoryRepository.SetSubcategoryActiveAsync(id, subcategoryId, true, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Subcategoría activada" : "La subcategoría ya estaba activa"));
    }

    [HttpPost("{id:guid}/subcategories/{subcategoryId:guid}/deactivate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateSubcategory(Guid id, Guid subcategoryId, CancellationToken cancellationToken)
    {
        bool changed = await categoryRepository.SetSubcategoryActiveAsync(id, subcategoryId, false, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Subcategoría desactivada" : "La subcategoría ya estaba inactiva"));
    }
}
