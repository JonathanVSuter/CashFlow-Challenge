using CashFlow.Api.Contracts;
using CashFlow.Application.Abstractions.Messaging;
using CashFlow.Application.Common.Models;
using CashFlow.Application.Features.CashEntries.Commands.CreateCashEntry;
using CashFlow.Application.Features.CashEntries.Queries.GetCashEntries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("api/entries")]
public sealed class EntriesController : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] ICommandHandler<CreateCashEntryCommand, CashEntryDto> handler,
        [FromBody] CreateCashEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await handler.HandleAsync(new CreateCashEntryCommand(request.Type, request.Amount, request.Description, request.OccurredAt), cancellationToken);
            return Created($"/api/entries/{response.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetCashEntriesQuery, IReadOnlyCollection<CashEntryDto>> handler,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        return Ok(await handler.HandleAsync(new GetCashEntriesQuery(date), cancellationToken));
    }
}
