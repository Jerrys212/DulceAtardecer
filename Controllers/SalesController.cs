using System.ComponentModel.DataAnnotations;
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
[Authorize]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
public class SalesController(ISaleRepository saleRepository) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    [HttpGet("pending")]
    [Authorize(Roles = Permissions.OrdersViewPending)]
    [ProducesResponseType<ApiResponse<List<SaleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        IReadOnlyList<Sale> sales = await saleRepository.GetPendingAsync(cancellationToken);

        return Ok(ApiResponse.Ok(sales.Adapt<List<SaleDto>>()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Permissions.SalesCreate},{Permissions.OrdersViewPending}")]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Sale sale = await saleRepository.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse.Ok(sale.Adapt<SaleDto>()));
    }

    [HttpPost]
    [Authorize(Roles = Permissions.SalesCreate)]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromHeader(Name = IdempotencyKeyHeader)]
        [Required(ErrorMessage = "El header Idempotency-Key es obligatorio")]
        [TrimmedLength(1, 100, ErrorMessage = "El header Idempotency-Key debe tener entre 1 y 100 caracteres")]
        string idempotencyKey,
        CreateSaleDto request,
        CancellationToken cancellationToken)
    {
        (Sale sale, bool created) = await saleRepository.CreateAsync(
            User.GetUserId(),
            idempotencyKey,
            request.CustomerDescription,
            request.ExpectedTotal.GetValueOrDefault(),
            request.Items.Adapt<List<SaleLine>>(),
            cancellationToken);

        if (!created)
        {
            return Ok(ApiResponse.Ok(sale.Adapt<SaleDto>(), "La venta ya estaba registrada"));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = sale.Id, version = "1" },
            ApiResponse.Ok(sale.Adapt<SaleDto>(), "Venta registrada"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Permissions.SalesCreate)]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateSaleDto request, CancellationToken cancellationToken)
    {
        Sale sale = await saleRepository.UpdateAsync(
            id,
            User.GetUserId(),
            request.CustomerDescription,
            request.ExpectedTotal.GetValueOrDefault(),
            request.Items.Adapt<List<SaleLine>>(),
            cancellationToken);

        return Ok(ApiResponse.Ok(sale.Adapt<SaleDto>(), "Venta actualizada"));
    }

    [HttpPost("{id:guid}/pay")]
    [Authorize(Roles = Permissions.SalesCharge)]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(Guid id, PaySaleDto request, CancellationToken cancellationToken)
    {
        Sale sale = await saleRepository.PayAsync(
            id,
            User.GetUserId(),
            request.PaymentMethod.GetValueOrDefault(),
            cancellationToken);

        return Ok(ApiResponse.Ok(sale.Adapt<SaleDto>(), "Venta cobrada"));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Permissions.SalesCancelUnpaid)]
    [ProducesResponseType<ApiResponse<SaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancelSaleDto request, CancellationToken cancellationToken)
    {
        Sale sale = await saleRepository.CancelAsync(
            id,
            User.GetUserId(),
            request.Reason,
            User.IsInRole(Permissions.SalesCancelPaid),
            cancellationToken);

        return Ok(ApiResponse.Ok(sale.Adapt<SaleDto>(), "Venta cancelada"));
    }
}
