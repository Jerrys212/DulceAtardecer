using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DulceAtardecer.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
public class ProductsController(IProductRepository productRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<List<AvailableProductDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailable([FromQuery] ProductQueryDto query, CancellationToken cancellationToken)
    {
        IReadOnlyList<AvailableProductDto> products = await productRepository.GetAvailableAsync(
            query.CategoryId,
            query.SubcategoryId,
            query.Search,
            cancellationToken);

        return Ok(ApiResponse.Ok(products));
    }

    [HttpGet("all")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<List<ProductDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] ProductAdminQueryDto query, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductDto> products = await productRepository.GetAllAsync(
            query.CategoryId,
            query.SubcategoryId,
            query.Search,
            query.IsActive,
            cancellationToken);

        return Ok(ApiResponse.Ok(products));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ProductDto product = await productRepository.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(product));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ProductDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductDto request, CancellationToken cancellationToken)
    {
        ProductDto product = await productRepository.CreateAsync(
            request.Name,
            request.Description,
            request.Price.GetValueOrDefault(),
            request.CategoryId.GetValueOrDefault(),
            request.SubcategoryId.GetValueOrDefault(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id, version = "1" },
            ApiResponse.Ok(product, "Producto creado"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateProductDto request, CancellationToken cancellationToken)
    {
        ProductDto product = await productRepository.UpdateAsync(
            id,
            request.Name,
            request.Description,
            request.Price.GetValueOrDefault(),
            request.CategoryId.GetValueOrDefault(),
            request.SubcategoryId.GetValueOrDefault(),
            cancellationToken);

        return Ok(ApiResponse.Ok(product, "Producto actualizado"));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await productRepository.SetActiveAsync(id, true, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Producto activado" : "El producto ya estaba activo"));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        bool changed = await productRepository.SetActiveAsync(id, false, cancellationToken);

        return Ok(ApiResponse.Ok<object?>(null, changed ? "Producto desactivado" : "El producto ya estaba inactivo"));
    }
}
